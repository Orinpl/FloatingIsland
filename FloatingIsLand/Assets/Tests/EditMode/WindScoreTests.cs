using System.Collections.Generic;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 风相关计分语义的契约测试（用户定稿的一次性结算口径）：
    /// - 建造时：风帆即时分 / 居民区穿风惩罚 / 风向标倍率 / 物流风覆盖，都只看建造那一刻的风；
    /// - 风变更时：只有物流风通道发新分（覆盖一生一次、互联每对一次、链式相邻配对），从不回收已得分。
    /// </summary>
    public sealed class WindScoreTests
    {
        private const string Island = "island";

        // ---------- 造测试数据 ----------

        private static MapSnapshot MakeMap(int size = 24, IReadOnlyList<MapElementPlacement> elements = null)
        {
            var cells = new List<MapCell>(size * size);
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    cells.Add(new MapCell(x, z, 0, Island));
                }
            }
            return new MapSnapshot(1, size, size, 1, cells, elements);
        }

        private static BuildingBlueprint MakeBlueprint(
            string buildingId,
            PlacementRule placement = PlacementRule.Any,
            int baseScore = 0,
            bool canCover = false,
            IReadOnlyList<ScoreSource> bonusFrom = null,
            IReadOnlyList<int> windScore = null,
            IReadOnlyList<int> windPassPenalty = null,
            IReadOnlyList<float> residentMult = null)
        {
            return new BuildingBlueprint(
                buildingId + "_01", buildingId, buildingId, "test",
                Footprint.Parse(new[] { "#" }, buildingId),
                placement, radius: 4, baseScore: baseScore,
                canLogisticsCover: canCover,
                isolationPenaltyScore: 0, prefabPath: "",
                elementBonus: null, bonusFrom: bonusFrom, penaltyFrom: null,
                windScoreByLevel: windScore,
                windPassPenaltyByLevel: windPassPenalty,
                residentWindMultByLevel: residentMult);
        }

        private static BuildRuleSet MakeRules(
            IReadOnlyList<BuildingBlueprint> blueprints,
            IReadOnlyList<MapElementDef> elements = null,
            int coverRadius = 0, int coverScore = 0,
            int extendLength = 0, int extendMax = 0, int linkScore = 0,
            int windLevelMin = 1, int windLevelMax = 3,
            int windLengthMin = 8, int windLengthMax = 16)
        {
            return new BuildRuleSet(
                blueprints, elements ?? new List<MapElementDef>(),
                layerHeightFactor: 1f, giantWindmillGenericScore: 0,
                anchorDockDecayPercents: null,
                logisticsCoverRadius: coverRadius, logisticsBaseCoverScore: coverScore,
                maxWindLevel: 5,
                initialWindLevelMin: windLevelMin, initialWindLevelMax: windLevelMax,
                initialWindLengthMin: windLengthMin, initialWindLengthMax: windLengthMax,
                windExtendLength: extendLength, windExtendMaxPerWind: extendMax,
                logisticsWindLinkScore: linkScore);
        }

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

        // ---------- 建造时 ----------

        [Test]
        public void 风帆_风带外拒绝_风带上可建()
        {
            BuildingBlueprint sail = MakeBlueprint(BuildRuleSet.SailBuildingId, PlacementRule.WindPath);
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { sail }));
            board.WindField = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 8));

            PlacementCheck offWind = board.CanPlace(sail, 5, 3, 0, Rotation.Deg0);
            Assert.IsFalse(offWind.IsValid);
            Assert.AreEqual(PlacementFailure.NoWind, offWind.Failure);

            Assert.IsTrue(board.CanPlace(sail, 5, 10, 0, Rotation.Deg0).IsValid);
        }

        [Test]
        public void 风帆即时分_按建造那一刻的落点风力()
        {
            BuildingBlueprint sail = MakeBlueprint(
                BuildRuleSet.SailBuildingId, PlacementRule.WindPath,
                baseScore: 10, windScore: new[] { 0, 10, 20, 35, 55, 80 });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { sail }));
            board.WindField = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 8));

            var scoring = new ScoreEngine(board);
            ScoreBreakdown breakdown = scoring.Evaluate(sail, 5, 10, 0, Rotation.Deg0);

            Assert.AreEqual(10 + 20, breakdown.Total, "基础 10 + 2 级风 20");
        }

        [Test]
        public void 居民区穿风惩罚_只在风路直接经过时生效()
        {
            BuildingBlueprint residence = MakeBlueprint(
                BuildRuleSet.ResidenceBuildingId, baseScore: 30,
                windPassPenalty: new[] { 0, 0, 0, 0, -20, -50 });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { residence }));
            board.WindField = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 4, 8));

            var scoring = new ScoreEngine(board);
            Assert.AreEqual(30 - 20, scoring.Evaluate(residence, 5, 10, 0, Rotation.Deg0).Total, "4 级风穿过 -20");
            Assert.AreEqual(30, scoring.Evaluate(residence, 5, 3, 0, Rotation.Deg0).Total, "风路没经过就没有惩罚");
        }

        [Test]
        public void 风向标倍率_只放大居民区来源的分()
        {
            BuildingBlueprint residence = MakeBlueprint(BuildRuleSet.ResidenceBuildingId, baseScore: 0);
            BuildingBlueprint workshop = MakeBlueprint("workshop", baseScore: 0);
            BuildingBlueprint vane = MakeBlueprint(
                "windVane", baseScore: 0,
                bonusFrom: new[]
                {
                    new ScoreSource(BuildRuleSet.ResidenceBuildingId, 10, 0),
                    new ScoreSource("workshop", 10, 0),
                },
                residentMult: new[] { 1f, 1.1f, 1.25f, 1.5f, 1.8f, 2.2f });
            var board = new BuildBoard(MakeMap(), MakeRules(new[] { residence, workshop, vane }));
            board.Place(residence, 4, 10, 0, Rotation.Deg0, 0);
            board.Place(workshop, 6, 10, 0, Rotation.Deg0, 0);
            board.WindField = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 3, 8));

            var scoring = new ScoreEngine(board);
            ScoreBreakdown breakdown = scoring.Evaluate(vane, 5, 10, 0, Rotation.Deg0);

            // 3 级风倍率 1.5：居民区 10→15，工坊保持 10
            Assert.AreEqual(15 + 10, breakdown.Total);
        }

        [Test]
        public void 物流风覆盖_建造时吃到且标记()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            BuildingBlueprint farm = MakeBlueprint("farm", baseScore: 5, canCover: true);
            var board = new BuildBoard(MakeMap(), MakeRules(
                new[] { point, farm }, coverRadius: 3, coverScore: 8, extendLength: 0, extendMax: 0));
            board.Place(point, 4, 10, 0, Rotation.Deg0, 0);
            // 风穿过物流点后变物流风，一直吹到 x=12；(11,12) 距物流点 7.3 格（本地覆盖 3 格够不着），
            // 但距物流风路径 (11,10) 只有 2 格——只能靠物流风拿到覆盖
            board.WindField = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 10));

            var scoring = new ScoreEngine(board);
            ScoreBreakdown covered = scoring.Evaluate(farm, 11, 12, 0, Rotation.Deg0);
            Assert.AreEqual(5 + 8, covered.Total);
            Assert.IsTrue(covered.LogisticsCovered);

            ScoreBreakdown uncovered = scoring.Evaluate(farm, 20, 20, 0, Rotation.Deg0);
            Assert.AreEqual(5, uncovered.Total);
            Assert.IsFalse(uncovered.LogisticsCovered);
        }

        // ---------- 风变更时（WindScoreKeeper） ----------

        [Test]
        public void 风变更覆盖_一生一次_再变不重发()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            BuildingBlueprint farm = MakeBlueprint("farm", canCover: true);
            var board = new BuildBoard(MakeMap(), MakeRules(
                new[] { point, farm }, coverRadius: 3, coverScore: 8));
            board.Place(farm, 11, 12, 0, Rotation.Deg0, 0);
            board.Place(point, 4, 10, 0, Rotation.Deg0, 0);

            WindField field = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 10));
            var keeper = new WindScoreKeeper();

            List<WindAward> first = keeper.SettleAfterWindChange(board, field);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(WindAwardKind.LogisticsCoverage, first[0].Kind);
            Assert.AreEqual(8, first[0].Score);

            List<WindAward> second = keeper.SettleAfterWindChange(board, field);
            Assert.AreEqual(0, second.Count, "同一建筑的覆盖分一生只发一次");
        }

        [Test]
        public void 建造时已覆盖_风变更不再重发()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            BuildingBlueprint farm = MakeBlueprint("farm", canCover: true);
            var board = new BuildBoard(MakeMap(), MakeRules(
                new[] { point, farm }, coverRadius: 3, coverScore: 8));
            PlacedBuilding placedFarm = board.Place(farm, 11, 12, 0, Rotation.Deg0, 0);
            board.Place(point, 4, 10, 0, Rotation.Deg0, 0);

            var keeper = new WindScoreKeeper();
            keeper.MarkCoveredAtBuild(placedFarm.Id);

            WindField field = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 10));
            Assert.AreEqual(0, keeper.SettleAfterWindChange(board, field).Count);
        }

        [Test]
        public void 物流点互联_链式相邻配对_每对一生一次()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            var board = new BuildBoard(MakeMap(), MakeRules(
                new[] { point }, coverRadius: 0, coverScore: 0, linkScore: 30));
            PlacedBuilding a = board.Place(point, 4, 10, 0, Rotation.Deg0, 0);
            PlacedBuilding b = board.Place(point, 8, 10, 0, Rotation.Deg0, 0);
            PlacedBuilding c = board.Place(point, 12, 10, 0, Rotation.Deg0, 0);

            WindField field = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 14));
            var keeper = new WindScoreKeeper();

            List<WindAward> awards = keeper.SettleAfterWindChange(board, field);
            Assert.AreEqual(2, awards.Count, "A-B 与 B-C 链式配对，不含 A-C");
            Assert.AreEqual(a.Id, awards[0].BuildingInstanceId);
            Assert.AreEqual(b.Id, awards[0].OtherInstanceId);
            Assert.AreEqual(b.Id, awards[1].BuildingInstanceId);
            Assert.AreEqual(c.Id, awards[1].OtherInstanceId);
            Assert.AreEqual(30, awards[0].Score);
            Assert.IsTrue(awards[0].LinkPathCells.Count >= 2, "互联特效要有路径可画");

            Assert.AreEqual(0, keeper.SettleAfterWindChange(board, field).Count, "每对只计一次");
        }

        [Test]
        public void 风变更后风路离开_已得分不回收()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            BuildingBlueprint farm = MakeBlueprint("farm", canCover: true);
            var board = new BuildBoard(MakeMap(), MakeRules(
                new[] { point, farm }, coverRadius: 3, coverScore: 8));
            board.Place(farm, 11, 12, 0, Rotation.Deg0, 0);
            board.Place(point, 4, 10, 0, Rotation.Deg0, 0);

            var keeper = new WindScoreKeeper();
            WindField withWind = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 10));
            Assert.AreEqual(1, keeper.SettleAfterWindChange(board, withWind).Count);

            // 风改道，物流风不再覆盖农田——不产生任何新账目，更不倒扣
            WindField windGone = Simulate(board, new WindSeed(new CellCoord(2, 2), 0, Dir8.N, 2, 5));
            List<WindAward> after = keeper.SettleAfterWindChange(board, windGone);
            Assert.AreEqual(0, after.Count);
        }

        [Test]
        public void 预览干跑_不消耗一生一次账本_且假设物流点参与互联()
        {
            BuildingBlueprint point = MakeBlueprint(BuildRuleSet.LogisticsPointBuildingId);
            BuildingBlueprint farm = MakeBlueprint("farm", canCover: true);
            var board = new BuildBoard(MakeMap(), MakeRules(
                new[] { point, farm }, coverRadius: 3, coverScore: 8, linkScore: 30));
            board.Place(farm, 11, 12, 0, Rotation.Deg0, 0);
            PlacedBuilding pointA = board.Place(point, 4, 10, 0, Rotation.Deg0, 0);

            WindField field = Simulate(board, new WindSeed(new CellCoord(2, 10), 0, Dir8.E, 2, 10));
            var keeper = new WindScoreKeeper();

            // 假设在 (8,10)（风路上、A 下游）摆一个新物流点
            var virtualPoint = new PlacedBuilding(
                -1, point, 8, 10, 0, Rotation.Deg0, new List<CellCoord> { new CellCoord(8, 10) }, 0);

            List<WindAward> preview = keeper.PreviewWindChange(board, field, virtualPoint);
            Assert.AreEqual(2, preview.Count, "覆盖 1 笔 + 与假设点互联 1 笔");
            Assert.AreEqual(WindAwardKind.LogisticsCoverage, preview[0].Kind);
            Assert.AreEqual(WindAwardKind.LogisticsLink, preview[1].Kind);
            Assert.AreEqual(pointA.Id, preview[1].BuildingInstanceId);
            Assert.AreEqual(-1, preview[1].OtherInstanceId, "下游是还没落地的假设点");

            // 重复预览结果一致——账本没被写
            Assert.AreEqual(2, keeper.PreviewWindChange(board, field, virtualPoint).Count);

            // 正式结算照常发覆盖分：预览没有偷走一生一次的额度
            List<WindAward> settled = keeper.SettleAfterWindChange(board, field);
            Assert.AreEqual(1, settled.Count);
            Assert.AreEqual(WindAwardKind.LogisticsCoverage, settled[0].Kind);
        }

        // ---------- 风源展开（WindSystem） ----------

        [Test]
        public void 风源展开_同种子同风()
        {
            var windSourceDef = new MapElementDef(
                BuildRuleSet.WindSourceElementId, "初始风源",
                Footprint.Parse(new[] { "#" }, "windSource"), 0, false, 2, 4, "");
            var placements = new[]
            {
                new MapElementPlacement(BuildRuleSet.WindSourceElementId, 3, 3, 0, Rotation.Deg0),
                new MapElementPlacement(BuildRuleSet.WindSourceElementId, 15, 15, 0, Rotation.Deg0),
            };
            BuildRuleSet rules = MakeRules(
                new List<BuildingBlueprint>(), new[] { windSourceDef });

            var boardA = new BuildBoard(MakeMap(24, placements), rules);
            var boardB = new BuildBoard(MakeMap(24, placements), rules);
            var systemA = new WindSystem(boardA, 42);
            var systemB = new WindSystem(boardB, 42);

            Assert.AreEqual(2, systemA.Seeds.Count);
            Assert.AreEqual(systemA.Seeds.Count, systemB.Seeds.Count);
            for (int i = 0; i < systemA.Seeds.Count; i++)
            {
                Assert.AreEqual(systemA.Seeds[i].Dir, systemB.Seeds[i].Dir);
                Assert.AreEqual(systemA.Seeds[i].Force, systemB.Seeds[i].Force);
                Assert.AreEqual(systemA.Seeds[i].Length, systemB.Seeds[i].Length);
                Assert.That(systemA.Seeds[i].Force, Is.InRange(1, 3));
                Assert.That(systemA.Seeds[i].Length, Is.InRange(8, 16));
            }
        }
    }
}
