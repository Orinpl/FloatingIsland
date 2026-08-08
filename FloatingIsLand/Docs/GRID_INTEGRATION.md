# EGB Pro 2 网格浅接入设计（Grid Integration）

> 本文档描述 SoulGames **Easy Grid Builder Pro 2**（下称 EGB，位于 `Assets/SoulGames/`）如何接入本项目：
> 接入深度为**浅接入**——EGB 只做「网格表现 + 鼠标选格 + ghost 预览」，一切规则真相（占用、合法性、计分）在领域层。
> 关联决策：GAME_DESIGN §21 决策 25（规则性高度层）/ 26（球形影响范围）；PROJECT_BUILD §1.2、§4.1。

---

## 1. 为什么是浅接入（调查结论）

对插件源码的调查结论（111 个脚本，命名空间 `SoulGames.EasyGridBuilderPro`，无 asmdef）：

| | 结论 | 依据 |
|---|---|---|
| ✅ 可用 | 多网格系统管理、运行时实例化、格子↔世界坐标转换 | `GridManager.InstantiateGridSystem`、`GetCellWorldPosition(cell, layer)` 等 |
| ✅ 可用 | 垂直层（等距多层格子，鼠标按表面碰撞体自动切层） | `verticalGridsCount` / `verticalGridHeight` / `autoDetectVerticalGrid`（EasyGridBuilderProXZ.cs:701-713） |
| ✅ 可用 | ghost 预览、格子吸附、4 方向旋转、放置事件 | `BuildableGridObjectGhost`、`OnBuildableObjectPlaced` |
| ❌ 不可当真相 | **无外部校验钩子**：自定义建造条件是注释掉的半成品，外部无法在放置前否决 | `BuildableObjectSO.cs:38-44` 注释残留 |
| ❌ 不可当真相 | **占地只支持矩形**（prefab 尺寸 ÷ 格大小取整），装不下居民区 L/U 形（`##\|#.\|#.`） | `BuildableGridObjectSO.GetObjectSizeRelativeToCellSize` |
| ❌ 不可当真相 | 格子无地形数据槽（只有占用字典 + float 自定义值） | `GridCellData.cs` |
| ❌ 不用 | 它的输入管理（GridInputManager）、存档、Undo、选中/移动/摧毁模块 | 我们的输入走 Game.Input；存档/摧毁本游戏没有 |

三处「不可当真相」与本项目「领域层是唯一真相」的原则（PROJECT_BUILD §2）天然一致：**EGB 永远不做裁决**。

## 2. 职责边界

```
玩家鼠标/键盘
   │
   ▼
EGB（表现外设，Assembly-CSharp）             领域层（Game.Domain，纯 C#）
├─ 网格线/格子可视化（分层）                  ├─ 地图状态 (x, z, layer)：地形/占用/风段
├─ 鼠标 → 悬停格子（吸附）        ──查询──►  ├─ 合法性校验（地形要求/占位/异形 footprint）
├─ ghost 白模跟随预览                        ├─ 球形范围判定 √(dx²+dz²+(Δlayer·k)²) ≤ r
└─ （M1 起）落地白模的生成表现     ◄──事件──  └─ 计分干跑（分数明细）
```

- **摆放确认链路**（M1 实装）：Game.Input 的确认键 → 领域层干跑（合法 + 明细）→ 合法才落地 → 领域事件 → View 生成白模。**不走 EGB 的 BuildMode 自动放置**，EGB 不记占用（两个 L 形互嵌这类合法摆法它的矩形占用会误判）。
- **非法高亮**：EGB ghost 的红/绿由其私有逻辑决定、接不上领域规则。初版用「确认时拒绝 + 原因提示」（合法性校验器本来就返回具体原因）；实时红绿后续由 View 自绘格子 overlay（风箭头/覆盖高亮同一套设施）。

## 3. 高度层的表现机制选择

领域坐标 `(x, z, layer)`，layer 为整数、层高统一（等距）。EGB 侧两个候选机制：

| 机制 | 适用 | 决定 |
|---|---|---|
| **垂直层**（一套网格 N 层，等距） | 层等高、整图覆盖，`(cell, verticalGridIndex)` 与领域 `(x,z,layer)` 一一映射 | ✅ **采用**：映射零成本，`autoDetectVerticalGrid` 让鼠标在高台碰撞体上自动切层 |
| 多网格系统（每台地一套独立网格） | 台地要不同格大小/错位原点/非等距高度 | 备选，需求出现再切（领域坐标不变，只改适配层映射） |

坐标契约：

- 领域 `(x, z, layer)` ↔ EGB `(new Vector2Int(x, z), verticalGridIndex = layer)`；
- 世界高度 = `layer × verticalGridHeight`；`verticalGridHeight = 层高折算系数 k × cellSize`（k 进 GameConfig，待数值化——**领域层球形范围判定与表现层高必须用同一个 k**）；
- 悬停格子不依赖 EGB 的 `Camera.main` + collider 检测链（脆弱），适配层自己做「鼠标射线 × 各层平面，从高层向低层取第一个含地形的格子」。

## 4. 代码布局（绕开 asmdef 限制）

EGB 无 asmdef、编在 `Assembly-CSharp`，而我们的 asmdef **不能引用 Assembly-CSharp**。约定：

| 位置 | 程序集 | 内容 |
|---|---|---|
| [Assets/Script/View/IGridPresenter.cs](../Assets/Script/View/IGridPresenter.cs) | Game.View | **接口**：建格、坐标转换、悬停格查询——View/App 只认这个接口，不知道 EGB |
| [Assets/Script/ViewEGB/](../Assets/Script/ViewEGB/) | Assembly-CSharp（无 asmdef，可同时看到 EGB 和全部 Game.* 程序集） | `EGBGridPresenter : MonoBehaviour, IGridPresenter` 适配器；场景里接线 |
| [Assets/Script/ViewEGB/Editor/](../Assets/Script/ViewEGB/Editor/) | Assembly-CSharp-Editor | 场景接线工具（Tools → 框架 → 给 Main 场景接入 EGB 网格） |

- **零改动插件**（不给 SoulGames 目录加 asmdef——它内含多个 Editor 文件夹，强加 asmdef 要拆一堆编辑器程序集，插件升级还会冲掉）。
- 换掉/去掉 EGB 时只动 `ViewEGB/` 与场景接线，`IGridPresenter` 之上的代码不动——这也是随时降级到纯自研（方案 C）的退路。

## 5. 场景接线

菜单 **Tools → 框架 → 给 Main 场景接入 EGB 网格**，向 `Main.unity` 注入（**用插件成品 prefab 当模板**——裸 AddComponent 会缺可视化设置与模块引用，表现为无网格线 + NRE，已踩过坑）：

```
Main.unity
└── GridSystems（+ EGBGridPresenter + EGBHoverDebug）
    ├── Grid Managers（插件成品 prefab 实例：GridManager + 各 Ghost/Selector/Destroyer/Mover，
    │                  InputActionAsset 已内配；场景级单例，随每关重载 Main 一起重置）
    └── EGB Pro 2 Grid XZ（插件成品 prefab 实例：预配 Object Grid 可视化，displayOnDefaultMode=true 常显网格线；
                           接线工具覆盖 gridWidth/gridLength/verticalGridsCount，cellSize 与 verticalGridHeight 沿用 prefab 值 2/2）
```

- 浅接入下暂保留 Grid Managers 里 GridInputManager 的原生按键：未注册任何 BuildableObjectSO 时无实际效果，M1 接自研输入时再定禁用策略。
- **接线工具修正的三个 prefab 出厂坑**（重跑菜单即自动处理）：
  1. `gridShowColor/gridHideColor` 出厂 alpha=1 → 网格是不透明实心板、非活动层"隐藏"后仍可见（隐藏=渐变到 hideColor）；覆盖为示例场景的半透明灰 / alpha 0；
  2. `gridSystemLayerMask` 出厂 = Nothing，而网格 prefab 在 Layer 31 → GridManager 鼠标射线永远检测不到网格（无法交互的根因，GridManager.cs:214）；接线时把 mask 指到 Layer 31，并给该未命名层补名 "EGB Grid System"；
  3. 垂直层数默认 1（风跨层规则未定、MVP 单层回避）；升层时每层各有一块可视化面片，非活动层靠 hideColor alpha=0 淡出。

- 前置：Main 场景相机带 `MainCamera` tag（EGB 内部用 `Camera.main`）✓、Boot 场景有 EventSystem ✓、Input System 新旧兼容模式 ✓——均已满足。
- **注意**：`Tools → 框架 → 生成启动场景` 会整体覆盖 Main.unity，重生成后需重跑本接线菜单（工具会检测并提示）。
- Ghost/BuildableObjectSO 在 M1 接入摆放流程时再配（按 BuildingVariant 表生成 SO + 白模 prefab，`prefabPath` 列就是为此准备的）。

## 6. M1 接入清单（本骨架之后的事）

1. 领域层 `Domain/Map`：`(x,z,layer)` 网格 + 地形 + 异形 footprint 占用 + 球形范围工具（含 §21 决策 26 公式，单测锁定）；
2. 按 BuildingVariant 生成 `BuildableGridObjectSO` + 白模 prefab（含 ghostObjectPrefab），`AddBuildableObjectSOToTheList` 注册给 EGB 做 ghost；
3. Game.Input：摆放 Action Map（选建筑/旋转/确认/取消），确认走「干跑 → 领域落地 → View 生成白模」链路；
4. 风帆落地前的左/右转选择弹窗（设计 §9.2）插在确认与落地之间；
5. View 自绘格子 overlay（非法红格、加减分高亮、覆盖范围），替代「确认时才知道非法」的初版体验。

## 7. 开放问题

| # | 问题 | 状态 |
|---|---|---|
| 1 | 风跨高度层规则（撞高台截断/爬升/穿过、落层） | 设计未定（GAME_DESIGN §20），M3 前必须闭合；MVP 手工图可先单层回避 |
| 2 | 层高折算系数 k 的数值 | 待数值化（进 GameConfig），领域范围判定与表现层高共用 |
| 3 | ghost 实时红绿高亮 | 初版不做（确认时拒绝+原因），M1 后由 View overlay 补 |
| 4 | 半径数值口径 | 球形欧氏下斜向 1 格 = √2，拍数值时按欧氏口径；配表注释里的"切比雪夫"字样下轮改表时清理 |
