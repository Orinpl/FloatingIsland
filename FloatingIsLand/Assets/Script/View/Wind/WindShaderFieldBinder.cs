using FloatingIsLand.App;
using FloatingIsLand.Domain.Wind;
using FloatingIsLand.View.Environment;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 玩法风场 → 帆布 shader 的桥：把 <see cref="WindField"/> 每格的合成方向/强度烘成
    /// Texture3D，喂给 <see cref="GlobalWindFieldController"/>（RGB=方向、A=强度，跟它的
    /// 程序化噪声贴图同一套编码），帆布就按所在格的真实风向风力飘、没风的格子静止。
    ///
    /// 订阅 <see cref="GameSession.WindFieldChanged"/> 全量重烘——风场只在放风帆/物流点后
    /// 重算，贴图分辨率 = 网格尺寸（几十×几层×几十），重烘成本可忽略，不是每帧的事。
    ///
    /// FI_SailWind 按物体 pivot 采样一次（FI_SampleWind），贴图 texel 中心与格心对齐，
    /// 所以「一栋建筑读到的风」正好就是它所在格的合成风；trilinear 让格间自然渐变。
    /// 本组件由 MapBootstrap 运行时创建，无需进场景资产（同 WindFieldView）。
    /// </summary>
    public sealed class WindShaderFieldBinder : MonoBehaviour
    {
        [Tooltip("全场主导风向的混合权重。贴图已是真实风场，只留一点点主方向做趋同，0 = 完全按格子局部风向")]
        [SerializeField, Range(0f, 1f)] private float mainDirectionWeight = 0.25f;

        [Tooltip("整体强度倍率（乘在每格强度上，调帆布摆动幅度的总开关）")]
        [SerializeField, Min(0f)] private float globalStrength = 1f;

        private GameSession _session;
        private GridGeometry _geometry;
        private int _layerCount;
        private bool _bound;

        private GlobalWindFieldController _controller;
        private Texture3D _bakedTexture;

        /// <summary>绑定本局会话与网格几何。由 MapBootstrap 在建造链路就绪后调用。</summary>
        public void Bind(GameSession session, GridGeometry geometry, int layerCount)
        {
            Unbind();
            _session = session;
            _geometry = geometry;
            _layerCount = Mathf.Max(1, layerCount);
            _bound = session != null && geometry.IsValid;
            if (!_bound)
            {
                return;
            }

            _session.WindFieldChanged += OnWindFieldChanged;
            Rebake();
        }

        private void Unbind()
        {
            if (_session != null)
            {
                _session.WindFieldChanged -= OnWindFieldChanged;
            }
            _session = null;
            _bound = false;
        }

        private void OnDestroy()
        {
            Unbind();
            // 控制器可能是 DontDestroyOnLoad 的常驻实例，把即将销毁的贴图从它身上摘下来，
            // 免得它跨场景后还举着一张已销毁的贴图（会退化成默认噪声风，方向对不上）
            if (_controller != null && _controller.WindFieldTexture == _bakedTexture)
            {
                _controller.WindFieldTexture = null;
            }
            if (_bakedTexture != null)
            {
                Destroy(_bakedTexture);
            }
        }

        private void OnWindFieldChanged()
        {
            Rebake();
        }

        /// <summary>按当前风场重烘贴图并配置控制器。</summary>
        private void Rebake()
        {
            if (!_bound || _session.Wind == null)
            {
                return;
            }

            WindField field = _session.Wind.Field;
            int maxLevel = _session.Rules != null ? _session.Rules.MaxWindLevel : 5;

            Texture3D texture = BakeTexture(field, maxLevel, out Vector3 dominantDir);
            GlobalWindFieldController controller = EnsureController();

            // 风场包围盒钉在网格世界包围盒上：texel 中心 (x+0.5)/w 正好落在格心 (x+0.5)*cellSize
            Vector3 origin = _geometry.LayerOrigin(0);
            var size = new Vector3(
                _geometry.Width * _geometry.CellSize,
                Mathf.Max(_layerCount * _geometry.LayerHeight, 1f),
                _geometry.Length * _geometry.CellSize);

            controller.FieldOrigin = origin;
            controller.FieldSize = size;
            controller.FieldScrollSpeed = Vector3.zero; // 玩法风是静态场，不滚动
            controller.GlobalStrength = globalStrength;
            controller.MainDirectionWeight = mainDirectionWeight;
            controller.WindFieldTexture = texture;

            // 控制器的「主风向 = 自身 X 轴」约定不破坏：把它转到全场强度加权的主导风向
            if (dominantDir.sqrMagnitude > 0.0001f)
            {
                controller.transform.rotation = Quaternion.FromToRotation(Vector3.right, dominantDir.normalized);
            }

            if (_bakedTexture != null)
            {
                Destroy(_bakedTexture);
            }
            _bakedTexture = texture;

            Debug.Log($"[风] 帆布 shader 风场已重烘：贴图 {_geometry.Width}×{_layerCount}×{_geometry.Length}，" +
                      $"风股 {field.Streams.Count}，主导风向 {(dominantDir.sqrMagnitude > 0.0001f ? dominantDir.normalized.ToString("F2") : "无风")}，" +
                      $"场域 origin={origin:F1} size={size:F1}，控制器 {controller.gameObject.name}", this);
        }

        /// <summary>
        /// 每格一 texel：RGB = 合成方向单位向量 *0.5+0.5，A = 合成强度/封顶。
        /// 无风格写 (0.5,0.5,0.5,0)——方向零向量交给 shader 的 SafeNormalize 兜底，
        /// A=0 保证帆布不动；有风↔无风的交界处 trilinear 会把幅度自然淡出。
        /// </summary>
        private Texture3D BakeTexture(WindField field, int maxLevel, out Vector3 dominantDir)
        {
            int width = _geometry.Width;
            int length = _geometry.Length;
            int layers = _layerCount;

            var texture = new Texture3D(width, layers, length, TextureFormat.RGBA32, false)
            {
                name = "Gameplay Wind Field (烘焙)",
                wrapMode = TextureWrapMode.Clamp, // 边缘 texel 不与对侧混（shader 的 frac 已保证采样在场内）
                filterMode = FilterMode.Trilinear,
            };

            var colors = new Color[width * layers * length];
            var idle = new Color(0.5f, 0.5f, 0.5f, 0f);
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = idle;
            }

            dominantDir = Vector3.zero;
            float invMax = 1f / Mathf.Max(1, maxLevel);

            // Texture3D 内存布局：x 最快，其次 y（此处=层），最后 z——与 uvw 的 (x, layer, z) 对应
            for (int layer = 0; layer < layers; layer++)
            {
                for (int z = 0; z < length; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (!field.TryGetCell(x, z, layer, out WindCellState cell) || cell.ResultForce <= 0)
                        {
                            continue;
                        }

                        int d = (int)cell.ResultDir;
                        // StepX/StepZ 归一化后就是含斜向 ±√2/2 的精确单位向量（域坐标 x 东 z 北 = 世界 XZ）
                        var dir = new Vector3(WindMath.StepX[d], 0f, WindMath.StepZ[d]).normalized;
                        float strength = Mathf.Clamp01(cell.ResultForce * invMax);

                        colors[x + width * (layer + layers * z)] = new Color(
                            dir.x * 0.5f + 0.5f,
                            dir.y * 0.5f + 0.5f,
                            dir.z * 0.5f + 0.5f,
                            strength);
                        dominantDir += dir * cell.ResultForce;
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>
        /// 优先复用已有实例（正常路径是控制器自己的 RuntimeInitializeOnLoadMethod 兜底建的那个），
        /// 找不到才现建——建在本组件下，跟着本局生命周期走。
        /// </summary>
        private GlobalWindFieldController EnsureController()
        {
            if (_controller == null)
            {
                _controller = FindObjectOfType<GlobalWindFieldController>();
            }
            if (_controller == null)
            {
                var go = new GameObject("Global Wind Field");
                go.transform.SetParent(transform, false);
                _controller = go.AddComponent<GlobalWindFieldController>();
            }
            return _controller;
        }
    }
}
