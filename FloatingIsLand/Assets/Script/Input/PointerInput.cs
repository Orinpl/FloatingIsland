using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace FloatingIsLand.GameInput
{
    /// <summary>指针来源。玩家最近一次输入是用鼠标还是手指——用来决定 HUD 显不显示触屏工具条。</summary>
    public enum PointerKind
    {
        None = 0,
        Mouse = 1,
        Touch = 2,
    }

    /// <summary>
    /// 统一指针 + 触屏手势采样。PC 与手机的输入差异全部收敛在这一个文件里，
    /// 相机、摆放、网格高亮三处调用方只问「指针指着哪」「本帧有没有点一下」「本帧捏了多少」，
    /// 不再各自去读 <c>Mouse.current</c>。
    ///
    /// 做成静态 + 帧内缓存，而不是场景里的一个 MonoBehaviour：本项目的场景是编辑器菜单生成的，
    /// 往里加节点就得重跑那条带模态弹窗的流程（会卡死主线程），为了一个纯采样器不值当。
    /// 帧号缓存保证同一帧内多个调用方读到的是同一份采样（否则先读的人会把 delta 吃掉）。
    ///
    /// 两端的操作映射（PC 侧一个字节都没改）：
    ///
    /// |            | PC              | 手机                    |
    /// |------------|-----------------|-------------------------|
    /// | 移动视角   | WASD / 中键拖   | 单指拖（落点不在建筑上时） |
    /// | 挪动待摆建筑 | 光标移动      | 从建筑上起手单指拖      |
    /// | 缩放       | 滚轮            | 双指捏合                |
    /// | 旋转视角   | 右键拖          | 双指转（偏航）/ 双指上下拖（俯仰） |
    /// | 指向格子   | 光标悬停        | 点一下（点到哪停在哪）  |
    /// | 落地       | 左键            | 再点同一格 / 点「建造」 |
    /// | 转 90°     | 滚轮            | 点「旋转」              |
    /// | 取消       | Esc             | 点「取消」              |
    ///
    /// 触屏没有「悬停」这回事，所以这里把它定义成**黏性的**：手指点过哪一格，指针就停在哪，
    /// 直到下次点击或 <see cref="ClearHover"/>。摆放预览（范围环 / 辉光 / 飘分）因此在手指抬起后
    /// 仍然留在屏幕上——手指不挡视线，玩家才看得清这一下能拿多少分。
    /// </summary>
    public static class PointerInput
    {
        /// <summary>点击判定：手指累计位移超过这个英寸数就算拖动，不再算点击。约等于 Android 的 touch slop。</summary>
        private const float TapSlopInches = 0.06f;

        /// <summary>取不到屏幕 DPI 时的兜底值（Unity 在部分设备/编辑器下返回 0）。</summary>
        private const float FallbackDpi = 160f;

        /// <summary>鼠标位移超过这个像素数才认为「玩家换回鼠标了」，避免手机模拟器里的幽灵光标抢走模式。</summary>
        private const float MouseWakePixels = 2f;

        private static int _sampledFrame = -1;

        private static PointerKind _kind = PointerKind.None;

        private static bool _hasHover;
        private static Vector2 _hoverPosition;

        /// <summary>手势判定全在这里，本类只负责把 Touchscreen 翻译成 TouchFrame 喂进去。</summary>
        private static readonly TouchGestureTracker _gestures = new TouchGestureTracker();

        private static TouchGesture _gesture;

        private static Vector2 _tapPosition;

        /// <summary>上一帧的手指数。手指刚抬起的那一帧仍算触屏活动，否则模式会在抬手瞬间跳回鼠标。</summary>
        private static int _gestureFingersLastFrame;

        private static readonly List<TouchControl> _activeTouches = new List<TouchControl>(4);
        private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>(8);

        // UI 射线的复用载荷。PointerEventData 的 eventSystem 只能在构造时给，
        // 所以连 EventSystem 一起缓存，换了场景（新 EventSystem）再重建。
        private static EventSystem _uiEvents;
        private static PointerEventData _uiPointer;

        private static int _uiCheckedFrame = -1;
        private static bool _hoverOverUi;

        /// <summary>
        /// 编辑器里强制走触屏分支，用来在 PC 上验触屏 HUD 与摆放流程，不必每次都装包上真机。
        /// 打开时会顺带启用 Input System 的触摸模拟（鼠标→手指），否则强制了触屏却一根手指都没有，
        /// 点不出落点，等于把游戏锁死。由 Tools/调试/触屏模拟 菜单切换；真机上永远是 false。
        /// </summary>
        public static bool ForceTouchMode
        {
            get { return _forceTouchMode; }
            set { _forceTouchMode = value; }
        }

        private static bool _forceTouchMode;

#if UNITY_EDITOR
        /// <summary>
        /// 从 EditorPrefs 取回强制触屏开关。必须在静态构造里读：菜单点完之后进 Play 会走一次域重载，
        /// 静态字段全部清零，只靠内存里的 bool 一进 Play 就丢了。
        /// </summary>
        static PointerInput()
        {
            _forceTouchMode = UnityEditor.EditorPrefs.GetBool(ForceTouchPrefKey, false);
        }

        /// <summary>编辑器强制触屏开关的存储键。菜单与本类共用，改名两边一起改。</summary>
        public const string ForceTouchPrefKey = "FloatingIsLand.Input.ForceTouch";

        private static bool _simulationEnabled;

        /// <summary>让触摸模拟跟着强制开关走。只在编辑器里编译，打出去的包里没有这段。</summary>
        private static void SyncTouchSimulation()
        {
            if (_simulationEnabled == _forceTouchMode)
            {
                return;
            }
            _simulationEnabled = _forceTouchMode;
            if (_simulationEnabled)
            {
                UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Enable();
            }
            else
            {
                UnityEngine.InputSystem.EnhancedTouch.TouchSimulation.Disable();
            }
        }
#endif

        /// <summary>最近一次输入来自鼠标还是手指。HUD 据此决定显不显示触屏工具条。</summary>
        public static PointerKind Kind
        {
            get { Sample(); return _kind; }
        }

        /// <summary>当前是否按触屏规则操作（含编辑器强制）。</summary>
        public static bool IsTouchMode
        {
            get { return Kind == PointerKind.Touch; }
        }

        /// <summary>
        /// 指针当前指着屏幕哪里。鼠标 = 光标位置（每帧都有）；触屏 = 最近一次点击的位置（黏性，见类注释）。
        /// 触屏下玩家还没点过任何地方时返回 false。
        /// </summary>
        public static bool TryGetHoverPosition(out Vector2 position)
        {
            Sample();
            position = _hoverPosition;
            return _hasHover;
        }

        /// <summary>本帧完成了一次点击（手指抬起，且没拖动没长按没变成多指）。仅触屏有效。</summary>
        public static bool TapThisFrame
        {
            get { Sample(); return _gesture.Tap; }
        }

        /// <summary>本帧点击的屏幕位置；<see cref="TapThisFrame"/> 为 false 时无意义。</summary>
        public static Vector2 TapPosition
        {
            get { Sample(); return _tapPosition; }
        }

        /// <summary>单指拖动位移（像素/帧）。已过点击判定阈值才开始输出，避免点一下画面跟着抖。</summary>
        public static Vector2 PanDelta
        {
            get { Sample(); return _gesture.Pan; }
        }

        /// <summary>双指捏合的间距变化（像素/帧，张开为正）。</summary>
        public static float PinchDelta
        {
            get { Sample(); return _gesture.PinchDelta; }
        }

        /// <summary>双指相对旋转角（度/帧，逆时针为正）。</summary>
        public static float TwistDegrees
        {
            get { Sample(); return _gesture.TwistDegrees; }
        }

        /// <summary>双指同向拖动的中点位移（像素/帧）。</summary>
        public static Vector2 TwoFingerDrag
        {
            get { Sample(); return _gesture.TwoFingerDrag; }
        }

        /// <summary>当前按在屏幕上的手指数。0 = 没手指（或不是触屏）。</summary>
        public static int TouchCount
        {
            get { Sample(); return _activeTouches.Count; }
        }

        /// <summary>
        /// 单指刚落到屏幕上的那一帧。玩法层用它抢在第一个 <see cref="PanDelta"/> 出现之前
        /// 决定这一次拖动归谁（详见 <see cref="TouchGesture.PressBegan"/>）。
        /// </summary>
        public static bool PressBeganThisFrame
        {
            get { Sample(); return _gesture.PressBegan; }
        }

        /// <summary>手指全部离开屏幕的那一帧。</summary>
        public static bool PressEndedThisFrame
        {
            get { Sample(); return _gesture.PressEnded; }
        }

        /// <summary>主手指的当前屏幕位置（抬手那一帧是离开前的最后位置）。仅触屏有意义。</summary>
        public static Vector2 PressPosition
        {
            get { Sample(); return _gesture.Position; }
        }

        /// <summary>这一帧屏幕上还有没有手指。</summary>
        public static bool HasFinger
        {
            get { Sample(); return _gesture.HasFinger; }
        }

        /// <summary>屏幕 DPI，取不到时按 160 兜底。凡是「多少英寸算近」的判定都该走这里，别各自写死像素。</summary>
        public static float ScreenDpi
        {
            get { return Screen.dpi > 1f ? Screen.dpi : FallbackDpi; }
        }

        /// <summary>
        /// 由玩法层改写黏性悬停位置。
        ///
        /// 触屏的悬停本来只由「点一下」产生（见类注释），但拖动建筑时建筑要跟手，
        /// 而下游（<c>IGridPresenter.TryGetHoveredCell</c>、摆放预览、地形高亮）统统是从这个位置
        /// 反查格子的。与其在下游各开一条「拖动中改读别处」的岔路，不如让拖动方直接改写这一个源头，
        /// 下游一行都不用动。
        /// </summary>
        public static void SetHover(Vector2 position)
        {
            // 先采样再写：否则本帧首次访问会在写入之后才跑 Sample，把刚设的位置盖掉
            Sample();
            _hoverPosition = position;
            _hasHover = true;
            // 位置变了，本帧缓存的「压没压在 UI 上」作废
            _uiCheckedFrame = -1;
        }

        /// <summary>把黏性悬停清掉（退出摆放模式 / 场景卸载时调）。鼠标模式下调用无害：下一帧会重新采到光标位置。</summary>
        public static void ClearHover()
        {
            _hasHover = false;
        }

        /// <summary>回到干净状态。场景切换时调，避免上一局的手势状态漏到下一局。</summary>
        public static void Reset()
        {
            _sampledFrame = -1;
            _uiCheckedFrame = -1;
            _hasHover = false;
            _gesture = default(TouchGesture);
            _gestureFingersLastFrame = 0;
            _gestures.Reset();
        }

        /// <summary>
        /// 屏幕坐标是否落在 UI 上。
        ///
        /// 不能用 <c>EventSystem.IsPointerOverGameObject()</c> 的**无参**重载：它查的是 pointerId = -1，
        /// 也就是鼠标。手指的 pointerId 是 0/1/2…，所以在手机上那个重载恒返回 false——
        /// 表现出来就是「点手牌按钮的同时还在按钮背后建了一栋楼」。
        /// 带 id 的重载也不好用：这里读的是新输入系统的 touchId，而 uGUI 的 StandaloneInputModule
        /// 走的是旧输入的 fingerId，两套编号对不上。
        /// 所以直接按**位置**做一次 UI 射线，两端同一套判定，不依赖任何编号。
        /// 只认 RectTransform 命中，免得场景里万一挂了 PhysicsRaycaster 把世界物体也算成 UI。
        /// </summary>
        public static bool IsOverUI(Vector2 screenPosition)
        {
            EventSystem events = EventSystem.current;
            if (events == null)
            {
                return false;
            }

            if (_uiPointer == null || !ReferenceEquals(_uiEvents, events))
            {
                _uiEvents = events;
                _uiPointer = new PointerEventData(events);
            }
            _uiPointer.position = screenPosition;

            _uiHits.Clear();
            events.RaycastAll(_uiPointer, _uiHits);
            for (int i = 0; i < _uiHits.Count; i++)
            {
                GameObject hit = _uiHits[i].gameObject;
                if (hit != null && hit.transform is RectTransform)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 当前指针位置是否压在 UI 上；没有指针时按「不在 UI 上」处理。
        /// 结果按帧缓存：摆放交互一帧要问好几次，而每问一次就是一趟 UI 射线。
        /// </summary>
        public static bool IsHoverOverUI()
        {
            Sample();
            if (_uiCheckedFrame == _sampledFrame)
            {
                return _hoverOverUi;
            }
            _uiCheckedFrame = _sampledFrame;

            Vector2 position;
            _hoverOverUi = TryGetHoverPosition(out position) && IsOverUI(position);
            return _hoverOverUi;
        }

        // ── 采样 ──────────────────────────────────────────────────────────────

        private static void Sample()
        {
            int frame = Time.frameCount;
            if (_sampledFrame == frame)
            {
                return;
            }
            _sampledFrame = frame;

#if UNITY_EDITOR
            SyncTouchSimulation();
#endif

            _gesture = default(TouchGesture);

            CollectTouches();
            bool touchActivity = _activeTouches.Count > 0 || _gestureFingersLastFrame > 0;

            PointerKind previousKind = _kind;
            if (touchActivity || ForceTouchMode)
            {
                _kind = PointerKind.Touch;
            }
            else if (MouseWokeUp())
            {
                _kind = PointerKind.Mouse;
            }
            else if (_kind == PointerKind.None)
            {
                // 首帧还没人动过任何设备：有触屏没鼠标的机器直接当触屏，否则默认鼠标
                _kind = Mouse.current == null && Touchscreen.current != null
                    ? PointerKind.Touch
                    : PointerKind.Mouse;
            }

            // 换手（插上鼠标 / 拿起手指）时把悬停丢掉：鼠标留下的光标位置对触屏没有意义，
            // 反过来触屏点出来的黏性位置也不该在鼠标模式下继续生效
            if (previousKind != PointerKind.None && previousKind != _kind)
            {
                _hasHover = false;
            }

            if (_kind == PointerKind.Touch)
            {
                SampleTouch();
            }
            else
            {
                SampleMouse();
            }

            _gestureFingersLastFrame = _activeTouches.Count;
        }

        private static void SampleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                _hasHover = false;
                return;
            }
            _hoverPosition = mouse.position.ReadValue();
            _hasHover = true;
        }

        private static void SampleTouch()
        {
            _gestures.TapSlopPixels = TapSlopPixels;
            _gesture = _gestures.Step(BuildFrame());

            if (!_gesture.Tap)
            {
                return;
            }

            _tapPosition = _gesture.TapPosition;

            // 触屏的「悬停」就是点出来的：点到哪，指针停在哪。
            // 但点在 UI 上的那一下不算——否则一按「旋转」按钮，黏性落点就跟着跳到按钮上，
            // 下一帧发现"指针压在 UI 上"，刚选好的落点连同范围环、飘分一起消失。
            if (!IsOverUI(_gesture.TapPosition))
            {
                _hoverPosition = _gesture.TapPosition;
                _hasHover = true;
            }
        }

        /// <summary>把本帧的 Touchscreen 读数打成手势状态机认的那一包。</summary>
        private static TouchFrame BuildFrame()
        {
            int count = _activeTouches.Count;
            float now = Time.unscaledTime;
            if (count == 0)
            {
                return TouchFrame.None(now);
            }
            if (count == 1)
            {
                return TouchFrame.One(
                    _activeTouches[0].position.ReadValue(), _activeTouches[0].delta.ReadValue(), now);
            }
            return new TouchFrame(
                count,
                _activeTouches[0].position.ReadValue(), _activeTouches[0].delta.ReadValue(),
                _activeTouches[1].position.ReadValue(), _activeTouches[1].delta.ReadValue(),
                now);
        }

        private static void CollectTouches()
        {
            _activeTouches.Clear();
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
            {
                return;
            }

            var touches = screen.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                TouchControl touch = touches[i];
                UnityEngine.InputSystem.TouchPhase phase = touch.phase.ReadValue();
                if (phase == UnityEngine.InputSystem.TouchPhase.Began
                    || phase == UnityEngine.InputSystem.TouchPhase.Moved
                    || phase == UnityEngine.InputSystem.TouchPhase.Stationary)
                {
                    _activeTouches.Add(touch);
                }
            }
        }

        private static bool MouseWokeUp()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }
            if (mouse.leftButton.wasPressedThisFrame
                || mouse.rightButton.wasPressedThisFrame
                || mouse.middleButton.wasPressedThisFrame)
            {
                return true;
            }
            if (Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
            {
                return true;
            }
            return mouse.delta.ReadValue().sqrMagnitude > MouseWakePixels * MouseWakePixels;
        }

        /// <summary>点击位移阈值换算成像素。DPI 取不到时按 160 兜底，否则高分屏上手一抖就不算点击了。</summary>
        private static float TapSlopPixels
        {
            get { return TapSlopInches * ScreenDpi; }
        }

    }
}
