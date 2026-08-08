using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 局内 HUD（骨架版：关数/种子信息 + 占位"结束本局"按钮）。
    /// M2 起替换为正式 HUD：顶部资源条、建筑组二选一、手牌栏、摆放预览明细（PROJECT_BUILD §4.8）；
    /// 占位按钮在 GameSession 结束判定实装后移除。
    /// </summary>
    public sealed class HudPanel : UIPanel
    {
        public Text runInfoText;
        public Button endRunButton;

        public void SetRunInfo(string info)
        {
            if (runInfoText != null)
            {
                runInfoText.text = info;
            }
        }
    }
}
