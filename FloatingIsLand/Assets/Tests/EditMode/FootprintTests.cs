using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 异形占地掩码与旋转的契约测试。
    ///
    /// 这块最容易悄悄错：旋转后如果不把偏移平移回第一象限，模型会整体飘出光标所在格，
    /// 而且是「看起来差不多、实际差一格」的那种错，靠肉眼在 Scene 视图里很难发现。
    /// </summary>
    public sealed class FootprintTests
    {
        private static List<CellCoord> CellsOf(Footprint footprint, int x, int z, Rotation rotation)
        {
            var cells = new List<CellCoord>();
            footprint.GetCells(x, z, rotation, cells);
            return cells;
        }

        [Test]
        public void 解析_2x2_掩码得到四个占用格()
        {
            Footprint footprint = Footprint.Parse(new[] { "##", "##" }, "test");

            Assert.AreEqual(2, footprint.Columns);
            Assert.AreEqual(2, footprint.Rows);
            Assert.AreEqual(4, footprint.CellCount);
        }

        [Test]
        public void 解析_L形_只占三格且空位不占()
        {
            // "###" / "#.." → 掩码最后一行贴 z 最小侧
            Footprint footprint = Footprint.Parse(new[] { "###", "#.." }, "test");
            List<CellCoord> cells = CellsOf(footprint, 0, 0, Rotation.Deg0);

            Assert.AreEqual(4, cells.Count);
            CollectionAssert.Contains(cells, new CellCoord(0, 0)); // 第二行的 '#'
            CollectionAssert.Contains(cells, new CellCoord(0, 1));
            CollectionAssert.Contains(cells, new CellCoord(1, 1));
            CollectionAssert.Contains(cells, new CellCoord(2, 1));
            CollectionAssert.DoesNotContain(cells, new CellCoord(1, 0));
            CollectionAssert.DoesNotContain(cells, new CellCoord(2, 0));
        }

        [Test]
        public void 掩码行长度不一致_当场抛异常()
        {
            Assert.Throws<System.ArgumentException>(() => Footprint.Parse(new[] { "##", "#" }, "test"));
        }

        [Test]
        public void 掩码没有任何占用格_当场抛异常()
        {
            Assert.Throws<System.ArgumentException>(() => Footprint.Parse(new[] { "..", ".." }, "test"));
        }

        [Test]
        public void 掩码含非法字符_当场抛异常()
        {
            Assert.Throws<System.ArgumentException>(() => Footprint.Parse(new[] { "#x" }, "test"));
        }

        [Test]
        public void 旋转不改变占用格数量()
        {
            Footprint footprint = Footprint.Parse(new[] { "###", "#.." }, "test");

            for (int r = 0; r < 4; r++)
            {
                Assert.AreEqual(4, CellsOf(footprint, 5, 7, (Rotation)r).Count, $"旋转 {r * 90}° 后格数变了");
            }
        }

        [Test]
        public void 旋转后占用格仍从锚点起算_不越出跨度()
        {
            Footprint footprint = Footprint.Parse(new[] { "###", "#.." }, "test");

            for (int r = 0; r < 4; r++)
            {
                var rotation = (Rotation)r;
                List<CellCoord> cells = CellsOf(footprint, 10, 20, rotation);
                int spanX = footprint.SpanX(rotation);
                int spanZ = footprint.SpanZ(rotation);

                foreach (CellCoord cell in cells)
                {
                    Assert.GreaterOrEqual(cell.X, 10, $"旋转 {r * 90}° 后 X 跑到锚点左边了");
                    Assert.GreaterOrEqual(cell.Z, 20, $"旋转 {r * 90}° 后 Z 跑到锚点下边了");
                    Assert.Less(cell.X, 10 + spanX, $"旋转 {r * 90}° 后 X 越出跨度");
                    Assert.Less(cell.Z, 20 + spanZ, $"旋转 {r * 90}° 后 Z 越出跨度");
                }
            }
        }

        [Test]
        public void 非方形占地旋转90度后跨度互换()
        {
            Footprint footprint = Footprint.Parse(new[] { "##" }, "test"); // 2 列 1 行

            Assert.AreEqual(2, footprint.SpanX(Rotation.Deg0));
            Assert.AreEqual(1, footprint.SpanZ(Rotation.Deg0));
            Assert.AreEqual(1, footprint.SpanX(Rotation.Deg90));
            Assert.AreEqual(2, footprint.SpanZ(Rotation.Deg90));
        }

        [Test]
        public void 旋转四次回到原状()
        {
            Footprint footprint = Footprint.Parse(new[] { "#.#", "###" }, "test");
            List<CellCoord> original = CellsOf(footprint, 3, 4, Rotation.Deg0);
            List<CellCoord> turned = CellsOf(footprint, 3, 4, Rotation.Deg0.Step(4));

            CollectionAssert.AreEquivalent(original, turned);
        }

        [Test]
        public void 旋转步进为负数时规约到合法区间()
        {
            Assert.AreEqual(Rotation.Deg270, Rotation.Deg0.Step(-1));
            Assert.AreEqual(Rotation.Deg180, Rotation.Deg90.Step(-3));
        }
    }
}
