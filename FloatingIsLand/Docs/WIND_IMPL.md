# 风路实现设计（数据层 + 表现层）

> 本文档是 [GAME_DESIGN.md](GAME_DESIGN.md) §8/§9/§10.4–10.5 的实现依据，对应 [PROJECT_BUILD.md](PROJECT_BUILD.md) 的 M3 里程碑。
> 规则口径以 GAME_DESIGN 为正本；本文记录实现选型、数据结构与算法。规则若变，先改 GAME_DESIGN 再同步本文。

---

## 1. 模型总览：叠加场（Superposition）

风采用**叠加场模型**，一条总纲推出全部性质：

> **风与风之间不相互影响。** 能影响一股风的路径与长度的只有建筑（风帆、物流点）；
> 多股风途经同一格时，该格做向量合成，但合成结果**只决定这一格的显示与计分**，不改变任何一股风的走向、强度或长度。

由此获得的性质：

| 性质 | 说明 |
|---|---|
| 每股风独立计算 | 传播时互相不可见，无合并成新股、无截断、无级联 |
| 重算 = 两遍扫描 | ① 逐股独立传播 → ② 逐格合成；无迭代、结果与处理顺序无关 |
| 终止有保证 | 总路径长度 ≤ 初始长度 + 物流延长上限×延长值；不需要任何循环截断规则 |
| 抵消/增强天然局部化 | 只发生在重叠格上；越过对方终点后各自恢复原有风力 |
| 剩余长度通过覆盖范围参与交汇 | 短风只覆盖得到少数格，影响天然有限；合成公式中**不引入长度权重** |

允许的"极端"情形（都是合法玩法，不做特殊处理）：

- **自交**：风可再次经过自己走过的格子，该格出现同股的两个分量，照常合成（风可以自我增强）；
- **风帆重复经过**：同一股风经过同一风帆的次数不限，每次都按该风帆的左/右设定转向；
- **绕圈**：风被风帆绕成环，就沿环走到风长耗尽为止。

## 2. 数据层（`Assets/Script/Domain/Wind/`，纯 C#，asmdef `Game.Domain` 无引擎引用）

### 2.1 风股 WindStream

正本是"风源 + 地图"；路径字段全部是传播结果的缓存，可随时重算。

```csharp
public sealed class WindStream {
    public int Id;                          // 源序号派生，稳定（渲染流线/调试用）
    public Coord Origin;                    // 起点
    public Dir8 OriginDir;                  // 出发方向
    public int InitialForce;                // 初始强度 1~5
    public int InitialLength;               // 初始长度（格）

    // ---- 传播产物（缓存）----
    public List<TurnPoint> Turns;           // 拐点：哪格、左转还是右转（由风帆产生）
    public List<LengthModifier> LengthMods; // 长度影响账本
    public Coord End;                       // 终点
    public EndReason EndReason;             // LengthExhausted / OutOfMap
    public int LogisticsFromIndex;          // 从路径第几格起变为物流风（-1 = 从未）

    public int CurrentLength => InitialLength + LengthMods.Sum(m => m.Delta);
}

public readonly struct TurnPoint { public Coord Cell; public TurnDir Turn; }   // Left / Right

public readonly struct LengthModifier {
    public Coord SourceCell;    // 影响来源（物流点坐标；预留其他因素）
    public ModifierKind Kind;   // LogisticsExtend / ...
    public int Delta;
}
```

要点：

- **路径完全可推导**：`起点 + 出发方向 + 拐点列表 + CurrentLength` 唯一确定整条折线；存档/回放只需存风源与建筑。
- **账本一物两用**：§10.5 两条限制直接查账本——"同点不重复" = `LengthMods` 中无该 `SourceCell`；"每股最多 2 次" = `Count(Kind == LogisticsExtend) < windExtendMaxPerWind`。
- 不需要 VisitedCells / VisitedSails 集合（无截断规则）。

### 2.2 格子风路层 WindCellState

朝向/强度由后台系统（叠加合成）算好挂在格子上，计分与渲染只读缓存结果。

```csharp
public sealed class WindCellState {
    public List<WindPass> Passes;   // 每股在该格的原始分量（自交时同股可出现多条）
    public Dir8 ResultDir;          // 合成方向（吸附 45°；ResultForce==0 时无意义）
    public int ResultForce;         // 合成强度 0~5（0 = 无风/完全抵消）
    public bool IsLogistics;        // 任一分量在该格已是物流风
}

public readonly struct WindPass {
    public int StreamId; public Dir8 Dir; public int Force; public bool IsLogistics;
}
```

### 2.3 对外查询 WindField

```csharp
public sealed class WindField {
    public bool TryGetWind(Coord c, out WindCellState cell);  // 渲染、风帆建造条件
    public int GetForce(Coord c);          // 无风返回 0（居民区穿风惩罚、船坞曲线、风帆即时分）
    public bool IsLogisticsWind(Coord c);  // 物流覆盖集合刷新
    public IReadOnlyList<WindStream> Streams { get; }         // 流线渲染、调试
    public IEnumerable<(Coord, WindCellState)> AllCells { get; }
}
```

计分、合法性校验、表现层一律走 `ResultDir / ResultForce / IsLogistics`；`Passes` 仅供风帆逐股转向、流线渲染与调试。

## 3. 重算算法（两遍扫描）

```csharp
public sealed class WindSimulator {
    // 纯函数：地图状态进，风场出。预览 = 对"假设放了这栋"的地图副本再跑一次
    public WindField Recompute(MapSnapshot map, WindRulesSnapshot rules);
}
```

```
Pass 1 逐股独立传播（互相不可见）：
    从每个风源生成风股，沿方向逐格推进：
    - 每格消耗 1 段风长（斜向一步与正交一步同价，切比雪夫度量）；
    - 写出该格 WindPass（方向、原始强度、当前是否物流风）；
    - 该格有风帆 → 记 TurnPoint，按风帆左/右设定转 45°（不恢复风长，重复经过不限次）；
    - 该格有物流点 → 置为物流风；若账本允许（同点不重复、次数未满）则记 LengthModifier 延长；
    - 剩余长度 0 或出图 → 记 End/EndReason，本股结束。

Pass 2 逐格叠加合成：
    对每个有 Passes 的格子执行 §4 的合成公式，缓存 ResultDir / ResultForce / IsLogistics。
```

- **全量重算**：每次影响风的操作（放/拆风帆、物流点）后整场重跑。复杂度 = 所有风股路径格数之和 + 有风格子数，64×64 规模下可忽略。
- **确定性**：传播互不可见 + 合成为纯函数，与股序无关；风源按序号稳定编号即可复现。
- 地形不阻挡风：风可越过虚空/浮空区域，仅出网格边界终止（待沙盒验证的默认值）。

## 4. 交汇合成公式

```
V = Σ Force_i × unit(Dir_i)      // unit = 八方向单位向量，斜向分量 ±√2/2（不是 ±1）
强度 = min(maxWindLevel, round(|V|))   // 标准四舍五入（2.5→3）；先取整后封顶；0 = 无风
方向 = angle(V) 吸附到最近 45°；恰好居中（22.5°）时取顺时针一侧
```

三条实现红线（各配单测）：

1. **斜向必须用单位向量**。用格子向量 (1,1) 会让斜向风权重天生大 41%，整个平衡是歪的。
2. **22.5° 平局裁决固定为顺时针**。等强度相邻 45° 风的合成必然落在角平分线上（如 2 级东 + 2 级东北），
   实现用 epsilon（1e-6）判定"居中"，防止浮点误差导致同局面结果漂移。
3. **封顶只作用于格子的合成结果**。参与合成的永远是各 Pass 的原始强度（自交增强同理）。

基准用例（全部固化为 EditMode 单测）：

| 参与风 | V | 模长 | 强度 | 方向 |
|---|---|---:|---:|---|
| 3级东 + 2级东 | (5, 0) | 5.00 | 5 | 东 |
| 3级东 + 2级北 | (3, 2) | 3.61 | 4 | 东北（33.7°→45°） |
| 3级东 + 3级西 | (0, 0) | 0 | 0 无风 | — |
| 5级东 + 2级西 | (3, 0) | 3.00 | 3 | 东 |
| 4级东 + 3级东南 | (6.12, −2.12) | 6.48 | 6→封顶 5 | 东（−19.1°→0°） |
| 2级东 + 2级东北 | (3.41, 1.41) | 3.70 | 4 | 恰好 22.5° → 顺时针取东 |

## 5. 配置（已有表，无需新表）

| 表 | 字段 | 用途 |
|---|---|---|
| `WindConfig`（单例） | maxWindLevel | 合成强度封顶（=5） |
| | initialWindLevelMin/Max、initialWindLengthMin/Max | 风源随机参数（数量在 `MapElement.windSource`） |
| `LogisticsConfig`（单例） | windExtendLength | 物流点单次延长格数 |
| | windExtendMaxPerWind | 每股延长次数上限（=2） |
| `WindLevel`（行表） | level、nameCn、scoreMultiplier | 等级名称与通用风力倍率（UI/计分） |
| `Building` | windScoreByLevel 等曲线列 | 各建筑的风力收益/惩罚曲线 |

固定规则（不进表，写死并单测锁定）：四舍五入取整、顺时针平局吸附、单位向量权重、斜向 1 格 1 段风长、交汇不改风长。

## 6. 调用时序与领域事件

```
放/拆 风帆、物流点 落地
  → App 层调 WindSimulator.Recompute(map, rules)
  → 替换 GameState.WindField
  → 物流覆盖集合刷新（依赖物流风，见 Domain/Logistics）
  → 广播 WindFieldChanged(oldField, newField) → 表现层刷新
```

计分引擎不订阅事件，结算/预览时直接读当前 `WindField`（风帆即时分与船坞曲线查 `GetForce`，居民区强风惩罚查落点格，物流覆盖查 `IsLogisticsWind`）。风帆建造条件 = 目标格 `ResultForce ≥ 1`。

## 7. 表现层（`Assets/Script/View/Wind/`）

```
View/Wind/
├── WindFieldView.cs        // 订阅 WindFieldChanged，触发重建
├── WindArrowMeshBuilder.cs // 把 AllCells 烘成单个程序化 Mesh（每有风格一个箭头 quad）
└── WindPreviewView.cs      // 摆放预览的 diff 叠加层
```

- **单 Mesh 全量重建**，不做每格 GameObject：64×64 最多四千 quad，重建在 1ms 量级；箭头朝向 = `ResultDir`，顶点色编码风力 1~5 颜色梯度，物流风独立色相/描边；Unlit 顶点色 shader（内置管线够用），UV 沿箭头方向滚动、滚速正比风力表达流动感。
- **预览 diff**（对齐 §7.4"高亮当前风路/风力"）：摆放风帆/物流点时用假设地图干跑 `Recompute` 得 previewField，对新旧 WindField 做逐格 diff 画半透明叠加：新增风段=绿、消失=红、方向或风力变化=黄。确认落地后清叠加层，主层随真实事件刷新。
- 流线渲染（按 `Streams` 的折线串格）非 MVP 必需，M6 打磨期再加。
- 坐标换算复用地图渲染的同一换算器；View 只读事件载荷，不触碰 Domain 内部状态。

## 8. 单测清单（EditMode）

- 直线传播长度耗尽；斜向一步与正交一步同价；
- 风帆左/右转向、不恢复风长；同一风帆重复经过每次都转向；
- §4 基准用例表逐条（含封顶、平局顺时针、单位向量权重一致性）；
- 抵消只在重叠格：3级东对撞3级西，重叠段 0 级，越过对方终点后各自恢复；
- 同向增强只持续到较短一股耗尽处；
- 自交格双分量合成（自我增强）；绕圈风在风长耗尽处终止（终止有界）；
- 交汇不改风长（两股交叉后各自剩余长度不变）；
- 物流延长：同点不重复、每股 ≤ windExtendMaxPerWind；LogisticsFromIndex 之后的格才算物流风；
- 合成为纯函数：股序打乱结果不变。

## 9. 决策记录

| # | 决策 | 依据/理由 |
|--:|---|---|
| 1 | 叠加场模型：风与风互不影响，合成只作用于交汇格的显示与计分 | 消灭合并截断的级联与时序问题；重算退化为两遍扫描 |
| 2 | 每格向量合成用"强度 × 单位向量"，不引入长度权重 | 剩余长度已通过覆盖范围参与交汇；长度权重会导致重叠带内方向逐格漂移、示例表失效、可预测性崩坏 |
| 3 | 强度 = 模长四舍五入后封顶 5 | §8.5 示例"3东+2北≈4级"反推：√13≈3.61 只有 round 能得 4 |
| 4 | 方向 45° 吸附，22.5° 平局取顺时针 | 等强度相邻风必然产生平局，需要确定可复现的裁决 |
| 5 | 斜向一步消耗 1 段风长 | 与全局切比雪夫度量一致；风长=可数格数，保住心算可预测性 |
| 6 | 交汇不改风长；风长只受初始值与物流延长影响 | 原 §8.6"取较长"作废；无限风长从机制上不可能 |
| 7 | 自交不截断、风帆重复经过不限次 | 终止由风长有界天然保证；原 §8.7 循环截断作废 |
| 8 | 长度影响用账本（LengthModifier 列表）记录 | 可审计，且直接实现 §10.5 两条限制 |
| 9 | 地形不阻挡风（默认） | 设计未要求阻挡；船坞在浮空区域必须有风可达；沙盒实测后可调 |
