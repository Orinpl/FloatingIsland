using System;
using System.Collections;
using FloatingIsLand.Config;
using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>
    /// Boot：全局初始化（目前只有配表加载；后续全局服务——种子随机服务、存档等——都挂这里）。
    /// 成功 → MainMenu；失败 → 停留本状态并广播 BootFailed（UI 显示错误信息）。
    /// </summary>
    public sealed class BootState : IGameState
    {
        private readonly GameFlow _flow;

        public BootState(GameFlow flow)
        {
            _flow = flow;
        }

        public void Enter()
        {
            _flow.StartCoroutine(BootRoutine());
        }

        public void Exit()
        {
        }

        public void Tick()
        {
        }

        private IEnumerator BootRoutine()
        {
            // 让"初始化中…"界面先渲染一帧，同时满足"Enter 内不同步 ChangeState"的约定。
            yield return null;

            string error = null;
            try
            {
                UnityTableLoader.LoadFromResources();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                error = e.Message;
            }

            if (error != null)
            {
                _flow.RaiseBootFailed(error);
                yield break;
            }

            _flow.ChangeState(GameStateId.MainMenu);
        }
    }
}
