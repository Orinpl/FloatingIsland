namespace FloatingIsLand.App
{
    /// <summary>
    /// 一关结束时的结算结果。
    ///
    /// 「本关结束」有两种来源：玩家达到通关分后主动点「进入下一关」，或者本关走不下去了
    /// （组发完 / 分数不够解锁下一组 / 摆不下）。两种都走这里，差别只在
    /// <see cref="StageCleared"/>——达标过就能继续下一关，没达标整局就到此为止。
    /// </summary>
    public sealed class RunResult
    {
        /// <summary>第几关。</summary>
        public int StageId;

        /// <summary>关卡显示名。</summary>
        public string StageName = "";

        /// <summary>本关内得到的分。</summary>
        public int StageScore;

        /// <summary>累计总分（跨关保留，排行榜记的就是它）。</summary>
        public int TotalScore;

        /// <summary>本关通关门槛（累计分口径）。</summary>
        public int ClearScore;

        /// <summary>本关是否达到了通关分。</summary>
        public bool StageCleared;

        /// <summary>本关是不是最后一关。</summary>
        public bool IsFinalStage;

        /// <summary>本关打到第几组 / 共几组。</summary>
        public int GroupsPlayed;

        public int GroupTotal;

        /// <summary>本关落地的建筑数。</summary>
        public int BuildingsPlaced;

        /// <summary>结束原因（给玩家看的一句话）。</summary>
        public string EndReason = "";

        /// <summary>还能不能进下一关：本关通关了且后面还有关。</summary>
        public bool CanAdvance
        {
            get { return StageCleared && !IsFinalStage; }
        }

        /// <summary>整局是否结束（未通关本关，或已经打穿最后一关）。上榜时机就是它为 true。</summary>
        public bool IsGameOver
        {
            get { return !StageCleared || IsFinalStage; }
        }

        /// <summary>是否打穿了全部关卡（排行榜上「通关」标记的依据）。</summary>
        public bool GameCompleted
        {
            get { return StageCleared && IsFinalStage; }
        }
    }
}
