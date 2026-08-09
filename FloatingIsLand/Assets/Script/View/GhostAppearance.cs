using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 摆放预览的虚影外观：**保留原材质的贴图与主色**，换成 `FloatingIsLand/BuildGhost`
    /// （半透明 + 菲涅尔边缘光 + 扫描线），再按合法性整体染绿 / 染红。
    ///
    /// 挂在 ghost 实例上，只在生成时转换一次材质；之后每帧改色走 <see cref="MaterialPropertyBlock"/>，
    /// 零分配。早先的写法是每帧 <c>new Material</c> 再赋给 <c>renderer.materials</c>——
    /// 旧实例既不销毁也不回收，摆放模式开着就一直漏。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GhostAppearance : MonoBehaviour
    {
        /// <summary>虚影 shader 的 Resources 路径。放 Resources 是为了保证进包。</summary>
        private const string ShaderResourcePath = "Shaders/BuildGhost";

        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static Shader _ghostShader;
        private static bool _shaderResolved;

        private Renderer[] _renderers;
        private readonly List<Material> _owned = new List<Material>();
        private MaterialPropertyBlock _block;
        private bool _hasTint;
        private Color _tint;

        /// <summary>
        /// 设置虚影染色（绿=可建 / 红=不可建）。第一次调用时顺带完成材质转换，
        /// 之后每帧调都只是写一次属性块，可以放心每帧调。
        /// </summary>
        public void SetTint(Color tint)
        {
            EnsureConverted();
            if (_renderers == null || (_hasTint && _tint == tint))
            {
                return;
            }

            _hasTint = true;
            _tint = tint;
            _block.SetColor(TintColorId, tint);
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null)
                {
                    _renderers[i].SetPropertyBlock(_block);
                }
            }
        }

        private void EnsureConverted()
        {
            if (_renderers != null)
            {
                return;
            }

            _block = new MaterialPropertyBlock();

            Shader shader = ResolveShader();
            if (shader == null)
            {
                // 虚影 shader 缺失时不动原材质：建筑照常显示为实体，落点格的绿/红仍在，
                // 预览不至于失灵。报一次错就够，别每帧刷屏。
                _renderers = new Renderer[0];
                return;
            }

            _renderers = GetComponentsInChildren<Renderer>(true);

            for (int r = 0; r < _renderers.Length; r++)
            {
                Renderer renderer = _renderers[r];
                if (renderer == null)
                {
                    continue;
                }

                Material[] sources = renderer.sharedMaterials;
                var ghosts = new Material[sources.Length];
                for (int m = 0; m < sources.Length; m++)
                {
                    ghosts[m] = CreateGhostMaterial(sources[m], shader);
                    _owned.Add(ghosts[m]);
                }

                // 用 sharedMaterials 赋值：数组里已经是我们自己 new 出来的实例，
                // 走 materials 会让 Unity 再复制一遍，多出一份没人负责销毁的材质。
                renderer.sharedMaterials = ghosts;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            // 预览不该参与任何物理查询——尤其不能挡住悬停格的射线
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }

        /// <summary>从原材质派生一份虚影材质：贴图与主色照抄，其余走 shader 默认值。</summary>
        private static Material CreateGhostMaterial(Material source, Shader shader)
        {
            var ghost = new Material(shader)
            {
                name = source != null ? source.name + " (Ghost)" : "Ghost",
                hideFlags = HideFlags.DontSave,
            };

            if (source == null)
            {
                return ghost;
            }

            // 贴图槽名两套都试：Built-in 叫 _MainTex，URP 叫 _BaseMap
            Texture texture = null;
            if (source.HasProperty(MainTexId))
            {
                texture = source.GetTexture(MainTexId);
            }
            if (texture == null && source.HasProperty(BaseMapId))
            {
                texture = source.GetTexture(BaseMapId);
            }
            if (texture != null)
            {
                ghost.SetTexture(MainTexId, texture);
            }

            Color color = Color.white;
            if (source.HasProperty(BaseColorId))
            {
                color = source.GetColor(BaseColorId);
            }
            else if (source.HasProperty(ColorId))
            {
                color = source.GetColor(ColorId);
            }
            // 原材质自己的 alpha 不参与：透明度统一归虚影 shader 的 _Alpha 管，
            // 否则不透明材质（alpha=1）和半透明材质摆出来的虚影浓淡会不一致
            color.a = 1f;
            ghost.SetColor(ColorId, color);

            return ghost;
        }

        private static Shader ResolveShader()
        {
            if (_shaderResolved)
            {
                return _ghostShader;
            }
            _shaderResolved = true;

            _ghostShader = Resources.Load<Shader>(ShaderResourcePath);
            if (_ghostShader == null)
            {
                _ghostShader = Shader.Find("FloatingIsLand/BuildGhost");
            }
            if (_ghostShader == null)
            {
                Debug.LogError($"[表现] 找不到虚影 shader（Resources/{ShaderResourcePath}.shader）。" +
                               "摆放预览会退化成实体模型，落点格的绿/红标记不受影响。");
            }
            return _ghostShader;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                Material material = _owned[i];
                if (material == null)
                {
                    continue;
                }
                if (Application.isPlaying)
                {
                    Destroy(material);
                }
                else
                {
                    DestroyImmediate(material);
                }
            }
            _owned.Clear();
        }
    }
}
