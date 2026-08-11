using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 主题化选组的行为约束（BuildRunState.RollOffers）：
    /// 组内只出同主题建筑、保底数量与上限被尊重、二选一的两组来自不同主题。
    /// 用手搓的规则集，不碰配表——领域层的这套逻辑必须能脱离 Excel 单测。
    /// </summary>
    public sealed class BuildRunStateTests
    {
        private static BuildingBlueprint Blueprint(string variantId)
        {
            return new BuildingBlueprint(
                variantId, variantId, variantId, "test",
                Footprint.Parse(new[] { "#" }, variantId),
                PlacementRule.Any, 1, 10, true, 0, string.Empty,
                null, null, null, null);
        }

        private static BuildRuleSet Rules()
        {
            return new BuildRuleSet(
                new List<BuildingBlueprint>
                {
                    Blueprint("core_01"), Blueprint("rare_01"), Blueprint("other_01"),
                },
                new List<MapElementDef>(),
                1f, 0, null, 3, 5);
        }

        /// <summary>核心建筑权重高、上限松；稀有建筑保底 1 上限 1 —— 「采矿站多、工坊少」的最小复现。</summary>
        private static GroupThemeDef CoreTheme()
        {
            return new GroupThemeDef("core", "核心主题", 1, 0, 10, 0, new List<ThemeMember>
            {
                new ThemeMember("core_01", 8, 2, 4),
                new ThemeMember("rare_01", 1, 1, 1),
            });
        }

        private static GroupThemeDef OtherTheme()
        {
            return new GroupThemeDef("other", "另一个主题", 1, 0, 10, 0, new List<ThemeMember>
            {
                new ThemeMember("other_01", 5, 1, 0),
            });
        }

        private static LevelDef Level(int level, int groupCount, int sizeMin, int sizeMax, params string[] forced)
        {
            return new LevelDef(level, 0, groupCount, sizeMin, sizeMax, forced);
        }

        /// <summary>带分数门槛的组：unlockScore = 解锁本组所需的「本关得分」增量。</summary>
        private static LevelDef GatedLevel(int level, int unlockScore)
        {
            return new LevelDef(level, unlockScore, 1, 3, 3, null);
        }

        private static StageDef Stage(int groupCount = 99, int clearScore = 100000, float mult = 1f)
        {
            return new StageDef(1, "测试关", groupCount, clearScore, mult);
        }

        private static BuildRunState Run(
            IReadOnlyList<LevelDef> levels, IReadOnlyList<GroupThemeDef> themes, int seed,
            StageDef stage = null, int baseScore = 0)
        {
            var run = new BuildRunState(levels, themes, stage ?? Stage(), seed, baseScore);
            run.Start();
            return run;
        }

        [Test]
        public void 组内只出同一个主题的建筑()
        {
            var themes = new List<GroupThemeDef> { CoreTheme(), OtherTheme() };
            for (int seed = 1; seed <= 20; seed++)
            {
                BuildRunState run = Run(new List<LevelDef> { Level(1, 2, 3, 5) }, themes, seed);
                foreach (BuildingGroup group in run.Offers)
                {
                    var allowed = new HashSet<string>(
                        group.ThemeId == "core" ? new[] { "core_01", "rare_01" } : new[] { "other_01" });
                    foreach (string variantId in group.VariantIds)
                    {
                        Assert.IsTrue(allowed.Contains(variantId),
                            $"种子 {seed}：主题 {group.ThemeId} 的组里混进了 {variantId}");
                    }
                }
            }
        }

        [Test]
        public void 保底数量与上限都被尊重()
        {
            var themes = new List<GroupThemeDef> { CoreTheme() };
            for (int seed = 1; seed <= 30; seed++)
            {
                BuildRunState run = Run(new List<LevelDef> { Level(1, 1, 3, 5, "core") }, themes, seed);
                Assert.AreEqual(1, run.Offers.Count);

                int core = 0;
                int rare = 0;
                foreach (string variantId in run.Offers[0].VariantIds)
                {
                    if (variantId == "core_01")
                    {
                        core++;
                    }
                    else
                    {
                        rare++;
                    }
                }

                Assert.GreaterOrEqual(core, 2, $"种子 {seed}：核心建筑没给够保底 2 栋");
                Assert.LessOrEqual(core, 4, $"种子 {seed}：核心建筑超过上限 4 栋");
                Assert.AreEqual(1, rare, $"种子 {seed}：稀有建筑必须恰好 1 栋（保底 1 上限 1）");
            }
        }

        [Test]
        public void 组大小落在配表区间内()
        {
            var themes = new List<GroupThemeDef> { CoreTheme(), OtherTheme() };
            for (int seed = 1; seed <= 30; seed++)
            {
                BuildRunState run = Run(new List<LevelDef> { Level(1, 2, 3, 5) }, themes, seed);
                foreach (BuildingGroup group in run.Offers)
                {
                    Assert.GreaterOrEqual(group.VariantIds.Count, 3, $"种子 {seed}：组太小");
                    Assert.LessOrEqual(group.VariantIds.Count, 5, $"种子 {seed}：组太大");
                }
            }
        }

        [Test]
        public void 候选够时两组必定是不同主题()
        {
            var themes = new List<GroupThemeDef> { CoreTheme(), OtherTheme() };
            for (int seed = 1; seed <= 30; seed++)
            {
                BuildRunState run = Run(new List<LevelDef> { Level(1, 2, 3, 5) }, themes, seed);
                Assert.AreEqual(2, run.Offers.Count);
                Assert.AreNotEqual(run.Offers[0].ThemeId, run.Offers[1].ThemeId,
                    $"种子 {seed}：二选一给了两个相同主题，等于没得选");
            }
        }

        [Test]
        public void 等级区间之外的主题不会出现()
        {
            var themes = new List<GroupThemeDef>
            {
                new GroupThemeDef("early", "前期", 1, 2, 10, 0, new List<ThemeMember> { new ThemeMember("core_01", 5, 1, 0) }),
                new GroupThemeDef("late", "后期", 3, 0, 10, 0, new List<ThemeMember> { new ThemeMember("other_01", 5, 1, 0) }),
            };
            var levels = new List<LevelDef> { Level(1, 1, 3, 3), Level(2, 1, 3, 3), Level(3, 1, 3, 3) };

            BuildRunState run = Run(levels, themes, 12345);
            Assert.AreEqual("early", run.Offers[0].ThemeId, "第 1 级不该出后期主题");

            run.ChooseOffer(0);
            run.AdvanceToNextLevel();
            Assert.AreEqual("early", run.Offers[0].ThemeId, "第 2 级仍在 early 的区间内");

            run.ChooseOffer(0);
            run.AdvanceToNextLevel();
            Assert.AreEqual("late", run.Offers[0].ThemeId, "第 3 级 early 已过期，只剩 late");
        }

        [Test]
        public void 强制主题列表压过等级区间()
        {
            var themes = new List<GroupThemeDef>
            {
                CoreTheme(),
                new GroupThemeDef("locked", "本来还没解锁", 9, 0, 10, 0,
                    new List<ThemeMember> { new ThemeMember("other_01", 5, 1, 0) }),
            };

            BuildRunState run = Run(new List<LevelDef> { Level(1, 1, 3, 3, "locked") }, themes, 7);
            Assert.AreEqual("locked", run.Offers[0].ThemeId);
        }

        /// <summary>
        /// 地标建筑的总量封顶：主题 maxPerRun=3 + 配方恰好 1 栋 ⇒ 一局最多 3 个市民中心。
        /// 玩家每次都优先选地标组，选满 3 次后它必须彻底退出候选池。
        /// </summary>
        [Test]
        public void 达到本局配额的主题不再出现()
        {
            var themes = new List<GroupThemeDef>
            {
                new GroupThemeDef("landmark", "地标", 1, 0, 10, 3, new List<ThemeMember>
                {
                    new ThemeMember("rare_01", 1, 1, 1),
                    new ThemeMember("core_01", 5, 1, 0),
                }),
                OtherTheme(),
            };

            var levels = new List<LevelDef>();
            for (int i = 1; i <= 10; i++)
            {
                levels.Add(Level(i, 2, 3, 4));
            }

            BuildRunState run = Run(levels, themes, 4321);
            int landmarks = 0;
            int offeredAfterQuota = 0;

            for (int level = 1; level <= 10; level++)
            {
                int landmarkIndex = -1;
                for (int g = 0; g < run.Offers.Count; g++)
                {
                    if (run.Offers[g].ThemeId == "landmark")
                    {
                        landmarkIndex = g;
                        if (landmarks >= 3)
                        {
                            offeredAfterQuota++;
                        }
                    }
                }

                // 有地标就选地标：把配额用到极限才能看出上限有没有兜住
                int pick = landmarkIndex >= 0 ? landmarkIndex : 0;
                if (run.Offers[pick].ThemeId == "landmark")
                {
                    landmarks++;
                }
                run.ChooseOffer(pick);
                run.AdvanceToNextLevel();
            }

            Assert.AreEqual(3, landmarks, "地标主题最多只能被选中 3 次");
            Assert.AreEqual(0, offeredAfterQuota, "配额用完后地标主题不该再进候选池");
        }

        /// <summary>配额把候选滤空时必须退回到不限配额，否则这一级玩家无组可选。</summary>
        [Test]
        public void 配额滤空候选时仍然发得出组()
        {
            var themes = new List<GroupThemeDef>
            {
                new GroupThemeDef("onlyOne", "唯一主题", 1, 0, 10, 1, new List<ThemeMember>
                {
                    new ThemeMember("core_01", 5, 1, 0),
                }),
            };
            var levels = new List<LevelDef> { Level(1, 1, 3, 3), Level(2, 1, 3, 3) };

            BuildRunState run = Run(levels, themes, 55);
            run.ChooseOffer(0);
            run.AdvanceToNextLevel();

            Assert.AreEqual(1, run.Offers.Count, "配额用完也不能让这一级空手");
            Assert.AreEqual("onlyOne", run.Offers[0].ThemeId);
        }

        // ---------- 分数门槛解锁（GAME_DESIGN §4.1） ----------

        private static List<LevelDef> GatedLadder()
        {
            // 首组免费，之后逐组涨价
            return new List<LevelDef> { GatedLevel(1, 0), GatedLevel(2, 100), GatedLevel(3, 300) };
        }

        [Test]
        public void 首组免费而后续组要够分才解锁()
        {
            var themes = new List<GroupThemeDef> { OtherTheme() };
            BuildRunState run = Run(GatedLadder(), themes, 1);

            Assert.AreEqual(1, run.Level, "第 1 组必须直接发出来，不看分数");
            Assert.IsFalse(run.CanAffordNextLevel(), "0 分不该解锁得了要 100 分的第 2 组");
            Assert.IsFalse(run.TryUnlockNextLevel());
            Assert.AreEqual(1, run.Level, "解锁失败不能改变任何状态");

            run.AddBuildScore(99);
            Assert.IsFalse(run.CanAffordNextLevel(), "差 1 分也是不够");

            run.AddBuildScore(1);
            Assert.IsTrue(run.CanAffordNextLevel());
            Assert.IsTrue(run.TryUnlockNextLevel());
            Assert.AreEqual(2, run.Level);
        }

        [Test]
        public void 解锁门槛是准入不是消耗()
        {
            var themes = new List<GroupThemeDef> { OtherTheme() };
            BuildRunState run = Run(GatedLadder(), themes, 1);

            run.AddBuildScore(150);
            Assert.IsTrue(run.TryUnlockNextLevel());
            Assert.AreEqual(150, run.TotalScore, "解锁不扣分，否则「分数保留」无从谈起");
        }

        [Test]
        public void 门槛以本关基线为准并被关卡倍率缩放()
        {
            var themes = new List<GroupThemeDef> { OtherTheme() };
            // 上一关带来 1000 分；本关倍率 2 ⇒ 第 2 组门槛 = 1000 + 100×2
            BuildRunState run = Run(GatedLadder(), themes, 1, Stage(mult: 2f), baseScore: 1000);

            Assert.AreEqual(1000, run.TotalScore, "进关时分数保留，不清零");
            Assert.AreEqual(0, run.StageScore);
            Assert.AreEqual(1200, run.NextUnlockScore);
            Assert.IsFalse(run.CanAffordNextLevel());

            run.AddBuildScore(200);
            Assert.AreEqual(1200, run.TotalScore);
            Assert.AreEqual(200, run.StageScore);
            Assert.IsTrue(run.CanAffordNextLevel(), "本关挣够 200 就该达标，不该被上一关的分数白送");
        }

        [Test]
        public void 通关门槛同样以本关基线为准()
        {
            var themes = new List<GroupThemeDef> { OtherTheme() };
            BuildRunState run = Run(GatedLadder(), themes, 1, Stage(clearScore: 500), baseScore: 1000);

            Assert.AreEqual(1500, run.ClearScore);
            Assert.IsFalse(run.IsStageCleared);

            run.AddBuildScore(499);
            Assert.IsFalse(run.IsStageCleared);

            run.AddBuildScore(1);
            Assert.IsTrue(run.IsStageCleared, "达到通关分即解锁下一关");
        }

        [Test]
        public void 本关组数被关卡配置截断()
        {
            var themes = new List<GroupThemeDef> { OtherTheme() };
            var levels = new List<LevelDef> { GatedLevel(1, 0), GatedLevel(2, 0), GatedLevel(3, 0) };

            BuildRunState run = Run(levels, themes, 1, Stage(groupCount: 2));
            Assert.AreEqual(2, run.TotalLevels, "Level 表有 3 组，但本关只开 2 组");

            Assert.IsTrue(run.TryUnlockNextLevel());
            Assert.AreEqual(2, run.Level);
            Assert.IsTrue(run.IsLastGroup);
            Assert.IsFalse(run.TryUnlockNextLevel(), "本关组数用完就不能再解锁了");
        }

        [Test]
        public void 负分会拉低总分并可能重新卡住门槛()
        {
            var themes = new List<GroupThemeDef> { OtherTheme() };
            BuildRunState run = Run(GatedLadder(), themes, 1);

            run.AddBuildScore(120);
            Assert.IsTrue(run.CanAffordNextLevel());

            // 摆不下跳过一栋要扣分（§4.3），扣完可能就够不着门槛了
            run.AddBuildScore(-30);
            Assert.AreEqual(90, run.TotalScore);
            Assert.IsFalse(run.CanAffordNextLevel());
        }

        [Test]
        public void 选组后整组进手牌()
        {
            var themes = new List<GroupThemeDef> { CoreTheme(), OtherTheme() };
            BuildRunState run = Run(new List<LevelDef> { Level(1, 2, 3, 5) }, themes, 99);

            int expected = run.Offers[1].VariantIds.Count;
            Assert.IsTrue(run.ChooseOffer(1));
            Assert.AreEqual(expected, run.Hand.Count);
            Assert.AreEqual(0, run.Offers.Count);
        }
    }
}
