using UnityEngine;

namespace FloatingIsLand.GameInput
{
    /// <summary>
    /// 一帧的原始触摸输入。只带前两根手指——本作没有三指手势，多出来的只用于「不是点击」的判定。
    /// </summary>
    public readonly struct TouchFrame
    {
        /// <summary>按在屏幕上的手指数（可以大于 2，后面的位置不记）。</summary>
        public readonly int Count;

        public readonly Vector2 Position0;
        public readonly Vector2 Delta0;
        public readonly Vector2 Position1;
        public readonly Vector2 Delta1;

        /// <summary>本帧时刻（秒）。用来判长按，取 unscaled 时间——摆放暂停时也要能操作。</summary>
        public readonly float Time;

        public TouchFrame(int count, Vector2 position0, Vector2 delta0, Vector2 position1, Vector2 delta1, float time)
        {
            Count = count;
            Position0 = position0;
            Delta0 = delta0;
            Position1 = position1;
            Delta1 = delta1;
            Time = time;
        }

        public static TouchFrame None(float time)
        {
            return new TouchFrame(0, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, time);
        }

        public static TouchFrame One(Vector2 position, Vector2 delta, float time)
        {
            return new TouchFrame(1, position, delta, Vector2.zero, Vector2.zero, time);
        }

        public static TouchFrame Two(Vector2 p0, Vector2 d0, Vector2 p1, Vector2 d1, float time)
        {
            return new TouchFrame(2, p0, d0, p1, d1, time);
        }
    }

    /// <summary>一帧解析出的手势量。全部是「相对上一帧的增量」，直接乘系数用。</summary>
    public readonly struct TouchGesture
    {
        /// <summary>单指拖动位移（像素）。过了点击阈值才有值，否则点一下画面也会跟着抖。</summary>
        public readonly Vector2 Pan;

        /// <summary>双指间距变化（像素，张开为正）。</summary>
        public readonly float PinchDelta;

        /// <summary>双指连线的转角变化（度，逆时针为正）。</summary>
        public readonly float TwistDegrees;

        /// <summary>双指中点位移（像素）。</summary>
        public readonly Vector2 TwoFingerDrag;

        /// <summary>本帧完成了一次点击。</summary>
        public readonly bool Tap;

        /// <summary>点击位置；<see cref="Tap"/> 为 false 时无意义。</summary>
        public readonly Vector2 TapPosition;

        public TouchGesture(Vector2 pan, float pinchDelta, float twistDegrees, Vector2 twoFingerDrag,
            bool tap, Vector2 tapPosition)
        {
            Pan = pan;
            PinchDelta = pinchDelta;
            TwistDegrees = twistDegrees;
            TwoFingerDrag = twoFingerDrag;
            Tap = tap;
            TapPosition = tapPosition;
        }
    }

    /// <summary>
    /// 触屏手势状态机：喂一帧原始触摸，吐一帧手势量。
    ///
    /// 与设备读取分开，是为了能测：真实手指没法在 EditMode 里模拟，但只要手势判定是纯函数，
    /// 「拖了 3 像素算不算点击」「中途多了一根手指还算不算点击」这类边界就能逐条钉住
    /// （见 TouchGestureTests）。<see cref="PointerInput"/> 只负责把 Touchscreen 翻译成
    /// <see cref="TouchFrame"/>，判定全在这里。
    ///
    /// 判定口径：
    /// - **点击** = 全程单指 + 累计位移不超过阈值 + 按住时间不超过上限。三条缺一不可。
    ///   累计位移而不是首尾直线距离——手指画个圈回到原点，那不是点击。
    /// - **平移** = 单指且已经超过点击阈值。超过之后就一直算拖动，不会因为手停下来又变回点击。
    /// - **捏合 / 转 / 双指拖** = 恰好在有两根及以上手指的帧里输出；第一帧只记基准不出量，
    ///   否则第二根手指刚落下的瞬间会窜出一个巨大的假捏合。
    /// </summary>
    public sealed class TouchGestureTracker
    {
        /// <summary>手指累计位移超过这个像素数就算拖动，不再算点击。由调用方按屏幕 DPI 折算。</summary>
        public float TapSlopPixels = 10f;

        /// <summary>按住超过这么久再抬手算长按，不算点击。</summary>
        public float TapMaxSeconds = 0.6f;

        private int _prevCount;
        private bool _gestureActive;
        private float _gestureStartTime;
        private float _travel;
        private bool _multiFinger;
        private Vector2 _lastPosition;

        /// <summary>上一帧的双指间距；负数 = 还没有基准（这一帧只记不出量）。</summary>
        private float _prevPinchDistance = -1f;
        private float _prevPinchAngle;

        /// <summary>回到没有任何手势在进行的状态。场景切换 / 输入模式切换时调。</summary>
        public void Reset()
        {
            _prevCount = 0;
            _gestureActive = false;
            _travel = 0f;
            _multiFinger = false;
            _prevPinchDistance = -1f;
        }

        public TouchGesture Step(TouchFrame frame)
        {
            Vector2 pan = Vector2.zero;
            float pinch = 0f;
            float twist = 0f;
            Vector2 twoFingerDrag = Vector2.zero;
            bool tap = false;
            Vector2 tapPosition = Vector2.zero;

            int count = frame.Count;
            if (count >= 2)
            {
                _multiFinger = true;
            }
            if (count >= 1)
            {
                _lastPosition = frame.Position0;
            }

            // 0 → 1：一次新手势开始
            if (count == 1 && _prevCount == 0)
            {
                _gestureActive = true;
                _gestureStartTime = frame.Time;
                _travel = 0f;
                _multiFinger = false;
            }

            if (count == 1)
            {
                _travel += frame.Delta0.magnitude;
                if (!_multiFinger && _travel > TapSlopPixels)
                {
                    pan = frame.Delta0;
                }
            }

            if (count >= 2)
            {
                Vector2 span = frame.Position1 - frame.Position0;
                float distance = span.magnitude;
                float angle = Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg;

                if (_prevPinchDistance >= 0f)
                {
                    pinch = distance - _prevPinchDistance;
                    twist = Mathf.DeltaAngle(_prevPinchAngle, angle);
                    twoFingerDrag = (frame.Delta0 + frame.Delta1) * 0.5f;
                }
                _prevPinchDistance = distance;
                _prevPinchAngle = angle;
            }
            else
            {
                _prevPinchDistance = -1f;
            }

            // 手指全抬起：这一次算不算点击
            if (count == 0 && _prevCount > 0 && _gestureActive)
            {
                _gestureActive = false;
                if (!_multiFinger && _travel <= TapSlopPixels && frame.Time - _gestureStartTime <= TapMaxSeconds)
                {
                    tap = true;
                    tapPosition = _lastPosition;
                }
            }

            _prevCount = count;
            return new TouchGesture(pan, pinch, twist, twoFingerDrag, tap, tapPosition);
        }
    }
}
