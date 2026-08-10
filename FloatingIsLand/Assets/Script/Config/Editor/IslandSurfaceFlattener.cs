using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// 把关卡岛屿模型的顶面**按台地分层压平**：先聚类出若干个高度层，每层各自吸附到自己的高度。
    ///
    /// 为什么要做：建筑一律摆在网格平面（GridGeometry.CellCorner 的 y 只随层数变、
    /// 不随地形），而 AI 重建出来的岛面是连续起伏的曲面——实测 stage_02 只有 2% 的格子落在中位面
    /// ±0.1m 内，一半格子偏差超过半米。顶面差多少，建筑就浮沉多少，看上去就是"地图糊了一整块上去"。
    ///
    /// **关键是分层，不是整平**：岛内本来就该有高低差，要的是「同一块平面各自保持平整」。
    /// 全压到一个高度会把地貌抹平成一张饼；所以这里先按高度聚类找台地，再逐层吸附。
    /// 副作用正好是想要的——每层绝对水平之后，层与层之间的过渡带自然收成一道陡坎，边界就明显了。
    ///
    /// 三条边界条件：
    ///   · **只压顶面**：法线朝上（<see cref="NormalUpThreshold"/>）才动，悬崖侧面和岛底根须不碰；
    ///   · **只压层附近**：离最近层超过 <see cref="SnapBand"/> 的是斜坡本身，压了就没有过渡了；
    ///   · **外缘留野**：到岛心距离超过 <see cref="EdgeKeepRatio"/> 的顶点不压，边缘保持不规则轮廓。
    ///
    /// 为什么不在 AssetPostprocessor 里做：导入后处理拿到的根局部空间还是 FBX 的 Z-up
    /// （导入器把 -90°X 修正烤在根 Transform 上），在那儿判断"法线朝上"会把侧面当顶面。
    /// 必须等模型挂进 identity 包装根、能拿到真正的世界空间了才算得对。
    ///
    /// 改过的网格**必须存成独立资产**：FBX 导入出来的 sharedMesh 是只读的导入产物，
    /// 直接把改完的临时 Mesh 塞给 Prefab，存盘后引用会变成 None（模型整个消失）。
    /// </summary>
    public static class IslandSurfaceFlattener
    {
        /// <summary>压平后的网格资产放这儿（与 Stage Prefab 同级，随 Prefab 一起重新生成）。</summary>
        private const string MeshDir = "Assets/Resources/Prefab/Stage/Meshes";

        /// <summary>法线 y 分量超过这个值才算"顶面"。0.5 ≈ 与水平面夹角 30° 以内的面。</summary>
        private const float NormalUpThreshold = 0.5f;

        /// <summary>高度直方图的分箱宽度（**缩放后的米**）。</summary>
        private const float LayerBinSize = 0.25f;

        /// <summary>一个高度层至少要占顶面顶点的这个比例，否则算噪声不成层。</summary>
        private const float MinLayerShare = 0.02f;

        /// <summary>两层高度至少相差这么多（米），更近的合并成一层——否则同一块平面会被切成两层。</summary>
        private const float MinLayerGap = 1.5f;

        /// <summary>顶点离最近层多远还吸附（米）。超出的是斜坡，保留为层间过渡。</summary>
        private const float SnapBand = 1.2f;

        /// <summary>到岛心的 XZ 距离超过「最大半径 × 本比例」的顶点不压，外缘保持不规则。</summary>
        private const float EdgeKeepRatio = 0.85f;

        /// <summary>
        /// 分层压平一棵已挂进 identity 包装根的岛屿模型，返回写进生成日志的说明。
        /// </summary>
        /// <param name="root">包装根（identity）。</param>
        /// <param name="assetId">资产名，用来命名网格资产。</param>
        /// <param name="cellSpan">配表 Stage.islandCellSpan，用来复刻运行时缩放。</param>
        /// <param name="cellSize">格边长。</param>
        public static string Flatten(GameObject root, string assetId, int cellSpan, float cellSize)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                return "无网格，未压平";
            }

            List<float> layers;
            float finalScale;
            Vector2 center;
            float keepRadius;
            if (!TryAnalyze(root, cellSpan, cellSize, out layers, out finalScale, out center, out keepRadius))
            {
                return "没找到可压平的高度层，未压平";
            }

            EnsureFolder(MeshDir);

            int movedTotal = 0;
            int vertexTotal = 0;
            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                Mesh baked = Object.Instantiate(filter.sharedMesh);
                baked.name = filters.Length == 1 ? assetId + "_flat" : $"{assetId}_flat_{i}";

                movedTotal += FlattenMesh(baked, filter.transform, finalScale, layers, center, keepRadius);
                vertexTotal += baked.vertexCount;

                string meshPath = $"{MeshDir}/{baked.name}.asset";
                AssetDatabase.DeleteAsset(meshPath);
                AssetDatabase.CreateAsset(baked, meshPath);
                filter.sharedMesh = baked;
            }

            if (vertexTotal == 0)
            {
                return "无可用顶点，未压平";
            }

            // 层高按相对最低层打印：绝对值取决于模型原点，对美术没有参考意义
            var layerText = new System.Text.StringBuilder();
            for (int i = 0; i < layers.Count; i++)
            {
                layerText.Append(i == 0 ? "" : " / ");
                layerText.Append($"{layers[i] - layers[0]:+0.0;-0.0;0.0}m");
            }
            return $"分 {layers.Count} 层压平（{layerText}），吸附 {movedTotal}/{vertexTotal} 个顶点，外缘 {1f - EdgeKeepRatio:P0} 留野";
        }

        /// <summary>
        /// 解析一棵岛屿模型的台地结构：缩放系数、岛心、留野半径、各层高度。
        ///
        /// 压平和验收探针**共用这一份**。两边各写一套聚类，"生成时认为分了 3 层"和
        /// "验收时按 4 层量"就会对不上，而且谁也说不清该信哪个——同 GridGeometry 那条教训。
        /// </summary>
        /// <returns>找不到任何高度层时返回 false。</returns>
        internal static bool TryAnalyze(
            GameObject root, int cellSpan, float cellSize,
            out List<float> layers, out float finalScale, out Vector2 center, out float keepRadius)
        {
            layers = new List<float>();
            finalScale = 1f;
            center = Vector2.zero;
            keepRadius = 0f;

            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0)
            {
                return false;
            }

            // 复刻 IslandFitter 的入场缩放，好让所有阈值都能按「玩家最终看到的米」来写。
            // 两边口径一旦分家，这里按 1.2m 压的带到了运行时就不是 1.2m 了。
            Bounds bounds;
            if (!TryComputeFinalScale(filters, cellSpan, cellSize, out finalScale, out bounds))
            {
                return false;
            }

            center = new Vector2(bounds.center.x, bounds.center.z) * finalScale;
            keepRadius = MeasureMaxRadius(filters, finalScale, center) * EdgeKeepRatio;
            layers = FindLayers(filters, finalScale, center, keepRadius);
            return layers.Count > 0;
        }

        /// <summary>某个高度该吸附到哪一层。</summary>
        internal static float NearestLayer(List<float> layers, float height)
        {
            return Nearest(layers, height);
        }

        /// <summary>法线 y 分量达到多少才算顶面——探针要按同一口径挑顶面顶点。</summary>
        internal static float SurfaceNormalThreshold
        {
            get { return NormalUpThreshold; }
        }

        /// <summary>复刻 IslandFitter：长边铺满 cellSpan 格。</summary>
        private static bool TryComputeFinalScale(
            MeshFilter[] filters, int cellSpan, float cellSize, out float scale, out Bounds bounds)
        {
            scale = 1f;
            if (!TryGetWorldBounds(filters, out bounds))
            {
                return false;
            }

            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest <= Mathf.Epsilon)
            {
                return false;
            }

            int span = cellSpan > 0 ? cellSpan : 40;
            scale = span * cellSize / longest;
            return true;
        }

        private static float MeasureMaxRadius(MeshFilter[] filters, float finalScale, Vector2 center)
        {
            float maxRadius = 0f;
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = toWorld.MultiplyPoint3x4(vertices[i]) * finalScale;
                    float radius = Vector2.Distance(new Vector2(p.x, p.z), center);
                    if (radius > maxRadius)
                    {
                        maxRadius = radius;
                    }
                }
            }
            return maxRadius;
        }

        /// <summary>
        /// 从顶面顶点的高度分布里聚类出台地层。
        ///
        /// 走一维直方图找密度峰，而不是 k-means 之类：台地数量事先不知道，而 k-means 要先给 k；
        /// 直方图能让"有几层"由数据自己说了算，也不会把一块大平面硬拆成两层。
        /// </summary>
        private static List<float> FindLayers(
            MeshFilter[] filters, float finalScale, Vector2 center, float keepRadius)
        {
            var heights = new List<float>();
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                if (normals == null || normals.Length != vertices.Length)
                {
                    continue;
                }

                Matrix4x4 toWorld = filter.transform.localToWorldMatrix;
                for (int i = 0; i < vertices.Length; i++)
                {
                    if (toWorld.MultiplyVector(normals[i]).normalized.y < NormalUpThreshold)
                    {
                        continue;
                    }
                    Vector3 p = toWorld.MultiplyPoint3x4(vertices[i]) * finalScale;
                    if (Vector2.Distance(new Vector2(p.x, p.z), center) > keepRadius)
                    {
                        continue;
                    }
                    heights.Add(p.y);
                }
            }

            var layers = new List<float>();
            if (heights.Count == 0)
            {
                return layers;
            }

            heights.Sort();
            float min = heights[0];
            float max = heights[heights.Count - 1];
            int binCount = Mathf.Max(1, Mathf.CeilToInt((max - min) / LayerBinSize) + 1);
            var bins = new int[binCount];
            foreach (float h in heights)
            {
                bins[Mathf.Clamp((int)((h - min) / LayerBinSize), 0, binCount - 1)]++;
            }

            int minPopulation = Mathf.Max(1, Mathf.RoundToInt(heights.Count * MinLayerShare));

            // 局部极大且够密的箱子 = 一个候选层
            var candidates = new List<KeyValuePair<float, int>>();
            for (int i = 0; i < binCount; i++)
            {
                if (bins[i] < minPopulation)
                {
                    continue;
                }
                if (i > 0 && bins[i - 1] > bins[i])
                {
                    continue;
                }
                if (i < binCount - 1 && bins[i + 1] > bins[i])
                {
                    continue;
                }
                candidates.Add(new KeyValuePair<float, int>(min + (i + 0.5f) * LayerBinSize, bins[i]));
            }

            // 靠得太近的峰合并，留人多的那个——同一块平面的顶点高度会散在相邻几个箱里，
            // 不合并就会把一块平面切成好几层，压完反而多出一堆看不见的台阶
            candidates.Sort((a, b) => a.Key.CompareTo(b.Key));
            foreach (KeyValuePair<float, int> candidate in candidates)
            {
                if (layers.Count > 0 && candidate.Key - layers[layers.Count - 1] < MinLayerGap)
                {
                    continue;
                }
                layers.Add(candidate.Key);
            }

            // 峰值只精确到箱宽，用箱内顶点的中位数精修一次，压出来的面才真在顶点密集处
            for (int i = 0; i < layers.Count; i++)
            {
                var near = new List<float>();
                foreach (float h in heights)
                {
                    if (Mathf.Abs(h - layers[i]) <= MinLayerGap * 0.5f)
                    {
                        near.Add(h);
                    }
                }
                if (near.Count > 0)
                {
                    near.Sort();
                    layers[i] = near[near.Count / 2];
                }
            }

            return layers;
        }

        /// <summary>压平一张网格，返回被吸附的顶点数。</summary>
        private static int FlattenMesh(
            Mesh mesh, Transform owner, float finalScale, List<float> layers, Vector2 center, float keepRadius)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (vertices.Length == 0 || normals == null || normals.Length != vertices.Length)
            {
                return 0;
            }

            Matrix4x4 toWorld = owner.localToWorldMatrix;
            Matrix4x4 toLocal = toWorld.inverse;
            Vector3 localUp = toLocal.MultiplyVector(Vector3.up).normalized;
            int moved = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (toWorld.MultiplyVector(normals[i]).normalized.y < NormalUpThreshold)
                {
                    continue;   // 侧面 / 岛底，不碰
                }

                Vector3 world = toWorld.MultiplyPoint3x4(vertices[i]) * finalScale;
                if (Vector2.Distance(new Vector2(world.x, world.z), center) > keepRadius)
                {
                    continue;   // 外缘留野
                }

                float target = Nearest(layers, world.y);
                if (Mathf.Abs(world.y - target) > SnapBand)
                {
                    continue;   // 斜坡本身，保留为层间过渡
                }

                world.y = target;
                vertices[i] = toLocal.MultiplyPoint3x4(world / finalScale);
                // 压平后这一片就是水平面，法线直接给 up；重算整张网格的法线会把低模的硬边磨圆
                normals[i] = localUp;
                moved++;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return moved;
        }

        private static float Nearest(List<float> layers, float value)
        {
            float best = layers[0];
            float bestDistance = Mathf.Abs(value - best);
            for (int i = 1; i < layers.Count; i++)
            {
                float distance = Mathf.Abs(value - layers[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = layers[i];
                }
            }
            return best;
        }

        private static bool TryGetWorldBounds(MeshFilter[] filters, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;

            foreach (MeshFilter filter in filters)
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
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

            return any;
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
    }
}
