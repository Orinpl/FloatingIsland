using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 网格表现外设的抽象：建格、坐标转换、悬停格查询。
    /// View/App 只依赖本接口，不知道具体插件；当前实现是 EGB Pro 2 适配器
    /// （Assets/Script/ViewEGB/EGBGridPresenter，编入 Assembly-CSharp，见 Docs/GRID_INTEGRATION.md §4），
    /// 换掉/去掉插件只换实现。
    /// 坐标约定：领域格子坐标 (x, z, layer)，layer 为整数高度层（0 = 最低层），
    /// 与 GAME_DESIGN §21 决策 25/26（规则性高度层、球形范围）一致。
    /// </summary>
    public interface IGridPresenter
    {
        /// <summary>按尺寸重建网格表现（骨架版由实现的 Inspector 默认值驱动；M1 起改为地图快照驱动）。</summary>
        void BuildGrid(int width, int length);

        /// <summary>
        /// 实现内部已初始化完毕、可以安全调用 BuildGrid 与坐标转换。
        /// EGB 实现要等插件自己的 Start 建完内部格子列表，所以并非 Awake 后立即可用——
        /// 地图装载（MapBootstrap）必须先等这个变 true。
        /// </summary>
        bool IsReady { get; }

        int Width { get; }
        int Length { get; }
        int LayerCount { get; }

        /// <summary>格子边长（世界单位）。</summary>
        float CellSize { get; }

        /// <summary>
        /// 格子 ↔ 世界坐标的几何参数。自绘 overlay（地形/风箭头/覆盖高亮）批量算坐标时用它，
        /// 避免逐格回调插件；编辑器地形刷子用的也是同一份数学（<see cref="GridGeometry"/>）。
        /// 注意：网格尺寸变化后需重新读取——BuildGrid 会改变原点。
        /// </summary>
        GridGeometry Geometry { get; }

        /// <summary>格子的世界坐标。已实测：EGB 返回的是格子**角点**（min 角），中心 = 角点 + (CellSize/2, 0, CellSize/2)。</summary>
        Vector3 CellToWorld(int x, int z, int layer);

        /// <summary>世界坐标 → 指定层的格子坐标；越界返回 false。</summary>
        bool WorldToCell(Vector3 worldPosition, int layer, out int x, out int z);

        /// <summary>当前鼠标悬停的格子；不在网格上返回 false。骨架版只探测第 0 层，M1 接入地形数据后按层归属扩展。</summary>
        bool TryGetHoveredCell(out int x, out int z, out int layer);
    }
}
