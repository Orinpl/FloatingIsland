using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Map
{
    /// <summary>
    /// 异形占地掩码（配表 BuildingVariant.footprint / MapElement.footprint 的领域表示）。
    ///
    /// 掩码是「自上而下」的若干行字符串，'#'=占用、'.'=空，如居民区 L 形 ["###", "#.."]。
    /// EGB 的占地只支持矩形装不下异形，所以占用判定全在领域层（GRID_INTEGRATION §1）。
    ///
    /// 坐标约定：掩码第 r 行第 c 列 → 相对锚点的偏移 (dx=c, dz=rows-1-r)。
    /// 即掩码最后一行贴着 z 最小侧，与「锚点=占地最小角」和 EGB 的格角点口径一致，
    /// 这样 Prefab 直接摆在锚点格的角点上就能对齐（见 BuildingModelPostprocessor 的轴心归位）。
    /// </summary>
    public sealed class Footprint
    {
        private readonly CellOffset[] _offsets;

        /// <summary>掩码列数（未旋转时的 X 跨度）。</summary>
        public int Columns { get; }

        /// <summary>掩码行数（未旋转时的 Z 跨度）。</summary>
        public int Rows { get; }

        /// <summary>占用格数。</summary>
        public int CellCount
        {
            get { return _offsets.Length; }
        }

        private Footprint(int columns, int rows, CellOffset[] offsets)
        {
            Columns = columns;
            Rows = rows;
            _offsets = offsets;
        }

        /// <summary>
        /// 解析掩码。各行长度须一致、至少一个 '#'，否则抛异常——占地是配表产物，坏数据当场炸。
        /// </summary>
        /// <param name="context">报错定位用，如 "BuildingVariant[residence_02].footprint"。</param>
        public static Footprint Parse(string[] mask, string context)
        {
            if (mask == null || mask.Length == 0)
            {
                throw new ArgumentException($"{context}: 占地掩码为空。");
            }

            int columns = mask[0] == null ? 0 : mask[0].Length;
            if (columns == 0)
            {
                throw new ArgumentException($"{context}: 占地掩码第 0 行为空。");
            }

            int rows = mask.Length;
            var offsets = new List<CellOffset>(columns * rows);

            for (int r = 0; r < rows; r++)
            {
                string line = mask[r];
                if (line == null || line.Length != columns)
                {
                    throw new ArgumentException(
                        $"{context}: 占地掩码第 {r} 行长度为 {(line == null ? "null" : line.Length.ToString())}，与第 0 行的 {columns} 不一致。");
                }

                for (int c = 0; c < columns; c++)
                {
                    char ch = line[c];
                    if (ch == '#')
                    {
                        offsets.Add(new CellOffset(c, rows - 1 - r));
                    }
                    else if (ch != '.')
                    {
                        throw new ArgumentException($"{context}: 占地掩码第 {r} 行第 {c} 列是 '{ch}'，只允许 '#' 或 '.'。");
                    }
                }
            }

            if (offsets.Count == 0)
            {
                throw new ArgumentException($"{context}: 占地掩码没有任何 '#'。");
            }

            return new Footprint(columns, rows, offsets.ToArray());
        }

        /// <summary>
        /// 按旋转取所有占用格的绝对坐标（锚点 = 旋转后占地的最小角）。
        /// 旋转后各偏移仍从 (0,0) 起算，保证「摆在哪个格子上」这件事与旋转无关。
        /// </summary>
        public void GetCells(int originX, int originZ, Rotation rotation, List<CellCoord> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            int spanX = SpanX(rotation);
            int spanZ = SpanZ(rotation);
            for (int i = 0; i < _offsets.Length; i++)
            {
                CellOffset rotated = Rotate(_offsets[i], rotation, spanX, spanZ);
                result.Add(new CellCoord(originX + rotated.Dx, originZ + rotated.Dz));
            }
        }

        /// <summary>旋转后的 X 跨度（格）。</summary>
        public int SpanX(Rotation rotation)
        {
            return rotation == Rotation.Deg0 || rotation == Rotation.Deg180 ? Columns : Rows;
        }

        /// <summary>旋转后的 Z 跨度（格）。</summary>
        public int SpanZ(Rotation rotation)
        {
            return rotation == Rotation.Deg0 || rotation == Rotation.Deg180 ? Rows : Columns;
        }

        /// <summary>绕锚点顺时针旋转一个偏移，再平移回第一象限（保证最小角仍是 (0,0)）。</summary>
        private static CellOffset Rotate(CellOffset offset, Rotation rotation, int spanX, int spanZ)
        {
            switch (rotation)
            {
                case Rotation.Deg0:
                    return offset;
                case Rotation.Deg90:
                    // (dx, dz) → (dz, -dx)，再沿 Z 平移 spanZ-1 回正
                    return new CellOffset(offset.Dz, spanZ - 1 - offset.Dx);
                case Rotation.Deg180:
                    return new CellOffset(spanX - 1 - offset.Dx, spanZ - 1 - offset.Dz);
                case Rotation.Deg270:
                    return new CellOffset(spanX - 1 - offset.Dz, offset.Dx);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }

        private readonly struct CellOffset
        {
            public readonly int Dx;
            public readonly int Dz;

            public CellOffset(int dx, int dz)
            {
                Dx = dx;
                Dz = dz;
            }
        }
    }

    /// <summary>摆放朝向：绕 Y 轴顺时针 90° 步进（EGB 也是 4 方向，表现层直接乘 90°）。</summary>
    public enum Rotation
    {
        Deg0 = 0,
        Deg90 = 1,
        Deg180 = 2,
        Deg270 = 3,
    }

    /// <summary>地图平面格坐标（高度层单独带）。</summary>
    public readonly struct CellCoord : IEquatable<CellCoord>
    {
        public readonly int X;
        public readonly int Z;

        public CellCoord(int x, int z)
        {
            X = x;
            Z = z;
        }

        public bool Equals(CellCoord other)
        {
            return X == other.X && Z == other.Z;
        }

        public override bool Equals(object obj)
        {
            return obj is CellCoord other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (X * 397) ^ Z;
        }

        public override string ToString()
        {
            return $"({X}, {Z})";
        }
    }

    /// <summary><see cref="Rotation"/> 的循环步进辅助。</summary>
    public static class RotationExtensions
    {
        /// <summary>顺时针转 steps 步（可为负），结果规约到 0~3。</summary>
        public static Rotation Step(this Rotation rotation, int steps)
        {
            int value = ((int)rotation + steps) % 4;
            if (value < 0)
            {
                value += 4;
            }
            return (Rotation)value;
        }

        /// <summary>转成表现层的 Y 轴角度（度）。</summary>
        public static float ToDegrees(this Rotation rotation)
        {
            return (int)rotation * 90f;
        }
    }
}
