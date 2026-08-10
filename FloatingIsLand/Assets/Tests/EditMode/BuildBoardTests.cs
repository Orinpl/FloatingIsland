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
                scoreToGoldRatio: 1f);
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
        public void 元素超出建筑自己的半径时不加分()
        {
            // 元素的 radius 再大也不管计分：判定的是「建筑的影响范围有没有覆盖到元素的地格」。
            // 巨型风车 radius 12 而工坊 radius 5，隔了 9 格的风车不该给工坊加分。
            MapElementDef giant = MakeElement("giantWindmill", new[] { "##", "##" }, radius: 12);
            BuildingBlueprint workshop = MakeBlueprint("workshop_01", "workshop", new[] { "#" }, radius: 5, baseScore: 0);
            var elements = new[] { new MapElementPlacement("giantWindmill", 5, 5, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { workshop }, new[] { giant }, giantWindmillGeneric: 10));
            var engine = new ScoreEngine(board);

            // 占地是 (5,5)~(6,6)，最近的格是 (6,5)：距离 9 > 5
            Assert.AreEqual(0, engine.Evaluate(workshop, 15, 5, 0, Rotation.Deg0).Total, "范围外的巨型风车不该加分");
            // 边界上（距离正好 5）仍然算，证明不是整条通道被关掉了
            Assert.AreEqual(10, engine.Evaluate(workshop, 11, 5, 0, Rotation.Deg0).Total, "半径边界上的元素应该加分");
        }

        [Test]
        public void 元素专属条目同样只看建筑自己的半径()
        {
            MapElementDef ore = MakeElement("ore", new[] { "##", "##" }, radius: 10);
            var exclusive = new[] { new ScoreSource("ore", 25, 0) };
            BuildingBlueprint mine = MakeBlueprint("mine_01", "miningStation", new[] { "#" }, radius: 3, baseScore: 0, elementBonus: exclusive);
            var elements = new[] { new MapElementPlacement("ore", 5, 5, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { mine }, new[] { ore }));
            var engine = new ScoreEngine(board);

            Assert.AreEqual(0, engine.Evaluate(mine, 12, 5, 0, Rotation.Deg0).Total, "矿藏 radius 10 不该顶替采矿站自己的 3");
            Assert.AreEqual(25, engine.Evaluate(mine, 9, 5, 0, Rotation.Deg0).Total);
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

        // ---------- 已落地建筑的分数不回溯 ----------
        //
        // 设计要求：只有"正在建的那一栋"会按范围内的邻居算分，已经建好的不会因为旁边新建了什么而补分。
        // 这条规则决定了玩家能不能提前规划，也是分数可预期的前提，所以要从多个角度钉死。

        [Test]
        public void 落地时算出的分被原样记住()
        {
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 13);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);

            ScoreBreakdown breakdown = engine.Evaluate(house, 5, 5, 0, Rotation.Deg0);
            PlacedBuilding placed = board.Place(house, 5, 5, 0, Rotation.Deg0, breakdown.Total);

            Assert.AreEqual(13, placed.InstantScore);
        }

        [Test]
        public void 旁边再建新建筑_已落地建筑的即时分不变()
        {
            var bonus = new[] { new ScoreSource("house", 10, 0) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 5, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);

            // 第一栋孤零零落地：只有基础分
            PlacedBuilding first = board.Place(house, 5, 5, 0, Rotation.Deg0,
                engine.Evaluate(house, 5, 5, 0, Rotation.Deg0).Total);
            Assert.AreEqual(5, first.InstantScore);

            // 紧挨着再建三栋，每一栋都能看到第一栋
            for (int i = 1; i <= 3; i++)
            {
                board.Place(house, 5 + i, 5, 0, Rotation.Deg0,
                    engine.Evaluate(house, 5 + i, 5, 0, Rotation.Deg0).Total);
            }

            Assert.AreEqual(5, first.InstantScore, "已落地建筑被后来的邻居补了分——分数回溯了");
        }

        [Test]
        public void 有向邻接不是双向_先建的拿不到后建者的分()
        {
            // A 因为身边有 B 而加分，B 对 A 没有任何条目。先建 A 再建 B，A 一分都不该多拿。
            var aBonus = new[] { new ScoreSource("b", 40, 0) };
            BuildingBlueprint a = MakeBlueprint("a_01", "a", new[] { "#" }, baseScore: 0, bonusFrom: aBonus);
            BuildingBlueprint b = MakeBlueprint("b_01", "b", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { a, b }));
            var engine = new ScoreEngine(board);

            PlacedBuilding placedA = board.Place(a, 5, 5, 0, Rotation.Deg0,
                engine.Evaluate(a, 5, 5, 0, Rotation.Deg0).Total);
            board.Place(b, 6, 5, 0, Rotation.Deg0, engine.Evaluate(b, 6, 5, 0, Rotation.Deg0).Total);

            Assert.AreEqual(0, placedA.InstantScore);
            // 反过来现在再建一个 A，它就该吃到那 40 分——证明规则本身是生效的，上面不是因为条目没读到
            Assert.AreEqual(40, engine.Evaluate(a, 7, 5, 0, Rotation.Deg0).Total);
        }

        [Test]
        public void 孤立惩罚在落地那刻定死_后来有了邻居也不退还()
        {
            var bonus = new[] { new ScoreSource("house", 10, 0) };
            BuildingBlueprint dock = MakeBlueprint("dock_01", "dock", new[] { "#" }, baseScore: 20, bonusFrom: bonus, isolationPenalty: -50);
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { dock, house }));
            var engine = new ScoreEngine(board);

            PlacedBuilding placedDock = board.Place(dock, 5, 5, 0, Rotation.Deg0,
                engine.Evaluate(dock, 5, 5, 0, Rotation.Deg0).Total);
            Assert.AreEqual(-30, placedDock.InstantScore);

            board.Place(house, 6, 5, 0, Rotation.Deg0, 0);
            Assert.AreEqual(-30, placedDock.InstantScore, "后来有了邻居就把孤立惩罚退了——分数回溯了");
        }

        [Test]
        public void 物流点后建_已落地建筑不会补发覆盖分()
        {
            var logistics = MakeBlueprint("lp_01", "logisticsPoint", new[] { "#" }, baseScore: 0);
            var house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { logistics, house }, logisticsCoverRadius: 5, logisticsCoverScore: 8));
            var engine = new ScoreEngine(board);

            PlacedBuilding placedHouse = board.Place(house, 6, 6, 0, Rotation.Deg0,
                engine.Evaluate(house, 6, 6, 0, Rotation.Deg0).Total);
            Assert.AreEqual(0, placedHouse.InstantScore);

            board.Place(logistics, 5, 5, 0, Rotation.Deg0, 0);
            Assert.AreEqual(0, placedHouse.InstantScore, "物流点后建就给已有建筑补发了覆盖分");
        }

        [Test]
        public void 计分是纯查询_算完之后棋盘状态不变()
        {
            var bonus = new[] { new ScoreSource("house", 10, 0) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 5, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);
            board.Place(house, 5, 5, 0, Rotation.Deg0, 99);

            int before = board.Buildings.Count;
            engine.Evaluate(house, 7, 7, 0, Rotation.Deg0);
            engine.Evaluate(house, 8, 8, 0, Rotation.Deg0);

            Assert.AreEqual(before, board.Buildings.Count, "干跑算分把建筑加进棋盘了");
            Assert.AreEqual(99, board.Buildings[0].InstantScore, "干跑算分改了已落地建筑的分");
        }

        [Test]
        public void 同一位置连算两次结果一致()
        {
            var bonus = new[] { new ScoreSource("house", 7, 0) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 5, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);
            board.Place(house, 5, 5, 0, Rotation.Deg0, 0);
            board.Place(house, 5, 6, 0, Rotation.Deg0, 0);

            Assert.AreEqual(
                engine.Evaluate(house, 7, 7, 0, Rotation.Deg0).Total,
                engine.Evaluate(house, 7, 7, 0, Rotation.Deg0).Total);
        }

        // ---------- 逐实例归因 ----------
        //
        // 表现层靠归因决定「在谁头上飘 +3」。归因必须和实际算分同源、且可复现，
        // 否则会出现"飘的数字和到手的分对不上"这种最难查的 bug。

        [Test]
        public void 归因合计等于总分()
        {
            var bonus = new[] { new ScoreSource("house", 7, 0) };
            var penalty = new[] { new ScoreSource("tower", 4, 0) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 11, bonusFrom: bonus, penaltyFrom: penalty);
            BuildingBlueprint tower = MakeBlueprint("tower_01", "tower", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house, tower }));
            var engine = new ScoreEngine(board);

            board.Place(house, 5, 5, 0, Rotation.Deg0, 0);
            board.Place(house, 5, 6, 0, Rotation.Deg0, 0);
            board.Place(tower, 6, 5, 0, Rotation.Deg0, 0);

            ScoreBreakdown breakdown = engine.Evaluate(house, 6, 6, 0, Rotation.Deg0);

            int sum = 0;
            for (int i = 0; i < breakdown.Attributions.Count; i++)
            {
                sum += breakdown.Attributions[i].Score;
            }
            Assert.AreEqual(breakdown.Total, sum, "归因加起来对不上总分——表现层飘的数字会和实际得分分叉");
        }

        [Test]
        public void 同一棋盘两次算分的归因序列逐项相等()
        {
            var bonus = new[] { new ScoreSource("house", 7, 2) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);
            for (int i = 0; i < 5; i++)
            {
                board.Place(house, 5 + i, 5, 0, Rotation.Deg0, 0);
            }

            IReadOnlyList<ScoreAttribution> first = engine.Evaluate(house, 6, 6, 0, Rotation.Deg0).Attributions;
            IReadOnlyList<ScoreAttribution> second = engine.Evaluate(house, 6, 6, 0, Rotation.Deg0).Attributions;

            Assert.AreEqual(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreEqual(first[i].InstanceId, second[i].InstanceId, $"第 {i} 条归因的实例变了");
                Assert.AreEqual(first[i].Score, second[i].Score, $"第 {i} 条归因的分变了");
                Assert.AreEqual(first[i].State, second[i].State, $"第 {i} 条归因的计入状态变了");
            }
        }

        [Test]
        public void 超出计数上限的实例仍会出现在归因里且标为未计入()
        {
            var bonus = new[] { new ScoreSource("house", 10, 2) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);
            board.Place(house, 5, 5, 0, Rotation.Deg0, 0);
            board.Place(house, 5, 6, 0, Rotation.Deg0, 0);
            board.Place(house, 5, 7, 0, Rotation.Deg0, 0);

            ScoreBreakdown breakdown = engine.Evaluate(house, 6, 6, 0, Rotation.Deg0);
            var buildings = new List<ScoreAttribution>();
            breakdown.CollectAttributions(ScoreSourceKind.Building, false, buildings);

            Assert.AreEqual(3, buildings.Count, "三个邻居都该出现在归因里——被上限挡住的也要能高亮");
            int counted = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i].Counted)
                {
                    counted++;
                }
                else
                {
                    Assert.AreEqual(ScoreCountState.OverMaxCount, buildings[i].State);
                    Assert.AreEqual(0, buildings[i].Score, "未计入的实例不该带分，否则归因合计会超过总分");
                }
            }
            Assert.AreEqual(2, counted);
            Assert.AreEqual(20, breakdown.Total);
        }

        [Test]
        public void 上限截断按距离由近到远_近的先算上()
        {
            var bonus = new[] { new ScoreSource("house", 10, 1) };
            BuildingBlueprint house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0, bonusFrom: bonus);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { house }));
            var engine = new ScoreEngine(board);

            // 先建远的、后建近的：如果按落地顺序取就会算错那一个
            PlacedBuilding far = board.Place(house, 9, 6, 0, Rotation.Deg0, 0);
            PlacedBuilding near = board.Place(house, 7, 6, 0, Rotation.Deg0, 0);

            ScoreBreakdown breakdown = engine.Evaluate(house, 6, 6, 0, Rotation.Deg0);
            var buildings = new List<ScoreAttribution>();
            breakdown.CollectAttributions(ScoreSourceKind.Building, true, buildings);

            Assert.AreEqual(1, buildings.Count);
            Assert.AreEqual(near.Id, buildings[0].InstanceId, "上限截断该留下最近的那个");
            Assert.AreNotEqual(far.Id, buildings[0].InstanceId);
        }

        [Test]
        public void 多个物流点覆盖时只有一个被记为计入()
        {
            var logistics = MakeBlueprint("lp_01", "logisticsPoint", new[] { "#" }, baseScore: 0);
            var house = MakeBlueprint("house_01", "house", new[] { "#" }, baseScore: 0);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { logistics, house }, logisticsCoverRadius: 5, logisticsCoverScore: 8));
            var engine = new ScoreEngine(board);
            board.Place(logistics, 5, 5, 0, Rotation.Deg0, 0);
            board.Place(logistics, 5, 6, 0, Rotation.Deg0, 0);
            board.Place(logistics, 5, 7, 0, Rotation.Deg0, 0);

            ScoreBreakdown breakdown = engine.Evaluate(house, 6, 6, 0, Rotation.Deg0);
            var all = new List<ScoreAttribution>();
            breakdown.CollectAttributions(ScoreSourceKind.Building, false, all);

            Assert.AreEqual(3, all.Count, "三个物流点都覆盖到了，都该能高亮");
            int counted = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Counted)
                {
                    counted++;
                }
                else
                {
                    Assert.AreEqual(ScoreCountState.Duplicated, all[i].State);
                }
            }
            Assert.AreEqual(1, counted, "覆盖分只计一次，归因也只能有一条算数");
            Assert.AreEqual(8, breakdown.Total);
        }

        [Test]
        public void 巨型风车专属替代通用时只产出一条元素归因()
        {
            MapElementDef giant = MakeElement("giantWindmill", new[] { "##", "##" }, radius: 6);
            var exclusive = new[] { new ScoreSource("giantWindmill", 30, 0) };
            BuildingBlueprint tower = MakeBlueprint("tower_01", "tower", new[] { "#" }, baseScore: 0, elementBonus: exclusive);
            var elements = new[] { new MapElementPlacement("giantWindmill", 5, 5, 0, Rotation.Deg0) };
            var board = new BuildBoard(MakeMap(20, Island, elements), MakeRules(new[] { tower }, new[] { giant }, giantWindmillGeneric: 10));
            var engine = new ScoreEngine(board);

            ScoreBreakdown breakdown = engine.Evaluate(tower, 8, 5, 0, Rotation.Deg0);
            var elementHits = new List<ScoreAttribution>();
            breakdown.CollectAttributions(ScoreSourceKind.Element, false, elementHits);

            Assert.AreEqual(1, elementHits.Count, "专属与通用同时归因会让风车头上飘两个数字");
            Assert.AreEqual(30, elementHits[0].Score);
        }

        [Test]
        public void 基础分与孤立惩罚归到自身_不指向任何对象()
        {
            BuildingBlueprint dock = MakeBlueprint("dock_01", "dock", new[] { "#" }, baseScore: 20, isolationPenalty: -50);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { dock }));
            var engine = new ScoreEngine(board);

            ScoreBreakdown breakdown = engine.Evaluate(dock, 5, 5, 0, Rotation.Deg0);
            var self = new List<ScoreAttribution>();
            breakdown.CollectAttributions(ScoreSourceKind.Self, true, self);

            Assert.AreEqual(2, self.Count, "基础分与孤立惩罚都该归到自身");
            Assert.AreEqual(0, breakdown.CollectAttributions(ScoreSourceKind.Building, false, new List<ScoreAttribution>()));
            Assert.AreEqual(0, breakdown.CollectAttributions(ScoreSourceKind.Element, false, new List<ScoreAttribution>()));
        }
    }
}
