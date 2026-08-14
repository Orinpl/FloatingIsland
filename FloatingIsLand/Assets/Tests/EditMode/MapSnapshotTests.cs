using System;
using System.Collections.Generic;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Map;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 地形层快照的核心契约：稀疏语义（未刷 = 虚空）、坏数据当场炸、存读 round-trip 一致。
    /// 这三条是"只保留刷过的地块"这个需求的地基，回归了就是地图数据静默损坏。
    /// </summary>
    public sealed class MapSnapshotTests
    {
        private static MapSnapshot Make(params MapCell[] cells)
        {
            return new MapSnapshot(1, 8, 8, 1, cells);
        }

        // ---------- 稀疏语义 ----------

        [Test]
        public void 未刷过的格子不算已刷()
        {
            MapSnapshot map = Make(new MapCell(2, 3, 0, "greenField"));

            Assert.IsTrue(map.IsPainted(2, 3, 0), "刷过的格子应为 true");
            Assert.IsFalse(map.IsPainted(2, 4, 0), "没刷过的格子应为 false（虚空）");
            Assert.AreEqual(1, map.PaintedCount);
        }

        [Test]
        public void 越界坐标一律当虚空而不是抛异常()
        {
            MapSnapshot map = Make(new MapCell(0, 0, 0, "island"));

            Assert.IsFalse(map.IsPainted(-1, 0, 0));
            Assert.IsFalse(map.IsPainted(8, 0, 0), "x == Width 已越界");
            Assert.IsFalse(map.IsPainted(0, 0, 1), "layer 1 越界（本图只有 1 层）");
            Assert.IsNull(map.GetElementIdOrNull(99, 99, 0));
        }

        [Test]
        public void 取地块能拿回刷的地形id()
        {
            MapSnapshot map = Make(
                new MapCell(1, 1, 0, "greenField"),
                new MapCell(2, 2, 0, "floatingZone"));

            Assert.AreEqual("greenField", map.GetElementIdOrNull(1, 1, 0));
            Assert.AreEqual("floatingZone", map.GetElementIdOrNull(2, 2, 0));

            MapCell cell;
            Assert.IsTrue(map.TryGetCell(2, 2, 0, out cell));
            Assert.AreEqual(2, cell.X);
            Assert.AreEqual(2, cell.Z);
            Assert.AreEqual("floatingZone", cell.ElementId);
        }

        [Test]
        public void 空地图合法且一格都没刷()
        {
            MapSnapshot map = Make();

            Assert.AreEqual(0, map.PaintedCount);
            Assert.IsFalse(map.IsPainted(0, 0, 0));
        }

        // ---------- 坏数据当场炸 ----------

        [Test]
        public void 越界地块构造时抛异常()
        {
            Assert.Throws<ArgumentException>(() => Make(new MapCell(8, 0, 0, "island")));
            Assert.Throws<ArgumentException>(() => Make(new MapCell(0, -1, 0, "island")));
        }

        [Test]
        public void 重复坐标构造时抛异常()
        {
            Assert.Throws<ArgumentException>(() => Make(
                new MapCell(3, 3, 0, "island"),
                new MapCell(3, 3, 0, "greenField")));
        }

        [Test]
        public void 空elementId构造时抛异常()
        {
            Assert.Throws<ArgumentException>(() => Make(new MapCell(0, 0, 0, null)));
            Assert.Throws<ArgumentException>(() => Make(new MapCell(0, 0, 0, "")));
        }

        [Test]
        public void 非法尺寸构造时抛异常()
        {
            Assert.Throws<ArgumentException>(() => new MapSnapshot(1, 0, 8, 1, new List<MapCell>()));
            Assert.Throws<ArgumentException>(() => new MapSnapshot(1, 8, 8, 0, new List<MapCell>()));
        }

        // ---------- 存读 round-trip ----------

        [Test]
        public void 存读一轮后数据完全一致()
        {
            var original = new MapSnapshot(7, 16, 12, 1, new[]
            {
                new MapCell(0, 0, 0, "island"),
                new MapCell(15, 11, 0, "greenField"),
                new MapCell(5, 6, 0, "floatingZone"),
            });

            MapSnapshot loaded = MapJson.Load("test", MapJson.Save(original));

            Assert.AreEqual(original.StageId, loaded.StageId);
            Assert.AreEqual(original.Width, loaded.Width);
            Assert.AreEqual(original.Length, loaded.Length);
            Assert.AreEqual(original.LayerCount, loaded.LayerCount);
            Assert.AreEqual(original.PaintedCount, loaded.PaintedCount);

            foreach (MapCell cell in original.Cells)
            {
                Assert.AreEqual(cell.ElementId, loaded.GetElementIdOrNull(cell.X, cell.Z, cell.Layer),
                    $"({cell.X}, {cell.Z}) 的地形在存读后变了");
            }
        }

        [Test]
        public void 存盘顺序稳定以免git全是噪音()
        {
            // 同样的地块、不同的输入顺序，存出来的 JSON 必须逐字节一致
            var a = new MapSnapshot(1, 8, 8, 1, new[]
            {
                new MapCell(5, 5, 0, "island"),
                new MapCell(1, 2, 0, "greenField"),
                new MapCell(3, 1, 0, "floatingZone"),
            });
            var b = new MapSnapshot(1, 8, 8, 1, new[]
            {
                new MapCell(3, 1, 0, "floatingZone"),
                new MapCell(5, 5, 0, "island"),
                new MapCell(1, 2, 0, "greenField"),
            });

            Assert.AreEqual(MapJson.Save(a), MapJson.Save(b));
        }

        [Test]
        public void 空地图也能存读()
        {
            MapSnapshot loaded = MapJson.Load("test", MapJson.Save(Make()));
            Assert.AreEqual(0, loaded.PaintedCount);
        }

        [Test]
        public void 坏json抛带地图名的异常()
        {
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9", ""));
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9", "null"));
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9", "{ not json"));

            // 越界地块：JSON 语法没问题，但 MapSnapshot 的校验要拦下来
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9",
                "{\"stageId\":1,\"width\":4,\"length\":4,\"layerCount\":1,\"cells\":[{\"x\":9,\"z\":0,\"layer\":0,\"e\":\"island\"}]}"));
        }

        // ---------- 风源手工参数（地图编辑器授权，f>0 = 授权标志） ----------

        [Test]
        public void 风源手工参数存读一轮后保持()
        {
            var original = new MapSnapshot(1, 16, 16, 1, new List<MapCell>(), new[]
            {
                // 授权的手工风 + 没授权的随机风混在一张图里
                new MapElementPlacement("windSource", 3, 4, 0, Rotation.Deg0, 2, 4, 18),
                new MapElementPlacement("windSource", 8, 8, 0, Rotation.Deg0),
                new MapElementPlacement("ore", 10, 10, 0, Rotation.Deg90),
            });

            MapSnapshot loaded = MapJson.Load("test", MapJson.Save(original));

            Assert.AreEqual(3, loaded.Elements.Count);
            MapElementPlacement authored = loaded.Elements[0];
            Assert.IsTrue(authored.HasWindParams, "授权风源的手工参数在存读后丢了");
            Assert.AreEqual(2, authored.WindDir);
            Assert.AreEqual(4, authored.WindForce);
            Assert.AreEqual(18, authored.WindLength);

            Assert.IsFalse(loaded.Elements[1].HasWindParams, "未授权风源不该带手工参数");
            Assert.IsFalse(loaded.Elements[2].HasWindParams);
            Assert.AreEqual(Rotation.Deg90, loaded.Elements[2].Rotation);
        }

        [Test]
        public void 无手工参数的元素json里不落风字段()
        {
            var map = new MapSnapshot(1, 8, 8, 1, new List<MapCell>(), new[]
            {
                new MapElementPlacement("ore", 1, 1, 0, Rotation.Deg0),
            });

            string json = MapJson.Save(map);
            StringAssert.DoesNotContain("\"f\"", json, "非风元素的 JSON 不该带风字段");
            StringAssert.DoesNotContain("\"len\"", json);
        }

        [Test]
        public void 老地图不带风字段可以直接读()
        {
            MapSnapshot loaded = MapJson.Load("stage_old",
                "{\"stageId\":1,\"width\":4,\"length\":4,\"layerCount\":1,\"cells\":[]," +
                "\"elements\":[{\"x\":1,\"z\":1,\"layer\":0,\"e\":\"windSource\",\"r\":0}]}");

            Assert.AreEqual(1, loaded.Elements.Count);
            Assert.IsFalse(loaded.Elements[0].HasWindParams);
        }

        // ---------- 逐层高度（地图编辑器逐层填写，贴合岛屿台地） ----------

        [Test]
        public void 逐层高度存读一轮后保持()
        {
            var original = new MapSnapshot(1, 8, 8, 3, new List<MapCell>(), null, new[] { 0f, 3.2f, 4.5f });

            MapSnapshot loaded = MapJson.Load("test", MapJson.Save(original));

            Assert.IsNotNull(loaded.LayerHeights);
            Assert.AreEqual(3, loaded.LayerHeights.Count);
            Assert.AreEqual(0f, loaded.LayerHeights[0], 1e-4f);
            Assert.AreEqual(3.2f, loaded.LayerHeights[1], 1e-4f);
            Assert.AreEqual(4.5f, loaded.LayerHeights[2], 1e-4f);

            // 换算成计分用的格数：高度差 ÷ 格边长
            float[] grids = loaded.LayerHeightsInGrids(2f);
            Assert.AreEqual(1.6f, grids[1], 1e-4f);
        }

        [Test]
        public void 老地图不带逐层高度可以直接读()
        {
            MapSnapshot loaded = MapJson.Load("test", MapJson.Save(new MapSnapshot(1, 8, 8, 2, new List<MapCell>())));
            Assert.IsNull(loaded.LayerHeights);
            Assert.IsNull(loaded.LayerHeightsInGrids(2f));
        }

        [Test]
        public void 逐层高度条数与层数不符时抛异常()
        {
            Assert.Throws<ArgumentException>(() =>
                new MapSnapshot(1, 8, 8, 2, new List<MapCell>(), null, new[] { 0f, 2f, 4f }));
        }

        [Test]
        public void 非法风参数读取时抛异常()
        {
            // 有风力却没风长
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9",
                "{\"stageId\":1,\"width\":4,\"length\":4,\"layerCount\":1,\"cells\":[]," +
                "\"elements\":[{\"x\":1,\"z\":1,\"layer\":0,\"e\":\"windSource\",\"r\":0,\"f\":3}]}"));

            // 风向超出八向
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9",
                "{\"stageId\":1,\"width\":4,\"length\":4,\"layerCount\":1,\"cells\":[]," +
                "\"elements\":[{\"x\":1,\"z\":1,\"layer\":0,\"e\":\"windSource\",\"r\":0,\"d\":8,\"f\":3,\"len\":10}]}"));

            // 负风力
            Assert.Throws<InvalidOperationException>(() => MapJson.Load("stage_9",
                "{\"stageId\":1,\"width\":4,\"length\":4,\"layerCount\":1,\"cells\":[]," +
                "\"elements\":[{\"x\":1,\"z\":1,\"layer\":0,\"e\":\"windSource\",\"r\":0,\"f\":-1}]}"));
        }
    }
}
