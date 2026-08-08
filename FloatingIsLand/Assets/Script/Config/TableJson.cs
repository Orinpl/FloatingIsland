using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// JSON → 配表对象的反序列化封装（Newtonsoft.Json）。
    /// 行表 = JSON 数组 [{...}]，单例参数组 = JSON 对象 {...}。
    /// 所有失败路径抛带表名/文件名上下文的异常，便于定位配表问题。
    /// </summary>
    public static class TableJson
    {
        /// <summary>反序列化行表 JSON（数组）为 ConfigTable；json 为空或解析失败抛异常（含表名）。</summary>
        public static ConfigTable<TKey, TRow> LoadRows<TKey, TRow>(string tableName, string json, Func<TRow, TKey> keySelector) where TRow : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    $"配表 [{tableName}] 的 JSON 内容为 null/空（文件 {tableName}.json 缺失或为空？请先运行转表：dotnet run --project Tools/TableTool -- convert）。");
            }

            List<TRow> rows;
            try
            {
                rows = JsonConvert.DeserializeObject<List<TRow>>(json);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"配表 [{tableName}] JSON 反序列化失败（文件 {tableName}.json，目标类型 List<{typeof(TRow).Name}>）：{e.Message}", e);
            }

            if (rows == null)
            {
                throw new InvalidOperationException(
                    $"配表 [{tableName}] JSON 反序列化结果为 null（文件 {tableName}.json 内容为字面量 null？行表应为 JSON 数组 [{{...}}]）。");
            }

            return new ConfigTable<TKey, TRow>(tableName, rows, keySelector);
        }

        /// <summary>反序列化单例参数组 JSON（对象）为 T；json 为空或解析失败抛异常（含表名）。</summary>
        public static T LoadSingleton<T>(string tableName, string json) where T : class
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException(
                    $"配表 [{tableName}] 的 JSON 内容为 null/空（文件 {tableName}.json 缺失或为空？请先运行转表：dotnet run --project Tools/TableTool -- convert）。");
            }

            T value;
            try
            {
                value = JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    $"配表 [{tableName}] JSON 反序列化失败（文件 {tableName}.json，目标类型 {typeof(T).Name}）：{e.Message}", e);
            }

            if (value == null)
            {
                throw new InvalidOperationException(
                    $"配表 [{tableName}] JSON 反序列化结果为 null（文件 {tableName}.json 内容为字面量 null？单例参数组应为 JSON 对象 {{...}}）。");
            }

            return value;
        }
    }
}
