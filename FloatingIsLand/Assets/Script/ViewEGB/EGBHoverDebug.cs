using UnityEngine;

namespace FloatingIsLand.ViewEGB
{
    /// <summary>骨架验证用：左上角实时显示悬停格子，证明「鼠标 → EGB 网格 → 领域格子坐标」链路通了。M1 有正式预览后删除。</summary>
    public sealed class EGBHoverDebug : MonoBehaviour
    {
        [SerializeField] private EGBGridPresenter presenter;

        private void OnGUI()
        {
            if (presenter == null || !presenter.enabled)
            {
                return;
            }
            string text = presenter.TryGetHoveredCell(out int x, out int z, out int layer)
                ? $"悬停格子: ({x}, {z}) / 层 {layer} / 网格 {presenter.Width}×{presenter.Length}×{presenter.LayerCount}层"
                : "悬停格子: 网格外";
            GUI.Label(new Rect(10f, 10f, 500f, 30f), text);
        }
    }
}
