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

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }

            float dt = Time.unscaledDeltaTime;
            Vector3 position = transform.position;

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

            position.y = Mathf.Clamp(position.y, minHeight, maxHeight);
            transform.position = position;

            // --- 右键拖动：原地旋转（偏航绕世界 Y，俯仰限幅，横滚恒 0） ---
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                Vector3 euler = transform.rotation.eulerAngles;
                float pitch = euler.x > 180f ? euler.x - 360f : euler.x;
                pitch = Mathf.Clamp(pitch - delta.y * rotateDegreesPerPixel, minPitch, maxPitch);
                float yaw = euler.y + delta.x * rotateDegreesPerPixel;
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
        }
    }
}
