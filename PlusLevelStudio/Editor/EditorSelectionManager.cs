using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlusLevelStudio.Editor
{
    /// <summary>
    /// Tracks the editor's multi-select state, and applies the related cell and visual highlights.
    /// </summary>
    public class EditorSelectionManager
    {
        private readonly EditorController editor;
        private readonly HashSet<Vector2Int> selectedCells = new HashSet<Vector2Int>();
        private readonly HashSet<IEditorVisualizable> selectedVisuals = new HashSet<IEditorVisualizable>();
        private readonly HashSet<Vector2Int> previewCells = new HashSet<Vector2Int>();
        private readonly HashSet<IEditorVisualizable> previewVisuals = new HashSet<IEditorVisualizable>();

        /// <summary>
        /// Creates a new selection manager tied to the specified editor controller.
        /// </summary>
        /// <param name="editor">The editor controller that owns the selection state.</param>
        public EditorSelectionManager(EditorController editor)
        {
            this.editor = editor;
        }

        /// <summary>
        /// Whether the editor currently has any selected cells or visual objects.
        /// </summary>
        public bool HasSelection => selectedCells.Count > 0 || selectedVisuals.Count > 0;

        /// <summary>
        /// Clears the committed selection and any active drag preview.
        /// </summary>
        public void ClearSelection()
        {
            ClearPreview();
            HighlightCells(selectedCells, "none");
            HighlightVisuals(selectedVisuals, "none");
            selectedCells.Clear();
            selectedVisuals.Clear();
        }

        /// <summary>
        /// Replaces the committed selection with the provided snapshot.
        /// </summary>
        /// <param name="snapshot">The selection snapshot to use.</param>
        public void SetSelection(EditorSelectionSnapshot snapshot)
        {
            ClearSelection();
            AddSelection(snapshot);
        }

        /// <summary>
        /// Adds the provided snapshot to the committed selection.
        /// </summary>
        /// <param name="snapshot">The selection snapshot to add.</param>
        public void AddSelection(EditorSelectionSnapshot snapshot)
        {
            foreach (Vector2Int cell in snapshot.Cells)
            {
                if (CellInBounds(cell))
                {
                    selectedCells.Add(cell);
                }
            }
            foreach (IEditorVisualizable visual in snapshot.Visuals)
            {
                if (editor.GetVisual(visual) != null)
                {
                    selectedVisuals.Add(visual);
                }
            }
            RefreshHighlights();
        }

        /// <summary>
        /// Removes the provided snapshot from the committed selection.
        /// </summary>
        /// <param name="snapshot">The selection snapshot to remove.</param>
        public void RemoveSelection(EditorSelectionSnapshot snapshot)
        {
            foreach (Vector2Int cell in snapshot.Cells)
            {
                if (selectedCells.Remove(cell) && CellInBounds(cell))
                {
                    editor.HighlightCells(new IntVector2[] { new IntVector2(cell.x, cell.y) }, "none");
                }
            }
            foreach (IEditorVisualizable visual in snapshot.Visuals)
            {
                if (selectedVisuals.Remove(visual))
                {
                    HighlightVisual(visual, "none");
                }
            }
            RefreshHighlights();
        }

        /// <summary>
        /// Applies a temporary preview highlight without changing the committed selection.
        /// </summary>
        /// <param name="snapshot">The current drag hit test result.</param>
        /// <param name="highlight">The lightmap highlight to use for the preview.</param>
        public void PreviewSelection(EditorSelectionSnapshot snapshot, string highlight)
        {
            ClearPreview();
            foreach (Vector2Int cell in snapshot.Cells)
            {
                if (CellInBounds(cell))
                {
                    previewCells.Add(cell);
                }
            }
            foreach (IEditorVisualizable visual in snapshot.Visuals)
            {
                if (editor.GetVisual(visual) != null)
                {
                    previewVisuals.Add(visual);
                }
            }
            HighlightCells(previewCells, highlight);
            HighlightVisuals(previewVisuals, highlight);
        }

        /// <summary>
        /// Clears the active drag preview and restores the committed selection highlight.
        /// </summary>
        public void ClearPreview()
        {
            if (previewCells.Count > 0)
            {
                foreach (Vector2Int cell in previewCells)
                {
                    if (!CellInBounds(cell))
                    {
                        continue;
                    }
                    editor.HighlightCells(new IntVector2[] { new IntVector2(cell.x, cell.y) }, selectedCells.Contains(cell) ? "blue" : "none");
                }
            }

            foreach (IEditorVisualizable visual in previewVisuals)
            {
                HighlightVisual(visual, selectedVisuals.Contains(visual) ? "blue" : "none");
            }
            previewCells.Clear();
            previewVisuals.Clear();
        }

        /// <summary>
        /// Reapplies selection highlights after cells or visuals have been refreshed.
        /// </summary>
        public void RefreshHighlights()
        {
            HighlightCells(selectedCells, "blue");
            HighlightVisuals(selectedVisuals, "blue");
        }

        /// <summary>
        /// Removes a visualizable from selection tracking before its visual is destroyed.
        /// </summary>
        /// <param name="visualizable">The visualizable being removed from the editor.</param>
        public void RemoveVisualizable(IEditorVisualizable visualizable)
        {
            selectedVisuals.Remove(visualizable);
            previewVisuals.Remove(visualizable);
        }

        /// <summary>
        /// Creates a selection snapshot by testing cells and visuals against an editor screen rectangle.
        /// </summary>
        /// <param name="rect">The rectangle in editor screen coordinates.</param>
        /// <returns>The cells and visuals overlapped by the rectangle.</returns>
        public EditorSelectionSnapshot CreateFromScreenRect(Rect rect)
        {
            EditorSelectionSnapshot snapshot = new EditorSelectionSnapshot();
            if (rect.width <= 0f || rect.height <= 0f)
            {
                return snapshot;
            }

            for (int x = 0; x < editor.levelData.mapSize.x; x++)
            {
                for (int z = 0; z < editor.levelData.mapSize.z; z++)
                {
                    if (editor.levelData.cells[x, z].type == 16)
                    {
                        continue;
                    }
                    if (TryGetScreenBounds(GetCellBounds(x, z), out Rect cellRect) && rect.Overlaps(cellRect))
                    {
                        snapshot.Cells.Add(new Vector2Int(x, z));
                    }
                }
            }

            foreach (KeyValuePair<IEditorVisualizable, GameObject> visual in editor.objectVisuals)
            {
                if (!TryGetObjectBounds(visual.Value, out Bounds bounds))
                {
                    continue;
                }
                if (TryGetScreenBounds(bounds, out Rect visualRect) && rect.Overlaps(visualRect))
                {
                    snapshot.Visuals.Add(visual.Key);
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Creates a selection snapshot by testing cells and visuals against a grid rectangle.
        /// </summary>
        /// <param name="rect">The rectangle in level grid coordinates.</param>
        /// <returns>The cells and visuals overlapped by the rectangle.</returns>
        public EditorSelectionSnapshot CreateFromGridRect(RectInt rect)
        {
            EditorSelectionSnapshot snapshot = new EditorSelectionSnapshot();
            RectInt clampedRect = ClampGridRect(rect);
            if (clampedRect.width <= 0 || clampedRect.height <= 0)
            {
                return snapshot;
            }

            for (int x = clampedRect.xMin; x < clampedRect.xMax; x++)
            {
                for (int z = clampedRect.yMin; z < clampedRect.yMax; z++)
                {
                    if (editor.levelData.cells[x, z].type != 16)
                    {
                        snapshot.Cells.Add(new Vector2Int(x, z));
                    }
                }
            }

            Rect worldRect = GridRectToWorldRect(clampedRect);
            foreach (KeyValuePair<IEditorVisualizable, GameObject> visual in editor.objectVisuals)
            {
                if (!TryGetObjectBounds(visual.Value, out Bounds bounds))
                {
                    continue;
                }
                if (worldRect.Overlaps(BoundsToWorldXZRect(bounds)))
                {
                    snapshot.Visuals.Add(visual.Key);
                }
            }
            return snapshot;
        }

        /// <summary>
        /// Clamps a grid rectangle to the current level bounds.
        /// </summary>
        /// <param name="rect">The grid rectangle to clamp.</param>
        /// <returns>A rectangle that stays inside the level grid.</returns>
        public RectInt ClampGridRect(RectInt rect)
        {
            int xMin = Mathf.Clamp(rect.xMin, 0, editor.levelData.mapSize.x);
            int xMax = Mathf.Clamp(rect.xMax, 0, editor.levelData.mapSize.x);
            int yMin = Mathf.Clamp(rect.yMin, 0, editor.levelData.mapSize.z);
            int yMax = Mathf.Clamp(rect.yMax, 0, editor.levelData.mapSize.z);
            return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
        }

        private Bounds GetCellBounds(int x, int z)
        {
            return new Bounds(new Vector3((x * 10f) + 5f, editor.gridManager.Height, (z * 10f) + 5f), new Vector3(10f, 0.2f, 10f));
        }

        private Rect GridRectToWorldRect(RectInt rect)
        {
            return Rect.MinMaxRect(rect.xMin * 10f, rect.yMin * 10f, rect.xMax * 10f, rect.yMax * 10f);
        }

        private Rect BoundsToWorldXZRect(Bounds bounds)
        {
            return Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);
        }

        private bool TryGetScreenBounds(Bounds bounds, out Rect rect)
        {
            bool hasPoint = false;
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            // Project each corner of the world-space bounds, then turn the visible points into an editor-space rectangle
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 worldPoint = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        Vector3 screenPoint = editor.camera.camCom.WorldToScreenPoint(worldPoint);
                        if (screenPoint.z < 0f)
                        {
                            continue;
                        }
                        Vector2 editorPoint = ScreenPointToEditorPoint(screenPoint);
                        min = Vector2.Min(min, editorPoint);
                        max = Vector2.Max(max, editorPoint);
                        hasPoint = true;
                    }
                }
            }

            if (!hasPoint)
            {
                rect = new Rect();
                return false;
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        private Vector2 ScreenPointToEditorPoint(Vector3 screenPoint)
        {
            return new Vector2(screenPoint.x / Screen.width * editor.screenSize.x, (screenPoint.y - Screen.height) / Screen.height * editor.screenSize.y);
        }

        private bool TryGetObjectBounds(GameObject visual, out Bounds bounds)
        {
            bounds = new Bounds();
            if (visual == null || !visual.activeInHierarchy)
            {
                return false;
            }

            bool foundBounds = false;
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                {
                    continue;
                }
                if (!foundBounds)
                {
                    bounds = renderers[i].bounds;
                    foundBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            if (foundBounds)
            {
                return true;
            }

            Collider[] colliders = visual.GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].enabled)
                {
                    continue;
                }
                if (!foundBounds)
                {
                    bounds = colliders[i].bounds;
                    foundBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }

            if (foundBounds)
            {
                return true;
            }

            bounds = new Bounds(visual.transform.position, Vector3.one);
            return true;
        }

        private bool CellInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < editor.levelData.mapSize.x && cell.y < editor.levelData.mapSize.z;
        }

        private void HighlightCells(IEnumerable<Vector2Int> cells, string highlight)
        {
            List<IntVector2> positions = new List<IntVector2>();
            foreach (Vector2Int cell in cells)
            {
                if (CellInBounds(cell))
                {
                    positions.Add(new IntVector2(cell.x, cell.y));
                }
            }
            if (positions.Count > 0)
            {
                editor.HighlightCells(positions.ToArray(), highlight);
            }
        }

        private void HighlightVisuals(IEnumerable<IEditorVisualizable> visuals, string highlight)
        {
            foreach (IEditorVisualizable visual in visuals)
            {
                HighlightVisual(visual, highlight);
            }
        }

        private void HighlightVisual(IEditorVisualizable visualizable, string highlight)
        {
            GameObject visual = editor.GetVisual(visualizable);
            if (visual == null)
            {
                return;
            }

            EditorRendererContainer[] containers = visual.GetComponentsInChildren<EditorRendererContainer>();
            if (containers.Length > 0)
            {
                // Containers know their default highlight, so prefer them over raw renderer lightmap edits
                for (int i = 0; i < containers.Length; i++)
                {
                    containers[i].Highlight(highlight);
                }
                return;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].materials;
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j].SetTexture("_LightMap", LevelStudioPlugin.Instance.lightmaps[highlight]);
                }
            }
        }
    }

    /// <summary>
    /// A set of cells and visual objects found by one selection hit test.
    /// </summary>
    public class EditorSelectionSnapshot
    {
        /// <summary>
        /// Cells included in the snapshot.
        /// </summary>
        public HashSet<Vector2Int> Cells { get; private set; }

        /// <summary>
        /// Visual objects included in the snapshot.
        /// </summary>
        public HashSet<IEditorVisualizable> Visuals { get; private set; }

        /// <summary>
        /// Creates an empty selection snapshot.
        /// </summary>
        public EditorSelectionSnapshot()
        {
            Cells = new HashSet<Vector2Int>();
            Visuals = new HashSet<IEditorVisualizable>();
        }
    }
}
