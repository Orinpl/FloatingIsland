using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using UnityEngine;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 把占地掩码画成一张小贴图，给手牌按钮当形状图标用。
    ///
    /// 手牌只写名字的话，2×2 居民区、L 形居民区、凹形居民区在按钮上完全一样——
    /// 名字能区分「是哪一种」，形状图标才能区分「摆下去占多大、缺哪个角」。
    /// 掩码是配表产物、数量有限（每个变体一张），按变体 Id 缓存，一局最多几十张 64×64 以内的贴图。
    /// </summary>
    public static class FootprintIcon
    {
        /// <summary>每格画多少像素（含缝）。够大才能让 Point 采样放大后边缘依旧干净。</summary>
        private const int CellPixels = 16;

        /// <summary>格与格之间留的缝宽（像素），画出来就是网格线。</summary>
        private const int Gap = 2;

        private static readonly Color Solid = new Color(0.14f, 0.20f, 0.32f, 1f);
        private static readonly Color Empty = new Color(0f, 0f, 0f, 0f);

        private static readonly Dictionary<string, Texture2D> Cache =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        private static readonly List<CellCoord> Scratch = new List<CellCoord>();

        /// <summary>
        /// 取某个变体的形状图标。<paramref name="key"/> 用变体 Id（同一变体的掩码不会变，可以一直复用）。
        /// </summary>
        public static Texture2D Get(string key, Footprint footprint)
        {
            if (footprint == null || string.IsNullOrEmpty(key))
            {
                return null;
            }

            Texture2D texture;
            if (Cache.TryGetValue(key, out texture) && texture != null)
            {
                return texture;
            }

            texture = Render(footprint);
            Cache[key] = texture;
            return texture;
        }

        private static Texture2D Render(Footprint footprint)
        {
            int cols = footprint.Columns;
            int rows = footprint.Rows;
            int width = cols * CellPixels;
            int height = rows * CellPixels;

            var pixels = new Color[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Empty;
            }

            // 掩码的 dz 是「自下而上」的行号，贴图 y 也是自下而上，两者直接对应，不用翻转
            Scratch.Clear();
            footprint.GetCells(0, 0, Rotation.Deg0, Scratch);
            for (int i = 0; i < Scratch.Count; i++)
            {
                CellCoord cell = Scratch[i];
                int x0 = cell.X * CellPixels + Gap;
                int y0 = cell.Z * CellPixels + Gap;
                int span = CellPixels - Gap * 2;

                for (int y = y0; y < y0 + span; y++)
                {
                    int row = y * width;
                    for (int x = x0; x < x0 + span; x++)
                    {
                        pixels[row + x] = Solid;
                    }
                }
            }

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"FootprintIcon_{cols}x{rows}",
                filterMode = FilterMode.Point, // 放大到按钮尺寸时保持方块边缘锐利
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
