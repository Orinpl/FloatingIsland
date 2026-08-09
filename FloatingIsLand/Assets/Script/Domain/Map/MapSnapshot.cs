using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Map
{
    /// <summary>
    /// 一张关卡地图的地形层快照：尺寸 + 稀疏的已刷地块集合。纯数据、可单测、无 UnityEngine 依赖。
    ///
    /// 稀疏语义是本类的核心约定：**没有 MapCell 的坐标 = 虚空 = 不可建造、不渲染**。
    /// 编辑器刷子只写刷过的格子，运行时也只加载这些格子，两端共用本结构。
    ///
    /// 本快照只含地形层。建筑占用、风段、异形 footprint 校验属于 Domain/Map 的其余部分（M1，
    /// PROJECT_BUILD §4.1）；<see cref="IsPainted"/> 是这些校验的地形前置条件，不是完整的可建造判定。
    /// </summary>
    public sealed class MapSnapshot
    {
        private readonly Dictionary<long, MapCell> _cellsByKey;
        private readonly MapCell[] _cells;

        /// <summary>关卡 Id（= 配表 Stage.stageId）。</summary>
        public int StageId { get; }

        /// <summary>地图宽（格）。坐标合法区间 x ∈ [0, Width)。</summary>
        public int Width { get; }

        /// <summary>地图长（格）。坐标合法区间 z ∈ [0, Length)。</summary>
        public int Length { get; }

        /// <summary>高度层数。当前手工图恒为 1（GRID_INTEGRATION §7 开放问题 1：风跨层规则未定）。</summary>
        public int LayerCount { get; }

        /// <summary>全部已刷地块，顺序与构造时传入一致（存盘顺序稳定，便于 git diff）。</summary>
        public IReadOnlyList<MapCell> Cells
        {
            get { return _cells; }
        }

        /// <summary>已刷地块数量。0 表示整张图还没刷过。</summary>
        public int PaintedCount
        {
            get { return _cells.Length; }
        }

        /// <summary>
        /// 生成期落位的地图元素（巨型风车 / 锚点 / 矿藏 / 初始风源），顺序与构造时一致。
        /// 与地形层分开：地形是逐格区域属性，元素是有个体身份的带占地物体（§6/§12.8 归属与递减要用）。
        /// </summary>
        public IReadOnlyList<MapElementPlacement> Elements
        {
            get { return _elements; }
        }

        private readonly MapElementPlacement[] _elements;

        /// <param name="cells">已刷地块；越界坐标、重复坐标一律抛异常——地图数据是产线产物，坏数据要当场炸而不是静默吞掉。</param>
        public MapSnapshot(int stageId, int width, int length, int layerCount, IReadOnlyList<MapCell> cells)
            : this(stageId, width, length, layerCount, cells, null)
        {
        }

        /// <param name="cells">已刷地块；越界坐标、重复坐标一律抛异常。</param>
        /// <param name="elements">已落位的地图元素；锚点格越界抛异常（占地是否越界由摆放期校验，这里只保证锚点可寻址）。</param>
        public MapSnapshot(
            int stageId, int width, int length, int layerCount,
            IReadOnlyList<MapCell> cells,
            IReadOnlyList<MapElementPlacement> elements)
        {
            if (width <= 0 || length <= 0)
            {
                throw new ArgumentException($"地图 [stage {stageId}] 尺寸非法：{width}×{length}（宽/长须为正）。");
            }
            if (layerCount <= 0)
            {
                throw new ArgumentException($"地图 [stage {stageId}] 高度层数非法：{layerCount}（须 ≥ 1）。");
            }

            StageId = stageId;
            Width = width;
            Length = length;
            LayerCount = layerCount;

            IReadOnlyList<MapCell> source = cells ?? Array.Empty<MapCell>();
            _cells = new MapCell[source.Count];
            _cellsByKey = new Dictionary<long, MapCell>(source.Count);

            for (int i = 0; i < source.Count; i++)
            {
                MapCell cell = source[i];
                if (!InBounds(cell.X, cell.Z, cell.Layer))
                {
                    throw new ArgumentException(
                        $"地图 [stage {stageId}] 第 {i} 个地块坐标越界：({cell.X}, {cell.Z}, layer {cell.Layer})，地图为 {width}×{length}×{layerCount} 层。");
                }
                if (string.IsNullOrEmpty(cell.ElementId))
                {
                    throw new ArgumentException(
                        $"地图 [stage {stageId}] 第 {i} 个地块 ({cell.X}, {cell.Z}, layer {cell.Layer}) 的 elementId 为空。");
                }

                long key = Key(cell.X, cell.Z, cell.Layer);
                if (_cellsByKey.ContainsKey(key))
                {
                    throw new ArgumentException(
                        $"地图 [stage {stageId}] 存在重复地块坐标：({cell.X}, {cell.Z}, layer {cell.Layer})。");
                }

                _cellsByKey.Add(key, cell);
                _cells[i] = cell;
            }

            IReadOnlyList<MapElementPlacement> elementSource = elements ?? Array.Empty<MapElementPlacement>();
            _elements = new MapElementPlacement[elementSource.Count];
            for (int i = 0; i < elementSource.Count; i++)
            {
                MapElementPlacement element = elementSource[i];
                if (string.IsNullOrEmpty(element.ElementId))
                {
                    throw new ArgumentException($"地图 [stage {stageId}] 第 {i} 个元素的 elementId 为空。");
                }
                if (!InBounds(element.X, element.Z, element.Layer))
                {
                    throw new ArgumentException(
                        $"地图 [stage {stageId}] 第 {i} 个元素 [{element.ElementId}] 锚点越界：({element.X}, {element.Z}, layer {element.Layer})，地图为 {width}×{length}×{layerCount} 层。");
                }
                _elements[i] = element;
            }
        }

        /// <summary>坐标是否在地图尺寸内。注意"在界内"不等于"刷过"。</summary>
        public bool InBounds(int x, int z, int layer)
        {
            return x >= 0 && x < Width && z >= 0 && z < Length && layer >= 0 && layer < LayerCount;
        }

        /// <summary>
        /// 该坐标是否刷过地形。false = 虚空：不可建造、不渲染。
        /// 越界同样返回 false（界外自然也是虚空，调用方不必先做边界判断）。
        /// </summary>
        public bool IsPainted(int x, int z, int layer)
        {
            return InBounds(x, z, layer) && _cellsByKey.ContainsKey(Key(x, z, layer));
        }

        /// <summary>取该坐标的地块；未刷过或越界返回 false。</summary>
        public bool TryGetCell(int x, int z, int layer, out MapCell cell)
        {
            if (!InBounds(x, z, layer))
            {
                cell = default(MapCell);
                return false;
            }
            return _cellsByKey.TryGetValue(Key(x, z, layer), out cell);
        }

        /// <summary>该坐标的地形 Id；未刷过或越界返回 null。</summary>
        public string GetElementIdOrNull(int x, int z, int layer)
        {
            MapCell cell;
            return TryGetCell(x, z, layer, out cell) ? cell.ElementId : null;
        }

        /// <summary>线性索引作字典键——调用前坐标已过 InBounds，不会溢出也不会冲突。</summary>
        private long Key(int x, int z, int layer)
        {
            return ((long)layer * Length + z) * Width + x;
        }
    }
}
