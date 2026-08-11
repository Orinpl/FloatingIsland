using System;
using FloatingIsLand.App;
using FloatingIsLand.GameInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FloatingIsLand.ViewEGB
{
    /// <summary>
    /// 格子交互反馈（骨架版）：悬停格半透明高亮 + 左键点击选中格高亮。
    /// 自绘 Quad 实现，不走 EGB 的 SelectMode/CellIndicator（浅接入原则：交互反馈归我们 View 侧，
    /// 见 GRID_INTEGRATION.md §2）。M1 摆放流程接入后，选中事件 CellClicked 就是"选格 → 干跑预览"的入口。
    /// </summary>
    public sealed class GridCellHighlighter : MonoBehaviour
    {
        [SerializeField] private EGBGridPresenter presenter;
        [SerializeField] private Color hoverColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color selectedColor = new Color(0.3f, 0.85f, 1f, 0.5f);
        [Tooltip("高亮片抬离网格面的高度，防 Z-fighting")]
        [SerializeField] private float surfaceOffset = 0.05f;

        /// <summary>左键选中某格时广播，参数 = (x, z, layer)。M1 起摆放流程订阅它做干跑预览。</summary>
        public event Action<Vector3Int> CellClicked;

        /// <summary>当前选中格 (x, z, layer)；未选中返回 false。</summary>
        public bool TryGetSelectedCell(out Vector3Int cell)
        {
            cell = _selectedCell;
            return _hasSelection;
        }

        private Transform _hoverQuad;
        private Transform _selectedQuad;
        private bool _hasSelection;
        private Vector3Int _selectedCell;

        private void Start()
        {
            _hoverQuad = CreateQuad("HoverCell", hoverColor);
            _selectedQuad = CreateQuad("SelectedCell", selectedColor);
        }

        private void Update()
        {
            if (presenter == null || !presenter.enabled)
            {
                return;
            }

            if (IsPlacingBuilding())
            {
                // 建造模式下地格反馈全归 BuildPlacementController 的落点格标记：
                // 那边是整块占地、逐格红绿、跟着朝向转，而这里只有光标下孤零零一个白框，
                // 既不体现占地也不跟旋转——两个一起画，玩家只会以为"转了但格子没转"。
                _hoverQuad.gameObject.SetActive(false);
                _selectedQuad.gameObject.SetActive(false);
                return;
            }

            bool hovered = presenter.TryGetHoveredCell(out int x, out int z, out int layer);
            if (hovered)
            {
                PlaceQuad(_hoverQuad, x, z, layer);
            }
            else
            {
                _hoverQuad.gameObject.SetActive(false);
            }

            if (hovered && ClickedThisFrame())
            {
                _hasSelection = true;
                _selectedCell = new Vector3Int(x, z, layer);
                CellClicked?.Invoke(_selectedCell);
            }

            if (_hasSelection)
            {
                PlaceQuad(_selectedQuad, _selectedCell.x, _selectedCell.y, _selectedCell.z);
            }
        }

        private Transform CreateQuad(string quadName, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = quadName;
            Destroy(go.GetComponent<Collider>()); // 不能挡住 EGB 与悬停查询的射线

            var renderer = go.GetComponent<MeshRenderer>();
            // Sprites/Default：无光照、支持顶点色透明，骨架够用；正式版随 View overlay（风箭头/覆盖高亮）统一换专用材质
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default")) { color = color };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Quad 默认立面，转平贴地
            go.SetActive(false);
            return go.transform;
        }

        private void PlaceQuad(Transform quad, int x, int z, int layer)
        {
            float cellSize = presenter.CellSize;
            // CellToWorld 返回格子角点（IGridPresenter 注释），中心补半格；抬高一点防 Z-fighting
            Vector3 center = presenter.CellToWorld(x, z, layer) + new Vector3(cellSize * 0.5f, surfaceOffset, cellSize * 0.5f);
            quad.position = center;
            quad.localScale = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 1f);
            quad.gameObject.SetActive(true);
        }

        /// <summary>当前是否正拿着一张建筑手牌在摆放。</summary>
        private static bool IsPlacingBuilding()
        {
            GameFlow flow = GameFlow.Instance;
            GameSession session = flow != null ? flow.CurrentSession : null;
            return session != null && session.SelectedBlueprint != null;
        }

        /// <summary>
        /// 本帧玩家点了地格没有。PC 是左键按下，手机是一次点击（抬手且没拖动，见 PointerInput）。
        /// UI 遮挡判定走 PointerInput 的按位置射线——手机上不能用 EventSystem 的无参重载，
        /// 那个查的是鼠标 pointerId，手指恒返回 false，会出现「点 HUD 的同时还选中了按钮背后的格子」。
        /// </summary>
        private static bool ClickedThisFrame()
        {
            if (PointerInput.IsTouchMode)
            {
                // 遮挡要按**这一下点在哪**判，不能按黏性悬停判：点 HUD 按钮时悬停还停在地面上，
                // 拿悬停去问就会得出「没压着 UI」，于是按个按钮顺带把格子也选了
                return PointerInput.TapThisFrame && !PointerInput.IsOverUI(PointerInput.TapPosition);
            }
            Mouse mouse = Mouse.current;
            return mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerInput.IsHoverOverUI();
        }
    }
}
