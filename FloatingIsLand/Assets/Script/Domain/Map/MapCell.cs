namespace FloatingIsLand.Domain.Map
{
    /// <summary>
    /// 一个"刷过"的地块（地形层）。地图数据是稀疏的——只有刷过的格子才有 MapCell，
    /// 没有 MapCell 的坐标即"虚空"（PROJECT_BUILD §4.1 的地形类型之一），不可建造、不渲染。
    /// ElementId 对应配表 MapElement.elementId 中 isTerrain=TRUE 的行（island / greenField / floatingZone）。
    /// 领域层不引用 Game.Config，所以这里只存 id 字符串，不存表行对象（PROJECT_BUILD §2 原则 2）。
    /// </summary>
    public readonly struct MapCell
    {
        /// <summary>格子横坐标（0 起，&lt; MapSnapshot.Width）。</summary>
        public readonly int X;

        /// <summary>格子纵坐标（0 起，&lt; MapSnapshot.Length）。</summary>
        public readonly int Z;

        /// <summary>整数高度层（0 = 最低层，GAME_DESIGN §21 决策 25）。当前单层地图恒为 0。</summary>
        public readonly int Layer;

        /// <summary>地形 Id（= MapElement.elementId）。</summary>
        public readonly string ElementId;

        public MapCell(int x, int z, int layer, string elementId)
        {
            X = x;
            Z = z;
            Layer = layer;
            ElementId = elementId;
        }
    }
}
