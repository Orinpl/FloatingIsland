namespace FloatingIsLand.App
{
    /// <summary>局外流程状态（状态图与职责详见 Docs/BOOT_FRAMEWORK.md）。</summary>
    public enum GameStateId
    {
        /// <summary>启动初始化：配表加载、全局服务；失败停留本状态并广播 BootFailed。</summary>
        Boot,

        /// <summary>主界面（等待玩家点开始）。</summary>
        MainMenu,

        /// <summary>进入一局前的加载：重载 Main 场景 + 地图生成 + 会话构建。</summary>
        Loading,

        /// <summary>局内游玩（托管 GameSession 核心循环）。</summary>
        Gameplay,

        /// <summary>终局结算展示（出口：下一关 / 回主界面）。</summary>
        Settlement,
    }
}
