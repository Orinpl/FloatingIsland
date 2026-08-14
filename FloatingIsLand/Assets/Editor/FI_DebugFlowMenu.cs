using FloatingIsLand.App;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 流程调试菜单：让自动化（MCP / 菜单队列）不点 UI 也能把局跑起来。
    /// 「开始游戏」只是替代主界面按钮那一下——之后的 Loading→Gameplay 仍走正常状态机。
    /// </summary>
    public static class FI_DebugFlowMenu
    {
        [MenuItem("Tools/FI/Debug/开始游戏（需在 Play 模式主界面）")]
        private static void StartGame()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[流程调试] 未在 Play 模式，先进 Play 再点。");
                return;
            }

            GameFlow flow = GameFlow.Instance;
            if (flow == null)
            {
                Debug.LogWarning("[流程调试] 找不到 GameFlow（当前不是从 Boot 场景启动？）。");
                return;
            }

            flow.StartGame(); // 状态不是 MainMenu 时它自己会忽略，防误触逻辑复用
            Debug.Log($"[流程调试] 已请求开始游戏，当前状态：{flow.CurrentStateId}");
        }
    }
}
