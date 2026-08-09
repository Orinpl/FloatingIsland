using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 摆放校验与即时计分的契约测试。
    ///
    /// 领域层是规则的唯一真相（GRID_INTEGRATION §1：EGB 与表现层永远不裁决），
    /// 所以这里锁死的是「什么摆得下、摆下去得几分」——表现层怎么画不影响这些断言。
    /// </summary>
    public sealed class BuildBoardTests
    {
        private const string Island = "island";
        private const string GreenField = "greenField";
        private const string FloatingZone = "floatingZone";

        // ---------- 造测试数据 ----------

        private static MapSnapshot MakeMap(int size = 20, string terrain = Island, IReadOnlyList<MapElementPlacement> elements = null)
        {
            var cells = new List<MapCell>(size * size);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    cells.Add(new MapCell(x, z, 0, terrain));
                }
            }
            return new MapSnapshot(1, size, size, 1, cells, elements);
        }

        private static BuildingBlueprint MakeBlueprint(
            string variantId, string buildingId, string[] mask,
            PlacementRule placement = PlacementRule.Any,
            int radius = 5, int baseScore = 10,
            IReadOnlyList<ScoreSource> bonusFrom = null,
            IReadOnlyList<ScoreSource> penaltyFrom = null,
            IReadOnlyList<ScoreSource> elementBonus = null,
            int isolationPenalty = 0)
        {
            return new BuildingBlueprint(
                variantId, buildingId, buildingId, "test",
                Footprint.Parse(mask, variantId),
                placement, radius, baseScore,
                canLogisticsCover: true,
                isolationPenaltyScore: isolationPenalty,
                prefabPath: "",
                elementBonus: elementBonus,
                bonusFrom: bonusFrom,
                penaltyFrom: penaltyFrom,
                windScoreByLevel: null);
        }

        private static MapElementDef MakeElement(string elementId, string[] mask, int radius)
        {
            return new MapElementDef(elementId, elementId, Footprint.Parse(mask, elementId), radius, false, 0, 0, "");
        }

        private static BuildRuleSet MakeRules(
            IReadOnlyList<BuildingBlueprint> blueprints,
            IReadOnlyList<MapElementDef> elements = null,
            int giantWindmillGeneric = 0,
            int logisticsCoverRadius = 0,
            int logisticsCoverScore = 0)
        {
            return new BuildRuleSet(
                blueprints,
                elements ?? new List<MapElementDef>(),
                layerHeightFactor: 1f,
                giantWindmillGenericScore: giantWindmillGeneric,
                anchorDockDecayPercents: new[] { 1f, 0.5f, 0.25f, 0f },
                logisticsCoverRadius: logisticsCoverRadius,
                logisticsBaseCoverScore: logisticsCoverScore,
                scoreToGoldRatio: 1f,
                windEnabled: false);
        }

        // ---------- 摆放校验 ----------

        [Test]
        public void 合法地块上可以摆放()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "##", "##" });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));

            Assert.IsTrue(board.CanPlace(house, 5, 5, 0, Rotation.Deg0).IsValid);
        }

        [Test]
        public void 占地压到虚空时拒绝()
        {
            // 只刷了 (0,0) 一格，2×2 建筑必然压到虚空
            var map = new MapSnapshot(1, 10, 10, 1, new[] { new MapCell(0, 0, 0, Island) });
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "##", "##" });
            var board = new BuildBoard(map, MakeRules(new[] { house }));

            PlacementCheck check = board.CanPlace(house, 0, 0, 0, Rotation.Deg0);
            Assert.IsFalse(check.IsValid);
            Assert.AreEqual(PlacementFailure.Void, check.Failure);
        }

        [Test]
        public void 超出地图范围时拒绝()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "##", "##" });
            var board = new BuildBoard(MakeMap(10), MakeRules(new[] { house }));

            PlacementCheck check = board.CanPlace(house, 9, 9, 0, Rotation.Deg0);
            Assert.IsFalse(check.IsValid);
            Assert.AreEqual(PlacementFailure.OutOfBounds, check.Failure);
        }

        [Test]
        public void 与已有建筑重叠时拒绝()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "##", "##" });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            board.Place(house, 5, 5, 0, Rotation.Deg0, 0);

            PlacementCheck check = board.CanPlace(house, 6, 6, 0, Rotation.Deg0);
            Assert.IsFalse(check.IsValid);
            Assert.AreEqual(PlacementFailure.Occupied, check.Failure);
        }

        [Test]
        public void 两个L形可以互嵌不算重叠()
        {
            // GRID_INTEGRATION §1 明确点名的场景：矩形占地装不下，异形判定必须逐格
            BuildingBlueprint lShape = MakeBlueprint("l_01", "l", new[] { "##", "#." });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { lShape }));

            // 第一个占 (0,0)(0,1)(1,1)
            board.Place(lShape, 0, 0, 0, Rotation.Deg0, 0);
            // 第二个转 180° 后占 (1,0)(0,0)... 换个锚点让它正好填进缺口 (1,0)
            List<CellCoord> cells = new List<CellCoord>();
            lShape.Footprint.GetCells(1, 0, Rotation.Deg180, cells);

            // 手工确认这次摆放只用到空着的格子，再断言领域层同意
            Assert.IsTrue(board.CanPlace(lShape, 1, 0, 0, Rotation.Deg180).IsValid,
                "互嵌的异形占地被误判为重叠：" + board.CanPlace(lShape, 1, 0, 0, Rotation.Deg180).Reason);
        }

        [Test]
        public void 农田必须建在绿地上()
        {
            BuildingBlueprint farm = MakeBlueprint("farm_01", "farm", new[] { "#" }, PlacementRule.GreenField);
            var board = new BuildBoard(MakeMap(terrain: Island), MakeRules(new[] { farm }));

            PlacementCheck check = board.CanPlace(farm, 3, 3, 0, Rotation.Deg0);
            Assert.IsFalse(check.IsValid);
            Assert.AreEqual(PlacementFailure.TerrainMismatch, check.Failure);

            var greenBoard = new BuildBoard(MakeMap(terrain: GreenField), MakeRules(new[] { farm }));
            Assert.IsTrue(greenBoard.CanPlace(farm, 3, 3, 0, Rotation.Deg0).IsValid);
        }

        [Test]
        public void 普通建筑不能建在浮空区域()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" });
            var board = new BuildBoard(MakeMap(terrain: FloatingZone), MakeRules(new[] { house }));

            PlacementCheck check = board.CanPlace(house, 3, 3, 0, Rotation.Deg0);
            Assert.IsFalse(check.IsValid);
            Assert.AreEqual(PlacementFailure.TerrainMismatch, check.Failure);
        }

        [Test]
        public void 船坞只能建在浮空区域()
        {
            BuildingBlueprint dock = MakeBlueprint("dock_01", "dock", new[] { "#" }, PlacementRule.FloatingZone);

            var islandBoard = new BuildBoard(MakeMap(terrain: Island), MakeRules(new[] { dock }));
            Assert.IsFalse(islandBoard.CanPlace(dock, 3, 3, 0, Rotation.Deg0).IsValid);

            var floatingBoard = new BuildBoard(MakeMap(terrain: FloatingZone), MakeRules(new[] { dock }));
            Assert.IsTrue(floatingBoard.CanPlace(dock, 3, 3, 0, Rotation.Deg0).IsValid);
        }

        [Test]
        public void 采矿站必须在矿藏有效范围内()
        {
            MapElementDef ore = MakeElement("ore", new[] { "##", "##" }, radius: 4);
            BuildingBlueprint mine = MakeBlueprint("mine_01", "miningStation", new[] { "#" }, PlacementRule.OreRange);
            var elements = new[] { new MapElementPlacement("ore", 2, 2, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { mine }, new[] { ore }));

            Assert.IsTrue(board.CanPlace(mine, 6, 3, 0, Rotation.Deg0).IsValid, "紧邻矿藏却判成超范围");

            PlacementCheck far = board.CanPlace(mine, 18, 18, 0, Rotation.Deg0);
            Assert.IsFalse(far.IsValid);
            Assert.AreEqual(PlacementFailure.OutOfOreRange, far.Failure);
        }

        [Test]
        public void 占地压到地图元素时拒绝()
        {
            MapElementDef ore = MakeElement("ore", new[] { "##", "##" }, radius: 4);
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" });
            var elements = new[] { new MapElementPlacement("ore", 5, 5, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { house }, new[] { ore }));

            PlacementCheck check = board.CanPlace(house, 5, 5, 0, Rotation.Deg0);
            Assert.IsFalse(check.IsValid);
            Assert.AreEqual(PlacementFailure.BlockedByElement, check.Failure);
        }

        [Test]
        public void 非法位置调用Place直接抛异常()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "##", "##" });
            var board = new BuildBoard(MakeMap(10), MakeRules(new[] { house }));

            Assert.Throws<System.InvalidOperationException>(() => board.Place(house, 9, 9, 0, Rotation.Deg0, 0));
        }

        // ---------- 计分 ----------

        [Test]
        public void 只有基础分时总分等于基础分()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 17);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);

            Assert.AreEqual(17, engine.Evaluate(house, 5, 5, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 邻近建筑按数量加分且吃上限()
        {
            var bonus = new[] { new ScoreSource("house", 10, 2) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);

            board.Place(house, 5, 5, 0, Rotation.Deg0, 0);
            board.Place(house, 5, 6, 0, Rotation.Deg0, 0);
            board.Place(house, 5, 7, 0, Rotation.Deg0, 0);

            // 三个邻居都在半径 5 内，但上限 2 → 只计 20
            Assert.AreEqual(20, engine.Evaluate(house, 6, 6, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 同类拥挤按正数配置扣分()
        {
            var penalty = new[] { new ScoreSource("tower", 30, 0) };
            BuildingBlueprint tower = MakeBlueprint("tower_01", "tower", new[] { "#" }, baseScore: 10, penaltyFrom: penalty);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { tower }));
            var engine = new ScoreEngine(board);

            board.Place(tower, 5, 5, 0, Rotation.Deg0, 0);

            Assert.AreEqual(10 - 30, engine.Evaluate(tower, 6, 5, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 超出半径的建筑不参与计分()
        {
            var bonus = new[] { new ScoreSource("house", 10, 0) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, radius: 3, baseScore: 0, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);

            board.Place(house, 0, 0, 0, Rotation.Deg0, 0);

            Assert.AreEqual(0, engine.Evaluate(house, 10, 10, 0, Rotation.Deg0).Total, "半径 3 之外的邻居不该加分");
            Assert.AreEqual(10, engine.Evaluate(house, 3, 0, 0, Rotation.Deg0).Total, "距离正好等于半径应计入");
        }

        [Test]
        public void 巨型风车通用加分在没配专属条目时生效()
        {
            MapElementDef giant = MakeElement("giantWindmill", new[] { "##", "##" }, radius: 6);
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0);
            var elements = new[] { new MapElementPlacement("giantWindmill", 5, 5, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { house }, new[] { giant }, giantWindmillGeneric: 10));
            var engine = new ScoreEngine(board);

            Assert.AreEqual(10, engine.Evaluate(house, 8, 5, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 巨型风车专属加分替代通用不叠加()
        {
            MapElementDef giant = MakeElement("giantWindmill", new[] { "##", "##" }, radius: 6);
            var exclusive = new[] { new ScoreSource("giantWindmill", 30, 0) };
            BuildingBlueprint tower = MakeBlueprint("tower_01", "tower", new[] { "#" }, baseScore: 0, elementBonus: exclusive);
            var elements = new[] { new MapElementPlacement("giantWindmill", 5, 5, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { tower }, new[] { giant }, giantWindmillGeneric: 10));
            var engine = new ScoreEngine(board);

            // 专属 30 替代通用 10，不是 40
            Assert.AreEqual(30, engine.Evaluate(tower, 8, 5, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 物流覆盖分每栋只计一次()
        {
            var logistics = MakeBlueprint("lp_01", "logisticsPoint", new[] { "#" }, baseScore: 0);
            var house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { logistics, house }, logisticsCoverRadius: 5, logisticsCoverScore: 8));
            var engine = new ScoreEngine(board);

            board.Place(logistics, 5, 5, 0, Rotation.Deg0, 0);
            board.Place(logistics, 5, 6, 0, Rotation.Deg0, 0);

            // 两个物流点都覆盖到了，覆盖分仍只给一次
            Assert.AreEqual(8, engine.Evaluate(house, 6, 6, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 孤立惩罚只在完全没有加分来源时生效()
        {
            var bonus = new[] { new ScoreSource("house", 10, 0) };
            BuildingBlueprint dock = MakeBlueprint("dock_01", "dock", new[] { "#" }, baseScore: 20, bonusFrom: bonus, isolationPenalty: -50);
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { dock, house }));
            var engine = new ScoreEngine(board);

            Assert.AreEqual(20 - 50, engine.Evaluate(dock, 5, 5, 0, Rotation.Deg0).Total, "孤零零的船坞应吃孤立惩罚");

            board.Place(house, 6, 5, 0, Rotation.Deg0, 0);
            Assert.AreEqual(20 + 10, engine.Evaluate(dock, 5, 5, 0, Rotation.Deg0).Total, "旁边有加分来源就不该再罚");
        }

        [Test]
        public void 风场未接入时不结算风力分()
        {
            // 船坞的 windScoreByLevel 首项是 -150（无风重罚）。风场没接入时若按 0 级结算，
            // 每座船坞都会平白吃 -150——这不是设计，是缺数据。
            var dock = new BuildingBlueprint(
                "dock_01", "dock", "船坞", "test",
                Footprint.Parse(new[] { "#" }, "dock"),
                PlacementRule.Any, 5, 20, true, 0, "",
                null, null, null,
                new[] { -150, 30, 60, 120, 240, 480 });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { dock }));
            var engine = new ScoreEngine(board);

            Assert.AreEqual(20, engine.Evaluate(dock, 5, 5, 0, Rotation.Deg0).Total);
        }
    }
}
