using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 一关的局外参数：第几关 + 地图种子 + 进入本关时的累计总分。
    ///
    /// 一局游戏 = 连打 Stage 表里的 3 关。分数**跨关保留**，所以每关都要知道自己的
    /// <see cref="CarryScore"/> 基线：本关的组解锁门槛和通关门槛都是「基线 + 增量」，
    /// 上一关拿了多少分不会让下一关的第 2 组变免费，但那些分也不会被清掉（GAME_DESIGN §4.1）。
    ///
    /// 注意与 Level 表区分：Level 表是「一关内的建筑组序列」，不是这里的「关」。
    /// </summary>
    public sealed class RunContext
    {
        /// <summary>第几关，从 1 开始（同时是 Stage 表主键与地图 Id）。</summary>
        public int StageId { get; }

        /// <summary>本关随机种子：地图生成、建筑组抽取共用同一种子，保证整关可复现（PROJECT_BUILD §1.2）。</summary>
        public int Seed { get; }

        /// <summary>进入本关时的累计总分（第 1 关为 0）。</summary>
        public int CarryScore { get; }

        public RunContext(int stageId, int seed, int carryScore)
        {
            StageId = stageId;
            Seed = seed;
            CarryScore = carryScore;
        }

        /// <summary>主界面开新游戏：第 1 关，随机种子，分数从 0 起。</summary>
        public static RunContext CreateFirst()
        {
            return new RunContext(1, NewSeed(), 0);
        }

        /// <summary>通关后进下一关：关数 +1，换新种子（新地图），把累计总分带过去当基线。</summary>
        public RunContext CreateNext(int carryScore)
        {
            return new RunContext(StageId + 1, NewSeed(), carryScore);
        }

        private static int NewSeed()
        {
            return Random.Range(int.MinValue, int.MaxValue);
        }
    }
}
