using System.Collections.Generic;
using System.IO;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.View;
using SoulGames.EasyGridBuilderPro;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.ViewEGB.EditorTools
{
    /// <summary>
    /// 按关卡岛屿模型的实际轮廓自动描摹地形，并散布地图元素，直接产出 Resources/Maps/stage_{id}.json。
    ///
    /// 手刷（<see cref="MapEditorWindow"/>）适合精修，但一整座岛逐格刷不现实，而且刷出来的轮廓
    /// 和岛屿模型对不齐——玩家会看到建筑悬空或陷进山体。这里改成用模型本身当权威轮廓：
    ///
    ///   1. 按 Stage.islandCellSpan 把岛屿缩放居中（与运行时 WorldRenderer 完全同一套对位算法）；
    ///   2. 从每个格子中心往下打射线，命中岛面且落在顶面附近的格子 → 刷成 island；
    ///      只取顶部薄片是为了排掉岛底那圈锥形裙边，避免把陡坡也标成可建造；
    ///   3. 岛外一圈（离岛 1~N 格的虚空）刷成 floatingZone —— 设计里浮空区域就在
    ///      「地图外围或岛屿之间」，且是船坞的唯一合法地形；
    ///   4. 岛内按种子挖若干块 greenField（农田的必要地形）；
    ///   5. 调领域层 <see cref="MapElementScatter"/> 散布巨型风车/锚点/矿藏/风源；
    ///   6. 存盘。
    ///
    /// 结果是确定性的：同一个种子 + 同一个模型 → 同一张图。
    ///
    /// 注意：本工具**整图重写** stage_{id}.json——在地图编辑器里手工精修的地形和授权的风参数
    /// 都会被覆盖。先自动生成、再手工精修，顺序别反。
    /// </summary>
    public static class MapAutoBuilder
    {
        private const string MapsFolder = "Assets/Resources/Maps";

        /// <summary>
        /// 岛外浮空区域的环宽（格）。
        /// 必须大于最大船坞占地（dock_01 是 6×6）——浮空区域是船坞唯一合法地形，
        /// 环太窄的话 6×6 的占地在环里根本找不到一块完整的合法区域，船坞会变成永远放不下的废牌。
        /// </summary>
        private const int FloatingRingWidth = 8;

        /// <summary>绿地块数与半径（格）。</summary>
        private const int GreenPatchCount = 6;
        private const int GreenPatchRadius = 3;

        [MenuItem("Tools/地图/按岛屿模型生成全部关卡地图（描摹地形 + 散布元素）", false, 1)]
        public static void BuildAllStages()
        {
            var grid = Object.FindObjectOfType<EasyGridBuilderPro>();
            if (grid == null)
            {
                Debug.LogError("[生成地图] 当前场景里没有 EGB 网格。先跑 Tools → 框架 → 给 Main 场景接入 EGB 网格。");
                return;
            }

            if (!TableLoader.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            foreach (StageRow stage in Tables.Stage.All)
            {
                // 种子按关卡号取固定质数倍：同一关每次生成都一样，不同关互不雷同
                Build(stage, grid, seed: stage.stageId * 7919);
            }
        }

        /// <summary>
        /// 诊断：打印缩放后岛屿的包围盒、有命中的格数，以及每格顶面高度的十分位分布。
        /// 顶面薄片的厚度阈值该取多少，靠这个量出来而不是拍脑袋。
        /// </summary>
        [MenuItem("Tools/地图/诊断：岛屿地形描摹", false, 2)]
        public static void DiagnoseTrace()
        {
            var grid = Object.FindObjectOfType<EasyGridBuilderPro>();
            if (grid == null)
            {
                Debug.LogError("[描摹诊断] 当前场景里没有 EGB 网格。");
                return;
            }
            if (!TableLoader.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            StageRow stage = Tables.Stage.GetOrNull(1);
            if (stage == null)
            {
                return;
            }

            EnsureGridSize(grid, stage.mapWidth, stage.mapHeight);
            GridGeometry geometry = MakeGeometry(grid);
            GameObject island = InstantiateIsland(stage);
            if (island == null)
            {
                return;
            }

            try
            {
                Dictionary<Vector3Int, float> heightMap = IslandFitter.Fit(island, geometry, stage.islandCellSpan);
                if (heightMap.Count == 0)
                {
                    Debug.LogError("[描摹诊断] 一格都没命中——岛屿 Prefab 上没有 MeshCollider。");
                    return;
                }

                Bounds bounds;
                TryGetWorldBounds(island, out bounds);
                float band = IslandFitter.SurfaceBand(island);
                Debug.Log($"[描摹诊断] 对位后包围盒：{bounds.size.x:0.#} × {bounds.size.y:0.#} × {bounds.size.z:0.#} m，" +
                          $"顶面 y={bounds.max.y:0.##}，底面 y={bounds.min.y:0.##}，" +
                          $"XZ 覆盖 {Mathf.CeilToInt(bounds.size.x / geometry.CellSize)} × {Mathf.CeilToInt(bounds.size.z / geometry.CellSize)} 格，" +
                          $"平台面 y={IslandFitter.SurfaceY:0.##}，高度带 ±{band:0.#} m");

                var heights = new List<float>(heightMap.Count);
                int onSurface = 0;
                foreach (KeyValuePair<Vector3Int, float> kv in heightMap)
                {
                    heights.Add(kv.Value);
                    if (IslandFitter.IsOnSurface(kv.Value, band))
                    {
                        onSurface++;
                    }
                }

                heights.Sort();
                Debug.Log($"[描摹诊断] 有命中的格数：{heights.Count}（整座岛的 XZ 轮廓上限），" +
                          $"其中落在平台面高度带内的：{onSurface}（{100f * onSurface / heights.Count:0.#}%，这就是可建造格数）");

                var buckets = new System.Text.StringBuilder("[描摹诊断] 顶面高度十分位：");
                for (int p = 0; p <= 10; p++)
                {
                    int index = Mathf.Clamp(Mathf.RoundToInt((heights.Count - 1) * p / 10f), 0, heights.Count - 1);
                    buckets.Append($" {p * 10}%={heights[index]:0.#}");
                }
                Debug.Log(buckets.ToString());
            }
            finally
            {
                Object.DestroyImmediate(island);
            }
        }

        private static GridGeometry MakeGeometry(EasyGridBuilderPro grid)
        {
            return new GridGeometry(
                grid.transform.position,
                grid.GetGridWidth(),
                grid.GetGridLength(),
                grid.GetCellSize(),
                grid.GetVerticalGridHeight(),
                grid.GetGridOriginType() == GridOrigin.Center);
        }

        private static void Build(StageRow stage, EasyGridBuilderPro grid, int seed)
        {
            // 场景网格必须先对齐到配表尺寸，否则描摹出来的坐标系和运行时对不上
            EnsureGridSize(grid, stage.mapWidth, stage.mapHeight);

            var geometry = new GridGeometry(
                grid.transform.position,
                grid.GetGridWidth(),
                grid.GetGridLength(),
                grid.GetCellSize(),
                grid.GetVerticalGridHeight(),
                grid.GetGridOriginType() == GridOrigin.Center);

            if (!geometry.IsValid)
            {
                Debug.LogError("[生成地图] EGB 网格参数非法（宽/长/格大小有 0？）。");
                return;
            }

            GameObject island = InstantiateIsland(stage);
            if (island == null)
            {
                return;
            }

            var terrain = new Dictionary<Vector3Int, string>();
            try
            {
                // 对位与运行时共用 IslandFitter，顺带拿回每格的顶面高度
                Dictionary<Vector3Int, float> heights = IslandFitter.Fit(island, geometry, stage.islandCellSpan);
                float band = IslandFitter.SurfaceBand(island);
                foreach (KeyValuePair<Vector3Int, float> kv in heights)
                {
                    // 只有落在平台面高度带里的格子可建造：尖峰和陡坡不刷，
                    // 否则建筑会浮在半空或陷进山体
                    if (IslandFitter.IsOnSurface(kv.Value, band))
                    {
                        terrain[kv.Key] = "island";
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(island);
            }

            if (terrain.Count == 0)
            {
                Debug.LogError("[生成地图] 一格都没描摹到。岛屿 Prefab 上有 MeshCollider 吗？" +
                               "（跑一次 Tools/美术/生成白模 Prefab 会给关卡岛屿自动补 MeshCollider）");
                return;
            }

            AddFloatingRing(terrain, geometry);
            CarveGreenFields(terrain, seed);

            MapSnapshot snapshot = ToSnapshot(stage.stageId, geometry, terrain, null);
            BuildRuleSet rules = BuildRuleSetFactory.Create();
            List<MapElementPlacement> elements = MapElementScatter.Scatter(snapshot, rules, seed);
            snapshot = ToSnapshot(stage.stageId, geometry, terrain, elements);

            Save(snapshot, stage.stageId);
            RefreshOverlay(snapshot, geometry);

            int island0 = Count(terrain, "island");
            int green = Count(terrain, "greenField");
            int floating = Count(terrain, "floatingZone");
            Debug.Log($"[生成地图] 第 {stage.stageId} 关已生成：地形 {terrain.Count} 格" +
                      $"（普通空岛 {island0} / 绿地 {green} / 浮空区域 {floating}），" +
                      $"地图元素 {elements.Count} 个 → {AssetPath(stage.stageId)}");

            WarnIfUnderfilled(stage, rules, elements, island0);
        }

        /// <summary>
        /// 散布数量不足配表 countMin 时报警。
        ///
        /// <see cref="MapElementScatter"/> 放不下就少放，**不会有任何提示**：可建造区一旦变小
        /// （比如收紧了可建造高度带），矿藏可能从 3 个掉到 1 个，而日志只会说"地图元素 7 个"，
        /// 看不出少的是什么、少了几个。等到局内发现资源不够，已经很难回想是哪一步导致的。
        /// </summary>
        private static void WarnIfUnderfilled(
            StageRow stage, BuildRuleSet rules, List<MapElementPlacement> elements, int islandCells)
        {
            var actual = new Dictionary<string, int>();
            for (int i = 0; i < elements.Count; i++)
            {
                string id = elements[i].ElementId;
                int n;
                actual.TryGetValue(id, out n);
                actual[id] = n + 1;
            }

            var missing = new System.Text.StringBuilder();
            for (int i = 0; i < rules.Elements.Count; i++)
            {
                MapElementDef def = rules.Elements[i];
                if (def.IsTerrain || def.CountMin <= 0)
                {
                    continue;
                }
                int placed;
                actual.TryGetValue(def.ElementId, out placed);
                if (placed < def.CountMin)
                {
                    missing.Append($"  · {def.ElementId}：实放 {placed}，配表最少 {def.CountMin}");
                }
            }

            if (missing.Length > 0)
            {
                Debug.LogWarning(
                    $"[生成地图] 第 {stage.stageId} 关的元素没放满（可建造格只有 {islandCells} 个，" +
                    "放不下就会静默少放)：\n" + missing +
                    "\n可建造区太小时的常见处理：调大 Stage.islandCellSpan 让岛占更多格，" +
                    "或放宽散布的最小间距。");
            }
        }

        private static GameObject InstantiateIsland(StageRow stage)
        {
            if (string.IsNullOrEmpty(stage.prefabPath))
            {
                Debug.LogError($"[生成地图] 第 {stage.stageId} 关的 Stage.prefabPath 为空，没有岛屿模型可描摹。");
                return null;
            }

            var prefab = Resources.Load<GameObject>(stage.prefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[生成地图] Resources 里没有岛屿模型 '{stage.prefabPath}'。" +
                               "跑一次 Tools/美术/生成白模 Prefab 生成。");
                return null;
            }

            GameObject island = Object.Instantiate(prefab);
            island.name = "__MapAutoBuilder_TempIsland";
            // 整棵树都要打标记，不能只打根：Prefab 是「identity 包装根 + FBX 子节点」两层结构，
            // 只标根的话一旦这期间发生域重载，DontSave 的根被丢掉、可保存的子节点会留成孤儿污染用户场景
            foreach (Transform node in island.GetComponentsInChildren<Transform>(true))
            {
                node.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
            // 缩放/对位交给 IslandFitter（与运行时共用同一份），这里只负责实例化
            return island;
        }


        /// <summary>岛外一圈虚空刷成浮空区域（§5：浮空区域在地图外围或岛屿之间，且是船坞唯一合法地形）。</summary>
        private static void AddFloatingRing(Dictionary<Vector3Int, string> terrain, GridGeometry geometry)
        {
            var islandCells = new List<Vector3Int>(terrain.Keys);
            var ring = new HashSet<Vector3Int>();

            for (int i = 0; i < islandCells.Count; i++)
            {
                Vector3Int cell = islandCells[i];
                for (int dz = -FloatingRingWidth; dz <= FloatingRingWidth; dz++)
                {
                    for (int dx = -FloatingRingWidth; dx <= FloatingRingWidth; dx++)
                    {
                        int x = cell.x + dx;
                        int z = cell.y + dz;
                        if (x < 0 || z < 0 || x >= geometry.Width || z >= geometry.Length)
                        {
                            continue;
                        }
                        var candidate = new Vector3Int(x, z, 0);
                        if (!terrain.ContainsKey(candidate))
                        {
                            ring.Add(candidate);
                        }
                    }
                }
            }

            foreach (Vector3Int cell in ring)
            {
                terrain[cell] = "floatingZone";
            }
        }

        /// <summary>在岛面上挖若干块绿地（农田的必要地形，§12.3）。</summary>
        private static void CarveGreenFields(Dictionary<Vector3Int, string> terrain, int seed)
        {
            var islandCells = new List<Vector3Int>();
            foreach (KeyValuePair<Vector3Int, string> kv in terrain)
            {
                if (kv.Value == "island")
                {
                    islandCells.Add(kv.Key);
                }
            }
            if (islandCells.Count == 0)
            {
                return;
            }

            // 字典遍历顺序不保证稳定，先排序再取样，否则同种子也可能刷出不同的绿地
            islandCells.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));

            var random = new DeterministicRandom(seed ^ 0x5EED);
            for (int p = 0; p < GreenPatchCount; p++)
            {
                Vector3Int center = islandCells[random.NextInt(0, islandCells.Count)];
                int radius = 1 + random.NextInt(1, GreenPatchRadius + 1);

                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dz * dz > radius * radius)
                        {
                            continue;
                        }
                        var cell = new Vector3Int(center.x + dx, center.y + dz, 0);
                        string existing;
                        // 只把普通空岛改成绿地，不动浮空区域
                        if (terrain.TryGetValue(cell, out existing) && existing == "island")
                        {
                            terrain[cell] = "greenField";
                        }
                    }
                }
            }
        }

        private static MapSnapshot ToSnapshot(
            int stageId, GridGeometry geometry,
            Dictionary<Vector3Int, string> terrain,
            IReadOnlyList<MapElementPlacement> elements)
        {
            var cells = new List<MapCell>(terrain.Count);
            foreach (KeyValuePair<Vector3Int, string> kv in terrain)
            {
                cells.Add(new MapCell(kv.Key.x, kv.Key.y, kv.Key.z, kv.Value));
            }
            return new MapSnapshot(stageId, geometry.Width, geometry.Length, 1, cells, elements);
        }

        private static void EnsureGridSize(EasyGridBuilderPro grid, int width, int length)
        {
            if (grid.GetGridWidth() == width && grid.GetGridLength() == length)
            {
                return;
            }

            var so = new SerializedObject(grid);
            SerializedProperty w = so.FindProperty("gridWidth");
            SerializedProperty l = so.FindProperty("gridLength");
            if (w == null || l == null)
            {
                Debug.LogWarning("[生成地图] EasyGridBuilderPro 上找不到 gridWidth/gridLength（插件版本变了？），沿用场景现值。", grid);
                return;
            }
            w.intValue = width;
            l.intValue = length;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(grid);
            Debug.Log($"[生成地图] 场景网格已按 Stage 表调整为 {width}×{length}。");
        }

        private static void Save(MapSnapshot snapshot, int stageId)
        {
            if (!Directory.Exists(MapsFolder))
            {
                Directory.CreateDirectory(MapsFolder);
            }
            string path = AssetPath(stageId);
            File.WriteAllText(path, MapJson.Save(snapshot));
            AssetDatabase.ImportAsset(path);
        }

        private static void RefreshOverlay(MapSnapshot snapshot, GridGeometry geometry)
        {
            var overlay = Object.FindObjectOfType<TerrainOverlayRenderer>();
            if (overlay != null)
            {
                overlay.Rebuild(snapshot, geometry);
                SceneView.RepaintAll();
            }
        }

        private static string AssetPath(int stageId)
        {
            return $"{MapsFolder}/stage_{stageId}.json";
        }

        private static int Count(Dictionary<Vector3Int, string> terrain, string elementId)
        {
            int count = 0;
            foreach (KeyValuePair<Vector3Int, string> kv in terrain)
            {
                if (kv.Value == elementId)
                {
                    count++;
                }
            }
            return count;
        }

        private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
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
