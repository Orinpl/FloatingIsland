using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Wind
{
    /// <summary>一笔风变更结算的类型。</summary>
    public enum WindAwardKind
    {
        /// <summary>物流风把覆盖送到了一栋此前从未接入物流的建筑（§10.4）。</summary>
        LogisticsCoverage = 0,

        /// <summary>两个物流点经物流风连通（额外特效 + 额外加分）。</summary>
        LogisticsLink = 1,
    }

    /// <summary>
    /// 风变更时新产生的一笔一次性得分。表现层据此打辉光/飘字/画互联特效；
    /// 分值已计入总分，之后风再变也不回收（「得分仅在改变那一次计算」）。
    /// </summary>
    public sealed class WindAward
    {
        public WindAwardKind Kind { get; }

        /// <summary>本笔分值。</summary>
        public int Score { get; }

        /// <summary>中文说明（飘字直接用）。</summary>
        public string Label { get; }

        /// <summary>受益建筑实例 Id（覆盖 = 被覆盖的建筑；互联 = 上游物流点）。</summary>
        public int BuildingInstanceId { get; }

        /// <summary>互联时的下游物流点实例 Id；覆盖时为 0。</summary>
        public int OtherInstanceId { get; }

        /// <summary>互联时两点之间的风路径格（画连线特效用）；覆盖时为空。</summary>
        public IReadOnlyList<CellCoord> LinkPathCells { get; }

        /// <summary>路径所在层。</summary>
        public int Layer { get; }

        public WindAward(
            WindAwardKind kind, int score, string label,
            int buildingInstanceId, int otherInstanceId,
            IReadOnlyList<CellCoord> linkPathCells, int layer)
        {
            Kind = kind;
            Score = score;
            Label = label;
            BuildingInstanceId = buildingInstanceId;
            OtherInstanceId = otherInstanceId;
            LinkPathCells = linkPathCells ?? Array.Empty<CellCoord>();
            Layer = layer;
        }
    }

    /// <summary>
    /// 风变更的一次性计分账本。核心语义（用户定稿）：
    ///
    /// - 建筑的风相关分只在「变化那一刻」结算一次：建造时吃当时的风；之后风变了，
    ///   已得的分**不回收**——本类只发新分，从不扣分。
    /// - 风变更能给已落地建筑发的分只有物流风通道：物流风覆盖分（每建筑一生一次，
    ///   与建造时的物流点本地覆盖同状态不重复，§10.3/10.4）与物流点互联分（每对一生一次）。
    /// - 互联按风路径上的物流点链式相邻配对：一股风依次经过 A、B、C，连 A-B 与 B-C。
    /// </summary>
    public sealed class WindScoreKeeper
    {
        /// <summary>已拿过「接入物流」覆盖分的建筑实例（含建造时经本地覆盖拿分的）。</summary>
        private readonly HashSet<int> _coverageScored = new HashSet<int>();

        /// <summary>已计分的物流点对（无序对归一化键）。</summary>
        private readonly HashSet<long> _linkScored = new HashSet<long>();

        /// <summary>建造结算里已吃到覆盖分的建筑在这里登记，风变更时不再重复发。</summary>
        public void MarkCoveredAtBuild(int buildingInstanceId)
        {
            _coverageScored.Add(buildingInstanceId);
        }

        /// <summary>
        /// 风变更后结算新产生的分。只发增量：已登记过的建筑/点对直接跳过。
        /// 返回的列表可能为空（风变了但没吹出任何新收益）。
        /// </summary>
        public List<WindAward> SettleAfterWindChange(BuildBoard board, WindField field)
        {
            return Compute(board, field, commit: true, virtualPoint: null);
        }

        /// <summary>
        /// 摆放预览的干跑（§7.4「预估得分」）：算出「假设摆了这栋、风变成 <paramref name="field"/>」
        /// 会发出哪些一次性分，**不写入任何账本**——预览一万次也不会把一生一次的额度用掉。
        /// <paramref name="virtualPoint"/>：正在摆的假设物流点（Id 用 -1），它要参与互联配对，
        /// 否则「新点连上老点」这最常见的一笔在预览里看不见；摆的不是物流点时传 null。
        /// </summary>
        public List<WindAward> PreviewWindChange(BuildBoard board, WindField field, PlacedBuilding virtualPoint)
        {
            return Compute(board, field, commit: false, virtualPoint: virtualPoint);
        }

        private List<WindAward> Compute(BuildBoard board, WindField field, bool commit, PlacedBuilding virtualPoint)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }

            var awards = new List<WindAward>();
            SettleCoverage(board, field, commit, awards);
            SettleLinks(board, field, commit, virtualPoint, awards);
            return awards;
        }

        /// <summary>本批结算的总分。</summary>
        public static int TotalOf(List<WindAward> awards)
        {
            int total = 0;
            for (int i = 0; i < awards.Count; i++)
            {
                total += awards[i].Score;
            }
            return total;
        }

        // ---------- 物流风覆盖 ----------

        private void SettleCoverage(BuildBoard board, WindField field, bool commit, List<WindAward> awards)
        {
            BuildRuleSet rules = board.Rules;
            if (rules.LogisticsBaseCoverScore == 0 || rules.LogisticsCoverRadius <= 0)
            {
                return;
            }

            for (int i = 0; i < board.Buildings.Count; i++)
            {
                PlacedBuilding building = board.Buildings[i];
                if (!building.Blueprint.CanLogisticsCover || _coverageScored.Contains(building.Id))
                {
                    continue;
                }
                if (!field.HasLogisticsWindNear(
                        building.Cells, building.Layer, rules.LogisticsCoverRadius, rules.LayerHeightFactor))
                {
                    continue;
                }

                if (commit)
                {
                    _coverageScored.Add(building.Id);
                }
                awards.Add(new WindAward(
                    WindAwardKind.LogisticsCoverage, rules.LogisticsBaseCoverScore,
                    $"{building.Blueprint.NameCn} 接入物流网络（物流风）",
                    building.Id, 0, null, building.Layer));
            }
        }

        // ---------- 物流点互联 ----------

        private void SettleLinks(
            BuildBoard board, WindField field, bool commit, PlacedBuilding virtualPoint, List<WindAward> awards)
        {
            BuildRuleSet rules = board.Rules;
            if (rules.LogisticsWindLinkScore == 0)
            {
                return;
            }

            // 干跑不写 _linkScored，用本次局部集合防同一对在多股风里被重复报出来
            var seenThisRun = new HashSet<long>();

            for (int s = 0; s < field.Streams.Count; s++)
            {
                WindStream stream = field.Streams[s];
                if (stream.LogisticsFromIndex < 0)
                {
                    continue;
                }

                PlacedBuilding previous = null;
                int previousIndex = -1;
                for (int i = 0; i < stream.Path.Count; i++)
                {
                    CellCoord cell = stream.Path[i].Cell;
                    PlacedBuilding building = FindLogisticsPointAt(board, virtualPoint, cell, stream.Layer);
                    if (building == null)
                    {
                        continue;
                    }
                    if (previous != null && building.Id != previous.Id)
                    {
                        long key = PairKey(previous.Id, building.Id);
                        if (!_linkScored.Contains(key) && seenThisRun.Add(key))
                        {
                            if (commit)
                            {
                                _linkScored.Add(key);
                            }
                            awards.Add(new WindAward(
                                WindAwardKind.LogisticsLink, rules.LogisticsWindLinkScore,
                                "物流点经风互联",
                                previous.Id, building.Id,
                                SlicePath(stream, previousIndex, i), stream.Layer));
                        }
                    }
                    // 无条件更新到本次出现的下标：同一点被绕圈风非相邻地再次经过时，
                    // 配对特效截的才是「相邻两次出现之间」的直接段，而不是把绕圈全程截进去
                    previous = building;
                    previousIndex = i;
                }
            }
        }

        /// <summary>该格上的物流点：先看正在预览的假设点（Id=-1），再看盘面上的真点。</summary>
        private static PlacedBuilding FindLogisticsPointAt(
            BuildBoard board, PlacedBuilding virtualPoint, CellCoord cell, int layer)
        {
            if (virtualPoint != null && virtualPoint.Layer == layer)
            {
                for (int i = 0; i < virtualPoint.Cells.Count; i++)
                {
                    if (virtualPoint.Cells[i].X == cell.X && virtualPoint.Cells[i].Z == cell.Z)
                    {
                        return virtualPoint;
                    }
                }
            }

            PlacedBuilding building = board.GetBuildingAt(cell.X, cell.Z, layer);
            if (building != null
                && string.Equals(building.Blueprint.BuildingId, BuildRuleSet.LogisticsPointBuildingId, StringComparison.Ordinal))
            {
                return building;
            }
            return null;
        }

        /// <summary>截取两点之间的路径格（含两端），互联特效沿它画线。</summary>
        private static List<CellCoord> SlicePath(WindStream stream, int fromIndex, int toIndex)
        {
            var cells = new List<CellCoord>(toIndex - fromIndex + 1);
            for (int i = fromIndex; i <= toIndex; i++)
            {
                cells.Add(stream.Path[i].Cell);
            }
            return cells;
        }

        /// <summary>无序对 → 归一化键（小 Id 在高位）。</summary>
        private static long PairKey(int a, int b)
        {
            int low = Math.Min(a, b);
            int high = Math.Max(a, b);
            return ((long)low << 32) | (uint)high;
        }
    }
}
