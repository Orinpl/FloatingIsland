using System;
using System.Collections.Generic;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>一个等级的建筑组配置（配表 Level 的领域表示）。</summary>
    public sealed class LevelDef
    {
        /// <summary>本关内的第几组（1 起）。</summary>
        public int Level { get; }

        /// <summary>
        /// 解锁本组所需的累计分门槛，**相对本关基线的增量**（第 1 组 = 0 免费）。
        /// 实际门槛还要乘 <see cref="StageDef.UnlockScoreMult"/>，且达标即解锁、不扣分。
        /// </summary>
        public int UnlockScore { get; }

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
            int level, int unlockScore, int groupCount, int groupSizeMin, int groupSizeMax,
            IReadOnlyList<string> forcedThemeIds)
        {
            Level = level;
            UnlockScore = unlockScore;
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
    /// 一关内的进度状态：第几组 / 建筑组二选一 / 手牌 / 分数（GAME_DESIGN §3、§4）。
    ///
    /// 解锁下一组看的是**累计总分够不够门槛**，不是花钱买：达标即解锁，分数不扣。
    /// 分数跨关保留，所以本关的门槛都以 <see cref="StageBaseScore"/>（进入本关时的累计分）
    /// 为基线做增量——上一关拿了多少分不会让下一关的第 2 组变免费，但那些分也不会被清掉。
    ///
    /// 纯领域对象，不知道 UI 也不知道场景。表现层通过事件同步，通过方法驱动。
    /// </summary>
    public sealed class BuildRunState
    {
        private readonly IReadOnlyList<LevelDef> _levels;
        private readonly IReadOnlyList<GroupThemeDef> _themes;
        private readonly StageDef _stage;
        private readonly DeterministicRandom _random;
        private readonly List<string> _hand = new List<string>();
        private readonly List<GroupThemeDef> _candidateThemes = new List<GroupThemeDef>();
        private readonly List<GroupThemeDef> _remainingThemes = new List<GroupThemeDef>();
        /// <summary>本局各主题已被选中的次数，用来兑现 <see cref="GroupThemeDef.MaxPerRun"/>。</summary>
        private readonly Dictionary<string, int> _themePicks = new Dictionary<string, int>(StringComparer.Ordinal);
        private BuildingGroup[] _offers = Array.Empty<BuildingGroup>();

        /// <summary>本关当前进行到第几组（1 起）。</summary>
        public int Level { get; private set; }

        /// <summary>累计总分（跨关保留，含进入本关前已有的分）。</summary>
        public int TotalScore { get; private set; }

        /// <summary>进入本关时的累计总分，本关一切门槛的基线。</summary>
        public int StageBaseScore { get; }

        /// <summary>本关内已得的分（= <see cref="TotalScore"/> − <see cref="StageBaseScore"/>，可为负）。</summary>
        public int StageScore
        {
            get { return TotalScore - StageBaseScore; }
        }

        /// <summary>本关配置。</summary>
        public StageDef Stage
        {
            get { return _stage; }
        }

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

        /// <summary>本关总组数（Level 表行数与 <see cref="StageDef.GroupCount"/> 取小）。</summary>
        public int TotalLevels
        {
            get { return Math.Min(_levels.Count, _stage.GroupCount); }
        }

        /// <summary>解锁下一组所需的**累计总分**（已含本关基线）；已是最后一组返回 0。</summary>
        public int NextUnlockScore
        {
            get
            {
                LevelDef next = LevelAt(Level + 1);
                return next == null ? 0 : StageBaseScore + _stage.ScaleUnlockScore(next.UnlockScore);
            }
        }

        /// <summary>通关本关所需的**累计总分**（已含本关基线）。</summary>
        public int ClearScore
        {
            get { return StageBaseScore + _stage.ClearScore; }
        }

        /// <summary>本关是否已达通关分。达标后「进下一关」解锁，但不强制——玩家可以继续摆。</summary>
        public bool IsStageCleared
        {
            get { return TotalScore >= ClearScore; }
        }

        /// <summary>本关的组是否已经发完（最后一组也选过了）。</summary>
        public bool IsLastGroup
        {
            get { return Level >= TotalLevels; }
        }

        /// <summary>状态变更事件（分数 / 手牌 / 待选组 任一变化都会触发）。</summary>
        public event Action Changed;

        public BuildRunState(
            IReadOnlyList<LevelDef> levels,
            IReadOnlyList<GroupThemeDef> themes,
            StageDef stage,
            int seed,
            int stageBaseScore)
        {
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
            _themes = themes ?? throw new ArgumentNullException(nameof(themes));
            _stage = stage ?? throw new ArgumentNullException(nameof(stage));
            _random = new DeterministicRandom(seed);
            StageBaseScore = stageBaseScore;
            TotalScore = stageBaseScore;
            Level = 0;
        }

        /// <summary>开关：进入第 1 组（免费）并抽出待选组。分数从本关基线起算，不清零。</summary>
        public void Start()
        {
            Level = 0;
            TotalScore = StageBaseScore;
            _hand.Clear();
            _themePicks.Clear();
            AdvanceToNextLevel();
        }

        /// <summary>累计分是否够解锁下一组。</summary>
        public bool CanAffordNextLevel()
        {
            LevelDef next = LevelAt(Level + 1);
            return next != null && TotalScore >= StageBaseScore + _stage.ScaleUnlockScore(next.UnlockScore);
        }

        /// <summary>
        /// 进入下一组并抽出待选建筑组。**不校验分数门槛**——
        /// 是否收门槛由调用方决定（正式流程走 <see cref="TryUnlockNextLevel"/>；
        /// demo / 调试直接走本入口）。
        /// </summary>
        /// <returns>false 表示本关的组已经发完。</returns>
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

        /// <summary>
        /// 按分数门槛解锁下一组；分数不够返回 false 且不改变任何状态。
        /// 门槛是**准入条件不是消耗**，达标解锁后总分一分不少——否则玩家越往后越穷，
        /// 「分数保留」也就无从谈起。
        /// </summary>
        public bool TryUnlockNextLevel()
        {
            if (!CanAffordNextLevel())
            {
                return false;
            }
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

            // 记一次「选中」而不是「出现」：MaxPerRun 限的是玩家最终拿到多少，
            // 只被提供却没选的不该占额度。
            if (!string.IsNullOrEmpty(chosen.ThemeId))
            {
                int taken;
                _themePicks.TryGetValue(chosen.ThemeId, out taken);
                _themePicks[chosen.ThemeId] = taken + 1;
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

        /// <summary>记一次建造得分：直接加到累计总分上（负分同样计入，§3.3）。</summary>
        public void AddBuildScore(int score)
        {
            TotalScore += score;
            RaiseChanged();
        }

        /// <summary>本关是否已经走不下去：组发完了，或分数不够解锁下一组（GAME_DESIGN §2.2 结束条件之三）。</summary>
        public bool IsStuck()
        {
            return _hand.Count == 0 && _offers.Length == 0 && !CanAffordNextLevel();
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

        /// <summary>
        /// 本级可用主题：Level 写死了就用写死的，否则按主题自己的等级区间筛；
        /// 再滤掉本局配额已用完的（<see cref="GroupThemeDef.MaxPerRun"/>）。
        /// 配额把候选滤空时忽略配额——宁可多给一次地标，也不能让玩家这一级无组可选。
        /// </summary>
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
            }
            else
            {
                for (int i = 0; i < _themes.Count; i++)
                {
                    if (_themes[i].IsAvailableAt(level.Level))
                    {
                        _candidateThemes.Add(_themes[i]);
                    }
                }
            }

            int kept = 0;
            for (int i = 0; i < _candidateThemes.Count; i++)
            {
                GroupThemeDef theme = _candidateThemes[i];
                int taken;
                _themePicks.TryGetValue(theme.ThemeId, out taken);
                if (theme.HasRunQuotaLeft(taken))
                {
                    _candidateThemes[kept++] = theme;
                }
            }
            if (kept > 0)
            {
                _candidateThemes.RemoveRange(kept, _candidateThemes.Count - kept);
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

        /// <summary>
        /// 取本关第 <paramref name="level"/> 组的配置；超出本关组数返回 null。
        ///
        /// 这里必须卡 <see cref="TotalLevels"/> 而不是直接查 Level 表——Level 表是全局的
        /// 组序列，各关按 <see cref="StageDef.GroupCount"/> 只取前 N 组。不卡的话
        /// 「本关组数用完」只在 UI 上成立，解锁按钮照样能把玩家推到第 N+1 组。
        /// </summary>
        private LevelDef LevelAt(int level)
        {
            if (level < 1 || level > TotalLevels)
            {
                return null;
            }

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
