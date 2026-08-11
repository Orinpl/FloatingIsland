using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>主界面：开始游戏 / 排行榜 / 退出。按钮行为由 FlowUIAdapter 统一绑定到 GameFlow（面板保持哑视图）。</summary>
    public sealed class MainMenuPanel : UIPanel
    {
        public Button startButton;
        public Button leaderboardButton;
        public Button quitButton;
    }
}
