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
    /// 手刷（<see cref="MapPainterWindow"/>）适合精修，但一整座岛逐格刷不现实，而且刷出来的轮廓
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
    /// </summary>
    public static class MapAutoBuilder
    {
        private const string MapsFolder = "Assets/Resources/Maps";

        /// <summary>顶面往下多少米之内算「可建造的岛面」，再往下就是陡坡与裙边。</summary>
        private const float PlateauDepth = 6f;

        /// <summary>岛外浮空区域的环宽（格）。</summary>
        private const int FloatingRingWidth = 3;

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

            GameObject island = InstantiateIsland(stage, geometry);
            if (island == null)
            {
                return;
            }

            var terrain = new Dictionary<Vector3Int, string>();
            try
            {
                // 编辑器态改了 transform 后必须手动同步给物理系统，否则射线打的是旧位置
                Physics.SyncTransforms();
                TraceIsland(island, geometry, terrain);
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
        }

        private static GameObject InstantiateIsland(StageRow stage, GridGeometry geometry)
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
            island.hideFlags = HideFlags.HideAndDontSave;

            if (!FitIsland(island, geometry, stage.islandCellSpan))
            {
                Object.DestroyImmediate(island);
                return null;
            }
            return island;
        }

        /// <summary>
        /// 与运行时 <see cref="WorldRenderer"/> 的对位算法保持一字不差：
        /// 等比缩放到 islandCellSpan 格跨度、居中到地图中心、顶面对到 y=0。
        /// 两边一旦漂移，刷出来的地形就会和玩家看到的岛错位。
        /// </summary>
        private static bool FitIsland(GameObject island, GridGeometry geometry, int cellSpan)
        {
            Bounds bounds;
            if (!TryGetWorldBounds(island, out bounds))
            {
                Debug.LogError("[生成地图] 岛屿模型没有任何 Renderer。");
                return false;
            }

            float longestSide = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longestSide <= Mathf.Epsilon)
            {
                Debug.LogError("[生成地图] 岛屿模型 XZ 包围盒为零。");
                return false;
            }

            int span = cellSpan > 0 ? cellSpan : 40;
            island.transform.localScale *= span * geometry.CellSize / longestSide;

            if (!TryGetWorldBounds(island, out bounds))
            {
                return false;
            }

            Vector3 mapCenter = geometry.CellCorner(geometry.Width / 2, geometry.Length / 2, 0);
            island.transform.position += new Vector3(
                mapCenter.x - bounds.center.x,
                -0.05f - bounds.max.y,
                mapCenter.z - bounds.center.z);
            return true;
        }

        /// <summary>逐格从上往下打射线，命中岛面顶部薄片的格子刷成 island。</summary>
        private static void TraceIsland(GameObject island, GridGeometry geometry, Dictionary<Vector3Int, string> terrain)
        {
            Bounds bounds;
            if (!TryGetWorldBounds(island, out bounds))
            {
                return;
            }

            // 只扫岛的包围盒覆盖的格子范围，250×250 全扫是 6 万次射线的无谓开销
            int minX, minZ, maxX, maxZ;
            geometry.WorldToCellUnclamped(bounds.min, 0, out minX, out minZ);
            geometry.WorldToCellUnclamped(bounds.max, 0, out maxX, out maxZ);
            minX = Mathf.Max(0, minX - 1);
            minZ = Mathf.Max(0, minZ - 1);
            maxX = Mathf.Min(geometry.Width - 1, maxX + 1);
            maxZ = Mathf.Min(geometry.Length - 1, maxZ + 1);

            float rayStartY = bounds.max.y + 10f;
            float rayLength = bounds.size.y + 20f;
            float plateauFloor = bounds.max.y - PlateauDepth;

            // RaycastNonAlloc 的结果无序且被缓冲区长度截断，缓冲区太小可能正好丢掉最高的那个面，
            // 描摹出来就会缺格。岛面一条射线撑死几层，32 足够宽裕。
            var hits = new RaycastHit[32];
            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector3 center = geometry.CellCenter(x, z, 0);
                    var ray = new Ray(new Vector3(center.x, rayStartY, center.z), Vector3.down);

                    int count = Physics.RaycastNonAlloc(ray, hits, rayLength, ~0, QueryTriggerInteraction.Ignore);
                    float topY = float.NegativeInfinity;
                    for (int i = 0; i < count; i++)
                    {
                        // 场景里可能还有别的碰撞体，只认这棵岛
                        if (!hits[i].transform.IsChildOf(island.transform) && hits[i].transform != island.transform)
                        {
                            continue;
                        }
                        if (hits[i].point.y > topY)
                        {
                            topY = hits[i].point.y;
                        }
                    }

                    if (topY > plateauFloor)
                    {
                        terrain[new Vector3Int(x, z, 0)] = "island";
                    }
                }
            }
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
