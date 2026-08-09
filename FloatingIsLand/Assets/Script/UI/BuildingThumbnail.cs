using System;
using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 建筑略缩图：把配表 <c>prefabPath</c> 指向的模型现场渲一张小图，给手牌按钮当预览。
    ///
    /// 为什么是**运行时渲**而不是编辑器预生成 PNG：美术资产还在返工（材质刚在重挂），
    /// 预生成的图会静悄悄地过期——玩家看到的是上一版模型，而且没有任何报错。
    /// 现场渲永远等于当前资产，也不用往仓库里塞十几张二进制图。
    /// 每个变体只渲一次，之后走缓存。
    ///
    /// 渲染台放在世界下方一万米、独占 TransparentFX 层：
    /// 相机与灯光都只认这一层，既照不到正式场景，也不会把正式场景拍进略缩图。
    /// </summary>
    public static class BuildingThumbnail
    {
        /// <summary>略缩图分辨率。宽高比要和 HUD 里那块位置大致一致，否则会拉伸。</summary>
        private const int Width = 256;
        private const int Height = 144;

        /// <summary>渲染台所在层：TransparentFX。本工程没用到它（正式对象在 Default 与 EGB Grid System）。</summary>
        private const int StageLayer = 1;

        /// <summary>渲染台位置。放到世界下方极远处，正式场景不可能有东西闯进取景框。</summary>
        private static readonly Vector3 StageOrigin = new Vector3(0f, -10000f, 0f);

        /// <summary>取景角度：略微俯视的 3/4 视角，比正视更能看出体量与屋顶形状。</summary>
        private static readonly Vector3 ViewAngles = new Vector3(22f, 135f, 0f);

        /// <summary>底色。模型现在多数是浅色白模，用深色底才看得清轮廓。</summary>
        private static readonly Color Background = new Color(0.16f, 0.19f, 0.26f, 1f);

        /// <summary>variantId → 略缩图。值为 null 表示这个变体没有模型，别再重试。</summary>
        private static readonly Dictionary<string, RenderTexture> Cache =
            new Dictionary<string, RenderTexture>(StringComparer.Ordinal);

        private static Transform _stage;
        private static Camera _camera;
        private static Light _light;

        /// <summary>
        /// 取某个变体的略缩图；没有模型（<paramref name="prefabPath"/> 为空或资源缺失）时返回 null。
        /// </summary>
        public static Texture Get(string variantId, string prefabPath)
        {
            if (string.IsNullOrEmpty(variantId))
            {
                return null;
            }

            RenderTexture cached;
            if (Cache.TryGetValue(variantId, out cached))
            {
                if (ReferenceEquals(cached, null))
                {
                    // 已经确认过这个变体没有模型，别每帧重试一次 Resources.Load
                    return null;
                }
                if (cached != null)
                {
                    return cached;
                }
                // 走到这儿说明 RenderTexture 被销毁了（换过场景）——Unity 的假 null。往下重渲一张。
            }

            RenderTexture texture = Render(prefabPath);
            Cache[variantId] = texture;
            return texture;
        }

        private static RenderTexture Render(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return null;
            }

            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                // 缺模型不是错误：美术资产分批到位，白模阶段手牌只显示占地图标就够了。
                // 真正的缺资源警告由 ModelSpawner 在生成实体时统一打，这里不重复刷屏。
                return null;
            }

            EnsureStage();

            GameObject instance = UnityEngine.Object.Instantiate(prefab, _stage);
            instance.transform.SetPositionAndRotation(StageOrigin, Quaternion.identity);
            SetLayerRecursive(instance.transform, StageLayer);

            Bounds bounds;
            if (!TryGetWorldBounds(instance, out bounds))
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return null;
            }

            Frame(bounds);

            var texture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Thumbnail",
                antiAliasing = 4,
                filterMode = FilterMode.Bilinear,
            };

            _camera.targetTexture = texture;
            _camera.Render();
            _camera.targetTexture = null;

            // 必须立刻销毁：Destroy 要等到帧末，同一帧连渲几张手牌时，
            // 上一栋会站在渲染台上一起被拍进下一张图里
            UnityEngine.Object.DestroyImmediate(instance);
            return texture;
        }

        /// <summary>
        /// 取景：把包围盒八个角变换到相机空间，按**实际投影范围**定取景框。
        ///
        /// 不能图省事用对角线半径当取景半径：那是「任意朝向都框得下」的保守值，
        /// 对扁平建筑（6×6 的船坞几乎没有高度）会把模型缩到只占半屏，一排手牌看过去大小忽大忽小。
        /// </summary>
        private static void Frame(Bounds bounds)
        {
            Quaternion view = Quaternion.Euler(ViewAngles);
            Quaternion toCamera = Quaternion.Inverse(view);
            Vector3 extents = bounds.extents;

            float halfWidth = 0f;
            float halfHeight = 0f;
            float halfDepth = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -extents.x : extents.x,
                    (i & 2) == 0 ? -extents.y : extents.y,
                    (i & 4) == 0 ? -extents.z : extents.z);
                Vector3 local = toCamera * corner;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(local.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(local.y));
                halfDepth = Mathf.Max(halfDepth, Mathf.Abs(local.z));
            }

            const float aspect = (float)Width / Height;
            const float margin = 1.08f; // 四周留一点空，模型别贴着边

            float size = Mathf.Max(halfHeight, halfWidth / aspect) * margin;
            float distance = halfDepth + size + 1f;

            _camera.aspect = aspect; // 显式给，别指望 targetTexture 赋值的时机
            _camera.orthographicSize = Mathf.Max(size, 0.01f);
            _camera.transform.rotation = view;
            _camera.transform.position = bounds.center - view * Vector3.forward * distance;
            _camera.nearClipPlane = 0.01f;
            _camera.farClipPlane = distance + halfDepth * 2f + 1f;
        }

        /// <summary>渲染台按需现建；场景切换把它销毁掉之后会自动重建。</summary>
        private static void EnsureStage()
        {
            if (_stage != null && _camera != null && _light != null)
            {
                return;
            }

            // HideAndDontSave：编辑器里也会用到这个渲染台（Tools/美术/导出建筑略缩图），
            // 不隐藏就会往当前打开的场景里塞一个节点，还可能被顺手存进场景文件
            var stage = new GameObject("BuildingThumbnailStage") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = StageOrigin;
            _stage = stage.transform;

            var cameraGo = new GameObject("Camera") { hideFlags = HideFlags.HideAndDontSave };
            cameraGo.transform.SetParent(stage.transform, false);
            _camera = cameraGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.cullingMask = 1 << StageLayer;
            _camera.allowHDR = false;
            _camera.allowMSAA = true;
            // 手动 Render()，不参与常规渲染循环
            _camera.enabled = false;

            var lightGo = new GameObject("Light") { hideFlags = HideFlags.HideAndDontSave };
            lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Directional;
            // 只照渲染台这一层：平行光本身没有位置概念，不限层就会把正式场景一起点亮
            _light.cullingMask = 1 << StageLayer;
            _light.shadows = LightShadows.None;
            _light.intensity = 1.1f;
        }

        private static void SetLayerRecursive(Transform node, int layer)
        {
            node.gameObject.layer = layer;
            for (int i = 0; i < node.childCount; i++)
            {
                SetLayerRecursive(node.GetChild(i), layer);
            }
        }

        private static bool TryGetWorldBounds(GameObject instance, out Bounds bounds)
        {
            bounds = new Bounds();
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            bool any = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }
    }
}
