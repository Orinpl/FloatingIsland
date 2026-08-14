using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>得分明细的一行（UI 摆放预览逐条显示，§7.4）。</summary>
    public readonly struct ScoreLine
    {
        /// <summary>中文说明，如 "邻近 居民区 ×3"。</summary>
        public readonly string Label;

        /// <summary>本行分值（可为负）。</summary>
        public readonly int Score;

        public ScoreLine(string label, int score)
        {
            Label = label;
            Score = score;
        }

        public override string ToString()
        {
            return $"{Label} {(Score >= 0 ? "+" : "")}{Score}";
        }
    }

    /// <summary>一条得分归因指向什么对象。</summary>
    public enum ScoreSourceKind
    {
        /// <summary>建筑自身（基础分、孤立惩罚）——地图上没有第二个对象可指。</summary>
        Self = 0,

        /// <summary>某一座已落地建筑。<see cref="ScoreAttribution.InstanceId"/> = <see cref="PlacedBuilding.Id"/>。</summary>
        Building = 1,

        /// <summary>某一个地图元素。<see cref="ScoreAttribution.InstanceId"/> = <see cref="PlacedElement.Id"/>。</summary>
        Element = 2,

        /// <summary>风场（M3 接入后才会出现）。</summary>
        Wind = 3,
    }

    /// <summary>这条归因到底有没有算进总分。</summary>
    public enum ScoreCountState
    {
        /// <summary>算进去了。</summary>
        Counted = 0,

        /// <summary>在范围内，但被条目的计数上限挡在外面。</summary>
        OverMaxCount = 1,

        /// <summary>在范围内，但这条通道只计一次（物流基础覆盖）。</summary>
        Duplicated = 2,

        /// <summary>算了，但收益已经衰减到 0（船坞锚点第 N 座）。</summary>
        Decayed = 3,
    }

    /// <summary>
    /// 一条能落到**具体对象**上的得分归因：这 N 分是地图上哪一座建筑 / 哪一个元素给的。
    ///
    /// 与 <see cref="ScoreLine"/> 的分工：Lines 是给玩家看的聚合文案（"邻近 居民区 ×3  +24"），
    /// Attributions 是给表现层用的逐实例清单（3 号居民区 +8、7 号居民区 +8、9 号居民区 +8），
    /// 表现层据此在那几座建筑身上打辉光、飘数字。
    ///
    /// 刻意做成与 Lines 平行的扁平表，而不是挂在每一行下面：<see cref="ScoreBreakdown.Add"/>
    /// 会丢弃 0 分行，而恰恰有两类「0 分但必须能高亮」的对象——被计数上限截断的、
    /// 收益衰减归零的锚点。挂在行上就会被那条规则连坐丢掉。
    /// </summary>
    public readonly struct ScoreAttribution
    {
        /// <summary>指向什么对象。</summary>
        public readonly ScoreSourceKind Kind;

        /// <summary>对象实例 Id；<see cref="ScoreSourceKind.Self"/> / <see cref="ScoreSourceKind.Wind"/> 时为 0。</summary>
        public readonly int InstanceId;

        /// <summary>来源的配表 Id（buildingId / elementId），便于表现层分色。</summary>
        public readonly string SourceId;

        /// <summary>中文来源名，飘字与提示直接用。</summary>
        public readonly string Label;

        /// <summary>本实例贡献的分（未计入时为 0；扣分为负）。</summary>
        public readonly int Score;

        /// <summary>计入状态。</summary>
        public readonly ScoreCountState State;

        /// <summary>与摆放建筑的最小距离（格）；无对象时为 0。</summary>
        public readonly float Distance;

        /// <summary>对应 <see cref="ScoreBreakdown.Lines"/> 的下标；未计入或该行被 0 分规则丢弃时为 -1。</summary>
        public readonly int LineIndex;

        public ScoreAttribution(
            ScoreSourceKind kind, int instanceId, string sourceId, string label,
            int score, ScoreCountState state, float distance, int lineIndex)
        {
            Kind = kind;
            InstanceId = instanceId;
            SourceId = sourceId;
            Label = label;
            Score = score;
            State = state;
            Distance = distance;
            LineIndex = lineIndex;
        }

        /// <summary>这条是否真的算进了总分。</summary>
        public bool Counted
        {
            get { return State == ScoreCountState.Counted; }
        }
    }

    /// <summary>一次摆放的完整得分明细。</summary>
    public sealed class ScoreBreakdown
    {
        private readonly List<ScoreLine> _lines = new List<ScoreLine>();
        private readonly List<ScoreAttribution> _attributions = new List<ScoreAttribution>();

        /// <summary>逐条明细，顺序即结算顺序。</summary>
        public IReadOnlyList<ScoreLine> Lines
        {
            get { return _lines; }
        }

        /// <summary>
        /// 逐实例归因，供表现层高亮"是谁给的分"。含未计入的实例（看 <see cref="ScoreAttribution.State"/>），
        /// 因为"在范围内但没算上"同样是玩家需要看到的信息。
        /// </summary>
        public IReadOnlyList<ScoreAttribution> Attributions
        {
            get { return _attributions; }
        }

        /// <summary>合计分。</summary>
        public int Total { get; private set; }

        /// <summary>
        /// 本次结算是否吃到了「接入物流网络」覆盖分（本地覆盖或物流风覆盖，同状态只会有一个）。
        /// App 层据此在风变更账本里登记，避免风变更时重复发同一笔覆盖分。
        /// </summary>
        public bool LogisticsCovered { get; internal set; }

        /// <summary>加一行明细，返回它在 <see cref="Lines"/> 里的下标；0 分行照旧丢弃并返回 -1。</summary>
        internal int Add(string label, int score)
        {
            if (score == 0)
            {
                return -1;
            }
            _lines.Add(new ScoreLine(label, score));
            Total += score;
            return _lines.Count - 1;
        }

        internal void Attribute(in ScoreAttribution attribution)
        {
            _attributions.Add(attribution);
        }

        /// <summary>是否一条明细都没有（除基础分外没吃到任何加成）。</summary>
        public bool IsEmpty
        {
            get { return _lines.Count == 0; }
        }

        /// <summary>
        /// 挑出指向具体对象的归因（<paramref name="countedOnly"/> = 只要真算进分的）。
        /// 返回条数，结果写进 <paramref name="result"/>——表现层每帧调，不要在这里分配。
        /// </summary>
        public int CollectAttributions(ScoreSourceKind kind, bool countedOnly, List<ScoreAttribution> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            result.Clear();
            for (int i = 0; i < _attributions.Count; i++)
            {
                ScoreAttribution attribution = _attributions[i];
                if (attribution.Kind != kind)
                {
                    continue;
                }
                if (countedOnly && !attribution.Counted)
                {
                    continue;
                }
                result.Add(attribution);
            }
            return result.Count;
        }
    }

    /// <summary>
    /// 即时建造分结算（GAME_DESIGN §7.1、PROJECT_BUILD §4.2 的结算顺序）：
    ///
    ///   基础分 → 地图元素（含巨型风车专属替代通用）→ 有向邻接（含计数上限）
    ///   → 同类惩罚 → 物流基础覆盖（去重 1 次）→ 风力即时分 → 孤立惩罚
    ///
    /// 「同类惩罚」和「物流专属邻接」不是独立通道：前者是 penaltyFrom 里来源等于自己的条目，
    /// 后者是 bonusFrom 里来源为 logisticsPoint/logisticsHub 的条目，都由有向邻接那一趟统一吃掉
    /// （配表 BuildingRelation 已按这个口径铺好，见 §15~§18 数据化）。
    ///
    /// 干跑与落地共用同一份代码：摆放前预览调 <see cref="Evaluate"/>，落地后同样调它记录实际得分，
    /// 两边永远不会算出不同的数。
    ///
    /// **只给"正在摆的这一栋"算分**：已落地建筑的分在它自己落地那一刻就定死了
    /// （<see cref="PlacedBuilding.InstantScore"/> 只读），后来在旁边建东西不会回头补分，
    /// 这是设计要求，也是玩家能提前规划的前提。
    /// </summary>
    public sealed class ScoreEngine
    {
        private readonly BuildBoard _board;
        private readonly BuildRuleSet _rules;

        /// <summary>复用的命中缓冲，避免每帧预览都分配。用完立刻消费，不跨通道保留。</summary>
        private readonly List<RangeHit> _hits = new List<RangeHit>(32);

        public ScoreEngine(BuildBoard board)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rules = board.Rules;
        }

        /// <summary>
        /// 算这次摆放能得多少分。纯查询，不改任何状态。
        /// 调用方要保证位置合法（<see cref="BuildBoard.CanPlace"/>）——非法位置也能算，但结果没有意义。
        /// </summary>
        public ScoreBreakdown Evaluate(BuildingBlueprint blueprint, int x, int z, int layer, Rotation rotation)
        {
            if (blueprint == null)
            {
                throw new ArgumentNullException(nameof(blueprint));
            }

            var breakdown = new ScoreBreakdown();
            var cells = new List<CellCoord>(blueprint.Footprint.CellCount);
            blueprint.Footprint.GetCells(x, z, rotation, cells);

            int baseLine = breakdown.Add("基础分", blueprint.BaseScore);
            breakdown.Attribute(new ScoreAttribution(
                ScoreSourceKind.Self, 0, blueprint.BuildingId, "基础分",
                blueprint.BaseScore, ScoreCountState.Counted, 0f, baseLine));

            bool gotAnyBonus = false;
            gotAnyBonus |= ScoreElements(breakdown, blueprint, cells, layer);
            gotAnyBonus |= ScoreNeighbours(breakdown, blueprint, cells, layer);
            ScorePenalties(breakdown, blueprint, cells, layer);
            gotAnyBonus |= ScoreLogisticsCoverage(breakdown, blueprint, cells, layer);
            gotAnyBonus |= ScoreWind(breakdown, blueprint, cells, layer);
            ScoreWindPassPenalty(breakdown, blueprint, cells, layer);

            // 孤立惩罚：范围内一个加分来源都没有（§12.8 船坞用）
            if (!gotAnyBonus && blueprint.IsolationPenaltyScore != 0)
            {
                const string label = "孤立惩罚（范围内无任何加分来源）";
                int line = breakdown.Add(label, blueprint.IsolationPenaltyScore);
                breakdown.Attribute(new ScoreAttribution(
                    ScoreSourceKind.Self, 0, blueprint.BuildingId, label,
                    blueprint.IsolationPenaltyScore, ScoreCountState.Counted, 0f, line));
            }

            return breakdown;
        }

        // ---------- 范围命中 ----------

        /// <summary>范围内命中的一个对象。建筑与元素共用一个结构，同一时刻只有一边非空。</summary>
        private readonly struct RangeHit
        {
            public readonly PlacedBuilding Building;
            public readonly PlacedElement Element;
            public readonly float Distance;

            /// <summary>量化后的距离，排序专用。</summary>
            public readonly int DistanceKey;

            public readonly int InstanceId;

            public RangeHit(PlacedBuilding building, PlacedElement element, float distance, int instanceId)
            {
                Building = building;
                Element = element;
                Distance = distance;
                DistanceKey = QuantizeDistance(distance);
                InstanceId = instanceId;
            }
        }

        /// <summary>
        /// 命中排序：近的在前，同距离按实例 Id 升序。
        ///
        /// 距离先量化成整数再比，**绝对不能拿 1e-4f 容差直接比 float**：
        /// 带容差的比较不满足传递性，<c>List.Sort</c> 会抛 "IComparer.Compare() method returns inconsistent results"，
        /// 而且是数据相关的偶发崩溃，极难复现。
        /// </summary>
        private static readonly Comparison<RangeHit> HitOrder = (a, b) =>
        {
            int byDistance = a.DistanceKey.CompareTo(b.DistanceKey);
            return byDistance != 0 ? byDistance : a.InstanceId.CompareTo(b.InstanceId);
        };

        private static int QuantizeDistance(float distance)
        {
            return (int)Math.Round(distance * 10000f, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// 收集范围内的同类元素，按 <see cref="HitOrder"/> 排好。
        ///
        /// radius 用**结算建筑自己的** <see cref="BuildingBlueprint.Radius"/>，与建筑邻接完全同一口径：
        /// 加分的条件是「这栋建筑的影响范围覆盖到了元素的地格」。元素自己的
        /// <see cref="MapElementDef.Radius"/> 不参与计分，它只管摆放合法性（如采矿站的 oreRange）。
        /// 这样表现层那个范围环（<c>RangeRingGeometry</c>，与 <see cref="RangeMath"/> 逐点相等）
        /// 就是玩家能看到的、唯一的计分边界——环外的巨型风车不会再莫名其妙地加分。
        /// </summary>
        private void CollectElementsInRange(string elementId, List<CellCoord> cells, int layer, int radius, List<RangeHit> hits)
        {
            hits.Clear();
            for (int i = 0; i < _board.Elements.Count; i++)
            {
                PlacedElement element = _board.Elements[i];
                if (!string.Equals(element.Def.ElementId, elementId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!RangeMath.InRange(cells, layer, element.Cells, element.Layer, radius, _rules.LayerHeightFactor, _board.LayerYGrids))
                {
                    continue;
                }
                float distance = RangeMath.MinDistance(cells, layer, element.Cells, element.Layer, _rules.LayerHeightFactor, _board.LayerYGrids);
                hits.Add(new RangeHit(null, element, distance, element.Id));
            }
            hits.Sort(HitOrder);
        }

        /// <summary>收集范围内的同类建筑。radius 由调用方给——邻接用摆放方自己的半径，物流覆盖用全局覆盖半径。</summary>
        private void CollectBuildingsInRange(string buildingId, List<CellCoord> cells, int layer, int radius, List<RangeHit> hits)
        {
            hits.Clear();
            for (int i = 0; i < _board.Buildings.Count; i++)
            {
                PlacedBuilding building = _board.Buildings[i];
                if (!string.Equals(building.Blueprint.BuildingId, buildingId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!RangeMath.InRange(cells, layer, building.Cells, building.Layer, radius, _rules.LayerHeightFactor, _board.LayerYGrids))
                {
                    continue;
                }
                float distance = RangeMath.MinDistance(cells, layer, building.Cells, building.Layer, _rules.LayerHeightFactor, _board.LayerYGrids);
                hits.Add(new RangeHit(building, null, distance, building.Id));
            }
            hits.Sort(HitOrder);
        }

        /// <summary>
        /// 把一批命中写成归因：前 <paramref name="counted"/> 个算进分，其余标成 <paramref name="skipState"/>。
        /// 排序已保证"近的优先算上"——被上限挡掉的一定是最远的那几个，这对玩家最直观。
        /// </summary>
        private static void AttributeHits(
            ScoreBreakdown breakdown, List<RangeHit> hits, int counted, int perInstanceScore,
            ScoreCountState skipState, ScoreSourceKind kind, string sourceId, string label, int lineIndex)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                RangeHit hit = hits[i];
                bool within = i < counted;
                breakdown.Attribute(new ScoreAttribution(
                    kind,
                    hit.InstanceId,
                    sourceId,
                    label,
                    within ? perInstanceScore : 0,
                    within ? ScoreCountState.Counted : skipState,
                    hit.Distance,
                    within ? lineIndex : -1));
            }
        }

        // ---------- 地图元素 ----------

        private bool ScoreElements(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            bool any = false;
            bool hasExclusiveGiantWindmillEntry = false;

            for (int i = 0; i < blueprint.ElementBonus.Count; i++)
            {
                ScoreSource entry = blueprint.ElementBonus[i];
                MapElementDef def = _rules.GetElementOrNull(entry.SourceId);
                if (def == null)
                {
                    continue;
                }

                bool isGiantWindmill = string.Equals(entry.SourceId, BuildRuleSet.GiantWindmillElementId, StringComparison.Ordinal);
                if (isGiantWindmill)
                {
                    hasExclusiveGiantWindmillEntry = true;
                }

                if (string.Equals(entry.SourceId, BuildRuleSet.AnchorElementId, StringComparison.Ordinal)
                    && string.Equals(blueprint.BuildingId, "dock", StringComparison.Ordinal))
                {
                    // 船坞的锚点收益走专门的归属 + 递减规则（§12.8），不是简单的「范围内每个 +N」
                    any |= ScoreDockAnchor(breakdown, entry, cells, layer, blueprint.Radius);
                    continue;
                }

                CollectElementsInRange(entry.SourceId, cells, layer, blueprint.Radius, _hits);
                if (_hits.Count == 0)
                {
                    continue;
                }

                // counted 只算一次，同时喂给算式和归因——两处各算一遍迟早会漂
                int counted = entry.MaxCount > 0 ? Math.Min(_hits.Count, entry.MaxCount) : _hits.Count;
                int score = entry.Score * counted;
                int line = breakdown.Add($"{def.NameCn} ×{counted}", score);
                AttributeHits(breakdown, _hits, counted, entry.Score,
                    ScoreCountState.OverMaxCount, ScoreSourceKind.Element, entry.SourceId, def.NameCn, line);
                any |= score > 0;
            }

            // 巨型风车通用加分：没配专属条目的建筑吃通用分，配了的用专属分替代不叠加（§6.3、决策 2/3）
            if (!hasExclusiveGiantWindmillEntry && _rules.GiantWindmillGenericScore != 0)
            {
                CollectElementsInRange(BuildRuleSet.GiantWindmillElementId, cells, layer, blueprint.Radius, _hits);
                if (_hits.Count > 0)
                {
                    int counted = _hits.Count;
                    int score = _rules.GiantWindmillGenericScore * counted;
                    int line = breakdown.Add($"巨型风车（通用）×{counted}", score);
                    AttributeHits(breakdown, _hits, counted, _rules.GiantWindmillGenericScore,
                        ScoreCountState.OverMaxCount, ScoreSourceKind.Element,
                        BuildRuleSet.GiantWindmillElementId, "巨型风车", line);
                    any |= score > 0;
                }
            }

            return any;
        }

        /// <summary>
        /// 船坞锚点收益（§12.8 + 决策 7）：每座船坞只归属一个锚点——范围内最近者优先，
        /// 距离相同时选当前归属船坞较少的那个；同一锚点下第 N 座按 anchorDockDecayPercents 递减。
        ///
        /// 「范围内」与 <see cref="CollectElementsInRange"/> 同口径：用船坞自己的 radius。
        /// </summary>
        private bool ScoreDockAnchor(ScoreBreakdown breakdown, ScoreSource entry, List<CellCoord> cells, int layer, int radius)
        {
            PlacedElement best = null;
            float bestDistance = float.PositiveInfinity;
            int bestDockCount = int.MaxValue;

            for (int i = 0; i < _board.Elements.Count; i++)
            {
                PlacedElement element = _board.Elements[i];
                if (!string.Equals(element.Def.ElementId, BuildRuleSet.AnchorElementId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (!RangeMath.InRange(cells, layer, element.Cells, element.Layer, radius, _rules.LayerHeightFactor, _board.LayerYGrids))
                {
                    continue;
                }

                float distance = RangeMath.MinDistance(cells, layer, element.Cells, element.Layer, _rules.LayerHeightFactor, _board.LayerYGrids);
                int dockCount = CountDocksOwnedBy(element);

                bool better = distance < bestDistance - 1e-4f
                              || (Math.Abs(distance - bestDistance) <= 1e-4f && dockCount < bestDockCount);
                if (better)
                {
                    best = element;
                    bestDistance = distance;
                    bestDockCount = dockCount;
                }
            }

            if (best == null)
            {
                return false;
            }

            float decay = _rules.AnchorDecayAt(bestDockCount);
            int score = (int)Math.Round(entry.Score * decay, MidpointRounding.AwayFromZero);
            if (score == 0)
            {
                string zeroLabel = $"锚点（第 {bestDockCount + 1} 座船坞，收益已归零）";
                breakdown.Add(zeroLabel, 0);
                // 分是 0，但这个锚点仍然是"你为什么没拿到分"的答案，必须能高亮出来
                breakdown.Attribute(new ScoreAttribution(
                    ScoreSourceKind.Element, best.Id, BuildRuleSet.AnchorElementId, best.Def.NameCn,
                    0, ScoreCountState.Decayed, bestDistance, -1));
                return false;
            }

            string label = $"锚点（第 {bestDockCount + 1} 座船坞 ×{decay:0.##}）";
            int line = breakdown.Add(label, score);
            breakdown.Attribute(new ScoreAttribution(
                ScoreSourceKind.Element, best.Id, BuildRuleSet.AnchorElementId, best.Def.NameCn,
                score, ScoreCountState.Counted, bestDistance, line));
            return score > 0;
        }

        /// <summary>已归属该锚点的船坞数。归属规则与 <see cref="ScoreDockAnchor"/> 一致，逐个反算。</summary>
        private int CountDocksOwnedBy(PlacedElement anchor)
        {
            int count = 0;
            for (int i = 0; i < _board.Buildings.Count; i++)
            {
                PlacedBuilding building = _board.Buildings[i];
                if (!string.Equals(building.Blueprint.BuildingId, "dock", StringComparison.Ordinal))
                {
                    continue;
                }
                if (ReferenceEquals(FindOwningAnchor(building), anchor))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>某座船坞归属哪个锚点（最近优先；同距离时选序号靠前者，保证反算稳定）。</summary>
        private PlacedElement FindOwningAnchor(PlacedBuilding dock)
        {
            PlacedElement best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < _board.Elements.Count; i++)
            {
                PlacedElement element = _board.Elements[i];
                if (!string.Equals(element.Def.ElementId, BuildRuleSet.AnchorElementId, StringComparison.Ordinal))
                {
                    continue;
                }
                // 半径用船坞自己的，与 ScoreDockAnchor 完全一致——两边不一致会让"第 N 座"数错，分直接算歪
                if (!RangeMath.InRange(dock.Cells, dock.Layer, element.Cells, element.Layer, dock.Blueprint.Radius, _rules.LayerHeightFactor, _board.LayerYGrids))
                {
                    continue;
                }

                float distance = RangeMath.MinDistance(dock.Cells, dock.Layer, element.Cells, element.Layer, _rules.LayerHeightFactor, _board.LayerYGrids);
                if (distance < bestDistance - 1e-4f)
                {
                    best = element;
                    bestDistance = distance;
                }
            }

            return best;
        }

        // ---------- 邻近建筑 ----------

        private bool ScoreNeighbours(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            // 风向标：居民区相关分按落点风力放大（§12.1，乘法只作用于居民区来源，避免全分放大）。
            // 只在建造那一刻取一次风力，之后风变不回头改分（用户定稿的一次性结算语义）。
            float residentMult = ResidentWindMultiplier(blueprint, cells, layer);

            bool any = false;
            for (int i = 0; i < blueprint.BonusFrom.Count; i++)
            {
                ScoreSource entry = blueprint.BonusFrom[i];
                CollectBuildingsInRange(entry.SourceId, cells, layer, blueprint.Radius, _hits);
                if (_hits.Count == 0)
                {
                    continue;
                }

                bool multiplied = residentMult != 1f
                    && string.Equals(entry.SourceId, BuildRuleSet.ResidenceBuildingId, StringComparison.Ordinal);
                int perInstance = multiplied
                    ? (int)Math.Round(entry.Score * residentMult, MidpointRounding.AwayFromZero)
                    : entry.Score;

                int counted = entry.MaxCount > 0 ? Math.Min(_hits.Count, entry.MaxCount) : _hits.Count;
                int score = perInstance * counted;
                string name = DisplayName(entry.SourceId);
                string label = multiplied
                    ? $"邻近 {name} ×{counted}（风力 ×{residentMult:0.##}）"
                    : $"邻近 {name} ×{counted}";
                int line = breakdown.Add(label, score);
                AttributeHits(breakdown, _hits, counted, perInstance,
                    ScoreCountState.OverMaxCount, ScoreSourceKind.Building, entry.SourceId, name, line);
                any |= score > 0;
            }
            return any;
        }

        /// <summary>风向标的居民区分倍率：落点风力取占地各格合成风力的最大值。无曲线/无风场时为 1。</summary>
        private float ResidentWindMultiplier(BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            IWindField wind = _board.WindField;
            if (wind == null || blueprint.ResidentWindMultByLevel.Count == 0)
            {
                return 1f;
            }

            int level = MaxWindForce(wind, cells, layer);
            if (level >= blueprint.ResidentWindMultByLevel.Count)
            {
                level = blueprint.ResidentWindMultByLevel.Count - 1;
            }
            float mult = blueprint.ResidentWindMultByLevel[level];
            return mult > 0f ? mult : 1f;
        }

        private void ScorePenalties(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            for (int i = 0; i < blueprint.PenaltyFrom.Count; i++)
            {
                ScoreSource entry = blueprint.PenaltyFrom[i];
                CollectBuildingsInRange(entry.SourceId, cells, layer, blueprint.Radius, _hits);
                if (_hits.Count == 0)
                {
                    continue;
                }

                int counted = entry.MaxCount > 0 ? Math.Min(_hits.Count, entry.MaxCount) : _hits.Count;
                bool sameType = string.Equals(entry.SourceId, blueprint.BuildingId, StringComparison.Ordinal);
                string name = DisplayName(entry.SourceId);
                string label = sameType
                    ? $"同类拥挤 {name} ×{counted}"
                    : $"邻近 {name} ×{counted}";
                int line = breakdown.Add(label, -entry.Score * counted);
                AttributeHits(breakdown, _hits, counted, -entry.Score,
                    ScoreCountState.OverMaxCount, ScoreSourceKind.Building, entry.SourceId, name, line);
            }
        }

        // ---------- 物流基础覆盖 ----------

        /// <summary>
        /// 物流点基础覆盖分：范围内有物流点即得，**每建筑只计一次**（§10.3，不随物流点数量叠加）。
        /// 与 BuildingRelation 里的物流专属邻接分可以叠加，那部分已在有向邻接通道结算。
        /// </summary>
        private bool ScoreLogisticsCoverage(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            if (!blueprint.CanLogisticsCover || _rules.LogisticsBaseCoverScore == 0 || _rules.LogisticsCoverRadius <= 0)
            {
                return false;
            }

            // 覆盖半径是物流点的能力，用全局 coverRadius 而不是被覆盖建筑自己的 radius
            CollectBuildingsInRange(BuildRuleSet.LogisticsPointBuildingId, cells, layer, _rules.LogisticsCoverRadius, _hits);
            if (_hits.Count == 0)
            {
                // 本地覆盖没吃到，再看物流风：风把覆盖传播到物流点本地范围外（§10.4），
                // 建在物流风覆盖带里的建筑同样获得「已接入物流」，同状态不重复计分。
                IWindField wind = _board.WindField;
                if (wind == null
                    || !wind.HasLogisticsWindNear(cells, layer, _rules.LogisticsCoverRadius, _rules.LayerHeightFactor))
                {
                    return false;
                }

                const string windLabel = "已接入物流网络（物流风）";
                int windLine = breakdown.Add(windLabel, _rules.LogisticsBaseCoverScore);
                breakdown.Attribute(new ScoreAttribution(
                    ScoreSourceKind.Wind, 0, "wind", windLabel,
                    _rules.LogisticsBaseCoverScore, ScoreCountState.Counted, 0f, windLine));
                breakdown.LogisticsCovered = true;
                return true;
            }

            const string label = "已接入物流网络";
            int line = breakdown.Add(label, _rules.LogisticsBaseCoverScore);
            // 只计一次：最近那个物流点吃下这份分，其余标 Duplicated——
            // 它们确实覆盖到了，只是不叠加，玩家看到"灰的"比看不到更清楚
            AttributeHits(breakdown, _hits, 1, _rules.LogisticsBaseCoverScore,
                ScoreCountState.Duplicated, ScoreSourceKind.Building,
                BuildRuleSet.LogisticsPointBuildingId, DisplayName(BuildRuleSet.LogisticsPointBuildingId), line);
            breakdown.LogisticsCovered = true;
            return true;
        }

        // ---------- 风力 ----------

        /// <summary>
        /// 风力即时分。风场未接入（<see cref="BuildBoard.WindField"/> 为 null）时整块跳过：
        /// 拿 0 级风冒充「没有风场」会让船坞永远吃到 -150 的无风惩罚，那是缺数据不是设计。
        /// </summary>
        private bool ScoreWind(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            IWindField wind = _board.WindField;
            if (wind == null || blueprint.WindScoreByLevel.Count == 0)
            {
                return false;
            }

            int level = MaxWindForce(wind, cells, layer);
            if (level >= blueprint.WindScoreByLevel.Count)
            {
                level = blueprint.WindScoreByLevel.Count - 1;
            }

            int score = blueprint.WindScoreByLevel[level];
            string label = $"风力 {level} 级";
            int line = breakdown.Add(label, score);
            breakdown.Attribute(new ScoreAttribution(
                ScoreSourceKind.Wind, 0, "wind", label, score, ScoreCountState.Counted, 0f, line));
            return score > 0;
        }

        /// <summary>
        /// 强风穿过惩罚（居民区 §12.13）：只在风路直接经过占地格时生效——合成风力就挂在
        /// 被经过的格子上，取占地内最大值查曲线即可。曲线值本身为负（低级别为 0）。
        /// 与其他风分同口径：只在建造那一刻结算，之后风变不追溯。
        /// </summary>
        private void ScoreWindPassPenalty(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            IWindField wind = _board.WindField;
            if (wind == null || blueprint.WindPassPenaltyByLevel.Count == 0)
            {
                return;
            }

            int level = MaxWindForce(wind, cells, layer);
            if (level >= blueprint.WindPassPenaltyByLevel.Count)
            {
                level = blueprint.WindPassPenaltyByLevel.Count - 1;
            }

            int score = blueprint.WindPassPenaltyByLevel[level];
            if (score == 0)
            {
                return;
            }

            string label = $"强风穿过（{level} 级）";
            int line = breakdown.Add(label, score);
            breakdown.Attribute(new ScoreAttribution(
                ScoreSourceKind.Wind, 0, "wind", label, score, ScoreCountState.Counted, 0f, line));
        }

        /// <summary>占地各格合成风力的最大值。</summary>
        private static int MaxWindForce(IWindField wind, List<CellCoord> cells, int layer)
        {
            int level = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                int force = wind.GetForce(cells[i].X, cells[i].Z, layer);
                if (force > level)
                {
                    level = force;
                }
            }
            return level;
        }

        private string DisplayName(string buildingId)
        {
            for (int i = 0; i < _rules.Blueprints.Count; i++)
            {
                BuildingBlueprint blueprint = _rules.Blueprints[i];
                if (string.Equals(blueprint.BuildingId, buildingId, StringComparison.Ordinal))
                {
                    return blueprint.NameCn;
                }
            }
            return buildingId;
        }
    }
}
