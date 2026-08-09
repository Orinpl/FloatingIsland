namespace FloatingIsLand.Domain.Map
{
    /// <summary>
    /// 一个已落位的地图元素（巨型风车 / 锚点 / 矿藏 / 初始风源）。
    ///
    /// 与地形层（<see cref="MapCell"/>）分开存：地形是逐格填充的区域属性，元素是带占地形状的
    /// 独立个体，需要「哪几个格属于同一个锚点」这种个体身份来做归属与收益递减（§12.8）。
    /// 元素是生成期写入的静态数据（PROJECT_BUILD §3），不属于建筑层，不占建筑的可建造判定之外的规则。
    /// </summary>
    public readonly struct MapElementPlacement
    {
        /// <summary>元素 Id（= 配表 MapElement.elementId）。</summary>
        public readonly string ElementId;

        /// <summary>锚点格 X（占地最小角，与建筑摆放同口径）。</summary>
        public readonly int X;

        /// <summary>锚点格 Z。</summary>
        public readonly int Z;

        /// <summary>高度层。</summary>
        public readonly int Layer;

        /// <summary>朝向。</summary>
        public readonly Rotation Rotation;

        public MapElementPlacement(string elementId, int x, int z, int layer, Rotation rotation)
        {
            ElementId = elementId;
            X = x;
            Z = z;
            Layer = layer;
            Rotation = rotation;
        }
    }
}
