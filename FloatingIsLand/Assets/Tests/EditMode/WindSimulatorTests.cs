using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 风传播与交汇合成的契约测试（WIND_IMPL §3/§4/§8 清单）。
    /// 锁死的是数据层规则：宽一格八向传播、斜向只走对角、风帆 45° 转向不改风长、
    /// 物流点账本两条限制、合成公式三条红线（单位向量 / 平局顺时针 / 先取整后封顶）。
    /// </summary>
    public sealed class WindSimulatorTests
    {
        private const string Island = "island";

        // ---------- 造测试数据 ----------

        private static MapSnapshot MakeMap(int size = 20)
        {
            var cells = new List<MapCell>(size * size);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    cells.Add(new MapCell(x, z, 0, Island));
                }
            }
            return new MapSnapshot(1, size, size, 1, cells);
        }

        private static BuildingBlueprint MakeBlueprint(string buildingId, string[] mask = null)
        {
            return new BuildingBlueprint(
                buildingId + "_01", buildingId, buildingId, "test",
                Footprint.Parse(mask ?? new[] { "#" }, buildingId),
                PlacementRule.Any, radius: 3, baseScore: 0,
                canLogisticsCover: false, isolationPenaltyScore: 0, prefabPath: "",
                elementBonus: null, bonusFrom: null, penaltyFrom: null, windScoreByLevel: null);
        }

        private static BuildRuleSet MakeRules(int extendLength = 0, int extendMax = 0, params BuildingBlueprint[] blueprints)
        {
            return new BuildRuleSet(
                blueprints, new List<MapElementDef>(),
                layerHeightFactor: 1f, giantWindmillGenericScore: 0,
                anchorDockDecayPercents: null,
                logisticsCoverRadius: 0, logisticsBaseCoverScore: 0,
                maxWindLevel: 5,
                windExtendLength: extendLength, windExtendMaxPerWind: extendMax);
        }

        /// <summary>与 WindSystem 的真实盘面查询同一口径的测试适配器。</summary>
        private sealed class BoardQuery : IWindBuildingQuery
        {
            private readonly BuildBoard _board;

            public BoardQuery(BuildBoard board)
            {
                _board = board;
            }

            public bool TryGetSailTurn(int x, int z, int layer, out TurnDir turn)
            {
                PlacedBuilding building = _board.GetBuildingAt(x, z, layer);
                if (building != null && building.Blueprint.BuildingId == BuildRuleSet.SailBuildingId)
                {
                    turn = WindSystem.TurnFromRotation(building.Rotation);
                    return true;
                }
                turn = TurnDir.Left;
                return false;
            }

            public bool TryGetLogisticsPoint(int x, int z, int layer, out int pointId)
            {
                PlacedBuilding building = _board.GetBuildingAt(x, z, layer);
                if (building != null && building.Blueprint.BuildingId == BuildRuleSet.LogisticsPointBuildingId)
                {
                    pointId = building.Id;
                    return true;
                }
                pointId = 0;
                return false;
            }
        }

        private static WindField Simulate(BuildBoard board, params WindSeed[] seeds)
        {
            return WindSimulator.Recompute(board.Map, seeds, new BoardQuery(board), board.Rules);
        }

        private static WindPass Pass(Dir8 dir, int force)
        {
            return new WindPass(1, 0, dir, force, false);
        }

        private static void AssertCompose(int maxLevel, WindPass[] passes, int expectedForce, Dir8 expectedDir)
        {
            Dir8 dir;
            int force;
            WindMath.Compose(passes, maxLevel, out dir, out force);
            Assert.AreEqual(expectedForce, force, "合成强度不符");
            if (expectedForce > 0)
            {
                Assert.AreEqual(expectedDir, dir, "合成方向不符");
            }
        }

        // ---------- 传播 ----------

        [Test]
        public void 直线传播_每步一段风长_耗尽即终止()
        {
            var board = new BuildBoard(MakeMap(), MakeRules());
            WindField field = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 3, 5));

            WindStream stream = field.Streams[0];
            Assert.AreEqual(6, stream.Path.Count, "长度 5 的风应覆盖起点 + 5 格");
            Assert.AreEqual(new CellCoord(7, 10), stream.End);
            Assert.AreEqual(WindEndReason.LengthExhausted, stream.EndReason);
            Assert.AreEqual(3, field.GetForce(4, 10, 0));
            Assert.AreEqual(0, field.GetForce(8, 10, 0), "终点之后不该有风");
        }

        [Test]
        public void 斜向传播_只经过对角两格_不影响两侧()
        {
            // 格局 12\34：东南风走 1→4 的对角线，2 与 3 不受影响
            var board = new BuildBoard(MakeMap(), MakeRules());
            WindField field = Simulate(board, new WindSeed(new CellCoord(0, 9), 0, Dir8.SE, 2, 3));

            WindStream stream = field.Streams[0];
            Assert.AreEqual(4, stream.Path.Count);
            Assert.AreEqual(new CellCoord(3, 6), stream.End);
            Assert.AreEqual(2, field.GetForce(1, 8, 0), "对角格应有风");
            Assert.AreEqual(0, field.GetForce(1, 9, 0), "侧邻格不该有风");
            Assert.AreEqual(0, field.GetForce(0, 8, 0), "侧邻格不该有风");
        }

        [Test]
        public void 出图即终止_地形不阻挡()
        {
            var board = new BuildBoard(MakeMap(), MakeRules());
            WindField field = Simulate(board, new WindSeed(new CellCoord(1, 5), 0, Dir8.W, 2, 10));

            WindStream stream = field.Streams[0];
            Assert.AreEqual(WindEndReason.OutOfMap, stream.EndReason);
            Assert.AreEqual(new CellCoord(0, 5), stream.End);
            Assert.AreEqual(2, stream.Path.Count);
        }

        [Test]
        public void 风帆转向_左右各45度_不改风长()
        {
            BuildingBlueprint sail = MakeBlueprint(BuildRuleSet.SailBuildingId);
            var boardLeft = new BuildBoard(MakeMap(), MakeRules(0, 0, sail));
            // Deg90 = 左转（TurnFromRotation 的奇偶映射）
            boardLeft.Place(sail, 6, 10, 0, Rotation.Deg90, 0);
            WindField leftField = Simulate(boardLeft, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 8));

            WindStream leftStream = leftField.Streams[0];
            Assert.AreEqual(1, leftStream.Turns.Count);
            Assert.AreEqual(TurnDir.Left, leftStream.Turns[0].Turn);
            Assert.AreEqual(9, leftStream.Path.Count, "转向不消耗额外风长");
            // 东风左转 45° = 东北：后续沿对角线走
            Assert.AreEqual(new CellCoord(10, 14), leftStream.End);

            var boardRight = new BuildBoard(MakeMap(), MakeRules(0, 0, sail));
            boardRight.Place(sail, 6, 10, 0, Rotation.Deg0, 0); // Deg0 = 右转
            WindField rightField = Simulate(boardRight, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 8));
            Assert.AreEqual(new CellCoord(10, 6), rightField.Streams[0].End, "东风右转 45° 应折向东南");
        }

        [Test]
        public void 多帆绕环_自交格出现同股双分量_终止有界()
        {
            BuildingBlueprint sail = MakeBlueprint(BuildRuleSet.SailBuildingId);
            var board = new BuildBoard(MakeMap(), MakeRules(0, 0, sail));
            // 七面左转帆把东风绕回起点，再次经过 (5,0) 后朝东南出图
            var sailCells = new[]
            {
                new CellCoord(8, 0), new CellCoord(10, 2), new CellCoord(10, 5), new CellCoord(8, 7),
                new CellCoord(5, 7), new CellCoord(3, 5), new CellCoord(3, 2),
            };
            foreach (CellCoord cell in sailCells)
            {
                board.Place(sail, cell.X, cell.Z, 0, Rotation.Deg90, 0);
            }

            WindField field = Simulate(board, new WindSeed(new CellCoord(5, 0), 0, Dir8.E, 2, 25));
            WindStream stream = field.Streams[0];

            Assert.AreEqual(7, stream.Turns.Count);
            Assert.AreEqual(WindEndReason.OutOfMap, stream.EndReason, "绕环后应从下边界出图，终止有界");

            WindCellState origin;
            Assert.IsTrue(field.TryGetCell(5, 0, 0, out origin));
            Assert.AreEqual(2, origin.Passes.Count, "自交格应有同股两个分量");
        }

        [Test]
        public void 物流点延长_同点不重复_每股至多两次_物流风从下一格起()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            var board = new BuildBoard(MakeMap(), MakeRules(3, 2, point));
            board.Place(point, 2, 5, 0, Rotation.Deg0, 0);
            board.Place(point, 4, 5, 0, Rotation.Deg0, 0);
            board.Place(point, 6, 5, 0, Rotation.Deg0, 0);

            WindField field = Simulate(board, new WindSeed(new CellCoord(0, 5), 0, Dir8.E, 2, 6));
            WindStream stream = field.Streams[0];

            Assert.AreEqual(2, stream.LengthMods.Count, "第三个物流点不该再延长（每股至多 2 次）");
            Assert.AreEqual(12, stream.CurrentLength);
            Assert.AreEqual(new CellCoord(12, 5), stream.End);
            Assert.AreEqual(3, stream.LogisticsFromIndex, "物流风从首个物流点的下一格起");
            Assert.IsFalse(field.IsLogisticsAt(2, 5, 0), "物流点自己那格还不是物流风");
            Assert.IsTrue(field.IsLogisticsAt(3, 5, 0));
            Assert.IsTrue(field.IsLogisticsAt(12, 5, 0));
        }

        [Test]
        public void 对撞抵消只在重叠格_越过对方终点各自恢复_且交汇不改风长()
        {
            var board = new BuildBoard(MakeMap(), MakeRules());
            WindField field = Simulate(
                board,
                new WindSeed(new CellCoord(0, 5), 0, Dir8.E, 3, 6),
                new WindSeed(new CellCoord(10, 5), 0, Dir8.W, 3, 6));

            Assert.AreEqual(3, field.GetForce(2, 5, 0), "重叠段之外东风保持原强度");
            Assert.AreEqual(0, field.GetForce(5, 5, 0), "重叠格完全抵消");
            Assert.AreEqual(3, field.GetForce(8, 5, 0), "重叠段之外西风保持原强度");

            // 交汇不改风长（决策 6/23）：两股的路径、终点、总长都与各自单独存在时一致
            Assert.AreEqual(7, field.Streams[0].Path.Count);
            Assert.AreEqual(new CellCoord(6, 5), field.Streams[0].End);
            Assert.AreEqual(6, field.Streams[0].CurrentLength);
            Assert.AreEqual(7, field.Streams[1].Path.Count);
            Assert.AreEqual(new CellCoord(4, 5), field.Streams[1].End);
            Assert.AreEqual(6, field.Streams[1].CurrentLength);
        }

        [Test]
        public void 同向增强_只持续到较短一股耗尽处()
        {
            var board = new BuildBoard(MakeMap(), MakeRules());
            WindField field = Simulate(
                board,
                new WindSeed(new CellCoord(0, 5), 0, Dir8.E, 2, 8),
                new WindSeed(new CellCoord(2, 5), 0, Dir8.E, 2, 3));

            Assert.AreEqual(2, field.GetForce(1, 5, 0), "短股起点之前只有长股");
            Assert.AreEqual(4, field.GetForce(3, 5, 0), "重叠段同向叠加");
            Assert.AreEqual(2, field.GetForce(7, 5, 0), "短股耗尽后长股恢复原强度");
            Assert.AreEqual(9, field.Streams[0].Path.Count, "交汇不改长股路径");
            Assert.AreEqual(4, field.Streams[1].Path.Count, "交汇不改短股路径");
        }

        [Test]
        public void 同一风帆重复经过_每次都转向()
        {
            BuildingBlueprint sail = MakeBlueprint(BuildRuleSet.SailBuildingId);
            var board = new BuildBoard(MakeMap(), MakeRules(0, 0, sail));
            // 八面左转帆构成闭环：风长 30 足够绕完一圈再进半圈，第二圈重进 (8,0) 等帆
            var sailCells = new[]
            {
                new CellCoord(8, 0), new CellCoord(10, 2), new CellCoord(10, 5), new CellCoord(8, 7),
                new CellCoord(5, 7), new CellCoord(3, 5), new CellCoord(3, 2), new CellCoord(5, 0),
            };
            foreach (CellCoord cell in sailCells)
            {
                board.Place(sail, cell.X, cell.Z, 0, Rotation.Deg90, 0);
            }

            WindField field = Simulate(board, new WindSeed(new CellCoord(6, 0), 0, Dir8.E, 2, 30));
            WindStream stream = field.Streams[0];

            Assert.AreEqual(WindEndReason.LengthExhausted, stream.EndReason, "绕环由风长有界终止");
            Assert.AreEqual(12, stream.Turns.Count, "一圈 8 转 + 第二圈 4 转");
            int turnsAtFirstSail = 0;
            for (int i = 0; i < stream.Turns.Count; i++)
            {
                if (stream.Turns[i].Cell.X == 8 && stream.Turns[i].Cell.Z == 0)
                {
                    turnsAtFirstSail++;
                }
            }
            Assert.AreEqual(2, turnsAtFirstSail, "同一面帆第二次被经过时必须再次转向");
        }

        [Test]
        public void 两格宽物流点_连穿两格只延长一次_不占用下游名额()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId, new[] { "##", "##" });
            var board = new BuildBoard(MakeMap(), MakeRules(3, 2, point));
            board.Place(point, 4, 5, 0, Rotation.Deg0, 0); // 占 (4,5)(5,5)(4,6)(5,6)
            board.Place(point, 8, 5, 0, Rotation.Deg0, 0); // 占 (8,5)(9,5)(8,6)(9,6)

            WindField field = Simulate(board, new WindSeed(new CellCoord(0, 5), 0, Dir8.E, 2, 6));
            WindStream stream = field.Streams[0];

            // 风沿 z=5 连穿每栋点的两格：同一栋只延长一次，下游第二栋仍有名额
            Assert.AreEqual(2, stream.LengthMods.Count, "两栋点各延长一次，而不是第一栋吃掉两次");
            Assert.AreEqual(new CellCoord(4, 5), stream.LengthMods[0].SourceCell);
            Assert.AreEqual(new CellCoord(8, 5), stream.LengthMods[1].SourceCell);
            Assert.AreEqual(12, stream.CurrentLength);
            Assert.AreEqual(new CellCoord(12, 5), stream.End);
        }

        [Test]
        public void 物流点是路径最后一格_LogisticsFromIndex按契约归位为负一()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            var board = new BuildBoard(MakeMap(), MakeRules(3, 2, point));
            board.Place(point, 19, 5, 0, Rotation.Deg0, 0); // 贴东边界

            WindField field = Simulate(board, new WindSeed(new CellCoord(17, 5), 0, Dir8.E, 2, 10));
            WindStream stream = field.Streams[0];

            Assert.AreEqual(WindEndReason.OutOfMap, stream.EndReason);
            Assert.AreEqual(-1, stream.LogisticsFromIndex, "点后一格已出图，股上没有任何物流风格");
            Assert.IsFalse(field.IsLogisticsAt(19, 5, 0));
            Assert.AreEqual(1, stream.LengthMods.Count, "延长账本照记（虽然延长的长度没能用上）");
        }

        // ---------- 合成公式（WIND_IMPL §4 基准表） ----------

        [Test]
        public void 合成基准_同向叠加()
        {
            AssertCompose(5, new[] { Pass(Dir8.E, 3), Pass(Dir8.E, 2) }, 5, Dir8.E);
        }

        [Test]
        public void 合成基准_直角交汇取整()
        {
            // (3,2) 模长 3.61 → round 4，33.7° → 东北
            AssertCompose(5, new[] { Pass(Dir8.E, 3), Pass(Dir8.N, 2) }, 4, Dir8.NE);
        }

        [Test]
        public void 合成基准_完全抵消为无风()
        {
            AssertCompose(5, new[] { Pass(Dir8.E, 3), Pass(Dir8.W, 3) }, 0, Dir8.E);
        }

        [Test]
        public void 合成基准_部分抵消()
        {
            AssertCompose(5, new[] { Pass(Dir8.E, 5), Pass(Dir8.W, 2) }, 3, Dir8.E);
        }

        [Test]
        public void 合成基准_斜向单位向量且先取整后封顶()
        {
            // 4东 + 3东南 = (6.12, -2.12) 模长 6.48 → round 6 → 封顶 5，-19.1° → 东
            AssertCompose(5, new[] { Pass(Dir8.E, 4), Pass(Dir8.SE, 3) }, 5, Dir8.E);
        }

        [Test]
        public void 合成基准_平局225度取顺时针()
        {
            // 2东 + 2东北恰好落在 22.5° 角平分线上 → 取顺时针一侧（东）
            AssertCompose(5, new[] { Pass(Dir8.E, 2), Pass(Dir8.NE, 2) }, 4, Dir8.E);
        }

        [Test]
        public void 合成为纯函数_股序无关()
        {
            var forward = new[] { Pass(Dir8.E, 3), Pass(Dir8.N, 2), Pass(Dir8.SE, 4) };
            var backward = new[] { Pass(Dir8.SE, 4), Pass(Dir8.N, 2), Pass(Dir8.E, 3) };

            Dir8 dirA, dirB;
            int forceA, forceB;
            WindMath.Compose(forward, 5, out dirA, out forceA);
            WindMath.Compose(backward, 5, out dirB, out forceB);

            Assert.AreEqual(forceA, forceB);
            Assert.AreEqual(dirA, dirB);
        }

        [Test]
        public void 转向映射_逆时针为左()
        {
            Assert.AreEqual(Dir8.NE, WindMath.TurnedBy(Dir8.E, TurnDir.Left));
            Assert.AreEqual(Dir8.SE, WindMath.TurnedBy(Dir8.E, TurnDir.Right));
            Assert.AreEqual(Dir8.E, WindMath.TurnedBy(Dir8.SE, TurnDir.Left));
            Assert.AreEqual(Dir8.W, WindMath.TurnedBy(Dir8.SW, TurnDir.Right));
        }

        [Test]
        public void 风帆朝向映射_偶右奇左()
        {
            Assert.AreEqual(TurnDir.Right, WindSystem.TurnFromRotation(Rotation.Deg0));
            Assert.AreEqual(TurnDir.Left, WindSystem.TurnFromRotation(Rotation.Deg90));
            Assert.AreEqual(TurnDir.Right, WindSystem.TurnFromRotation(Rotation.Deg180));
            Assert.AreEqual(TurnDir.Left, WindSystem.TurnFromRotation(Rotation.Deg270));
        }
    }
}
