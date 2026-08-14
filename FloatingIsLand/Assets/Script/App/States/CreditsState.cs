namespace FloatingIsLand.App
{
    /// <summary>
    /// 开发者名单：从主界面进入的纯展示状态，出口只有 GameFlow.CloseCredits() 回主界面。
    /// 名单是静态内容，直接写在 UI 层 CreditsPanel 里，状态不持有任何数据。
    /// </summary>
    public sealed class CreditsState : IGameState
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
