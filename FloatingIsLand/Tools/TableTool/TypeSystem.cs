using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace TableTool;

public static class TypeSystem
{
    private static readonly Dictionary<string, FieldType> NameToType = new(StringComparer.Ordinal)
    {
        ["int"] = FieldType.Int,
        ["long"] = FieldType.Long,
        ["float"] = FieldType.Float,
        ["bool"] = FieldType.Bool,
        ["string"] = FieldType.String,
        ["int[]"] = FieldType.IntArray,
        ["float[]"] = FieldType.FloatArray,
        ["string[]"] = FieldType.StringArray,
    };

    /// <summary>C# 关键字（生成公共字段时禁止用作字段名/表名）。</summary>
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract","as","base","bool","break","byte","case","catch","char","checked","class","const",
        "continue","decimal","default","delegate","do","double","else","enum","event","explicit","extern",
        "false","finally","fixed","float","for","foreach","goto","if","implicit","in","int","interface",
        "internal","is","lock","long","namespace","new","null","object","operator","out","override",
        "params","private","protected","public","readonly","ref","return","sbyte","sealed","short",
        "sizeof","stackalloc","static","string","struct","switch","this","throw","true","try","typeof",
        "uint","ulong","unchecked","unsafe","ushort","using","virtual","void","volatile","while",
    };

    public static bool TryParseTypeName(string raw, out FieldType type)
        => NameToType.TryGetValue((raw ?? "").Trim(), out type);

    public static string KnownTypeNames => string.Join(" / ", NameToType.Keys);

    /// <summary>C# 源码里的类型写法。</summary>
    public static string CSharpName(FieldType t) => t switch
    {
        FieldType.Int => "int",
        FieldType.Long => "long",
        FieldType.Float => "float",
        FieldType.Bool => "bool",
        FieldType.String => "string",
        FieldType.IntArray => "int[]",
        FieldType.FloatArray => "float[]",
        FieldType.StringArray => "string[]",
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    /// <summary>类型默认值（空单元格 = 默认值）。</summary>
    public static object DefaultValue(FieldType t) => t switch
    {
        FieldType.Int => 0,
        FieldType.Long => 0L,
        FieldType.Float => 0f,
        FieldType.Bool => false,
        FieldType.String => "",
        FieldType.IntArray => Array.Empty<int>(),
        FieldType.FloatArray => Array.Empty<float>(),
        FieldType.StringArray => Array.Empty<string>(),
        _ => throw new ArgumentOutOfRangeException(nameof(t)),
    };

    /// <summary>合法 C# 标识符且非关键字（字段名 / 表名校验）。</summary>
    public static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (CSharpKeywords.Contains(name)) return false;
        if (!(char.IsLetter(name[0]) || name[0] == '_')) return false;
        return name.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    // ---------- 文本 → 值（InvariantCulture），失败时返回 false ----------

    public static bool TryParseInt(string text, out int value)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        // 允许 "3.0" 这类“值为整数”的写法；"3.5" 仍为错误
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && d == Math.Truncate(d) && d >= int.MinValue && d <= int.MaxValue)
        {
            value = (int)d;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryParseLong(string text, out long value)
    {
        text = text.Trim();
        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            && d == Math.Truncate(d) && d >= long.MinValue && d <= long.MaxValue)
        {
            value = (long)d;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryParseFloat(string text, out float value)
    {
        if (double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            value = (float)d;
            return true;
        }
        value = 0f;
        return false;
    }

    /// <summary>bool 接受 TRUE/FALSE/1/0/是/否（TRUE/FALSE 不区分大小写）。</summary>
    public static bool TryParseBool(string text, out bool value)
    {
        switch (text.Trim().ToUpperInvariant())
        {
            case "TRUE": case "1": case "是": value = true; return true;
            case "FALSE": case "0": case "否": value = false; return true;
            default: value = false; return false;
        }
    }

    /// <summary>double 单元格按 int 取值：必须是整数且在 int 范围内。</summary>
    public static bool TryNumberToInt(double d, out int value)
    {
        if (d == Math.Truncate(d) && d >= int.MinValue && d <= int.MaxValue)
        {
            value = (int)d;
            return true;
        }
        value = 0;
        return false;
    }

    public static bool TryNumberToLong(double d, out long value)
    {
        if (d == Math.Truncate(d) && d >= long.MinValue && d <= long.MaxValue)
        {
            value = (long)d;
            return true;
        }
        value = 0;
        return false;
    }

    public static string FormatNumber(double d) => d.ToString("R", CultureInfo.InvariantCulture);
}
