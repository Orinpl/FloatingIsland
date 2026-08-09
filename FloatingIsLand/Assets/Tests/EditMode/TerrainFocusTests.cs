using FloatingIsLand.View;
using NUnit.Framework;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 建造时地形网格「只亮光标附近几圈 + 边缘渐隐」的曲线契约。
    ///
    /// 这条曲线只能靠肉眼在 Game 视图里看，一旦被顺手改成 (rings - ring) / rings
    /// 就会变成第 rings 圈直接归零、少显示一圈，而且看起来「差不多」——所以在这里钉住。
    /// </summary>
    public sealed class TerrainFocusTests
    {
        [Test]
        public void 光标所在格与第一圈保持满亮()
        {
            Assert.AreEqual(1f, TerrainOverlayRenderer.FocusVisibility(0, 3));
            Assert.AreEqual(1f, TerrainOverlayRenderer.FocusVisibility(1, 3));
        }

        [Test]
        public void 三圈之内都还看得见()
        {
            // 第 2、3 圈递减但不为 0：正好就是用户要的「附近 3 圈可见」
            Assert.Greater(TerrainOverlayRenderer.FocusVisibility(2, 3), 0f);
            Assert.Greater(TerrainOverlayRenderer.FocusVisibility(3, 3), 0f);
            Assert.Less(TerrainOverlayRenderer.FocusVisibility(3, 3),
                        TerrainOverlayRenderer.FocusVisibility(2, 3));
        }

        [Test]
        public void 第四圈起完全不显示()
        {
            Assert.AreEqual(0f, TerrainOverlayRenderer.FocusVisibility(4, 3));
            Assert.AreEqual(0f, TerrainOverlayRenderer.FocusVisibility(50, 3));
        }

        [Test]
        public void 圈数配成零时只亮光标那一格()
        {
            Assert.AreEqual(1f, TerrainOverlayRenderer.FocusVisibility(0, 0));
            Assert.AreEqual(0f, TerrainOverlayRenderer.FocusVisibility(1, 0));
        }
    }
}
