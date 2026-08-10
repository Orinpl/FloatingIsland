using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Wind
{
    /// <summary>
    /// 传播时风股能「看见」的建筑查询口。只认两类会影响风的建筑：
    /// 风帆（转向）与物流点（延长 + 物流风标记）。摆放预览用叠加实现（假设多摆了一栋）。
    /// </summary>
    public interface IWindBuildingQuery
    {
        /// <summary>该格有风帆则给出其转向设定。</summary>
        bool TryGetSailTurn(int x, int z, int layer, out TurnDir turn);

        /// <summary>
        /// 该格被物流点占用时给出该点的实例标识。「同点不重复延长」按建筑判定而不是按格判定——
        /// 物流点占地 2×2，一股风连穿它两格仍只能算同一个点（预览的假设建筑用保留值 -1）。
        /// </summary>
        bool TryGetLogisticsPoint(int x, int z, int layer, out int pointId);
    }

    /// <summary>
    /// 风场重算（WIND_IMPL §3 两遍扫描）。纯函数：地图 + 风源 + 建筑查询进，风场出，
    /// 不改任何输入状态——预览就是对「假设放了这栋」的查询叠加再跑一次。
    ///
    /// Pass 1 逐股独立传播（互相不可见）；Pass 2 逐格叠加合成。
    /// 结果与股序无关（合成为纯函数），全量重算的复杂度 = 各股路径格数之和 + 有风格子数。
    /// </summary>
    public static class WindSimulator
    {
        /// <summary>重算整场。宽一格、只走八向；斜向一步只经过对角两格（不扫两侧），一步同价消耗 1 段风长。</summary>
        public static WindField Recompute(
            MapSnapshot map, IReadOnlyList<WindSeed> seeds, IWindBuildingQuery buildings, BuildRuleSet rules)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var streams = new List<WindStream>(seeds != null ? seeds.Count : 0);
            if (seeds != null)
            {
                for (int i = 0; i < seeds.Count; i++)
                {
                    streams.Add(Propagate(i + 1, seeds[i], map, buildings, rules));
                }
            }

            return ComposeCells(map, streams, rules);
        }

        // ---------- Pass 1：逐股独立传播 ----------

        private static WindStream Propagate(
            int id, WindSeed seed, MapSnapshot map, IWindBuildingQuery buildings, BuildRuleSet rules)
        {
            var path = new List<WindPathStep>(seed.Length + 1);
            var turns = new List<TurnPoint>(2);
            var mods = new List<LengthModifier>(2);
            var extendedPointIds = new List<int>(2);
            int logisticsFrom = -1;

            Dir8 dir = seed.Dir;
            int x = seed.Origin.X;
            int z = seed.Origin.Z;
            int layer = seed.Layer;
            int remaining = seed.Length;
            WindEndReason endReason;

            while (true)
            {
                // 处理本格的建筑影响（风帆与物流点不可能同格——一格只有一栋建筑）
                TurnDir turn;
                int pointId;
                if (buildings != null && buildings.TryGetSailTurn(x, z, layer, out turn))
                {
                    // 每次经过都转向，不限次数（决策 24）；不恢复风长
                    dir = WindMath.TurnedBy(dir, turn);
                    turns.Add(new TurnPoint(new CellCoord(x, z), turn));
                }
                else if (buildings != null && buildings.TryGetLogisticsPoint(x, z, layer, out pointId))
                {
                    if (logisticsFrom < 0)
                    {
                        // 「经过后」变物流风：物流点的下一格起携带物流效果（§10.4）
                        logisticsFrom = path.Count + 1;
                    }

                    // 账本两条限制（§10.5）：同点不重复、每股最多 windExtendMaxPerWind 次。
                    // 「同点」按建筑实例判定：2×2 的物流点被连穿两格仍是同一个点，
                    // 按格判定会让它延长两次并吃掉全部名额。账本上仍记触发格坐标。
                    bool samePoint = false;
                    for (int i = 0; i < extendedPointIds.Count; i++)
                    {
                        if (extendedPointIds[i] == pointId)
                        {
                            samePoint = true;
                            break;
                        }
                    }
                    if (!samePoint && mods.Count < rules.WindExtendMaxPerWind && rules.WindExtendLength > 0)
                    {
                        remaining += rules.WindExtendLength;
                        mods.Add(new LengthModifier(
                            new CellCoord(x, z), LengthModifierKind.LogisticsExtend, rules.WindExtendLength));
                        extendedPointIds.Add(pointId);
                    }
                }

                // 拐点格记录的是转向后的出向
                path.Add(new WindPathStep(new CellCoord(x, z), dir));

                if (remaining <= 0)
                {
                    endReason = WindEndReason.LengthExhausted;
                    break;
                }

                int nx = x + WindMath.StepX[(int)dir];
                int nz = z + WindMath.StepZ[(int)dir];
                if (nx < 0 || nx >= map.Width || nz < 0 || nz >= map.Length)
                {
                    // 地形不阻挡风（可越过虚空），仅出网格边界终止（决策 9）
                    endReason = WindEndReason.OutOfMap;
                    break;
                }

                x = nx;
                z = nz;
                remaining--;
            }

            // 首个物流点恰好是路径最后一格（如贴边出图）时，logisticsFrom 会指向路径末尾之外——
            // 此时股上没有任何格真正携带物流风，按「-1 = 从未」的字段契约归位
            if (logisticsFrom >= path.Count)
            {
                logisticsFrom = -1;
            }

            return new WindStream(
                id, seed.Origin, layer, seed.Dir, seed.Force, seed.Length,
                path, turns, mods, path[path.Count - 1].Cell, endReason, logisticsFrom);
        }

        // ---------- Pass 2：逐格叠加合成 ----------

        private static WindField ComposeCells(MapSnapshot map, List<WindStream> streams, BuildRuleSet rules)
        {
            var passesByCell = new Dictionary<long, List<WindPass>>();
            var logisticsByLayer = new Dictionary<int, List<CellCoord>>();
            var logisticsSeen = new HashSet<long>();

            for (int s = 0; s < streams.Count; s++)
            {
                WindStream stream = streams[s];
                for (int i = 0; i < stream.Path.Count; i++)
                {
                    WindPathStep step = stream.Path[i];
                    bool isLogistics = stream.IsLogisticsAt(i);
                    long key = Key(map, step.Cell.X, step.Cell.Z, stream.Layer);

                    List<WindPass> passes;
                    if (!passesByCell.TryGetValue(key, out passes))
                    {
                        passes = new List<WindPass>(2);
                        passesByCell.Add(key, passes);
                    }
                    passes.Add(new WindPass(stream.Id, i, step.DirOut, stream.Force, isLogistics));

                    if (isLogistics && logisticsSeen.Add(key))
                    {
                        List<CellCoord> layerCells;
                        if (!logisticsByLayer.TryGetValue(stream.Layer, out layerCells))
                        {
                            layerCells = new List<CellCoord>(16);
                            logisticsByLayer.Add(stream.Layer, layerCells);
                        }
                        layerCells.Add(step.Cell);
                    }
                }
            }

            var cells = new Dictionary<long, WindCellState>(passesByCell.Count);
            foreach (KeyValuePair<long, List<WindPass>> pair in passesByCell)
            {
                List<WindPass> passes = pair.Value;
                Dir8 resultDir;
                int resultForce;
                WindMath.Compose(passes, rules.MaxWindLevel, out resultDir, out resultForce);

                bool anyLogistics = false;
                for (int i = 0; i < passes.Count; i++)
                {
                    if (passes[i].IsLogistics)
                    {
                        anyLogistics = true;
                        break;
                    }
                }

                cells.Add(pair.Key, new WindCellState(passes, resultDir, resultForce, anyLogistics));
            }

            return new WindField(map.Width, map.Length, streams, cells, logisticsByLayer);
        }

        private static long Key(MapSnapshot map, int x, int z, int layer)
        {
            return ((long)layer * map.Length + z) * map.Width + x;
        }
    }
}
