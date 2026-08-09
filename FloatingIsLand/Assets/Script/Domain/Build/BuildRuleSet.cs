using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>
    /// 一局游戏的全部建造/计分规则：蓝图目录 + 元素目录 + 全局数值。
    /// 由 Config 层从配表装配一次后只读共享（<see cref="FloatingIsLand.Config"/> 的蓝图工厂）。
    /// 领域层的所有校验与结算都只认这个对象，不认配表类型，因此可脱离 Unity 单测。
    /// </summary>
    public sealed class BuildRuleSet
    {
        private readonly Dictionary<string, BuildingBlueprint> _byVariantId;
        private readonly Dictionary<string, MapElementDef> _byElementId;
        private readonly BuildingBlueprint[] _blueprints;
        private readonly MapElementDef[] _elements;

        /// <summary>全部建筑变体蓝图，顺序与配表一致。</summary>
        public IReadOnlyList<BuildingBlueprint> Blueprints
        {
            get { return _blueprints; }
        }

        /// <summary>全部地图元素定义，顺序与配表一致。</summary>
        public IReadOnlyList<MapElementDef> Elements
        {
            get { return _elements; }
        }

        /// <summary>层高折算系数 k（GameConfig.layerHeightFactor），球形范围判定用。</summary>
        public float LayerHeightFactor { get; }

        /// <summary>巨型风车通用加分：范围内任意建筑获得；有专属条目的建筑用专属分替代不叠加（§6.3）。</summary>
        public int GiantWindmillGenericScore { get; }

        /// <summary>巨型风车的元素 Id，通用加分靠它识别。</summary>
        public const string GiantWindmillElementId = "giantWindmill";

        /// <summary>锚点元素 Id，船坞归属与收益递减靠它识别。</summary>
        public const string AnchorElementId = "anchor";

        /// <summary>矿藏元素 Id，采矿站的 oreRange 建造限制靠它识别。</summary>
        public const string OreElementId = "ore";

        /// <summary>绿地地形 Id。</summary>
        public const string GreenFieldTerrainId = "greenField";

        /// <summary>浮空区域地形 Id。</summary>
        public const string FloatingZoneTerrainId = "floatingZone";

        /// <summary>物流点建筑 Id，基础覆盖分靠它识别。</summary>
        public const string LogisticsPointBuildingId = "logisticsPoint";

        /// <summary>同一锚点下第 N 座船坞的锚点收益倍率（§12.8：1|0.5|0.25|0）。</summary>
        public IReadOnlyList<float> AnchorDockDecayPercents { get; }

        /// <summary>物流点本地覆盖半径（格）。</summary>
        public int LogisticsCoverRadius { get; }

        /// <summary>基础物流覆盖分（每建筑仅计一次）。</summary>
        public int LogisticsBaseCoverScore { get; }

        /// <summary>正分转金币比例。</summary>
        public float ScoreToGoldRatio { get; }

        /// <summary>
        /// 风系统是否已接入。M3 之前恒为 false：风场未实现时若按「0 级风」结算，
        /// 船坞会永远吃到 -150 的无风惩罚（§12.8 的曲线首项），那不是设计意图而是缺数据，
        /// 因此未接入时整块风力项直接不参与结算，并在明细里注明。
        /// </summary>
        public bool WindEnabled { get; }

        public BuildRuleSet(
            IReadOnlyList<BuildingBlueprint> blueprints,
            IReadOnlyList<MapElementDef> elements,
            float layerHeightFactor,
            int giantWindmillGenericScore,
            IReadOnlyList<float> anchorDockDecayPercents,
            int logisticsCoverRadius,
            int logisticsBaseCoverScore,
            float scoreToGoldRatio,
            bool windEnabled)
        {
            IReadOnlyList<BuildingBlueprint> blueprintSource = blueprints ?? Array.Empty<BuildingBlueprint>();
            _blueprints = new BuildingBlueprint[blueprintSource.Count];
            _byVariantId = new Dictionary<string, BuildingBlueprint>(blueprintSource.Count, StringComparer.Ordinal);
            for (int i = 0; i < blueprintSource.Count; i++)
            {
                BuildingBlueprint blueprint = blueprintSource[i];
                if (_byVariantId.ContainsKey(blueprint.VariantId))
                {
                    throw new ArgumentException($"建筑变体 Id 重复：{blueprint.VariantId}。");
                }
                _byVariantId.Add(blueprint.VariantId, blueprint);
                _blueprints[i] = blueprint;
            }

            IReadOnlyList<MapElementDef> elementSource = elements ?? Array.Empty<MapElementDef>();
            _elements = new MapElementDef[elementSource.Count];
            _byElementId = new Dictionary<string, MapElementDef>(elementSource.Count, StringComparer.Ordinal);
            for (int i = 0; i < elementSource.Count; i++)
            {
                MapElementDef element = elementSource[i];
                if (_byElementId.ContainsKey(element.ElementId))
                {
                    throw new ArgumentException($"地图元素 Id 重复：{element.ElementId}。");
                }
                _byElementId.Add(element.ElementId, element);
                _elements[i] = element;
            }

            LayerHeightFactor = layerHeightFactor > 0f ? layerHeightFactor : 1f;
            GiantWindmillGenericScore = giantWindmillGenericScore;
            AnchorDockDecayPercents = anchorDockDecayPercents ?? Array.Empty<float>();
            LogisticsCoverRadius = logisticsCoverRadius;
            LogisticsBaseCoverScore = logisticsBaseCoverScore;
            ScoreToGoldRatio = scoreToGoldRatio;
            WindEnabled = windEnabled;
        }

        /// <summary>按变体 Id 取蓝图；不存在返回 null。</summary>
        public BuildingBlueprint GetBlueprintOrNull(string variantId)
        {
            if (string.IsNullOrEmpty(variantId))
            {
                return null;
            }
            BuildingBlueprint blueprint;
            return _byVariantId.TryGetValue(variantId, out blueprint) ? blueprint : null;
        }

        /// <summary>按元素 Id 取定义；不存在返回 null。</summary>
        public MapElementDef GetElementOrNull(string elementId)
        {
            if (string.IsNullOrEmpty(elementId))
            {
                return null;
            }
            MapElementDef element;
            return _byElementId.TryGetValue(elementId, out element) ? element : null;
        }

        /// <summary>同一锚点下第 ordinal 座船坞（0 起）的收益倍率；超出配表长度按 0 处理（第 4 座及以后没有收益）。</summary>
        public float AnchorDecayAt(int ordinal)
        {
            if (ordinal < 0)
            {
                return 0f;
            }
            return ordinal < AnchorDockDecayPercents.Count ? AnchorDockDecayPercents[ordinal] : 0f;
        }
    }
}
