using System;
using System.Collections.Generic;
using System.Globalization;

namespace FloatingIsLand.Config
{
    /// <summary>
    /// BuildingRelation 表打包单元格的解析结果。
    /// 单元格格式：来源Id:分值[:上限]，多个条目在 Excel 里用 | 分隔（转表后即 string[] 的各元素）。
    /// 判定范围一律用结算建筑自身的 Building.radius，不在条目里配。
    /// 例："restaurant:10:2" = 附近每个餐厅 +10 分，最多计 2 个。
    /// </summary>
    public readonly struct RelationEntry
    {
        /// <summary>来源建筑 Id（→ Building 表主键）。</summary>
        public readonly string SourceId;

        /// <summary>分值（bonusFrom 为加分，penaltyFrom 填正数表示扣多少分）。</summary>
        public readonly int Score;

        /// <summary>最多计入的来源个数；0 = 不限。</summary>
        public readonly int MaxCount;

        public RelationEntry(string sourceId, int score, int maxCount)
        {
            SourceId = sourceId;
            Score = score;
            MaxCount = maxCount;
        }

        /// <summary>解析单个条目；格式非法抛异常（context 用于报错定位，如 "BuildingRelation[residence].bonusFrom"）。</summary>
        public static RelationEntry Parse(string cell, string context)
        {
            if (string.IsNullOrWhiteSpace(cell))
            {
                throw new FormatException($"{context}: 条目为空（格式应为 来源Id:分值[:上限]）。");
            }

            string[] parts = cell.Split(':');
            if (parts.Length < 2 || parts.Length > 3)
            {
                throw new FormatException($"{context}: 条目 '{cell}' 段数非法（应为 2~3 段：来源Id:分值[:上限]）。");
            }

            string sourceId = parts[0].Trim();
            if (sourceId.Length == 0)
            {
                throw new FormatException($"{context}: 条目 '{cell}' 来源 Id 为空。");
            }

            return new RelationEntry(
                sourceId,
                ParseInt(parts[1], cell, "分值", context),
                parts.Length > 2 ? ParseInt(parts[2], cell, "上限", context) : 0);
        }

        /// <summary>解析整列（string[] 的每个元素一条）；cells 为 null/空返回空列表。</summary>
        public static List<RelationEntry> ParseAll(string[] cells, string context)
        {
            var result = new List<RelationEntry>(cells?.Length ?? 0);
            if (cells == null)
            {
                return result;
            }
            for (int i = 0; i < cells.Length; i++)
            {
                result.Add(Parse(cells[i], $"{context}[{i}]"));
            }
            return result;
        }

        private static int ParseInt(string text, string cell, string what, string context)
        {
            int value;
            if (int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return value;
            }
            throw new FormatException($"{context}: 条目 '{cell}' 的{what} '{text}' 不是整数。");
        }
    }
}
