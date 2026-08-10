using System.Collections.Generic;
using FloatingIsLand.Domain.Map;

namespace FloatingIsLand.Domain.Build
{
    /// <summary>
    /// 风场只读查询口。实现在 Domain/Wind（WIND_IMPL），建造链路未接入时为 null——
    /// 「没有风场」和「风力为 0 级」是两回事：前者表示这块规则尚未接入，后者是无风的真实结算值。
    /// 建造校验与计分都按「实现为 null 就整块跳过风相关规则」处理，不拿 0 级去冒充。
    /// </summary>
    public interface IWindField
    {
        /// <summary>该格的合成风力等级（0~5，0=无风）。越界返回 0。</summary>
        int GetForce(int x, int z, int layer);

        /// <summary>
        /// 这组占用格是否在任一物流风格子的球形覆盖范围内（§10.4 物流风把覆盖传播到
        /// 本地范围外）。口径与建筑覆盖一致：RangeMath 三维欧氏、自占地边缘起算。
        /// </summary>
        bool HasLogisticsWindNear(IReadOnlyList<CellCoord> cells, int layer, int radius, float layerHeightFactor);
    }
}
