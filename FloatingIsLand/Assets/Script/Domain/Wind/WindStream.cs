using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Wind
{
    /// <summary>
    /// 一股风（WIND_IMPL §2.1）。正本是「风源 + 地图」，本类的路径字段全部是
    /// <see cref="WindSimulator"/> 传播产物的缓存，可随时重算；每股互相不可见（叠加场总纲）。
    /// 数据层仅方向会被建筑改写（风帆转向）；强度沿路径恒定，交汇格的强度由逐格合成单独得出。
    /// </summary>
    public sealed class WindStream
    {
        /// <summary>源序号派生，稳定（渲染流线 / 调试用）。</summary>
        public int Id { get; }

        public CellCoord Origin { get; }

        /// <summary>风依附的高度层（与起点格的层级对齐；传播不跨层，PROJECT_BUILD §9 风险 8 的 MVP 口径）。</summary>
        public int Layer { get; }

        public Dir8 OriginDir { get; }

        /// <summary>本股风力 1~5（沿路径恒定）。</summary>
        public int Force { get; }

        /// <summary>初始长度（格）。</summary>
        public int InitialLength { get; }

        /// <summary>完整路径（含起点格）；下标即「路径第几格」。</summary>
        public IReadOnlyList<WindPathStep> Path { get; }

        /// <summary>拐点列表（风帆产生；同帆重复经过会出现多条）。</summary>
        public IReadOnlyList<TurnPoint> Turns { get; }

        /// <summary>长度影响账本。</summary>
        public IReadOnlyList<LengthModifier> LengthMods { get; }

        /// <summary>终点格（路径最后一格）。</summary>
        public CellCoord End { get; }

        public WindEndReason EndReason { get; }

        /// <summary>从路径第几格起变为物流风（物流点的下一格起算，「经过后」携带；-1 = 从未）。</summary>
        public int LogisticsFromIndex { get; }

        public WindStream(
            int id, CellCoord origin, int layer, Dir8 originDir, int force, int initialLength,
            IReadOnlyList<WindPathStep> path, IReadOnlyList<TurnPoint> turns,
            IReadOnlyList<LengthModifier> lengthMods, CellCoord end, WindEndReason endReason,
            int logisticsFromIndex)
        {
            Id = id;
            Origin = origin;
            Layer = layer;
            OriginDir = originDir;
            Force = force;
            InitialLength = initialLength;
            Path = path;
            Turns = turns;
            LengthMods = lengthMods;
            End = end;
            EndReason = endReason;
            LogisticsFromIndex = logisticsFromIndex;
        }

        /// <summary>当前总长度 = 初始长度 + 账本增量之和。</summary>
        public int CurrentLength
        {
            get
            {
                int total = InitialLength;
                for (int i = 0; i < LengthMods.Count; i++)
                {
                    total += LengthMods[i].Delta;
                }
                return total;
            }
        }

        /// <summary>路径第 index 格是否已是物流风。</summary>
        public bool IsLogisticsAt(int index)
        {
            return LogisticsFromIndex >= 0 && index >= LogisticsFromIndex;
        }
    }
}
