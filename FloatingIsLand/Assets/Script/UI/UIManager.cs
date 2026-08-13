using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 面板注册与互斥显示：Awake 时收集 UIRoot 下所有 UIPanel（含未激活）按类型索引，并全部隐藏。
    /// 骨架版策略是"同屏只显示一个全屏面板"；后续需要叠加弹窗时再扩展面板栈。
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        private readonly Dictionary<Type, UIPanel> _panels = new Dictionary<Type, UIPanel>();

        private void Awake()
        {
            // 先套皮再收面板：列表模板（手牌 / 二选一）也在这一遍里被刷到，
            // 之后 Instantiate 出来的条目就天然带皮，不用每次刷新手牌再补一次。
            UISkin.Apply(transform);

            foreach (UIPanel panel in GetComponentsInChildren<UIPanel>(true))
            {
                Type type = panel.GetType();
                if (_panels.ContainsKey(type))
                {
                    Debug.LogError($"[UI] 同类型面板重复：{type.Name}（UIRoot 下同一面板只允许一个实例）。", panel);
                    continue;
                }
                _panels.Add(type, panel);
                panel.Hide();
            }
        }

        public T Get<T>() where T : UIPanel
        {
            UIPanel panel;
            if (_panels.TryGetValue(typeof(T), out panel))
            {
                return (T)panel;
            }
            throw new KeyNotFoundException(
                $"[UI] 找不到面板 {typeof(T).Name}（Boot 场景 UIRoot 下缺该面板节点？可用菜单 Tools/框架/生成启动场景 重建）。");
        }

        /// <summary>只显示指定面板，隐藏其余所有面板；返回该面板便于链式设置内容。</summary>
        public T ShowOnly<T>() where T : UIPanel
        {
            T target = Get<T>();
            foreach (UIPanel panel in _panels.Values)
            {
                if (!ReferenceEquals(panel, target))
                {
                    panel.Hide();
                }
            }
            target.Show();
            return target;
        }
    }
}
