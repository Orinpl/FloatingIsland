using UnityEngine;

namespace FloatingIsLand.ViewEGB
{
    /// <summary>骨架验证用：左上角实时显示悬停格子，证明「鼠标 → EGB 网格 → 领域格子坐标」链路通了。M1 有正式预览后删除。</summary>
    public sealed class EGBHoverDebug : MonoBehaviour
    {
        [SerializeField] private EGBGridPresenter presenter;

        /// <summary>OnGUI 走的是 IMGUI，够不着 UISkin 那趟刷字重，只能自己配一份加粗深字。</summary>
        private GUIStyle _style;

        private void OnGUI()
        {
            if (presenter == null || !presenter.enabled)
            {
                return;
            }
            string text = presenter.TryGetHoveredCell(out int x, out int z, out int layer)
                ? $"悬停格子: ({x}, {z}) / 层 {layer} / 网格 {presenter.Width}×{presenter.Length}×{presenter.LayerCount}层"
                : "悬停格子: 网格外";

            EnsureStyle();
            var rect = new Rect(10f, 10f, 500f, 30f);
            // IMGUI 没有描边，用一块半透明底板把字和天空隔开——这行压的是浅到发白的天空
            GUI.color = new Color(1f, 0.97f, 0.90f, 0.75f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, text, _style);
        }

        private void EnsureStyle()
        {
            // GUI.skin 只在 OnGUI 里有效，所以建在这儿而不是 Awake；每帧新建会白白产生垃圾，缓存一份
            if (_style != null)
            {
                return;
            }
            _style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _style.normal.textColor = new Color(0.16f, 0.12f, 0.07f, 1f);
            _style.padding = new RectOffset(6, 6, 4, 4);
        }
    }
}
