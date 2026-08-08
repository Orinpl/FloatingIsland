using FloatingIsLand.View;
using SoulGames.EasyGridBuilderPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FloatingIsLand.ViewEGB
{
    /// <summary>
    /// IGridPresenter 的 EGB Pro 2 适配器（浅接入，职责边界见 Docs/GRID_INTEGRATION.md §2）。
    /// 本目录无 asmdef、编入 Assembly-CSharp——是仅有的允许 using SoulGames 的地方。
    /// EGB 在这里只做表现：不记占用、不做校验，规则真相在领域层。
    /// 高度层映射：领域 (x, z, layer) ↔ EGB (Vector2Int(x, z), verticalGridIndex = layer)。
    /// </summary>
    public sealed class EGBGridPresenter : MonoBehaviour, IGridPresenter
    {
        [Tooltip("场景里的 EGB 网格系统；垂直层数/层高在其 Inspector 配置（层高须与领域层高折算系数一致，GRID_INTEGRATION.md §3）")]
        [SerializeField] private EasyGridBuilderPro gridSystem;

        [Tooltip("骨架版默认网格尺寸；M1 起由地图数据调用 BuildGrid 驱动")]
        [SerializeField] private int defaultWidth = 32;
        [SerializeField] private int defaultLength = 32;

        public int Width
        {
            get { return gridSystem.GetGridWidth(); }
        }

        public int Length
        {
            get { return gridSystem.GetGridLength(); }
        }

        public int LayerCount
        {
            get { return gridSystem.GetVerticalGridsCount(); }
        }

        public float CellSize
        {
            get { return gridSystem.GetCellSize(); }
        }

        private System.Collections.IEnumerator Start()
        {
            if (gridSystem == null)
            {
                Debug.LogError("[网格] EGBGridPresenter 未指定 gridSystem（可用菜单 Tools/框架/给 Main 场景接入 EGB 网格 重新接线）。", this);
                enabled = false;
                yield break;
            }

            // EGB 的内部格子列表（gridList）在它自己的 Start 里才创建（EasyGridBuilderProXZ.SetupVerticalGrids），
            // 与本组件 Start 同帧且顺序不保证——空一帧再动它，否则 SetGridWidthAndLength 会 NRE。
            yield return null;
            BuildGrid(defaultWidth, defaultLength);
        }

        public void BuildGrid(int width, int length)
        {
            gridSystem.SetGridWidthAndLength(width, length, true);
        }

        public Vector3 CellToWorld(int x, int z, int layer)
        {
            return gridSystem.GetCellWorldPosition(new Vector2Int(x, z), layer);
        }

        public bool WorldToCell(Vector3 worldPosition, int layer, out int x, out int z)
        {
            Vector2Int cell = gridSystem.GetCellPosition(worldPosition, layer);
            x = cell.x;
            z = cell.y;
            return gridSystem.IsWithinGridBounds(cell, layer);
        }

        public bool TryGetHoveredCell(out int x, out int z, out int layer)
        {
            // 骨架版只探测第 0 层平面。TODO(M1)：接入地形数据后，从最高层往低层找第一个"该格在该层有地形"的层，
            // 高台碰撞体挡低层的行为届时一并处理（GRID_INTEGRATION.md §3）。
            x = 0;
            z = 0;
            layer = 0;

            Camera camera = Camera.main;
            if (camera == null || Mouse.current == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var plane = new Plane(Vector3.up, new Vector3(0f, gridSystem.transform.position.y, 0f));
            if (!plane.Raycast(ray, out float distance))
            {
                return false;
            }

            return WorldToCell(ray.GetPoint(distance), 0, out x, out z);
        }
    }
}
