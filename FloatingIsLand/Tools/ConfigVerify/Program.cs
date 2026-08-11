using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FloatingIsLand.Config;

namespace ConfigVerify
{
    /// <summary>
    /// 配表冒烟验证：加载 Assets/Resources/Tables 下全部 JSON，逐表打印行数。
    /// 不硬编码任何表名（全靠 Tables.AllTableNames + 反射），加表删表都不用改这里。
    /// 任一表 JSON 缺失 / 反序列化失败 / 主键重复，都会在这里抛异常并返回非零退出码。
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string jsonDir = args.Length > 0 ? args[0] : FindJsonDir();
            if (jsonDir == null)
            {
                Console.Error.WriteLine("[验证] 找不到 Assets/Resources/Tables，请先转表：dotnet run --project Tools/TableTool -- convert");
                return 1;
            }

            try
            {
                TableLoader.LoadFromDirectory(jsonDir);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("[验证] 加载失败：" + e.Message);
                return 1;
            }

            Console.WriteLine($"[验证] 已加载：{jsonDir}");
            Console.WriteLine($"[验证] 共 {Tables.AllTableNames.Length} 张表");
            foreach (string name in Tables.AllTableNames)
            {
                Console.WriteLine($"  - {name,-20} {Describe(name)}");
            }

            if (!ValidateBuildingRelations() || !ValidateElementBonuses() || !ValidateFootprints()
                || !ValidateGroupThemes() || !ValidateStages())
            {
                return 1;
            }

            Console.WriteLine("[验证] 通过：读表层 + 生成代码编译正常，全部 JSON 反序列化成功。");
            return 0;
        }

        /// <summary>
        /// 校验 BuildingRelation 的打包单元格（来源Id:分值[:上限[:半径]]）：格式合法 + 建筑 Id 外键存在。
        /// 用反射软依赖：表或列不存在时跳过而不是报错，避免后续改表结构把本工程改坏。
        /// </summary>
        private static bool ValidateBuildingRelations()
        {
            object buildingTable = typeof(Tables).GetProperty("Building", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object relationTable = typeof(Tables).GetProperty("BuildingRelation", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (buildingTable == null || relationTable == null)
            {
                Console.WriteLine("[验证] 跳过 BuildingRelation 校验（表不存在）。");
                return true;
            }

            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (object row in (System.Collections.IEnumerable)buildingTable.GetType().GetProperty("All").GetValue(buildingTable))
            {
                buildingIds.Add((string)row.GetType().GetField("buildingId").GetValue(row));
            }

            int entryCount = 0;
            var errors = new List<string>();
            foreach (object row in (System.Collections.IEnumerable)relationTable.GetType().GetProperty("All").GetValue(relationTable))
            {
                Type rowType = row.GetType();
                FieldInfo keyField = rowType.GetField("buildingId");
                if (keyField == null)
                {
                    Console.WriteLine("[验证] 跳过 BuildingRelation 校验（无 buildingId 列，表结构与校验器不匹配）。");
                    return true;
                }
                string key = (string)keyField.GetValue(row);
                if (!buildingIds.Contains(key))
                {
                    errors.Add($"BuildingRelation[{key}]: 主键在 Building 表中不存在");
                }

                foreach (string column in new[] { "bonusFrom", "penaltyFrom" })
                {
                    FieldInfo field = rowType.GetField(column);
                    if (field == null)
                    {
                        continue;
                    }
                    string context = $"BuildingRelation[{key}].{column}";
                    try
                    {
                        foreach (RelationEntry entry in RelationEntry.ParseAll((string[])field.GetValue(row), context))
                        {
                            entryCount++;
                            if (!buildingIds.Contains(entry.SourceId))
                            {
                                errors.Add($"{context}: 来源建筑 '{entry.SourceId}' 在 Building 表中不存在");
                            }
                        }
                    }
                    catch (FormatException e)
                    {
                        errors.Add(e.Message);
                    }
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("[验证] " + error);
                }
                Console.Error.WriteLine($"[验证] BuildingRelation 校验失败：{errors.Count} 个错误。");
                return false;
            }

            Console.WriteLine($"[验证] BuildingRelation 校验通过：{entryCount} 条关系条目格式与外键均合法。");
            return true;
        }

        /// <summary>
        /// 校验 Building.elementBonus 打包单元格（元素Id:分值[:上限]）：格式合法 + 元素 Id 存在于 MapElement 表。
        /// 反射软依赖，表/列缺失时跳过。
        /// </summary>
        private static bool ValidateElementBonuses()
        {
            object buildingTable = typeof(Tables).GetProperty("Building", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object elementTable = typeof(Tables).GetProperty("MapElement", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (buildingTable == null || elementTable == null)
            {
                Console.WriteLine("[验证] 跳过 elementBonus 校验（表不存在）。");
                return true;
            }

            var elementIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (object row in (System.Collections.IEnumerable)elementTable.GetType().GetProperty("All").GetValue(elementTable))
            {
                elementIds.Add((string)row.GetType().GetField("elementId").GetValue(row));
            }

            int entryCount = 0;
            var errors = new List<string>();
            foreach (object row in (System.Collections.IEnumerable)buildingTable.GetType().GetProperty("All").GetValue(buildingTable))
            {
                Type rowType = row.GetType();
                FieldInfo field = rowType.GetField("elementBonus");
                if (field == null)
                {
                    Console.WriteLine("[验证] 跳过 elementBonus 校验（Building 表无该列）。");
                    return true;
                }
                string key = (string)rowType.GetField("buildingId").GetValue(row);
                string context = $"Building[{key}].elementBonus";
                try
                {
                    foreach (RelationEntry entry in RelationEntry.ParseAll((string[])field.GetValue(row), context))
                    {
                        entryCount++;
                        if (!elementIds.Contains(entry.SourceId))
                        {
                            errors.Add($"{context}: 地图元素 '{entry.SourceId}' 在 MapElement 表中不存在");
                        }
                    }
                }
                catch (FormatException e)
                {
                    errors.Add(e.Message);
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("[验证] " + error);
                }
                Console.Error.WriteLine($"[验证] elementBonus 校验失败：{errors.Count} 个错误。");
                return false;
            }

            Console.WriteLine($"[验证] elementBonus 校验通过：{entryCount} 条元素加分条目格式与外键均合法。");
            return true;
        }

        /// <summary>
        /// 校验占地掩码（BuildingVariant.footprint / MapElement.footprint）：各行长度一致、只含 #/.、至少一个 #；
        /// 以及 BuildingVariant.buildingId 外键存在。同样反射软依赖，表/列缺失时跳过。
        /// </summary>
        private static bool ValidateFootprints()
        {
            var errors = new List<string>();

            object buildingTable = typeof(Tables).GetProperty("Building", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            if (buildingTable != null)
            {
                foreach (object row in (System.Collections.IEnumerable)buildingTable.GetType().GetProperty("All").GetValue(buildingTable))
                {
                    buildingIds.Add((string)row.GetType().GetField("buildingId").GetValue(row));
                }
            }

            int maskCount = 0;
            foreach (string tableName in new[] { "BuildingVariant", "MapElement" })
            {
                object table = typeof(Tables).GetProperty(tableName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (table == null)
                {
                    continue;
                }
                foreach (object row in (System.Collections.IEnumerable)table.GetType().GetProperty("All").GetValue(table))
                {
                    Type rowType = row.GetType();
                    string key = (string)rowType.GetField(rowType.GetField("variantId") != null ? "variantId" : "elementId").GetValue(row);
                    string context = $"{tableName}[{key}]";

                    FieldInfo ownerField = rowType.GetField("buildingId");
                    if (ownerField != null && buildingIds.Count > 0)
                    {
                        string owner = (string)ownerField.GetValue(row);
                        if (!buildingIds.Contains(owner))
                        {
                            errors.Add($"{context}: 所属建筑 '{owner}' 在 Building 表中不存在");
                        }
                    }

                    FieldInfo maskField = rowType.GetField("footprint");
                    if (maskField == null)
                    {
                        continue;
                    }
                    maskCount++;
                    ValidateMask((string[])maskField.GetValue(row), context, errors);
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("[验证] " + error);
                }
                Console.Error.WriteLine($"[验证] 占地掩码校验失败：{errors.Count} 个错误。");
                return false;
            }

            Console.WriteLine($"[验证] 占地掩码校验通过：{maskCount} 个 footprint 形状合法。");
            return true;
        }

        /// <summary>
        /// 校验建筑组主题（BuildingGroupTheme + Level.themes）。除了格式与外键，这里还把
        /// 「相互加分的放一组、相互扣分的分开放」这条设计约束变成硬校验——靠人肉记关系表迟早会漏：
        ///
        /// - **协同**：组内每个建筑至少要和组内另一个建筑（或同类的自己）存在一条 bonusFrom 有向边，
        ///   否则它在这组里就是无关的搭头。完全没有关系条目的建筑（风帆靠风力分吃饭）豁免。
        /// - **互斥**：组内不许出现异类 penaltyFrom 边（居民区被采矿站/工坊扣分 → 不能同组）。
        ///   同类自扣（采矿站扎堆互扣）是设计要的空间取舍，不算错。
        ///
        /// 另外校验每级候选主题数够不够 groupCount，不够会导致同一级出两组同主题。
        /// 反射软依赖，表/列缺失时跳过。
        /// </summary>
        private static bool ValidateGroupThemes()
        {
            List<Dictionary<string, object>> themeRows = ReadTable("BuildingGroupTheme");
            List<Dictionary<string, object>> levelRows = ReadTable("Level");
            List<Dictionary<string, object>> variantRows = ReadTable("BuildingVariant");
            if (themeRows == null || levelRows == null || variantRows == null)
            {
                Console.WriteLine("[验证] 跳过建筑组主题校验（表不存在）。");
                return true;
            }

            var variantToBuilding = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Dictionary<string, object> row in variantRows)
            {
                variantToBuilding[Str(row, "variantId")] = Str(row, "buildingId");
            }

            Dictionary<string, HashSet<string>> bonusFrom = ReadRelationSets("bonusFrom");
            Dictionary<string, HashSet<string>> penaltyFrom = ReadRelationSets("penaltyFrom");

            var errors = new List<string>();

            // 组大小上限取全表最小值：主题可能出现在任何一级，保底数量必须在最紧的那一级也塞得下
            int groupSizeMaxFloor = int.MaxValue;
            foreach (Dictionary<string, object> row in levelRows)
            {
                groupSizeMaxFloor = Math.Min(groupSizeMaxFloor, Int(row, "groupSizeMax"));
            }

            var themesById = new Dictionary<string, ParsedTheme>(StringComparer.Ordinal);
            foreach (Dictionary<string, object> row in themeRows)
            {
                ParsedTheme theme = ParseTheme(row, variantToBuilding, groupSizeMaxFloor, errors);
                themesById[theme.ThemeId] = theme;
                ValidateThemeCohesion(theme, bonusFrom, penaltyFrom, errors);
            }

            foreach (Dictionary<string, object> row in levelRows)
            {
                int level = Int(row, "level");
                int groupCount = Int(row, "groupCount");
                string[] forced = Arr(row, "themes");

                var candidates = new List<string>();
                foreach (string raw in forced)
                {
                    string themeId = (raw ?? string.Empty).Trim();
                    if (themeId.Length == 0)
                    {
                        continue;
                    }
                    if (!themesById.ContainsKey(themeId))
                    {
                        errors.Add($"Level[{level}].themes: 主题 '{themeId}' 在 BuildingGroupTheme 表中不存在");
                        continue;
                    }
                    candidates.Add(themeId);
                }

                if (candidates.Count == 0)
                {
                    foreach (ParsedTheme theme in themesById.Values)
                    {
                        if (theme.IsAvailableAt(level))
                        {
                            candidates.Add(theme.ThemeId);
                        }
                    }
                }

                if (candidates.Count < groupCount)
                {
                    errors.Add(
                        $"Level[{level}]: 本级可用主题只有 {candidates.Count} 个，凑不满 groupCount={groupCount} 组"
                        + "（会退化成同一级出两组同主题，检查各主题的 minLevel/maxLevel 是否铺满 20 级）");
                }

                // 带 maxPerRun 的主题是「地标额度」，用完就退出候选池。一级里若全是带额度的主题，
                // 玩家用完额度后这一级就只能靠运行时兜底（忽略额度）撑着——那等于额度形同虚设。
                bool anyUncapped = false;
                foreach (string themeId in candidates)
                {
                    if (themesById[themeId].MaxPerRun <= 0)
                    {
                        anyUncapped = true;
                        break;
                    }
                }
                if (candidates.Count > 0 && !anyUncapped)
                {
                    errors.Add(
                        $"Level[{level}]: 本级候选主题全部带 maxPerRun 额度，额度用完这一级就没有常规主题可发"
                        + "（至少留一个 maxPerRun=0 的主题托底）");
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("[验证] " + error);
                }
                Console.Error.WriteLine($"[验证] 建筑组主题校验失败：{errors.Count} 个错误。");
                return false;
            }

            Console.WriteLine(
                $"[验证] 建筑组主题校验通过：{themesById.Count} 个主题，成员配方格式/外键、组内协同与互斥、每级候选数均合法。");
            return true;
        }

        /// <summary>
        /// 校验关卡表：组数不能超过 Level 表能提供的组、通关分与门槛倍率必须为正，
        /// 且**第 1 组必须免费**（Level[1].unlockScore = 0）——否则玩家开局就卡死，
        /// 一分没有却要求有分才能拿第一组建筑。反射软依赖，表/列缺失时跳过。
        /// </summary>
        private static bool ValidateStages()
        {
            List<Dictionary<string, object>> stageRows = ReadTable("Stage");
            List<Dictionary<string, object>> levelRows = ReadTable("Level");
            if (stageRows == null || levelRows == null)
            {
                Console.WriteLine("[验证] 跳过关卡校验（表不存在）。");
                return true;
            }

            var errors = new List<string>();

            // 第 1 组免费：这是「首组建筑免费」的唯一数据出处
            foreach (Dictionary<string, object> row in levelRows)
            {
                if (Int(row, "level") == 1 && Int(row, "unlockScore") != 0)
                {
                    errors.Add($"Level[1].unlockScore = {Int(row, "unlockScore")}，第 1 组必须免费（填 0）");
                }
            }

            // 门槛必须随组序单调不减，否则「越往后越贵」的承诺就破了
            var byLevel = new List<Dictionary<string, object>>(levelRows);
            byLevel.Sort((a, b) => Int(a, "level").CompareTo(Int(b, "level")));
            for (int i = 1; i < byLevel.Count; i++)
            {
                int prev = Int(byLevel[i - 1], "unlockScore");
                int cur = Int(byLevel[i], "unlockScore");
                if (cur < prev)
                {
                    errors.Add(
                        $"Level[{Int(byLevel[i], "level")}].unlockScore = {cur} 比上一组的 {prev} 还低"
                        + "（组解锁门槛必须逐组递增）");
                }
            }

            foreach (Dictionary<string, object> row in stageRows)
            {
                int stageId = Int(row, "stageId");
                string context = $"Stage[{stageId}]";
                int groupCount = Int(row, "groupCount");
                if (groupCount <= 0)
                {
                    errors.Add($"{context}: groupCount 须为正整数");
                }
                else if (groupCount > levelRows.Count)
                {
                    errors.Add(
                        $"{context}: groupCount = {groupCount} 超过 Level 表的 {levelRows.Count} 行"
                        + "（本关会在组数用完前就没组可发）");
                }
                if (Int(row, "clearScore") <= 0)
                {
                    errors.Add($"{context}: clearScore 须为正整数（通关门槛不能是 0，那样开局即通关）");
                }
                if (Flt(row, "unlockScoreMult") <= 0f)
                {
                    errors.Add($"{context}: unlockScoreMult 须为正数（1 = 直接用 Level 表的门槛曲线）");
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("[验证] " + error);
                }
                Console.Error.WriteLine($"[验证] 关卡校验失败：{errors.Count} 个错误。");
                return false;
            }

            Console.WriteLine($"[验证] 关卡校验通过：{stageRows.Count} 关，组数/通关分/门槛倍率与首组免费均合法。");
            return true;
        }

        private sealed class ParsedMember
        {
            public string VariantId = "";
            public string BuildingId = "";
            public int Weight;
            public int MinCount;
            public int MaxCount;
        }

        private sealed class ParsedTheme
        {
            public string ThemeId = "";
            public int MinLevel;
            public int MaxLevel;
            public int MaxPerRun;
            public readonly List<ParsedMember> Members = new List<ParsedMember>();

            public bool IsAvailableAt(int level)
            {
                if (MinLevel > 0 && level < MinLevel)
                {
                    return false;
                }
                return MaxLevel <= 0 || level <= MaxLevel;
            }
        }

        private static ParsedTheme ParseTheme(
            Dictionary<string, object> row,
            Dictionary<string, string> variantToBuilding,
            int groupSizeMaxFloor,
            List<string> errors)
        {
            var theme = new ParsedTheme
            {
                ThemeId = Str(row, "themeId"),
                MinLevel = Int(row, "minLevel"),
                MaxLevel = Int(row, "maxLevel"),
                MaxPerRun = Int(row, "maxPerRun"),
            };
            string context = $"BuildingGroupTheme[{theme.ThemeId}]";

            if (Int(row, "weight") <= 0)
            {
                errors.Add($"{context}: weight 须为正整数（0 权重的主题永远抽不到）");
            }
            if (theme.MaxPerRun < 0)
            {
                errors.Add($"{context}: maxPerRun 不能为负（0 = 不限）");
            }
            if (theme.MaxLevel > 0 && theme.MinLevel > theme.MaxLevel)
            {
                errors.Add($"{context}: minLevel {theme.MinLevel} 大于 maxLevel {theme.MaxLevel}");
            }

            foreach (string cell in Arr(row, "members"))
            {
                if (string.IsNullOrWhiteSpace(cell))
                {
                    continue;
                }

                string entryContext = $"{context}.members 条目 '{cell}'";
                string[] parts = cell.Split(':');
                if (parts.Length < 2 || parts.Length > 4)
                {
                    errors.Add($"{entryContext}: 格式非法（应为 变体Id:权重[:最少[:最多]]）");
                    continue;
                }

                var member = new ParsedMember { VariantId = parts[0].Trim() };
                if (!variantToBuilding.TryGetValue(member.VariantId, out member.BuildingId))
                {
                    errors.Add($"{entryContext}: 变体 '{member.VariantId}' 在 BuildingVariant 表中不存在");
                    continue;
                }

                if (!TryNonNegative(parts[1], out member.Weight)
                    || (parts.Length > 2 && !TryNonNegative(parts[2], out member.MinCount))
                    || (parts.Length > 3 && !TryNonNegative(parts[3], out member.MaxCount)))
                {
                    errors.Add($"{entryContext}: 权重/最少/最多须为非负整数");
                    continue;
                }
                if (member.MaxCount > 0 && member.MinCount > member.MaxCount)
                {
                    errors.Add($"{entryContext}: 最少 {member.MinCount} 大于最多 {member.MaxCount}");
                    continue;
                }

                theme.Members.Add(member);
            }

            if (theme.Members.Count == 0)
            {
                errors.Add($"{context}: members 为空（主题至少要有一个成员建筑）");
                return theme;
            }

            int minTotal = 0;
            bool anyWeight = false;
            foreach (ParsedMember member in theme.Members)
            {
                minTotal += member.MinCount;
                anyWeight |= member.Weight > 0;
            }
            if (!anyWeight)
            {
                errors.Add($"{context}: 全部成员权重都是 0，随机名额无从分配（至少留一个正权重成员）");
            }
            if (groupSizeMaxFloor != int.MaxValue && minTotal > groupSizeMaxFloor)
            {
                errors.Add(
                    $"{context}: 成员保底数量之和 {minTotal} 超过 Level.groupSizeMax 的最小值 {groupSizeMaxFloor}"
                    + "（保底优先于组大小，会撑出超规格的一组）");
            }

            return theme;
        }

        /// <summary>组内协同与互斥：本次改版的核心约束，见 <see cref="ValidateGroupThemes"/> 的说明。</summary>
        private static void ValidateThemeCohesion(
            ParsedTheme theme,
            Dictionary<string, HashSet<string>> bonusFrom,
            Dictionary<string, HashSet<string>> penaltyFrom,
            List<string> errors)
        {
            var buildingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ParsedMember member in theme.Members)
            {
                if (member.BuildingId.Length > 0)
                {
                    buildingIds.Add(member.BuildingId);
                }
            }
            string context = $"BuildingGroupTheme[{theme.ThemeId}]";

            foreach (string self in buildingIds)
            {
                foreach (string source in Sources(penaltyFrom, self))
                {
                    // 同类自扣（扎堆惩罚）是设计要的空间取舍，只拦异类互扣
                    if (!string.Equals(source, self, StringComparison.Ordinal) && buildingIds.Contains(source))
                    {
                        errors.Add(
                            $"{context}: '{self}' 会被同组的 '{source}' 扣分（BuildingRelation.penaltyFrom），"
                            + "互相扣分的建筑必须分属不同主题");
                    }
                }

                bool hasAnyRelation = Sources(bonusFrom, self).Count > 0 || Sources(penaltyFrom, self).Count > 0;
                if (!hasAnyRelation)
                {
                    continue; // 例如风帆：全靠风力分，本来就没有邻接关系，不参与协同判据
                }

                bool linked = false;
                foreach (string source in Sources(bonusFrom, self))
                {
                    if (buildingIds.Contains(source))
                    {
                        linked = true;
                        break;
                    }
                }
                if (!linked)
                {
                    foreach (string other in buildingIds)
                    {
                        if (Sources(bonusFrom, other).Contains(self))
                        {
                            linked = true;
                            break;
                        }
                    }
                }

                if (!linked)
                {
                    errors.Add(
                        $"{context}: '{self}' 和组内其它建筑之间没有任何加分关系，"
                        + "同组建筑必须互相加分（改配方或给 BuildingRelation 补一条边）");
                }
            }
        }

        private static HashSet<string> Sources(Dictionary<string, HashSet<string>> map, string buildingId)
        {
            HashSet<string> set;
            return map.TryGetValue(buildingId, out set) ? set : EmptySources;
        }

        private static readonly HashSet<string> EmptySources = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>读 BuildingRelation 的一列，摊成 建筑Id → 来源建筑Id 集合。</summary>
        private static Dictionary<string, HashSet<string>> ReadRelationSets(string column)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            List<Dictionary<string, object>> rows = ReadTable("BuildingRelation");
            if (rows == null)
            {
                return result;
            }

            foreach (Dictionary<string, object> row in rows)
            {
                string key = Str(row, "buildingId");
                var sources = new HashSet<string>(StringComparer.Ordinal);
                try
                {
                    foreach (RelationEntry entry in RelationEntry.ParseAll(Arr(row, column), $"BuildingRelation[{key}].{column}"))
                    {
                        sources.Add(entry.SourceId);
                    }
                }
                catch (FormatException)
                {
                    // 格式错误由 ValidateBuildingRelations 报，这里只管取到能取的部分
                }
                result[key] = sources;
            }
            return result;
        }

        /// <summary>把一张表反射成 行 → 字段名/值 的字典，表或 All 属性不存在返回 null（软依赖）。</summary>
        private static List<Dictionary<string, object>> ReadTable(string tableName)
        {
            object table = typeof(Tables).GetProperty(tableName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            PropertyInfo all = table?.GetType().GetProperty("All");
            if (all == null)
            {
                return null;
            }

            var rows = new List<Dictionary<string, object>>();
            foreach (object row in (System.Collections.IEnumerable)all.GetValue(table))
            {
                var fields = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (FieldInfo field in row.GetType().GetFields())
                {
                    fields[field.Name] = field.GetValue(row);
                }
                rows.Add(fields);
            }
            return rows;
        }

        private static string Str(Dictionary<string, object> row, string field)
        {
            object value;
            return row.TryGetValue(field, out value) && value is string s ? s : string.Empty;
        }

        private static int Int(Dictionary<string, object> row, string field)
        {
            object value;
            return row.TryGetValue(field, out value) && value is int i ? i : 0;
        }

        private static float Flt(Dictionary<string, object> row, string field)
        {
            object value;
            return row.TryGetValue(field, out value) && value is float f ? f : 0f;
        }

        private static string[] Arr(Dictionary<string, object> row, string field)
        {
            object value;
            return row.TryGetValue(field, out value) && value is string[] a ? a : new string[0];
        }

        private static bool TryNonNegative(string text, out int value)
        {
            return int.TryParse(text.Trim(), out value) && value >= 0;
        }

        private static void ValidateMask(string[] mask, string context, List<string> errors)
        {
            if (mask == null || mask.Length == 0)
            {
                errors.Add($"{context}: footprint 为空（至少要有一行，如 1×1 填 #）");
                return;
            }

            int width = mask[0]?.Length ?? 0;
            bool hasSolid = false;
            for (int i = 0; i < mask.Length; i++)
            {
                string line = mask[i] ?? "";
                if (line.Length == 0 || line.Length != width)
                {
                    errors.Add($"{context}: footprint 第 {i + 1} 行 '{line}' 长度与首行不一致（各行长度必须相同，用 . 补空位）");
                    return;
                }
                foreach (char c in line)
                {
                    if (c == '#')
                    {
                        hasSolid = true;
                    }
                    else if (c != '.')
                    {
                        errors.Add($"{context}: footprint 含非法字符 '{c}'（只允许 # 和 .）");
                        return;
                    }
                }
            }

            if (!hasSolid)
            {
                errors.Add($"{context}: footprint 没有任何 # 占用格");
            }
        }

        /// <summary>行表打印行数，单例参数组打印字段数。</summary>
        private static string Describe(string tableName)
        {
            PropertyInfo prop = typeof(Tables).GetProperty(tableName, BindingFlags.Public | BindingFlags.Static);
            if (prop == null)
            {
                return "(Tables 上没有同名属性？)";
            }

            object value = prop.GetValue(null);
            if (value == null)
            {
                return "(null——未加载？)";
            }

            PropertyInfo count = value.GetType().GetProperty("Count");
            if (count != null)
            {
                return $"行表 {count.GetValue(value)} 行";
            }
            return $"单例参数组 {value.GetType().GetFields().Length} 个参数";
        }

        /// <summary>从当前目录与程序目录向上找 Unity 工程根（含 Assets/ 与 Tools/）下的 JSON 目录。</summary>
        private static string FindJsonDir()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, "Assets", "Resources", "Tables");
                    if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(dir.FullName, "Tools")))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }
    }
}
