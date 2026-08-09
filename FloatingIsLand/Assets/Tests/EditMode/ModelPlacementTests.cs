using System.Collections.Generic;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.View;
using NUnit.Framework;
using UnityEngine;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 模型摆放位姿与占地格的对齐契约。
    ///
    /// 生成 Prefab 时模型被塞进一个 Columns×Rows 的占地矩形里（XZ 居中、底面贴地），包装根原点 = 该矩形的最小角。
    /// 摆放时 Unity 绕 Y 轴旋转会把矩形甩向负半轴（Euler(0,90,0) 把 +X 映射到 -Z），
    /// 而 <see cref="Footprint"/> 的占地始终从锚点向 +X/+Z 展开——差的那一段由
    /// <see cref="ModelSpawner.PlacementPosition"/> 的补偿补上。
    ///
    /// 这里钉两条口径，各自对应一个真踩过的坑：
    ///   · 补偿量错 → 「0° 好好的，一转 90° 模型整个挪到隔壁去了」；
    ///   · 模型在矩形里不居中（比如贴最小角）→ 「转 180° 模型在同一片占地里横跳一整格」。
    /// 两种都是 Scene 视图里很难一眼看出差多少的错。
    /// </summary>
    public sealed class ModelPlacementTests
    {
        private const float CellSize = 2f;
        private const float Tolerance = 1e-4f;

        /// <summary>刻意用非零原点：原点在 0 会让「少加了一段偏移」这类错误碰巧也能通过。</summary>
        private static readonly GridGeometry Geometry =
            new GridGeometry(new Vector3(13f, 0f, -7f), 64, 64, CellSize, CellSize, false);

        private const int AnchorX = 5;
        private const int AnchorZ = 9;

        /// <summary>
        /// 模拟生成器产出的 Prefab 局部几何：模型在 Columns×Rows 的占地矩形里**居中**，
        /// 按 fill 比例欠填充。min() 缩放注定至少有一轴填不满，所以 fill&lt;1 才是常态而非特例。
        /// </summary>
        private static Bounds LocalModelBounds(Footprint footprint, float fillX, float fillZ)
        {
            float w = footprint.Columns * CellSize;
            float d = footprint.Rows * CellSize;
            return new Bounds(
                new Vector3(w * 0.5f, 0.5f, d * 0.5f),
                new Vector3(w * fillX, 1f, d * fillZ));
        }

        /// <summary>把局部几何按摆放位姿变换到世界，取 XZ 包围盒。</summary>
        private static Bounds PlacedBounds(Footprint footprint, Rotation rotation, Bounds local)
        {
            Vector3 corner = Geometry.CellCorner(AnchorX, AnchorZ, 0);
            Vector3 position = ModelSpawner.PlacementPosition(corner, rotation, footprint, CellSize);
            Quaternion q = ModelSpawner.PlacementRotation(rotation);

            var corners = new[]
            {
                new Vector3(local.min.x, 0f, local.min.z),
                new Vector3(local.max.x, 0f, local.min.z),
                new Vector3(local.min.x, 0f, local.max.z),
                new Vector3(local.max.x, 0f, local.max.z),
            };

            var bounds = new Bounds(position + q * corners[0], Vector3.zero);
            for (int i = 1; i < corners.Length; i++)
            {
                bounds.Encapsulate(position + q * corners[i]);
            }
            return bounds;
        }

        [Test]
        public void 四个朝向_占地矩形的最小角都落在锚点格的角点上()
        {
            Footprint footprint = Footprint.Parse(new[] { "###", "#.." }, "test");
            Bounds local = LocalModelBounds(footprint, 1f, 1f);
            Vector3 corner = Geometry.CellCorner(AnchorX, AnchorZ, 0);

            for (int r = 0; r < 4; r++)
            {
                Bounds bounds = PlacedBounds(footprint, (Rotation)r, local);
                Assert.AreEqual(corner.x, bounds.min.x, Tolerance, $"旋转 {r * 90}° 后 X 最小角没对到锚点格");
                Assert.AreEqual(corner.z, bounds.min.z, Tolerance, $"旋转 {r * 90}° 后 Z 最小角没对到锚点格");
            }
        }

        [Test]
        public void 四个朝向_占地矩形大小与旋转后跨度一致()
        {
            Footprint footprint = Footprint.Parse(new[] { "###", "#.." }, "test");
            Bounds local = LocalModelBounds(footprint, 1f, 1f);

            for (int r = 0; r < 4; r++)
            {
                var rotation = (Rotation)r;
                Bounds bounds = PlacedBounds(footprint, rotation, local);
                Assert.AreEqual(footprint.SpanX(rotation) * CellSize, bounds.size.x, Tolerance,
                    $"旋转 {r * 90}° 后 X 跨度对不上");
                Assert.AreEqual(footprint.SpanZ(rotation) * CellSize, bounds.size.z, Tolerance,
                    $"旋转 {r * 90}° 后 Z 跨度对不上");
            }
        }

        [Test]
        public void 欠填充的模型_四个朝向下都停在占地正中不横跳()
        {
            // 2×1 占地 + 只填一半宽：这正是 miningStation_01 的实际情形（min() 缩放后 X 只占 50%）。
            // 如果生成 Prefab 时把模型贴在占地最小角而不是居中，0° 和 180° 会差整整一格。
            Footprint footprint = Footprint.Parse(new[] { "##" }, "test");
            Bounds local = LocalModelBounds(footprint, 0.5f, 1f);
            Vector3 corner = Geometry.CellCorner(AnchorX, AnchorZ, 0);

            for (int r = 0; r < 4; r++)
            {
                var rotation = (Rotation)r;
                Bounds bounds = PlacedBounds(footprint, rotation, local);
                float expectedX = corner.x + footprint.SpanX(rotation) * CellSize * 0.5f;
                float expectedZ = corner.z + footprint.SpanZ(rotation) * CellSize * 0.5f;

                Assert.AreEqual(expectedX, bounds.center.x, Tolerance, $"旋转 {r * 90}° 后模型在 X 上偏出了占地中心");
                Assert.AreEqual(expectedZ, bounds.center.z, Tolerance, $"旋转 {r * 90}° 后模型在 Z 上偏出了占地中心");
            }
        }

        [Test]
        public void 欠填充的模型_四个朝向下都不越出占地矩形()
        {
            Footprint footprint = Footprint.Parse(new[] { "####", "#..." }, "test");
            Bounds local = LocalModelBounds(footprint, 0.4f, 1f);
            Vector3 corner = Geometry.CellCorner(AnchorX, AnchorZ, 0);

            for (int r = 0; r < 4; r++)
            {
                var rotation = (Rotation)r;
                Bounds bounds = PlacedBounds(footprint, rotation, local);

                Assert.GreaterOrEqual(bounds.min.x, corner.x - Tolerance, $"旋转 {r * 90}°：越出占地 -X 边");
                Assert.GreaterOrEqual(bounds.min.z, corner.z - Tolerance, $"旋转 {r * 90}°：越出占地 -Z 边");
                Assert.LessOrEqual(bounds.max.x, corner.x + footprint.SpanX(rotation) * CellSize + Tolerance,
                    $"旋转 {r * 90}°：越出占地 +X 边");
                Assert.LessOrEqual(bounds.max.z, corner.z + footprint.SpanZ(rotation) * CellSize + Tolerance,
                    $"旋转 {r * 90}°：越出占地 +Z 边");
            }
        }

        [Test]
        public void 四个朝向_每个占用格的格心都落在占地矩形里()
        {
            // 非方形 + 异形：方形占地会让 90° 的偏移错误自己抵消掉，测不出问题
            Footprint footprint = Footprint.Parse(new[] { "####", "#..." }, "test");
            Bounds local = LocalModelBounds(footprint, 1f, 1f);
            var cells = new List<CellCoord>();

            for (int r = 0; r < 4; r++)
            {
                var rotation = (Rotation)r;
                Bounds bounds = PlacedBounds(footprint, rotation, local);
                footprint.GetCells(AnchorX, AnchorZ, rotation, cells);

                foreach (CellCoord cell in cells)
                {
                    Vector3 center = Geometry.CellCenter(cell.X, cell.Z, 0);
                    Assert.GreaterOrEqual(center.x, bounds.min.x - Tolerance, $"旋转 {r * 90}°：格 {cell} 在占地左边界外");
                    Assert.LessOrEqual(center.x, bounds.max.x + Tolerance, $"旋转 {r * 90}°：格 {cell} 在占地右边界外");
                    Assert.GreaterOrEqual(center.z, bounds.min.z - Tolerance, $"旋转 {r * 90}°：格 {cell} 在占地下边界外");
                    Assert.LessOrEqual(center.z, bounds.max.z + Tolerance, $"旋转 {r * 90}°：格 {cell} 在占地上边界外");
                }
            }
        }

        [Test]
        public void 零度朝向_不做任何补偿()
        {
            Footprint footprint = Footprint.Parse(new[] { "##", "#." }, "test");
            Vector3 corner = Geometry.CellCorner(AnchorX, AnchorZ, 0);

            Assert.AreEqual(corner, ModelSpawner.PlacementPosition(corner, Rotation.Deg0, footprint, CellSize));
        }

        [Test]
        public void 没有占地定义时_直接摆在格角点上()
        {
            Vector3 corner = Geometry.CellCorner(AnchorX, AnchorZ, 0);

            for (int r = 0; r < 4; r++)
            {
                Assert.AreEqual(corner, ModelSpawner.PlacementPosition(corner, (Rotation)r, null, CellSize),
                    $"旋转 {r * 90}°：没有 footprint 时不该有补偿");
            }
        }

        [Test]
        public void 旋转90度把正X轴甩到负Z轴_这正是补偿存在的原因()
        {
            // 补偿公式整个建立在这条映射上；哪天 Rotation.ToDegrees() 改了符号，这里会先炸
            Vector3 turned = ModelSpawner.PlacementRotation(Rotation.Deg90) * Vector3.right;

            Assert.AreEqual(0f, turned.x, Tolerance);
            Assert.AreEqual(-1f, turned.z, Tolerance, "Euler(0,90,0) 应当把 +X 映射到 -Z");
        }
    }
}
