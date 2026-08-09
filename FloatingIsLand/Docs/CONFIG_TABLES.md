# 配表工具链（TableTool）

Excel 正本 → 一条命令 → 运行时 JSON + 强类型 C# 访问代码。**策划改表零代码，加新表零代码。**

```
Tables\*.xlsx                                 ← 【正本】策划编辑，改这里
   │  转表.bat（或 Unity 菜单 Tools/配表/转表）
   ├──→ Assets\Resources\Tables\*.json        ← 运行时读的数据
   └──→ Assets\Script\Config\Generated\Tables.g.cs  ← 行类 + 单例类 + Tables 注册表（勿手改）
```

命名空间统一为 `FloatingIsLand.Config`。前置：机器上装了 .NET SDK（`dotnet --version` 有输出）。

## 日常怎么用

**改表 / 加表**：

1. 用 Excel 打开 `Tables\FloatingIsland.xlsx`（或自己新建的 xlsx）改数据 / 新建 Sheet
2. 双击工程根的 **`转表.bat`**，或在 Unity 里点菜单 **Tools → 配表 → 转表**
3. 回 Unity，等它导入完就能用 `Tables.新表名` 访问——**不用写任何读表代码**

**提交前自查**：双击 `校验配表.bat`（只校验不落盘），或菜单 Tools → 配表 → 只校验不落盘。

**冒烟验证**：双击 `验证读表.bat` —— 脱离 Unity 编译读表层 + `Tables.g.cs` 并真实加载全部 JSON，
能抓到「生成代码编不过 / JSON 反序列化炸 / 主键重复」这类问题。

**提交**：`xlsx` + `json` + `Tables.g.cs` **三者同步提交**（都是文本 diff，除 xlsx 外可审）。

命令行等价写法（在工程根 `FloatingIsLand\` 下）：

```powershell
dotnet run --project Tools\TableTool -- convert     # 全量转表：校验 + 写 JSON + 写 Tables.g.cs
dotnet run --project Tools\TableTool -- check       # 只校验不落盘
dotnet run --project Tools\TableTool -- bootstrap   # 从 seed 重建初始 Excel（已存在则拒绝，--force 覆盖）
dotnet run --project Tools\ConfigVerify             # 冒烟验证：编译 + 加载全部 JSON
```

可选参数：`--root <工程根>`（在别处调用时指定）、`--namespace <命名空间>`（默认 `FloatingIsLand.Config`）。

## 游戏侧怎么读

```csharp
using FloatingIsLand.Config;

// 启动时加载一次（Unity 侧走这个；Resources 下的全部 TextAsset 一次读进来）
// 注：启动框架的 Boot 状态已接入本调用（见 BOOT_FRAMEWORK.md），游戏内业务代码无需再加载
UnityTableLoader.LoadFromResources("Tables");

// 之后任意处静态强类型访问
BuildingRow b = Tables.Building.Get("windVane");   // 行表：按主键取行（缺键抛带表名的异常）
foreach (LevelRow lv in Tables.Level.All) {}       // 遍历；还有 Count / TryGet / GetOrNull
int levels = Tables.GameConfig.totalLevels;        // 单例参数组：直接取字段
```

- 主键列类型决定 `TKey`（`int` 或 `string`），生成器自动选
- 未加载就访问 `Tables.*` 会抛明确异常（`TableLoader.IsLoaded` 可查）
- 纯 C# 场景（控制台工具 / EditTest / 模拟器）用 `TableLoader.LoadFromDirectory(目录)`
- IL2CPP 安全：反序列化目标全是生成的具体类，无运行时泛型构造

## Excel 布局约定（写表必须遵守）

### 行表（rows）——一个 Sheet 一张表，Sheet 名 = 表名（PascalCase）

| 行 | 内容 |
|---|---|
| 第 1 行 | 字段名（camelCase，与 JSON/C# 字段名一字不差） |
| 第 2 行 | 类型：`int` `long` `float` `bool` `string` `int[]` `float[]` `string[]` |
| 第 3 行 | 中文说明（含单位 / 取值范围） |
| 第 4 行起 | 数据行 |

- **第 1 列 = 主键**：类型必须 `int` 或 `string`，非空且唯一（转表器校验）
- 字段名以 `#` 开头的列 = 策划注释列，不导出；Sheet 名以 `#` 开头 = 整页跳过
- 主键单元格为空的行跳过（可作空行分隔）
- 数组用 `|` 分隔（如 `consumable|heal`）；bool 接受 `TRUE/FALSE/1/0/是/否`；空单元格 = 类型默认值（0/false/空串/空数组）
- 数值按 InvariantCulture 解析；`int` 列出现非整数值算错误；跨工作簿表名不许冲突

### 单例参数组（singleton）——Sheet 名以 `Config` 结尾即按此布局解析

（反过来，行表的表名**禁止**以 `Config` 结尾）

| key | type | value | desc |
|---|---|---|---|
| startGold | int | 500 | 初始金币 |
| enablePvp | bool | FALSE | 是否开启 PVP |

第 1 行表头固定 `key / type / value / desc`，第 2 行起每行一个参数。导出为单个 JSON 对象、
生成一个同名 C# 类，代码里直接 `Tables.GameConfig.startGold`。

### 校验与报错

`convert` / `check` 失败时以 `文件!Sheet!单元格` 坐标报错并返回非零退出码，常见：重复主键、
类型解析失败、未知类型、首列非 int/string、跨工作簿表名冲突、行表误用 `Config` 后缀。
**校验不通过时不写任何输出**，不会产出半截产物。

`convert` 是全量重写：JSON 目录先清空再写、`Tables.g.cs` 单文件重写，所以删表 / 改字段不会留陈旧产物。

## 工程里的落点

| 路径 | 是什么 |
|---|---|
| `Tables\*.xlsx` | 【正本】策划编辑的 Excel |
| `Tools\TableTool\` | 转表工具源码（convert / check / bootstrap），不参与 Unity 编译 |
| `Tools\TableTool\bootstrap\*.seed.json` | 建表种子（仅首次生成 Excel 用，之后 Excel 是正本） |
| `Tools\ConfigVerify\` | 冒烟验证工程（链接编译读表层 + 生成代码） |
| `Assets\Script\Config\` | 读表层：`ConfigTable` / `TableJson` / `TableLoader`（纯 C#，锁 C# 9） |
| `Assets\Script\Config\Unity\UnityTableLoader.cs` | 唯一引 UnityEngine 的文件（从 Resources 读） |
| `Assets\Script\Config\Editor\TableToolMenu.cs` | Unity 菜单 Tools/配表/… |
| `Assets\Script\Config\Generated\Tables.g.cs` | 【产物】生成代码，勿手改 |
| `Assets\Resources\Tables\*.json` | 【产物】运行时数据，必须在 Resources 下 |

依赖：`com.unity.nuget.newtonsoft-json`（已加进 `Packages/manifest.json`）；工具侧 ClosedXML 0.105 +
Newtonsoft.Json 13，首次 `dotnet run` 自动还原。读表层锁 C# 9 是为兼容 Unity 2022。

## 本项目的表（Tables\FloatingIsland.xlsx，10 个 Sheet）

表结构由 [GAME_DESIGN.md](GAME_DESIGN.md) 推导（§13～§18 五张关系表全部数据化为有向条目），
对应 [PROJECT_BUILD.md](PROJECT_BUILD.md) §5 的四类配置收敛方案。**当前只定了表头与结构行，
具体数值待填**（填数原则：设计文档已明确的值已填入，如船坞风能曲线、锚点递减、居民区计数上限；
其余 0/空 = 待数值化，见设计 §20）。

| Sheet | 类型 | 主键 | 内容 | 对应设计 |
|---|---|---|---|---|
| `Building` | 行表 | `buildingId` string | **模板表**，15 栋建筑：分类、建造限制、半径、基础分、`elementBonus` 地图元素加分（微格式 `元素Id:分值[:上限]`；判定用元素的 radius；写了 `giantWindmill` 条目=专属分替代通用分）、物流覆盖资格、风力曲线（船坞/风帆/居民区/风向标各自专列）、MVP 批次 | §6、§11、§12、§14、§19 |
| `BuildingVariant` | 行表 | `variantId` string | **表现表**，一行一个变体：`buildingId` 归属模板、`nameCn` 变体显示名（空=沿用 `Building.nameCn`）、`footprint` 占地掩码（`#`=占用 `.`=空、\|分行，如 2×2=`##\|##`、L形=`##\|#.\|#.`）、`prefabPath` 表现 Prefab。一个模板可挂多套占地/外观（如居民区 3 种结构），抽哪个变体由 `Level` 表的抽取池直接配到变体粒度；摆放旋转不配表，默认全部允许 90° 旋转。**一个模板挂多个变体时 `nameCn` 必须逐个填**——手牌是按变体发的，都叫「居民区」玩家分不出方形和 L 形（UI 另有形状图标辅助） | 占地与表现 |
| `BuildingRelation` | 行表 | `buildingId` string | 每建筑一行的有向邻接关系（真值表 A/B + 单向 + 双向 + 负面 + 同类）：`bonusFrom` 加分来源 / `penaltyFrom` 扣分来源两列，单元格微格式 `来源Id:分值[:上限]`、多条目用 `\|` 分隔；判定范围一律用结算建筑自身 `radius`；来源=自己即同类关系；方向不可反读，双向关系在两行各写一条。解析器 `RelationEntry.ParseAll`，ConfigVerify 会校验格式与建筑 Id 外键 | §13、§15～§18 |
| `MapElement` | 行表 | `elementId` string | 7 种地图元素：占地掩码、有效范围、生成数量区间 | §5.2 |
| `WindLevel` | 行表 | `level` int | 风力 0~5 级：名称、通用风力倍率 | §8.3 |
| `Level` | 行表 | `level` int | 20 级：解锁费用、组数、组大小、抽取池 `pool`（微格式 `变体Id:数量`、\|分隔，如 `residence_01:2\|farm_01:3`；变体 Id → `BuildingVariant.variantId`，配到占地结构粒度） | §4 |
| `Stage` | 行表 | `stageId` int | 3 个关卡：每关一张独立浮空岛地图（尺寸 250×250、岛屿模型资源路径） | 关卡需求 |
| `GameConfig` | 单例 | — | 格子边长 `cellSize`（世界单位，须与 EGB 网格预制体一致）、总等级、分转金币比例、刷新保护、巨型风车通用分、锚点递减曲线 | §3、§4.3、§6 |
| `WindConfig` | 单例 | — | 风力上限、初始风强度/长度区间 | §8 |
| `LogisticsConfig` | 单例 | — | 覆盖半径、覆盖分、延长风长与次数上限、终局网络奖励 | §10 |

跨表引用约定：`BuildingRelation` 打包条目里的来源 Id、`BuildingVariant.buildingId` → `Building.buildingId`；
`Level.pool` 条目里的变体 Id → `BuildingVariant.variantId`；`Building.elementBonus` 条目里的元素 Id
→ `MapElement.elementId`。转表器只查主键唯一，不查外键；ConfigVerify（`验证读表.bat`）已校验：
`BuildingRelation` 与 `Building.elementBonus` 微格式与外键、`BuildingVariant` 外键、
footprint 掩码合法性（行长一致、只含 `#`/`.`、至少一个 `#`）、`Level.pool` 条目格式
（`变体Id:数量`，数量为正整数）与外键。

三条不进表的全局规则（写死在代码/由字段组合表达）：巨型风车"专属替代通用"的结算逻辑、
船坞"每座只归属一个锚点（最近优先/少者优先）"、物流覆盖"同建筑只计一次"。

**配表驱动美术资产**：`Assets/Res/<资产名>/fbx/*.fbx` 由菜单 **Tools/美术/生成白模 Prefab** 转成
`Assets/Resources/Prefab/{Building,Element,Stage}/<id>.prefab`，路径填进配表的 `prefabPath` 列。
对齐由 [ModelPrefabGenerator](../Assets/Script/Config/Editor/ModelPrefabGenerator.cs) 在**生成 Prefab 时**做
（不是导入时）：按 `footprint` 的格数 × `GameConfig.cellSize` 算目标尺寸，把模型 XZ 包围盒等比缩放到
刚好放进占地（取 min 保证不越格），再在占地矩形里 **XZ 居中、底面 y=0**。

生成出来的 Prefab 是**两层结构**，这是整条表现链路的地基，别去动它：

```
<id>              ← 包装根：identity（pos 0 / rot 0 / scale 1），原点 = 占地矩形最小角
└── <FBX 实例>    ← 承载 Z-up→Y-up 轴向修正(-90°X)、cm 单位换算(×100)、按格缩放、居中偏移
```

摆放时表现层只碰包装根，`ModelSpawner.PlaceAt` 把它放到锚点格角点并按朝向补一段平移
（`Footprint` 的占地恒从锚点向 +X/+Z 展开，而 Unity 绕 Y 轴转会把矩形甩向负半轴）。

> **工作流铁律：改了 FBX、改了 `footprint` 或改了 `cellSize`，必须重跑一次生成菜单。**
> 对齐结果是烤在 Prefab 的 transform override 里的，重导 FBX 只换网格、不重算缩放和轴心——
> 会出现「模型换了、尺寸没换」且哪里都不报错。兜底有两层：FBX 重导后会自动跑一次校验，
> 也可以随时手动点 **Tools/美术/校验模型对位**（查包装根是否 identity、模型是否居中贴地不越格、
> Prefab 与配表 `prefabPath` 是否互相对得上）。
> `BuildingModelPostprocessor.GetVersion()` 现在只管导入开关（关动画/相机/灯光），改那些才需要 +1。

**模型形状对不对得上配表**：跑 **Tools/美术/打印模型实际占格**，它把每个模型的三角形投影到 XZ 平面、
每格 9×9 采样量出实际覆盖，和配表 footprint 并排打出来（明细写到 `Temp/footprint_probe.txt`）。
注意两类"不一致"的性质完全不同：

- **比例不符 = 真问题。** 工具是等比缩放取 min，模型底面外接矩形的长宽比必须等于 footprint 的长宽比，
  否则必然有一维填不满、模型缩在中间。截至 2026-08-09 有三个：
  `residence_02`（实测 3.29×4 m，需 6×4）、`residence_03`（3.53×4 m，需 6×4）、
  `logisticsHub_01`（5.86×8 m，需 8×8）。已提美术按格数重做，配表不动。
- **轮廓不是方的 = 正常，别改。** `workshop_01`（圆形/十字）、`dock_01`（船形）、`giantWindmill`（八边形底座）
  的外接矩形比例都是对的，只是四角空出来——占地格是规则上的地皮权属，模型不必填满每一格。
  探针会一直把它们标成不一致，这是预期内的。

> **踩过的三个坑（都出在"在哪个空间量包围盒"）：**
> 1. **单位换算要算进去。** 这批 FBX 以厘米为单位，导入器把 `root.localScale` 设成了 100。
>    按局部尺寸求倍数再 `*=` 上去等于把 100 又乘一遍，4 m 的房子会变 400 m。
>    现在在 identity 包装根下量**世界**包围盒，这个 100 天然包含在内，不用再单独补。
> 2. **不能在模型根的局部空间量。** 模型根自带 -90°X 的轴向修正，它的局部空间还是 Z-up ——
>    在那里量出来的 `size.z` 是**高度**不是进深，按格缩放会拿目标进深去除以高度。包一层壳再量才对。
> 3. **模型要居中，不能贴占地最小角。** min() 缩放注定有一轴填不满；贴角的话转 180° 模型会翻到
>    占地矩形另一侧，同一栋楼滚轮转半圈就横跳一整格（2×1 占地实测跳 2 m）。

想重建 Excel：删 `Tables\FloatingIsland.xlsx` 后跑 `dotnet run --project Tools\TableTool -- bootstrap`
（种子在 `Tools\TableTool\bootstrap\FloatingIsland-*.seed.json`）。注意 Excel 才是正本——
日常改表**直接改 xlsx**，种子只在重建初始表时有用，不会随 xlsx 更新。

---

## 数值标定：积分曲线仿真

`Tools/BalanceSim` 是一个脱离 Unity 的控制台工程（链接编译领域层 + 读表层），
用贪心 AI 玩家把一局跑完，用来判定 **建筑组配表 / 得分配表 / 范围配表** 的难易度：

```
dotnet run --project Tools\BalanceSim -- --runs 30                # 按真实金币门槛跑
dotnet run --project Tools\BalanceSim -- --runs 30 --free-unlock  # 免费解锁，量「不被卡住时的收入曲线」
```

**定价顺序不能反**：先用 `--free-unlock` 量出每级收入，再把 `Level.unlockCost` 定到
「总支出 ≈ 总收入 × 85%」上。拿一个自己卡住自己的样本去调价，量到的收入是被截断的。

同理，判据不能拿**累计金币**比**单级费用**——金币只进不出，累计额必然远大于单级价，
那样算出来永远是「偏易」。用「本级收入 / 本级解锁价」的覆盖率才能看出压力分布。

当前标定结果（stage_1，30 局）：平均到达 **19.7 / 20** 级，通关率 **83%**，
总支出占总收入 **85%**，单栋均分从 27 成长到 ~105；前期覆盖率 1.2~2.1（学习期宽松），
后期 L14/15/17/18 覆盖率 0.7~0.96 需要吃老本（设计要的压力区）。
贪心 AI 比真人强得多，所以 83% 对真人而言已经是有真实失败风险的强度；
再往下调需要真实试玩数据，而不是继续跑仿真。

仿真还报出了一个**内容问题而非数值问题**：船坞占地 6×6，而地图生成器最初只刷 3 格宽的
浮空区域环——船坞永远找不到合法落点，直接变成废牌。环宽改成 8 格后，
「放不下而跳过的建筑」从每局 2~4 栋降到 0。
