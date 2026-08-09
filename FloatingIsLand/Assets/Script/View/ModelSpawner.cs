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
    /// 摆放口径与 ModelPrefabGenerator（菜单 Tools/美术/生成白模 Prefab）严格对齐：
    /// 生成的 Prefab 根是一层 **identity 包装根**，模型挂在它下面。包装根原点 = 未旋转时占地矩形的最小角，
    /// 模型在这个 Columns×Rows 的矩形里 **XZ 居中、底面 y=0**（居中而非贴角，否则转 180° 会跳一格）。
    /// 表现层因此可以放心地直接给根 transform 赋位姿——早期直接实例化 FBX 时不行，
    /// 因为导入器把 Blender 的 Z-up→Y-up 轴向修正（-90°X）烤在了模型根上，
    /// 赋值 <c>Euler(0, yaw, 0)</c> 会把它一并抹掉，模型就整个躺倒了。
    /// </summary>
    public static class ModelSpawner
    {
        private static readonly Dictionary<string, GameObject> PrefabCache = new Dictionary<string, GameObject>();
        private static readonly HashSet<string> WarnedPaths = new HashSet<string>();
        private static Material _whiteBoxMaterial;

        /// <summary>
        /// 生成一个表现物件并摆到指定格。
        /// </summary>
        /// <param name="prefabPath">Resources 相对路径；空 = 直接走白模。</param>
        /// <param name="cornerPosition">锚点格的角点世界坐标（<see cref="GridGeometry.CellCorner"/> 的返回值）。</param>
        /// <param name="rotation">朝向。</param>
        /// <param name="parent">挂载父节点。</param>
        /// <param name="name">物件名（便于在 Hierarchy 里定位）。</param>
        /// <param name="footprint">占地掩码；白模退化时也用它定尺寸。</param>
        /// <param name="cellSize">格边长（米）。</param>
        public static GameObject Spawn(
            string prefabPath, Vector3 cornerPosition, Rotation rotation, Transform parent,
            string name, Footprint footprint, float cellSize)
        {
            GameObject prefab = LoadPrefab(prefabPath);
            GameObject instance;

            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, parent);
            }
            else
            {
                instance = CreateWhiteBox(footprint, cellSize, parent);
            }

            instance.name = name;
            PlaceAt(instance, cornerPosition, rotation, footprint, cellSize);
            return instance;
        }

        /// <summary>
        /// 把一个已生成的实例摆到指定格 —— ghost 每帧刷新与正式落地共用这一份，
        /// 两边各写一遍必然漂移，症状是「预览位置和落地位置差一截」。
        ///
        /// 为什么不能只写 <c>SetPositionAndRotation(corner, Euler(0, yaw, 0))</c>：
        /// 包装根原点在**未旋转**占地矩形的最小角，绕这个角点转 90° 会把整个矩形甩到负半轴
        /// （Unity 的 <c>Euler(0,90,0)</c> 把 +X 映射到 -Z），而 <see cref="Footprint.GetCells"/>
        /// 的口径是「旋转后各偏移仍从锚点向 +X/+Z 展开」。两边对不上，模型就会整体偏出占地格。
        /// 所以按朝向补一个平移，把旋转后的占地矩形重新推回锚点格的角点。
        /// </summary>
        public static void PlaceAt(
            GameObject instance, Vector3 cornerPosition, Rotation rotation, Footprint footprint, float cellSize)
        {
            if (instance == null)
            {
                return;
            }

            instance.transform.SetPositionAndRotation(
                PlacementPosition(cornerPosition, rotation, footprint, cellSize),
                PlacementRotation(rotation));
        }

        /// <summary>模型根该用的世界朝向。</summary>
        public static Quaternion PlacementRotation(Rotation rotation)
        {
            return Quaternion.Euler(0f, rotation.ToDegrees(), 0f);
        }

        /// <summary>
        /// 模型根该摆的世界坐标 = 锚点格角点 + 旋转补偿。
        ///
        /// 补偿是这么来的：包装根原点在**未旋转**占地矩形的最小角，绕这个角点施加 <see cref="PlacementRotation"/> 后，
        /// 矩形会被甩到负半轴（Unity 的 Euler(0,90,0) 把 +X 映射到 -Z、+Z 映射到 +X）。
        /// 而 <see cref="Footprint.GetCells"/> 的口径是「旋转后各偏移仍从锚点向 +X/+Z 展开」。
        /// 所以按朝向把跑到负半轴的那条边整体推回来，两边就重合了：
        ///   0°   +X,+Z 都在正向 → 不用补
        ///   90°  +X→-Z         → 补 +Z 一整条 SpanZ
        ///   180° 两轴都反向     → 两条都补
        ///   270° +Z→-X         → 补 +X 一整条 SpanX
        /// </summary>
        public static Vector3 PlacementPosition(
            Vector3 cornerPosition, Rotation rotation, Footprint footprint, float cellSize)
        {
            if (footprint == null)
            {
                return cornerPosition;
            }

            float spanX = footprint.SpanX(rotation) * cellSize;
            float spanZ = footprint.SpanZ(rotation) * cellSize;
            switch (rotation)
            {
                case Rotation.Deg90:
                    return cornerPosition + new Vector3(0f, 0f, spanZ);
                case Rotation.Deg180:
                    return cornerPosition + new Vector3(spanX, 0f, spanZ);
                case Rotation.Deg270:
                    return cornerPosition + new Vector3(spanX, 0f, 0f);
                default:
                    return cornerPosition;
            }
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

        /// <summary>
        /// 白模：一个贴着占地、底面在 y=0 的方块。
        /// 尺寸取**未旋转**的 Columns×Rows —— 白模和真模型一样由 <see cref="PlaceAt"/> 统一施加旋转，
        /// 这里再按旋转后的跨度建盒就等于转了两次。
        /// </summary>
        private static GameObject CreateWhiteBox(Footprint footprint, float cellSize, Transform parent)
        {
            var root = new GameObject("WhiteBox");
            root.transform.SetParent(parent, false);

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.transform.SetParent(root.transform, false);
            Object.Destroy(box.GetComponent<Collider>());

            int columns = footprint != null ? footprint.Columns : 1;
            int rows = footprint != null ? footprint.Rows : 1;
            float width = Mathf.Max(1, columns) * cellSize;
            float depth = Mathf.Max(1, rows) * cellSize;
            float height = Mathf.Max(width, depth) * 0.6f;

            box.transform.localScale = new Vector3(width * 0.9f, height, depth * 0.9f);
            // Cube 轴心在中心：往正方向挪半个身位，让包装根原点落到占地最小角、底面贴地
            box.transform.localPosition = new Vector3(width * 0.5f, height * 0.5f, depth * 0.5f);

            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = GetWhiteBoxMaterial();
            return root;
        }

        /// <summary>
        /// 当前工程是否启用了 SRP（URP/HDRP）。
        ///
        /// 不能靠 <c>Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")</c> 这种写法挑 Shader：
        /// 只要装了 URP 包，那个 Shader.Find 就找得到、永远轮不到 Standard；而 URP 的 Shader 在 Built-in 下
        /// 所有 SubShader 都会被跳过，最终落到错误着色器 —— 渲染出来是纯品红。
        /// 本工程装了 URP 包但**没启用**（ProjectSettings 里没有管线资产），正是这种情况。
        /// </summary>
        private static bool UsingScriptableRenderPipeline
        {
            get { return UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null; }
        }

        private static Material GetWhiteBoxMaterial()
        {
            if (_whiteBoxMaterial != null)
            {
                return _whiteBoxMaterial;
            }

            Shader shader = UsingScriptableRenderPipeline
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
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
            if (!UsingScriptableRenderPipeline)
            {
                // Built-in 下用 Sprites/Default：本来就是「不受光 + 按 _Color 的 alpha 混合」，
                // 不用手动配混合状态。Unlit/Color 不行——它是不透明的，ghost 会变成实心色块。
                var builtIn = new Material(Shader.Find("Sprites/Default"))
                {
                    hideFlags = HideFlags.DontSave,
                    color = tint,
                };
                return builtIn;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
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
