using FloatingIsLand.App;
using UnityEngine;
using UnityEngine.UI;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 按钮点击音。不手动往按钮上挂——<see cref="UISkin.ApplyButton"/> 套皮时统一补挂，
    /// 手牌/二选一模板被 Instantiate 时组件随克隆复制，各实例在自己的 Awake 里挂监听。
    /// 走 onClick 而不是 IPointerClickHandler：interactable=false 的按钮不该响。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSound : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClicked);
        }

        private static void OnClicked()
        {
            Sfx.Play(Sfx.UiClick);
        }
    }
}
