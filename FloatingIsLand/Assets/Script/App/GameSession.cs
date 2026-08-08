using System;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 局内会话（骨架占位）。
    /// 正式版按 PROJECT_BUILD §4.7 实现"抽组 → 选组 → 逐栋摆放 → 结算发钱 → 解锁下一级"核心循环状态机，
    /// 并在四个结束条件任一命中时触发 Ended（携带真实终局结算结果）。
    /// </summary>
    public sealed class GameSession
    {
        /// <summary>本局局外参数（关数 + 种子）。</summary>
        public RunContext Context { get; }

        /// <summary>本局结束（终局结算完成）。GameplayState 订阅后驱动流程进入 Settlement。</summary>
        public event Action<RunResult> Ended;

        public GameSession(RunContext context)
        {
            Context = context;
        }

        /// <summary>结束本局。骨架版直接给占位结果；正式版由结束判定触发并产出真实终局结算。</summary>
        public void EndRun()
        {
            var result = new RunResult
            {
                TotalScore = 0,
                EndReason = "占位：手动结束（GameSession 核心循环未实装，见 PROJECT_BUILD §4.7）",
            };
            Ended?.Invoke(result);
        }
    }
}
