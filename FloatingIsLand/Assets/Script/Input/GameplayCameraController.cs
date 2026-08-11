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
    /// 手机端是同一套相机、另一组手势（<see cref="PointerInput"/>）：
    /// - 单指拖：屏幕面平移，与中键拖同一条代码路径；
    /// - 双指捏合：沿视线推拉，与滚轮同一条代码路径；
    /// - 双指旋转：偏航；双指同向上下拖：俯仰。
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
        [Tooltip("单指拖平移相对中键拖的倍率。手指比鼠标粗、屏幕比显示器小，1:1 会觉得拖不动")]
        [SerializeField] private float touchPanScale = 2.2f;

        [Tooltip("双指捏合每像素间距变化沿视线推进的距离（米）")]
        [SerializeField] private float touchZoomUnitsPerPixel = 0.06f;

        [Tooltip("双指相对旋转每度换算成的偏航角度")]
        [SerializeField] private float touchTwistToYaw = 1f;

        [Tooltip("双指同向上下拖相对右键拖的俯仰倍率")]
        [SerializeField] private float touchPitchScale = 1.5f;

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
        /// 触屏：单指拖平移、双指捏合缩放、双指转偏航、双指同向上下拖俯仰。
        /// 手势解析全在 <see cref="PointerInput"/>，这里只把像素换算成米和度。
        ///
        /// 缩放不看 <see cref="InputArbiter"/>：那条让位规则是给滚轮的（建造模式下滚轮改成转建筑），
        /// 而触屏转建筑走的是 HUD 按钮，双指捏合和它不抢同一个物理输入。
        /// </summary>
        private void StepTouch(ref Vector3 position, ref float yawDelta, ref float pitchDelta)
        {
            Vector2 pan = PointerInput.PanDelta;
            if (pan.sqrMagnitude > 0f)
            {
                // 与中键拖同一口径：内容跟手，相机沿屏幕轴反向走；幅度随高度缩放
                float heightScale = Mathf.Max(position.y, 2f) * panUnitsPerPixel * touchPanScale;
                position -= transform.right * (pan.x * heightScale);
                position -= transform.up * (pan.y * heightScale);
            }

            float pinch = PointerInput.PinchDelta;
            if (Mathf.Abs(pinch) > 0.01f)
            {
                position += transform.forward * (pinch * touchZoomUnitsPerPixel);
            }

            float twist = PointerInput.TwistDegrees;
            if (Mathf.Abs(twist) > 0.01f)
            {
                // 手指逆时针转 → 画面跟着逆时针 → 相机偏航反向
                yawDelta -= twist * touchTwistToYaw;
            }

            float dragY = PointerInput.TwoFingerDrag.y;
            if (Mathf.Abs(dragY) > 0.01f)
            {
                pitchDelta -= dragY * rotateDegreesPerPixel * touchPitchScale;
            }
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
