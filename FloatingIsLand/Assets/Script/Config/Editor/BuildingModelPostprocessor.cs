using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// Assets/Res/&lt;资产名&gt;/fbx/&lt;资产名&gt;.fbx 导入后处理：
    /// 按配表 footprint 把模型缩放到对应格数，并把轴心对到「占地最小角 + 地面」。
    ///
    /// AI 生成的模型尺寸是随机的，手动逐个对格子太慢；这里以配表为唯一真相自动对齐：
    ///   缩放系数 = min(目标宽 / 包围盒宽, 目标深 / 包围盒深)  —— 取 min 保证不越格（设计要求可以不占满，但不能超出）
    ///   目标宽/深 = footprint 列数/行数 × GameConfig.cellSize
    /// 轴心放在占地最小角、底面 y=0，与 EGB 的 GetCellWorldPosition（返回格子角点）对齐，
    /// 摆放时直接把 transform.position 设成该格角点即可，不用再补偏移。
    /// </summary>
    public sealed class BuildingModelPostprocessor : AssetPostprocessor
    {
        private const string ResRoot = "Assets/Res/";
        private const string TableDir = "Assets/Resources/Tables";

        /// <summary>
        /// 改了对齐规则后把这个数 +1：Unity 用它判断后处理器是否变更，
        /// 变更时自动重新导入所有受影响的模型（否则已导入的资产不会重跑对齐）。
        /// </summary>
        public override uint GetVersion()
        {
            return 2;
        }

        private void OnPreprocessModel()
        {
            if (!IsResModel(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            // 生成的建筑资产没有动画/相机/灯光，关掉省导入时间与无用子资产
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsResModel(assetPath))
            {
                return;
            }

            string assetId = Path.GetFileNameWithoutExtension(assetPath);
            string[] footprint = FootprintCache.Lookup(assetId);
            if (footprint == null)
            {
                // 关卡岛屿（stage_*）等没有占地定义的资产不参与对齐
                return;
            }

            int cols, rows;
            if (!TryMeasure(footprint, out cols, out rows))
            {
                Debug.LogWarning($"[模型对齐] {assetId} 的 footprint 非法，跳过缩放。");
                return;
            }

            Bounds bounds;
            if (!TryGetLocalBounds(root, out bounds))
            {
                Debug.LogWarning($"[模型对齐] {assetId} 没有任何网格，跳过缩放。");
                return;
            }

            float cellSize = FootprintCache.CellSize;
            float targetX = cols * cellSize;
            float targetZ = rows * cellSize;
            if (bounds.size.x <= Mathf.Epsilon || bounds.size.z <= Mathf.Epsilon)
            {
                Debug.LogWarning($"[模型对齐] {assetId} 的包围盒在 XZ 上为零，跳过缩放。");
                return;
            }

            float scale = Mathf.Min(targetX / bounds.size.x, targetZ / bounds.size.z);

            // 轴心归位要在缩放前做：children 的 localPosition 与 bounds 都在 root 局部空间，
            // 平移完再让 root 整体缩放，两者不会互相干扰。
            Vector3 offset = new Vector3(-bounds.min.x, -bounds.min.y, -bounds.min.z);
            foreach (Transform child in root.transform)
            {
                child.localPosition += offset;
            }

            root.transform.localScale *= scale;

            Debug.Log($"[模型对齐] {assetId}: footprint {cols}×{rows} 格 → 目标 {targetX:0.##}×{targetZ:0.##}m，" +
                      $"原始包围盒 {bounds.size.x:0.##}×{bounds.size.z:0.##}m，缩放 ×{scale:0.####}");
        }

        private static bool IsResModel(string path)
        {
            return path.StartsWith(ResRoot, StringComparison.Ordinal)
                   && path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>掩码行列数；行长不一致或全空返回 false。</summary>
        private static bool TryMeasure(string[] footprint, out int cols, out int rows)
        {
            cols = 0;
            rows = 0;
            if (footprint == null || footprint.Length == 0)
            {
                return false;
            }

            cols = footprint[0] == null ? 0 : footprint[0].Length;
            rows = footprint.Length;
            if (cols == 0)
            {
                return false;
            }

            bool hasSolid = false;
            for (int i = 0; i < footprint.Length; i++)
            {
                string line = footprint[i];
                if (line == null || line.Length != cols)
                {
                    return false;
                }
                if (line.IndexOf('#') >= 0)
                {
                    hasSolid = true;
                }
            }
            return hasSolid;
        }

        /// <summary>合并所有网格在 root 局部空间的包围盒。</summary>
        private static bool TryGetLocalBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            Transform rootTransform = root.transform;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Bounds local = mesh.bounds;
                // 网格局部 → root 局部：只需 root 的逆矩阵乘子物体矩阵
                Matrix4x4 toRoot = rootTransform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Vector3 center = local.center;
                Vector3 extents = local.extents;

                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        center.x + ((i & 1) == 0 ? -extents.x : extents.x),
                        center.y + ((i & 2) == 0 ? -extents.y : extents.y),
                        center.z + ((i & 4) == 0 ? -extents.z : extents.z));
                    Vector3 p = toRoot.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return any;
        }

        /// <summary>
        /// 直接读 Assets/Resources/Tables 下的 JSON，而不是走 TableLoader ——
        /// 后者要求全部表齐备，且资产导入期间不宜调 Resources API。按文件写入时间失效重载。
        /// </summary>
        private static class FootprintCache
        {
            private static Dictionary<string, string[]> _byId;
            private static float _cellSize = 2f;
            private static DateTime _stamp;

            public static float CellSize
            {
                get
                {
                    EnsureLoaded();
                    return _cellSize;
                }
            }

            public static string[] Lookup(string assetId)
            {
                EnsureLoaded();
                string[] mask;
                return _byId != null && _byId.TryGetValue(assetId, out mask) ? mask : null;
            }

            private static void EnsureLoaded()
            {
                DateTime latest = LatestWriteTime();
                if (_byId != null && latest == _stamp)
                {
                    return;
                }

                _byId = new Dictionary<string, string[]>(StringComparer.Ordinal);
                _stamp = latest;

                Merge<BuildingVariantRow>("BuildingVariant", r => r.variantId, r => r.footprint);
                Merge<MapElementRow>("MapElement", r => r.elementId, r => r.footprint);
                MergeVariantsByBuildingId();

                GameConfig config = ReadJson<GameConfig>("GameConfig");
                if (config != null && config.cellSize > 0f)
                {
                    _cellSize = config.cellSize;
                }
            }

            /// <summary>
            /// 美术资产多数以 buildingId 命名（farm.fbx），而 footprint 挂在 variantId（farm_01）上。
            /// buildingId 只对应唯一变体时补一条别名；一对多（residence 有 3 个变体）时占地不唯一，
            /// 不猜——那种资产本来就按 variantId 命名（residence_01.fbx）。
            /// </summary>
            private static void MergeVariantsByBuildingId()
            {
                List<BuildingVariantRow> rows = ReadJson<List<BuildingVariantRow>>("BuildingVariant");
                if (rows == null)
                {
                    return;
                }

                var countByBuilding = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (BuildingVariantRow row in rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.buildingId))
                    {
                        continue;
                    }
                    int n;
                    countByBuilding.TryGetValue(row.buildingId, out n);
                    countByBuilding[row.buildingId] = n + 1;
                }

                foreach (BuildingVariantRow row in rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.buildingId))
                    {
                        continue;
                    }
                    if (countByBuilding[row.buildingId] == 1 && !_byId.ContainsKey(row.buildingId))
                    {
                        _byId[row.buildingId] = row.footprint;
                    }
                }
            }

            private static void Merge<TRow>(string table, Func<TRow, string> keyOf, Func<TRow, string[]> maskOf)
                where TRow : class
            {
                List<TRow> rows = ReadJson<List<TRow>>(table);
                if (rows == null)
                {
                    return;
                }
                foreach (TRow row in rows)
                {
                    if (row == null)
                    {
                        continue;
                    }
                    string key = keyOf(row);
                    if (!string.IsNullOrEmpty(key))
                    {
                        _byId[key] = maskOf(row);
                    }
                }
            }

            private static T ReadJson<T>(string table) where T : class
            {
                string path = Path.Combine(TableDir, table + ".json");
                if (!File.Exists(path))
                {
                    return null;
                }
                try
                {
                    return JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[模型对齐] 读 {path} 失败：{e.Message}");
                    return null;
                }
            }

            private static DateTime LatestWriteTime()
            {
                DateTime latest = DateTime.MinValue;
                foreach (string table in new[] { "BuildingVariant", "MapElement", "GameConfig" })
                {
                    string path = Path.Combine(TableDir, table + ".json");
                    if (File.Exists(path))
                    {
                        DateTime t = File.GetLastWriteTimeUtc(path);
                        if (t > latest)
                        {
                            latest = t;
                        }
                    }
                }
                return latest;
            }
        }
    }
}
