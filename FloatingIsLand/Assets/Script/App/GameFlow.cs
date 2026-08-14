using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 局外流程状态机（挂在 Boot 场景 AppRoot 上；Boot 场景常驻，因此无需 DontDestroyOnLoad）。
    /// 状态推进：Boot → MainMenu → Loading → Gameplay → Settlement → (Loading 下一关 / MainMenu)。
    /// UI 层只通过下方"UI 入口"公开方法驱动流程，并订阅 StateEntered / BootFailed 刷新界面
    /// （依赖方向 UI → App 单向，本类不知道任何面板类型；桥接见 UI 层 FlowUIAdapter）。
    /// </summary>
    public sealed class GameFlow : MonoBehaviour
    {
        public static GameFlow Instance { get; private set; }

        /// <summary>进入新状态后广播。顺序保证：先更新 CurrentStateId、再广播、最后才执行新状态 Enter。</summary>
        public event Action<GameStateId> StateEntered;

        /// <summary>Boot 初始化失败（配表缺失/损坏等），参数为给玩家看的错误信息。</summary>
        public event Action<string> BootFailed;

        public GameStateId CurrentStateId { get; private set; }

        /// <summary>当前这一关的局外参数（第几关、种子）；未开局（Boot/MainMenu）时为 null。</summary>
        public RunContext CurrentRun { get; private set; }

        /// <summary>当前局内会话；仅 Gameplay / Settlement 状态非 null。</summary>
        public GameSession CurrentSession { get; internal set; }

        /// <summary>最近一关的结算结果；仅 Settlement 状态可靠。</summary>
        public RunResult LastRunResult { get; internal set; }

        /// <summary>本局成绩上榜后的名次；0 = 还没提交，-1 = 提交了但没进榜。</summary>
        public int LastSubmittedRank { get; private set; }

        private readonly Dictionary<GameStateId, IGameState> _states = new Dictionary<GameStateId, IGameState>();
        private IGameState _current;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[流程] 场景里出现第二个 GameFlow，已销毁多余实例。", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _states.Add(GameStateId.Boot, new BootState(this));
            _states.Add(GameStateId.MainMenu, new MainMenuState());
            _states.Add(GameStateId.Leaderboard, new LeaderboardState());
            _states.Add(GameStateId.Loading, new LoadingState(this));
            _states.Add(GameStateId.Gameplay, new GameplayState(this));
            _states.Add(GameStateId.Settlement, new SettlementState());
        }

        private IEnumerator Start()
        {
            // 空一帧再进 Boot：保证同场景内的 UI 桥接（FlowUIAdapter.Start）先完成事件订阅。
            yield return null;
            ChangeState(GameStateId.Boot);
        }

        private void Update()
        {
            _current?.Tick();
        }

        // ---------- UI 入口（由 UI 层按钮调用；状态不匹配时忽略，防连点/误触） ----------

        /// <summary>主界面点"开始游戏"：开第 1 关（随机种子，分数从 0 起）。</summary>
        public void StartGame()
        {
            if (CurrentStateId != GameStateId.MainMenu)
            {
                return;
            }
            LastSubmittedRank = 0;
            CurrentRun = RunContext.CreateFirst();
            ChangeState(GameStateId.Loading);
        }

        /// <summary>
        /// 结算面板点"下一关"：换新地图（新种子）再开一关，**把累计总分带过去当基线**。
        /// 只有本关达到通关分才允许（<see cref="RunResult.CanAdvance"/>）。
        /// </summary>
        public void NextRun()
        {
            if (CurrentStateId != GameStateId.Settlement || LastRunResult == null || !LastRunResult.CanAdvance)
            {
                return;
            }
            CurrentRun = CurrentRun.CreateNext(LastRunResult.TotalScore);
            ChangeState(GameStateId.Loading);
        }

        /// <summary>结算面板点"回主界面"：卸载 Main 后回 MainMenu。</summary>
        public void ReturnToMainMenu()
        {
            if (CurrentStateId != GameStateId.Settlement)
            {
                return;
            }
            StartCoroutine(ReturnToMenuRoutine());
        }

        /// <summary>
        /// 结算面板提交成绩上榜（整局结束时才可用）。返回名次（1 起），没进榜返回 -1。
        /// 同一局只会真正写入一次，重复点按钮拿的是第一次的名次。
        /// </summary>
        public int SubmitScore(string playerName)
        {
            if (CurrentStateId != GameStateId.Settlement || LastRunResult == null || !LastRunResult.IsGameOver)
            {
                return -1;
            }
            if (LastSubmittedRank != 0)
            {
                return LastSubmittedRank;
            }

            LastSubmittedRank = Leaderboard.Submit(playerName, LastRunResult);
            return LastSubmittedRank;
        }

        /// <summary>主界面点"排行榜"。</summary>
        public void ShowLeaderboard()
        {
            if (CurrentStateId != GameStateId.MainMenu)
            {
                return;
            }
            ChangeState(GameStateId.Leaderboard);
        }

        /// <summary>排行榜点"返回"。</summary>
        public void CloseLeaderboard()
        {
            if (CurrentStateId != GameStateId.Leaderboard)
            {
                return;
            }
            ChangeState(GameStateId.MainMenu);
        }

        /// <summary>主界面点"退出游戏"。</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// HUD 的「进入下一关 / 结算」按钮：玩家达到通关分后主动收官。
        /// 没达通关分时同样可以点——那就是提前认输，整局到此结束并进入上榜流程。
        /// </summary>
        public void EndCurrentRun()
        {
            if (CurrentStateId != GameStateId.Gameplay || CurrentSession == null)
            {
                return;
            }
            CurrentSession.EndRun(playerRequested: true);
        }

        // ---------- 状态机内部 ----------

        /// <summary>
        /// 切换状态：Exit 旧 → 更新 CurrentStateId → 广播 StateEntered → Enter 新。
        /// 约定：IGameState.Enter 内禁止同步再调本方法（见 IGameState 注释）。
        /// </summary>
        internal void ChangeState(GameStateId id)
        {
            _current?.Exit();
            _current = _states[id];
            CurrentStateId = id;
            ApplyBgm(id);
            StateEntered?.Invoke(id);
            _current.Enter();
        }

        /// <summary>
        /// BGM 跟状态走：主界面曲盖 MainMenu / Leaderboard，局内曲盖 Gameplay；
        /// Boot / Loading / Settlement 不切——Loading 延续菜单曲到进局那刻，结算浮在局内场景上延续局内曲。
        /// 同曲目重复请求在 <see cref="Bgm"/> 内部是空操作，下一关的 Loading → Gameplay 不会重头播。
        /// </summary>
        private static void ApplyBgm(GameStateId id)
        {
            switch (id)
            {
                case GameStateId.MainMenu:
                case GameStateId.Leaderboard:
                    Bgm.Play(Bgm.MainMenu);
                    break;
                case GameStateId.Gameplay:
                    Bgm.Play(Bgm.Gameplay);
                    break;
            }
        }

        internal void RaiseBootFailed(string message)
        {
            BootFailed?.Invoke(message);
        }

        private IEnumerator ReturnToMenuRoutine()
        {
            CurrentSession = null;
            CurrentRun = null;
            yield return SceneLoader.UnloadMainAsync();
            ChangeState(GameStateId.MainMenu);
        }
    }
}
