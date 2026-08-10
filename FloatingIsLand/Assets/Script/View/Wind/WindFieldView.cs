using System.Collections.Generic;
using FloatingIsLand.App;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 风路流线渲染：每股风一条 LineRenderer，顶点取「起点 → 各拐点 → 终点」的格心
    /// （直线段不插点；物流风起始处额外补一个顶点，让分段变色的界线落在正确的格子上）。
    ///
    /// 订阅 <see cref="GameSession.WindFieldChanged"/> 全量重建——风场是不可变对象，
    /// 变了就是整个换，正好整批重画；股数 ≤ 风源数，重建成本可忽略。
    ///
    /// 另管两层：摆放预览（半透明画「假设摆了这栋」的新风场，主线暂隐）与
    /// 物流点互联脉冲（沿两点间的风路径闪一条亮线）。
    /// 本组件由 MapBootstrap 运行时创建，无需进场景资产。
    /// </summary>
    public sealed class WindFieldView : MonoBehaviour
    {
        [Tooltip("流线悬浮高度（乘以格子尺寸）")]
        [SerializeField] private float heightFactor = 0.45f;

        [Tooltip("流线宽度（乘以格子尺寸）")]
        [SerializeField] private float widthFactor = 0.09f;

        [Tooltip("风力 1~5 级的流线颜色（下标 0 = 1 级）")]
        [SerializeField] private Color[] forceColors =
        {
            new Color(0.78f, 0.95f, 1.00f, 0.85f), // 1 微风
            new Color(0.55f, 0.86f, 1.00f, 0.85f), // 2 较弱风
            new Color(0.34f, 0.72f, 1.00f, 0.88f), // 3 中等风
            new Color(0.45f, 0.52f, 1.00f, 0.90f), // 4 强风
            new Color(0.68f, 0.40f, 1.00f, 0.92f), // 5 极强风
        };

        [Tooltip("物流风段的颜色（§10.4，物流点之后的风路）")]
        [SerializeField] private Color logisticsColor = new Color(0.30f, 1.00f, 0.72f, 0.92f);

        [Tooltip("摆放预览流线的整体透明度")]
        [SerializeField] private float previewAlpha = 0.55f;

        /// <summary>互联脉冲的持续秒数。</summary>
        private const float LinkPulseSeconds = 2.2f;

        private GameSession _session;
        private GridGeometry _geometry;
        private bool _bound;

        private Material _lineMaterial;
        private Transform _streamsRoot;
        private Transform _previewRoot;
        private Transform _effectsRoot;
        private Transform _markersRoot;

        /// <summary>流线池：主层与预览层各一份，按下标复用，重建时多余的隐藏。</summary>
        private readonly List<LineRenderer> _streamLines = new List<LineRenderer>(8);
        private readonly List<LineRenderer> _previewLines = new List<LineRenderer>(8);

        /// <summary>风源格贴地标记圈。windSource 元素没有模型却挡建造，没有这个标记它就是隐形死格。</summary>
        private readonly List<LineRenderer> _originMarkers = new List<LineRenderer>(4);

        /// <summary>互联脉冲（数量稀少，直接建/销毁）。</summary>
        private sealed class LinkPulse
        {
            public LineRenderer Line;
            public float StartTime;
            public float BaseWidth;
            public Color BaseColor;
        }

        private readonly List<LinkPulse> _pulses = new List<LinkPulse>(4);

        /// <summary>复用的顶点缓冲。</summary>
        private readonly List<Vector3> _vertexBuffer = new List<Vector3>(32);

        private bool _previewActive;

        /// <summary>绑定本局会话与网格几何。由 MapBootstrap 在建造链路就绪后调用。</summary>
        public void Bind(GameSession session, GridGeometry geometry)
        {
            Unbind();
            _session = session;
            _geometry = geometry;
            _bound = session != null && geometry.IsValid;
            if (!_bound)
            {
                return;
            }

            _session.WindFieldChanged += OnWindFieldChanged;
            _session.WindAwardsGranted += OnWindAwardsGranted;
            RebuildStreams();
        }

        private void Unbind()
        {
            if (_session != null)
            {
                _session.WindFieldChanged -= OnWindFieldChanged;
                _session.WindAwardsGranted -= OnWindAwardsGranted;
            }
            _session = null;
            _bound = false;
        }

        private void OnDestroy()
        {
            Unbind();
            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
            }
        }

        private void OnWindFieldChanged()
        {
            RebuildStreams();
        }

        private void OnWindAwardsGranted(IReadOnlyList<WindAward> awards)
        {
            for (int i = 0; i < awards.Count; i++)
            {
                WindAward award = awards[i];
                if (award.Kind == WindAwardKind.LogisticsLink && award.LinkPathCells.Count >= 2)
                {
                    SpawnLinkPulse(award);
                }
            }
        }

        // ---------- 主层流线 ----------

        /// <summary>按当前风场全量重建主层流线。</summary>
        public void RebuildStreams()
        {
            if (!_bound || _session.Wind == null)
            {
                return;
            }

            WindField field = _session.Wind.Field;
            EnsureRoots();

            int used = 0;
            int markers = 0;
            for (int i = 0; i < field.Streams.Count; i++)
            {
                WindStream stream = field.Streams[i];
                if (stream.Force <= 0 || stream.Path.Count == 0)
                {
                    continue;
                }
                LineRenderer line = TakeLine(_streamLines, _streamsRoot, used++);
                DrawStream(line, stream, 1f);
                DrawOriginMarker(TakeLine(_originMarkers, _markersRoot, markers++), stream);
            }
            HideExtra(_streamLines, used);
            HideExtra(_originMarkers, markers);
        }

        /// <summary>
        /// 风源格贴地小圈：windSource 元素占格挡建造但没有任何模型（表现归风系统管），
        /// 没有地面标记时玩家会看到「空格子却压到看不见的元素」。挂在独立根下，预览期间不隐藏——
        /// 风源位置不随预览变，被挡的格子任何时候都该有解释。
        /// </summary>
        private void DrawOriginMarker(LineRenderer line, WindStream stream)
        {
            const int segments = 20;
            float radius = _geometry.CellSize * 0.34f;
            float y = _geometry.CellSize * 0.06f;
            Vector3 center = _geometry.CellCenter(stream.Origin.X, stream.Origin.Z, stream.Layer) + Vector3.up * y;

            line.gameObject.SetActive(true);
            line.loop = true;
            line.alignment = LineAlignment.TransformZ; // 贴地平铺，不面向相机
            line.transform.SetPositionAndRotation(center, Quaternion.Euler(90f, 0f, 0f));
            line.useWorldSpace = false;
            line.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }

            float width = _geometry.CellSize * widthFactor * 0.7f;
            line.startWidth = width;
            line.endWidth = width;
            Color color = ForceColor(stream.Force);
            color.a = 0.9f;
            line.colorGradient = BuildGradient(color, color, -1f);
        }

        /// <summary>
        /// 摆放预览：半透明画出「假设摆了这栋」的整个新风场，同时暂隐主线——
        /// 两套线叠着画会糊成一团，预览期间玩家关心的是"摆下去会变成什么样"。
        /// </summary>
        public void ShowPreview(WindField previewField)
        {
            if (!_bound || previewField == null)
            {
                ClearPreview();
                return;
            }

            EnsureRoots();
            _previewActive = true;
            _streamsRoot.gameObject.SetActive(false);
            _previewRoot.gameObject.SetActive(true);

            int used = 0;
            for (int i = 0; i < previewField.Streams.Count; i++)
            {
                WindStream stream = previewField.Streams[i];
                if (stream.Force <= 0 || stream.Path.Count == 0)
                {
                    continue;
                }
                LineRenderer line = TakeLine(_previewLines, _previewRoot, used++);
                DrawStream(line, stream, previewAlpha);
            }
            HideExtra(_previewLines, used);
        }

        /// <summary>清预览，恢复主层流线。</summary>
        public void ClearPreview()
        {
            if (!_previewActive)
            {
                return;
            }
            _previewActive = false;
            if (_previewRoot != null)
            {
                _previewRoot.gameObject.SetActive(false);
            }
            if (_streamsRoot != null)
            {
                _streamsRoot.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// 把一股风画到一条 LineRenderer 上：顶点 = 起点 + 方向变化处 + 物流起始处 + 终点，
        /// 颜色沿线长渐变——物流起始点之前用风力色、之后用物流色（两个近距离色键做硬过渡）。
        /// </summary>
        private void DrawStream(LineRenderer line, WindStream stream, float alphaScale)
        {
            _vertexBuffer.Clear();
            float y = _geometry.CellSize * heightFactor;
            // 同格重叠的多股线用股号错开一点高度，避免 z-fighting 闪烁
            y += (stream.Id % 4) * _geometry.CellSize * 0.02f;

            float logisticsFraction = -1f;
            float totalLength = 0f;
            Vector3 previous = default;

            IReadOnlyList<WindPathStep> path = stream.Path;
            for (int i = 0; i < path.Count; i++)
            {
                bool isVertex = i == 0
                                || i == path.Count - 1
                                || path[i].DirOut != path[i - 1].DirOut
                                || i == stream.LogisticsFromIndex;
                if (!isVertex)
                {
                    continue;
                }

                Vector3 pos = _geometry.CellCenter(path[i].Cell.X, path[i].Cell.Z, stream.Layer) + Vector3.up * y;
                if (_vertexBuffer.Count > 0)
                {
                    totalLength += Vector3.Distance(previous, pos);
                }
                if (i == stream.LogisticsFromIndex)
                {
                    logisticsFraction = totalLength; // 先存长度，最后归一化
                }
                _vertexBuffer.Add(pos);
                previous = pos;
            }

            line.gameObject.SetActive(true);
            line.positionCount = _vertexBuffer.Count;
            for (int i = 0; i < _vertexBuffer.Count; i++)
            {
                line.SetPosition(i, _vertexBuffer[i]);
            }

            float width = _geometry.CellSize * widthFactor;
            line.startWidth = width;
            line.endWidth = width * 0.55f; // 尾端收细，顺带表达方向

            Color baseColor = ForceColor(stream.Force);
            baseColor.a *= alphaScale;
            Color logColor = logisticsColor;
            logColor.a *= alphaScale;
            line.colorGradient = BuildGradient(
                baseColor, logColor,
                stream.LogisticsFromIndex >= 0 && totalLength > 0f ? logisticsFraction / totalLength : -1f);
        }

        private Color ForceColor(int force)
        {
            if (forceColors == null || forceColors.Length == 0)
            {
                return Color.white;
            }
            int index = Mathf.Clamp(force - 1, 0, forceColors.Length - 1);
            return forceColors[index];
        }

        private static Gradient BuildGradient(Color baseColor, Color logisticsColor, float logisticsFraction)
        {
            var gradient = new Gradient();
            if (logisticsFraction < 0f || logisticsFraction >= 1f)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(baseColor, 0f), new GradientColorKey(baseColor, 1f) },
                    new[] { new GradientAlphaKey(baseColor.a, 0f), new GradientAlphaKey(baseColor.a, 1f) });
                return gradient;
            }

            float f = Mathf.Clamp(logisticsFraction, 0.001f, 0.998f);
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(baseColor, 0f),
                    new GradientColorKey(baseColor, f),
                    new GradientColorKey(logisticsColor, Mathf.Min(f + 0.001f, 1f)),
                    new GradientColorKey(logisticsColor, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(baseColor.a, 0f),
                    new GradientAlphaKey(baseColor.a, f),
                    new GradientAlphaKey(logisticsColor.a, Mathf.Min(f + 0.001f, 1f)),
                    new GradientAlphaKey(logisticsColor.a, 1f),
                });
            return gradient;
        }

        // ---------- 互联脉冲 ----------

        /// <summary>沿两个物流点之间的风路径闪一条亮线，随时间收窄淡出后自毁。</summary>
        private void SpawnLinkPulse(WindAward award)
        {
            EnsureRoots();
            var go = new GameObject("LinkPulse");
            go.transform.SetParent(_effectsRoot, false);
            var line = go.AddComponent<LineRenderer>();
            SetupLine(line);

            float y = _geometry.CellSize * (heightFactor + 0.10f);
            IReadOnlyList<CellCoord> cells = award.LinkPathCells;
            line.positionCount = cells.Count;
            for (int i = 0; i < cells.Count; i++)
            {
                line.SetPosition(i, _geometry.CellCenter(cells[i].X, cells[i].Z, award.Layer) + Vector3.up * y);
            }

            Color color = logisticsColor;
            color.a = 1f;
            float width = _geometry.CellSize * widthFactor * 1.8f;
            line.startWidth = width;
            line.endWidth = width;
            line.colorGradient = BuildGradient(color, color, -1f);

            _pulses.Add(new LinkPulse { Line = line, StartTime = Time.time, BaseWidth = width, BaseColor = color });
        }

        private void Update()
        {
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                LinkPulse pulse = _pulses[i];
                float t = (Time.time - pulse.StartTime) / LinkPulseSeconds;
                if (t >= 1f || pulse.Line == null)
                {
                    if (pulse.Line != null)
                    {
                        Destroy(pulse.Line.gameObject);
                    }
                    _pulses.RemoveAt(i);
                    continue;
                }

                // 呼吸式宽度 + 线性淡出
                float breathe = 1f + 0.45f * Mathf.Sin(t * Mathf.PI * 6f);
                float width = pulse.BaseWidth * breathe;
                pulse.Line.startWidth = width;
                pulse.Line.endWidth = width;

                Color color = pulse.BaseColor;
                color.a = Mathf.Lerp(1f, 0f, t);
                // startColor/endColor 原地改内置双键渐变，零托管分配——每帧 new Gradient 会持续产 GC
                pulse.Line.startColor = color;
                pulse.Line.endColor = color;
            }
        }

        // ---------- 池与资源 ----------

        private void EnsureRoots()
        {
            if (_streamsRoot == null)
            {
                _streamsRoot = CreateRoot("Streams");
                _previewRoot = CreateRoot("Preview");
                _effectsRoot = CreateRoot("Effects");
                _markersRoot = CreateRoot("OriginMarkers");
                _previewRoot.gameObject.SetActive(false);
            }
        }

        private Transform CreateRoot(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private LineRenderer TakeLine(List<LineRenderer> pool, Transform root, int index)
        {
            while (pool.Count <= index)
            {
                var go = new GameObject($"WindStream_{pool.Count}");
                go.transform.SetParent(root, false);
                var line = go.AddComponent<LineRenderer>();
                SetupLine(line);
                pool.Add(line);
            }
            return pool[index];
        }

        private static void HideExtra(List<LineRenderer> pool, int used)
        {
            for (int i = used; i < pool.Count; i++)
            {
                pool[i].gameObject.SetActive(false);
            }
        }

        private void SetupLine(LineRenderer line)
        {
            // sharedMaterial：走 .material 会给每条线实例化一份材质副本，纯浪费
            line.sharedMaterial = EnsureMaterial();
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.useWorldSpace = true;
        }

        /// <summary>
        /// 内置管线 + 顶点色 + 半透明：Sprites/Default 正好（TerrainOverlayRenderer 同款占位方案），
        /// LineRenderer 的 colorGradient 走顶点色，不需要专门 shader。
        /// </summary>
        private Material EnsureMaterial()
        {
            if (_lineMaterial == null)
            {
                _lineMaterial = new Material(Shader.Find("Sprites/Default"))
                {
                    name = "WindStream (运行时生成)",
                    hideFlags = HideFlags.DontSave,
                };
            }
            return _lineMaterial;
        }
    }
}
