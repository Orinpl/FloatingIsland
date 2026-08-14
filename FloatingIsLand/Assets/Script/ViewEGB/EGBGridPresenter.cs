using FloatingIsLand.Domain.Map;
using FloatingIsLand.GameInput;
using FloatingIsLand.View;
using SoulGames.EasyGridBuilderPro;
using UnityEngine;

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

        /// <summary>当前局内地图；<see cref="BindTerrain"/> 绑定，用于悬停的层归属与层数。</summary>
        private MapSnapshot _terrain;

        /// <summary>地图逐层高度的缓存数组（米）；null = 地图没配，等距口径。BindTerrain 时重建。</summary>
        private float[] _layerYOffsets;

        public int LayerCount
        {
            // 快照的层数是正本——EGB 的 verticalGridsCount 只是场景骨架配置（恒为 1），
            // 多层地图的层高/层数全走 GridGeometry，不依赖插件的垂直格子列表
            get { return _terrain != null ? _terrain.LayerCount : gridSystem.GetVerticalGridsCount(); }
        }

        public float CellSize
        {
            get { return gridSystem.GetCellSize(); }
        }

        /// <summary>
        /// 每次读取都按 EGB 当前的序列化参数现算——BuildGrid 改了尺寸后原点会变，缓存会过期。
        /// 这些 getter 都是字段直返（EasyGridBuilderProXZ.cs:4043-4089），不碰运行时才建的 gridList，
        /// 所以编辑器态也能取（地形刷子就靠这个）。
        /// </summary>
        public GridGeometry Geometry
        {
            get
            {
                return new GridGeometry(
                    gridSystem.transform.position,
                    gridSystem.GetGridWidth(),
                    gridSystem.GetGridLength(),
                    gridSystem.GetCellSize(),
                    gridSystem.GetVerticalGridHeight(),
                    gridSystem.GetGridOriginType() == GridOrigin.Center,
                    _layerYOffsets);
            }
        }

        /// <summary>EGB 内部格子列表已就绪、可以安全调用 BuildGrid / 坐标转换。地图装载要等它（MapBootstrap）。</summary>
        public bool IsReady { get; private set; }

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
            IsReady = true;
        }

        public void BuildGrid(int width, int length)
        {
            gridSystem.SetGridWidthAndLength(width, length, true);
        }

        public void BindTerrain(MapSnapshot snapshot)
        {
            _terrain = snapshot;

            _layerYOffsets = null;
            if (snapshot != null && snapshot.LayerHeights != null && snapshot.LayerHeights.Count > 0)
            {
                _layerYOffsets = new float[snapshot.LayerHeights.Count];
                for (int i = 0; i < _layerYOffsets.Length; i++)
                {
                    _layerYOffsets[i] = snapshot.LayerHeights[i];
                }
            }
        }

        /// <summary>
        /// 坐标换算走 <see cref="GridGeometry"/> 而不是 EGB 的 GetCellWorldPosition/GetCellPosition：
        /// 那两个 API 要索引运行时才建的 gridList[layer]，而场景网格只配了 1 个垂直层——
        /// 多层地图取 layer ≥ 1 会越界。GridGeometry 精确复刻了 EGB 的公式（见其类注释），任意层可用。
        /// </summary>
        public Vector3 CellToWorld(int x, int z, int layer)
        {
            return Geometry.CellCorner(x, z, layer);
        }

        public bool WorldToCell(Vector3 worldPosition, int layer, out int x, out int z)
        {
            return Geometry.WorldToCell(worldPosition, layer, out x, out z);
        }

        public bool TryGetHoveredCell(out int x, out int z, out int layer)
        {
            x = 0;
            z = 0;
            layer = 0;

            // 指针位置统一问 PointerInput：PC 是光标，手机是最近点过的那一点
            // （触屏没有悬停，"指着哪"只能靠点出来，见 PointerInput 类注释）。
            Camera camera = Camera.main;
            Vector2 screenPoint;
            if (camera == null || !PointerInput.TryGetHoverPosition(out screenPoint))
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPoint);
            GridGeometry geometry = Geometry;

            // 从最高层往低层找第一个「该格在该层有地形」的层：高台优先被指到（GRID_INTEGRATION §3）。
            // 第 0 层不要求有地形——虚空格照样返回，是否可建交给摆放校验与高亮反馈（骨架版行为不变）。
            if (_terrain != null)
            {
                for (int probe = _terrain.LayerCount - 1; probe >= 1; probe--)
                {
                    int hx, hz;
                    if (geometry.RaycastCell(ray, probe, out hx, out hz) && _terrain.IsPainted(hx, hz, probe))
                    {
                        x = hx;
                        z = hz;
                        layer = probe;
                        return true;
                    }
                }
            }

            return geometry.RaycastCell(ray, 0, out x, out z);
        }
    }
}
