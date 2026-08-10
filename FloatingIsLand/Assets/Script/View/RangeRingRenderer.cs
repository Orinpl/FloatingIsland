using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 建筑作用范围圆环：贴地一张四边形，真正的形状由 `FloatingIsLand/RangeRing` 在 fragment 里
    /// 用一组圆角矩形 SDF 算出来（形状来自 <see cref="RangeRingGeometry"/>，与领域层判定逐点相等）。
    ///
    /// 半径来自配表 Building.radius（格），乘 cellSize 换成米。范围是**自占地边缘起算**的，
    /// 所以四边形要罩住「占地外扩 R」这一整块，不是以中心画个正方形。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RangeRingRenderer : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/RangeRing";

        /// <summary>抬离地面的高度。地形 overlay 在 0.02、落点格在 0.08，环夹在中间。</summary>
        private const float SurfaceOffset = 0.05f;

        /// <summary>四边形比实际范围再放一圈，给 shader 的外沿羽化留地方。</summary>
        private const float QuadPadding = 0.5f;

        private static readonly int BoxesId = Shader.PropertyToID("_Boxes");
        private static readonly int BoxCountId = Shader.PropertyToID("_BoxCount");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [Tooltip("环的颜色（乳白）。alpha 是整体强度的总闸门")]
        [SerializeField] private Color ringColor = new Color(0.95f, 0.96f, 1f, 1f);

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        private readonly List<RangeRingGeometry.CellBox> _boxes = new List<RangeRingGeometry.CellBox>(8);

        /// <summary>定长数组：Unity 的 SetVectorArray 首次设定长度后就锁死，每次都得填满。</summary>
        private readonly Vector4[] _worldBoxes = new Vector4[RangeRingGeometry.MaxBoxes];

        private readonly Vector3[] _vertices = new Vector3[4];
        private static readonly int[] Triangles = { 0, 1, 2, 0, 2, 3 };

        private void Awake()
        {
            EnsureResources();
            SetVisible(false);
        }

        /// <summary>
        /// 画出这块占地的作用范围。
        /// </summary>
        /// <param name="cells">占用格（已含旋转与锚点平移）。</param>
        /// <param name="layer">高度层。</param>
        /// <param name="radiusCells">作用半径（格），来自配表 Building.radius。</param>
        /// <param name="geometry">格↔世界换算。</param>
        public void Show(IReadOnlyList<CellCoord> cells, int layer, int radiusCells, GridGeometry geometry)
        {
            EnsureResources();

            if (cells == null || cells.Count == 0 || radiusCells <= 0 || !geometry.IsValid)
            {
                Hide();
                return;
            }

            RangeRingGeometry.BuildBoxes(cells, _boxes);
            if (_boxes.Count == 0)
            {
                Hide();
                return;
            }

            float radiusWorld = radiusCells * geometry.CellSize;

            // 罩住「所有矩形并集再外扩 R」的最小四边形
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            for (int i = 0; i < _boxes.Count; i++)
            {
                Vector4 box = RangeRingGeometry.ToWorldBox(_boxes[i], geometry, layer);
                _worldBoxes[i] = box;
                minX = Mathf.Min(minX, box.x - box.z);
                maxX = Mathf.Max(maxX, box.x + box.z);
                minZ = Mathf.Min(minZ, box.y - box.w);
                maxZ = Mathf.Max(maxZ, box.y + box.w);
            }
            // 数组定长，剩下的槽位填一个退化到极远的盒子，免得 shader 读到上一次的残留
            for (int i = _boxes.Count; i < _worldBoxes.Length; i++)
            {
                _worldBoxes[i] = new Vector4(1e9f, 1e9f, 0f, 0f);
            }

            float pad = radiusWorld + QuadPadding;
            float y = geometry.LayerOrigin(layer).y + SurfaceOffset;
            _vertices[0] = new Vector3(minX - pad, y, minZ - pad);
            _vertices[1] = new Vector3(minX - pad, y, maxZ + pad);
            _vertices[2] = new Vector3(maxX + pad, y, maxZ + pad);
            _vertices[3] = new Vector3(maxX + pad, y, minZ - pad);

            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.triangles = Triangles;
            _mesh.RecalculateBounds();

            _renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(BoxesId, _worldBoxes);
            _block.SetInt(BoxCountId, _boxes.Count);
            _block.SetFloat(RadiusId, radiusWorld);
            _block.SetColor(ColorId, ringColor);
            _renderer.SetPropertyBlock(_block);

            SetVisible(true);
        }

        /// <summary>收起圆环。</summary>
        public void Hide()
        {
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
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            if (_renderer.sharedMaterial == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                {
                    shader = Shader.Find("FloatingIsLand/RangeRing");
                }
                if (shader == null)
                {
                    Debug.LogError($"[表现] 找不到范围环 shader（Resources/{ShaderResourcePath}.shader），作用范围不会显示。");
                    enabled = false;
                    return;
                }
                _renderer.sharedMaterial = new Material(shader)
                {
                    name = "RangeRing (运行时生成)",
                    hideFlags = HideFlags.DontSave,
                };
            }
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "RangeRing", hideFlags = HideFlags.DontSave };
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
