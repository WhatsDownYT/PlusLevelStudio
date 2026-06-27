using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace PlusLevelStudio
{
    public class EditorCursorController : CursorController
    {
        public Image toolIcon;

        public void SetIcon(Sprite sprite)
        {
            if (sprite == null)
            {
                toolIcon.enabled = false;
                toolIcon.color = Color.white;
                return;
            }
            toolIcon.enabled = true;
            toolIcon.sprite = sprite;
            toolIcon.color = Color.white;
        }

        /// <summary>
        /// Sets the tint used by the active tool icon.
        /// </summary>
        /// <param name="color">The color to tint the icon with.</param>
        public void SetIconColor(Color color)
        {
            toolIcon.color = color;
        }
    }
}
