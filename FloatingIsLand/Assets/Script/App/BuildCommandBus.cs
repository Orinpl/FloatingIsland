using System;

namespace FloatingIsLand.App
{
    /// <summary>
    /// HUD 按钮 → 摆放交互的指令桥。
    ///
    /// 存在的理由和 <see cref="InputArbiter"/> 一样是分层：触屏工具条（旋转 / 建造 / 取消）是 uGUI，
    /// 只能待在 Game.UI；而执行它们要动 ghost 的朝向和落点，那是 Game.View 的 BuildPlacementController。
    /// 两个程序集互不引用，共同的上游只有 Game.App——所以指令位放这里是唯一不破坏依赖方向的位置。
    ///
    /// 只走「按钮 → 玩法」这一个方向。反向（玩法状态 → HUD）已经有 GameSession 的事件，不要在这里加。
    ///
    /// 静态事件的老问题是订阅活得比场景久：View 侧必须在 OnDestroy 里退订，
    /// 场景切换时调 <see cref="Reset"/> 兜底，否则「进入播放模式不重载域」时会留下指向已销毁对象的委托。
    /// </summary>
    public static class BuildCommandBus
    {
        /// <summary>请求把当前手上的建筑转 90°（触屏没有滚轮，靠按钮转）。</summary>
        public static event Action RotateRequested;

        /// <summary>请求在当前落点落地（触屏的显式确认；PC 上左键已经能建，这条是给按钮用的）。</summary>
        public static event Action PlaceRequested;

        /// <summary>请求退出摆放模式（触屏没有 Esc）。</summary>
        public static event Action CancelRequested;

        public static void RequestRotate()
        {
            Action handler = RotateRequested;
            if (handler != null) { handler(); }
        }

        public static void RequestPlace()
        {
            Action handler = PlaceRequested;
            if (handler != null) { handler(); }
        }

        public static void RequestCancel()
        {
            Action handler = CancelRequested;
            if (handler != null) { handler(); }
        }

        /// <summary>清空全部订阅。场景卸载 / 退出局内时调。</summary>
        public static void Reset()
        {
            RotateRequested = null;
            PlaceRequested = null;
            CancelRequested = null;
        }
    }
}
