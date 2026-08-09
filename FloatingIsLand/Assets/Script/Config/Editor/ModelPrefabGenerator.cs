using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 按配表把 Assets/Res/&lt;资产名&gt;/fbx/&lt;资产名&gt;.fbx 批量转成 Resources 下的 Prefab，
    /// 生成结果就是配表 prefabPath 列填的东西（Resources 相对路径，不带扩展名）：
    ///   BuildingVariant → Assets/Resources/Prefab/Building/&lt;variantId&gt;.prefab → "Prefab/Building/&lt;variantId&gt;"
    ///   MapElement      → Assets/Resources/Prefab/Element/&lt;elementId&gt;.prefab  → "Prefab/Element/&lt;elementId&gt;"
    ///   Stage           → Assets/Resources/Prefab/Stage/stage_NN.prefab        → "Prefab/Stage/stage_NN"
    ///
    /// 生成的是引用 FBX 的嵌套 Prefab（不是拍平副本）：美术重导模型后表现自动跟着更新，
    /// BuildingModelPostprocessor 的按格对齐结果也直接继承。
    /// 关卡岛屿额外补 MeshCollider——地形描摹刷子要往岛面上打射线。
    /// </summary>
    public static class ModelPrefabGenerator
    {
        private const string ResRoot = "Assets/Res";
        private const string PrefabRoot = "Assets/Resources/Prefab";

        public const string BuildingSubDir = "Building";
        public const string ElementSubDir = "Element";
        public const string StageSubDir = "Stage";

        [MenuItem("Tools/美术/生成白模 Prefab（FBX → Resources/Prefab）", false, 1)]
        public static void GenerateAll()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            var log = new StringBuilder();
            int made = 0;
            int missing = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
                {
                    // 资产可能按变体名（residence_01.fbx）也可能按建筑名（farm.fbx）给，两个都试
                    string fbx = ResolveFbx(variant.variantId) ?? ResolveFbx(variant.buildingId);
                    if (Generate(fbx, BuildingSubDir, variant.variantId, false, log))
                    {
                        made++;
                    }
                    else
                    {
                        missing++;
                    }
                }

                foreach (MapElementRow element in Tables.MapElement.All)
                {
                    string fbx = ResolveFbx(element.elementId);
                    if (fbx == null)
                    {
                        // 地形类元素（绿地/空岛/浮空区域）与风源本就没有模型，靠地形层着色表现，不算缺失
                        continue;
                    }
                    if (Generate(fbx, ElementSubDir, element.elementId, false, log))
                    {
                        made++;
                    }
                    else
                    {
                        missing++;
                    }
                }

                foreach (StageRow stage in Tables.Stage.All)
                {
                    string assetId = StageAssetId(stage.stageId);
                    string fbx = ResolveFbx(assetId);
                    if (Generate(fbx, StageSubDir, assetId, true, log))
                    {
                        made++;
                    }
                    else
                    {
                        missing++;
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[白模 Prefab] 生成 {made} 个，缺模型 {missing} 个。\n{log}");
        }

        /// <summary>关卡岛屿资产名：stageId 1 → stage_01。</summary>
        public static string StageAssetId(int stageId)
        {
            return "stage_" + stageId.ToString("00");
        }

        /// <summary>配表 prefabPath 该填的值（Resources 相对、无扩展名）。</summary>
        public static string ResourcePath(string subDir, string assetId)
        {
            return "Prefab/" + subDir + "/" + assetId;
        }

        private static string ResolveFbx(string assetId)
        {
            if (string.IsNullOrEmpty(assetId))
            {
                return null;
            }
            string path = $"{ResRoot}/{assetId}/fbx/{assetId}.fbx";
            return File.Exists(path) ? path : null;
        }

        private static bool Generate(string fbxPath, string subDir, string assetId, bool addCollider, StringBuilder log)
        {
            if (fbxPath == null)
            {
                log.AppendLine($"  - 缺模型：{subDir}/{assetId}（Assets/Res/{assetId}/fbx/{assetId}.fbx 不存在，prefabPath 留空走白模占位）");
                return false;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
            {
                log.AppendLine($"  - 读不到模型：{fbxPath}");
                return false;
            }

            string dir = $"{PrefabRoot}/{subDir}";
            EnsureFolder(dir);
            string outPath = $"{dir}/{assetId}.prefab";

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = assetId;
                if (addCollider)
                {
                    AddMeshColliders(instance);
                }
                PrefabUtility.SaveAsPrefabAsset(instance, outPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            log.AppendLine($"  + {outPath}  →  prefabPath \"{ResourcePath(subDir, assetId)}\"");
            return true;
        }

        private static void AddMeshColliders(GameObject root)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                if (filter.sharedMesh == null || filter.GetComponent<MeshCollider>() != null)
                {
                    continue;
                }
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
        }

        private static void EnsureFolder(string assetDir)
        {
            if (AssetDatabase.IsValidFolder(assetDir))
            {
                return;
            }

            string[] parts = assetDir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        /// <summary>
        /// 打印各关卡岛屿模型的世界包围盒——决定网格铺多大、岛要不要挪。
        /// </summary>
        [MenuItem("Tools/美术/打印岛屿模型尺寸", false, 2)]
        public static void LogStageBounds()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            Debug.Log("[岛屿尺寸] cellSize=" + Tables.GameConfig.cellSize);
            foreach (StageRow stage in Tables.Stage.All)
            {
                string assetId = StageAssetId(stage.stageId);
                string fbx = ResolveFbx(assetId);
                if (fbx == null)
                {
                    Debug.Log($"[岛屿尺寸] {assetId}: 无模型");
                    continue;
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    Bounds bounds;
                    if (!TryGetWorldBounds(instance, out bounds))
                    {
                        Debug.Log($"[岛屿尺寸] {assetId}: 无网格");
                        continue;
                    }
                    float cellSize = Tables.GameConfig.cellSize;
                    Debug.Log(
                        $"[岛屿尺寸] {assetId}: 尺寸 {bounds.size.x:0.##} × {bounds.size.y:0.##} × {bounds.size.z:0.##} m" +
                        $"，中心 ({bounds.center.x:0.##}, {bounds.center.y:0.##}, {bounds.center.z:0.##})" +
                        $"，顶面 y={bounds.max.y:0.##}" +
                        $"  → 约 {Mathf.CeilToInt(bounds.size.x / cellSize)} × {Mathf.CeilToInt(bounds.size.z / cellSize)} 格");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
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
