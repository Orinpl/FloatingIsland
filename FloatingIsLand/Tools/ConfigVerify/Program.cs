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

            if (!ValidateBuildingRelations() || !ValidateElementBonuses() || !ValidateFootprints() || !ValidateLevelPools())
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
        /// 校验 Level 抽取池：条目格式须为 变体Id:数量（数量为正整数），变体 Id 须存在于 BuildingVariant 表。
        /// 反射软依赖，表/列缺失时跳过。
        /// </summary>
        private static bool ValidateLevelPools()
        {
            object variantTable = typeof(Tables).GetProperty("BuildingVariant", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            object levelTable = typeof(Tables).GetProperty("Level", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (variantTable == null || levelTable == null)
            {
                Console.WriteLine("[验证] 跳过 Level 抽取池校验（表不存在）。");
                return true;
            }

            var variantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (object row in (System.Collections.IEnumerable)variantTable.GetType().GetProperty("All").GetValue(variantTable))
            {
                variantIds.Add((string)row.GetType().GetField("variantId").GetValue(row));
            }

            int refCount = 0;
            var errors = new List<string>();
            foreach (object row in (System.Collections.IEnumerable)levelTable.GetType().GetProperty("All").GetValue(levelTable))
            {
                Type rowType = row.GetType();
                FieldInfo poolField = rowType.GetField("pool");
                if (poolField == null)
                {
                    Console.WriteLine("[验证] 跳过 Level 抽取池校验（无 pool 列）。");
                    return true;
                }
                int level = (int)rowType.GetField("level").GetValue(row);
                string[] pool = (string[])poolField.GetValue(row) ?? new string[0];
                foreach (string entry in pool)
                {
                    refCount++;
                    string context = $"Level[{level}].pool";
                    string[] parts = entry.Split(':');
                    int count;
                    if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out count))
                    {
                        errors.Add($"{context}: 条目 '{entry}' 格式非法（应为 变体Id:数量，如 residence_01:2）");
                        continue;
                    }
                    if (count < 1)
                    {
                        errors.Add($"{context}: 条目 '{entry}' 数量须为正整数");
                    }
                    if (!variantIds.Contains(parts[0].Trim()))
                    {
                        errors.Add($"{context}: 变体 '{parts[0].Trim()}' 在 BuildingVariant 表中不存在");
                    }
                }
            }

            if (errors.Count > 0)
            {
                foreach (string error in errors)
                {
                    Console.Error.WriteLine("[验证] " + error);
                }
                Console.Error.WriteLine($"[验证] Level 抽取池校验失败：{errors.Count} 个错误。");
                return false;
            }

            Console.WriteLine($"[验证] Level 抽取池校验通过：{refCount} 个变体引用均合法。");
            return true;
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
