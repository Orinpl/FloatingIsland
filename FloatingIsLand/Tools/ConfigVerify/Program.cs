using System;
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
            Console.WriteLine("[验证] 通过：读表层 + 生成代码编译正常，全部 JSON 反序列化成功。");
            return 0;
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
