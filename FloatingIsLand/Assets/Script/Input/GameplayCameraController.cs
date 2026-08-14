using FloatingIsLand.App;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FloatingIsLand.GameInput
{
    /// <summary>
    /// 局内自由相机（Unity 编辑器式操作逻辑）：
    /// - W/S/A/D：沿相机朝向的水平投影前后左右平移；
    /// - Shift / Ctrl：升 / 降；
    /// - 滚轮：沿视线方向拉近拉远（建造模式下让给建筑旋转，见 <see cref="InputArbiter"/>）；
    /// - 按住右键拖动：原地旋转（偏航 + 俯仰，编辑器飞行视角）；
    /// - 按住中键拖动：屏幕面整体平移（编辑器抓手：内容跟随光标，相机反向移动）。
    /// 直接轮询 Input System 设备——相机操作固定键位无重绑需求，不占 Action Map；
    /// 摆放类玩法输入仍按 PROJECT_BUILD §1.2 走 Action Map（M1）。
    ///
    /// 手机端是同一套相机、另一组手势（<see cref="PointerInput"/>），且**建造模式内外还不一样**：
    /// 非建造模式下双指整套模拟 PC 的右键拖（自由视角），建造模式才换成以待摆建筑为中心的那套。
    /// 完整对照表见 <see cref="StepTouch"/>。
    ///
    /// 三种双指手势互斥，由 <see cref="TwoFingerMode"/> 三选一，所以不会出现「转着转着镜头自己拉近了」。
    /// 两端共用后半段的高度限幅与俯仰限幅，所以手机上不会出现「能翻到地底下」这种 PC 上没有的姿态。
    /// </summary>
    public sealed class GameplayCameraController : MonoBehaviour
    {
        [Header("平移 / 升降")]
        [Tooltip("WASD 水平移动速度（米/秒）")]
        [SerializeField] private float moveSpeed = 12f;
        [Tooltip("Shift/Ctrl 升降速度（米/秒）")]
        [SerializeField] private float liftSpeed = 8f;

        [Header("缩放（滚轮）")]
        [Tooltip("每格滚轮沿视线推进的距离（米）")]
        [SerializeField] private float zoomPerNotch = 2.5f;

        [Header("旋转（右键拖动）")]
        [Tooltip("每像素鼠标位移的旋转角度（度）")]
        [SerializeField] private float rotateDegreesPerPixel = 0.2f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 89f;

        [Header("平移（中键拖动）")]
        [Tooltip("每像素平移量的基准系数（实际平移随相机高度缩放，越高拖得越快）")]
        [SerializeField] private float panUnitsPerPixel = 0.002f;

        [Header("高度限制")]
        [SerializeField] private float minHeight = 1.5f;
        [SerializeField] private float maxHeight = 80f;

        [Header("触屏")]
        [Tooltip("单指拖平移相对中键拖的倍率。手指比鼠标粗、屏幕比显示器小，1:1 会觉得拖不动；" +
                 "但倍率给高了在手机上又会「一划过半张图」，找不回刚才在看的地方")]
        [SerializeField] private float touchPanScale = 1.2f;

        [Tooltip("双指捏合每像素间距变化沿视线推进的距离（米）")]
        [SerializeField] private float touchZoomUnitsPerPixel = 0.06f;

        [Tooltip("双指反向划每度换算成的绕建筑公转角度")]
        [SerializeField] private float touchTwistToOrbit = 1.5f;

        [Tooltip("建造模式下双指同向上下划每像素的升降距离（米）")]
        [SerializeField] private float touchLiftUnitsPerPixel = 0.05f;

        [Tooltip("建造模式下双指同向的分流门槛：两指间距超过屏幕长边的这个比例才算升降，否则算滑屏")]
        [Range(0.1f, 1f)]
        [SerializeField] private float touchLiftSpreadFraction = 0.5f;

        [Tooltip("非建造模式下双指拖（等同 PC 右键拖）相对右键的转动倍率")]
        [SerializeField] private float touchLookScale = 1.2f;

        [Tooltip("没有待摆建筑时，绕转支点取视线与地面的交点；超过这个距离就认为没支点，退化成原地偏航")]
        [SerializeField] private float maxOrbitPivotDistance = 240f;

        /// <summary>绕转支点所在的地面高度（米）。网格铺在 y=0 上，视线与这个平面的交点就是「屏幕中心在看哪」。</summary>
        private const float GroundY = 0f;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Vector3 position = transform.position;
            float yawDelta = 0f;
            float pitchDelta = 0f;

            if (PointerInput.IsTouchMode)
            {
                StepTouch(ref position, ref yawDelta, ref pitchDelta);
            }
            else
            {
                StepMouseKeyboard(dt, ref position, ref yawDelta, ref pitchDelta);
            }

            // 限幅与落位两端共用：手机上不会出现「能翻到地底下」这种 PC 上没有的姿态
            position.y = Mathf.Clamp(position.y, minHeight, maxHeight);
            transform.position = position;

            if (yawDelta != 0f || pitchDelta != 0f)
            {
                Vector3 euler = transform.rotation.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
                transform.rotation = Quaternion.Euler(pitch, euler.y + yawDelta, 0f);
            }
        }

        /// <summary>
        /// 触屏。**建造模式内外是两套手势**，由 <see cref="InputArbiter.TouchBuildMode"/> 切换：
        ///
        /// | 手势 | 建造模式 | 非建造模式 |
        /// |------|----------|-----------|
        /// | 单指拖 | 归建筑（相机不动） | 屏幕面平移 |
        /// | 双指外拉 / 内缩 | 缩放 | 缩放 |
        /// | 双指反向划 | 绕建筑公转 | 原地偏航 |
        /// | 双指同向划 | 靠得近 = 滑屏，张得开 = 升降 | 原地偏航 + 俯仰（= PC 右键拖） |
        ///
        /// 非建造模式下没有「要绕着转的那栋楼」，所以整套双指退回 PC 右键拖那套自由视角，
        /// 玩家在两端得到的是同一种手感；建造模式才需要那套以建筑为中心的操作。
        ///
        /// 手势解析全在 <see cref="PointerInput"/>，这里只把像素换算成米和度。
        /// 缩放不看仲裁：那条让位规则是给滚轮的（建造模式下滚轮改成转建筑），
        /// 而触屏转建筑走的是 HUD 按钮，双指手势和它不抢同一个物理输入。
        /// </summary>
        private void StepTouch(ref Vector3 position, ref float yawDelta, ref float pitchDelta)
        {
            bool building = InputArbiter.TouchBuildMode;

            // 建造模式下单指整条通道归建筑：手机屏幕小，「这一下是拖楼还是拖地图」
            // 如果要靠起手点在不在楼上来猜，猜错的代价是楼被甩到屏幕外
            if (!building)
            {
                Vector2 pan = PointerInput.PanDelta;
                if (pan.sqrMagnitude > 0f)
                {
                    PanAlongScreen(ref position, pan);
                }
            }

            switch (PointerInput.TwoFingerMode)
            {
                case TwoFingerMode.Pinch:
                    float pinch = PointerInput.PinchDelta;
                    if (Mathf.Abs(pinch) > 0.01f)
                    {
                        position += transform.forward * (pinch * touchZoomUnitsPerPixel);
                    }
                    break;

                case TwoFingerMode.Twist:
                    if (building)
                    {
                        OrbitAroundTarget(ref position, ref yawDelta, PointerInput.TwistDegrees);
                    }
                    else
                    {
                        // 没有支点可绕，退化成原地偏航；转向与建造模式一致（手指怎么转、画面怎么转）
                        yawDelta += PointerInput.TwistDegrees * touchTwistToOrbit;
                    }
                    break;

                case TwoFingerMode.Drag:
                    if (building)
                    {
                        StepBuildModeTwoFingerDrag(ref position);
                    }
                    else
                    {
                        // 等同 PC 右键拖：偏航 + 俯仰，符号与 StepMouseKeyboard 里那段一致
                        Vector2 look = PointerInput.TwoFingerDrag;
                        yawDelta += look.x * rotateDegreesPerPixel * touchLookScale;
                        pitchDelta -= look.y * rotateDegreesPerPixel * touchLookScale;
                    }
                    break;
            }
        }

        /// <summary>
        /// 建造模式下的双指同向划：按**两指间距**分流，而不是按滑动方向。
        ///
        /// 两指并在一起 = 一只手就能做的小动作 = 滑屏（横竖都能滑）；
        /// 两指张到半个屏幕开外 = 明显是两只手掰着的大动作 = 拉高拉低镜头。
        /// 用间距而不是「横划滑屏、竖划升降」来分，是因为滑屏本来就需要能上下滑，
        /// 按方向分等于把纵向滑屏整个砍掉。
        ///
        /// 间距取的是分类锁定那一刻的快照（<see cref="TouchGesture.Spread"/>），
        /// 滑动途中手指再张合也不会中途改判。
        /// </summary>
        private void StepBuildModeTwoFingerDrag(ref Vector3 position)
        {
            Vector2 drag = PointerInput.TwoFingerDrag;
            // 屏幕长边为准，横竖屏都是同一个「半屏」口径
            float screenSpan = Mathf.Max(Screen.width, Screen.height);

            if (PointerInput.TwoFingerSpread >= screenSpan * touchLiftSpreadFraction)
            {
                if (Mathf.Abs(drag.y) > 0.01f)
                {
                    // 上划抬高、下划压低。落位后统一走 Update 里的 minHeight/maxHeight 限幅
                    position.y += drag.y * touchLiftUnitsPerPixel;
                }
                return;
            }

            if (drag.sqrMagnitude > 0f)
            {
                PanAlongScreen(ref position, drag);
            }
        }

        /// <summary>屏幕面平移：内容跟手，相机沿屏幕轴反向走；幅度随高度缩放。与中键拖同一口径。</summary>
        private void PanAlongScreen(ref Vector3 position, Vector2 pixels)
        {
            float heightScale = Mathf.Max(position.y, 2f) * panUnitsPerPixel * touchPanScale;
            position -= transform.right * (pixels.x * heightScale);
            position -= transform.up * (pixels.y * heightScale);
        }

        /// <summary>
        /// 双指反向划 = 相机**绕建筑公转**（而不是原地偏航）。
        ///
        /// 方向：左手在左侧上划、右手在右侧下滑，两指连线是顺时针转，
        /// <see cref="PointerInput.TwistDegrees"/>（逆时针为正）因此是负数；
        /// 绕世界 Y 转负角在俯视下就是逆时针公转，而相机逆时针绕着建筑走，
        /// 玩家看到的正是建筑在顺时针转——也就是手指怎么划、楼怎么转。
        ///
        /// 公转 + 同角度偏航才能一直正对支点：只转位置不转朝向，楼会滑出画面；
        /// 只转朝向就是原地偏航，楼会甩到视野边上。绕的是**世界 Y 轴**，
        /// 所以无论怎么转，建筑的 y 轴在画面里始终是竖直向上的，不会歪。
        /// </summary>
        private void OrbitAroundTarget(ref Vector3 position, ref float yawDelta, float twistDegrees)
        {
            if (Mathf.Abs(twistDegrees) <= 0.01f)
            {
                return;
            }

            float orbit = twistDegrees * touchTwistToOrbit;

            Vector3 pivot;
            if (TryGetOrbitPivot(position, out pivot))
            {
                // 绕 Vector3.up 转不改变 y 分量，所以支点的高度取多少都不影响结果，只有 xz 有意义
                position = pivot + Quaternion.AngleAxis(orbit, Vector3.up) * (position - pivot);
            }
            yawDelta += orbit;
        }

        /// <summary>
        /// 公转支点。优先取正在摆的那栋建筑；没有则退回「屏幕中心在看的那块地面」，
        /// 这样非建造模式下转视角也是绕着看的东西转，而不是原地打转。
        ///
        /// 视线太平（几乎不朝下）或交点远在天边时返回 false：绕一个几百米外的支点转，
        /// 相机会整个甩出岛外。那种姿态下退化成原地偏航反而是对的。
        /// </summary>
        private bool TryGetOrbitPivot(Vector3 position, out Vector3 pivot)
        {
            if (BuildPreviewState.HasTarget)
            {
                pivot = BuildPreviewState.ToolbarAnchorWorld;
                return true;
            }

            pivot = Vector3.zero;
            Vector3 forward = transform.forward;
            if (forward.y > -0.05f)
            {
                return false;
            }

            float distance = (GroundY - position.y) / forward.y;
            if (distance <= 0f || distance > maxOrbitPivotDistance)
            {
                return false;
            }

            pivot = position + forward * distance;
            return true;
        }

        /// <summary>
        /// PC：WASD / Shift-Ctrl / 滚轮 / 右键拖 / 中键拖。手感参数与判定逻辑与接入触屏前完全一致，
        /// 只是把旋转量交出去由 <see cref="Update"/> 统一落位（两端共用同一份限幅）。
        /// </summary>
        private void StepMouseKeyboard(float dt, ref Vector3 position, ref float yawDelta, ref float pitchDelta)
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            // --- WASD：沿相机水平投影方向移动（俯视 90° 时 forward 投影退化，改用 up 的投影当"前方"） ---
            Vector3 flatForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 1e-4f)
            {
                flatForward = Vector3.ProjectOnPlane(transform.up, Vector3.up);
            }
            flatForward.Normalize();
            Vector3 flatRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

            Vector3 move = Vector3.zero;
            if (keyboard.wKey.isPressed) { move += flatForward; }
            if (keyboard.sKey.isPressed) { move -= flatForward; }
            if (keyboard.dKey.isPressed) { move += flatRight; }
            if (keyboard.aKey.isPressed) { move -= flatRight; }
            if (move.sqrMagnitude > 0f)
            {
                position += move.normalized * (moveSpeed * dt);
            }

            // --- Shift / Ctrl：升降 ---
            float lift = 0f;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) { lift += 1f; }
            if (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed) { lift -= 1f; }
            position.y += lift * liftSpeed * dt;

            // --- 滚轮：沿视线拉近拉远（Windows 原始值 ±120/格，其它平台多为 ±1，统一折算成"格"） ---
            // 建造模式下滚轮归玩法（旋转建筑），相机让出缩放；其余相机操作不受影响。
            float scroll = InputArbiter.ScrollConsumedByGameplay ? 0f : mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float notches = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
                position += transform.forward * (notches * zoomPerNotch);
            }

            // --- 中键拖动：屏幕面平移（内容跟随光标 → 相机沿屏幕轴反向移动；幅度随高度缩放） ---
            if (mouse.middleButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float heightScale = Mathf.Max(position.y, 2f) * panUnitsPerPixel;
                position -= transform.right * (delta.x * heightScale);
                position -= transform.up * (delta.y * heightScale);
            }

            // --- 右键拖动：原地旋转（偏航绕世界 Y，俯仰限幅，横滚恒 0） ---
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yawDelta += delta.x * rotateDegreesPerPixel;
                pitchDelta -= delta.y * rotateDegreesPerPixel;
            }
        }
    }
}
