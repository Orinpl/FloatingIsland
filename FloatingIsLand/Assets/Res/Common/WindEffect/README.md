# LineRenderer 风特效

- 直接把 `VFX_WindLine.prefab` 拖进场景即可。
- 缩放根节点可改变整体长度；修改子节点 LineRenderer 的位置和宽度曲线可改变风带形状。
- `M_WindLine.mat` 使用 `FI/VFX/Wind Line`，材质面板可调颜色、亮度、羽化、可见宽度、笔锋、流速、摆动、脉冲和混合模式。
- `风丝羽化` 为 0 时是更硬的风格化边缘，为 1 时保留柔和的烟雾渐变。
- 根节点的 `FI_WindLineEffect` 统一控制三层 LineRenderer 的真实宽度；`层级宽度衰减` 决定后两层依次变细的程度。
- 默认笔锋为左右两端对称收尖、中间最粗；左右收尖长度、锐度和偏移均可继续调整。
- 根节点脚本同时控制速度并给三条风线错开动画相位，不会覆盖材质颜色或亮度。
- 如需恢复默认配置，执行菜单 `Tools/Floating Island/重建 LineRenderer 风特效`。
