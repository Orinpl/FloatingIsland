namespace FloatingIsLand.App
{
    /// <summary>
    /// 输入归属仲裁。玩法与相机抢同一个物理输入时，在这里声明「这一帧归谁」。
    ///
    /// 存在的理由是分层：相机控制器在 Game.Input，摆放控制器在 Game.View。
    /// View 单向引用 Input（读 PointerInput 拿指针与触屏手势），反过来不行，
    /// 而这个标志是**双向**的——摆放置位、相机读取——所以只能放在两边共同的上游 Game.App。
    ///
    /// 当前只有一条：建造模式下滚轮改为旋转建筑，相机的滚轮缩放让位（其余相机操作照旧）。
    /// </summary>
    public static class InputArbiter
    {
        /// <summary>
        /// 滚轮是否已被玩法占用。true 时 <see cref="FloatingIsLand.GameInput.GameplayCameraController"/>
        /// 跳过缩放，把滚轮让给建筑旋转。摆放结束务必复位，否则相机会一直失去缩放。
        /// </summary>
        public static bool ScrollConsumedByGameplay { get; set; }

        /// <summary>回到「无玩法占用」的干净状态。场景卸载 / 退出建造模式时调。</summary>
        public static void Reset()
        {
            ScrollConsumedByGameplay = false;
        }
    }
}
