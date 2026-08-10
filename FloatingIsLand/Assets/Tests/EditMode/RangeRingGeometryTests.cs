using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.View;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 范围圆环的形状与领域层判定的等价性。
    ///
    /// 圆环是画给玩家看的"能罩到哪"，如果它和 <see cref="RangeMath"/> 对不上，
    /// 玩家会照着错的图规划——而且这种错不会报任何错，只会让人觉得"计分好像有点玄"。
    /// 所以这里逐格比对：环覆盖的格子集合必须和领域层判定在范围内的格子集合完全一致。
    /// </summary>
    public sealed class RangeRingGeometryTests
    {
        /// <summary>配表里真实存在的几种占地：1×1、2×2、1×2、3×3、6×6、L 形、凹形。</summary>
        private static readonly string[][] Shapes =
        {
            new[] { "#" },
            new[] { "##", "##" },
            new[] { "##" },
            new[] { "###", "###", "###" },
            new[] { "######", "######", "######", "######", "######", "######" },
            new[] { "###", "#.." },
            new[] { "#.#", "###" },
        };

        private static List<CellCoord> CellsOf(string[] mask, int x, int z, Rotation rotation)
        {
            var cells = new List<CellCoord>();
            Footprint.Parse(mask, "test").GetCells(x, z, rotation, cells);
            return cells;
        }

        [Test]
        public void 环覆盖的格子与领域层判定逐点一致()
        {
            var boxes = new List<RangeRingGeometry.CellBox>();
            int[] radii = { 3, 5, 6, 7, 10 };

            foreach (string[] mask in Shapes)
            {
                for (int r = 0; r < 4; r++)
                {
                    var rotation = (Rotation)r;
                    List<CellCoord> cells = CellsOf(mask, 20, 20, rotation);
                    RangeRingGeometry.BuildBoxes(cells, boxes);

                    foreach (int radius in radii)
                    {
                        for (int z = 0; z < 40; z++)
                        {
                            for (int x = 0; x < 40; x++)
                            {
                                var probe = new List<CellCoord> { new CellCoord(x, z) };
                                bool byDomain = RangeMath.InRange(cells, 0, probe, 0, radius, 1f);
                                bool byRing = RangeRingGeometry.DistanceToBoxes(boxes, x, z) <= radius + 1e-4f;

                                Assert.AreEqual(byDomain, byRing,
                                    $"占地 [{string.Join("|", mask)}] 朝向 {(int)rotation * 90}° 半径 {radius} " +
                                    $"在格 ({x}, {z}) 上判定不一致");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void 满矩形占地只拆出一个盒子()
        {
            var boxes = new List<RangeRingGeometry.CellBox>();

            RangeRingGeometry.BuildBoxes(CellsOf(new[] { "#" }, 3, 3, Rotation.Deg0), boxes);
            Assert.AreEqual(1, boxes.Count);

            RangeRingGeometry.BuildBoxes(CellsOf(new[] { "######", "######", "######", "######", "######", "######" }, 3, 3, Rotation.Deg0), boxes);
            Assert.AreEqual(1, boxes.Count, "6×6 满占地该合成一个盒子，拆碎了 shader 的定长数组会不够用");
        }

        [Test]
        public void 异形占地拆出的盒子数不超过上限()
        {
            var boxes = new List<RangeRingGeometry.CellBox>();
            foreach (string[] mask in Shapes)
            {
                for (int r = 0; r < 4; r++)
                {
                    RangeRingGeometry.BuildBoxes(CellsOf(mask, 5, 5, (Rotation)r), boxes);
                    Assert.LessOrEqual(boxes.Count, RangeRingGeometry.MaxBoxes,
                        $"占地 [{string.Join("|", mask)}] 拆出了 {boxes.Count} 个盒子，超过 shader 数组长度");
                    Assert.Greater(boxes.Count, 0);
                }
            }
        }

        [Test]
        public void 占地格自身的距离为零()
        {
            var boxes = new List<RangeRingGeometry.CellBox>();
            List<CellCoord> cells = CellsOf(new[] { "###", "#.." }, 10, 10, Rotation.Deg0);
            RangeRingGeometry.BuildBoxes(cells, boxes);

            for (int i = 0; i < cells.Count; i++)
            {
                Assert.AreEqual(0f, RangeRingGeometry.DistanceToBoxes(boxes, cells[i].X, cells[i].Z), 1e-4f,
                    "占地格自己到占地的距离该是 0——环的中心必须是透明的");
            }
        }

        [Test]
        public void 空占地不产出盒子()
        {
            var boxes = new List<RangeRingGeometry.CellBox>();
            RangeRingGeometry.BuildBoxes(new List<CellCoord>(), boxes);
            Assert.AreEqual(0, boxes.Count);
        }
    }
}
