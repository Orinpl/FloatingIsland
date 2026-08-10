using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 把一块占地拆成**若干个矩形**，供范围圆环的 shader 用一组圆角矩形 SDF 把作用范围画出来。
    ///
    /// 为什么不是"以建筑中心画个圆"：领域层的范围判定是
    /// <see cref="RangeMath"/>——**自占地边缘起算**的最小距离。6×6 的船坞，真实范围是
    /// 「占地矩形外扩 R」的圆角矩形，画成圆要么少画一大片，要么多画一大片，玩家会照着错的图规划。
    ///
    /// 精度上这不是近似，是**恰好相等**：占地格中心都在整数坐标上，拆出来的矩形边界也在整数坐标上，
    /// 而把一个整数点钳进整数边界的矩形，落点仍是整数——也就是某个占地格的中心。
    /// 所以「到矩形的距离」在所有格中心处都等于「到最近占地格中心的距离」，与 RangeMath 一字不差。
    /// RangeRingGeometryTests 对全部占地形状 × 四个朝向逐点比对过这条等价性。
    /// </summary>
    public static class RangeRingGeometry
    {
        /// <summary>shader 里 box 数组的长度。超出的占地退化成外接矩形（只会多画，不会少画）。</summary>
        public const int MaxBoxes = 8;

        /// <summary>格空间里的一个矩形：中心与半长都以「格」为单位，半长可以是 0（单格）或 0.5 的倍数。</summary>
        public readonly struct CellBox
        {
            public readonly float CenterX;
            public readonly float CenterZ;
            public readonly float HalfX;
            public readonly float HalfZ;

            public CellBox(float centerX, float centerZ, float halfX, float halfZ)
            {
                CenterX = centerX;
                CenterZ = centerZ;
                HalfX = halfX;
                HalfZ = halfZ;
            }
        }

        /// <summary>
        /// 把占用格合并成尽量少的矩形：先按行合并成横条，再把左右边界相同、上下相邻的横条并成矩形。
        /// 满矩形占地（绝大多数建筑）合成 1 个；L 形 / 凹形是 2~3 个。
        /// </summary>
        public static void BuildBoxes(IReadOnlyList<CellCoord> cells, List<CellBox> result)
        {
            result.Clear();
            if (cells == null || cells.Count == 0)
            {
                return;
            }

            // 1. 按 z 分行，行内按 x 排序后切成连续段
            var runs = new List<(int Z, int X0, int X1)>();
            var byRow = new Dictionary<int, List<int>>();
            for (int i = 0; i < cells.Count; i++)
            {
                CellCoord cell = cells[i];
                List<int> row;
                if (!byRow.TryGetValue(cell.Z, out row))
                {
                    row = new List<int>();
                    byRow[cell.Z] = row;
                }
                row.Add(cell.X);
            }

            var rowKeys = new List<int>(byRow.Keys);
            rowKeys.Sort();
            for (int r = 0; r < rowKeys.Count; r++)
            {
                int z = rowKeys[r];
                List<int> xs = byRow[z];
                xs.Sort();

                int start = xs[0];
                int previous = xs[0];
                for (int i = 1; i < xs.Count; i++)
                {
                    if (xs[i] == previous + 1)
                    {
                        previous = xs[i];
                        continue;
                    }
                    runs.Add((z, start, previous));
                    start = xs[i];
                    previous = xs[i];
                }
                runs.Add((z, start, previous));
            }

            // 2. 上下合并：左右边界一致且 z 连续的横条并成一个矩形
            var merged = new List<(int Z0, int Z1, int X0, int X1)>();
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                bool appended = false;
                for (int m = 0; m < merged.Count; m++)
                {
                    var box = merged[m];
                    if (box.X0 == run.X0 && box.X1 == run.X1 && box.Z1 + 1 == run.Z)
                    {
                        merged[m] = (box.Z0, run.Z, box.X0, box.X1);
                        appended = true;
                        break;
                    }
                }
                if (!appended)
                {
                    merged.Add((run.Z, run.Z, run.X0, run.X1));
                }
            }

            // 3. 太碎就退化成外接矩形：只会把范围画大一点，不会漏画，
            //    而 shader 里的 box 数组是定长的，越界比画大更糟
            if (merged.Count > MaxBoxes)
            {
                result.Add(BoundingBox(cells));
                return;
            }

            for (int i = 0; i < merged.Count; i++)
            {
                var box = merged[i];
                result.Add(new CellBox(
                    (box.X0 + box.X1) * 0.5f,
                    (box.Z0 + box.Z1) * 0.5f,
                    (box.X1 - box.X0) * 0.5f,
                    (box.Z1 - box.Z0) * 0.5f));
            }
        }

        private static CellBox BoundingBox(IReadOnlyList<CellCoord> cells)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minZ = int.MaxValue, maxZ = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                CellCoord cell = cells[i];
                if (cell.X < minX) minX = cell.X;
                if (cell.X > maxX) maxX = cell.X;
                if (cell.Z < minZ) minZ = cell.Z;
                if (cell.Z > maxZ) maxZ = cell.Z;
            }
            return new CellBox(
                (minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f,
                (maxX - minX) * 0.5f, (maxZ - minZ) * 0.5f);
        }

        /// <summary>
        /// 点到这组矩形的最小距离（格）。矩形内部返回 0——
        /// 范围判定只关心"离占地多远"，钻进占地里没有更近一说。
        /// </summary>
        public static float DistanceToBoxes(IReadOnlyList<CellBox> boxes, float x, float z)
        {
            float best = float.PositiveInfinity;
            for (int i = 0; i < boxes.Count; i++)
            {
                CellBox box = boxes[i];
                float dx = Mathf.Max(Mathf.Abs(x - box.CenterX) - box.HalfX, 0f);
                float dz = Mathf.Max(Mathf.Abs(z - box.CenterZ) - box.HalfZ, 0f);
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance < best)
                {
                    best = distance;
                }
            }
            return best == float.PositiveInfinity ? 0f : best;
        }

        /// <summary>
        /// 把格空间的矩形换算到世界 XZ。格中心 = 格角点 + 半格，所以中心要补半个 cellSize。
        /// 返回 (centerX, centerZ, halfX, halfZ)，直接喂 shader 的 float4 数组。
        /// </summary>
        public static Vector4 ToWorldBox(CellBox box, GridGeometry geometry, int layer)
        {
            Vector3 origin = geometry.LayerOrigin(layer);
            float cellSize = geometry.CellSize;
            return new Vector4(
                origin.x + (box.CenterX + 0.5f) * cellSize,
                origin.z + (box.CenterZ + 0.5f) * cellSize,
                box.HalfX * cellSize,
                box.HalfZ * cellSize);
        }
    }
}
