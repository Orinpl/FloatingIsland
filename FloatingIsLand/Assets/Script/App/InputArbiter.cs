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
    /// - 手机上手指落在待摆建筑附近时，这一次单指拖归建筑，相机的平移让位。
    /// 两条都只夺走**一个**通道，其余相机操作照旧。
    /// </summary>
    public static class InputArbiter
    {
        /// <summary>
        /// 滚轮是否已被玩法占用。true 时 <see cref="FloatingIsLand.GameInput.GameplayCameraController"/>
        /// 跳过缩放，把滚轮让给建筑旋转。摆放结束务必复位，否则相机会一直失去缩放。
        /// </summary>
        public static bool ScrollConsumedByGameplay { get; set; }

        /// <summary>
        /// 单指拖动是否已被玩法占用。true 时相机跳过触屏平移，把这一次拖动让给「拖建筑」。
        ///
        /// 认领必须发生在**手指落下**那一帧，而不是第一个平移量到来时：手势层要等累计位移过了
        /// 点击阈值才输出平移，两者之间隔着至少一帧，所以在落指帧认领就能完全避开
        /// 「相机先跟着动一帧再交接」的跳动，也就不必去管两个 MonoBehaviour 的执行顺序。
        /// 抬手 / 第二根手指落下时务必复位，否则相机会一直拖不动。
        /// </summary>
        public static bool PanConsumedByGameplay { get; set; }

        /// <summary>回到「无玩法占用」的干净状态。场景卸载 / 退出建造模式时调。</summary>
        public static void Reset()
        {
            ScrollConsumedByGameplay = false;
            PanConsumedByGameplay = false;
        }
    }
}
