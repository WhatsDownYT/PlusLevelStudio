using System;
using UnityEngine;
using UnityEngine.UI;

namespace PlusLevelStudio.Editor.Tools
{
    /// <summary>
    /// A tool that selects cells and visuals by dragging a rectangle in either screen space or grid space.
    /// </summary>
    public class SelectionTool : EditorTool
    {
        private readonly SelectionToolMode mode;
        private readonly SelectionDragVisual dragVisual = new SelectionDragVisual();
        private bool dragging;
        private bool additiveDrag;
        private bool subtractiveDrag;
        private Vector2 screenStart;
        private IntVector2 gridStart;
        private EditorSelectionSnapshot currentSnapshot = new EditorSelectionSnapshot();

        /// <summary>
        /// Creates a new selection tool for the specified selection mode.
        /// </summary>
        /// <param name="mode">Whether this tool uses screen coordinates or level grid coordinates.</param>
        public SelectionTool(SelectionToolMode mode)
        {
            this.mode = mode;
            sprite = LevelStudioPlugin.Instance.uiAssetMan.Get<Sprite>(mode == SelectionToolMode.Screen ? "Tools/select_screen" : "Tools/select_grid");
        }

        /// <summary>
        /// The ID for this selection tool.
        /// </summary>
        public override string id => mode == SelectionToolMode.Screen ? "select_screen" : "select_grid";

        /// <summary>
        /// Selection should stick around when switching between selection tools.
        /// </summary>
        public override bool preservesEditorSelection => true;

        /// <summary>
        /// Selection drags need to start even when the cursor is on top of an editor interactable.
        /// </summary>
        public override bool allowsInteractableClicking => false;

        /// <summary>
        /// Resets drag state when the tool is equipped.
        /// </summary>
        public override void Begin()
        {
            dragging = false;
            additiveDrag = false;
            subtractiveDrag = false;
            SetCursorTint(Color.white);
        }

        /// <summary>
        /// Cleans up the active drag preview and cursor tint when the tool is put away.
        /// </summary>
        public override void Exit()
        {
            EndDragPreview();
            SetCursorTint(Color.white);
        }

        /// <summary>
        /// Cancels the active rectangle drag, or lets the editor put the tool away if no drag is active.
        /// </summary>
        /// <returns>Whether the tool should be put away.</returns>
        public override bool Cancelled()
        {
            if (dragging)
            {
                EndDragPreview();
                return false;
            }
            return true;
        }

        /// <summary>
        /// Starts a selection drag and records whether this drag is replacing, adding, or removing selection.
        /// </summary>
        /// <returns>False, since selection tools stay active while the mouse is held.</returns>
        public override bool MousePressed()
        {
            dragging = true;
            subtractiveDrag = IsAltHeld();
            additiveDrag = !subtractiveDrag && IsShiftHeld();
            currentSnapshot = new EditorSelectionSnapshot();

            if (!additiveDrag && !subtractiveDrag)
            {
                EditorController.Instance.selectionManager.ClearSelection();
            }

            if (mode == SelectionToolMode.Screen)
            {
                screenStart = CursorController.Instance.LocalPosition;
            }
            else
            {
                gridStart = EditorController.Instance.mouseGridPosition;
            }

            UpdateDragPreview();
            return false;
        }

        /// <summary>
        /// Applies the current drag preview to the persistent selection.
        /// </summary>
        /// <returns>Whether the tool should be put away after releasing the mouse.</returns>
        public override bool MouseReleased()
        {
            if (!dragging)
            {
                return false;
            }

            UpdateDragPreview();
            EditorController.Instance.selectionManager.ClearPreview();
            dragVisual.Hide();
            dragging = false;

            if (subtractiveDrag)
            {
                EditorController.Instance.selectionManager.RemoveSelection(currentSnapshot);
            }
            else
            {
                EditorController.Instance.selectionManager.AddSelection(currentSnapshot);
            }

            bool keepTool = additiveDrag || subtractiveDrag;
            additiveDrag = false;
            subtractiveDrag = false;
            SetCursorTint(keepTool && IsAltHeld() ? Color.red : Color.white);
            return !keepTool;
        }

        /// <summary>
        /// Updates cursor tint and the live rectangle preview.
        /// </summary>
        public override void Update()
        {
            SetCursorTint(IsAltHeld() || subtractiveDrag ? Color.red : Color.white);
            if (dragging)
            {
                UpdateDragPreview();
            }
        }

        private void EndDragPreview()
        {
            if (!dragging)
            {
                return;
            }
            EditorController.Instance.selectionManager.ClearPreview();
            dragVisual.Hide();
            dragging = false;
            additiveDrag = false;
            subtractiveDrag = false;
        }

        private void UpdateDragPreview()
        {
            Color color = subtractiveDrag ? Color.red : Color.blue;
            string highlight = subtractiveDrag ? "red" : "blue";

            // The visual rectangle and the hit-test both use the same coordinate mode, but the final snapshot is always in editor data.
            if (mode == SelectionToolMode.Screen)
            {
                Rect rect = GetScreenRect();
                dragVisual.ShowScreen(rect, color);
                currentSnapshot = EditorController.Instance.selectionManager.CreateFromScreenRect(rect);
            }
            else
            {
                RectInt rect = GetGridRect();
                RectInt clampedRect = EditorController.Instance.selectionManager.ClampGridRect(rect);
                dragVisual.ShowGrid(clampedRect, color);
                currentSnapshot = EditorController.Instance.selectionManager.CreateFromGridRect(clampedRect);
            }
            EditorController.Instance.selectionManager.PreviewSelection(currentSnapshot, highlight);
        }

        private Rect GetScreenRect()
        {
            Vector2 current = CursorController.Instance.LocalPosition;
            return Rect.MinMaxRect(Mathf.Min(screenStart.x, current.x), Mathf.Min(screenStart.y, current.y), Mathf.Max(screenStart.x, current.x), Mathf.Max(screenStart.y, current.y));
        }

        private RectInt GetGridRect()
        {
            return gridStart.ToUnityVector().ToRect(EditorController.Instance.mouseGridPosition.ToUnityVector());
        }

        private static bool IsShiftHeld()
        {
            return Input.GetKey(KeyCode.LeftShift);
        }

        private static bool IsAltHeld()
        {
            return Input.GetKey(KeyCode.LeftAlt);
        }

        private void SetCursorTint(Color color)
        {
            EditorCursorController cursor = CursorController.Instance as EditorCursorController;
            if (cursor != null)
            {
                cursor.SetIconColor(color);
            }
        }
    }

    /// <summary>
    /// The coordinate space a selection drag is performed in.
    /// </summary>
    public enum SelectionToolMode
    {
        /// <summary>
        /// Selects anything overlapped by the rectangle on the editor screen.
        /// </summary>
        Screen,

        /// <summary>
        /// Selects anything overlapped by the rectangle on the level grid.
        /// </summary>
        Grid
    }

    /// <summary>
    /// Handles the temporary blue or red rectangle shown while a selection drag is active.
    /// </summary>
    internal class SelectionDragVisual
    {
        private GameObject screenObject;
        private RectTransform screenRect;
        private Image screenFill;
        private Image[] screenBorders;
        private GameObject gridObject;
        private Mesh gridFillMesh;
        private Mesh gridBorderMesh;
        private MeshFilter gridFillFilter;
        private MeshFilter gridBorderFilter;
        private Material gridFillMaterial;
        private Material gridBorderMaterial;
        private const float GridCellSize = 10f;
        private const float GridFillYOffset = 0.025f;
        private const float GridBorderYOffset = 0.035f;
        private const float GridBorderWidth = 0.25f;

        public void ShowScreen(Rect rect, Color color)
        {
            EnsureScreenObject();
            HideGrid();
            screenObject.SetActive(rect.width > 0f && rect.height > 0f);
            if (!screenObject.activeSelf)
            {
                return;
            }

            screenRect.anchoredPosition = new Vector2(rect.xMin, rect.yMax);
            screenRect.sizeDelta = new Vector2(rect.width, rect.height);
            screenFill.color = Transparent(color);
            for (int i = 0; i < screenBorders.Length; i++)
            {
                screenBorders[i].color = Opaque(color);
            }
        }

        public void ShowGrid(RectInt rect, Color color)
        {
            EnsureGridObject();
            ResetGridTransform();
            HideScreen();
            gridObject.SetActive(rect.width > 0 && rect.height > 0);
            if (!gridObject.activeSelf)
            {
                return;
            }

            Color fillColor = Transparent(color);
            Color borderColor = Opaque(color);
            SetMaterialColor(gridFillMaterial, fillColor);
            SetMaterialColor(gridBorderMaterial, borderColor);

            UpdateGridFill(rect, EditorController.Instance.gridManager.Height + GridFillYOffset);
            UpdateGridBorder(rect, EditorController.Instance.gridManager.Height + GridBorderYOffset);
        }

        public void Hide()
        {
            HideScreen();
            HideGrid();
        }

        private void HideScreen()
        {
            if (screenObject != null)
            {
                screenObject.SetActive(false);
            }
        }

        private void HideGrid()
        {
            if (gridObject != null)
            {
                gridObject.SetActive(false);
            }
        }

        private void EnsureScreenObject()
        {
            if (screenObject != null)
            {
                return;
            }

            screenObject = new GameObject("SelectionScreenRectangle");
            screenObject.transform.SetParent(EditorController.Instance.canvas.transform, false);
            screenRect = screenObject.AddComponent<RectTransform>();
            screenRect.anchorMin = new Vector2(0f, 1f);
            screenRect.anchorMax = new Vector2(0f, 1f);
            screenRect.pivot = new Vector2(0f, 1f);
            screenFill = screenObject.AddComponent<Image>();
            screenFill.raycastTarget = false;
            screenBorders = new Image[4];

            // Keep the fill as the root image and create thin child images for the border.
            CreateScreenBorder(0, "Bottom", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
            CreateScreenBorder(1, "Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 2f));
            CreateScreenBorder(2, "Left", new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(2f, 0f));
            CreateScreenBorder(3, "Right", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(2f, 0f));
            CursorController.Instance.transform.SetAsLastSibling();
            EditorController.Instance.tooltipBase.transform.SetAsLastSibling();
        }

        private void CreateScreenBorder(int index, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            GameObject borderObject = new GameObject(name);
            borderObject.transform.SetParent(screenObject.transform, false);
            Image border = borderObject.AddComponent<Image>();
            border.raycastTarget = false;
            border.rectTransform.anchorMin = anchorMin;
            border.rectTransform.anchorMax = anchorMax;
            border.rectTransform.pivot = pivot;
            border.rectTransform.sizeDelta = size;
            border.rectTransform.anchoredPosition = Vector2.zero;
            screenBorders[index] = border;
        }

        private void EnsureGridObject()
        {
            if (gridObject != null)
            {
                return;
            }

            gridObject = new GameObject("SelectionGridRectangle");
            ResetGridTransform();
            gridFillMaterial = CreateGridMaterial("SelectionGridFill", Transparent(Color.blue));
            gridBorderMaterial = CreateGridMaterial("SelectionGridBorder", Opaque(Color.blue));

            GameObject fillObject = CreateGridMeshObject("Fill", gridFillMaterial, out gridFillFilter);
            fillObject.transform.SetParent(gridObject.transform, false);
            gridFillMesh = new Mesh();
            gridFillMesh.name = "SelectionGridFillMesh";
            gridFillMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            gridFillFilter.sharedMesh = gridFillMesh;

            GameObject borderObject = CreateGridMeshObject("Border", gridBorderMaterial, out gridBorderFilter);
            borderObject.transform.SetParent(gridObject.transform, false);
            gridBorderMesh = new Mesh();
            gridBorderMesh.name = "SelectionGridBorderMesh";
            gridBorderFilter.sharedMesh = gridBorderMesh;
        }

        private void ResetGridTransform()
        {
            // The editor controller transform is also used as the camera target, so keep this mesh in world space.
            gridObject.transform.SetParent(null, false);
            gridObject.transform.position = Vector3.zero;
            gridObject.transform.rotation = Quaternion.identity;
            gridObject.transform.localScale = Vector3.one;

            if (gridFillFilter != null)
            {
                ResetLocalTransform(gridFillFilter.transform);
            }
            if (gridBorderFilter != null)
            {
                ResetLocalTransform(gridBorderFilter.transform);
            }
        }

        private void ResetLocalTransform(Transform target)
        {
            target.localPosition = Vector3.zero;
            target.localRotation = Quaternion.identity;
            target.localScale = Vector3.one;
        }

        private GameObject CreateGridMeshObject(string name, Material material, out MeshFilter filter)
        {
            GameObject meshObject = new GameObject(name);
            filter = meshObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
            renderer.material = material;
            return meshObject;
        }

        private void UpdateGridFill(RectInt rect, float y)
        {
            int cellCount = rect.width * rect.height;
            Vector3[] vertices = new Vector3[cellCount * 4];
            Vector2[] uvs = new Vector2[cellCount * 4];
            int[] triangles = new int[cellCount * 6];

            int vertexIndex = 0;
            int triangleIndex = 0;

            // Build one quad per cell so the dithered grid preview behaves like the editor grid instead of one stretched plane.
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                for (int z = rect.yMin; z < rect.yMax; z++)
                {
                    AddGridQuad(
                        vertices,
                        uvs,
                        triangles,
                        ref vertexIndex,
                        ref triangleIndex,
                        x * GridCellSize,
                        z * GridCellSize,
                        GridCellSize,
                        GridCellSize,
                        y);
                }
            }

            gridFillMesh.Clear();
            gridFillMesh.vertices = vertices;
            gridFillMesh.uv = uvs;
            gridFillMesh.triangles = triangles;
            gridFillMesh.RecalculateBounds();
            gridFillMesh.RecalculateNormals();
        }

        private void UpdateGridBorder(RectInt rect, float y)
        {
            float xMin = rect.xMin * GridCellSize;
            float xMax = rect.xMax * GridCellSize;
            float zMin = rect.yMin * GridCellSize;
            float zMax = rect.yMax * GridCellSize;
            float width = xMax - xMin;
            float height = zMax - zMin;
            float innerHeight = Mathf.Max(0f, height - (GridBorderWidth * 2f));

            Vector3[] vertices = new Vector3[16];
            Vector2[] uvs = new Vector2[16];
            int[] triangles = new int[24];
            int vertexIndex = 0;
            int triangleIndex = 0;

            AddGridQuad(vertices, uvs, triangles, ref vertexIndex, ref triangleIndex, xMin, zMin, width, GridBorderWidth, y);
            AddGridQuad(vertices, uvs, triangles, ref vertexIndex, ref triangleIndex, xMin, zMax - GridBorderWidth, width, GridBorderWidth, y);
            AddGridQuad(vertices, uvs, triangles, ref vertexIndex, ref triangleIndex, xMin, zMin + GridBorderWidth, GridBorderWidth, innerHeight, y);
            AddGridQuad(vertices, uvs, triangles, ref vertexIndex, ref triangleIndex, xMax - GridBorderWidth, zMin + GridBorderWidth, GridBorderWidth, innerHeight, y);

            gridBorderMesh.Clear();
            gridBorderMesh.vertices = vertices;
            gridBorderMesh.uv = uvs;
            gridBorderMesh.triangles = triangles;
            gridBorderMesh.RecalculateBounds();
            gridBorderMesh.RecalculateNormals();
        }

        private void AddGridQuad(Vector3[] vertices, Vector2[] uvs, int[] triangles, ref int vertexIndex, ref int triangleIndex, float x, float z, float width, float height, float y)
        {
            vertices[vertexIndex] = new Vector3(x, y, z);
            vertices[vertexIndex + 1] = new Vector3(x + width, y, z);
            vertices[vertexIndex + 2] = new Vector3(x, y, z + height);
            vertices[vertexIndex + 3] = new Vector3(x + width, y, z + height);

            uvs[vertexIndex] = Vector2.zero;
            uvs[vertexIndex + 1] = Vector2.right;
            uvs[vertexIndex + 2] = Vector2.up;
            uvs[vertexIndex + 3] = Vector2.one;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 2;
            triangles[triangleIndex + 2] = vertexIndex + 1;
            triangles[triangleIndex + 3] = vertexIndex + 2;
            triangles[triangleIndex + 4] = vertexIndex + 3;
            triangles[triangleIndex + 5] = vertexIndex + 1;

            vertexIndex += 4;
            triangleIndex += 6;
        }

        private Material CreateGridMaterial(string name, Color color)
        {
            Material material = new Material(LevelStudioPlugin.Instance.assetMan.Get<Material>("tileAlpha"));
            material.name = name;
            material.mainTexture = CreateColorTexture(color);
            material.SetTexture("_LightMap", LevelStudioPlugin.Instance.lightmaps["white"]);
            return material;
        }

        private Texture2D CreateColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void SetMaterialColor(Material material, Color color)
        {
            Texture2D texture = material.mainTexture as Texture2D;
            if (texture == null)
            {
                material.mainTexture = CreateColorTexture(color);
                return;
            }
            texture.SetPixel(0, 0, color);
            texture.Apply();
        }

        private Color Transparent(Color color)
        {
            return new Color(color.r, color.g, color.b, 0.25f);
        }

        private Color Opaque(Color color)
        {
            return new Color(color.r, color.g, color.b, 0.95f);
        }
    }
}
