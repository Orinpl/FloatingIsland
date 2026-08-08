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

            HudPanel hud = _ui.Get<HudPanel>();
            hud.endRunButton.onClick.AddListener(_flow.EndCurrentRunForDebug);

            SettlementPanel settlement = _ui.Get<SettlementPanel>();
            settlement.nextRunButton.onClick.AddListener(_flow.NextRun);
            settlement.menuButton.onClick.AddListener(_flow.ReturnToMainMenu);
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
                case GameStateId.Loading:
                    _ui.ShowOnly<LoadingPanel>().SetMessage($"第 {_flow.CurrentRun.RunIndex} 关 加载中…");
                    break;
                case GameStateId.Gameplay:
                    _ui.ShowOnly<HudPanel>().SetRunInfo($"第 {_flow.CurrentRun.RunIndex} 关 · 种子 {_flow.CurrentRun.Seed}");
                    break;
                case GameStateId.Settlement:
                    _ui.ShowOnly<SettlementPanel>().SetResult(_flow.CurrentRun.RunIndex, _flow.LastRunResult);
                    break;
            }
        }

        private void OnBootFailed(string message)
        {
            _ui.ShowOnly<LoadingPanel>().SetMessage("启动失败：\n" + message);
        }
    }
}
