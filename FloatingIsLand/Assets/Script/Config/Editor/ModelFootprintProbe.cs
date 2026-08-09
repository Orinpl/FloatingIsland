using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 量出「模型实际盖住哪些格」，和配表 footprint 并排打出来。
    ///
    /// 异形占地（如居民区的 L 形）最容易出的错是**美术给的形状和配表掩码根本不是一个形状**——
    /// 模型是 3 块地基摆成 L，配表却写了 4 格；或者 L 的缺口朝向反了。
    /// 这种错在 Scene 里只能看出"怪怪的"，说不清差在哪；量出来就成了可对账的数据。
    ///
    /// 量法是纯几何：把所有三角形投影到 XZ 平面，在每个格子里撒 N×N 个采样点做点在三角形内测试，
    /// 覆盖率 = 命中采样点的比例。不走物理射线——预览场景的 PhysicsScene 和默认场景是分开的，
    /// 而且这批模型不一定都有碰撞体。
    /// </summary>
    public static class ModelFootprintProbe
    {
        private const string PrefabRoot = "Assets/Resources/Prefab";

        /// <summary>每格采样密度（N×N）。9×9 对这批低模足够，再密只是白烧时间。</summary>
        private const int SamplesPerAxis = 9;

        /// <summary>覆盖率超过这个比例就算"模型占了这一格"。</summary>
        private const float OccupiedThreshold = 0.25f;

        /// <summary>本轮有多少个模型读不到顶点——一个都不能瞒，见 ProbeAll 的结论分支。</summary>
        private static int _unreadable;

        [MenuItem("Tools/美术/打印模型实际占格（对账配表 footprint）", false, 4)]
        public static void ProbeAll()
        {
            if (!Tables.IsLoaded)
            {
                UnityTableLoader.LoadFromResources();
            }

            float cellSize = Tables.GameConfig.cellSize;
            if (cellSize <= 0f)
            {
                Debug.LogError($"[占格探针] GameConfig.cellSize = {cellSize}，没法量。");
                return;
            }

            var report = new StringBuilder();
            int mismatched = 0;
            _unreadable = 0;

            foreach (BuildingVariantRow variant in Tables.BuildingVariant.All)
            {
                if (Probe(ModelPrefabGenerator.BuildingSubDir, variant.variantId, variant.footprint, cellSize, report))
                {
                    mismatched++;
                }
            }

            foreach (MapElementRow element in Tables.MapElement.All)
            {
                if (Probe(ModelPrefabGenerator.ElementSubDir, element.elementId, element.footprint, cellSize, report))
                {
                    mismatched++;
                }
            }

            // 也落一份到文件：Console 只显示日志首行，逐格覆盖率那种多行明细在里面根本看不全
            string outPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Application.dataPath) ?? ".", "Temp/footprint_probe.txt");
            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(outPath));
                System.IO.File.WriteAllText(outPath, report.ToString());
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[占格探针] 报告落盘失败：{e.Message}");
            }

            if (_unreadable > 0)
            {
                // 量不到就必须当错误报。之前这里返回"一致"，结果一个网格都没读到还打了句"全部一致"——
                // 假阴性比不量更危险，它会让人以为已经查过了。
                Debug.LogError($"[占格探针] {_unreadable} 个模型读不到网格顶点，**没有量到任何数据**" +
                               "（模型导入设置的 Read/Write 关着？本工程由 BuildingModelPostprocessor 强制打开，" +
                               $"改完要等 Unity 重新导入一遍）。明细：{outPath}\n{report}");
                return;
            }

            if (mismatched == 0)
            {
                Debug.Log($"[占格探针] 模型实际占格与配表 footprint 全部一致。明细：{outPath}\n{report}");
            }
            else
            {
                Debug.LogWarning($"[占格探针] {mismatched} 个模型的实际占格与配表 footprint 不一致" +
                                 "（配表是领域层的唯一真相，不一致就意味着玩家看到的建筑和它真正占的格子对不上）。" +
                                 $"明细：{outPath}\n" + report);
            }
        }

        /// <summary>返回 true 表示实际占格与配表不一致。</summary>
        private static bool Probe(string subDir, string assetId, string[] footprint, float cellSize, StringBuilder report)
        {
            if (footprint == null || footprint.Length == 0)
            {
                return false;
            }

            string path = $"{PrefabRoot}/{subDir}/{assetId}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                return false;
            }

            int cols = footprint[0].Length;
            int rows = footprint.Length;

            GameObject instance = PrefabUtility.LoadPrefabContents(path);
            float[,] coverage;
            Vector3 size;
            try
            {
                coverage = MeasureCoverage(instance, cols, rows, cellSize);
                size = WorldSize(instance);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }

            if (coverage == null)
            {
                report.AppendLine($"  {subDir}/{assetId}：读不到网格顶点，没量到。");
                _unreadable++;
                return false;
            }

            // 掩码第 r 行 → dz = rows-1-r，所以打印时行序与掩码一致，直接逐字符可比
            var measured = new string[rows];
            int measuredSolid = 0;
            for (int r = 0; r < rows; r++)
            {
                var line = new char[cols];
                for (int c = 0; c < cols; c++)
                {
                    bool occupied = coverage[c, rows - 1 - r] >= OccupiedThreshold;
                    line[c] = occupied ? '#' : '.';
                    if (occupied)
                    {
                        measuredSolid++;
                    }
                }
                measured[r] = new string(line);
            }

            bool mismatch = false;
            for (int r = 0; r < rows && !mismatch; r++)
            {
                mismatch = !string.Equals(measured[r], footprint[r]);
            }

            report.AppendLine($"  {subDir}/{assetId}  配表 {string.Join("|", footprint)}" +
                              $"  实测 {string.Join("|", measured)}" +
                              $"  （配表 {CountSolid(footprint)} 格 / 实测 {measuredSolid} 格）" +
                              $"  尺寸 宽{size.x:0.##} × 高{size.y:0.##} × 深{size.z:0.##} m" +
                              $"{(mismatch ? "  ← 不一致" : "")}");

            // 不一致时把每格覆盖率也打出来，好判断是"模型形状不对"还是"只差临界一点点"
            if (mismatch)
            {
                for (int r = 0; r < rows; r++)
                {
                    var line = new StringBuilder("      覆盖率 ");
                    for (int c = 0; c < cols; c++)
                    {
                        line.Append($"{coverage[c, rows - 1 - r],6:P0}");
                    }
                    report.AppendLine(line.ToString());
                }
            }

            return mismatch;
        }

        /// <summary>
        /// 整棵子树的世界包围盒尺寸；高度明显小于进深往往说明模型还躺着。
        ///
        /// 逐顶点求，**不能用 <see cref="Renderer.bounds"/>**：那个值是「网格的局部 AABB 变换后再取 AABB」，
        /// 对带偏航的模型会保守放大（ModelPrefabGenerator.SolveYaw 会把斜 45° 的地基转正，
        /// 于是局部 AABB 自己是斜的，虚报可达 2 倍）。报告里的尺寸是美术判断「模型有没有超格」的依据，
        /// 虚报会让人以为对齐炸了，跑去改本来没问题的资产。
        /// </summary>
        private static Vector3 WorldSize(GameObject root)
        {
            var bounds = new Bounds();
            bool any = false;

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
                    // 读不到顶点的情形由 CollectTrianglesXZ 统一报，这里静默跳过不重复刷屏
                    continue;
                }
                if (vertices == null)
                {
                    continue;
                }

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = toWorld.MultiplyPoint3x4(vertices[i]);
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

            return any ? bounds.size : Vector3.zero;
        }

        private static int CountSolid(string[] mask)
        {
            int n = 0;
            foreach (string line in mask)
            {
                foreach (char ch in line)
                {
                    if (ch == '#')
                    {
                        n++;
                    }
                }
            }
            return n;
        }

        /// <summary>
        /// 每格的覆盖率 [c, r]（r 为 dz 方向，0 = z 最小侧）。没有可读网格返回 null。
        ///
        /// 前提：<paramref name="root"/> 是生成器产出的 identity 包装根，占地矩形就是
        /// [0, cols*cellSize] × [0, rows*cellSize]（见 ModelPrefabGenerator 的对齐口径）。
        ///
        /// 对生成器开放：<see cref="ModelPrefabGenerator"/> 给异形掩码定向时要用同一把尺子打分。
        /// 「生成时怎么摆」和「验收时怎么量」共用这一份，两边各写一遍必然漂移——
        /// 漂移的表现是生成器自认为摆对了、探针却报不一致，而且谁也说不清该信哪个。
        /// </summary>
        internal static float[,] MeasureCoverage(GameObject root, int cols, int rows, float cellSize)
        {
            List<Vector2> triangles = CollectTrianglesXZ(root);
            if (triangles == null || triangles.Count == 0)
            {
                return null;
            }

            var coverage = new float[cols, rows];
            float step = cellSize / (SamplesPerAxis + 1);

            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    int hits = 0;
                    for (int sx = 1; sx <= SamplesPerAxis; sx++)
                    {
                        for (int sz = 1; sz <= SamplesPerAxis; sz++)
                        {
                            var p = new Vector2(c * cellSize + sx * step, r * cellSize + sz * step);
                            if (CoveredByAnyTriangle(triangles, p))
                            {
                                hits++;
                            }
                        }
                    }
                    coverage[c, r] = (float)hits / (SamplesPerAxis * SamplesPerAxis);
                }
            }

            return coverage;
        }

        /// <summary>所有网格的三角形投影到 XZ 平面（每 3 个 Vector2 一组）。</summary>
        private static List<Vector2> CollectTrianglesXZ(GameObject root)
        {
            var result = new List<Vector2>();
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                // 不看 mesh.isReadable：这批 FBX 的导入设置是 Read/Write Off（isReadable=false），
                // 但那个开关只管运行时；编辑器里 Unity 始终保留着导入资产的网格数据，照样读得到。
                // 之前按 isReadable 提前 continue，结果一个三角形都没收集到，还报了句"全部一致"——假阴性最坑。
                Vector3[] vertices;
                try
                {
                    vertices = mesh.vertices;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[占格探针] 读不到 {mesh.name} 的顶点：{e.Message}");
                    continue;
                }
                if (vertices == null || vertices.Length == 0)
                {
                    continue;
                }
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                var projected = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 w = toWorld.MultiplyPoint3x4(vertices[i]);
                    projected[i] = new Vector2(w.x, w.z);
                }

                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    int[] indices = mesh.GetTriangles(sub);
                    for (int i = 0; i + 2 < indices.Length; i += 3)
                    {
                        result.Add(projected[indices[i]]);
                        result.Add(projected[indices[i + 1]]);
                        result.Add(projected[indices[i + 2]]);
                    }
                }
            }

            return result;
        }

        private static bool CoveredByAnyTriangle(List<Vector2> triangles, Vector2 p)
        {
            for (int i = 0; i + 2 < triangles.Count; i += 3)
            {
                if (PointInTriangle(p, triangles[i], triangles[i + 1], triangles[i + 2]))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p - a, b - a);
            float d2 = Cross(p - b, c - b);
            float d3 = Cross(p - c, a - c);
            bool anyNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool anyPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            // 三角形有正反面，符号可能整体翻转；只要不是"有正有负"就说明点在同侧 = 在三角形内
            return !(anyNegative && anyPositive);
        }

        private static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }
    }
}
