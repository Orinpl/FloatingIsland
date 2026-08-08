using System.Collections.Generic;

namespace TableTool;

/// <summary>配表字段类型（Excel 第 2 行 / 种子 fields[].type 的合法值）。</summary>
public enum FieldType
{
    Int,
    Long,
    Float,
    Bool,
    String,
    IntArray,
    FloatArray,
    StringArray,
}

/// <summary>一列（行表）或一个参数（单例 KV 行）。</summary>
public sealed class FieldDef
{
    public string Name = "";
    public FieldType Type;
    public string Desc = "";
    /// <summary>来源 Excel 列号（1 基，行表用）或 KV 行号（单例用），仅用于报错定位。</summary>
    public int SourceIndex;
}

/// <summary>一张解析完成的表（行表或单例参数组）。</summary>
public sealed class TableDef
{
    /// <summary>工作簿文件名（含扩展名），如 Core.xlsx。</summary>
    public string Workbook = "";
    /// <summary>工作簿内 Sheet 序号（1 基），用于确定性排序。</summary>
    public int SheetIndex;
    /// <summary>表名 = Sheet 名。</summary>
    public string Name = "";
    public bool IsSingleton;
    public List<FieldDef> Fields = new();
    /// <summary>行表数据：每行一个 object[]，与 Fields 对齐（boxed int/long/float/bool/string/数组）。</summary>
    public List<object[]> Rows = new();
    /// <summary>单例数据：与 Fields 对齐。</summary>
    public object[] SingletonValues = System.Array.Empty<object>();
    /// <summary>行表主键类型（仅 Int / String 合法）。</summary>
    public FieldType KeyType;
}
