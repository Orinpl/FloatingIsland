# FI_Lit Shader

## 目的

`FI_Lit` 是一个用于浮岛低多边形美术风格的 Built-in Render Pipeline shader。它不依赖 Unity 的标准 PBR 光照，而是在片元阶段计算可控的色块光照、暖色高光、冷色阴影和假倒角高光，用来贴近低模经营/建造类场景的清爽卡通质感。

## 文件位置

- Shader: `Assets/Resources/Shaders/FI_Lit.shader`
- HLSL: `Assets/Resources/Shaders/FI_LitCore.hlsl`
- Shader GUI: `Assets/Editor/FI_LitShaderGUI.cs`
- 默认材质: `Assets/Res/Common/FI_Lit.mat`

## 核心效果

1. Lowpoly 面片感
   - `FI_FlatNormal` 使用 `ddx/ddy` 从世界坐标重建片元面法线。
   - 光照使用面法线而不是平滑法线，所以模型会呈现硬面、切面、低多边形块感。

2. 阶梯光照
   - `FI_SteppedLight` 把 Lambert 光照量离散成几档。
   - `_LightSteps` 越低，色块越硬；越高，过渡越细。

3. 冷暖色调
   - `_ShadowColor` 控制暗面冷色。
   - `_TopColor` 控制亮面和上表面暖色。
   - `_TopTint` 给朝上的面额外叠加暖光，适合草地、屋顶、岛屿顶部。

4. 倒角高光
   - `FI_BevelHighlight` 比较平滑法线和面法线的差异，生成假倒角高光。
   - 模型有真实倒角或合理法线时效果最好。
   - 没有真实倒角时也会有轻微边缘高光，但不会完全替代建模倒角。

5. 面高光和边缘光
   - `FI_FaceHighlight` 提供硬面上的主高光。
   - `FI_RimHighlight` 提供视角相关的轮廓提亮。

## 材质参数

### Surface

- `_MainTex`: 主贴图。没有贴图时使用白色。
- `_BaseColor`: 基础色，会乘到主贴图上。

### Lighting

- `_LightDirection`: 手动光照方向，xyz 有效。
- `_Ambient`: 最低亮度，避免暗面死黑。
- `_LightSteps`: 光照分层数量，推荐 2 到 4。
- `_TopColor`: 亮面/顶面暖色。
- `_ShadowColor`: 暗面冷色。
- `_TopTint`: 朝上表面的暖色叠加强度。

### Highlights

- `_HighlightColor`: 高光颜色，建议用偏暖的黄白色。
- `_HighlightStrength`: 面高光强度。
- `_HighlightSize`: 面高光大小，数值越大高光越小越锐。
- `_BevelHighlight`: 倒角高光强度。
- `_BevelSharpness`: 倒角高光锐度。
- `_BevelWidth`: 假倒角范围。
- `_RimStrength`: 边缘光强度。
- `_RimPower`: 边缘光衰减，数值越大边缘越窄。

## 推荐预设

低模建筑/岛屿通用：

- `_Ambient`: `0.3 - 0.45`
- `_LightSteps`: `3`
- `_TopTint`: `0.12 - 0.25`
- `_HighlightStrength`: `0.2 - 0.5`
- `_BevelHighlight`: `0.6 - 1.1`
- `_BevelWidth`: `1.0 - 2.0`
- `_RimStrength`: `0.05 - 0.15`

更硬的玩具感：

- `_LightSteps`: `2`
- `_HighlightSize`: `64 - 96`
- `_BevelSharpness`: `24 - 40`

更柔和的经营游戏场景：

- `_LightSteps`: `4`
- `_Ambient`: `0.4`
- `_BevelHighlight`: `0.4 - 0.7`
- `_RimStrength`: `0.05`

## 注意事项

- 这个 shader 是 Opaque 单 Pass，不处理透明。
- 需要 shader target 3.0，因为片元阶段使用了 `ddx/ddy`。
- 当前写法适用于 Built-in Render Pipeline。项目切换 URP 后需要改成 URP HLSL include 和 LightMode。
- 如果一个模型的法线完全拆硬，假倒角高光会变弱；如果要更明显的倒角反光，应在模型上增加真实 bevel 或调整导入法线。
