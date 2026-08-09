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

        /// <summary>抽取池：每个元素是一个变体 Id 的一份，同一变体出现几次就是几份权重。</summary>
        public IReadOnlyList<string> Pool { get; }

        public LevelDef(int level, int unlockCost, int groupCount, int groupSizeMin, int groupSizeMax, IReadOnlyList<string> pool)
        {
            Level = level;
            UnlockCost = unlockCost;
            GroupCount = groupCount;
            GroupSizeMin = groupSizeMin;
            GroupSizeMax = groupSizeMax;
            Pool = pool ?? Array.Empty<string>();
        }
    }

    /// <summary>一组待选建筑（二选一的其中一组）。</summary>
    public sealed class BuildingGroup
    {
        /// <summary>组内建筑变体 Id，允许重复（§4.2：同组可重复建筑）。</summary>
        public IReadOnlyList<string> VariantIds { get; }

        public BuildingGroup(IReadOnlyList<string> variantIds)
        {
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
        private readonly BuildRuleSet _rules;
        private readonly DeterministicRandom _random;
        private readonly List<string> _hand = new List<string>();
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

        public BuildRunState(IReadOnlyList<LevelDef> levels, BuildRuleSet rules, int seed)
        {
            _levels = levels ?? throw new ArgumentNullException(nameof(levels));
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

        /// <summary>抽出本级的若干组建筑。</summary>
        private BuildingGroup[] RollOffers(LevelDef level)
        {
            if (level.Pool.Count == 0 || level.GroupCount <= 0)
            {
                return Array.Empty<BuildingGroup>();
            }

            var offers = new BuildingGroup[level.GroupCount];
            for (int g = 0; g < level.GroupCount; g++)
            {
                int size = level.GroupSizeMin >= level.GroupSizeMax
                    ? level.GroupSizeMin
                    : _random.NextInt(level.GroupSizeMin, level.GroupSizeMax + 1);

                var picks = new List<string>(size);
                for (int i = 0; i < size; i++)
                {
                    picks.Add(level.Pool[_random.NextInt(0, level.Pool.Count)]);
                }
                offers[g] = new BuildingGroup(picks);
            }
            return offers;
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
