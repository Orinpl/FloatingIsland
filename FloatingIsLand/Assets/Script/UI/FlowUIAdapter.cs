using FloatingIsLand.App;
using UnityEngine;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 流程 → UI 的唯一桥接（挂在 UIRoot，与 UIManager 同物体）：
    /// 订阅 GameFlow.StateEntered 切换面板，把按钮点击转发回 GameFlow 公开入口。
    /// 依赖方向 UI → App 单向：GameFlow 不知道任何面板类型，换 UI 实现只改本类。
    /// </summary>
    [RequireComponent(typeof(UIManager))]
    public sealed class FlowUIAdapter : MonoBehaviour
    {
        private UIManager _ui;
        private GameFlow _flow;

        private void Start()
        {
            _ui = GetComponent<UIManager>();
            _flow = GameFlow.Instance;
            if (_flow == null)
            {
                Debug.LogError("[UI] 找不到 GameFlow（Boot 场景缺 AppRoot？可用菜单 Tools/框架/生成启动场景 重建）。", this);
                enabled = false;
                return;
            }

            _flow.StateEntered += OnStateEntered;
            _flow.BootFailed += OnBootFailed;
            WireButtons();
        }

        private void OnDestroy()
        {
            if (_flow == null)
            {
                return;
            }
            _flow.StateEntered -= OnStateEntered;
            _flow.BootFailed -= OnBootFailed;
        }

        private void WireButtons()
        {
            MainMenuPanel menu = _ui.Get<MainMenuPanel>();
            menu.startButton.onClick.AddListener(_flow.StartGame);
            menu.quitButton.onClick.AddListener(_flow.QuitGame);
            if (menu.leaderboardButton != null)
            {
                menu.leaderboardButton.onClick.AddListener(_flow.ShowLeaderboard);
            }

            LeaderboardPanel leaderboard = _ui.Get<LeaderboardPanel>();
            if (leaderboard.backButton != null)
            {
                leaderboard.backButton.onClick.AddListener(_flow.CloseLeaderboard);
            }

            HudPanel hud = _ui.Get<HudPanel>();
            hud.endRunButton.onClick.AddListener(_flow.EndCurrentRun);

            SettlementPanel settlement = _ui.Get<SettlementPanel>();
            settlement.nextRunButton.onClick.AddListener(_flow.NextRun);
            settlement.menuButton.onClick.AddListener(_flow.ReturnToMainMenu);
            if (settlement.submitButton != null)
            {
                settlement.submitButton.onClick.AddListener(OnSubmitScoreClicked);
            }
        }

        /// <summary>结算面板点「提交成绩」：写本地榜单并回显名次。</summary>
        private void OnSubmitScoreClicked()
        {
            SettlementPanel settlement = _ui.Get<SettlementPanel>();
            string playerName = settlement.nameInput != null ? settlement.nameInput.text : string.Empty;
            settlement.SetSubmitResult(_flow.SubmitScore(playerName));
        }

        private void OnStateEntered(GameStateId state)
        {
            switch (state)
            {
                case GameStateId.Boot:
                    _ui.ShowOnly<LoadingPanel>().SetMessage("初始化中…");
                    break;
                case GameStateId.MainMenu:
                    _ui.ShowOnly<MainMenuPanel>();
                    break;
                case GameStateId.Leaderboard:
                    // 刚打完的那局高亮出来，玩家不用自己在榜里找
                    _ui.ShowOnly<LeaderboardPanel>().Refresh(_flow.LastSubmittedRank);
                    break;
                case GameStateId.Loading:
                    _ui.ShowOnly<LoadingPanel>().SetMessage($"第 {_flow.CurrentRun.StageId} 关 加载中…");
                    break;
                case GameStateId.Gameplay:
                    _ui.ShowOnly<HudPanel>().SetRunInfo(BuildRunInfo());
                    break;
                case GameStateId.Settlement:
                    _ui.ShowOnly<SettlementPanel>().SetResult(_flow.LastRunResult);
                    break;
            }
        }

        private string BuildRunInfo()
        {
            RunContext run = _flow.CurrentRun;
            GameSession session = _flow.CurrentSession;
            int stageCount = session != null ? session.StageCount : 0;
            string stageLabel = stageCount > 0
                ? $"第 {run.StageId} / {stageCount} 关"
                : $"第 {run.StageId} 关";
            // 带上基线分，玩家才知道「本关门槛为什么这么高」
            return run.CarryScore > 0
                ? $"{stageLabel} · 入关分 {run.CarryScore} · 种子 {run.Seed}"
                : $"{stageLabel} · 种子 {run.Seed}";
        }

        private void OnBootFailed(string message)
        {
            _ui.ShowOnly<LoadingPanel>().SetMessage("启动失败：\n" + message);
        }
    }
}
