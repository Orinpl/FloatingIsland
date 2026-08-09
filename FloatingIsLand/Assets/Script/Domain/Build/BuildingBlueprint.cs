using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>建造地形限制（配表 Building.placement）。</summary>
    public enum PlacementRule
    {
        /// <summary>普通合法地块：占用格全部刷过地形即可。</summary>
        Any = 0,

        /// <summary>必须建在绿地（农田 §12.3）。</summary>
        GreenField = 1,

        /// <summary>必须建在风带（风帆 §12.5）。风系统为 M3，见 <see cref="BuildRuleSet.WindEnabled"/>。</summary>
        WindPath = 2,

        /// <summary>只能建在浮空区域（船坞 §12.8）。</summary>
        FloatingZone = 3,

        /// <summary>必须位于矿藏有效范围内（采矿站 §12.6）。</summary>
        OreRange = 4,
    }

    /// <summary>加/扣分来源条目的领域表示（对应配表 BuildingRelation 的 来源Id:分值[:上限]）。</summary>
    public readonly struct ScoreSource
    {
        /// <summary>来源 Id（建筑 Id 或元素 Id，视所在集合而定）。</summary>
        public readonly string SourceId;

        /// <summary>分值。加分为正；扣分条目同样填正数，由结算方取负。</summary>
        public readonly int Score;

        /// <summary>最多计入的来源个数；0 = 不限。</summary>
        public readonly int MaxCount;

        public ScoreSource(string sourceId, int score, int maxCount)
        {
            SourceId = sourceId;
            Score = score;
            MaxCount = maxCount;
        }
    }

    /// <summary>
    /// 一个可摆放建筑变体的完整规则（配表 Building + BuildingVariant + BuildingRelation 三表合成）。
    /// 纯数据：领域层不认识配表类型，由 Config 层的蓝图工厂装配后注入。
    /// </summary>
    public sealed class BuildingBlueprint
    {
        /// <summary>变体 Id（= BuildingVariant.variantId），摆放与表现的粒度。</summary>
        public string VariantId { get; }

        /// <summary>建筑模板 Id（= Building.buildingId），计分与关系的粒度。</summary>
        public string BuildingId { get; }

        /// <summary>显示名。</summary>
        public string NameCn { get; }

        /// <summary>分类（UI 分组用）。</summary>
        public string Category { get; }

        /// <summary>占地掩码。</summary>
        public Footprint Footprint { get; }

        /// <summary>地形限制。</summary>
        public PlacementRule Placement { get; }

        /// <summary>作用半径（格，球形欧氏，自占地边缘起算）。</summary>
        public int Radius { get; }

        /// <summary>基础分（落地即得）。</summary>
        public int BaseScore { get; }

        /// <summary>是否可获得基础物流覆盖分。</summary>
        public bool CanLogisticsCover { get; }

        /// <summary>孤立惩罚（负值；范围内无任何加分来源时生效，0=不启用）。</summary>
        public int IsolationPenaltyScore { get; }

        /// <summary>表现 Prefab 路径（Resources 相对，空=白模占位）。</summary>
        public string PrefabPath { get; }

        /// <summary>地图元素加分条目。</summary>
        public IReadOnlyList<ScoreSource> ElementBonus { get; }

        /// <summary>邻近建筑加分条目（有向：本建筑因谁而加分）。</summary>
        public IReadOnlyList<ScoreSource> BonusFrom { get; }

        /// <summary>邻近建筑扣分条目（分值为正，结算时取负）。</summary>
        public IReadOnlyList<ScoreSource> PenaltyFrom { get; }

        /// <summary>风力直接得分曲线，索引 = 风力 0~5 级；空 = 不吃风力分。</summary>
        public IReadOnlyList<int> WindScoreByLevel { get; }

        public BuildingBlueprint(
            string variantId,
            string buildingId,
            string nameCn,
            string category,
            Footprint footprint,
            PlacementRule placement,
            int radius,
            int baseScore,
            bool canLogisticsCover,
            int isolationPenaltyScore,
            string prefabPath,
            IReadOnlyList<ScoreSource> elementBonus,
            IReadOnlyList<ScoreSource> bonusFrom,
            IReadOnlyList<ScoreSource> penaltyFrom,
            IReadOnlyList<int> windScoreByLevel)
        {
            if (string.IsNullOrEmpty(variantId))
            {
                throw new ArgumentException("变体 Id 为空。", nameof(variantId));
            }

            VariantId = variantId;
            BuildingId = buildingId;
            NameCn = nameCn;
            Category = category;
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            Placement = placement;
            Radius = radius;
            BaseScore = baseScore;
            CanLogisticsCover = canLogisticsCover;
            IsolationPenaltyScore = isolationPenaltyScore;
            PrefabPath = prefabPath ?? string.Empty;
            ElementBonus = elementBonus ?? Array.Empty<ScoreSource>();
            BonusFrom = bonusFrom ?? Array.Empty<ScoreSource>();
            PenaltyFrom = penaltyFrom ?? Array.Empty<ScoreSource>();
            WindScoreByLevel = windScoreByLevel ?? Array.Empty<int>();
        }
    }

    /// <summary>一个地图元素的规则（配表 MapElement 的领域表示）。</summary>
    public sealed class MapElementDef
    {
        /// <summary>元素 Id。</summary>
        public string ElementId { get; }

        /// <summary>显示名。</summary>
        public string NameCn { get; }

        /// <summary>占地掩码。</summary>
        public Footprint Footprint { get; }

        /// <summary>有效范围（格，自占地边缘起算；地形类为 0）。</summary>
        public int Radius { get; }

        /// <summary>是否为可刷地形（TRUE = 1×1 区域填充，走地形层不走元素层）。</summary>
        public bool IsTerrain { get; }

        /// <summary>生成数量下限。</summary>
        public int CountMin { get; }

        /// <summary>生成数量上限。</summary>
        public int CountMax { get; }

        /// <summary>表现 Prefab 路径（Resources 相对，空=无独立模型）。</summary>
        public string PrefabPath { get; }

        public MapElementDef(
            string elementId, string nameCn, Footprint footprint,
            int radius, bool isTerrain, int countMin, int countMax, string prefabPath)
        {
            if (string.IsNullOrEmpty(elementId))
            {
                throw new ArgumentException("元素 Id 为空。", nameof(elementId));
            }

            ElementId = elementId;
            NameCn = nameCn;
            Footprint = footprint ?? throw new ArgumentNullException(nameof(footprint));
            Radius = radius;
            IsTerrain = isTerrain;
            CountMin = countMin;
            CountMax = countMax;
            PrefabPath = prefabPath ?? string.Empty;
        }
    }
}
