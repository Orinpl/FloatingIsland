using System;
using System.Collections.Generic;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// 单张行表的运行时容器：持有全部行 + 主键索引。
    /// 纯 C#（无 UnityEngine 依赖），由 Tables.g.cs（生成代码）在 LoadAll 中构造。
    /// </summary>
    public sealed class ConfigTable<TKey, TRow> where TRow : class
    {
        private readonly Dictionary<TKey, TRow> _byKey;

        /// <summary>表名（= Sheet 名 = JSON 文件名，不含扩展名），用于错误信息定位。</summary>
        public string TableName { get; }

        /// <summary>全部行，保持 JSON（即 Excel 行）原始顺序。</summary>
        public IReadOnlyList<TRow> All { get; }

        /// <summary>行数。</summary>
        public int Count
        {
            get { return All.Count; }
        }

        /// <summary>构造时建立主键索引并校验主键非空、不重复（违反则抛异常，含表名与键）。</summary>
        public ConfigTable(string tableName, IReadOnlyList<TRow> rows, Func<TRow, TKey> keySelector)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentException("ConfigTable 构造失败：tableName 为 null 或空串。", nameof(tableName));
            }
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows), "配表 [" + tableName + "] 构造失败：rows 为 null。");
            }
            if (keySelector == null)
            {
                throw new ArgumentNullException(nameof(keySelector), "配表 [" + tableName + "] 构造失败：keySelector 为 null。");
            }

            TableName = tableName;
            All = rows;
            _byKey = new Dictionary<TKey, TRow>(rows.Count);

            for (int i = 0; i < rows.Count; i++)
            {
                TRow row = rows[i];
                if (row == null)
                {
                    throw new InvalidOperationException(
                        $"配表 [{tableName}] 第 {i} 行为 null（{tableName}.json 数组中存在 null 元素？）。");
                }

                TKey key = keySelector(row);
                if (key == null)
                {
                    throw new InvalidOperationException(
                        $"配表 [{tableName}] 第 {i} 行主键为 null（主键列不允许为空）。");
                }

                if (_byKey.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        $"配表 [{tableName}] 主键重复：'{key}'（第 {i} 行与先前行冲突，请检查 Excel 首列唯一性）。");
                }

                _byKey.Add(key, row);
            }
        }

        /// <summary>按主键取行；缺键抛 KeyNotFoundException（含表名与键）。</summary>
        public TRow Get(TKey key)
        {
            TRow row;
            if (_byKey.TryGetValue(key, out row))
            {
                return row;
            }
            throw new KeyNotFoundException(
                $"配表 [{TableName}] 找不到主键 '{key}'（全表共 {Count} 行，请检查调用方传键或 {TableName}.json 数据）。");
        }

        /// <summary>按主键取行；存在返回 true 并输出该行，否则返回 false。</summary>
        public bool TryGet(TKey key, out TRow row)
        {
            return _byKey.TryGetValue(key, out row);
        }

        /// <summary>按主键取行；缺键返回 null（不抛异常）。</summary>
        public TRow GetOrNull(TKey key)
        {
            TRow row;
            return _byKey.TryGetValue(key, out row) ? row : null;
        }
    }
}
