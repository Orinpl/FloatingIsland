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

1. 用 Excel 打开 `Tables\Demo.xlsx`（或自己新建的 xlsx）改数据 / 新建 Sheet
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
UnityTableLoader.LoadFromResources("Tables");

// 之后任意处静态强类型访问
ItemRow item = Tables.Item.Get("gem_ruby");     // 行表：按主键取行（缺键抛带表名的异常）
foreach (MonsterRow m in Tables.Monster.All) {} // 遍历；还有 Count / TryGet / GetOrNull
int gold = Tables.GameConfig.startGold;         // 单例参数组：直接取字段
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

## 关于示例表 Demo.xlsx

`Tables\Demo.xlsx` 是随工具带来的样例，三个 Sheet 刻意覆盖了全部特性，可直接当抄写模板：

| Sheet | 类型 | 演示点 |
|---|---|---|
| `Item` | 行表 | string 主键、int/float/bool 字段、`string[]` 数组列 |
| `Monster` | 行表 | int 主键、`string[]`+`float[]` 双数组、跨表引用（掉落引用 Item 主键） |
| `GameConfig` | 单例 | key/type/value/desc 布局 |

开始写真表后想清掉它：删 `Tables\Demo.xlsx` 与 `Tools\TableTool\bootstrap\Demo-*.seed.json`，
把自己的 xlsx 放进 `Tables\` 再跑一次 `转表.bat` —— 产物是全量重写的，Demo 的 JSON 与类会自动消失。
