using System;
using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Wind
{
    /// <summary>
    /// 风系统的领域入口：持有风源与当前风场，负责「风被改变 → 全量重算 → 广播」这条链
    /// （WIND_IMPL §6 调用时序）。自身实现 <see cref="IWindField"/> 并转发到当前
    /// <see cref="Field"/>，这样 <see cref="BuildBoard.WindField"/> 只需挂一次，
    /// 重算后不会持有过期引用。
    ///
    /// 风源在构造时从地图的 windSource 元素按局种子确定性展开（同种子同风）。
    /// </summary>
    public sealed class WindSystem : IWindField, IWindBuildingQuery
    {
        /// <summary>风源随机流的盐值：与地图元素散布的随机流解耦，互不偷数。</summary>
        private const int SeedSalt = 0x57494E44; // "WIND"

        private readonly BuildBoard _board;
        private readonly List<WindSeed> _seeds;

        /// <summary>当前风场（不可变对象，重算即整体替换）。</summary>
        public WindField Field { get; private set; }

        /// <summary>全局风变更事件：每次重算完成后广播，载荷是新风场。</summary>
        public event Action<WindField> FieldChanged;

        public WindSystem(BuildBoard board, int seed)
        {
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _seeds = BuildSeeds(board, seed);
            Field = Simulate(this);
        }

        /// <summary>展开的风源（调试/测试用）。</summary>
        public IReadOnlyList<WindSeed> Seeds
        {
            get { return _seeds; }
        }

        /// <summary>这栋建筑会不会改变风（= 需要落地后重算）。</summary>
        public static bool AffectsWind(BuildingBlueprint blueprint)
        {
            if (blueprint == null)
            {
                return false;
            }
            return string.Equals(blueprint.BuildingId, BuildRuleSet.SailBuildingId, StringComparison.Ordinal)
                   || string.Equals(blueprint.BuildingId, BuildRuleSet.LogisticsPointBuildingId, StringComparison.Ordinal);
        }

        /// <summary>
        /// 风帆的转向设定从摆放朝向派生：偶数朝向（0°/180°）= 右转，奇数（90°/270°）= 左转。
        /// 1×1 的风帆本不需要朝向，把已有的滚轮旋转链路复用成左右切换，UI 零新增。
        /// </summary>
        public static TurnDir TurnFromRotation(Rotation rotation)
        {
            return ((int)rotation & 1) == 0 ? TurnDir.Right : TurnDir.Left;
        }

        /// <summary>风被改变后的全量重算 + 广播（放风帆/物流点后由 App 层调用）。</summary>
        public void Recompute()
        {
            Field = Simulate(this);
            FieldChanged?.Invoke(Field);
        }

        /// <summary>
        /// 摆放预览：假设把这栋摆在这里，风场会变成什么样。纯干跑，不改任何状态。
        /// 不影响风的建筑返回 null（调用方据此跳过预览渲染）。
        /// </summary>
        public WindField Preview(BuildingBlueprint blueprint, int x, int z, int layer, Rotation rotation)
        {
            if (!AffectsWind(blueprint))
            {
                return null;
            }

            var cells = new List<CellCoord>(blueprint.Footprint.CellCount);
            blueprint.Footprint.GetCells(x, z, rotation, cells);
            var overlay = new OverlayQuery(this, blueprint, cells, layer, rotation);
            return Simulate(overlay);
        }

        private WindField Simulate(IWindBuildingQuery query)
        {
            return WindSimulator.Recompute(_board.Map, _seeds, query, _board.Rules);
        }

        // ---------- 风源展开 ----------

        /// <summary>
        /// 每个 windSource 元素展开一股：方向八向随机，强度/长度取 WindConfig 区间。
        /// 顺序按元素落位序，随机流独立加盐——保证「同种子同风」且与散布逻辑互不干扰。
        /// </summary>
        private static List<WindSeed> BuildSeeds(BuildBoard board, int seed)
        {
            var seeds = new List<WindSeed>(4);
            var random = new DeterministicRandom(seed ^ SeedSalt);
            BuildRuleSet rules = board.Rules;

            for (int i = 0; i < board.Elements.Count; i++)
            {
                PlacedElement element = board.Elements[i];
                if (!string.Equals(element.Def.ElementId, BuildRuleSet.WindSourceElementId, StringComparison.Ordinal))
                {
                    continue;
                }

                var dir = (Dir8)random.NextInt(0, 8);
                int force = random.NextInt(rules.InitialWindLevelMin, rules.InitialWindLevelMax + 1);
                int length = random.NextInt(rules.InitialWindLengthMin, rules.InitialWindLengthMax + 1);
                if (force <= 0 || length <= 0)
                {
                    continue;
                }
                seeds.Add(new WindSeed(element.Cells[0], element.Layer, dir, force, length));
            }
            return seeds;
        }

        // ---------- IWindField（转发到当前场） ----------

        public int GetForce(int x, int z, int layer)
        {
            return Field.GetForce(x, z, layer);
        }

        public bool HasLogisticsWindNear(IReadOnlyList<CellCoord> cells, int layer, int radius, float layerHeightFactor)
        {
            return Field.HasLogisticsWindNear(cells, layer, radius, layerHeightFactor);
        }

        // ---------- IWindBuildingQuery（真实盘面） ----------

        public bool TryGetSailTurn(int x, int z, int layer, out TurnDir turn)
        {
            PlacedBuilding building = _board.GetBuildingAt(x, z, layer);
            if (building != null
                && string.Equals(building.Blueprint.BuildingId, BuildRuleSet.SailBuildingId, StringComparison.Ordinal))
            {
                turn = TurnFromRotation(building.Rotation);
                return true;
            }
            turn = TurnDir.Left;
            return false;
        }

        public bool TryGetLogisticsPoint(int x, int z, int layer, out int pointId)
        {
            PlacedBuilding building = _board.GetBuildingAt(x, z, layer);
            if (building != null
                && string.Equals(building.Blueprint.BuildingId, BuildRuleSet.LogisticsPointBuildingId, StringComparison.Ordinal))
            {
                pointId = building.Id;
                return true;
            }
            pointId = 0;
            return false;
        }

        /// <summary>预览用叠加查询：真实盘面 + 一栋假设建筑。</summary>
        private sealed class OverlayQuery : IWindBuildingQuery
        {
            private readonly WindSystem _base;
            private readonly BuildingBlueprint _blueprint;
            private readonly List<CellCoord> _cells;
            private readonly int _layer;
            private readonly Rotation _rotation;

            public OverlayQuery(WindSystem baseQuery, BuildingBlueprint blueprint, List<CellCoord> cells, int layer, Rotation rotation)
            {
                _base = baseQuery;
                _blueprint = blueprint;
                _cells = cells;
                _layer = layer;
                _rotation = rotation;
            }

            private bool Covers(int x, int z, int layer)
            {
                if (layer != _layer)
                {
                    return false;
                }
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (_cells[i].X == x && _cells[i].Z == z)
                    {
                        return true;
                    }
                }
                return false;
            }

            public bool TryGetSailTurn(int x, int z, int layer, out TurnDir turn)
            {
                if (string.Equals(_blueprint.BuildingId, BuildRuleSet.SailBuildingId, StringComparison.Ordinal)
                    && Covers(x, z, layer))
                {
                    turn = TurnFromRotation(_rotation);
                    return true;
                }
                return _base.TryGetSailTurn(x, z, layer, out turn);
            }

            public bool TryGetLogisticsPoint(int x, int z, int layer, out int pointId)
            {
                if (string.Equals(_blueprint.BuildingId, BuildRuleSet.LogisticsPointBuildingId, StringComparison.Ordinal)
                    && Covers(x, z, layer))
                {
                    // 假设建筑还没有实例 Id，用保留值 -1——它的所有占用格必须是同一个点
                    pointId = -1;
                    return true;
                }
                return _base.TryGetLogisticsPoint(x, z, layer, out pointId);
            }
        }
    }
}
