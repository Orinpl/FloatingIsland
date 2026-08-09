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

    /// <summary>一次摆放的完整得分明细。</summary>
    public sealed class ScoreBreakdown
    {
        private readonly List<ScoreLine> _lines = new List<ScoreLine>();

        /// <summary>逐条明细，顺序即结算顺序。</summary>
        public IReadOnlyList<ScoreLine> Lines
        {
            get { return _lines; }
        }

        /// <summary>合计分。</summary>
        public int Total { get; private set; }

        internal void Add(string label, int score)
        {
            if (score == 0)
            {
                return;
            }
            _lines.Add(new ScoreLine(label, score));
            Total += score;
        }

        /// <summary>是否一条明细都没有（除基础分外没吃到任何加成）。</summary>
        public bool IsEmpty
        {
            get { return _lines.Count == 0; }
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
    /// </summary>
    public sealed class ScoreEngine
    {
        private readonly BuildBoard _board;
        private readonly BuildRuleSet _rules;

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

            breakdown.Add("基础分", blueprint.BaseScore);

            bool gotAnyBonus = false;
            gotAnyBonus |= ScoreElements(breakdown, blueprint, cells, layer);
            gotAnyBonus |= ScoreNeighbours(breakdown, blueprint, cells, layer);
            ScorePenalties(breakdown, blueprint, cells, layer);
            gotAnyBonus |= ScoreLogisticsCoverage(breakdown, blueprint, cells, layer);
            gotAnyBonus |= ScoreWind(breakdown, blueprint, cells, layer);

            // 孤立惩罚：范围内一个加分来源都没有（§12.8 船坞用）
            if (!gotAnyBonus && blueprint.IsolationPenaltyScore != 0)
            {
                breakdown.Add("孤立惩罚（范围内无任何加分来源）", blueprint.IsolationPenaltyScore);
            }

            return breakdown;
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
                    any |= ScoreDockAnchor(breakdown, entry, cells, layer);
                    continue;
                }

                int count = CountElementsInRange(entry.SourceId, cells, layer);
                if (count == 0)
                {
                    continue;
                }
                if (entry.MaxCount > 0 && count > entry.MaxCount)
                {
                    count = entry.MaxCount;
                }

                int score = entry.Score * count;
                breakdown.Add($"{def.NameCn} ×{count}", score);
                any |= score > 0;
            }

            // 巨型风车通用加分：没配专属条目的建筑吃通用分，配了的用专属分替代不叠加（§6.3、决策 2/3）
            if (!hasExclusiveGiantWindmillEntry && _rules.GiantWindmillGenericScore != 0)
            {
                int count = CountElementsInRange(BuildRuleSet.GiantWindmillElementId, cells, layer);
                if (count > 0)
                {
                    int score = _rules.GiantWindmillGenericScore * count;
                    breakdown.Add($"巨型风车（通用）×{count}", score);
                    any |= score > 0;
                }
            }

            return any;
        }

        /// <summary>
        /// 船坞锚点收益（§12.8 + 决策 7）：每座船坞只归属一个锚点——范围内最近者优先，
        /// 距离相同时选当前归属船坞较少的那个；同一锚点下第 N 座按 anchorDockDecayPercents 递减。
        /// </summary>
        private bool ScoreDockAnchor(ScoreBreakdown breakdown, ScoreSource entry, List<CellCoord> cells, int layer)
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
                if (!RangeMath.InRange(cells, layer, element.Cells, element.Layer, element.Def.Radius, _rules.LayerHeightFactor))
                {
                    continue;
                }

                float distance = RangeMath.MinDistance(cells, layer, element.Cells, element.Layer, _rules.LayerHeightFactor);
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
                breakdown.Add($"锚点（第 {bestDockCount + 1} 座船坞，收益已归零）", 0);
                return false;
            }

            breakdown.Add($"锚点（第 {bestDockCount + 1} 座船坞 ×{decay:0.##}）", score);
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
                if (!RangeMath.InRange(dock.Cells, dock.Layer, element.Cells, element.Layer, element.Def.Radius, _rules.LayerHeightFactor))
                {
                    continue;
                }

                float distance = RangeMath.MinDistance(dock.Cells, dock.Layer, element.Cells, element.Layer, _rules.LayerHeightFactor);
                if (distance < bestDistance - 1e-4f)
                {
                    best = element;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private int CountElementsInRange(string elementId, List<CellCoord> cells, int layer)
        {
            int count = 0;
            for (int i = 0; i < _board.Elements.Count; i++)
            {
                PlacedElement element = _board.Elements[i];
                if (!string.Equals(element.Def.ElementId, elementId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (RangeMath.InRange(cells, layer, element.Cells, element.Layer, element.Def.Radius, _rules.LayerHeightFactor))
                {
                    count++;
                }
            }
            return count;
        }

        // ---------- 邻近建筑 ----------

        private bool ScoreNeighbours(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            bool any = false;
            for (int i = 0; i < blueprint.BonusFrom.Count; i++)
            {
                ScoreSource entry = blueprint.BonusFrom[i];
                int count = CountBuildingsInRange(entry.SourceId, cells, layer, blueprint.Radius);
                if (count == 0)
                {
                    continue;
                }
                if (entry.MaxCount > 0 && count > entry.MaxCount)
                {
                    count = entry.MaxCount;
                }

                int score = entry.Score * count;
                breakdown.Add($"邻近 {DisplayName(entry.SourceId)} ×{count}", score);
                any |= score > 0;
            }
            return any;
        }

        private void ScorePenalties(ScoreBreakdown breakdown, BuildingBlueprint blueprint, List<CellCoord> cells, int layer)
        {
            for (int i = 0; i < blueprint.PenaltyFrom.Count; i++)
            {
                ScoreSource entry = blueprint.PenaltyFrom[i];
                int count = CountBuildingsInRange(entry.SourceId, cells, layer, blueprint.Radius);
                if (count == 0)
                {
                    continue;
                }
                if (entry.MaxCount > 0 && count > entry.MaxCount)
                {
                    count = entry.MaxCount;
                }

                bool sameType = string.Equals(entry.SourceId, blueprint.BuildingId, StringComparison.Ordinal);
                string label = sameType
                    ? $"同类拥挤 {DisplayName(entry.SourceId)} ×{count}"
                    : $"邻近 {DisplayName(entry.SourceId)} ×{count}";
                breakdown.Add(label, -entry.Score * count);
            }
        }

        private int CountBuildingsInRange(string buildingId, List<CellCoord> cells, int layer, int radius)
        {
            int count = 0;
            for (int i = 0; i < _board.Buildings.Count; i++)
            {
                PlacedBuilding building = _board.Buildings[i];
                if (!string.Equals(building.Blueprint.BuildingId, buildingId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (RangeMath.InRange(cells, layer, building.Cells, building.Layer, radius, _rules.LayerHeightFactor))
                {
                    count++;
                }
            }
            return count;
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

            for (int i = 0; i < _board.Buildings.Count; i++)
            {
                PlacedBuilding building = _board.Buildings[i];
                if (!string.Equals(building.Blueprint.BuildingId, BuildRuleSet.LogisticsPointBuildingId, StringComparison.Ordinal))
                {
                    continue;
                }
                // 覆盖半径是物流点的能力，用全局 coverRadius 而不是被覆盖建筑自己的 radius
                if (RangeMath.InRange(cells, layer, building.Cells, building.Layer, _rules.LogisticsCoverRadius, _rules.LayerHeightFactor))
                {
                    breakdown.Add("已接入物流网络", _rules.LogisticsBaseCoverScore);
                    return true;
                }
            }

            return false;
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

            int level = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                int force = wind.GetForce(cells[i].X, cells[i].Z, layer);
                if (force > level)
                {
                    level = force;
                }
            }

            if (level >= blueprint.WindScoreByLevel.Count)
            {
                level = blueprint.WindScoreByLevel.Count - 1;
            }

            int score = blueprint.WindScoreByLevel[level];
            breakdown.Add($"风力 {level} 级", score);
            return score > 0;
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
