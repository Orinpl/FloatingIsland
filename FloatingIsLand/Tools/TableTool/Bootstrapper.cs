using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TableTool;

/// <summary>
/// bootstrap 命令：读 seedDir 下 *.seed.json，按 workbook 分组生成 tablesDir/{workbook}.xlsx。
/// 种子格式（一个文件一张表）：
/// {
///   "workbook": "Core",            // 目标工作簿名（可带可不带 .xlsx）
///   "sheetOrder": 1,               // 工作簿内 Sheet 排序
///   "table": "RentPeriod",        // 表名 = Sheet 名
///   "kind": "rows" | "singleton",
///   "fields": [ { "name": "period", "type": "int", "desc": "期数" }, ... ],
///   "rows": [ { "period": 1, "rentG": 100 }, ... ]   // singleton 取 rows[0] 作为参数值
/// }
/// 数组值用 | 连接写入单元格，bool 写 TRUE/FALSE；表头加粗、冻结表头行。
/// </summary>
public static class Bootstrapper
{
    private sealed class SeedTable
    {
        public string SeedFile = "";
        public string Workbook = "";
        public int SheetOrder;
        public string Table = "";
        public bool IsSingleton;
        public List<FieldDef> Fields = new();
        public List<JObject> Rows = new();
    }

    /// <summary>返回进程退出码。</summary>
    public static int Run(string tablesDir, string seedDir, bool force)
    {
        var seedFiles = Directory.Exists(seedDir)
            ? Directory.GetFiles(seedDir, "*.seed.json").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).ToArray()
            : Array.Empty<string>();

        if (seedFiles.Length == 0)
        {
            Console.WriteLine($"[bootstrap] 种子文件尚未就绪：未在 {seedDir} 找到 *.seed.json。");
            Console.WriteLine("[bootstrap] 种子写好后重跑本命令即可生成初始 Excel（之后 Tables/*.xlsx 是正本，不再需要种子）。");
            return 0;
        }

        var errors = new ErrorSink();
        var seeds = new List<SeedTable>();
        foreach (var path in seedFiles)
        {
            var seed = ParseSeed(path, errors);
            if (seed != null)
                seeds.Add(seed);
        }

        // 跨种子表名唯一
        var byName = new Dictionary<string, SeedTable>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in seeds)
        {
            if (byName.TryGetValue(s.Table, out var first))
                errors.Add(s.SeedFile, s.Table, "", $"表名与 {first.SeedFile} 中的 '{first.Table}' 冲突（表名须跨工作簿唯一）");
            else
                byName.Add(s.Table, s);
        }

        if (errors.HasErrors)
        {
            errors.Print();
            return 1;
        }

        Directory.CreateDirectory(tablesDir);

        var groups = seeds
            .GroupBy(s => s.Workbook, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 先整体检查目标是否已存在（无 --force 拒绝，且不写任何文件）
        var existing = groups
            .Select(g => Path.Combine(tablesDir, g.Key + ".xlsx"))
            .Where(File.Exists)
            .ToList();
        if (existing.Count > 0 && !force)
        {
            foreach (var p in existing)
                Console.Error.WriteLine($"[错误] 目标已存在：{p}（Excel 是正本，bootstrap 不覆盖；确认要重建请加 --force）");
            return 1;
        }

        foreach (var group in groups)
        {
            var outPath = Path.Combine(tablesDir, group.Key + ".xlsx");
            using var wb = new XLWorkbook();
            foreach (var seed in group.OrderBy(s => s.SheetOrder).ThenBy(s => s.Table, StringComparer.Ordinal))
            {
                if (seed.IsSingleton) WriteSingletonSheet(wb, seed);
                else WriteRowSheet(wb, seed);
            }
            wb.SaveAs(outPath);
            Console.WriteLine($"[bootstrap] 已生成 {outPath}（{group.Count()} 个 Sheet：{string.Join("、", group.OrderBy(s => s.SheetOrder).Select(s => s.Table))}）");
        }

        Console.WriteLine("[bootstrap] 完成。请跑 convert 验证并产出 JSON 与 Tables.g.cs。");
        return 0;
    }

    // ---------------- 种子解析 ----------------

    private static SeedTable ParseSeed(string path, ErrorSink errors)
    {
        var file = Path.GetFileName(path);
        JObject root;
        try
        {
            root = JObject.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            errors.AddGlobal($"{file}: 种子 JSON 解析失败 — {ex.Message}");
            return null;
        }

        var workbook = (root.Value<string>("workbook") ?? "").Trim();
        if (workbook.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            workbook = workbook[..^".xlsx".Length];
        var table = (root.Value<string>("table") ?? "").Trim();
        var kind = (root.Value<string>("kind") ?? "").Trim();

        var seed = new SeedTable
        {
            SeedFile = file,
            Workbook = workbook,
            SheetOrder = root.Value<int?>("sheetOrder") ?? 0,
            Table = table,
        };

        var ok = true;
        if (workbook.Length == 0 || workbook.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        { errors.Add(file, table, "", $"workbook '{workbook}' 非法（须为合法文件名，不带路径）"); ok = false; }
        if (!TypeSystem.IsValidIdentifier(table))
        { errors.Add(file, table, "", $"table '{table}' 不是合法表名（须为合法 C# 标识符，建议 PascalCase）"); ok = false; }

        switch (kind)
        {
            case "rows":
                seed.IsSingleton = false;
                if (table.EndsWith("Config", StringComparison.Ordinal))
                { errors.Add(file, table, "", "行表表名禁止以 Config 结尾（Config 后缀保留给单例参数组）"); ok = false; }
                break;
            case "singleton":
                seed.IsSingleton = true;
                if (!table.EndsWith("Config", StringComparison.Ordinal))
                { errors.Add(file, table, "", "单例参数组表名必须以 Config 结尾（转表器按名字分发布局）"); ok = false; }
                break;
            default:
                errors.Add(file, table, "", $"kind '{kind}' 非法（须为 rows 或 singleton）");
                ok = false;
                break;
        }

        // fields
        if (root["fields"] is not JArray fields || fields.Count == 0)
        {
            errors.Add(file, table, "", "fields 缺失或为空");
            return null;
        }
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (token, index) in fields.Select((t, i) => (t, i)))
        {
            if (token is not JObject fo)
            { errors.Add(file, table, "", $"fields[{index}] 不是对象"); ok = false; continue; }
            var name = (fo.Value<string>("name") ?? "").Trim();
            var typeName = (fo.Value<string>("type") ?? "").Trim();
            if (!TypeSystem.IsValidIdentifier(name))
            { errors.Add(file, table, "", $"fields[{index}].name '{name}' 不是合法 C# 标识符"); ok = false; continue; }
            if (!seenNames.Add(name))
            { errors.Add(file, table, "", $"字段名 '{name}' 重复"); ok = false; continue; }
            if (!TypeSystem.TryParseTypeName(typeName, out var type))
            { errors.Add(file, table, "", $"fields[{index}]('{name}').type '{typeName}' 未知（合法类型：{TypeSystem.KnownTypeNames}）"); ok = false; continue; }
            seed.Fields.Add(new FieldDef { Name = name, Type = type, Desc = (fo.Value<string>("desc") ?? "").Trim(), SourceIndex = index });
        }

        if (!seed.IsSingleton && seed.Fields.Count > 0)
        {
            var pk = seed.Fields[0];
            if (pk.Type != FieldType.Int && pk.Type != FieldType.String)
            { errors.Add(file, table, "", $"行表首字段 '{pk.Name}' 是主键，类型须为 int 或 string（当前 '{TypeSystem.CSharpName(pk.Type)}'）"); ok = false; }
        }

        // rows
        if (root["rows"] is JArray rows)
        {
            foreach (var (token, index) in rows.Select((t, i) => (t, i)))
            {
                if (token is JObject ro)
                    seed.Rows.Add(ro);
                else
                { errors.Add(file, table, "", $"rows[{index}] 不是对象（每行须为 {{字段名: 值}} 对象）"); ok = false; }
            }
        }
        if (seed.IsSingleton && seed.Rows.Count > 1)
            errors.Add(file, table, "", $"单例参数组 rows 最多 1 个对象（实际 {seed.Rows.Count} 个）"); // 视作错误，避免静默丢值

        return ok && seed.Rows.Count <= (seed.IsSingleton ? 1 : int.MaxValue) ? seed : null;
    }

    // ---------------- 写 Excel ----------------

    private static void WriteRowSheet(XLWorkbook wb, SeedTable seed)
    {
        var ws = wb.AddWorksheet(seed.Table);
        for (var c = 0; c < seed.Fields.Count; c++)
        {
            var f = seed.Fields[c];
            ws.Cell(1, c + 1).Value = f.Name;
            ws.Cell(2, c + 1).Value = TypeSystem.CSharpName(f.Type);
            ws.Cell(3, c + 1).Value = f.Desc;
        }
        var header = ws.Range(1, 1, 3, Math.Max(1, seed.Fields.Count));
        header.Style.Font.SetBold();
        ws.SheetView.FreezeRows(3);

        for (var r = 0; r < seed.Rows.Count; r++)
            for (var c = 0; c < seed.Fields.Count; c++)
                SetCell(ws.Cell(4 + r, c + 1), seed.Fields[c], seed.Rows[r][seed.Fields[c].Name]);

        ws.Columns(1, Math.Max(1, seed.Fields.Count)).AdjustToContents();
    }

    private static void WriteSingletonSheet(XLWorkbook wb, SeedTable seed)
    {
        var ws = wb.AddWorksheet(seed.Table);
        ws.Cell(1, 1).Value = "key";
        ws.Cell(1, 2).Value = "type";
        ws.Cell(1, 3).Value = "value";
        ws.Cell(1, 4).Value = "desc";
        ws.Range(1, 1, 1, 4).Style.Font.SetBold();
        ws.SheetView.FreezeRows(1);

        var values = seed.Rows.Count > 0 ? seed.Rows[0] : new JObject();
        for (var i = 0; i < seed.Fields.Count; i++)
        {
            var f = seed.Fields[i];
            ws.Cell(2 + i, 1).Value = f.Name;
            ws.Cell(2 + i, 2).Value = TypeSystem.CSharpName(f.Type);
            SetCell(ws.Cell(2 + i, 3), f, values[f.Name]);
            ws.Cell(2 + i, 4).Value = f.Desc;
        }

        ws.Columns(1, 4).AdjustToContents();
    }

    /// <summary>把种子里的 JSON 值按字段类型写入单元格（缺失/null 留空 = 类型默认值）。</summary>
    private static void SetCell(IXLCell cell, FieldDef field, JToken token)
    {
        if (token == null || token.Type == JTokenType.Null)
            return;

        switch (field.Type)
        {
            case FieldType.Int:
                cell.Value = token.Value<int>();
                break;
            case FieldType.Long:
                cell.Value = token.Value<long>();
                break;
            case FieldType.Float:
                cell.Value = token.Value<double>();
                break;
            case FieldType.Bool:
            {
                var b = token.Type == JTokenType.String
                    ? TypeSystem.TryParseBool(token.Value<string>(), out var parsed) && parsed
                    : token.Value<bool>();
                cell.Value = b ? "TRUE" : "FALSE"; // 按约定写文本 TRUE/FALSE
                break;
            }
            case FieldType.String:
                cell.Value = TokenText(token);
                break;
            case FieldType.IntArray:
            case FieldType.FloatArray:
            case FieldType.StringArray:
                cell.Value = token is JArray arr
                    ? string.Join("|", arr.Select(TokenText))
                    : TokenText(token); // 也接受已用 | 连接好的字符串
                break;
        }
    }

    private static string TokenText(JToken token) => token.Type switch
    {
        JTokenType.Float => token.Value<double>().ToString("R", CultureInfo.InvariantCulture),
        JTokenType.Integer => token.Value<long>().ToString(CultureInfo.InvariantCulture),
        JTokenType.Boolean => token.Value<bool>() ? "TRUE" : "FALSE",
        _ => token.Value<string>() ?? "",
    };
}
