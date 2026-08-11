using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>一栋已落地的建筑。</summary>
    public sealed class PlacedBuilding
    {
        /// <summary>局内唯一序号（落地顺序，1 起）。</summary>
        public int Id { get; }

        /// <summary>蓝图。</summary>
        public BuildingBlueprint Blueprint { get; }

        /// <summary>锚点格 X（占地最小角）。</summary>
        public int X { get; }

        /// <summary>锚点格 Z。</summary>
        public int Z { get; }

        /// <summary>高度层。</summary>
        public int Layer { get; }

        /// <summary>朝向。</summary>
        public Rotation Rotation { get; }

        /// <summary>占用格（绝对坐标，已含旋转）。</summary>
        public IReadOnlyList<CellCoord> Cells { get; }

        /// <summary>落地时结算出的即时建造分（落地前算好传进来，之后不再变）。</summary>
        public int InstantScore { get; }

        public PlacedBuilding(int id, BuildingBlueprint blueprint, int x, int z, int layer, Rotation rotation, IReadOnlyList<CellCoord> cells, int instantScore)
        {
            Id = id;
            Blueprint = blueprint;
            X = x;
            Z = z;
            Layer = layer;
            Rotation = rotation;
            Cells = cells;
            InstantScore = instantScore;
        }
    }

    /// <summary>一个已落位地图元素的运行时视图（含展开后的占用格）。</summary>
    public sealed class PlacedElement
    {
        /// <summary>局内唯一序号（1 起）。</summary>
        public int Id { get; }

        /// <summary>元素定义。</summary>
        public MapElementDef Def { get; }

        /// <summary>锚点格 X。</summary>
        public int X { get; }

        /// <summary>锚点格 Z。</summary>
        public int Z { get; }

        /// <summary>高度层。</summary>
        public int Layer { get; }

        /// <summary>朝向。</summary>
        public Rotation Rotation { get; }

        /// <summary>占用格（绝对坐标，已含旋转）。</summary>
        public IReadOnlyList<CellCoord> Cells { get; }

        public PlacedElement(int id, MapElementDef def, int x, int z, int layer, Rotation rotation, IReadOnlyList<CellCoord> cells)
        {
            Id = id;
            Def = def;
            X = x;
            Z = z;
            Layer = layer;
            Rotation = rotation;
            Cells = cells;
        }
    }

    /// <summary>摆放校验失败的原因（UI 直接显示 <see cref="PlacementCheck.Reason"/>）。</summary>
    public enum PlacementFailure
    {
        None = 0,

        /// <summary>变体 Id 不在规则集中。</summary>
        UnknownBlueprint,

        /// <summary>占用格超出地图尺寸。</summary>
        OutOfBounds,

        /// <summary>占用格里有虚空（未刷地形）。</summary>
        Void,

        /// <summary>占用格已被别的建筑占了。</summary>
        Occupied,

        /// <summary>占用格压到地图元素了。</summary>
        BlockedByElement,

        /// <summary>地形不满足 placement 限制。</summary>
        TerrainMismatch,

        /// <summary>不在矿藏有效范围内。</summary>
        OutOfOreRange,

        /// <summary>不在风带上（风系统未接入时不会出现）。</summary>
        NoWind,
    }

    /// <summary>
    /// 单格的占地判定结果（表现层画落点格用）。
    ///
    /// 只反映**逐格规则**（越界 / 虚空 / 已被占 / 压到元素 / 地形不符）。
    /// 矿脉范围、风带这类整体规则不属于任何一格，所以可能出现「每格都 IsValid、整体却摆不下」——
    /// 整体结论一律以 <see cref="BuildBoard.CanPlace"/> 为准。
    /// </summary>
    public readonly struct CellPlacement
    {
        /// <summary>格坐标（已含旋转）。</summary>
        public readonly CellCoord Cell;

        /// <summary>本格自身是否合格。</summary>
        public readonly bool IsValid;

        /// <summary>本格不合格的原因；合格时为 <see cref="PlacementFailure.None"/>。</summary>
        public readonly PlacementFailure Failure;

        public CellPlacement(CellCoord cell, bool isValid, PlacementFailure failure)
        {
            Cell = cell;
            IsValid = isValid;
            Failure = failure;
        }
    }

    /// <summary>摆放校验结果。</summary>
    public readonly struct PlacementCheck
    {
        /// <summary>是否可以摆。</summary>
        public readonly bool IsValid;

        /// <summary>失败类型。</summary>
        public readonly PlacementFailure Failure;

        /// <summary>给玩家看的中文原因；合法时为空串。</summary>
        public readonly string Reason;

        private PlacementCheck(bool isValid, PlacementFailure failure, string reason)
        {
            IsValid = isValid;
            Failure = failure;
            Reason = reason ?? string.Empty;
        }

        public static PlacementCheck Ok()
        {
            return new PlacementCheck(true, PlacementFailure.None, string.Empty);
        }

        public static PlacementCheck Fail(PlacementFailure failure, string reason)
        {
            return new PlacementCheck(false, failure, reason);
        }
    }

    /// <summary>
    /// 局内建造盘：地形快照 + 元素层 + 建筑占用，负责「能不能摆」和「摆下去」。
    ///
    /// 规则真相全在这里，EGB 永远不做裁决（GRID_INTEGRATION §1）：表现层只负责把鼠标位置换成格子、
    /// 把落地结果画出来，合法性一律回来问 <see cref="CanPlace"/>。
    /// </summary>
    public sealed class BuildBoard
    {
        private readonly MapSnapshot _map;
        private readonly BuildRuleSet _rules;

        /// <summary>格 → 占用它的建筑。用线性键，与 MapSnapshot 同口径。</summary>
        private readonly Dictionary<long, PlacedBuilding> _buildingByCell;

        /// <summary>格 → 压在它上面的元素。</summary>
        private readonly Dictionary<long, PlacedElement> _elementByCell;

        private readonly List<PlacedBuilding> _buildings = new List<PlacedBuilding>();
        private readonly List<PlacedElement> _elements = new List<PlacedElement>();

        /// <summary>复用的临时占用格缓冲，避免每次校验都分配。</summary>
        private readonly List<CellCoord> _scratch = new List<CellCoord>(64);

        private int _nextBuildingId = 1;

        /// <summary>地形快照。</summary>
        public MapSnapshot Map
        {
            get { return _map; }
        }

        /// <summary>规则集。</summary>
        public BuildRuleSet Rules
        {
            get { return _rules; }
        }

        /// <summary>已落地建筑，按落地顺序。</summary>
        public IReadOnlyList<PlacedBuilding> Buildings
        {
            get { return _buildings; }
        }

        /// <summary>已落位地图元素。</summary>
        public IReadOnlyList<PlacedElement> Elements
        {
            get { return _elements; }
        }

        /// <summary>建筑落地事件（表现层据此生成模型）。</summary>
        public event Action<PlacedBuilding> BuildingPlaced;

        /// <summary>
        /// 风场（M3 接入）。为 null 时风带建造限制与风力计分整块跳过，
        /// 不用 0 级风冒充「没有风场」——那会让船坞永远吃到 -150 的无风惩罚。
        /// </summary>
        public IWindField WindField { get; set; }

        public BuildBoard(MapSnapshot map, BuildRuleSet rules)
        {
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _buildingByCell = new Dictionary<long, PlacedBuilding>();
            _elementByCell = new Dictionary<long, PlacedElement>();

            LoadElements();
        }

        /// <summary>把快照里的元素展开成占用格索引。占地越界/压虚空只警告不抛——地图是编辑期产物，宁可少占也别让整局起不来。</summary>
        private void LoadElements()
        {
            var cells = new List<CellCoord>(16);
            int nextId = 1;

            for (int i = 0; i < _map.Elements.Count; i++)
            {
                MapElementPlacement placement = _map.Elements[i];
                MapElementDef def = _rules.GetElementOrNull(placement.ElementId);
                if (def == null || def.IsTerrain)
                {
                    // 地形类元素属于地形层（MapCell），不在元素层展开
                    continue;
                }

                def.Footprint.GetCells(placement.X, placement.Z, placement.Rotation, cells);
                var owned = new List<CellCoord>(cells.Count);
                for (int c = 0; c < cells.Count; c++)
                {
                    CellCoord cell = cells[c];
                    if (!_map.InBounds(cell.X, cell.Z, placement.Layer))
                    {
                        continue;
                    }
                    owned.Add(cell);
                }

                if (owned.Count == 0)
                {
                    continue;
                }

                var element = new PlacedElement(nextId++, def, placement.X, placement.Z, placement.Layer, placement.Rotation, owned);
                _elements.Add(element);
                for (int c = 0; c < owned.Count; c++)
                {
                    _elementByCell[Key(owned[c].X, owned[c].Z, placement.Layer)] = element;
                }
            }
        }

        /// <summary>该格是否已被建筑占用。</summary>
        public bool IsOccupied(int x, int z, int layer)
        {
            return _buildingByCell.ContainsKey(Key(x, z, layer));
        }

        /// <summary>取占用该格的建筑；没有返回 null。</summary>
        public PlacedBuilding GetBuildingAt(int x, int z, int layer)
        {
            PlacedBuilding building;
            return _buildingByCell.TryGetValue(Key(x, z, layer), out building) ? building : null;
        }

        /// <summary>取压在该格上的元素；没有返回 null。</summary>
        public PlacedElement GetElementAt(int x, int z, int layer)
        {
            PlacedElement element;
            return _elementByCell.TryGetValue(Key(x, z, layer), out element) ? element : null;
        }

        /// <summary>
        /// 把蓝图按锚点+朝向展开成占用格。给表现层画 ghost 用，不含合法性判断。
        /// </summary>
        public void GetCells(BuildingBlueprint blueprint, int x, int z, Rotation rotation, List<CellCoord> result)
        {
            blueprint.Footprint.GetCells(x, z, rotation, result);
        }

        /// <summary>能不能把这个变体摆在这里（干跑，不改状态）。</summary>
        public PlacementCheck CanPlace(string variantId, int x, int z, int layer, Rotation rotation)
        {
            BuildingBlueprint blueprint = _rules.GetBlueprintOrNull(variantId);
            if (blueprint == null)
            {
                return PlacementCheck.Fail(PlacementFailure.UnknownBlueprint, $"未知建筑变体：{variantId}");
            }
            return CanPlace(blueprint, x, z, layer, rotation);
        }

        /// <summary>能不能把这个蓝图摆在这里（干跑，不改状态）。</summary>
        public PlacementCheck CanPlace(BuildingBlueprint blueprint, int x, int z, int layer, Rotation rotation)
        {
            if (blueprint == null)
            {
                return PlacementCheck.Fail(PlacementFailure.UnknownBlueprint, "未知建筑变体。");
            }

            blueprint.Footprint.GetCells(x, z, rotation, _scratch);

            for (int i = 0; i < _scratch.Count; i++)
            {
                PlacementCheck cellCheck = CheckCell(blueprint, _scratch[i], layer);
                if (!cellCheck.IsValid)
                {
                    return cellCheck;
                }
            }

            if (blueprint.Placement == PlacementRule.OreRange && !IsInElementRange(_scratch, layer, BuildRuleSet.OreElementId))
            {
                return PlacementCheck.Fail(PlacementFailure.OutOfOreRange, "必须建在矿藏的有效范围内。");
            }

            // 风带限制：目标格 ResultForce ≥ 1（WIND_IMPL §6）。风场未接入（M3 之前 WindField 为 null）
            // 时整条规则跳过——否则风帆在风系统落地前完全无法摆放。
            if (blueprint.Placement == PlacementRule.WindPath && WindField != null)
            {
                bool onWind = false;
                for (int i = 0; i < _scratch.Count; i++)
                {
                    if (WindField.GetForce(_scratch[i].X, _scratch[i].Z, layer) >= 1)
                    {
                        onWind = true;
                        break;
                    }
                }
                if (!onWind)
                {
                    return PlacementCheck.Fail(PlacementFailure.NoWind, "必须建在有风经过的地块上。");
                }
            }

            return PlacementCheck.Ok();
        }

        /// <summary>
        /// 逐格给出占地判定，供表现层把落点格逐个标绿/标红。
        ///
        /// 与 <see cref="CanPlace"/> 的分工：那边碰到第一个坏格就返回（玩家只需要一条原因），
        /// 这边每格都判，好让玩家一眼看出**是哪几格**挡住了。两边共用同一个 <see cref="CheckCell"/>，
        /// 不会出现「格子标绿但摆不下」这种预览与裁决分叉。
        /// 整体规则（矿脉范围 / 风带）不属于任何单格，不在这里体现。
        /// </summary>
        public void CheckCells(
            BuildingBlueprint blueprint, int x, int z, int layer, Rotation rotation, List<CellPlacement> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            if (blueprint == null)
            {
                return;
            }

            blueprint.Footprint.GetCells(x, z, rotation, _scratch);
            for (int i = 0; i < _scratch.Count; i++)
            {
                CellCoord cell = _scratch[i];
                PlacementCheck check = CheckCell(blueprint, cell, layer);
                result.Add(new CellPlacement(cell, check.IsValid, check.Failure));
            }
        }

        /// <summary>单格的逐格规则：越界 / 虚空 / 已被占 / 压到元素 / 地形不符。</summary>
        private PlacementCheck CheckCell(BuildingBlueprint blueprint, CellCoord cell, int layer)
        {
            if (!_map.InBounds(cell.X, cell.Z, layer))
            {
                return PlacementCheck.Fail(PlacementFailure.OutOfBounds, "超出地图范围。");
            }

            string terrain = _map.GetElementIdOrNull(cell.X, cell.Z, layer);
            if (terrain == null)
            {
                return PlacementCheck.Fail(PlacementFailure.Void, "占地压到虚空，必须整块建在已有地形上。");
            }

            if (IsOccupied(cell.X, cell.Z, layer))
            {
                return PlacementCheck.Fail(PlacementFailure.Occupied, "占地与已有建筑重叠。");
            }

            if (GetElementAt(cell.X, cell.Z, layer) != null)
            {
                return PlacementCheck.Fail(PlacementFailure.BlockedByElement, "占地压到地图元素。");
            }

            return CheckTerrain(blueprint, terrain);
        }

        private PlacementCheck CheckTerrain(BuildingBlueprint blueprint, string terrain)
        {
            switch (blueprint.Placement)
            {
                case PlacementRule.GreenField:
                    return string.Equals(terrain, BuildRuleSet.GreenFieldTerrainId, StringComparison.Ordinal)
                        ? PlacementCheck.Ok()
                        : PlacementCheck.Fail(PlacementFailure.TerrainMismatch, "必须整块建在绿地上。");

                case PlacementRule.FloatingZone:
                    return string.Equals(terrain, BuildRuleSet.FloatingZoneTerrainId, StringComparison.Ordinal)
                        ? PlacementCheck.Ok()
                        : PlacementCheck.Fail(PlacementFailure.TerrainMismatch, "只能建在浮空区域。");

                case PlacementRule.Any:
                case PlacementRule.OreRange:
                case PlacementRule.WindPath:
                default:
                    // 普通建筑不能建在浮空区域上——那是船坞专属的空域（§12.8）
                    return string.Equals(terrain, BuildRuleSet.FloatingZoneTerrainId, StringComparison.Ordinal)
                        ? PlacementCheck.Fail(PlacementFailure.TerrainMismatch, "浮空区域只能建船坞。")
                        : PlacementCheck.Ok();
            }
        }

        /// <summary>这组格子是否落在某类元素的有效范围内。</summary>
        public bool IsInElementRange(IReadOnlyList<CellCoord> cells, int layer, string elementId)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                PlacedElement element = _elements[i];
                if (!string.Equals(element.Def.ElementId, elementId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (RangeMath.InRange(cells, layer, element.Cells, element.Layer, element.Def.Radius, _rules.LayerHeightFactor))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 落地。调用前必须自己过一遍 <see cref="CanPlace"/>——这里再校验一次，不合法直接抛，
        /// 因为「校验通过才落地」是领域不变量，静默失败会让表现层和领域层的状态悄悄分叉。
        /// </summary>
        /// <param name="instantScore">落地前算好的即时建造分（计分必须看落地前的邻居，否则会自己给自己加同类分）。</param>
        public PlacedBuilding Place(BuildingBlueprint blueprint, int x, int z, int layer, Rotation rotation, int instantScore)
        {
            PlacementCheck check = CanPlace(blueprint, x, z, layer, rotation);
            if (!check.IsValid)
            {
                throw new InvalidOperationException(
                    $"建筑 [{blueprint?.VariantId}] 无法摆放在 ({x}, {z}, layer {layer})：{check.Reason}");
            }

            var cells = new List<CellCoord>(blueprint.Footprint.CellCount);
            blueprint.Footprint.GetCells(x, z, rotation, cells);

            var building = new PlacedBuilding(_nextBuildingId++, blueprint, x, z, layer, rotation, cells, instantScore);
            _buildings.Add(building);
            for (int i = 0; i < cells.Count; i++)
            {
                _buildingByCell[Key(cells[i].X, cells[i].Z, layer)] = building;
            }

            BuildingPlaced?.Invoke(building);
            return building;
        }

        /// <summary>
        /// 地图上是否还存在该变体的合法落点（§2 结束条件之二：无合法建造位置即结束）。
        ///
        /// 只遍历**已刷地形的格子**而不是整张 250×250 的网格：岛屿只占地图中间一小块，
        /// 按整网格从 (0,0) 扫要先空转十几万次虚空格才碰到岛。已刷格通常几千个，
        /// 且正常情况下头几格就能命中并提前返回，因此可以放在「每次落地后」这种频次上调用。
        /// </summary>
        public bool HasAnyValidPlacement(BuildingBlueprint blueprint)
        {
            if (blueprint == null)
            {
                return false;
            }

            IReadOnlyList<MapCell> cells = _map.Cells;
            for (int i = 0; i < cells.Count; i++)
            {
                MapCell cell = cells[i];
                for (int r = 0; r < 4; r++)
                {
                    if (CanPlace(blueprint, cell.X, cell.Z, cell.Layer, (Rotation)r).IsValid)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private long Key(int x, int z, int layer)
        {
            return ((long)layer * _map.Length + z) * _map.Width + x;
        }
    }
}
