# Wind Controller 使用说明

## 文件位置

这套风摆测试资源和运行代码已经提交到 GitHub，核心文件如下：

| 内容 | 路径 |
|---|---|
| 测试场景和测试模型 | `Assets/cicheng/WindController/` |
| 风帆材质 Inspector | `Assets/Editor/FI_SailWindShaderGUI.cs` |
| 风帆 Shader | `Assets/Resources/Shaders/FI_SailWind.shader` |
| 全局风场控制器 | `Assets/Script/View/Environment/GlobalWindFieldController.cs` |
| 物体缩放传值脚本 | `Assets/Script/View/Environment/SailWindObjectScaleBinder.cs` |
| 默认 3D 风场 | `Assets/Resources/Wind/GlobalWindField_Seamless.asset` |

## Global Wind Field Controller

`GlobalWindFieldController` 只用物体旋转表示风向，不再用 Inspector 里的方向向量。

- 风向 = `WindController` 物体的本地 X 轴，也就是 Scene 视图里的红色轴。
- 要改风向，直接旋转 `WindController` 物体。
- `fieldOrigin` 和 `fieldSize` 控制 3D 风场采样范围。
- `globalStrength` 控制全局风力强度。
- `mainDirectionWeight` 控制 3D 风场采样方向和主风向的混合比例。
- `fieldScrollSpeed` 控制 3D 风场滚动速度。

测试场景里的旧方向 `{x: 1, y: 0, z: 0.55}` 已经迁移成 `WindController` 的 Y 轴旋转。

## Sail Wind Shader

风帆材质使用 `FI/Sail Wind`。

风动画使用第二套 UV：

- `uv2.y` 用于固定边到自由边的渐变。
- `uv2.x` 用于 `UV U Edges` 模式的左右边缘固定。
- `Mask Texture` 模式下，遮罩贴图也用 `uv2` 采样。
- 主贴图、描边贴图仍使用第一套 UV，不受影响。

## Max Offset 和 Scale

`Max Offset` 是最终世界空间位移上限。

`SailWindObjectScaleBinder` 会把物体当前 `lossyScale` 转成相对倍率传给 Shader：

```text
scaleMultiplier = currentLossyScale / referenceScale
displacementLimit = MaxOffset * scaleMultiplier
```

默认 `referenceScale` 是 `(100, 100, 100)`，因为当前测试资源正常尺寸使用 Transform Scale 100。

示例：

| Transform Scale | Reference Scale | Scale Multiplier | Max Offset 10 时位移上限 |
|---:|---:|---:|---:|
| 10 | 100 | 0.1 | 1 |
| 100 | 100 | 1 | 10 |
| 500 | 100 | 5 | 50 |

`uv2`、遮罩、波形相位和噪声不乘物体 scale。scale 只影响最终位移上限，避免小物体摆动显得过大。

## 使用步骤

1. 场景中放一个 `GlobalWindFieldController`。
2. 旋转这个 Controller，红色 X 轴就是风向。
3. 风摆物体使用 `FI/Sail Wind` 材质。
4. 风摆物体必须有第二套 UV。
5. 在风摆物体上挂 `SailWindObjectScaleBinder`。
6. 如果模型正常尺寸不是 Scale 100，把 `Reference Scale` 改成该模型的正常 Transform Scale。

## 常用材质参数

| 参数 | 作用 |
|---|---|
| `Max Offset` | 最终最大位移上限 |
| `Sway` | 主摆动强度 |
| `Wave Speed` | 主波速度 |
| `Flutter` | 高频细颤强度 |
| `Wind Push` | 沿风向推动权重 |
| `Pinned Mode` | 固定边来源 |
| `Invert Pin` | 反转固定边 |
| `Fixed` | 固定区域起点 |
| `Full Move` | 完整移动区域终点 |

## 注意事项

- 不要再通过向量字段设置全局风向。
- 不要把物体 scale 乘进 `uv2`，否则固定边、遮罩和动画分布都会变化。
- 如果缩放后幅度不符合预期，先看 `SailWindObjectScaleBinder` 的 `Sent Scale Multiplier` 是否等于 `当前 Scale / Reference Scale`。
- `Max Offset` 不是材质滑条显示范围，它现在实际参与最终位移上限计算。
