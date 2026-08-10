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
                1f, 0, null, 3, 5, 0.5f);
        }

        /// <summary>核心建筑权重高、上限松；稀有建筑保底 1 上限 1 —— 「采矿站多、工坊少」的最小复现。</summary>
        private static GroupThemeDef CoreTheme()
        {
            return new GroupThemeDef("core", "核心主题", 1, 0, 10, new List<ThemeMember>
            {
                new ThemeMember("core_01", 8, 2, 4),
                new ThemeMember("rare_01", 1, 1, 1),
            });
        }

        private static GroupThemeDef OtherTheme()
        {
            return new GroupThemeDef("other", "另一个主题", 1, 0, 10, new List<ThemeMember>
            {
                new ThemeMember("other_01", 5, 1, 0),
            });
        }

        private static LevelDef Level(int level, int groupCount, int sizeMin, int sizeMax, params string[] forced)
        {
            return new LevelDef(level, 0, groupCount, sizeMin, sizeMax, forced);
        }

        private static BuildRunState Run(IReadOnlyList<LevelDef> levels, IReadOnlyList<GroupThemeDef> themes, int seed)
        {
            var run = new BuildRunState(levels, themes, Rules(), seed);
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
                new GroupThemeDef("early", "前期", 1, 2, 10, new List<ThemeMember> { new ThemeMember("core_01", 5, 1, 0) }),
                new GroupThemeDef("late", "后期", 3, 0, 10, new List<ThemeMember> { new ThemeMember("other_01", 5, 1, 0) }),
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
                new GroupThemeDef("locked", "本来还没解锁", 9, 0, 10,
                    new List<ThemeMember> { new ThemeMember("other_01", 5, 1, 0) }),
            };

            BuildRunState run = Run(new List<LevelDef> { Level(1, 1, 3, 3, "locked") }, themes, 7);
            Assert.AreEqual("locked", run.Offers[0].ThemeId);
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
