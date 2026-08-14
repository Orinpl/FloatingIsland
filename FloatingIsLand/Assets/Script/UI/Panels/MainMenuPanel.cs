using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>主界面：开始游戏 / 排行榜 / 退出。按钮行为由 FlowUIAdapter 统一绑定到 GameFlow（面板保持哑视图）。</summary>
    public sealed class MainMenuPanel : UIPanel
    {
        public Button startButton;
        public Button leaderboardButton;
        public Button quitButton;

        private void Awake()
        {
            // 开场封面 + 循环背景视频，用的是宣传图（风脉城主视觉）那一版，与启动页同一素材。
            // 面板本身仍是哑视图：背景只是自己的皮，不碰任何流程。
            PanelVideoBackground.AttachTo(gameObject, "UI/splash_cover", "Video/splash_windvein");
        }
    }
}
