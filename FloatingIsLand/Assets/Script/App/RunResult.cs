namespace FloatingIsLand.App
{
    /// <summary>
    /// 一局的终局结算结果（骨架版：只有总分与结束原因）。
    /// M5 终局结算实装后扩展为"分数明细列表"（即时分 + 终局网络分分离，PROJECT_BUILD §4.4）。
    /// </summary>
    public sealed class RunResult
    {
        /// <summary>本局总分。</summary>
        public int TotalScore;

        /// <summary>结束原因（四种结束条件之一 / 玩家主动结算，PROJECT_BUILD §4.6）。</summary>
        public string EndReason;
    }
}
