namespace FloatingIsLand.App
{
    /// <summary>
    /// Settlement：结算展示（结果在 GameFlow.LastRunResult，UI 读取显示）。
    /// 出口由 UI 调 GameFlow.NextRun()（下一关）/ ReturnToMainMenu()（回主界面）触发。
    /// </summary>
    public sealed class SettlementState : IGameState
    {
        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Tick()
        {
        }
    }
}
