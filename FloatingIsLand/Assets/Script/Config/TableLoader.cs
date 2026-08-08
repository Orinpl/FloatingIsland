using System;
using System.IO;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// 配表加载入口（纯 C#，无 UnityEngine 依赖）。
    /// 实际的逐表加载逻辑在生成代码 Tables.g.cs 的 Tables.LoadAll 中（由 TableTool 生成，勿手改）。
    /// Unity 侧请走 UnityTableLoader.LoadFromResources；控制台模拟器/EditorTest 走 LoadFromDirectory。
    /// </summary>
    public static class TableLoader
    {
        /// <summary>全部配表是否已加载完成（未加载就访问 Tables.* 会得到 null/异常）。</summary>
        public static bool IsLoaded
        {
            get { return Tables.IsLoaded; }
        }

        /// <summary>通用加载：readJsonByTableName 接收表名（不含扩展名）返回该表 JSON 文本。</summary>
        public static void Load(Func<string, string> readJsonByTableName)
        {
            if (readJsonByTableName == null)
            {
                throw new ArgumentNullException(nameof(readJsonByTableName), "TableLoader.Load 失败：readJsonByTableName 为 null。");
            }
            Tables.LoadAll(readJsonByTableName);
        }

        /// <summary>从目录加载全部配表（目录下须有 {表名}.json）；目录或单表文件缺失抛带路径/表名的异常。</summary>
        public static void LoadFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException("TableLoader.LoadFromDirectory 失败：directory 为 null 或空串。", nameof(directory));
            }
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException(
                    $"配表目录不存在：{Path.GetFullPath(directory)}（请先运行转表：dotnet run --project Tools/TableTool -- convert）。");
            }

            Load(delegate (string tableName)
            {
                string path = Path.Combine(directory, tableName + ".json");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        $"配表 [{tableName}] 缺少 JSON 文件：{Path.GetFullPath(path)}（表已在 Tables.g.cs 注册但目录中无对应文件，是否漏转表？）。", path);
                }
                return File.ReadAllText(path);
            });
        }
    }
}
