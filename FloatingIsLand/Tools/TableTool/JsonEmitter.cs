using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TableTool;

/// <summary>
/// 写出 Assets/Resources/Tables/{表名}.json。
/// 行表 = 数组、单例 = 对象；2 空格缩进、UTF-8 无 BOM、字段序 = Excel 列序。
/// </summary>
public static class JsonEmitter
{
    private static readonly UTF8Encoding Utf8NoBom = new(false);

    public static void WriteTable(TableDef table, string outputDir)
    {
        var path = Path.Combine(outputDir, table.Name + ".json");
        using var sw = new StreamWriter(path, false, Utf8NoBom);
        using var jw = new JsonTextWriter(sw)
        {
            Formatting = Formatting.Indented,
            Indentation = 2,
            IndentChar = ' ',
        };

        if (table.IsSingleton)
        {
            WriteObject(jw, table, table.SingletonValues);
        }
        else
        {
            jw.WriteStartArray();
            foreach (var row in table.Rows)
                WriteObject(jw, table, row);
            jw.WriteEndArray();
        }
        sw.Write('\n'); // 末尾换行，diff 友好
    }

    private static void WriteObject(JsonTextWriter jw, TableDef table, object[] values)
    {
        jw.WriteStartObject();
        for (var i = 0; i < table.Fields.Count; i++)
        {
            jw.WritePropertyName(table.Fields[i].Name);
            WriteValue(jw, table.Fields[i].Type, values[i]);
        }
        jw.WriteEndObject();
    }

    private static void WriteValue(JsonTextWriter jw, FieldType type, object value)
    {
        switch (type)
        {
            case FieldType.Int: jw.WriteValue((int)value); break;
            case FieldType.Long: jw.WriteValue((long)value); break;
            case FieldType.Float: jw.WriteValue((float)value); break;
            case FieldType.Bool: jw.WriteValue((bool)value); break;
            case FieldType.String: jw.WriteValue((string)value); break;
            case FieldType.IntArray:
                jw.WriteStartArray();
                foreach (var v in (int[])value) jw.WriteValue(v);
                jw.WriteEndArray();
                break;
            case FieldType.FloatArray:
                jw.WriteStartArray();
                foreach (var v in (float[])value) jw.WriteValue(v);
                jw.WriteEndArray();
                break;
            case FieldType.StringArray:
                jw.WriteStartArray();
                foreach (var v in (string[])value) jw.WriteValue(v);
                jw.WriteEndArray();
                break;
            default:
                throw new InvalidOperationException($"未实现的类型 {type}");
        }
    }
}
