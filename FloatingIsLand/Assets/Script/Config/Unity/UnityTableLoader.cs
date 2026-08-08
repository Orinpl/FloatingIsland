using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// Unity 薄适配器：从 Resources/{folder} 读取全部配表 TextAsset 后交给 TableLoader。
    /// 本文件是读表链路中唯一允许 using UnityEngine 的文件（核心保持纯 C#）。
    /// </summary>
    public static class UnityTableLoader
    {
        /// <summary>从 Resources/{folder} 加载全部配表；缺表抛带表名的异常。</summary>
        public static void LoadFromResources(string folder = "Tables")
        {
            TextAsset[] assets = Resources.LoadAll<TextAsset>(folder);
            if (assets == null || assets.Length == 0)
            {
                throw new InvalidOperationException(
                    $"配表加载失败：Resources/{folder} 下没有任何 TextAsset（目录不存在或为空？请先运行转表：dotnet run --project Tools/TableTool -- convert）。");
            }

            var jsonByName = new Dictionary<string, string>(assets.Length, StringComparer.Ordinal);
            foreach (TextAsset asset in assets)
            {
                if (jsonByName.ContainsKey(asset.name))
                {
                    throw new InvalidOperationException(
                        $"配表加载失败：Resources/{folder} 下存在同名资源 '{asset.name}'（同一表名出现多份 JSON？）。");
                }
                jsonByName.Add(asset.name, asset.text);
            }

            TableLoader.Load(delegate (string tableName)
            {
                string json;
                if (jsonByName.TryGetValue(tableName, out json))
                {
                    return json;
                }
                throw new InvalidOperationException(
                    $"配表 [{tableName}] 缺失：Resources/{folder} 下找不到 {tableName}.json（实际加载到 {jsonByName.Count} 个 TextAsset），是否漏转表或漏提交？");
            });
        }
    }
}
