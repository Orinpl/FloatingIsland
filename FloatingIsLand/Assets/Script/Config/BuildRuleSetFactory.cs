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

                // 显示名以变体为准：一个模板挂多个变体时（居民区有方形/L 形/凹形三种占地），
                // 全用模板名会让手牌上三张牌长得一模一样。变体没填就沿用模板名。
                string nameCn = string.IsNullOrEmpty(variant.nameCn) ? building.nameCn : variant.nameCn;

                blueprints.Add(new BuildingBlueprint(
                    variant.variantId,
                    variant.buildingId,
                    nameCn,
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
                    building.windScoreByLevel,
                    building.windPassPenaltyByLevel,
                    building.residentWindMultByLevel));
            }

            return new BuildRuleSet(
                blueprints,
                elements,
                Tables.GameConfig.layerHeightFactor,
                Tables.GameConfig.giantWindmillGenericScore,
                Tables.GameConfig.anchorDockDecayPercents,
                Tables.LogisticsConfig.coverRadius,
                Tables.LogisticsConfig.baseCoverScore,
                maxWindLevel: Tables.WindConfig.maxWindLevel,
                initialWindLevelMin: Tables.WindConfig.initialWindLevelMin,
                initialWindLevelMax: Tables.WindConfig.initialWindLevelMax,
                initialWindLengthMin: Tables.WindConfig.initialWindLengthMin,
                initialWindLengthMax: Tables.WindConfig.initialWindLengthMax,
                windExtendLength: Tables.LogisticsConfig.windExtendLength,
                windExtendMaxPerWind: Tables.LogisticsConfig.windExtendMaxPerWind,
                logisticsWindLinkScore: Tables.LogisticsConfig.windLinkScore);
        }

        /// <summary>装配 Level 表（一关内的建筑组序列）。</summary>
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
                    row.unlockScore,
                    row.groupCount,
                    row.groupSizeMin,
                    row.groupSizeMax,
                    ReadForcedThemes(row)));
            }
            return levels;
        }

        /// <summary>装配 Stage 表（3 关的关卡配置）。</summary>
        public static List<StageDef> CreateStages()
        {
            if (!Tables.IsLoaded)
            {
                throw new InvalidOperationException("配表尚未加载，不能装配关卡表。");
            }

            var stages = new List<StageDef>(Tables.Stage.Count);
            foreach (StageRow row in Tables.Stage.All)
            {
                stages.Add(new StageDef(
                    row.stageId,
                    row.nameCn,
                    row.groupCount,
                    row.clearScore,
                    row.unlockScoreMult));
            }
            return stages;
        }

        /// <summary>按关卡 Id 装配单关配置；表里没有该关返回 null。</summary>
        public static StageDef CreateStage(int stageId)
        {
            foreach (StageDef stage in CreateStages())
            {
                if (stage.StageId == stageId)
                {
                    return stage;
                }
            }
            return null;
        }

        /// <summary>一局共几关（Stage 表行数）。</summary>
        public static int StageCount()
        {
            if (!Tables.IsLoaded)
            {
                throw new InvalidOperationException("配表尚未加载，不能读关卡数。");
            }
            return Tables.Stage.Count;
        }

        /// <summary>装配 BuildingGroupTheme 表（选组用的主题配方）。</summary>
        public static List<GroupThemeDef> CreateGroupThemes()
        {
            if (!Tables.IsLoaded)
            {
                throw new InvalidOperationException("配表尚未加载，不能装配建筑组主题表。");
            }

            var themes = new List<GroupThemeDef>(Tables.BuildingGroupTheme.Count);
            foreach (BuildingGroupThemeRow row in Tables.BuildingGroupTheme.All)
            {
                themes.Add(new GroupThemeDef(
                    row.themeId,
                    row.nameCn,
                    row.minLevel,
                    row.maxLevel,
                    row.weight,
                    row.maxPerRun,
                    ParseThemeMembers(row)));
            }
            return themes;
        }

        /// <summary>解析主题成员配方 "变体Id:权重[:最少[:最多]]"。</summary>
        private static List<ThemeMember> ParseThemeMembers(BuildingGroupThemeRow row)
        {
            var members = new List<ThemeMember>();
            if (row.members == null)
            {
                return members;
            }

            for (int i = 0; i < row.members.Length; i++)
            {
                string cell = row.members[i];
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                string context = $"BuildingGroupTheme[{row.themeId}].members 条目 '{cell}'";
                string[] parts = cell.Split(':');
                if (parts.Length < 2 || parts.Length > 4)
                {
                    throw new InvalidOperationException(
                        $"{context} 格式非法（应为 变体Id:权重[:最少[:最多]]）。");
                }

                string variantId = parts[0].Trim();
                if (Tables.BuildingVariant.GetOrNull(variantId) == null)
                {
                    throw new InvalidOperationException($"{context} 的变体 Id 在 BuildingVariant 表里不存在。");
                }

                int weight = ParseNonNegative(parts[1], context, "权重");
                int minCount = parts.Length > 2 ? ParseNonNegative(parts[2], context, "最少数量") : 0;
                int maxCount = parts.Length > 3 ? ParseNonNegative(parts[3], context, "最多数量") : 0;
                if (maxCount > 0 && minCount > maxCount)
                {
                    throw new InvalidOperationException($"{context} 的最少数量大于最多数量。");
                }

                members.Add(new ThemeMember(variantId, weight, minCount, maxCount));
            }
            return members;
        }

        /// <summary>解析 Level.themes（可空；填了就是本级写死的主题列表）。</summary>
        private static List<string> ReadForcedThemes(LevelRow row)
        {
            var themeIds = new List<string>();
            if (row.themes == null)
            {
                return themeIds;
            }

            for (int i = 0; i < row.themes.Length; i++)
            {
                string themeId = (row.themes[i] ?? string.Empty).Trim();
                if (themeId.Length == 0)
                {
                    continue;
                }
                if (Tables.BuildingGroupTheme.GetOrNull(themeId) == null)
                {
                    throw new InvalidOperationException(
                        $"Level[{row.level}].themes 的主题 '{themeId}' 在 BuildingGroupTheme 表里不存在。");
                }
                themeIds.Add(themeId);
            }
            return themeIds;
        }

        private static int ParseNonNegative(string text, string context, string fieldName)
        {
            int value;
            if (!int.TryParse(text.Trim(), out value) || value < 0)
            {
                throw new InvalidOperationException($"{context} 的{fieldName}不是非负整数。");
            }
            return value;
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
