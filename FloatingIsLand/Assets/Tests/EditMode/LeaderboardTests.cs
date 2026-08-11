using System.Collections.Generic;
using FloatingIsLand.App;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 本地排行榜的行为约束：排序口径、昵称兜底、容量截断。
    /// 排行榜是这个游戏唯一持久化的东西（局内进度退出即销毁），所以它必须自己扛住脏输入。
    /// 每个用例先 Clear，避免相互污染以及被开发机上真实的榜单干扰。
    /// </summary>
    public sealed class LeaderboardTests
    {
        [SetUp]
        public void ClearBoard()
        {
            Leaderboard.Clear();
        }

        [TearDown]
        public void CleanUp()
        {
            Leaderboard.Clear();
        }

        private static RunResult Result(int totalScore, bool completed, int stageId = 3)
        {
            return new RunResult
            {
                StageId = stageId,
                TotalScore = totalScore,
                StageCleared = completed,
                IsFinalStage = completed,
            };
        }

        [Test]
        public void 按分数从高到低排名()
        {
            Leaderboard.Submit("低分", Result(100, false, 1));
            Leaderboard.Submit("高分", Result(900, true));
            Leaderboard.Submit("中分", Result(500, false, 2));

            IReadOnlyList<LeaderboardEntry> entries = Leaderboard.Load();
            Assert.AreEqual(3, entries.Count);
            Assert.AreEqual("高分", entries[0].name);
            Assert.AreEqual("中分", entries[1].name);
            Assert.AreEqual("低分", entries[2].name);
        }

        [Test]
        public void 提交返回的名次与榜上位置一致()
        {
            Leaderboard.Submit("甲", Result(500, false, 2));
            int rank = Leaderboard.Submit("乙", Result(900, true));

            Assert.AreEqual(1, rank);
            Assert.AreEqual("乙", Leaderboard.Load()[0].name);
        }

        [Test]
        public void 同分时通关的排在前面()
        {
            Leaderboard.Submit("没通关", Result(700, false, 2));
            Leaderboard.Submit("通关了", Result(700, true));

            IReadOnlyList<LeaderboardEntry> entries = Leaderboard.Load();
            Assert.AreEqual("通关了", entries[0].name);
            Assert.IsTrue(entries[0].completed);
        }

        [Test]
        public void 未通关的局也会上榜并记下止步关卡()
        {
            Leaderboard.Submit("半途", Result(300, false, 2));

            LeaderboardEntry entry = Leaderboard.Load()[0];
            Assert.IsFalse(entry.completed);
            Assert.AreEqual(2, entry.stageReached);
        }

        [Test]
        public void 空昵称给默认名且过长会截断()
        {
            Leaderboard.Submit("   ", Result(100, false, 1));
            Assert.AreEqual("无名建造者", Leaderboard.Load()[0].name);

            Leaderboard.Clear();
            Leaderboard.Submit(new string('长', 40), Result(100, false, 1));
            Assert.AreEqual(16, Leaderboard.Load()[0].name.Length, "昵称最多 16 字，否则榜单排版会散");
        }

        [Test]
        public void 超出容量的低分被丢弃()
        {
            for (int i = 0; i < Leaderboard.Capacity; i++)
            {
                Leaderboard.Submit($"玩家{i}", Result(1000 + i, false, 1));
            }

            int rank = Leaderboard.Submit("垫底", Result(1, false, 1));
            Assert.AreEqual(-1, rank, "没进前 N 名应返回 -1");
            Assert.AreEqual(Leaderboard.Capacity, Leaderboard.Load().Count);
        }
    }
}
