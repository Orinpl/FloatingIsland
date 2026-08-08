using System.Collections;

namespace FloatingIsLand.App
{
    /// <summary>
    /// Loading：重载 Main 场景 → （M1 起）按种子生成地图/构建领域层 → 创建 GameSession → Gameplay。
    /// </summary>
    public sealed class LoadingState : IGameState
    {
        private readonly GameFlow _flow;

        public LoadingState(GameFlow flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            _flow.StartCoroutine(LoadRoutine());
        }

        public void Exit()
        {
        }

        public void Tick()
        {
        }

        private IEnumerator LoadRoutine()
        {
            yield return SceneLoader.ReloadMainAsync();

            // TODO(M1)：按 _flow.CurrentRun.Seed 生成地图 → 构建领域层状态 → 绑定 View 渲染（PROJECT_BUILD §4.1/§4.8）。
            _flow.CurrentSession = new GameSession(_flow.CurrentRun);

            _flow.ChangeState(GameStateId.Gameplay);
        }
    }
}
