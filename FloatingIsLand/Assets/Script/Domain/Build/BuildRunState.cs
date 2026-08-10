using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>一个等级的建筑组配置（配表 Level 的领域表示）。</summary>
    public sealed class LevelDef
    {
        /// <summary>等级（1 起）。</summary>
        public int Level { get; }

        /// <summary>解锁本级消耗的金币。</summary>
        public int UnlockCost { get; }

        /// <summary>本级提供几组供二选一。</summary>
        public int GroupCount { get; }

        /// <summary>每组建筑数量下限。</summary>
        public int GroupSizeMin { get; }

        /// <summary>每组建筑数量上限。</summary>
        public int GroupSizeMax { get; }

        /// <summary>
        /// 本级强制使用的主题 Id 列表；为空表示按主题自己的等级区间自动筛选（常规做法）。
        /// 只在需要写死体验时填，例如第 1 级固定「矿业 vs 农业」。
        /// </summary>
        public IReadOnlyList<string> ForcedThemeIds { get; }

        public LevelDef(
            int level, int unlockCost, int groupCount, int groupSizeMin, int groupSizeMax,
            IReadOnlyList<string> forcedThemeIds)
        {
            Level = level;
            UnlockCost = unlockCost;
            GroupCount = groupCount;
            GroupSizeMin = groupSizeMin;
            GroupSizeMax = groupSizeMax;
            ForcedThemeIds = forcedThemeIds ?? Array.Empty<string>();
        }
    }

    /// <summary>一组待选建筑（二选一的其中一组）。</summary>
    public sealed class BuildingGroup
    {
        /// <summary>本组来自哪个主题（<see cref="GroupThemeDef.ThemeId"/>）。</summary>
        public string ThemeId { get; }

        /// <summary>主题显示名，UI 直接拿去当组标题。</summary>
        public string NameCn { get; }

        /// <summary>组内建筑变体 Id，允许重复（§4.2：同组可重复建筑）；按配方声明顺序聚在一起。</summary>
        public IReadOnlyList<string> VariantIds { get; }

        public BuildingGroup(string themeId, string nameCn, IReadOnlyList<string> variantIds)
        {
            ThemeId = themeId ?? string.Empty;
            NameCn = nameCn ?? string.Empty;
            VariantIds = variantIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// 局内进度状态：等级 / 建筑组二选一 / 手牌 / 总分 / 金币（GAME_DESIGN §3、§4）。
    ///
    /// 纯领域对象，不知道 UI 也不知道场景。表现层通过事件同步，通过方法驱动。
    /// </summary>
    public sealed class BuildRunState
    {
        private readonly IReadOnlyList<LevelDef> _levels;
        private readonly IReadOnlyList<GroupThemeDef> _themes;
        private readonly BuildRuleSet _rules;
        private readonly DeterministicRandom _random;
        private readonly List<string> _hand = new List<string>();
        private readonly List<GroupThemeDef> _candidateThemes = new List<GroupThemeDef>();
        private readonly List<GroupThemeDef> _remainingThemes = new List<GroupThemeDef>();
        private BuildingGroup[] _offers = Array.Empty<BuildingGroup>();

        /// <summary>当前等级（1 起）。</summary>
        public int Level { get; private set; }

        /// <summary>总分。</summary>
        public int TotalScore { get; private set; }

        /// <summary>金币。</summary>
        public int Gold { get; private set; }

        /// <summary>手牌：当前可摆放的建筑变体 Id，按获得顺序。</summary>
        public IReadOnlyList<string> Hand
        {
            get { return _hand; }
        }

        /// <summary>当前待二选一的建筑组；为空表示不在选组阶段。</summary>
        public IReadOnlyList<BuildingGroup> Offers
        {
            get { return _offers; }
        }

        /// <summary>总等级数。</summary>
        public int TotalLevels
        {
            get { return _levels.Count; }
        }

        /// <summary>解锁下一级需要的金币；已是最后一级返回 0。</summary>
        public int NextUnlockCost
        {
            get
            {
                LevelDef next = LevelAt(Level + 1);
                return next == null ? 0 : next.UnlockCost;
            }
        }

        /// <summary>状态变更事件（分数 / 金币 / 手牌 / 待选组 任一变化都会触发）。</summary>
        public event Action Changed;

        public BuildRunState(
            IReadOnlyList<LevelDef> levels,
            IReadOnlyList<GroupThemeDef> themes,
            BuildRuleSet rules,
            int seed)
        {
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
            _themes = themes ?? throw new ArgumentNullException(nameof(themes));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _random = new DeterministicRandom(seed);
            Level = 0;
        }

        /// <summary>开局：进入第 1 级并抽出待选组。</summary>
        public void Start()
        {
            Level = 0;
            TotalScore = 0;
            Gold = 0;
            _hand.Clear();
            AdvanceToNextLevel();
        }

        /// <summary>金币是否够解锁下一级。</summary>
        public bool CanAffordNextLevel()
        {
            LevelDef next = LevelAt(Level + 1);
            return next != null && Gold >= next.UnlockCost;
        }

        /// <summary>
        /// 进入下一级并抽出该级的待选建筑组。**不扣金币也不校验金币**——
        /// 费用是否收取由调用方决定（正式流程扣 <see cref="LevelDef.UnlockCost"/>；
        /// demo 的「点得分按钮直接拿下一组」走同一个入口但不收费）。
        /// </summary>
        /// <returns>false 表示已经是最后一级，没有下一级可进。</returns>
        public bool AdvanceToNextLevel()
        {
            LevelDef next = LevelAt(Level + 1);
            if (next == null)
            {
                return false;
            }

            Level = next.Level;
            _offers = RollOffers(next);
            RaiseChanged();
            return true;
        }

        /// <summary>扣金币解锁下一级；金币不足返回 false 且不改变任何状态。</summary>
        public bool TryUnlockNextLevel()
        {
            LevelDef next = LevelAt(Level + 1);
            if (next == null || Gold < next.UnlockCost)
            {
                return false;
            }

            Gold -= next.UnlockCost;
            return AdvanceToNextLevel();
        }

        /// <summary>选定其中一组，组内全部建筑进手牌（§4.2）。</summary>
        public bool ChooseOffer(int index)
        {
            if (index < 0 || index >= _offers.Length)
            {
                return false;
            }

            BuildingGroup chosen = _offers[index];
            for (int i = 0; i < chosen.VariantIds.Count; i++)
            {
                _hand.Add(chosen.VariantIds[i]);
            }
            _offers = Array.Empty<BuildingGroup>();
            RaiseChanged();
            return true;
        }

        /// <summary>从手牌里消耗一张（摆放成功后调用）。</summary>
        public bool ConsumeFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count)
            {
                return false;
            }
            _hand.RemoveAt(handIndex);
            RaiseChanged();
            return true;
        }

        /// <summary>
        /// 记一次建造得分：加总分，正分按比例转金币（负分只降总分不倒扣金币，§3.3）。
        /// </summary>
        public void AddBuildScore(int score)
        {
            TotalScore += score;
            if (score > 0)
            {
                Gold += (int)Math.Round(score * _rules.ScoreToGoldRatio, MidpointRounding.AwayFromZero);
            }
            RaiseChanged();
        }

        /// <summary>
        /// 抽出本级的若干组建筑。
        ///
        /// 一组 = 一个主题：先在本级可用的主题里**不放回**地抽 <see cref="LevelDef.GroupCount"/> 个，
        /// 再各自按主题配方发牌。不放回是为了让二选一真的是「两条不同的路线」而不是同一堆建筑
        /// 洗两次；组内只出同主题建筑，则保证了组里的建筑互相加分。
        /// </summary>
        private BuildingGroup[] RollOffers(LevelDef level)
        {
            if (level.GroupCount <= 0)
            {
                return Array.Empty<BuildingGroup>();
            }

            CollectCandidateThemes(level);
            if (_candidateThemes.Count == 0)
            {
                return Array.Empty<BuildingGroup>();
            }

            _remainingThemes.Clear();
            var offers = new BuildingGroup[level.GroupCount];
            for (int g = 0; g < level.GroupCount; g++)
            {
                if (_remainingThemes.Count == 0)
                {
                    // 本级可用主题比组数还少（只会出现在配表铺得不够的等级上）：
                    // 允许重复出题，配方随机仍会让两组内容不同，好过少给玩家一组。
                    _remainingThemes.AddRange(_candidateThemes);
                }

                int index = PickWeightedTheme(_remainingThemes);
                GroupThemeDef theme = _remainingThemes[index];
                _remainingThemes.RemoveAt(index);
                offers[g] = BuildGroup(theme, level);
            }
            return offers;
        }

        /// <summary>本级可用主题：Level 写死了就用写死的，否则按主题自己的等级区间筛。</summary>
        private void CollectCandidateThemes(LevelDef level)
        {
            _candidateThemes.Clear();

            if (level.ForcedThemeIds.Count > 0)
            {
                for (int i = 0; i < level.ForcedThemeIds.Count; i++)
                {
                    GroupThemeDef theme = ThemeById(level.ForcedThemeIds[i]);
                    if (theme != null)
                    {
                        _candidateThemes.Add(theme);
                    }
                }
                return;
            }

            for (int i = 0; i < _themes.Count; i++)
            {
                if (_themes[i].IsAvailableAt(level.Level))
                {
                    _candidateThemes.Add(_themes[i]);
                }
            }
        }

        /// <summary>按配方把一个主题展开成一组建筑。</summary>
        private BuildingGroup BuildGroup(GroupThemeDef theme, LevelDef level)
        {
            IReadOnlyList<ThemeMember> members = theme.Members;
            var counts = new int[members.Count];
            int total = 0;
            for (int i = 0; i < members.Count; i++)
            {
                counts[i] = members[i].MinCount;
                total += counts[i];
            }

            int size = level.GroupSizeMin >= level.GroupSizeMax
                ? level.GroupSizeMin
                : _random.NextInt(level.GroupSizeMin, level.GroupSizeMax + 1);

            // 保底数量优先于组大小下限：配方说了必出的建筑一定给足
            // （配表校验保证保底和不超过 groupSizeMax，所以这里不会撑爆一组）。
            if (size < total)
            {
                size = total;
            }

            while (total < size)
            {
                int pick = PickWeightedMember(members, counts);
                if (pick < 0)
                {
                    break; // 所有成员都到上限了，这一组就比 size 小
                }
                counts[pick]++;
                total++;
            }

            var picks = new List<string>(total);
            for (int i = 0; i < members.Count; i++)
            {
                for (int n = 0; n < counts[i]; n++)
                {
                    picks.Add(members[i].VariantId);
                }
            }
            return new BuildingGroup(theme.ThemeId, theme.NameCn, picks);
        }

        /// <summary>按权重抽一个主题的下标；权重全为 0 时退化成均匀抽。</summary>
        private int PickWeightedTheme(List<GroupThemeDef> themes)
        {
            int totalWeight = 0;
            for (int i = 0; i < themes.Count; i++)
            {
                totalWeight += Math.Max(0, themes[i].Weight);
            }
            if (totalWeight <= 0)
            {
                return _random.NextInt(0, themes.Count);
            }

            int roll = _random.NextInt(0, totalWeight);
            for (int i = 0; i < themes.Count; i++)
            {
                roll -= Math.Max(0, themes[i].Weight);
                if (roll < 0)
                {
                    return i;
                }
            }
            return themes.Count - 1;
        }

        /// <summary>按权重抽一个还没到上限的成员下标；没有可抽的返回 -1。</summary>
        private int PickWeightedMember(IReadOnlyList<ThemeMember> members, int[] counts)
        {
            int totalWeight = 0;
            for (int i = 0; i < members.Count; i++)
            {
                if (CanTakeMore(members[i], counts[i]))
                {
                    totalWeight += members[i].Weight;
                }
            }
            if (totalWeight <= 0)
            {
                return -1;
            }

            int roll = _random.NextInt(0, totalWeight);
            for (int i = 0; i < members.Count; i++)
            {
                if (!CanTakeMore(members[i], counts[i]))
                {
                    continue;
                }
                roll -= members[i].Weight;
                if (roll < 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool CanTakeMore(ThemeMember member, int taken)
        {
            return member.Weight > 0 && (member.MaxCount <= 0 || taken < member.MaxCount);
        }

        private GroupThemeDef ThemeById(string themeId)
        {
            for (int i = 0; i < _themes.Count; i++)
            {
                if (string.Equals(_themes[i].ThemeId, themeId, StringComparison.Ordinal))
                {
                    return _themes[i];
                }
            }
            return null;
        }

        private LevelDef LevelAt(int level)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Level == level)
                {
                    return _levels[i];
                }
            }
            return null;
        }

        private void RaiseChanged()
        {
            Changed?.Invoke();
        }
    }
}
