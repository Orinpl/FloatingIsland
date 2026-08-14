namespace FloatingIsLand.App
{
    /// <summary>
    /// 输入归属仲裁。玩法与相机抢同一个物理输入时，在这里声明「这一帧归谁」。
    ///
    /// 存在的理由是分层：相机控制器在 Game.Input，摆放控制器在 Game.View。
    /// View 单向引用 Input（读 PointerInput 拿指针与触屏手势），反过来不行，
    /// 而这个标志是**双向**的——摆放置位、相机读取——所以只能放在两边共同的上游 Game.App。
    ///
    /// 当前有两条：
    /// - PC 建造模式下滚轮改为旋转建筑，相机的滚轮缩放让位；
    /// - 手机建造模式下单指拖一律归建筑，相机的单指平移让位，双指也换一套手势
    ///   （见 <see cref="TouchBuildMode"/>）。
    /// </summary>
    public static class InputArbiter
    {
        /// <summary>
        /// 滚轮是否已被玩法占用。true 时 <see cref="FloatingIsLand.GameInput.GameplayCameraController"/>
        /// 跳过缩放，把滚轮让给建筑旋转。摆放结束务必复位，否则相机会一直失去缩放。
        /// </summary>
        public static bool ScrollConsumedByGameplay { get; set; }

        /// <summary>
        /// 触屏 + 正在摆放。相机据此整套换挡（见 GameplayCameraController.StepTouch）：
        /// - 单指整条通道让给「拖建筑」，相机不再单指滑屏；
        /// - 双指反向 = 绕建筑公转，双指同向按**两指间距**分流滑屏 / 升降。
        /// 反过来非建造模式下双指一律模拟 PC 的右键拖（原地偏航 + 俯仰）。
        ///
        /// 按「是否在摆放」整段声明，不随单次拖动的起止翻转：手势层要等累计位移过了点击阈值
        /// 才输出平移，若等到那时再认领，相机已经跟着动过一帧了。整段声明也就顺带
        /// 不必去管 BuildPlacementController 与 GameplayCameraController 谁先 Update。
        /// 退出摆放 / 组件失活时务必复位，否则相机会一直拖不动。
        /// </summary>
        public static bool TouchBuildMode { get; set; }

        /// <summary>回到「无玩法占用」的干净状态。场景卸载 / 退出建造模式时调。</summary>
        public static void Reset()
        {
            ScrollConsumedByGameplay = false;
            TouchBuildMode = false;
        }
    }
}
