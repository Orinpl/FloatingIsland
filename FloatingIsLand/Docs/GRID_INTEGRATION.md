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
└── GridSystems（+ EGBGridPresenter + EGBHoverDebug + GridCellHighlighter + MapBootstrap）
    ├── Grid Managers（插件成品 prefab 实例：GridManager + 各 Ghost/Selector/Destroyer/Mover，
    │                  InputActionAsset 已内配；场景级单例，随每关重载 Main 一起重置）
    ├── EGB Pro 2 Grid XZ（插件成品 prefab 实例：接线工具覆盖 gridWidth/gridLength/verticalGridsCount，
    │                      cellSize 与 verticalGridHeight 沿用 prefab 值 2/2；
    │                      **Object Grid 可视化已关闭**，见下方第 4 条）
    └── TerrainOverlay（TerrainOverlayRenderer：网格可视化的实际承担者，按已刷地块合批画 Mesh）
```

- 浅接入下暂保留 Grid Managers 里 GridInputManager 的原生按键：未注册任何 BuildableObjectSO 时无实际效果，M1 接自研输入时再定禁用策略。
- **接线工具修正的四个 prefab 出厂坑**（重跑菜单即自动处理）：
  1. `gridShowColor/gridHideColor` 出厂 alpha=1 → 网格是不透明实心板、非活动层"隐藏"后仍可见（隐藏=渐变到 hideColor）；覆盖为示例场景的半透明灰 / alpha 0（面片虽已关闭，留着以防临时打开排查）；
  2. `gridSystemLayerMask` 出厂 = Nothing，而网格 prefab 在 Layer 31 → GridManager 鼠标射线永远检测不到网格（无法交互的根因，GridManager.cs:214）；接线时把 mask 指到 Layer 31，并给该未命名层补名 "EGB Grid System"；
  3. 垂直层数默认 1（风跨层规则未定、MVP 单层回避）；
  4. `displayObjectGrid` 置 false —— 本项目要求「没刷过的地块不显示」，而 EGB 每个垂直层只有**一整块面片**、无逐格显隐 API（`alphaMask` 是跟随光标的单一遮罩，EasyGridBuilderPro.cs:85-91）。网格可视化改由 `TerrainOverlayRenderer` 按已刷地块合批绘制，见 §8。

- 前置：Main 场景相机带 `MainCamera` tag（EGB 内部用 `Camera.main`）✓、Boot 场景有 EventSystem ✓、Input System 新旧兼容模式 ✓——均已满足。
- **注意**：`Tools → 框架 → 生成启动场景` 会整体覆盖 Main.unity，重生成后需重跑本接线菜单（工具会检测并提示）。
- Ghost/BuildableObjectSO 在 M1 接入摆放流程时再配（按 BuildingVariant 表生成 SO + 白模 prefab，`prefabPath` 列就是为此准备的）。

## 6. M1 接入清单（本骨架之后的事）

1. 领域层 `Domain/Map` + `Domain/Build`：`(x,z,layer)` 网格 + 地形 + 异形 footprint 占用 + 球形范围工具（含 §21 决策 26 公式，单测锁定）—— **已完成**：
   `Footprint`（异形掩码 + 4 方向旋转）、`RangeMath`（球形欧氏， k = GameConfig.layerHeightFactor）、
   `BuildBoard`（占用 + 地形/矿藏/浮空区域校验）、`ScoreEngine`（即时建造分）、`BuildRunState`（等级/手牌/金币）；
2. **不走 EGB 的 BuildableObjectSO。** ghost 与落地模型由 `View/ModelSpawner` 按配表 `prefabPath` 从 Resources 实例化，
   EGB 只提供网格与格子坐标——保持「EGB 永远不裁决」，也免掉维护一套插件 SO 资产；
3. 摆放输入：`View/BuildPlacementController` 直接轮询 Input System 设备（与相机控制器同口径）——
   鼠标跟随吸附 / 滚轮 90° 旋转 / 左键落地 / Esc 取消；**建造模式下滚轮归玩法**，
   相机通过 `App/InputArbiter` 让出缩放，其余相机操作（WASD / 右键拖旋转 / 中键拖平移 / Shift・Ctrl 升降）保持原有逻辑；
4. 风帆落地前的左/右转选择弹窗（设计 §9.2）插在确认与落地之间 —— **待做（随风系统 M3）**；
5. View 自绘格子 overlay（加减分高亮、覆盖范围）—— **部分完成**：ghost 已按干跑结果实时红/绿，
   加减分对象与作用范围高亮待做。

## 7. 开放问题

| # | 问题 | 状态 |
|---|---|---|
| 1 | 风跨高度层规则（撞高台截断/爬升/穿过、落层） | 设计未定（GAME_DESIGN §20），M3 前必须闭合；MVP 手工图可先单层回避 |
| 2 | 层高折算系数 k 的数值 | **已闭合**：`GameConfig.layerHeightFactor = 1`（对齐 EGB 预制体 cellSize 2 / verticalGridHeight 2），`RangeMath` 与表现层共用 |
| 3 | ghost 实时红绿高亮 | **已完成**：`BuildPlacementController` 每帧拿领域层干跑结果给 ghost 上色，并在 HUD 提示预计得分 / 非法原因 |
| 4 | 半径数值口径 | 球形欧氏下斜向 1 格 = √2，拍数值时按欧氏口径；配表注释里的"切比雪夫"字样下轮改表时清理 |

---

## 8. 地形手刷产线

MVP 的地图是手工布局（PROJECT_BUILD §4.1：随机生成放到 M6）。产线：**编辑器刷 → 稀疏 JSON → 运行时只加载刷过的格**。

```
Tools → 地图 → 地形刷子（Scene 视图左键刷 / Shift+左键擦）
   │  只列配表 MapElement 里 isTerrain=TRUE 的行：island / greenField / floatingZone
   ▼
Assets/Resources/Maps/stage_{id}.json   ← 稀疏，只存刷过的格，按 (layer,z,x) 排序保证 diff 干净
   │  MapBootstrap 在局内 Start 时装载
   ▼
MapSnapshot（Domain，纯数据）→ BuildGrid(快照尺寸) → TerrainOverlayRenderer 合批画 Mesh
```

**核心约定：没有 MapCell 的坐标 = 虚空 = 不可建造、不渲染。** 未刷的格子不生成任何顶点，
所以"建造模式下没刷过的格子不显示"是结构上成立的，不需要逐格开关。

| 位置 | 程序集 | 职责 |
|---|---|---|
| [Domain/Map/MapSnapshot.cs](../Assets/Script/Domain/Map/MapSnapshot.cs) | Game.Domain | 稀疏地块 + `IsPainted`；越界/重复/空 id 构造时即抛 |
| [Config/MapJson.cs](../Assets/Script/Config/MapJson.cs) | Game.Config | JSON ↔ 快照（Newtonsoft，与 TableJson 同机制） |
| [View/GridGeometry.cs](../Assets/Script/View/GridGeometry.cs) | Game.View | 格↔世界数学，**编辑器与运行时共用的唯一一份** |
| [View/TerrainOverlayRenderer.cs](../Assets/Script/View/TerrainOverlayRenderer.cs) | Game.View | 已刷地块合批 Mesh；`[ExecuteAlways]`，刷子的落笔预览也走它 |
| [View/MapBootstrap.cs](../Assets/Script/View/MapBootstrap.cs) | Game.View | 局内装载：读图 → 建格 → 画地形 |
| [ViewEGB/Editor/MapPainterWindow.cs](../Assets/Script/ViewEGB/Editor/MapPainterWindow.cs) | Assembly-CSharp-Editor | 刷子窗口 + Scene 交互 |

### 8.1 按岛屿模型自动描摹（实际产线）

手刷适合精修，但一整座岛逐格刷不现实，而且刷出来的轮廓与岛屿模型对不齐（玩家会看到建筑悬空或陷进山体）。
所以主产线改成**用模型本身当权威轮廓**：`Tools → 地图 → 按岛屿模型生成全部关卡地图`
（[MapAutoBuilder.cs](../Assets/Script/ViewEGB/Editor/MapAutoBuilder.cs)）：

1. 按 `Stage.islandCellSpan` 把岛屿缩放居中（与运行时 `WorldRenderer` **完全同一套对位算法**，一旦漂移地形就会与岛错位）；
2. 逐格从上往下打射线，命中岛面**顶部薄片**的格刷成 `island`——只取顶部是为了排掉岛底锥形裙边，避免把降坡也标成可建造；
3. 岛外一圈虚空刷成 `floatingZone`（设计 §5：浮空区域在地图外围或岛屿之间，且是船坞唯一合法地形）；
4. 岛内按种子挖若干块 `greenField`（农田的必要地形）；
5. 调领域层 `MapElementScatter` 散布巨型风车/锚点/矿藏/风源，写进地图 JSON 的 `elements` 数组。

结果是确定性的：同一个种子 + 同一个模型 → 同一张图。手刷刷子仍在，用于在自动结果上做局部修改。

地图 JSON 因此分两层：`cells` 是逐格地形，`elements` 是带占地形状的地图元素。
两者分开存是因为地形是区域属性，而元素需要**个体身份**才能做锚点归属与收益递减（§12.8）。

两个易踩的点：

1. **刷子不能用 `IGridPresenter.CellToWorld`。** EGB 的 `GetCellWorldPosition` 要走 `gridList[layer]`，
   而 `gridList` 只在运行时 `SetupVerticalGrids` 里创建（EasyGridBuilderProXZ.cs:227-242），编辑器态是 null。
   所以坐标一律走 `GridGeometry`——它精确复刻了 `CalculateGridOrigin`(L244-256) 与 `GridXZ.GetCellWorldPosition`(L1028-1032)。
   **只留这一份**：两边各写一遍必然漂移，症状是"编辑器刷的位置和跑起来差半格"，极难查。
2. **刷子放在 `ViewEGB/Editor/` 而不是 `Assets/Tool/Editor/`**，因为它必须读 EGB 组件的
   `gridWidth/gridLength/cellSize/gridOriginType`，而 `using SoulGames` 只允许出现在 `ViewEGB/`（§4）。
