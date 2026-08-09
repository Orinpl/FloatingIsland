using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>
    /// 把地图元素按配表数量区间散布到已刷好的地形上（PROJECT_BUILD §3：元素是生成期写入的静态数据）。
    ///
    /// 确定性：同一张地形快照 + 同一个种子 → 同一份元素布局。地图是编辑期产物，
    /// 散布结果要能复现，否则每次打开工程刷出来的图都不一样，没法调数值也没法回归。
    ///
    /// 散布约束（GAME_DESIGN §5）：
    /// - 只落在普通空岛（island）地形上——绿地留给农田、浮空区域留给船坞；
    /// - 占地整块要在已刷地形内，且互不重叠；
    /// - 巨型风车唯一且优先靠近地图中心（它是初始核心建筑，§5）；
    /// - 元素之间留出最小间距，避免几个锚点挤成一堆让范围加成退化成一个点。
    /// </summary>
    public static class MapElementScatter
    {
        /// <summary>元素锚点之间的最小中心距（格）。纯生成期手感参数，不进配表。</summary>
        private const int MinSpacing = 6;

        /// <summary>
        /// 生成元素布局。返回的列表可直接塞进 <see cref="MapSnapshot"/> 的 elements 参数。
        /// </summary>
        /// <param name="terrain">已刷好地形的快照（其自带的 elements 会被忽略，本方法是重新散布）。</param>
        /// <param name="rules">规则集，提供各元素的占地与数量区间。</param>
        /// <param name="seed">随机种子。</param>
        public static List<MapElementPlacement> Scatter(MapSnapshot terrain, BuildRuleSet rules, int seed)
        {
            if (terrain == null)
            {
                throw new ArgumentNullException(nameof(terrain));
            }
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var random = new DeterministicRandom(seed);
            var result = new List<MapElementPlacement>();
            var takenCells = new HashSet<long>();
            var anchors = new List<CellCoord>();

            // 候选锚点格：所有刷成 island 的地块，按 (z, x) 稳定排序后再洗牌，保证与快照顺序无关
            List<CellCoord> candidates = CollectIslandCells(terrain);
            if (candidates.Count == 0)
            {
                return result;
            }

            // 巨型风车先放，且要在岛中心附近——它是全局加成源，落在边角会让大半张图吃不到
            foreach (MapElementDef def in OrderedElements(rules))
            {
                int count = RollCount(def, random);
                if (count <= 0)
                {
                    continue;
                }

                bool preferCenter = string.Equals(def.ElementId, BuildRuleSet.GiantWindmillElementId, StringComparison.Ordinal);
                List<CellCoord> order = preferCenter
                    ? SortByDistanceToCentroid(candidates)
                    : Shuffle(candidates, random);

                int placed = 0;
                for (int i = 0; i < order.Count && placed < count; i++)
                {
                    CellCoord origin = order[i];
                    if (!TryFit(terrain, def, origin, takenCells, anchors, out List<CellCoord> cells))
                    {
                        continue;
                    }

                    result.Add(new MapElementPlacement(def.ElementId, origin.X, origin.Z, 0, Rotation.Deg0));
                    anchors.Add(origin);
                    for (int c = 0; c < cells.Count; c++)
                    {
                        takenCells.Add(CellKey(cells[c]));
                    }
                    placed++;
                }
            }

            return result;
        }

        /// <summary>参与散布的元素：非地形、数量上限为正。巨型风车排最前（占地最大且要占中心）。</summary>
        private static List<MapElementDef> OrderedElements(BuildRuleSet rules)
        {
            var result = new List<MapElementDef>();
            for (int i = 0; i < rules.Elements.Count; i++)
            {
                MapElementDef def = rules.Elements[i];
                if (def.IsTerrain || def.CountMax <= 0)
                {
                    continue;
                }
                result.Add(def);
            }

            result.Sort((a, b) =>
            {
                bool aGiant = string.Equals(a.ElementId, BuildRuleSet.GiantWindmillElementId, StringComparison.Ordinal);
                bool bGiant = string.Equals(b.ElementId, BuildRuleSet.GiantWindmillElementId, StringComparison.Ordinal);
                if (aGiant != bGiant)
                {
                    return aGiant ? -1 : 1;
                }
                // 占地大的先放：小元素能见缝插针，大元素后放容易找不到位置
                int byArea = b.Footprint.CellCount.CompareTo(a.Footprint.CellCount);
                return byArea != 0 ? byArea : string.CompareOrdinal(a.ElementId, b.ElementId);
            });

            return result;
        }

        private static int RollCount(MapElementDef def, DeterministicRandom random)
        {
            int min = Math.Max(0, def.CountMin);
            int max = Math.Max(min, def.CountMax);
            return min == max ? min : random.NextInt(min, max + 1);
        }

        private static List<CellCoord> CollectIslandCells(MapSnapshot terrain)
        {
            var cells = new List<CellCoord>();
            for (int i = 0; i < terrain.Cells.Count; i++)
            {
                MapCell cell = terrain.Cells[i];
                if (cell.Layer != 0)
                {
                    continue;
                }
                if (string.Equals(cell.ElementId, "island", StringComparison.Ordinal))
                {
                    cells.Add(new CellCoord(cell.X, cell.Z));
                }
            }
            cells.Sort((a, b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X));
            return cells;
        }

        /// <summary>元素占地能否整块落在 island 地形上、且不与已放元素重叠或过近。</summary>
        private static bool TryFit(
            MapSnapshot terrain, MapElementDef def, CellCoord origin,
            HashSet<long> takenCells, List<CellCoord> anchors, out List<CellCoord> cells)
        {
            cells = new List<CellCoord>(def.Footprint.CellCount);
            def.Footprint.GetCells(origin.X, origin.Z, Rotation.Deg0, cells);

            for (int i = 0; i < cells.Count; i++)
            {
                CellCoord cell = cells[i];
                if (!string.Equals(terrain.GetElementIdOrNull(cell.X, cell.Z, 0), "island", StringComparison.Ordinal))
                {
                    return false;
                }
                if (takenCells.Contains(CellKey(cell)))
                {
                    return false;
                }
            }

            for (int i = 0; i < anchors.Count; i++)
            {
                int dx = anchors[i].X - origin.X;
                int dz = anchors[i].Z - origin.Z;
                if (dx * dx + dz * dz < MinSpacing * MinSpacing)
                {
                    return false;
                }
            }

            return true;
        }

        private static List<CellCoord> SortByDistanceToCentroid(List<CellCoord> cells)
        {
            long sumX = 0;
            long sumZ = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                sumX += cells[i].X;
                sumZ += cells[i].Z;
            }
            int cx = (int)(sumX / cells.Count);
            int cz = (int)(sumZ / cells.Count);

            var sorted = new List<CellCoord>(cells);
            sorted.Sort((a, b) =>
            {
                int da = (a.X - cx) * (a.X - cx) + (a.Z - cz) * (a.Z - cz);
                int db = (b.X - cx) * (b.X - cx) + (b.Z - cz) * (b.Z - cz);
                if (da != db)
                {
                    return da.CompareTo(db);
                }
                return a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
            });
            return sorted;
        }

        private static List<CellCoord> Shuffle(List<CellCoord> cells, DeterministicRandom random)
        {
            var copy = new List<CellCoord>(cells);
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = random.NextInt(0, i + 1);
                CellCoord tmp = copy[i];
                copy[i] = copy[j];
                copy[j] = tmp;
            }
            return copy;
        }

        private static long CellKey(CellCoord cell)
        {
            return ((long)cell.Z << 32) ^ (uint)cell.X;
        }
    }

    /// <summary>
    /// 确定性伪随机（xorshift32）。不用 System.Random：它的实现在不同 .NET 版本间变过，
    /// 而地图生成要求「同种子同结果」跨版本成立。
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(int seed)
        {
            // 0 是 xorshift 的不动点，换成任意非零常量
            _state = seed == 0 ? 0x9E3779B9u : (uint)seed;
        }

        /// <summary>下一个非负随机数。</summary>
        public uint NextUInt()
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return _state;
        }

        /// <summary>[minInclusive, maxExclusive) 区间的随机整数。</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }
            uint range = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % range);
        }
    }
}
