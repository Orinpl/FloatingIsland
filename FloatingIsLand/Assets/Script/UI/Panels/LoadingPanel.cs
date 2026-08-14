using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>Boot 初始化与进关加载共用的过场面板；Boot 失败时也用它显示错误信息。</summary>
    public sealed class LoadingPanel : UIPanel
    {
        public Text messageText;

        private PanelVideoBackground _background;

        private void Awake()
        {
            // 启动页动画：风脉城主视觉。这块面板 Boot 与进关加载共用，默认关掉背景，
            // 只有 Boot 状态由 FlowUIAdapter 打开——进关加载不该再放一遍启动动画。
            _background = PanelVideoBackground.AttachTo(gameObject, "UI/splash_cover", "Video/splash_windvein");
            SetBackgroundVisible(false);
        }

        /// <summary>启动动画开关，由 FlowUIAdapter 按流程状态调用。</summary>
        public void SetBackgroundVisible(bool visible)
        {
            if (_background != null)
            {
                _background.SetVisible(visible);
            }
        }

        public void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }
    }
}
