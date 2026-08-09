using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 摆放预览的**落点格标记**：把建筑将要占的每一格画成一张贴地方片，绿=可建、红=挡住了。
    ///
    /// 格子来自 <see cref="Domain.Map.Footprint.GetCells"/>，天然带旋转——
    /// 转 90° 时占地矩形跟着转，不是只有模型在转。异形占地（L 形、凹形）也是逐格画，
    /// 不会退化成外接矩形。
    ///
    /// 三档配色而不是简单的绿/红两档：整体摆不下时，**具体是哪几格出的问题**要能一眼看出来，
    /// 否则 6×6 的船坞压到一格虚空，玩家只看到一片红，根本不知道往哪挪。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlacementFootprintRenderer : MonoBehaviour
    {
        /// <summary>顶点色专用 shader（ZTest Always，保证不被建筑模型挡掉）的 Resources 路径。</summary>
        private const string ShaderResourcePath = "Shaders/CellOverlay";

        /// <summary>抬离地面的高度。要高于地形 overlay 的 0.02，否则两层会 Z-fighting。</summary>
        private const float SurfaceOffset = 0.08f;

        /// <summary>每格四周内缩的比例，缩出来的缝就是格与格的分界。</summary>
        private const float CellInset = 0.06f;

        private Mesh _mesh;
        private MeshRenderer _renderer;

        private readonly List<Vector3> _vertices = new List<Vector3>(144);
        private readonly List<Color> _colors = new List<Color>(144);
        private readonly List<int> _triangles = new List<int>(216);

        /// <summary>当前画出来的格子数。</summary>
        public int CellCount { get; private set; }

        private void Awake()
        {
            EnsureResources();
            SetVisible(false);
        }

        /// <summary>
        /// 画出这一组落点格。
        /// </summary>
        /// <param name="cells">逐格判定结果（<see cref="BuildBoard.CheckCells"/> 的产物，已含旋转）。</param>
        /// <param name="layer">高度层。</param>
        /// <param name="geometry">格↔世界换算。</param>
        /// <param name="placementValid">整体是否可摆（矿脉范围/风带这类整体规则也算在内）。</param>
        /// <param name="validColor">整体可摆时每格的颜色。</param>
        /// <param name="invalidColor">整体不可摆、但本格自身没问题时的颜色。</param>
        /// <param name="blockedColor">本格自身就是障碍时的颜色。</param>
        public void Show(
            IReadOnlyList<CellPlacement> cells, int layer, GridGeometry geometry,
            bool placementValid, Color validColor, Color invalidColor, Color blockedColor)
        {
            EnsureResources();

            int count = cells != null ? cells.Count : 0;
            if (count == 0 || !geometry.IsValid)
            {
                Hide();
                return;
            }

            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();

            float inset = geometry.CellSize * CellInset * 0.5f;
            float span = geometry.CellSize - inset * 2f;

            for (int i = 0; i < count; i++)
            {
                CellPlacement cell = cells[i];
                Vector3 corner = geometry.CellCorner(cell.Cell.X, cell.Cell.Z, layer)
                                 + new Vector3(inset, SurfaceOffset, inset);

                Color color = placementValid
                    ? validColor
                    : (cell.IsValid ? invalidColor : blockedColor);

                int v = _vertices.Count;
                _vertices.Add(corner);
                _vertices.Add(corner + new Vector3(0f, 0f, span));
                _vertices.Add(corner + new Vector3(span, 0f, span));
                _vertices.Add(corner + new Vector3(span, 0f, 0f));
                for (int k = 0; k < 4; k++)
                {
                    _colors.Add(color);
                }

                // 法线朝 +Y（从上往下看），顶点逆时针绕序
                _triangles.Add(v + 0);
                _triangles.Add(v + 1);
                _triangles.Add(v + 2);
                _triangles.Add(v + 0);
                _triangles.Add(v + 2);
                _triangles.Add(v + 3);
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();

            CellCount = count;
            SetVisible(true);
        }

        /// <summary>收起标记（退出摆放模式 / 光标不在格子上）。</summary>
        public void Hide()
        {
            CellCount = 0;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
            }
            _renderer.enabled = visible;
        }

        private void EnsureResources()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
            }

            if (_renderer.sharedMaterial == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResourcePath)
                                ?? Shader.Find("FloatingIsLand/CellOverlay")
                                // 兜底：Sprites/Default 也是「无光照 + 乘顶点色」，只是会被建筑模型挡住
                                ?? Shader.Find("Sprites/Default");
                _renderer.sharedMaterial = new Material(shader)
                {
                    name = "PlacementFootprint (运行时生成)",
                    hideFlags = HideFlags.DontSave,
                };
            }
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "PlacementFootprint", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }
        }

        private void OnDestroy()
        {
            if (_mesh == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
        }
    }
}
