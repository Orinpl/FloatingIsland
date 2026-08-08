using FloatingIsLand.App;
using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>终局结算面板：显示本关结果，出口"下一关 / 回主界面"。M5 结算实装后扩展为逐条分数明细列表。</summary>
    public sealed class SettlementPanel : UIPanel
    {
        public Text summaryText;
        public Button nextRunButton;
        public Button menuButton;

        public void SetResult(int runIndex, RunResult result)
        {
            if (summaryText == null || result == null)
            {
                return;
            }
            summaryText.text = $"第 {runIndex} 关 结算\n\n总分：{result.TotalScore}\n\n{result.EndReason}";
        }
    }
}
