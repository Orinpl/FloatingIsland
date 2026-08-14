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

    /// <summary>
    /// 双指手势的归类。三者互斥：一次双指手势从头到尾只走一条通道。
    ///
    /// 不互斥的话三个量会同时出：玩家想转视角，结果转的同时镜头还在悄悄升高、悄悄拉近，
    /// 因为人手不可能画出纯粹的旋转。互斥之后每个手势的结果都是可预期的。
    /// </summary>
    public enum TwoFingerMode
    {
        /// <summary>还分不清玩家想干什么（位移没到门槛）。这一帧三个通道都不出量。</summary>
        None = 0,

        /// <summary>往外拉 / 往里缩：缩放。</summary>
        Pinch = 1,

        /// <summary>两指反向划（左上右下那种）：绕支点转视角。</summary>
        Twist = 2,

        /// <summary>两指同向划：横向滑屏、纵向拉高拉低镜头。</summary>
        Drag = 3,
    }

    /// <summary>一帧解析出的手势量。全部是「相对上一帧的增量」，直接乘系数用。</summary>
    public readonly struct TouchGesture
    {
        /// <summary>单指拖动位移（像素）。过了点击阈值才有值，否则点一下画面也会跟着抖。</summary>
        public readonly Vector2 Pan;

        /// <summary>双指间距变化（像素，张开为正）。只在 <see cref="Mode"/> 为 Pinch 时有值。</summary>
        public readonly float PinchDelta;

        /// <summary>双指连线的转角变化（度，逆时针为正）。只在 <see cref="Mode"/> 为 Twist 时有值。</summary>
        public readonly float TwistDegrees;

        /// <summary>双指中点位移（像素）。只在 <see cref="Mode"/> 为 Drag 时有值。</summary>
        public readonly Vector2 TwoFingerDrag;

        /// <summary>本次双指手势被归成了哪一类。三条通道互斥，同一时刻只有一条出量。</summary>
        public readonly TwoFingerMode Mode;

        /// <summary>
        /// 分类锁定那一刻两指相距多少像素；<see cref="Mode"/> 为 None 时是 0。
        ///
        /// 取锁定时的快照而不是每帧现算：调用方拿它分流「两指靠得近 = 滑屏 / 张得开 = 升降」，
        /// 现算的话手在滑动过程中稍微张合一点，行为就会在两者之间来回跳。
        /// </summary>
        public readonly float Spread;

        /// <summary>本帧完成了一次点击。</summary>
        public readonly bool Tap;

        /// <summary>点击位置；<see cref="Tap"/> 为 false 时无意义。</summary>
        public readonly Vector2 TapPosition;

        /// <summary>
        /// 单指刚落到屏幕上的那一帧（0 → 1 根手指）。
        ///
        /// 和 <see cref="Pan"/> 分开报是有用的：<see cref="Pan"/> 要等累计位移过了点击阈值才出量，
        /// 也就是说手指落下与「开始拖」之间必然隔着至少一帧。玩法层要在这一帧就决定
        /// 「这一次拖归建筑还是归相机」，才能赶在第一个平移量产生之前把归属定下来
        /// （否则画面会先跳一帧再交接）。
        /// </summary>
        public readonly bool PressBegan;

        /// <summary>手指全部离开屏幕的那一帧（>0 → 0 根手指）。</summary>
        public readonly bool PressEnded;

        /// <summary>当前主手指的屏幕位置；抬手那一帧是离开前的最后位置。</summary>
        public readonly Vector2 Position;

        /// <summary>这一帧屏幕上还有没有手指。</summary>
        public readonly bool HasFinger;

        public TouchGesture(Vector2 pan, float pinchDelta, float twistDegrees, Vector2 twoFingerDrag,
            TwoFingerMode mode, float spread, bool tap, Vector2 tapPosition,
            bool pressBegan, bool pressEnded, Vector2 position, bool hasFinger)
        {
            Pan = pan;
            PinchDelta = pinchDelta;
            TwistDegrees = twistDegrees;
            TwoFingerDrag = twoFingerDrag;
            Mode = mode;
            Spread = spread;
            Tap = tap;
            TapPosition = tapPosition;
            PressBegan = pressBegan;
            PressEnded = pressEnded;
            Position = position;
            HasFinger = hasFinger;
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
    ///   否则第二根手指刚落下的瞬间会窜出一个巨大的假捏合。三者**互斥**，见
    ///   <see cref="TwoFingerMode"/>：一次双指手势起手时先攒够位移分清意图，之后就锁死在那条通道上。
    /// </summary>
    public sealed class TouchGestureTracker
    {
        /// <summary>手指累计位移超过这个像素数就算拖动，不再算点击。由调用方按屏幕 DPI 折算。</summary>
        public float TapSlopPixels = 10f;

        /// <summary>按住超过这么久再抬手算长按，不算点击。</summary>
        public float TapMaxSeconds = 0.6f;

        /// <summary>
        /// 双指三选一的判定门槛（像素）。三条通道各自累计到这个量，最大的那条胜出并锁定。
        /// 由调用方按屏幕 DPI 折算。
        ///
        /// 给得太小会在起手的抖动里瞎猜（想缩放却判成了转），太大则手势有明显的迟滞感。
        /// </summary>
        public float TwoFingerModeSlopPixels = 12f;

        private int _prevCount;
        private bool _gestureActive;
        private float _gestureStartTime;
        private float _travel;
        private bool _multiFinger;
        private Vector2 _lastPosition;

        /// <summary>上一帧的双指间距；负数 = 还没有基准（这一帧只记不出量）。</summary>
        private float _prevPinchDistance = -1f;
        private float _prevPinchAngle;

        /// <summary>本次双指手势锁定的通道；None = 还没分清。手指少于两根时清空。</summary>
        private TwoFingerMode _twoFingerMode;

        /// <summary>锁定那一刻的两指间距（像素）。</summary>
        private float _twoFingerSpread;

        // 三条通道各自的累计位移，全部折算成像素后才能横向比大小。
        private float _accPinch;
        private float _accTwist;
        private float _accDrag;

        /// <summary>回到没有任何手势在进行的状态。场景切换 / 输入模式切换时调。</summary>
        public void Reset()
        {
            _prevCount = 0;
            _gestureActive = false;
            _travel = 0f;
            _multiFinger = false;
            _prevPinchDistance = -1f;
            ResetTwoFinger();
        }

        private void ResetTwoFinger()
        {
            _twoFingerMode = TwoFingerMode.None;
            _twoFingerSpread = 0f;
            _accPinch = 0f;
            _accTwist = 0f;
            _accDrag = 0f;
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
            bool pressBegan = count == 1 && _prevCount == 0;
            if (pressBegan)
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
                    // 中点位移 / 间距变化 / 连线转角 是双指运动的一组正交分解，先各算各的
                    float rawPinch = distance - _prevPinchDistance;
                    float rawTwist = Mathf.DeltaAngle(_prevPinchAngle, angle);
                    Vector2 rawDrag = (frame.Delta0 + frame.Delta1) * 0.5f;

                    if (_twoFingerMode == TwoFingerMode.None)
                    {
                        ClassifyTwoFinger(rawPinch, rawTwist, rawDrag, distance);
                    }

                    // 锁定之后只放行胜出的那一条：手画不出纯粹的旋转，不掐掉另外两条的话
                    // 「转视角」会连带把镜头拉近、升高
                    switch (_twoFingerMode)
                    {
                        case TwoFingerMode.Pinch:
                            pinch = rawPinch;
                            break;
                        case TwoFingerMode.Twist:
                            twist = rawTwist;
                            break;
                        case TwoFingerMode.Drag:
                            twoFingerDrag = rawDrag;
                            break;
                    }
                }
                _prevPinchDistance = distance;
                _prevPinchAngle = angle;
            }
            else
            {
                _prevPinchDistance = -1f;
                // 松掉一根手指就重新分类：剩下的这根再配上新落下的一根是另一次手势了
                ResetTwoFinger();
            }

            // 手指全抬起：这一次算不算点击
            bool pressEnded = count == 0 && _prevCount > 0;
            if (pressEnded && _gestureActive)
            {
                _gestureActive = false;
                if (!_multiFinger && _travel <= TapSlopPixels && frame.Time - _gestureStartTime <= TapMaxSeconds)
                {
                    tap = true;
                    tapPosition = _lastPosition;
                }
            }

            _prevCount = count;
            return new TouchGesture(
                pan, pinch, twist, twoFingerDrag, _twoFingerMode, _twoFingerSpread, tap, tapPosition,
                pressBegan, pressEnded, _lastPosition, count > 0);
        }

        /// <summary>
        /// 攒够位移之后判定这次双指手势是三类中的哪一类，判完就锁死到手指抬起。
        ///
        /// 三条通道的原始单位不一样（像素 / 像素 / 度），直接比大小没有意义。
        /// 转角按**两指连线的半径**折成弧长再比：同样转 5°，两指张得越开，手指实际划过的
        /// 距离就越长、意图也越明确，弧长恰好表达了这件事。
        /// </summary>
        private void ClassifyTwoFinger(float rawPinch, float rawTwist, Vector2 rawDrag, float distance)
        {
            _accPinch += Mathf.Abs(rawPinch);
            _accTwist += Mathf.Abs(rawTwist) * Mathf.Deg2Rad * (distance * 0.5f);
            _accDrag += rawDrag.magnitude;

            float best = Mathf.Max(_accPinch, Mathf.Max(_accTwist, _accDrag));
            if (best < TwoFingerModeSlopPixels)
            {
                return;
            }

            if (best <= _accPinch)
            {
                _twoFingerMode = TwoFingerMode.Pinch;
            }
            else if (best <= _accTwist)
            {
                _twoFingerMode = TwoFingerMode.Twist;
            }
            else
            {
                _twoFingerMode = TwoFingerMode.Drag;
            }

            // 快照两指间距：调用方据此分流「靠得近 = 滑屏 / 张得开 = 升降」，
            // 定死在锁定这一刻，滑动途中手再张合也不会改判
            _twoFingerSpread = distance;
        }
    }
}
