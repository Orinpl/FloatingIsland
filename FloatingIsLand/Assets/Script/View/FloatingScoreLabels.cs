using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 飘在建筑头顶的加减分数字（"+3" / "-8"）。
    ///
    /// 用 <see cref="TextMesh"/> 而不是世界空间 Canvas：Game.View 没有引用 UnityEngine.UI，
    /// TextMesh 在 UnityEngine 核心里，加它不用动 asmdef——而动 asmdef 会把
    /// Game.Tests.EditMode 的编译面一起牵动，为了几个数字不值当。
    ///
    /// 数字池化复用：光标每动一格就要重排一次，反复 Instantiate/Destroy 会让 GC 一直抖。
    /// </summary>
    public sealed class FloatingScoreLabels : MonoBehaviour
    {
        private const string ShaderResourcePath = "Shaders/WorldLabel";

        /// <summary>字号取大、字符尺寸取小：动态字体是按 fontSize 烘图集的，字号太小放大就糊。</summary>
        private const int FontSize = 64;
        private const float CharacterSize = 0.14f;

        private readonly List<TextMesh> _pool = new List<TextMesh>(32);
        private int _used;

        private Font _font;
        private Material _material;

        /// <summary>开始这一轮布置。</summary>
        public void Begin()
        {
            _used = 0;
        }

        /// <summary>在世界坐标处放一个数字。</summary>
        public void Add(Vector3 worldPosition, string text, Color color)
        {
            if (!EnsureResources())
            {
                return;
            }

            TextMesh label = Take();
            label.text = text;
            label.color = color;
            label.transform.position = worldPosition;
            label.gameObject.SetActive(true);
        }

        /// <summary>收尾：把这一轮没用到的收起来。</summary>
        public void End()
        {
            for (int i = _used; i < _pool.Count; i++)
            {
                _pool[i].gameObject.SetActive(false);
            }
        }

        /// <summary>全部收起。</summary>
        public void Clear()
        {
            Begin();
            End();
        }

        /// <summary>当前显示了几个数字（测试与调试用）。</summary>
        public int VisibleCount
        {
            get { return _used; }
        }

        private TextMesh Take()
        {
            if (_used < _pool.Count)
            {
                return _pool[_used++];
            }

            var go = new GameObject($"ScoreLabel_{_pool.Count}");
            go.transform.SetParent(transform, false);

            TextMesh label = go.AddComponent<TextMesh>();
            label.font = _font;
            label.fontSize = FontSize;
            label.characterSize = CharacterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontStyle = FontStyle.Bold;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _pool.Add(label);
            _used++;
            return label;
        }

        private void LateUpdate()
        {
            if (_used == 0)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return;
            }

            // 面向相机。用相机的 forward 而不是「看向相机」，一排数字才不会各朝各的方向
            Quaternion facing = Quaternion.LookRotation(camera.transform.forward, camera.transform.up);
            for (int i = 0; i < _used; i++)
            {
                _pool[i].transform.rotation = facing;
            }
        }

        private bool EnsureResources()
        {
            if (_font != null && _material != null)
            {
                return true;
            }

            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null)
            {
                Debug.LogError("[表现] 取不到 LegacyRuntime.ttf，飘分数字不会显示。");
                enabled = false;
                return false;
            }

            Shader shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader == null)
            {
                shader = Shader.Find("FloatingIsLand/WorldLabel");
            }
            if (shader == null)
            {
                Debug.LogError($"[表现] 找不到 {ShaderResourcePath} shader，飘分数字不会显示。");
                enabled = false;
                return false;
            }

            _material = new Material(shader)
            {
                name = "WorldLabel (运行时生成)",
                hideFlags = HideFlags.DontSave,
                mainTexture = _font.material != null ? _font.material.mainTexture : null,
            };

            // 动态字体的图集会在字符变多时重建，重建后贴图对象可能整个换掉，
            // 不跟着换材质里的引用，数字就会变成一片乱码或空白
            Font.textureRebuilt += OnFontTextureRebuilt;
            return true;
        }

        private void OnFontTextureRebuilt(Font font)
        {
            if (font != _font || _material == null || _font.material == null)
            {
                return;
            }
            _material.mainTexture = _font.material.mainTexture;
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;

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
