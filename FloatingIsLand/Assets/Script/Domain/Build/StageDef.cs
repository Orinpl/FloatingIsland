using System;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>
    /// 一关的规则（配表 Stage 的领域表示，GAME_DESIGN §4.1）。
    ///
    /// 「关」和「组」是两层：一局游戏打 3 关，每关内部是一串建筑组。
    /// 每关都从「第 1 组免费」重新开始，门槛按**进入本关时的累计总分**为基线重新起算——
    /// 上一关拿了多少分不会让下一关的第 2 组变得免费，但那些分也不会被清掉。
    /// </summary>
    public sealed class StageDef
    {
        /// <summary>关卡 Id（1 起，同时是地图 Id）。</summary>
        public int StageId { get; }

        /// <summary>显示名。</summary>
        public string NameCn { get; }

        /// <summary>本关提供几组建筑（取 Level 表的前 N 组）。</summary>
        public int GroupCount { get; }

        /// <summary>通关本关所需的累计分门槛（相对本关基线的增量）。</summary>
        public int ClearScore { get; }

        /// <summary>本关对 <see cref="LevelDef.UnlockScore"/> 的整体倍率。</summary>
        public float UnlockScoreMult { get; }

        public StageDef(int stageId, string nameCn, int groupCount, int clearScore, float unlockScoreMult)
        {
            StageId = stageId;
            NameCn = string.IsNullOrEmpty(nameCn) ? $"第 {stageId} 关" : nameCn;
            GroupCount = groupCount;
            ClearScore = clearScore;
            UnlockScoreMult = unlockScoreMult > 0f ? unlockScoreMult : 1f;
        }

        /// <summary>把 Level 表里的门槛增量折算成本关的实际增量（乘倍率，四舍五入）。</summary>
        public int ScaleUnlockScore(int baseUnlockScore)
        {
            if (baseUnlockScore <= 0)
            {
                return 0;
            }
            return (int)Math.Round(baseUnlockScore * (double)UnlockScoreMult, MidpointRounding.AwayFromZero);
        }
    }
}
