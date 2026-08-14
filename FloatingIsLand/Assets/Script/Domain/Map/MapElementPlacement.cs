namespace FloatingIsLand.Domain.Map
{
    /// <summary>
    /// 一个已落位的地图元素（巨型风车 / 锚点 / 矿藏 / 初始风源）。
    ///
    /// 与地形层（<see cref="MapCell"/>）分开存：地形是逐格填充的区域属性，元素是带占地形状的
    /// 独立个体，需要「哪几个格属于同一个锚点」这种个体身份来做归属与收益递减（§12.8）。
    /// 元素是生成期写入的静态数据（PROJECT_BUILD §3），不属于建筑层，不占建筑的可建造判定之外的规则。
    ///
    /// 风源可以额外携带一组手工风参数（地图编辑器写入）：<see cref="WindForce"/> &gt; 0 即「已授权」，
    /// 运行时直接按这组参数展开风；未授权（老地图 / 自动散布产物）仍按局种子随机。
    /// 字段用裸 int 而不是 Dir8——Domain.Map 不反向依赖 Domain.Wind。
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

        /// <summary>手工风向（Dir8 数值 0~7，E 起逆时针每步 45°）。仅 <see cref="HasWindParams"/> 时有意义。</summary>
        public readonly int WindDir;

        /// <summary>手工风力（1~5）。&gt; 0 = 手工授权标志；0 = 无手工参数，运行时按局种子随机。</summary>
        public readonly int WindForce;

        /// <summary>手工风长（格）。仅 <see cref="HasWindParams"/> 时有意义。</summary>
        public readonly int WindLength;

        public MapElementPlacement(string elementId, int x, int z, int layer, Rotation rotation)
            : this(elementId, x, z, layer, rotation, 0, 0, 0)
        {
        }

        public MapElementPlacement(
            string elementId, int x, int z, int layer, Rotation rotation,
            int windDir, int windForce, int windLength)
        {
            ElementId = elementId;
            X = x;
            Z = z;
            Layer = layer;
            Rotation = rotation;
            WindDir = windDir;
            WindForce = windForce;
            WindLength = windLength;
        }

        /// <summary>是否带手工风参数（地图编辑器授权过的风源）。</summary>
        public bool HasWindParams
        {
            get { return WindForce > 0; }
        }
    }
}
