using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 民居拼装：把 Assets/Res/resHouse_NN/fbx 的 1×1 小房子按 BuildingVariant 的 footprint
    /// 拼成 田 / L / 凹 三种居民区 Prefab（residence_01/02/03），输出路径与
    /// <see cref="ModelPrefabGenerator"/> 同口径（Assets/Resources/Prefab/Building/&lt;variantId&gt;.prefab）。
    ///
    /// 结构仍是「identity 包装根 + 子节点」，只是子节点从单个 FBX 变成每格一栋小房子：
    /// <code>
    ///   residence_02            ← 包装根 identity，原点=占地矩形最小角（与生成器口径一致）
    ///   ├── cell_0_0            ← 每格一个 identity 壳，位于格子最小角
    ///   │   └── resHouse_03 实例 ← 轴向修正在这里，缩放到 1 格、格内居中、贴地
    ///   └── ...
    /// </code>
    ///
    /// 房子样式与朝向按 variantId 确定性伪随机（djb2 种子），重跑结果不变；
    /// 不用 UnityEngine.Random，免得每次拼出来不一样导致 Prefab diff 噪声。
    ///
    /// 注意：residence 变体如今不再走「单 FBX → 生成白模 Prefab」路径；
    /// 若 Assets/Res/residence_0N 还留着旧 FBX，先跑本菜单会被 GenerateAll 覆盖——
    /// 正确顺序是先 GenerateAll 再拼装，或删掉旧的 residence_0N 资产目录。
    /// </summary>
    public static class ResidenceAssembler
    {
        private const string ResRoot = "Assets/Res";
        private const string HousePrefix = "resHouse_";
        private const string ResidenceBuildingId = "residence";

        /// <summary>房子在格内的占比：1.0 = 贴满格宽。取 1.0 让长边贴满，校验口径与单体建筑一致。</summary>
        private const float CellFill = 1.0f;

        [MenuItem("Tools/美术/拼装民居 Prefab（resHouse_* → residence 变体）", false, 4)]
        public static void AssembleAll()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            List<string> houseFbx = FindHouseAssets();
            if (houseFbx.Count == 0)
            {
                Debug.LogError($"[民居拼装] {ResRoot} 下没有任何 {HousePrefix}*/fbx/*.fbx，先跑生成流水线。");
                return;
            }

            float cellSize = Tables.GameConfig.cellSize;
            if (cellSize <= 0f)
            {
                Debug.LogError($"[民居拼装] GameConfig.cellSize = {cellSize}，无法拼装。");
                return;
            }

            var log = new StringBuilder();
            int made = 0;

            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
                {
                    if (variant.buildingId != ResidenceBuildingId)
                    {
                        continue;
                    }
                    if (Assemble(variant, houseFbx, cellSize, preview, log))
                    {
                        made++;
                    }
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[民居拼装] 生成 {made} 个居民区 Prefab（房子样式 {houseFbx.Count} 种）。\n{log}");
        }

        private static List<string> FindHouseAssets()
        {
            var result = new List<string>();
            if (!Directory.Exists(ResRoot))
            {
                return result;
            }
            foreach (string dir in Directory.GetDirectories(ResRoot, HousePrefix + "*"))
            {
                string id = Path.GetFileName(dir);
                string fbx = $"{ResRoot}/{id}/fbx/{id}.fbx";
                if (File.Exists(fbx))
                {
                    result.Add(fbx);
                }
            }
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        private static bool Assemble(
            BuildingVariantRow variant, List<string> houseFbx, float cellSize, Scene workScene, StringBuilder log)
        {
            string[] footprint = variant.footprint;
            if (footprint == null || footprint.Length == 0)
            {
                log.AppendLine($"  - {variant.variantId}: footprint 为空，跳过");
                return false;
            }
            int rows = footprint.Length;
            int cols = footprint[0].Length;

            uint seed = Djb2(variant.variantId);

            var root = new GameObject(variant.variantId);
            SceneManager.MoveGameObjectToScene(root, workScene);
            try
            {
                int cellIndex = 0;
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        // 与 ModelPrefabGenerator.OrientToMask 同口径：掩码第一行在 z 最大侧
                        if (footprint[rows - 1 - r][c] != '#')
                        {
                            continue;
                        }

                        // 样式：种子决定起始偏移与步进（步进取奇数保证 5 种样式全被轮到）
                        int styleIndex = (int)((seed / 7u + (uint)cellIndex * (1u + seed % 3u * 2u)) % (uint)houseFbx.Count);
                        // 朝向：0/90/180/270 确定性伪随机
                        float yaw = 90f * ((seed >> 3) + (uint)cellIndex * 5u & 3u);

                        string fbxPath = houseFbx[styleIndex];
                        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                        if (modelAsset == null)
                        {
                            log.AppendLine($"  - {variant.variantId}: 读不到 {fbxPath}");
                            continue;
                        }

                        var cellShell = new GameObject($"cell_{c}_{r}");
                        cellShell.transform.SetParent(root.transform, false);
                        cellShell.transform.localPosition = new Vector3(c * cellSize, 0f, r * cellSize);

                        var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, workScene);
                        model.transform.SetParent(cellShell.transform, false);
                        FitHouseIntoCell(model, cellShell.transform, yaw, cellSize);

                        cellIndex++;
                    }
                }

                if (cellIndex == 0)
                {
                    log.AppendLine($"  - {variant.variantId}: footprint 没有实心格");
                    return false;
                }

                // 整块收尾（与 ModelPrefabGenerator 的单体口径对齐，两步）：
                // 1) min-fit 缩放：逐格 min 缩放后整块长边不保证精确贴满（差零点几个百分点
                //    就会被「长边应当贴满」判据拦下）。绕整块中心把所有格壳等比放缩到
                //    min(目标宽/实测宽, 目标深/实测深)，长边精确贴满且不越格。
                // 2) 重居中：把整块包围盒平移到占地矩形正中、底面贴地，转 180° 不跳格。
                Bounds blockBounds;
                float targetX = cols * cellSize;
                float targetZ = rows * cellSize;
                if (TryGetWorldMeshBounds(root, out blockBounds))
                {
                    float fit = Mathf.Min(targetX / blockBounds.size.x, targetZ / blockBounds.size.z);
                    if (Mathf.Abs(fit - 1f) > 1e-4f)
                    {
                        Vector3 c = blockBounds.center;
                        foreach (Transform child in root.transform)
                        {
                            Vector3 p = child.localPosition;
                            child.localPosition = new Vector3(
                                c.x + (p.x - c.x) * fit, p.y * fit, c.z + (p.z - c.z) * fit);
                            child.localScale *= fit;
                        }
                    }
                }
                if (TryGetWorldMeshBounds(root, out blockBounds))
                {
                    var shift = new Vector3(
                        targetX * 0.5f - blockBounds.center.x,
                        -blockBounds.min.y,
                        targetZ * 0.5f - blockBounds.center.z);
                    foreach (Transform child in root.transform)
                    {
                        child.localPosition += shift;
                    }
                }

                string outPath = $"Assets/Resources/Prefab/{ModelPrefabGenerator.BuildingSubDir}/{variant.variantId}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, outPath);
                log.AppendLine($"  + {outPath}（{cellIndex} 栋，掩码 {string.Join("|", footprint)}）");
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// 把一栋房子塞进一个格子：先施加偏航，再等比缩放到格宽（取 min 不越格），
        /// 最后在格内 XZ 居中、底面贴地。量包围盒走逐顶点世界坐标，与生成器同口径
        /// （cm 单位的 ×100 与 -90°X 轴向修正都天然包含在世界坐标里）。
        /// </summary>
        private static void FitHouseIntoCell(GameObject model, Transform cellShell, float yaw, float cellSize)
        {
            if (Mathf.Abs(yaw) > 0.01f)
            {
                model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * model.transform.localRotation;
            }

            float target = cellSize * CellFill;

            Bounds bounds;
            if (!TryGetWorldMeshBounds(model, out bounds))
            {
                Debug.LogWarning($"[民居拼装] {model.name} 没有网格，跳过缩放。");
                return;
            }
            float scale = Mathf.Min(target / Mathf.Max(bounds.size.x, 1e-4f), target / Mathf.Max(bounds.size.z, 1e-4f));
            model.transform.localScale *= scale;

            // 缩放绕模型轴心，包围盒会挪，必须重测再归位
            if (!TryGetWorldMeshBounds(model, out bounds))
            {
                return;
            }

            // cellShell 位于格子最小角：把包围盒最小角挪到格子最小角，再推半个空隙居中
            Vector3 cellMin = cellShell.position;
            model.transform.position += new Vector3(
                cellMin.x + (cellSize - bounds.size.x) * 0.5f - bounds.min.x,
                -bounds.min.y,
                cellMin.z + (cellSize - bounds.size.z) * 0.5f - bounds.min.z);
        }

        private static bool TryGetWorldMeshBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                Vector3[] vertices;
                try
                {
                    vertices = mesh.vertices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[民居拼装] 读不到 {mesh.name} 的顶点：{e.Message}");
                    continue;
                }
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]);
                    if (!any)
                    {
                        bounds = new Bounds(world, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(world);
                    }
                }
            }
            return any;
        }

        /// <summary>字符串稳定哈希（djb2-xor）。不用 GetHashCode：跨域重载/版本不稳定。</summary>
        private static uint Djb2(string s)
        {
            uint hash = 5381;
            for (int i = 0; i < s.Length; i++)
            {
                hash = (hash * 33) ^ s[i];
            }
            return hash;
        }
    }
}
