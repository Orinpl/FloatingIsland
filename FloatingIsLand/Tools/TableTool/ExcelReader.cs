using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace TableTool;

/// <summary>
/// 读取 Tables/*.xlsx 并按 10-ConfigTables.md §1.3 布局解析 + 校验。
/// 错误全部进 ErrorSink（带 文件!Sheet!单元格 坐标），尽量收集完所有错误而非 fail-fast。
/// </summary>
public static class ExcelReader
{
    private const string SingletonSuffix = "Config";

    /// <summary>解析一批工作簿（路径已按文件名排序、已剔除 ~$ 临时文件）。</summary>
    public static List<TableDef> ReadWorkbooks(IReadOnlyList<string> xlsxPaths, ErrorSink errors)
    {
        var tables = new List<TableDef>();
        // 表名 → 首次出现的 "文件!Sheet"（跨工作簿冲突检测，Windows 下 JSON 文件名不区分大小写）
        var nameOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in xlsxPaths)
        {
            var file = Path.GetFileName(path);
            XLWorkbook wb;
            try
            {
                wb = new XLWorkbook(path);
            }
            catch (Exception ex)
            {
                errors.AddGlobal($"{file}: 打开工作簿失败 — {ex.Message}");
                continue;
            }

            using (wb)
            {
                foreach (var ws in wb.Worksheets)
                {
                    var sheetName = ws.Name.Trim();
                    if (sheetName.StartsWith("#", StringComparison.Ordinal))
                        continue; // 整页注释，跳过

                    if (!TypeSystem.IsValidIdentifier(sheetName))
                    {
                        errors.Add(file, ws.Name, "", $"Sheet 名 '{ws.Name}' 不是合法的表名（须为合法 C# 标识符，建议 PascalCase）");
                        continue;
                    }

                    var table = sheetName.EndsWith(SingletonSuffix, StringComparison.Ordinal)
                        ? ReadSingletonSheet(file, ws, sheetName, errors)
                        : ReadRowSheet(file, ws, sheetName, errors);
                    if (table == null)
                        continue;

                    if (nameOwner.TryGetValue(sheetName, out var owner))
                    {
                        errors.Add(file, sheetName, "", $"表名与 {owner} 冲突（表名须跨工作簿唯一）");
                        continue;
                    }
                    nameOwner[sheetName] = $"{file}!{sheetName}";
                    tables.Add(table);
                }
            }
        }
        return tables;
    }

    // ---------------- 行表布局 ----------------
    // 第 1 行字段名 / 第 2 行类型 / 第 3 行说明 / 第 4 行起数据；# 前缀列跳过；第 1 列 = 主键（int|string，非空唯一）

    private static TableDef ReadRowSheet(string file, IXLWorksheet ws, string tableName, ErrorSink errors)
    {
        var lastHeaderCol = ws.Row(1).LastCellUsed()?.Address.ColumnNumber ?? 0;
        if (lastHeaderCol == 0)
        {
            errors.Add(file, tableName, "A1", "行表第 1 行（字段名行）为空");
            return null;
        }

        var before = errors.Count;
        var fields = new List<FieldDef>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var col = 1; col <= lastHeaderCol; col++)
        {
            var nameCell = ws.Cell(1, col);
            var name = RawString(nameCell.Value).Trim();

            if (col == 1)
            {
                if (name.Length == 0)
                {
                    errors.Add(file, tableName, "A1", "主键列（第 1 列）字段名为空");
                    return null;
                }
                if (name.StartsWith("#", StringComparison.Ordinal))
                {
                    errors.Add(file, tableName, "A1", "主键列（第 1 列）不可为 # 注释列");
                    return null;
                }
            }
            if (name.Length == 0 || name.StartsWith("#", StringComparison.Ordinal))
                continue; // 注释列 / 空列：不导出

            if (!TypeSystem.IsValidIdentifier(name))
            {
                errors.Add(file, tableName, Coord(1, col), $"字段名 '{name}' 不是合法 C# 标识符（须 camelCase，不可为关键字）");
                continue;
            }
            if (!seenNames.Add(name))
            {
                errors.Add(file, tableName, Coord(1, col), $"字段名 '{name}' 重复");
                continue;
            }

            var typeCell = ws.Cell(2, col);
            var typeText = RawString(typeCell.Value).Trim();
            if (!TypeSystem.TryParseTypeName(typeText, out var type))
            {
                errors.Add(file, tableName, Coord(2, col), $"未知类型 '{typeText}'（合法类型：{TypeSystem.KnownTypeNames}）");
                continue;
            }

            fields.Add(new FieldDef
            {
                Name = name,
                Type = type,
                Desc = RawString(ws.Cell(3, col).Value).Trim(),
                SourceIndex = col,
            });
        }

        if (errors.Count > before)
            return null; // 表头有错，不再解析数据行（避免连锁误报）

        if (fields.Count == 0)
        {
            errors.Add(file, tableName, "A1", "行表没有任何可导出的列");
            return null;
        }

        var pk = fields[0];
        if (pk.SourceIndex != 1)
        {
            errors.Add(file, tableName, "A1", "主键列必须是第 1 列");
            return null;
        }
        if (pk.Type != FieldType.Int && pk.Type != FieldType.String)
        {
            errors.Add(file, tableName, Coord(2, 1), $"主键列类型须为 int 或 string，当前为 '{TypeSystem.CSharpName(pk.Type)}'");
            return null;
        }

        var table = new TableDef
        {
            Workbook = file,
            SheetIndex = ws.Position,
            Name = tableName,
            IsSingleton = false,
            Fields = fields,
            KeyType = pk.Type,
        };

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        var seenKeys = new Dictionary<string, int>(StringComparer.Ordinal); // 主键文本 → 首次出现行号
        for (var row = 4; row <= lastRow; row++)
        {
            var pkValue = ws.Cell(row, 1).Value;
            if (IsBlankLike(pkValue))
                continue; // 主键为空的行跳过（可用作空行分隔）

            var values = new object[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                var f = fields[i];
                var cell = ws.Cell(row, f.SourceIndex);
                if (!TryParseCell(cell.Value, f.Type, out values[i], out var err))
                {
                    errors.Add(file, tableName, Coord(row, f.SourceIndex), err);
                    values[i] = TypeSystem.DefaultValue(f.Type);
                }
            }

            var keyText = pk.Type == FieldType.Int
                ? ((int)values[0]).ToString(CultureInfo.InvariantCulture)
                : (string)values[0];
            if (seenKeys.TryGetValue(keyText, out var firstRow))
                errors.Add(file, tableName, Coord(row, 1), $"重复主键 '{keyText}'（与第 {firstRow} 行重复）");
            else
                seenKeys.Add(keyText, row);

            table.Rows.Add(values);
        }
        return errors.Count > before ? null : table;
    }

    // ---------------- 单例 KV 布局 ----------------
    // 第 1 行表头固定 key/type/value/desc，第 2 行起每行一个参数

    private static TableDef ReadSingletonSheet(string file, IXLWorksheet ws, string tableName, ErrorSink errors)
    {
        var header = new[] { "key", "type", "value", "desc" };
        for (var col = 1; col <= 4; col++)
        {
            var got = RawString(ws.Cell(1, col).Value).Trim();
            if (!string.Equals(got, header[col - 1], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(file, tableName, Coord(1, col),
                    $"Sheet 名以 Config 结尾按单例 KV 布局解析，第 1 行表头须为 key/type/value/desc（实际为 '{got}'）；若本表是行表，表名禁止以 Config 结尾");
                return null;
            }
        }

        var before = errors.Count;
        var table = new TableDef
        {
            Workbook = file,
            SheetIndex = ws.Position,
            Name = tableName,
            IsSingleton = true,
        };
        var values = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        for (var row = 2; row <= lastRow; row++)
        {
            var key = RawString(ws.Cell(row, 1).Value).Trim();
            if (key.Length == 0 || key.StartsWith("#", StringComparison.Ordinal))
                continue; // 空行 / 注释行跳过

            if (!TypeSystem.IsValidIdentifier(key))
            {
                errors.Add(file, tableName, Coord(row, 1), $"参数名 '{key}' 不是合法 C# 标识符（须 camelCase，不可为关键字）");
                continue;
            }
            if (!seen.Add(key))
            {
                errors.Add(file, tableName, Coord(row, 1), $"参数名 '{key}' 重复");
                continue;
            }

            var typeText = RawString(ws.Cell(row, 2).Value).Trim();
            if (!TypeSystem.TryParseTypeName(typeText, out var type))
            {
                errors.Add(file, tableName, Coord(row, 2), $"未知类型 '{typeText}'（合法类型：{TypeSystem.KnownTypeNames}）");
                continue;
            }

            if (!TryParseCell(ws.Cell(row, 3).Value, type, out var value, out var err))
            {
                errors.Add(file, tableName, Coord(row, 3), err);
                value = TypeSystem.DefaultValue(type);
            }

            table.Fields.Add(new FieldDef
            {
                Name = key,
                Type = type,
                Desc = RawString(ws.Cell(row, 4).Value).Trim(),
                SourceIndex = row,
            });
            values.Add(value);
        }

        table.SingletonValues = values.ToArray();
        return errors.Count > before ? null : table;
    }

    // ---------------- 单元格取值 ----------------

    /// <summary>空白单元格或纯空白文本视作“空”（= 类型默认值 / 行跳过）。</summary>
    private static bool IsBlankLike(XLCellValue v)
        => v.Type == XLDataType.Blank || (v.Type == XLDataType.Text && v.GetText().Trim().Length == 0);

    /// <summary>单元格的原始文本表示（数值按 InvariantCulture）。</summary>
    private static string RawString(XLCellValue v) => v.Type switch
    {
        XLDataType.Blank => "",
        XLDataType.Text => v.GetText(),
        XLDataType.Number => TypeSystem.FormatNumber(v.GetNumber()),
        XLDataType.Boolean => v.GetBoolean() ? "TRUE" : "FALSE",
        _ => v.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>把单元格值按声明类型解析为 boxed 值；失败时返回 false 并给出错误消息（不含坐标）。</summary>
    public static bool TryParseCell(XLCellValue v, FieldType type, out object value, out string error)
    {
        error = null;
        if (IsBlankLike(v))
        {
            value = TypeSystem.DefaultValue(type);
            return true;
        }

        switch (type)
        {
            case FieldType.Int:
                if (v.Type == XLDataType.Number && TypeSystem.TryNumberToInt(v.GetNumber(), out var i))
                { value = i; return true; }
                if (v.Type == XLDataType.Text && TypeSystem.TryParseInt(v.GetText(), out i))
                { value = i; return true; }
                value = TypeSystem.DefaultValue(type);
                error = $"int 列出现非整数值 '{RawString(v)}'";
                return false;

            case FieldType.Long:
                if (v.Type == XLDataType.Number && TypeSystem.TryNumberToLong(v.GetNumber(), out var l))
                { value = l; return true; }
                if (v.Type == XLDataType.Text && TypeSystem.TryParseLong(v.GetText(), out l))
                { value = l; return true; }
                value = TypeSystem.DefaultValue(type);
                error = $"long 列出现非整数值 '{RawString(v)}'";
                return false;

            case FieldType.Float:
                if (v.Type == XLDataType.Number)
                { value = (float)v.GetNumber(); return true; }
                if (v.Type == XLDataType.Text && TypeSystem.TryParseFloat(v.GetText(), out var f))
                { value = f; return true; }
                value = TypeSystem.DefaultValue(type);
                error = $"float 列无法解析 '{RawString(v)}'";
                return false;

            case FieldType.Bool:
                if (v.Type == XLDataType.Boolean)
                { value = v.GetBoolean(); return true; }
                if (v.Type == XLDataType.Number && (v.GetNumber() == 0 || v.GetNumber() == 1))
                { value = v.GetNumber() == 1; return true; }
                if (v.Type == XLDataType.Text && TypeSystem.TryParseBool(v.GetText(), out var b))
                { value = b; return true; }
                value = TypeSystem.DefaultValue(type);
                error = $"bool 列无法解析 '{RawString(v)}'（接受 TRUE/FALSE/1/0/是/否）";
                return false;

            case FieldType.String:
                value = RawString(v);
                return true;

            case FieldType.IntArray:
            case FieldType.FloatArray:
            case FieldType.StringArray:
                return TryParseArray(RawString(v), type, out value, out error);

            default:
                value = null;
                error = $"未实现的类型 {type}";
                return false;
        }
    }

    private static bool TryParseArray(string raw, FieldType type, out object value, out string error)
    {
        error = null;
        var tokens = raw.Split('|').Select(t => t.Trim()).ToArray();

        switch (type)
        {
            case FieldType.IntArray:
            {
                var arr = new int[tokens.Length];
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (!TypeSystem.TryParseInt(tokens[i], out arr[i]))
                    {
                        value = Array.Empty<int>();
                        error = $"int[] 第 {i + 1} 个元素 '{tokens[i]}' 不是整数（原文 '{raw}'）";
                        return false;
                    }
                }
                value = arr;
                return true;
            }
            case FieldType.FloatArray:
            {
                var arr = new float[tokens.Length];
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (!TypeSystem.TryParseFloat(tokens[i], out arr[i]))
                    {
                        value = Array.Empty<float>();
                        error = $"float[] 第 {i + 1} 个元素 '{tokens[i]}' 无法解析（原文 '{raw}'）";
                        return false;
                    }
                }
                value = arr;
                return true;
            }
            default: // StringArray
                value = tokens;
                return true;
        }
    }

    /// <summary>R1C1 → "B3" 这类 A1 坐标。</summary>
    public static string Coord(int row, int col)
    {
        var letters = "";
        while (col > 0)
        {
            col--;
            letters = (char)('A' + col % 26) + letters;
            col /= 26;
        }
        return letters + row.ToString(CultureInfo.InvariantCulture);
    }
}
