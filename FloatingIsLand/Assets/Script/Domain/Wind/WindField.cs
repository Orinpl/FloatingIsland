using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Wind
{
    /// <summary>
    /// 一个格子的风路层状态（WIND_IMPL §2.2）。朝向/强度由重算时叠加合成好挂在格子上，
    /// 计分与渲染只读缓存结果；<see cref="Passes"/> 仅供风帆逐股转向、流线渲染与调试。
    /// </summary>
    public sealed class WindCellState
    {
        /// <summary>每股在该格的原始分量（自交时同股可出现多条）。</summary>
        public IReadOnlyList<WindPass> Passes { get; }

        /// <summary>合成方向（吸附 45°；<see cref="ResultForce"/> 为 0 时无意义）。</summary>
        public Dir8 ResultDir { get; }

        /// <summary>合成强度 0~5（0 = 无风/完全抵消）。</summary>
        public int ResultForce { get; }

        /// <summary>任一分量在该格已是物流风。</summary>
        public bool IsLogistics { get; }

        public WindCellState(IReadOnlyList<WindPass> passes, Dir8 resultDir, int resultForce, bool isLogistics)
        {
            Passes = passes;
            ResultDir = resultDir;
            ResultForce = resultForce;
            IsLogistics = isLogistics;
        }
    }

    /// <summary>
    /// 对外只读风场（WIND_IMPL §2.3）：一次 <see cref="WindSimulator.Recompute"/> 的不可变产物。
    /// 计分、合法性校验、表现层一律走合成结果；风变化时整个对象被替换，不做原地修改。
    /// </summary>
    public sealed class WindField : IWindField
    {
        private readonly Dictionary<long, WindCellState> _cells;
        private readonly IReadOnlyList<WindStream> _streams;

        /// <summary>物流风覆盖判定用：各层的物流风格子（去重后）。</summary>
        private readonly Dictionary<int, List<CellCoord>> _logisticsCellsByLayer;

        private readonly int _width;
        private readonly int _length;

        internal WindField(
            int width, int length,
            IReadOnlyList<WindStream> streams,
            Dictionary<long, WindCellState> cells,
            Dictionary<int, List<CellCoord>> logisticsCellsByLayer)
        {
            _width = width;
            _length = length;
            _streams = streams;
            _cells = cells;
            _logisticsCellsByLayer = logisticsCellsByLayer;
        }

        /// <summary>全部风股（渲染流线 / 互联结算用）。</summary>
        public IReadOnlyList<WindStream> Streams
        {
            get { return _streams; }
        }

        /// <summary>取该格的风路层状态；无风返回 false。</summary>
        public bool TryGetCell(int x, int z, int layer, out WindCellState cell)
        {
            cell = null;
            if (x < 0 || x >= _width || z < 0 || z >= _length)
            {
                return false;
            }
            return _cells.TryGetValue(Key(x, z, layer), out cell);
        }

        /// <summary>该格的合成风力等级（0~5，0=无风）。越界返回 0。</summary>
        public int GetForce(int x, int z, int layer)
        {
            WindCellState cell;
            return TryGetCell(x, z, layer, out cell) ? cell.ResultForce : 0;
        }

        /// <summary>该格是否有物流风经过。</summary>
        public bool IsLogisticsAt(int x, int z, int layer)
        {
            WindCellState cell;
            return TryGetCell(x, z, layer, out cell) && cell.IsLogistics;
        }

        /// <summary>
        /// 这组占用格是否在任一物流风格子的球形范围内（§10.4 物流风把覆盖传播到本地范围外）。
        /// 口径与建筑覆盖一致：<see cref="RangeMath"/> 三维欧氏、自占地边缘起算。
        /// </summary>
        public bool HasLogisticsWindNear(IReadOnlyList<CellCoord> cells, int layer, int radius, float layerHeightFactor)
        {
            if (cells == null || cells.Count == 0 || radius <= 0)
            {
                return false;
            }
            foreach (KeyValuePair<int, List<CellCoord>> pair in _logisticsCellsByLayer)
            {
                if (RangeMath.InRange(cells, layer, pair.Value, pair.Key, radius, layerHeightFactor))
                {
                    return true;
                }
            }
            return false;
        }

        private long Key(int x, int z, int layer)
        {
            return ((long)layer * _length + z) * _width + x;
        }
    }
}
