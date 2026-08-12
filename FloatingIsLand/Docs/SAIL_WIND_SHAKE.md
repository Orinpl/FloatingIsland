# 风帆全局 3D 风场方案

本文档已完全替代旧的 Transform 抖动和 CPU Mesh 变形方案。当前实现是：全局 `Texture3D` 风场 + Shader 顶点动画。

## 当前实现

| 内容 | 文件 |
|---|---|
| 风帆顶点动画 Shader | `Assets/Resources/Shaders/FI_SailWind.shader` |
| 风帆材质 Inspector | `Assets/Editor/FI_SailWindShaderGUI.cs` |
| 全局风场控制器 | `Assets/Script/View/Environment/GlobalWindFieldController.cs` |
| 六面连续 3D 风场 | `Assets/Resources/Wind/GlobalWindField_Seamless.asset` |
| 3D 风场生成器 | `Assets/Editor/SeamlessWindTexture3DGenerator.cs` |
| 风帆材质 | `Assets/Res/sail/mat/sail.mat` |

旧的 `SailWindShake` 及其 meta 已删除，`AmbientWindMotionBinder` 不再给风帆挂 Transform 动画脚本。

## Texture3D 数据定义

`GlobalWindField_Seamless.asset` 是真实 Unity `Texture3D` 资产，不是画了立方体的二维图片。

- 尺寸：`32 × 32 × 32`
- 格式：`RGBA32`
- Filter Mode：`Trilinear`
- Wrap Mode：`Repeat`
- RGB：归一化风向，从 `[0, 1]` 解码到 `[-1, 1]`
- A：风力强度，范围 `[0, 1]`

体素由三轴周期函数生成。X、Y、Z 每个轴在一个周期后数值和变化趋势都连续，因此左右、上下、前后六个面均可循环拼接。纹理不包含重复边界切片，跨边界插值由 `Repeat` 完成。

如需重新生成，在 Unity 菜单执行：

```text
Tools/Floating Island/Regenerate Seamless 3D Wind Field
```

## 全局风场采样

`GlobalWindFieldController` 在没有手工指定纹理时，会默认加载：

```text
Resources/Wind/GlobalWindField_Seamless
```

控制器向 Shader 发布：

```text
_GlobalWindField3D
_GlobalWindFieldOrigin
_GlobalWindFieldSize
_GlobalWindFieldScrollSpeed
_GlobalWindDirection
_GlobalWindStrength
_GlobalWindFieldEnabled
```

世界坐标先转换为周期 UVW：

```hlsl
float3 uvw = frac(
    (worldPos - _GlobalWindFieldOrigin.xyz) / _GlobalWindFieldSize.xyz +
    _GlobalWindFieldScrollSpeed.xyz * _Time.y);
```

风场在顶点阶段采样，必须显式指定 LOD：

```hlsl
float4 windSample = tex3Dlod(_GlobalWindField3D, float4(uvw, 0.0));
float3 windDirection = normalize(windSample.rgb * 2.0 - 1.0);
float windStrength = windSample.a * _GlobalWindStrength;
```

不能在顶点阶段使用普通 `tex3D()`。它需要隐式屏幕导数，会触发 D3D 顶点着色器编译错误 `cannot map expression to vs_4_0 instruction set`。

## 风帆顶点动画

最终位移由三部分组成：

```text
固定边权重 × 风力 ×（低频整体摆动 + 中频布面波 + 高频细颤）
```

风帆 Shader 使用 `FI_SailWindShaderGUI`，Inspector 按 Surface、Lighting、Highlights、Sail Motion、Attachment Mask 和 Advanced 分组，操作方式与 `FI_LitShaderGUI` 一致。

`Attachment Mask` 中通过 `Displacement Control Mode` 明确二选一：

- `Vertex Color`：只读取 `vertex color.r`，`R=0` 固定，`R=1` 完整位移。
- `Mask Texture`：只读取 `_SailMaskTex.r`，使用 UV0 采样，黑色固定，白色完整位移。

最终顶点位移 Mask 为：

```text
重映射后的当前模式 Mask × _SailMaskStrength
```

- `_SailMaskMode`：选择 `Vertex Color` 或 `Mask Texture`，两种来源不会相乘。
- `_SailMaskTex`：仅在 `Mask Texture` 模式显示并生效。
- `_SailMaskStart`：小于或等于该值的顶点严格保持零位移，用于扩大连接处固定带。
- `_SailMaskEnd`：大于或等于该值的顶点使用完整位移，中间平滑过渡。
- `_SailMaskStrength`：整体位移 Mask 强度，`0` 表示整张帆不发生顶点位移。
- `_SailMaskInvert`：连接边的通道值为 `1` 时开启反转。

当前材质默认使用 `Vertex Color`，固定区从 `0` 到 `0.08`。如果连接边写成了 `R=1`，开启 `Invert Mask`。需要按图片精细控制绳结和连接点时，切换到 `Mask Texture` 并把对应区域涂黑。

主要材质参数：

| 参数 | 默认值 | 用途 |
|---|---:|---|
| `_SailSwayAmplitude` | `0.08` | 整体摆动幅度 |
| `_SailWaveSpeed` | `1.2` | 主波速度 |
| `_SailWaveScale` | `0.8` | 世界空间波形尺度 |
| `_SailClothAmplitude` | `0.03` | 布面波幅度 |
| `_SailClothFrequency` | `2.5` | 布面波频率 |
| `_SailFlutterAmplitude` | `0.01` | 高频细颤幅度 |
| `_SailFlutterSpeed` | `8.0` | 高频细颤速度 |
| `_SailWindPush` | `1.0` | 沿风向位移权重 |
| `_SailNormalPush` | `0.35` | 沿法线鼓起权重 |

## 运行规则

- 场景有手工放置的 `GlobalWindFieldController` 时，使用该实例。
- 场景没有控制器时，运行后自动创建一个全局实例。
- 手工指定 `windFieldTexture` 时优先使用指定资源。
- 没有指定时加载默认六面连续 `Texture3D`。
- 默认资产丢失时，才使用运行时周期风场作为兜底。
- 不逐个遍历风帆，不逐帧修改 Transform，不在 CPU 修改 Mesh 顶点。

## 验收

- Shader 无编译错误，风帆不显示紫色。
- 多个风帆按同一全局风场响应，但因世界坐标不同不会完全同步。
- 风场越过 X/Y/Z 边界时没有跳变接缝。
- 固定边稳定，自由边摆动明显。
- 修改全局风力、方向、风场尺寸和滚动速度时，所有风帆统一响应。
