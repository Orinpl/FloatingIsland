using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// 配表 → 领域规则集的唯一装配口。
    ///
    /// 领域层（Game.Domain）刻意不引用 Game.Config：规则对象是纯数据，才能脱离 Unity 与 Excel 单测。
    /// 所以「读表」这件事全部收在这里，Building + BuildingVariant + BuildingRelation 三表
    /// 合成一个 <see cref="BuildingBlueprint"/>，MapElement 表合成 <see cref="MapElementDef"/>。
    ///
    /// 调用前必须已 <see cref="TableLoader.IsLoaded"/>（局外 Boot 状态里加载）。
    /// </summary>
    public static class BuildRuleSetFactory
    {
        /// <summary>按当前已加载的配表装配规则集。任一表数据非法当场抛——规则是全局单例，坏数据不能带病上路。</summary>
        public static BuildRuleSet Create()
        {
            if (!Tables.IsLoaded)
            {
                throw new InvalidOperationException(
                    "配表尚未加载，不能装配建造规则集（局外 Boot 状态会调 UnityTableLoader.LoadFromResources）。");
            }

            var elements = new List<MapElementDef>(Tables.MapElement.Count);
            foreach (MapElementRow row in Tables.MapElement.All)
            {
                elements.Add(new MapElementDef(
                    row.elementId,
                    row.nameCn,
                    Footprint.Parse(row.footprint, $"MapElement[{row.elementId}].footprint"),
                    row.radius,
                    row.isTerrain,
                    row.countMin,
                    row.countMax,
                    row.prefabPath));
            }

            var blueprints = new List<BuildingBlueprint>(Tables.BuildingVariant.Count);
            foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
            {
                BuildingRow building = Tables.Building.GetOrNull(variant.buildingId);
                if (building == null)
                {
                    throw new InvalidOperationException(
                        $"BuildingVariant[{variant.variantId}].buildingId = '{variant.buildingId}' 在 Building 表里不存在。");
                }

                BuildingRelationRow relation = Tables.BuildingRelation.GetOrNull(variant.buildingId);

                blueprints.Add(new BuildingBlueprint(
                    variant.variantId,
                    variant.buildingId,
                    building.nameCn,
                    building.category,
                    Footprint.Parse(variant.footprint, $"BuildingVariant[{variant.variantId}].footprint"),
                    ParsePlacement(building.placement, variant.buildingId),
                    building.radius,
                    building.baseScore,
                    building.canLogisticsCover,
                    building.isolationPenaltyScore,
                    variant.prefabPath,
                    ToScoreSources(building.elementBonus, $"Building[{variant.buildingId}].elementBonus"),
                    ToScoreSources(relation?.bonusFrom, $"BuildingRelation[{variant.buildingId}].bonusFrom"),
                    ToScoreSources(relation?.penaltyFrom, $"BuildingRelation[{variant.buildingId}].penaltyFrom"),
                    building.windScoreByLevel));
            }

            return new BuildRuleSet(
                blueprints,
                elements,
                Tables.GameConfig.layerHeightFactor,
                Tables.GameConfig.giantWindmillGenericScore,
                Tables.GameConfig.anchorDockDecayPercents,
                Tables.LogisticsConfig.coverRadius,
                Tables.LogisticsConfig.baseCoverScore,
                Tables.GameConfig.scoreToGoldRatio,
                // 风系统是 M3（WIND_IMPL）。接入后这里改成读风场是否就绪，
                // 在那之前整块风相关规则不参与结算——详见 BuildRuleSet.WindEnabled。
                false);
        }

        /// <summary>装配 Level 表（局内 20 级进度）。</summary>
        public static List<LevelDef> CreateLevels()
        {
            if (!Tables.IsLoaded)
            {
                throw new InvalidOperationException("配表尚未加载，不能装配等级表。");
            }

            var levels = new List<LevelDef>(Tables.Level.Count);
            foreach (LevelRow row in Tables.Level.All)
            {
                levels.Add(new LevelDef(
                    row.level,
                    row.unlockCost,
                    row.groupCount,
                    row.groupSizeMin,
                    row.groupSizeMax,
                    ExpandPool(row)));
            }
            return levels;
        }

        /// <summary>把 "变体Id:份数" 展开成一份一个元素的抽取池，抽取时直接均匀取下标即可。</summary>
        private static List<string> ExpandPool(LevelRow row)
        {
            var pool = new List<string>();
            if (row.pool == null)
            {
                return pool;
            }

            for (int i = 0; i < row.pool.Length; i++)
            {
                string cell = row.pool[i];
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                string[] parts = cell.Split(':');
                if (parts.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"Level[{row.level}].pool 条目 '{cell}' 格式非法（应为 变体Id:份数）。");
                }

                string variantId = parts[0].Trim();
                int count;
                if (!int.TryParse(parts[1].Trim(), out count) || count <= 0)
                {
                    throw new InvalidOperationException(
                        $"Level[{row.level}].pool 条目 '{cell}' 的份数不是正整数。");
                }
                if (Tables.BuildingVariant.GetOrNull(variantId) == null)
                {
                    throw new InvalidOperationException(
                        $"Level[{row.level}].pool 条目 '{cell}' 的变体 Id 在 BuildingVariant 表里不存在。");
                }

                for (int n = 0; n < count; n++)
                {
                    pool.Add(variantId);
                }
            }
            return pool;
        }

        private static PlacementRule ParsePlacement(string placement, string buildingId)
        {
            switch (placement)
            {
                case "any":
                case "":
                case null:
                    return PlacementRule.Any;
                case "greenField":
                    return PlacementRule.GreenField;
                case "windPath":
                    return PlacementRule.WindPath;
                case "floatingZone":
                    return PlacementRule.FloatingZone;
                case "oreRange":
                    return PlacementRule.OreRange;
                default:
                    throw new InvalidOperationException(
                        $"Building[{buildingId}].placement = '{placement}' 未知（应为 any/greenField/windPath/floatingZone/oreRange）。");
            }
        }

        private static List<ScoreSource> ToScoreSources(string[] cells, string context)
        {
            List<RelationEntry> entries = RelationEntry.ParseAll(cells, context);
            var result = new List<ScoreSource>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                RelationEntry entry = entries[i];
                result.Add(new ScoreSource(entry.SourceId, entry.Score, entry.MaxCount));
            }
            return result;
        }
    }
}
