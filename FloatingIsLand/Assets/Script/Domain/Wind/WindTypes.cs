using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Wind
{
    /// <summary>
    /// 八方向，逆时针排列（E 起，每步 45°）。逆时针排列让转向变成模 8 加减：
    /// 左转 45° = +1，右转 45° = -1。坐标系与地图一致：x 向东为正，z 向北为正。
    /// </summary>
    public enum Dir8
    {
        E = 0,
        NE = 1,
        N = 2,
        NW = 3,
        W = 4,
        SW = 5,
        S = 6,
        SE = 7,
    }

    /// <summary>风帆的转向设定（相对入风方向，§9.2 决策 14：只能左/右 45° 二选一）。</summary>
    public enum TurnDir
    {
        Left = 0,
        Right = 1,
    }

    /// <summary>一股风的终止原因。</summary>
    public enum WindEndReason
    {
        /// <summary>风长耗尽。</summary>
        LengthExhausted = 0,

        /// <summary>走出网格边界（WIND_IMPL 决策 9：地形不阻挡，仅出图终止）。</summary>
        OutOfMap = 1,
    }

    /// <summary>长度影响来源类型（WIND_IMPL §2.1 账本）。</summary>
    public enum LengthModifierKind
    {
        /// <summary>物流点延长（§10.5）。</summary>
        LogisticsExtend = 0,
    }

    /// <summary>一个风源展开出的初始参数（起点 + 方向 + 强度 + 长度，layer 与所在格对齐）。</summary>
    public readonly struct WindSeed
    {
        public readonly CellCoord Origin;
        public readonly int Layer;
        public readonly Dir8 Dir;

        /// <summary>本股风力 1~5。逐格强度不在股上算——交汇格由 <see cref="WindMath.Compose"/> 单独合成。</summary>
        public readonly int Force;

        /// <summary>初始长度（格）。斜向一步与正交一步同价（决策 22）。</summary>
        public readonly int Length;

        public WindSeed(CellCoord origin, int layer, Dir8 dir, int force, int length)
        {
            Origin = origin;
            Layer = layer;
            Dir = dir;
            Force = force;
            Length = length;
        }
    }

    /// <summary>拐点：风在哪一格被风帆按哪个方向转了 45°。</summary>
    public readonly struct TurnPoint
    {
        public readonly CellCoord Cell;
        public readonly TurnDir Turn;

        public TurnPoint(CellCoord cell, TurnDir turn)
        {
            Cell = cell;
            Turn = turn;
        }
    }

    /// <summary>
    /// 长度影响账本的一条（WIND_IMPL §2.1）。账本一物两用：
    /// 「同点不重复」= 无同 SourceCell 条目；「每股最多 N 次」= 条目数 &lt; windExtendMaxPerWind。
    /// </summary>
    public readonly struct LengthModifier
    {
        public readonly CellCoord SourceCell;
        public readonly LengthModifierKind Kind;
        public readonly int Delta;

        public LengthModifier(CellCoord sourceCell, LengthModifierKind kind, int delta)
        {
            SourceCell = sourceCell;
            Kind = kind;
            Delta = delta;
        }
    }

    /// <summary>路径上的一步：该格坐标 + 风离开该格的方向（拐点格记录的是转向后的出向）。</summary>
    public readonly struct WindPathStep
    {
        public readonly CellCoord Cell;
        public readonly Dir8 DirOut;

        public WindPathStep(CellCoord cell, Dir8 dirOut)
        {
            Cell = cell;
            DirOut = dirOut;
        }
    }

    /// <summary>某股风在某格的原始分量（自交时同股可出现多条，照常参与合成）。</summary>
    public readonly struct WindPass
    {
        public readonly int StreamId;

        /// <summary>该分量对应股内路径的第几步（渲染/调试定位用）。</summary>
        public readonly int PathIndex;

        public readonly Dir8 Dir;

        /// <summary>原始强度——合成的输入永远是各分量的原始强度，封顶只作用于合成结果（红线 3）。</summary>
        public readonly int Force;

        public readonly bool IsLogistics;

        public WindPass(int streamId, int pathIndex, Dir8 dir, int force, bool isLogistics)
        {
            StreamId = streamId;
            PathIndex = pathIndex;
            Dir = dir;
            Force = force;
            IsLogistics = isLogistics;
        }
    }

    /// <summary>
    /// 八方向几何与交汇合成公式（WIND_IMPL §4）。三条红线在此实现并由单测锁死：
    /// ① 斜向用单位向量（±√2/2，不是 ±1）；② 22.5° 平局固定取顺时针一侧；③ 先四舍五入再封顶。
    /// </summary>
    public static class WindMath
    {
        /// <summary>各方向一步的格偏移（按 <see cref="Dir8"/> 下标）。</summary>
        public static readonly int[] StepX = { 1, 1, 0, -1, -1, -1, 0, 1 };
        public static readonly int[] StepZ = { 0, 1, 1, 1, 0, -1, -1, -1 };

        private const double Diagonal = 0.70710678118654752; // √2/2

        /// <summary>各方向单位向量（红线 1：斜向分量是 ±√2/2）。</summary>
        private static readonly double[] UnitX = { 1, Diagonal, 0, -Diagonal, -1, -Diagonal, 0, Diagonal };
        private static readonly double[] UnitZ = { 0, Diagonal, 1, Diagonal, 0, -Diagonal, -1, -Diagonal };

        /// <summary>22.5° 居中判定的容差（度）。防浮点误差导致同局面结果漂移（WIND_IMPL §4 红线 2）。</summary>
        private const double TieEpsilonDeg = 1e-6;

        public static Dir8 TurnedBy(Dir8 dir, TurnDir turn)
        {
            // 逆时针枚举下：左转（逆时针）= +1，右转（顺时针）= -1
            int delta = turn == TurnDir.Left ? 1 : 7;
            return (Dir8)(((int)dir + delta) & 7);
        }

        /// <summary>
        /// 交汇合成：V = Σ Force×unit(Dir)，强度 = min(maxLevel, round(|V|))，
        /// 方向吸附最近 45°、恰好居中（22.5°）取顺时针一侧。
        /// 强度为 0（无风/完全抵消）时方向无意义，返回 <see cref="Dir8.E"/>。
        /// </summary>
        public static void Compose(IReadOnlyList<WindPass> passes, int maxLevel, out Dir8 resultDir, out int resultForce)
        {
            double vx = 0, vz = 0;
            for (int i = 0; i < passes.Count; i++)
            {
                WindPass pass = passes[i];
                vx += pass.Force * UnitX[(int)pass.Dir];
                vz += pass.Force * UnitZ[(int)pass.Dir];
            }

            double magnitude = Math.Sqrt(vx * vx + vz * vz);
            int force = (int)Math.Round(magnitude, MidpointRounding.AwayFromZero);
            if (force > maxLevel)
            {
                force = maxLevel;
            }

            if (force <= 0)
            {
                resultDir = Dir8.E;
                resultForce = 0;
                return;
            }

            double angle = Math.Atan2(vz, vx) * (180.0 / Math.PI);
            if (angle < 0)
            {
                angle += 360.0;
            }

            double remainder = angle % 45.0;
            int index;
            if (Math.Abs(remainder - 22.5) <= TieEpsilonDeg)
            {
                // 平局取顺时针一侧 = 角度较小的那个 45° 倍数（红线 2）
                index = (int)Math.Floor(angle / 45.0);
            }
            else
            {
                index = (int)Math.Round(angle / 45.0, MidpointRounding.AwayFromZero);
            }

            resultDir = (Dir8)(index & 7);
            resultForce = force;
        }
    }
}
