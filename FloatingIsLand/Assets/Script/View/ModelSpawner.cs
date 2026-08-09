using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 按配表 prefabPath 从 Resources 实例化表现物件的公共件（建筑 / 地图元素 / 关卡岛屿共用）。
    ///
    /// prefabPath 为空或资源缺失时退化成白模方块：美术资产是分批到位的，
    /// 缺一个模型不该让整局跑不起来，但要在控制台留一条可定位的警告。
    ///
    /// 摆放口径与 BuildingModelPostprocessor（模型导入后处理）的轴心归位对齐：
    /// 模型轴心 = 占地最小角 + 底面 y=0，所以直接把 transform 放在锚点格的**角点**上即可，
    /// 不需要再补半格偏移。旋转绕锚点角点转，与 <see cref="Footprint"/> 的旋转口径一致。
    /// </summary>
    public static class ModelSpawner
    {
        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> WarnedPaths = new HashSet<string>();
        private static Material _whiteBoxMaterial;

        /// <summary>
        /// 生成一个表现物件。
        /// </summary>
        /// <param name="prefabPath">Resources 相对路径；空 = 直接走白模。</param>
        /// <param name="cornerPosition">锚点格的角点世界坐标（<see cref="IGridPresenter.CellToWorld"/> 的返回值）。</param>
        /// <param name="rotation">朝向。</param>
        /// <param name="parent">挂载父节点。</param>
        /// <param name="name">物件名（便于在 Hierarchy 里定位）。</param>
        /// <param name="fallbackSpanX">白模退化时的 X 跨度（格）。</param>
        /// <param name="fallbackSpanZ">白模退化时的 Z 跨度（格）。</param>
        /// <param name="cellSize">格边长（米）。</param>
        public static GameObject Spawn(
            string prefabPath, Vector3 cornerPosition, Rotation rotation, Transform parent,
            string name, int fallbackSpanX, int fallbackSpanZ, float cellSize)
        {
            GameObject prefab = LoadPrefab(prefabPath);
            GameObject instance;

            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, parent);
            }
            else
            {
                instance = CreateWhiteBox(fallbackSpanX, fallbackSpanZ, cellSize, parent);
            }

            instance.name = name;
            // 绕锚点角点旋转：Footprint 旋转后各偏移仍从 (0,0) 起算，两边口径必须一致，
            // 否则模型会转到占地格之外
            instance.transform.SetPositionAndRotation(cornerPosition, Quaternion.Euler(0f, rotation.ToDegrees(), 0f));
            return instance;
        }

        private static GameObject LoadPrefab(string prefabPath)
        {
            if (string.IsNullOrEmpty(prefabPath))
            {
                return null;
            }

            GameObject cached;
            if (PrefabCache.TryGetValue(prefabPath, out cached))
            {
                return cached;
            }

            var prefab = Resources.Load<GameObject>(prefabPath);
            PrefabCache[prefabPath] = prefab;
            if (prefab == null && WarnedPaths.Add(prefabPath))
            {
                Debug.LogWarning($"[表现] Resources 里没有 '{prefabPath}'，改用白模占位。" +
                                 "（跑一次菜单 Tools/美术/生成白模 Prefab 可批量补齐）");
            }
            return prefab;
        }

        /// <summary>白模：一个贴着占地、底面在 y=0 的方块，轴心同样在最小角。</summary>
        private static GameObject CreateWhiteBox(int spanX, int spanZ, float cellSize, Transform parent)
        {
            var root = new GameObject("WhiteBox");
            root.transform.SetParent(parent, false);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(root.transform, false);
            Object.Destroy(box.GetComponent<Collider>());

            float width = Mathf.Max(1, spanX) * cellSize;
            float depth = Mathf.Max(1, spanZ) * cellSize;
            float height = Mathf.Max(width, depth) * 0.6f;

            box.transform.localScale = new Vector3(width * 0.9f, height, depth * 0.9f);
            // Cube 轴心在中心：往正方向挪半个身位，让轴心落到占地最小角、底面贴地
            box.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, depth * 0.5f);

            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetWhiteBoxMaterial();
            return root;
        }

        private static Material GetWhiteBoxMaterial()
        {
            if (_whiteBoxMaterial != null)
            {
                return _whiteBoxMaterial;
            }

            // URP 工程里 Standard 会变紫，优先取管线默认 Shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            _whiteBoxMaterial = new Material(shader)
            {
                color = new Color(0.85f, 0.85f, 0.88f),
                hideFlags = HideFlags.DontSave,
            };
            return _whiteBoxMaterial;
        }

        /// <summary>把一棵实例整体设成半透明的 ghost 外观（摆放预览用）。</summary>
        public static void ApplyGhostAppearance(GameObject instance, Color tint)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = CreateGhostMaterial(tint);
                }
                renderer.materials = materials;
            }

            // ghost 只是预览，不该参与任何物理查询
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                collider.enabled = false;
            }
        }

        private static Material CreateGhostMaterial(Color tint)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader)
            {
                hideFlags = HideFlags.DontSave,
            };
            material.color = tint;

            // URP Lit/Unlit 的透明开关是一组 keyword + 渲染队列，少一样都会变成不透明
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            return material;
        }
    }
}
