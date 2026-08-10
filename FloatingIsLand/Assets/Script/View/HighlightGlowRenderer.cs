using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 给「这次摆放里给你加/扣分的那些对象」叠一层辉光。
    ///
    /// 用 <see cref="Graphics.DrawMesh"/> 每帧重画对方的网格，**一个字节都不写进对方的组件**：
    /// 不改材质、不加子物体、不动 Renderer。不调用就什么都没有，清理成本为零。
    /// 相比之下，往 <c>renderer.materials</c> 追加一个槽位只能盖到最后一个 submesh，
    /// 而复制一份 Renderer 会往层级里塞一堆临时节点、还要自己管销毁。
    /// </summary>
    public sealed class HighlightGlowRenderer : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/HighlightGlow";

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        /// <summary>一次要重画的网格。矩阵在加入时取一次——建筑落地后不会再动。</summary>
        private readonly struct GlowDraw
        {
            public readonly Mesh Mesh;
            public readonly Matrix4x4 Matrix;
            public readonly int SubMesh;
            public readonly int Layer;
            public readonly int StyleIndex;

            public GlowDraw(Mesh mesh, Matrix4x4 matrix, int subMesh, int layer, int styleIndex)
            {
                Mesh = mesh;
                Matrix = matrix;
                SubMesh = subMesh;
                Layer = layer;
                StyleIndex = styleIndex;
            }
        }

        private readonly List<GlowDraw> _draws = new List<GlowDraw>(64);
        private MaterialPropertyBlock[] _styles;
        private Material _material;
        private Camera _camera;

        /// <summary>辉光配色。索引与 <see cref="GlowStyle"/> 一一对应。</summary>
        public enum GlowStyle
        {
            /// <summary>这栋给你加了分。</summary>
            Bonus = 0,

            /// <summary>这栋让你扣了分。</summary>
            Penalty = 1,

            /// <summary>在范围内但没算上（超上限 / 不叠加 / 收益归零）。</summary>
            Ignored = 2,
        }

        // 刻意不用绿/红：那套颜色在落点格上已经表示「能不能建」，混用玩家会读成"这里不能建"
        private static readonly Color[] StyleColors =
        {
            new Color(1.00f, 0.84f, 0.36f, 1f), // 加分：暖金
            new Color(0.78f, 0.42f, 1.00f, 1f), // 扣分：冷紫
            new Color(0.75f, 0.78f, 0.85f, 1f), // 未计入：灰白
        };

        // 「弱」的量化：加分最亮也只到 0.8，未计入的压到三分之一
        private static readonly float[] StyleIntensities = { 0.80f, 0.70f, 0.28f };

        /// <summary>开始收集这一帧要发光的对象。</summary>
        public void Begin()
        {
            _draws.Clear();
        }

        /// <summary>把一个世界实例的所有网格加进发光列表。</summary>
        public void Add(WorldInstanceIndex.Entry entry, GlowStyle style)
        {
            if (entry == null || entry.Root == null)
            {
                return;
            }

            MeshFilter[] meshes = entry.Meshes;
            for (int i = 0; i < meshes.Length; i++)
            {
                MeshFilter filter = meshes[i];
                if (filter == null || filter.sharedMesh == null)
                {
                    continue;
                }

                Mesh mesh = filter.sharedMesh;
                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    _draws.Add(new GlowDraw(mesh, matrix, sub, filter.gameObject.layer, (int)style));
                }
            }
        }

        /// <summary>清空，什么都不画。</summary>
        public void Clear()
        {
            _draws.Clear();
        }

        /// <summary>当前有多少个网格在发光（测试与调试用）。</summary>
        public int DrawCount
        {
            get { return _draws.Count; }
        }

        private void LateUpdate()
        {
            if (_draws.Count == 0)
            {
                return;
            }
            if (!EnsureResources())
            {
                return;
            }

            for (int i = 0; i < _draws.Count; i++)
            {
                GlowDraw draw = _draws[i];
                Graphics.DrawMesh(
                    draw.Mesh, draw.Matrix, _material, draw.Layer,
                    // 显式给相机：传 null 会提交给本帧渲染的**每一台**相机，
                    // 包括略缩图那台隐藏相机，手牌小图上会平白多一层光
                    _camera,
                    draw.SubMesh,
                    _styles[draw.StyleIndex]);
            }
        }

        private bool EnsureResources()
        {
            if (_material == null)
            {
                Shader shader = Resources.Load<Shader>(ShaderResourcePath);
                if (shader == null)
                {
                    shader = Shader.Find("FloatingIsLand/HighlightGlow");
                }
                if (shader == null)
                {
                    Debug.LogError($"[表现] 找不到辉光 shader（Resources/{ShaderResourcePath}.shader），受影响建筑不会发光。");
                    enabled = false;
                    return false;
                }
                _material = new Material(shader)
                {
                    name = "HighlightGlow (运行时生成)",
                    hideFlags = HideFlags.DontSave,
                };
            }

            if (_styles == null)
            {
                _styles = new MaterialPropertyBlock[StyleColors.Length];
                for (int i = 0; i < _styles.Length; i++)
                {
                    var block = new MaterialPropertyBlock();
                    block.SetColor(ColorId, StyleColors[i]);
                    block.SetFloat(IntensityId, StyleIntensities[i]);
                    _styles[i] = block;
                }
            }

            if (_camera == null)
            {
                _camera = Camera.main;
            }
            return true;
        }

        private void OnDisable()
        {
            _draws.Clear();
        }

        private void OnDestroy()
        {
            if (_material == null)
            {
                return;
            }
            if (Application.isPlaying)
            {
                Destroy(_material);
            }
            else
            {
                DestroyImmediate(_material);
            }
        }
    }
}
