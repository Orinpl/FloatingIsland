namespace FloatingIsLand.App
{
    /// <summary>
    /// 摆放预览的当帧结论，给 HUD 看。方向与 <see cref="BuildCommandBus"/> 正好相反：
    /// 那个是「按钮 → 玩法」，这个是「玩法 → 界面」。
    ///
    /// 由 Game.View 的 BuildPlacementController 每帧写，Game.UI 的 BuildHudAdapter 读。
    /// 两个程序集互不引用，共同上游只有 Game.App——同 <see cref="InputArbiter"/> 的理由。
    ///
    /// 手机上这三个字段不是锦上添花而是必需：触屏的「建造」是个按钮，
    /// 玩家必须能看出它为什么是灰的（<see cref="Message"/> 里就是原因），
    /// 而 PC 玩家至少还能靠地格红绿和虚影颜色自己判断。
    /// </summary>
    public static class BuildPreviewState
    {
        /// <summary>当前有没有落点（光标 / 手指指到了某个格子上）。</summary>
        public static bool HasTarget { get; set; }

        /// <summary>当前落点是否可摆。<see cref="HasTarget"/> 为 false 时无意义。</summary>
        public static bool CanPlace { get; set; }

        /// <summary>给玩家看的一句话：可摆时是「预计得分 N」，摆不下时是失败原因。</summary>
        public static string Message { get; set; } = string.Empty;

        /// <summary>回到「没有落点」的干净状态。退出摆放模式 / 场景卸载时调。</summary>
        public static void Clear()
        {
            HasTarget = false;
            CanPlace = false;
            Message = string.Empty;
        }
    }
}
