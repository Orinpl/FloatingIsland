using UnityEngine;

namespace FloatingIsLand.UI
{
    /// <summary>全屏面板基类：一个流程状态对应一个面板，由 UIManager 统一互斥显隐。</summary>
    public abstract class UIPanel : MonoBehaviour
    {
        public bool IsVisible
        {
            get { return gameObject.activeSelf; }
        }

        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}
