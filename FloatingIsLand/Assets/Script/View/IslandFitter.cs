using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 关卡岛屿模型的入场对位：缩放到设计跨度、居中到地图中心、把**可建造平台面**对到网格平面。
    ///
    /// 编辑器（描摹地形）与运行时（生成表现）**必须共用这一份**。两边各写一遍必然漂移，
    /// 症状是「刷出来的地形和玩家看到的岛错开」，和 <see cref="GridGeometry"/> 那条教训一模一样。
    ///
    /// 为什么不能直接把包围盒顶面对到 y=0：实测 stage_01 缩放后是 80×55.8×79.3 m，
    /// 但顶面高度的 10%~90% 分位都落在 −8.4~−5.7 m —— 也就是说岛的主体是一块平顶，
    /// 顶上另有一个只占约 1% 格数的小尖峰。按包围盒顶面对齐等于按尖峰对齐，
    /// 整块平台会沉到网格平面以下 7 米，描摹只描得到山尖。
    ///
    /// 对齐基准取**顶面高度的众数**（分布最密的那一档），不是中位数。岛面经
    /// IslandSurfaceFlattener 分层压平后是多个台地，高度分布是多峰的；中位数在多峰分布上
    /// 可能正好落进两层之间的空档——实测 stage_02 的中位面附近只有 1% 的格子，
    /// 按它对齐会把整块主台地推偏几米，建筑全部浮空。众数则一定落在某个真实台地上。
    /// </summary>
    public static class IslandFitter
    {
        /// <summary>平台面相对网格平面的高度（略低一点，避免与地形 overlay Z-fighting）。</summary>
        public const float SurfaceY = -0.05f;

        /// <summary>
        /// 可建造平台的高度容差（米）。
        ///
        /// 从前是 max(4m, 岛高×8%)——stage_01 算出来 4.46m，等于把主台地、上一层台地和它们之间的
        /// 斜坡**全部**算成同一块可建造面，98% 的命中格都算可建造，建筑于是会陷进坡里。
        /// 岛面压平之后主台地是数学平整的（实测顶面 20%~80% 分位跨度 0.1m），容差就该收到这个量级：
        /// 比压平精度略宽，又远小于最小层间距（1.5m）的一半，才不会把邻层吃进来。
        /// </summary>
        private const float SurfaceBandMeters = 0.35f;

        /// <summary>找主台地面用的高度分箱宽度（米）。</summary>
        private const float SurfaceBinSize = 0.25f;

        /// <summary>射线缓冲：RaycastNonAlloc 结果无序且会被长度截断，太小可能丢掉最高面。</summary>
        private const int HitBufferSize = 32;

        /// <summary>
        /// 缩放 + 居中 + 对齐平台面，并返回每格的顶面世界高度（没命中岛的格不在字典里）。
        /// 运行时只需要副作用（transform 被摆好），编辑器还要拿这份高度图去刷地形。
        /// </summary>
        /// <param name="island">已实例化的岛屿模型（需带 MeshCollider 才能测高）。</param>
        /// <param name="geometry">网格几何。</param>
        /// <param name="cellSpan">缩放后 XZ 长边占多少格（配表 Stage.islandCellSpan）。</param>
        /// <returns>格坐标 (x, z, layer=0) → 顶面世界高度；测不到时为空字典。</returns>
        public static Dictionary<Vector3Int, float> Fit(GameObject island, GridGeometry geometry, int cellSpan)
        {
            var heights = new Dictionary<Vector3Int, float>();
            Bounds bounds;
            if (!TryGetWorldBounds(island, out bounds))
            {
                Debug.LogWarning("[岛屿对位] 模型没有任何 Renderer，无法对位。", island);
                return heights;
            }

            float longestSide = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longestSide <= Mathf.Epsilon)
            {
                Debug.LogWarning("[岛屿对位] 模型 XZ 包围盒为零，无法对位。", island);
                return heights;
            }

            int span = cellSpan > 0 ? cellSpan : 40;
            island.transform.localScale *= span * geometry.CellSize / longestSide;

            // 缩放改变了包围盒，重新取一次再居中
            if (!TryGetWorldBounds(island, out bounds))
            {
                return heights;
            }
            Vector3 mapCenter = geometry.CellCorner(geometry.Width / 2, geometry.Length / 2, 0);
            island.transform.position += new Vector3(
                mapCenter.x - bounds.center.x,
                0f,
                mapCenter.z - bounds.center.z);

            // 先量一遍高度找中位面，再整体升降到位；量高要求碰撞体的位置是最新的
            Physics.SyncTransforms();
            TraceTopHeights(island, geometry, heights);
            if (heights.Count == 0)
            {
                Debug.LogWarning("[岛屿对位] 一格都没测到高度——模型上没有 MeshCollider？" +
                                 "（Tools/美术/生成白模 Prefab 会给关卡岛屿自动补）", island);
                return heights;
            }

            float mainSurface = MainSurface(heights);
            float shift = SurfaceY - mainSurface;
            island.transform.position += new Vector3(0f, shift, 0f);
            Physics.SyncTransforms();

            // 高度图跟着整体平移，省掉再打一遍射线
            var shifted = new Dictionary<Vector3Int, float>(heights.Count);
            foreach (KeyValuePair<Vector3Int, float> kv in heights)
            {
                shifted[kv.Key] = kv.Value + shift;
            }
            return shifted;
        }

        /// <summary>可建造平台的高度容差：与主台地面相差在此之内的格子算平台，其余是邻层、尖峰或陡坡。</summary>
        public static float SurfaceBand(GameObject island)
        {
            return SurfaceBandMeters;
        }

        /// <summary>该格是否属于可建造平台面。</summary>
        public static bool IsOnSurface(float topY, float band)
        {
            return Mathf.Abs(topY - SurfaceY) <= band;
        }

        /// <summary>逐格从上往下打射线，记录命中该模型的最高点世界高度。</summary>
        private static void TraceTopHeights(GameObject island, GridGeometry geometry, Dictionary<Vector3Int, float> heights)
        {
            Bounds bounds;
            if (!TryGetWorldBounds(island, out bounds))
            {
                return;
            }

            // 只扫岛的包围盒覆盖的格子，250×250 全扫是 6 万次射线的无谓开销
            int minX, minZ, maxX, maxZ;
            geometry.WorldToCellUnclamped(bounds.min, 0, out minX, out minZ);
            geometry.WorldToCellUnclamped(bounds.max, 0, out maxX, out maxZ);
            minX = Mathf.Max(0, minX - 1);
            minZ = Mathf.Max(0, minZ - 1);
            maxX = Mathf.Min(geometry.Width - 1, maxX + 1);
            maxZ = Mathf.Min(geometry.Length - 1, maxZ + 1);

            float rayStartY = bounds.max.y + 10f;
            float rayLength = bounds.size.y + 20f;
            var hits = new RaycastHit[HitBufferSize];
            Transform islandTransform = island.transform;

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
                        Transform hit = hits[i].transform;
                        // 场景里可能还有别的碰撞体，只认这棵岛
                        if (hit != islandTransform && !hit.IsChildOf(islandTransform))
                        {
                            continue;
                        }
                        if (hits[i].point.y > topY)
                        {
                            topY = hits[i].point.y;
                        }
                    }

                    if (!float.IsNegativeInfinity(topY))
                    {
                        heights[new Vector3Int(x, z, 0)] = topY;
                    }
                }
            }
        }

        /// <summary>
        /// 主台地面的高度：一维直方图里格子最多的那一档，再用档内中位数精修。
        ///
        /// 用众数而不是中位数的理由见类注释——压平后的岛面是多峰分布，中位数可能落在层间空档。
        /// 精修一步是因为峰值只精确到箱宽，直接拿箱心当平台面会有半个箱宽的系统偏差。
        /// </summary>
        private static float MainSurface(Dictionary<Vector3Int, float> heights)
        {
            var values = new List<float>(heights.Count);
            foreach (KeyValuePair<Vector3Int, float> kv in heights)
            {
                values.Add(kv.Value);
            }
            values.Sort();

            float min = values[0];
            float max = values[values.Count - 1];
            int binCount = Mathf.Max(1, Mathf.CeilToInt((max - min) / SurfaceBinSize) + 1);
            var bins = new int[binCount];
            for (int i = 0; i < values.Count; i++)
            {
                bins[Mathf.Clamp((int)((values[i] - min) / SurfaceBinSize), 0, binCount - 1)]++;
            }

            int bestBin = 0;
            for (int i = 1; i < binCount; i++)
            {
                if (bins[i] > bins[bestBin])
                {
                    bestBin = i;
                }
            }

            float binMin = min + bestBin * SurfaceBinSize;
            var inBin = new List<float>(bins[bestBin]);
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] >= binMin && values[i] < binMin + SurfaceBinSize)
                {
                    inBin.Add(values[i]);
                }
            }
            return inBin.Count > 0 ? inBin[inBin.Count / 2] : values[values.Count / 2];
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
