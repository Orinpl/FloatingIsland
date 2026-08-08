namespace FloatingIsLand.App
{
    /// <summary>
    /// 局外状态接口。约定：Enter 内禁止同步调用 GameFlow.ChangeState
    /// （需要立即转移的状态请开协程延迟一帧，如 BootState），
    /// 保证 StateEntered 事件严格按真实进入顺序广播。
    /// </summary>
    public interface IGameState
    {
        void Enter();

        void Exit();

        /// <summary>每帧驱动（由 GameFlow.Update 转发；无逻辑可空实现）。</summary>
        void Tick();
    }
}
