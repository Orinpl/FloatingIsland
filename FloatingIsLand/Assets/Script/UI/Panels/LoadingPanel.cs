using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>Boot 初始化与进关加载共用的过场面板；Boot 失败时也用它显示错误信息。</summary>
    public sealed class LoadingPanel : UIPanel
    {
        public Text messageText;

        public void SetMessage(string message)
        {
            if (messageText != null)
            {
                messageText.text = message;
            }
        }
    }
}
