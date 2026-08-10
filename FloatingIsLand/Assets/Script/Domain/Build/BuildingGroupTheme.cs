using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>
    /// 主题里的一个成员建筑（配表 BuildingGroupTheme.members 的 <c>变体Id:权重[:最少[:最多]]</c>）。
    /// </summary>
    public readonly struct ThemeMember
    {
        /// <summary>建筑变体 Id。</summary>
        public readonly string VariantId;

        /// <summary>填充剩余名额时的抽取权重；0 = 只出保底那几栋，不参与随机填充。</summary>
        public readonly int Weight;

        /// <summary>本组保底数量（先于随机填充发下去）。</summary>
        public readonly int MinCount;

        /// <summary>本组数量上限；0 = 不限。</summary>
        public readonly int MaxCount;

        public ThemeMember(string variantId, int weight, int minCount, int maxCount)
        {
            VariantId = variantId;
            Weight = weight;
            MinCount = minCount;
            MaxCount = maxCount;
        }
    }

    /// <summary>
    /// 一个建筑组主题（配表 BuildingGroupTheme 的领域表示）。
    ///
    /// 主题是「选组」这件事的最小语义单位：一组建筑之所以能凑一组，是因为它们在
    /// <see cref="BuildingBlueprint.BonusFrom"/> 上互相加分（矿业＝采矿站给工坊加分、
    /// 农业＝农田与风车互相加分……），而互相扣分的建筑（居民区被采矿站/工坊扣分）
    /// 一定分属不同主题。配比写在 <see cref="Members"/> 里：核心建筑权重高、出得多，
    /// 增幅建筑靠 <see cref="ThemeMember.MaxCount"/> 压成稀有。
    ///
    /// 阶段节奏由 <see cref="MinLevel"/>/<see cref="MaxLevel"/> 表达，前期只开生产类主题，
    /// 居住商业与物流港口分别在中后期接进来。
    /// </summary>
    public sealed class GroupThemeDef
    {
        /// <summary>主题 Id。</summary>
        public string ThemeId { get; }

        /// <summary>显示名（选组按钮上给玩家看）。</summary>
        public string NameCn { get; }

        /// <summary>最早出现等级（含）。</summary>
        public int MinLevel { get; }

        /// <summary>最晚出现等级（含）；0 = 一直可用。</summary>
        public int MaxLevel { get; }

        /// <summary>同一等级内被抽中的权重。</summary>
        public int Weight { get; }

        /// <summary>成员配方，顺序即发牌顺序。</summary>
        public IReadOnlyList<ThemeMember> Members { get; }

        public GroupThemeDef(
            string themeId, string nameCn, int minLevel, int maxLevel, int weight, IReadOnlyList<ThemeMember> members)
        {
            if (string.IsNullOrEmpty(themeId))
            {
                throw new ArgumentException("主题 Id 为空。", nameof(themeId));
            }

            ThemeId = themeId;
            NameCn = string.IsNullOrEmpty(nameCn) ? themeId : nameCn;
            MinLevel = minLevel;
            MaxLevel = maxLevel;
            Weight = weight;
            Members = members ?? Array.Empty<ThemeMember>();
        }

        /// <summary>本主题在给定等级是否可用。</summary>
        public bool IsAvailableAt(int level)
        {
            if (MinLevel > 0 && level < MinLevel)
            {
                return false;
            }
            return MaxLevel <= 0 || level <= MaxLevel;
        }

        /// <summary>配方保底数量之和（组大小的下限，<see cref="LevelDef.GroupSizeMin"/> 拗不过它）。</summary>
        public int MinTotal()
        {
            int total = 0;
            for (int i = 0; i < Members.Count; i++)
            {
                total += Members[i].MinCount;
            }
            return total;
        }
    }
}
