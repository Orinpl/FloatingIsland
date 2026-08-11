using FloatingIsLand.App;
using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 一关结算面板。两种形态由 <see cref="RunResult"/> 决定，面板本身不做判断（哑视图）：
    /// - **还能继续**（本关达通关分且不是最后一关）：显示「下一关」，隐藏上榜区；
    /// - **整局结束**（没达通关分，或已打穿最后一关）：显示昵称输入 + 上榜按钮。
    ///
    /// 局内进度不做保存，所以这里是玩家这一局唯一的留痕机会。
    /// </summary>
    public sealed class SettlementPanel : UIPanel
    {
        public Text titleText;
        public Text summaryText;
        public Button nextRunButton;
        public Button menuButton;

        [UnityEngine.Header("上榜区（仅整局结束时显示）")]
        public UnityEngine.GameObject submitRoot;
        public InputField nameInput;
        public Button submitButton;
        public Text submitResultText;

        /// <summary>刷新结算内容；返回本次是否处于「整局结束」形态（外部据此决定要不要等玩家上榜）。</summary>
        public bool SetResult(RunResult result)
        {
            if (result == null)
            {
                return false;
            }

            bool gameOver = result.IsGameOver;

            if (titleText != null)
            {
                titleText.text = result.GameCompleted
                    ? "通关！全部关卡打穿"
                    : (gameOver ? $"第 {result.StageId} 关 止步" : $"第 {result.StageId} 关 通过");
            }

            if (summaryText != null)
            {
                string clearLine = result.StageCleared
                    ? $"通关分 {result.ClearScore} —— 已达标"
                    : $"通关分 {result.ClearScore} —— 差 {result.ClearScore - result.TotalScore} 分";

                summaryText.text =
                    $"{result.StageName}\n\n"
                    + $"本关得分：{result.StageScore}\n"
                    + $"累计总分：{result.TotalScore}\n"
                    + $"{clearLine}\n"
                    + $"建筑组：{result.GroupsPlayed} / {result.GroupTotal}    已建 {result.BuildingsPlaced} 栋\n\n"
                    + result.EndReason;
            }

            if (nextRunButton != null)
            {
                nextRunButton.gameObject.SetActive(result.CanAdvance);
            }
            if (submitRoot != null)
            {
                submitRoot.SetActive(gameOver);
            }
            if (submitResultText != null)
            {
                submitResultText.text = string.Empty;
            }
            if (submitButton != null)
            {
                submitButton.interactable = gameOver;
            }

            return gameOver;
        }

        /// <summary>显示上榜结果。<paramref name="rank"/> ≤ 0 表示没能进榜。</summary>
        public void SetSubmitResult(int rank)
        {
            if (submitResultText != null)
            {
                submitResultText.text = rank > 0
                    ? $"已上榜，第 {rank} 名"
                    : $"没能进前 {Leaderboard.Capacity} 名，成绩未记录";
            }
            if (submitButton != null)
            {
                submitButton.interactable = false;
            }
            if (nameInput != null)
            {
                nameInput.interactable = false;
            }
        }
    }
}
