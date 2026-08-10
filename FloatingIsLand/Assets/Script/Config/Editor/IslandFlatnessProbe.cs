using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 量「关卡岛屿的顶面到底有多平」，按格给出高度分布。
    ///
    /// 建筑一律摆在网格平面（<see cref="View.GridGeometry.CellCorner"/> 的 y 只随层数变，不随地形），
    /// 所以岛面只要起伏，建筑就会浮空或陷地——而且这事在 Scene 里只能看出"怪怪的"，说不清差多少。
    /// 这里把它量成可对账的数字：**可建造区有百分之多少的格子落在中位面附近**。
    ///
    /// 走顶点而不是物理射线：射线要碰撞体，而临时实例建在预览场景里，
    /// 预览场景的 PhysicsScene 和默认场景是分开的，<c>Physics.Raycast</c> 在那儿一格都打不中
    /// （占格探针踩过同一个坑）。逐格取顶点最高 y 与射线测顶面等价，还不依赖碰撞体。
    /// </summary>
    public static class IslandFlatnessProbe
    {
        /// <summary>判定"这一格是平的"的高度容差（米）。半个格宽以内玩家基本看不出建筑浮沉。</summary>
        private static readonly float[] FlatBands = { 0.1f, 0.25f, 0.5f, 1f, 2f };

        [MenuItem("Tools/美术/打印岛屿平整度（顶面高度分布）", false, 5)]
        public static void ProbeAll()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            float cellSize = Tables.GameConfig.cellSize;
            if (cellSize <= 0f)
            {
                Debug.LogError($"[岛屿平整度] GameConfig.cellSize = {cellSize}，没法量。");
                return;
            }

            var report = new StringBuilder();
            foreach (StageRow stage in Tables.Stage.All)
            {
                Probe(stage, cellSize, report);
            }

            string outPath = WriteReport(report.ToString());
            Debug.Log($"[岛屿平整度] 明细：{outPath}\n{report}");
        }

        private static void Probe(StageRow stage, float cellSize, StringBuilder report)
        {
            string assetId = ModelPrefabGenerator.StageAssetId(stage.stageId);
            string path = $"Assets/Resources/Prefab/Stage/{assetId}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.AppendLine($"  {assetId}：Prefab 不存在，跳过。");
                return;
            }

            GameObject instance = PrefabUtility.LoadPrefabContents(path);
            try
            {
                List<float> layers;
                float scale;
                Vector2 center;
                float keepRadius;
                if (!IslandSurfaceFlattener.TryAnalyze(
                        instance, stage.islandCellSpan, cellSize,
                        out layers, out scale, out center, out keepRadius))
                {
                    report.AppendLine($"  {assetId}：读不到顶面/高度层。");
                    return;
                }

                // 逐格取「顶面顶点」的最高点。只认法线朝上的顶点，与压平同一口径——
                // 拿悬崖侧面的顶点当顶面，量出来的偏差全是崖壁高度，看不出平台平不平。
                var top = new Dictionary<long, float>();
                var edgeCells = new HashSet<long>();
                CollectTopHeights(instance, scale, cellSize, center, keepRadius, top, edgeCells);
                if (top.Count == 0)
                {
                    report.AppendLine($"  {assetId}：没有朝上的顶面顶点。");
                    return;
                }

                var layerText = new StringBuilder();
                for (int i = 0; i < layers.Count; i++)
                {
                    layerText.Append(i == 0 ? "" : " / ");
                    layerText.Append($"{layers[i] - layers[0]:+0.0;-0.0;0.0}m");
                }
                report.AppendLine(
                    $"  {assetId}：可建造区 {top.Count} 格（另有外缘留野 {edgeCells.Count} 格不计），" +
                    $"检测到 {layers.Count} 层台地（{layerText}）");

                // 分层地形的正确验收口径是「到**最近层**的偏差」。按"到中位面的偏差"量，
                // 非中位层的格子会被整片算成不平——地形明明是对的，报告却一路飘红。
                var deviations = new List<float>(top.Count);
                foreach (KeyValuePair<long, float> kv in top)
                {
                    deviations.Add(Mathf.Abs(kv.Value - IslandSurfaceFlattener.NearestLayer(layers, kv.Value)));
                }
                deviations.Sort();

                var line = new StringBuilder("      到最近台地面的偏差：");
                foreach (float band in FlatBands)
                {
                    int within = 0;
                    for (int i = 0; i < deviations.Count; i++)
                    {
                        if (deviations[i] <= band)
                        {
                            within++;
                        }
                    }
                    line.Append($"  ≤{band:0.##}m={within * 100f / deviations.Count:0}%");
                }
                report.AppendLine(line.ToString());
                report.AppendLine(
                    $"      偏差分位 p50={Quantile(deviations, 0.5f):0.00} " +
                    $"p90={Quantile(deviations, 0.9f):0.00} " +
                    $"p99={Quantile(deviations, 0.99f):0.00} m");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        /// <summary>
        /// 逐格收集顶面高度（法线朝上的顶点里取最高）。留野半径以外的格子只记数、不参与偏差统计——
        /// 那些格子按设计就该保持不规则，算进去等于给自己扣分。
        /// </summary>
        private static void CollectTopHeights(
            GameObject root, float scale, float cellSize, Vector2 center, float keepRadius,
            Dictionary<long, float> top, HashSet<long> edgeCells)
        {
            float normalThreshold = IslandSurfaceFlattener.SurfaceNormalThreshold;

            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices;
                Vector3[] normals;
                try
                {
                    vertices = mesh.vertices;
                    normals = mesh.normals;
                }
                catch (System.Exception)
                {
                    continue;
                }
                if (vertices == null || normals == null || normals.Length != vertices.Length)
                {
                    continue;
                }

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (toWorld.MultiplyVector(normals[i]).normalized.y < normalThreshold)
                    {
                        continue;
                    }

                    Vector3 p = toWorld.MultiplyPoint3x4(vertices[i]) * scale;
                    long key = CellKey(Mathf.FloorToInt(p.x / cellSize), Mathf.FloorToInt(p.z / cellSize));

                    if (Vector2.Distance(new Vector2(p.x, p.z), center) > keepRadius)
                    {
                        edgeCells.Add(key);
                        continue;
                    }

                    float existing;
                    if (!top.TryGetValue(key, out existing) || p.y > existing)
                    {
                        top[key] = p.y;
                    }
                }
            }

            // 一格可能既有内圈顶点又有外缘顶点，内圈优先（它是可建造区的一部分）
            foreach (long key in top.Keys)
            {
                edgeCells.Remove(key);
            }
        }

        private static float Quantile(List<float> sorted, float q)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt((sorted.Count - 1) * q), 0, sorted.Count - 1);
            return sorted[index];
        }

        private static long CellKey(int x, int z)
        {
            return ((long)z << 32) ^ (uint)x;
        }

        private static Bounds Encapsulate(List<Vector3> vertices)
        {
            var bounds = new Bounds(vertices[0], Vector3.zero);
            for (int i = 1; i < vertices.Count; i++)
            {
                bounds.Encapsulate(vertices[i]);
            }
            return bounds;
        }

        private static List<Vector3> CollectWorldVertices(GameObject root)
        {
            var result = new List<Vector3>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
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
                catch (System.Exception)
                {
                    continue;
                }
                if (vertices == null)
                {
                    continue;
                }

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    result.Add(toWorld.MultiplyPoint3x4(vertices[i]));
                }
            }
            return result;
        }

        private static string WriteReport(string content)
        {
            string outPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp/island_flatness.txt");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                System.IO.File.WriteAllText(outPath, content);
                return outPath;
            }
            catch (System.Exception e)
            {
                return $"（落盘失败：{e.Message}）";
            }
        }
    }
}
