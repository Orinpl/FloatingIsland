namespace FloatingIsLand.Domain.Build
{
    /// <summary>
    /// 风场只读查询口。风系统本体是 M3（WIND_IMPL），在那之前建造链路持有的实现为 null——
    /// 「没有风场」和「风力为 0 级」是两回事：前者表示这块规则尚未接入，后者是无风的真实结算值。
    /// 建造校验与计分都按「实现为 null 就整块跳过风相关规则」处理，不拿 0 级去冒充。
    /// </summary>
    public interface IWindField
    {
        /// <summary>该格的合成风力等级（0~5，0=无风）。越界返回 0。</summary>
        int GetForce(int x, int z, int layer);
    }
}
