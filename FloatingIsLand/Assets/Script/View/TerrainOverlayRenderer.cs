using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 把 <see cref="MapSnapshot"/> 的已刷地块画成**一个合批 Mesh**（每格 4 顶点 2 三角形，顶点色 = 地形色）。
    ///
    /// 「没刷过的格子不显示」就落在这里：未刷的坐标压根不生成顶点，天然不可见——不需要逐格开关，
    /// 也不需要遮罩。EGB 自己的 Object Grid 做不到逐格显隐（它每层只有一块面片、alphaMask 是跟随光标的
    /// 单一遮罩，EasyGridBuilderPro.cs:85-91），所以接线时会把 displayObjectGrid 关掉，网格可视化由本组件全权负责。
    ///
    /// 不用「每格一个 Quad GameObject」（GridCellHighlighter 那种）：那个类只有 2 个 quad 才成立，
    /// 上千格会直接拖垮编辑器和运行时。
    ///
    /// [ExecuteAlways]：地图编辑器（ViewEGB/Editor/MapEditorWindow）直接驱动本组件做落笔预览——
    /// 预览与运行时是同一段代码、同一个 Mesh，从机制上杜绝"编辑器看着对、跑起来不一样"。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class TerrainOverlayRenderer : MonoBehaviour
    {
        [Serializable]
        public sealed class TerrainColor
        {
            [Tooltip("配表 MapElement.elementId（isTerrain=TRUE 的行）")]
            public string elementId;
            [ColorUsage(true)] public Color color = Color.white;
        }

        [Tooltip("地形配色。表里没列到的 elementId 用 fallbackColor 并打一次警告")]
        [SerializeField] private List<TerrainColor> palette = new List<TerrainColor>
        {
            new TerrainColor { elementId = "island",       color = new Color(0.62f, 0.60f, 0.55f, 0.75f) },
            new TerrainColor { elementId = "greenField",   color = new Color(0.42f, 0.72f, 0.33f, 0.75f) },
            new TerrainColor { elementId = "floatingZone", color = new Color(0.36f, 0.72f, 0.90f, 0.55f) },
        };

        [SerializeField] private Color fallbackColor = new Color(1f, 0f, 1f, 0.75f);

        [Tooltip("抬离网格面的高度，防 Z-fighting（与 GridCellHighlighter 同口径）")]
        [SerializeField] private float surfaceOffset = 0.02f;

        [Tooltip("每格四周内缩的比例（0.04 = 缩掉 4%）。缩出来的缝隙就是网格线——EGB 的网格面片已关闭")]
        [Range(0f, 0.3f)]
        [SerializeField] private float cellInset = 0.04f;

        [Tooltip("聚焦模式（建造时跟随光标）下以光标格为中心保持全亮的圈数，再往外线性渐隐到不可见")]
        [Range(1, 12)]
        [SerializeField] private int focusRings = 3;

        [Tooltip("聚焦时高度层差算作几圈平面距离——上下层的格子应该比同层邻居先淡出")]
        [Range(1, 8)]
        [SerializeField] private int layerRingWeight = 2;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private readonly Dictionary<string, Color> _colorById = new Dictionary<string, Color>(StringComparer.Ordinal);
        private readonly HashSet<string> _warnedIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>最近一次 Rebuild 的格子表与它们的原始颜色，聚焦时只改 alpha，不重建顶点。</summary>
        private IReadOnlyList<MapCell> _cells;
        private Color[] _baseColors;
        private Color[] _tintedColors;

        private bool _hasFocus;
        private int _focusX;
        private int _focusZ;
        private int _focusLayer;

        /// <summary>当前已画出的地块数（= 快照的 PaintedCount）。</summary>
        public int RenderedCellCount { get; private set; }

        private void Awake()
        {
            EnsureResources();
        }

        /// <summary>建 Mesh 与材质。编辑器态不走 Awake，所以 Rebuild 前都要确保调过一次。</summary>
        private void EnsureResources()
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
            }

            if (_renderer.sharedMaterial == null || _renderer.sharedMaterial.shader == null)
            {
                // Sprites/Default：无光照、乘顶点色、支持透明，与 GridCellHighlighter 同一套占位材质。
                // 正式版随 View overlay（风箭头/覆盖高亮）统一换专用材质（GRID_INTEGRATION §6 第 5 条）
                _renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "TerrainOverlay (运行时生成)",
                    hideFlags = HideFlags.DontSave, // 编辑器态生成的材质不要被存进场景
                };
            }
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "TerrainOverlay", hideFlags = HideFlags.DontSave };
                _mesh.MarkDynamic();
            }

            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter.sharedMesh != _mesh)
            {
                filter.sharedMesh = _mesh;
            }

            // 调色板只有几条，每次重建最省心——Inspector 里改了颜色能立刻生效，不用管失效标记
            RebuildPalette();
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

        /// <summary>按快照重建整张 overlay。snapshot 为 null 或没刷过任何格子时清空显示。</summary>
        public void Rebuild(MapSnapshot snapshot, GridGeometry geometry)
        {
            EnsureResources();

            _mesh.Clear();
            RenderedCellCount = 0;
            _cells = null;
            _baseColors = null;
            _tintedColors = null;

            if (snapshot == null || snapshot.PaintedCount == 0 || !geometry.IsValid)
            {
                return;
            }

            IReadOnlyList<MapCell> cells = snapshot.Cells;
            int count = cells.Count;

            // 每格 4 顶点：超过 16383 格就爆 16 位索引上限，250×250 满刷有 62500 格
            _mesh.indexFormat = count * 4 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;

            var vertices = new Vector3[count * 4];
            var colors = new Color[count * 4];
            var triangles = new int[count * 6];

            float inset = geometry.CellSize * cellInset * 0.5f;
            float span = geometry.CellSize - inset * 2f;

            for (int i = 0; i < count; i++)
            {
                MapCell cell = cells[i];
                Vector3 corner = geometry.CellCorner(cell.X, cell.Z, cell.Layer)
                                 + new Vector3(inset, surfaceOffset, inset);
                Color color = ResolveColor(cell.ElementId);

                int v = i * 4;
                vertices[v + 0] = corner;
                vertices[v + 1] = corner + new Vector3(0f, 0f, span);
                vertices[v + 2] = corner + new Vector3(span, 0f, span);
                vertices[v + 3] = corner + new Vector3(span, 0f, 0f);
                colors[v + 0] = color;
                colors[v + 1] = color;
                colors[v + 2] = color;
                colors[v + 3] = color;

                // 法线朝 +Y（从上往下看），顶点按逆时针绕序
                int t = i * 6;
                triangles[t + 0] = v + 0;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;
                triangles[t + 3] = v + 0;
                triangles[t + 4] = v + 2;
                triangles[t + 5] = v + 3;
            }

            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();

            RenderedCellCount = count;
            _cells = cells;
            _baseColors = colors;
            _tintedColors = new Color[colors.Length];

            // 重建等于换了一张图，之前的聚焦点没有意义了
            _hasFocus = false;
        }

        /// <summary>整块 overlay 显隐（进出建造模式用）。</summary>
        public void SetVisible(bool visible)
        {
            if (_renderer == null)
            {
                _renderer = GetComponent<MeshRenderer>();
            }
            _renderer.enabled = visible;
        }

        /// <summary>
        /// 聚焦到某个格子：只有它周围 <c>focusRings</c> 圈内的格子保持全亮，往外线性渐隐到透明。
        /// 建造时跟随光标调用；坐标没变时直接返回，不重复写顶点色。
        /// </summary>
        public void SetFocus(int x, int z, int layer)
        {
            if (_hasFocus && _focusX == x && _focusZ == z && _focusLayer == layer)
            {
                return;
            }

            _hasFocus = true;
            _focusX = x;
            _focusZ = z;
            _focusLayer = layer;
            ApplyFocus();
        }

        /// <summary>取消聚焦，整张图按原色显示（编辑器刷子与纯看图模式的默认状态）。</summary>
        public void ClearFocus()
        {
            if (!_hasFocus)
            {
                return;
            }
            _hasFocus = false;
            ApplyFocus();
        }

        /// <summary>按当前聚焦点重写顶点 alpha。顶点数不变，只改颜色数组，比重建 Mesh 便宜得多。</summary>
        private void ApplyFocus()
        {
            if (_mesh == null || _cells == null || _baseColors == null || _tintedColors == null)
            {
                return;
            }

            if (!_hasFocus)
            {
                _mesh.colors = _baseColors;
                return;
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                MapCell cell = _cells[i];
                float factor = FocusFactor(cell);
                int v = i * 4;
                for (int k = 0; k < 4; k++)
                {
                    Color color = _baseColors[v + k];
                    color.a *= factor;
                    _tintedColors[v + k] = color;
                }
            }
            _mesh.colors = _tintedColors;
        }

        /// <summary>
        /// 某格离聚焦点几圈：平面上取切比雪夫距离（正方形一圈一圈往外数），
        /// 层差按 <c>layerRingWeight</c> 折算成圈——上下层的格子应该比同层邻居先淡出。
        /// </summary>
        private float FocusFactor(MapCell cell)
        {
            int ring = Mathf.Max(Mathf.Abs(cell.X - _focusX), Mathf.Abs(cell.Z - _focusZ))
                       + Mathf.Abs(cell.Layer - _focusLayer) * layerRingWeight;
            return FocusVisibility(ring, focusRings);
        }

        /// <summary>
        /// 聚焦渐隐曲线：第 <paramref name="ring"/> 圈的可见度（1 = 原色，0 = 完全透明）。
        ///
        /// 分母取 rings 而分子多给一圈，于是第 1 圈仍是满亮、第 rings+1 圈正好归零——
        /// 边缘是一圈渐隐而不是硬切边。抽成公开静态纯函数，这条曲线才能被单测钉住。
        /// </summary>
        public static float FocusVisibility(int ring, int rings)
        {
            if (rings <= 0)
            {
                return ring <= 0 ? 1f : 0f;
            }
            return Mathf.Clamp01((rings + 1 - ring) / (float)rings);
        }

        private void RebuildPalette()
        {
            _colorById.Clear();
            foreach (TerrainColor entry in palette)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.elementId))
                {
                    _colorById[entry.elementId] = entry.color;
                }
            }
        }

        private Color ResolveColor(string elementId)
        {
            if (_colorById.Count == 0)
            {
                RebuildPalette();
            }

            Color color;
            if (_colorById.TryGetValue(elementId, out color))
            {
                return color;
            }

            if (_warnedIds.Add(elementId))
            {
                Debug.LogWarning($"[地形] TerrainOverlayRenderer 的配色表里没有 '{elementId}'，用兜底色显示。请在 Inspector 的 palette 补一条。", this);
            }
            return fallbackColor;
        }
    }
}
