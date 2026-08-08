using System;
using System.IO;
using System.Linq;
using TableTool;

// TableTool 入口：convert / check / bootstrap（用法见 Docs/CONFIG_TABLES.md）
//
// 目录布局（相对 Unity 工程根，root = 含 Assets/ 与 Tools/ 的那一层）：
//   {root}/Tables/*.xlsx                          Excel 正本（输入）
//   {root}/Assets/Resources/Tables/*.json         转出的 JSON（输出，必须在 Resources 下才能被 Unity 读到）
//   {root}/Assets/Script/Config/Generated/Tables.g.cs  生成的强类型访问代码（输出）
//   {root}/Tools/TableTool/bootstrap/*.seed.json  建表种子（仅 bootstrap 用）
// 想换布局只改下面 4 个常量即可。
const string TablesDirName = "Tables";
const string JsonOutRelPath = "Assets/Resources/Tables";
const string CodeOutRelPath = "Assets/Script/Config/Generated/Tables.g.cs";
const string SeedRelPath = "Tools/TableTool/bootstrap";
const string DefaultNamespace = "FloatingIsLand.Config";

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";
var force = args.Contains("--force", StringComparer.OrdinalIgnoreCase);
var rootArg = OptionValue("--root");
var ns = OptionValue("--namespace") ?? DefaultNamespace;

var root = rootArg ?? DetectRoot();
if (root == null)
{
    Console.Error.WriteLine("[错误] 未能定位 Unity 工程根（需同时含 Assets/ 与 Tools/TableTool/TableTool.csproj 的目录），请用 --root 指定。");
    return 1;
}

switch (command)
{
    case "bootstrap":
        return Bootstrapper.Run(Path.Combine(root, TablesDirName), Path.Combine(root, ToPath(SeedRelPath)), force);
    case "check":
        return Convert(root, writeOutputs: false);
    case "convert":
        return Convert(root, writeOutputs: true);
    default:
        Console.Error.WriteLine("用法: TableTool <convert|check|bootstrap> [--force] [--root <Unity 工程根>] [--namespace <生成代码命名空间>]");
        return command.Length == 0 ? 1 : 2;
}

string OptionValue(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}

// 从当前工作目录与程序所在目录分别向上找 Unity 工程根（bin/ 下运行也能定位）。
// 判据是「同时含 Assets/ 与 Tools/TableTool/TableTool.csproj」——只认 Tables/ 会在
// bin/ 下向上走时误命中 Tools/，只认 csproj 会命中 Tools/ 本身。
static string DetectRoot()
{
    foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "Assets"))
                && File.Exists(Path.Combine(dir.FullName, "Tools", "TableTool", "TableTool.csproj")))
                return dir.FullName;
    return null;
}

static string ToPath(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);

int Convert(string rootDir, bool writeOutputs)
{
    var tablesDir = Path.Combine(rootDir, TablesDirName);
    if (!Directory.Exists(tablesDir))
    {
        Console.Error.WriteLine($"[错误] 不存在 {tablesDir}（先跑 bootstrap 生成初始 Excel）。");
        return 1;
    }

    var xlsxPaths = Directory.GetFiles(tablesDir, "*.xlsx")
        .Where(p => !Path.GetFileName(p).StartsWith("~$", StringComparison.Ordinal)) // Excel 锁文件
        .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    if (xlsxPaths.Length == 0)
    {
        Console.Error.WriteLine($"[错误] {tablesDir} 下没有 xlsx。");
        return 1;
    }

    var errors = new ErrorSink();
    var tables = ExcelReader.ReadWorkbooks(xlsxPaths, errors);
    if (errors.HasErrors)
    {
        errors.Print();
        Console.Error.WriteLine($"[{(writeOutputs ? "convert" : "check")}] 校验失败，未写任何输出。");
        return 1;
    }

    Console.WriteLine($"[{(writeOutputs ? "convert" : "check")}] 校验通过：{xlsxPaths.Length} 个工作簿 / {tables.Count} 张表（{tables.Count(t => !t.IsSingleton)} 行表 + {tables.Count(t => t.IsSingleton)} 单例）。");
    if (!writeOutputs)
        return 0;

    var jsonDir = Path.Combine(rootDir, ToPath(JsonOutRelPath));
    Directory.CreateDirectory(jsonDir);
    foreach (var stale in Directory.GetFiles(jsonDir, "*.json")) // 全量重写，清陈旧表
        File.Delete(stale);
    foreach (var table in tables)
        JsonEmitter.WriteTable(table, jsonDir);

    var genPath = Path.Combine(rootDir, ToPath(CodeOutRelPath));
    Directory.CreateDirectory(Path.GetDirectoryName(genPath)!);
    CodeGenerator.WriteTo(genPath, tables, ns);

    Console.WriteLine($"[convert] 已写 {tables.Count} 个 JSON → {jsonDir}");
    Console.WriteLine($"[convert] 已写生成代码 → {genPath}（namespace {ns}）");
    return 0;
}
