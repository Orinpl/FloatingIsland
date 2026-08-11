namespace FloatingIsLand.App
{
    /// <summary>
    /// 排行榜：从主界面进入的纯展示状态，出口只有 GameFlow.CloseLeaderboard() 回主界面。
    /// 榜单数据由 UI 直接读 <see cref="Leaderboard"/>（纯静态本地存储，不需要状态持有）。
    /// </summary>
    public sealed class LeaderboardState : IGameState
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
