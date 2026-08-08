namespace FloatingIsLand.App
{
    /// <summary>
    /// Gameplay：托管 GameSession 局内循环，订阅其结束事件；结束后携带结算结果进 Settlement。
    /// </summary>
    public sealed class GameplayState : IGameState
    {
        private readonly GameFlow _flow;
        private GameSession _session;

        public GameplayState(GameFlow flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            _session = _flow.CurrentSession;
            _session.Ended += OnRunEnded;
        }

        public void Exit()
        {
            if (_session != null)
            {
                _session.Ended -= OnRunEnded;
                _session = null;
            }
        }

        public void Tick()
        {
        }

        private void OnRunEnded(RunResult result)
        {
            _flow.LastRunResult = result;
            _flow.ChangeState(GameStateId.Settlement);
        }
    }
}
