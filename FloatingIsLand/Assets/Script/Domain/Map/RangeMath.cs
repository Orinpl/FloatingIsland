using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Map
{
    /// <summary>
    /// 影响范围判定（GAME_DESIGN 决策 26）：范围是**球形**，用三维欧氏距离判定，
    /// 距离 = √(水平格距² + 层差²)。层差有两种口径：
    ///   · 地图配了逐层高度（<see cref="MapSnapshot.LayerHeights"/>）→ 用真实高度差换算的格数
    ///     （<paramref name="layerYGrids"/>，= 高度差 ÷ 格边长），画面对齐台地后计分跟着对；
    ///   · 没配（老地图）→ 层差 × 统一系数 k，k = GameConfig.layerHeightFactor。
    /// 早期文档里的「平面切比雪夫」口径已废弃，配表注释以本文件为准。
    ///
    /// 半径「自占地边缘起算」：两个多格物体之间取各自占用格的最小距离，
    /// 所以斜向相邻一格 = √2，正交相邻一格 = 1。
    ///
    /// 注意风长度量不走这里——风长仍是切比雪夫（斜走一步也只吃一段长度，决策 22）。
    /// </summary>
    public static class RangeMath
    {
        /// <summary>两组占用格之间的最小三维距离（格）。任一组为空返回 <see cref="float.PositiveInfinity"/>。</summary>
        /// <param name="layerYGrids">逐层高度表（格数，下标 = 层号）；null 或下标越界时退回统一系数口径。</param>
        public static float MinDistance(
            IReadOnlyList<CellCoord> a, int layerA,
            IReadOnlyList<CellCoord> b, int layerB,
            float layerHeightFactor,
            IReadOnlyList<float> layerYGrids = null)
        {
            if (a == null || b == null || a.Count == 0 || b.Count == 0)
            {
                return float.PositiveInfinity;
            }

            float dy;
            if (layerYGrids != null
                && layerA >= 0 && layerA < layerYGrids.Count
                && layerB >= 0 && layerB < layerYGrids.Count)
            {
                dy = layerYGrids[layerA] - layerYGrids[layerB];
            }
            else
            {
                dy = (layerA - layerB) * layerHeightFactor;
            }
            float dySquared = dy * dy;

            float best = float.PositiveInfinity;
            for (int i = 0; i < a.Count; i++)
            {
                CellCoord ca = a[i];
                for (int j = 0; j < b.Count; j++)
                {
                    CellCoord cb = b[j];
                    float dx = ca.X - cb.X;
                    float dz = ca.Z - cb.Z;
                    float squared = dx * dx + dz * dz + dySquared;
                    if (squared < best)
                    {
                        best = squared;
                    }
                }
            }

            return (float)Math.Sqrt(best);
        }

        /// <summary>b 是否落在以 a 为中心、半径 radius 的球形范围内（含边界）。</summary>
        public static bool InRange(
            IReadOnlyList<CellCoord> a, int layerA,
            IReadOnlyList<CellCoord> b, int layerB,
            int radius, float layerHeightFactor,
            IReadOnlyList<float> layerYGrids = null)
        {
            if (radius <= 0)
            {
                return false;
            }
            // 浮点比较放宽一点：半径 5 想包含 (3,4) 这种恰好等于 5 的整格距离，
            // 不留容差会被 sqrt 的舍入误差挡在门外。
            return MinDistance(a, layerA, b, layerB, layerHeightFactor, layerYGrids) <= radius + 1e-4f;
        }
    }
}
