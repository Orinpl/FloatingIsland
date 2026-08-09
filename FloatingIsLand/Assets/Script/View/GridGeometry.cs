using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 格子 ↔ 世界坐标的纯数学，**编辑器刷子与运行时表现共用的唯一一份**。
    ///
    /// 为什么不直接用 <see cref="IGridPresenter"/>：EGB 的 GetCellWorldPosition / GetCellPosition 都要走
    /// gridList[layer]，而 gridList 只在运行时 SetupVerticalGrids 里创建（EasyGridBuilderProXZ.cs:227-242），
    /// 编辑器态是 null。刷子必须在编辑器里算坐标，只能自己算。
    ///
    /// 本结构精确复刻 EGB 的公式，两处来源：
    ///   · 层原点 —— EasyGridBuilderProXZ.CalculateGridOrigin (L244-256)
    ///   · 格角点 —— GridXZ.GetCellWorldPosition (L1028-1032)
    ///   · 反查取整 —— GridXZ.GetCellPosition，FloorToInt
    /// 返回的是格子**角点**（min 角），与 IGridPresenter.CellToWorld 的实测结论一致；要格心用 <see cref="CellCenter"/>。
    ///
    /// 两边各写一遍必然漂移，漂移表现为"编辑器刷的位置和运行时差半格"，极难查——所以只留这一份。
    /// </summary>
    public readonly struct GridGeometry
    {
        /// <summary>EGB 网格系统的 transform.position。</summary>
        public readonly Vector3 GridPosition;

        public readonly int Width;
        public readonly int Length;
        public readonly float CellSize;

        /// <summary>层间世界高度（EGB 的 verticalGridHeight）。</summary>
        public readonly float LayerHeight;

        /// <summary>true = EGB 的 GridOrigin.Center（网格以 transform 为中心），false = GridOrigin.Default（transform 在 min 角）。</summary>
        public readonly bool CenterOrigin;

        public GridGeometry(Vector3 gridPosition, int width, int length, float cellSize, float layerHeight, bool centerOrigin)
        {
            GridPosition = gridPosition;
            Width = width;
            Length = length;
            CellSize = cellSize;
            LayerHeight = layerHeight;
            CenterOrigin = centerOrigin;
        }

        /// <summary>参数是否可用于坐标换算（cellSize 为 0 会除零，尺寸为 0 说明还没接线）。</summary>
        public bool IsValid
        {
            get { return Width > 0 && Length > 0 && CellSize > 0f; }
        }

        /// <summary>指定层的原点（该层 (0,0) 格的角点）。复刻 CalculateGridOrigin。</summary>
        public Vector3 LayerOrigin(int layer)
        {
            float offsetX = CenterOrigin ? (CellSize * Width / 2f) * -1f : 0f;
            float offsetZ = CenterOrigin ? (CellSize * Length / 2f) * -1f : 0f;
            return GridPosition + new Vector3(offsetX, LayerHeight * layer, offsetZ);
        }

        /// <summary>格子角点（min 角）的世界坐标。</summary>
        public Vector3 CellCorner(int x, int z, int layer)
        {
            return LayerOrigin(layer) + new Vector3(x, 0f, z) * CellSize;
        }

        /// <summary>格心的世界坐标。</summary>
        public Vector3 CellCenter(int x, int z, int layer)
        {
            return CellCorner(x, z, layer) + new Vector3(CellSize * 0.5f, 0f, CellSize * 0.5f);
        }

        /// <summary>世界坐标 → 格子坐标（FloorToInt，与 EGB 一致）。**不做边界判断**，界外会得到负数或超界值。</summary>
        public void WorldToCellUnclamped(Vector3 worldPosition, int layer, out int x, out int z)
        {
            Vector3 local = worldPosition - LayerOrigin(layer);
            x = Mathf.FloorToInt(local.x / CellSize);
            z = Mathf.FloorToInt(local.z / CellSize);
        }

        /// <summary>世界坐标 → 格子坐标；越界返回 false。</summary>
        public bool WorldToCell(Vector3 worldPosition, int layer, out int x, out int z)
        {
            WorldToCellUnclamped(worldPosition, layer, out x, out z);
            return Contains(x, z);
        }

        /// <summary>格子坐标是否在网格尺寸内。</summary>
        public bool Contains(int x, int z)
        {
            return x >= 0 && x < Width && z >= 0 && z < Length;
        }

        /// <summary>
        /// 射线 × 指定层的水平面 → 该层格子坐标；不相交或越界返回 false。
        /// 走数学平面而不是碰撞体，编辑器态和运行时都可用（GRID_INTEGRATION §3 坐标契约末条）。
        /// </summary>
        public bool RaycastCell(Ray ray, int layer, out int x, out int z)
        {
            x = 0;
            z = 0;
            if (!IsValid)
            {
                return false;
            }

            var plane = new Plane(Vector3.up, LayerOrigin(layer));
            float distance;
            if (!plane.Raycast(ray, out distance))
            {
                return false;
            }
            return WorldToCell(ray.GetPoint(distance), layer, out x, out z);
        }
    }
}
