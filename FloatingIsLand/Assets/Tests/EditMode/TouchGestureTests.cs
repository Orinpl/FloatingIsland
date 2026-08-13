using FloatingIsLand.GameInput;
using NUnit.Framework;
using UnityEngine;

namespace FloatingIsLand.Tests
{
    /// <summary>
    /// 触屏手势判定。真实手指没法在 EditMode 里模拟，所以判定逻辑被抽成了纯状态机
    /// （<see cref="TouchGestureTracker"/>），这里逐帧喂合成输入把边界钉住。
    ///
    /// 最要紧的是**误建防线**：手机上一次误触就是一栋楼，而楼落地即结算、不可回退
    /// （见 BuildBoardTests 的「已落地建筑的分数不回溯」）。所以「什么不算点击」的用例
    /// 比「什么算点击」还多——拖过、按久、中途多一根手指，任一条都必须判否。
    /// </summary>
    public sealed class TouchGestureTests
    {
        private const float Slop = 10f;

        private static TouchGestureTracker NewTracker()
        {
            return new TouchGestureTracker { TapSlopPixels = Slop, TapMaxSeconds = 0.6f };
        }

        [Test]
        public void 按下原地抬手算一次点击()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0.05f));
            TouchGesture lift = tracker.Step(TouchFrame.None(0.1f));

            Assert.IsTrue(lift.Tap, "原地按一下抬手必须算点击");
            Assert.AreEqual(finger, lift.TapPosition, "点击位置应是抬手前最后一帧的手指位置");
        }

        [Test]
        public void 拖过阈值再抬手不算点击()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            finger += new Vector2(30f, 0f);
            tracker.Step(TouchFrame.One(finger, new Vector2(30f, 0f), 0.05f));
            TouchGesture lift = tracker.Step(TouchFrame.None(0.1f));

            Assert.IsFalse(lift.Tap, "拖了地图之后抬手不能顺手建一栋楼");
        }

        [Test]
        public void 位移按累计算而不是首尾直线距离()
        {
            // 手指画个来回回到原点：首尾距离是 0，但玩家的意图明显是拖动而不是点击。
            // 这条如果按直线距离判，慢慢蹭着拖回原点就会误触发建造。
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            tracker.Step(TouchFrame.One(finger + new Vector2(8f, 0f), new Vector2(8f, 0f), 0.05f));
            tracker.Step(TouchFrame.One(finger, new Vector2(-8f, 0f), 0.1f));
            TouchGesture lift = tracker.Step(TouchFrame.None(0.15f));

            Assert.IsFalse(lift.Tap, "累计位移 16 已超过阈值 10，即使回到原点也不算点击");
        }

        [Test]
        public void 按太久不算点击()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            tracker.Step(TouchFrame.One(finger, Vector2.zero, 1.2f));
            TouchGesture lift = tracker.Step(TouchFrame.None(1.3f));

            Assert.IsFalse(lift.Tap, "长按超过上限算别的意图，不是点击");
        }

        [Test]
        public void 中途多一根手指就不再算点击()
        {
            // 双指缩放结束时两根手指往往不同时离开屏幕，最后那根抬起的位移可能极小。
            // 不记「这次手势曾经是多指」的话，缩放完就会凭空建一栋楼。
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);

            tracker.Step(TouchFrame.One(a, Vector2.zero, 0f));
            tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0.05f));
            tracker.Step(TouchFrame.One(a, Vector2.zero, 0.1f));
            TouchGesture lift = tracker.Step(TouchFrame.None(0.15f));

            Assert.IsFalse(lift.Tap, "这一次手势里出现过第二根手指，抬手不能算点击");
        }

        [Test]
        public void 两指同时落下再抬起不算点击()
        {
            // 0 → 2 根本没开始过单指手势，抬手时不该冒出一次点击
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);

            tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0f));
            TouchGesture lift = tracker.Step(TouchFrame.None(0.1f));

            Assert.IsFalse(lift.Tap);
        }

        [Test]
        public void 单指位移未过阈值前不输出平移()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            TouchGesture small = tracker.Step(TouchFrame.One(finger + new Vector2(4f, 0f), new Vector2(4f, 0f), 0.02f));
            Assert.AreEqual(Vector2.zero, small.Pan, "点一下时画面不能跟着抖");

            TouchGesture past = tracker.Step(
                TouchFrame.One(finger + new Vector2(20f, 0f), new Vector2(16f, 0f), 0.04f));
            Assert.AreEqual(new Vector2(16f, 0f), past.Pan, "累计过了阈值之后每帧的位移就是平移量");
        }

        [Test]
        public void 双指第一帧只记基准不出捏合量()
        {
            // 第二根手指落下的那一帧，两指间距是从「无」跳到 200——直接当成捏合会让镜头瞬移
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);

            TouchGesture first = tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0f));

            Assert.AreEqual(0f, first.PinchDelta, 1e-4f);
            Assert.AreEqual(0f, first.TwistDegrees, 1e-4f);
        }

        [Test]
        public void 双指张开为正靠拢为负()
        {
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);

            tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0f));

            var spread = new Vector2(650f, 300f);
            TouchGesture apart = tracker.Step(
                TouchFrame.Two(a, Vector2.zero, spread, new Vector2(50f, 0f), 0.02f));
            Assert.AreEqual(50f, apart.PinchDelta, 1e-3f, "张开 50 像素 → +50");

            TouchGesture together = tracker.Step(
                TouchFrame.Two(a, Vector2.zero, b, new Vector2(-50f, 0f), 0.04f));
            Assert.AreEqual(-50f, together.PinchDelta, 1e-3f, "收回 50 像素 → -50");
        }

        [Test]
        public void 双指转动输出转角()
        {
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var right = new Vector2(600f, 300f);
            var up = new Vector2(400f, 500f);

            tracker.Step(TouchFrame.Two(a, Vector2.zero, right, Vector2.zero, 0f));
            // 第二根手指从正右转到正上 = 逆时针 90°
            TouchGesture turned = tracker.Step(TouchFrame.Two(a, Vector2.zero, up, up - right, 0.02f));

            Assert.AreEqual(90f, turned.TwistDegrees, 1e-2f);
        }

        [Test]
        public void 双指同向拖输出中点位移()
        {
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);
            var move = new Vector2(0f, 20f);

            tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0f));
            TouchGesture dragged = tracker.Step(TouchFrame.Two(a + move, move, b + move, move, 0.02f));

            Assert.AreEqual(move, dragged.TwoFingerDrag);
            Assert.AreEqual(0f, dragged.PinchDelta, 1e-3f, "同向平移不该被当成捏合");
        }

        [Test]
        public void 手指落下那一帧报按下并给出位置()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            TouchGesture down = tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));

            Assert.IsTrue(down.PressBegan, "0 → 1 根手指就是一次按下");
            Assert.IsTrue(down.HasFinger);
            Assert.AreEqual(finger, down.Position);

            TouchGesture hold = tracker.Step(TouchFrame.One(finger, Vector2.zero, 0.02f));
            Assert.IsFalse(hold.PressBegan, "按下只在落指那一帧报一次，不是「按着就一直报」");
        }

        [Test]
        public void 按下必定早于第一个平移量至少一帧()
        {
            // 这是「拖建筑」能抢在相机之前认领这次拖动的前提（见 InputArbiter.PanConsumedByGameplay）：
            // 玩法层在落指帧就能定归属，而平移量要等累计位移过阈值才出现。
            // 这条一旦破了，相机会先跟着手指动一帧再交接，表现为建筑拖起来时画面抖一下。
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            TouchGesture down = tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            Assert.IsTrue(down.PressBegan);
            Assert.AreEqual(Vector2.zero, down.Pan, "落指那一帧不可能已经有平移量");

            // 一口气拖过阈值：即便如此，平移也只能在落指之后的帧里出现
            TouchGesture moved = tracker.Step(
                TouchFrame.One(finger + new Vector2(40f, 0f), new Vector2(40f, 0f), 0.02f));
            Assert.IsFalse(moved.PressBegan);
            Assert.AreNotEqual(Vector2.zero, moved.Pan);
        }

        [Test]
        public void 抬手那一帧报抬起且位置是离开前的最后一点()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);
            var moved = finger + new Vector2(40f, 0f);

            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            tracker.Step(TouchFrame.One(moved, new Vector2(40f, 0f), 0.05f));
            TouchGesture lift = tracker.Step(TouchFrame.None(0.1f));

            Assert.IsTrue(lift.PressEnded, ">0 → 0 根手指就是一次抬起");
            Assert.IsFalse(lift.HasFinger);
            Assert.AreEqual(moved, lift.Position, "抬手帧屏幕上已经没有手指，位置得沿用离开前那一点");
        }

        [Test]
        public void 两指同时落下不算按下()
        {
            // 0 → 2 是双指手势起手，不该被当成「单指按在了某栋楼上」而把这次拖动判给建筑
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);

            TouchGesture down = tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0f));

            Assert.IsFalse(down.PressBegan);
        }

        [Test]
        public void 抬起只在手指全部离开时报一次()
        {
            // 双指缩放里松开一根手指不算抬起——手势还在继续，这时候交还拖动归属会让操作断掉
            TouchGestureTracker tracker = NewTracker();
            var a = new Vector2(400f, 300f);
            var b = new Vector2(600f, 300f);

            tracker.Step(TouchFrame.Two(a, Vector2.zero, b, Vector2.zero, 0f));
            TouchGesture oneLeft = tracker.Step(TouchFrame.One(a, Vector2.zero, 0.05f));
            Assert.IsFalse(oneLeft.PressEnded, "还剩一根手指，手势没结束");

            TouchGesture allGone = tracker.Step(TouchFrame.None(0.1f));
            Assert.IsTrue(allGone.PressEnded);
        }

        [Test]
        public void 重置之后上一次手势不会漏进下一次()
        {
            TouchGestureTracker tracker = NewTracker();
            var finger = new Vector2(400f, 300f);

            // 拖到一半（已过阈值）就切场景
            tracker.Step(TouchFrame.One(finger, Vector2.zero, 0f));
            tracker.Step(TouchFrame.One(finger + new Vector2(40f, 0f), new Vector2(40f, 0f), 0.05f));
            tracker.Reset();

            // 新的一局：原地点一下必须还能识别成点击
            tracker.Step(TouchFrame.One(finger, Vector2.zero, 1f));
            TouchGesture lift = tracker.Step(TouchFrame.None(1.05f));

            Assert.IsTrue(lift.Tap, "Reset 之后旧手势的累计位移不能继续算数");
        }
    }
}
