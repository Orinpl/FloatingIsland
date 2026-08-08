using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public abstract class Grid
    {
        public abstract void SetRuntimeObjectGridGeneratedTextureCellToDefault(Vector2Int cellPosition);
        public abstract void SetRuntimeObjectGridGeneratedTextureCellColor(Vector2Int cellPosition, Color color);

        public abstract void SetRuntimeObjectGridHeatMapActiveSelf(bool toggleMode);
        public abstract void SetRuntimeObjectGridHeatMapActiveSelfToggle(out bool toggleMode);
        public abstract void SetRuntimeObjectGridHeatMapTexture(Texture2D generatedTexture, Color overrideHDRColor);
    }
}