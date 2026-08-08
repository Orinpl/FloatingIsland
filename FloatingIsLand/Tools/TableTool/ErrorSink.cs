using System;
using System.Collections.Generic;

namespace TableTool;

/// <summary>
/// 校验错误收集器。错误格式：文件!Sheet!单元格: 消息（尽量一次跑完收集全部错误再退出）。
/// </summary>
public sealed class ErrorSink
{
    private readonly List<string> _errors = new();

    public bool HasErrors => _errors.Count > 0;
    public int Count => _errors.Count;

    public void Add(string file, string sheet, string cell, string message)
    {
        var loc = string.IsNullOrEmpty(cell) ? $"{file}!{sheet}" : $"{file}!{sheet}!{cell}";
        _errors.Add($"{loc}: {message}");
    }

    /// <summary>无单元格定位的全局错误（如跨工作簿表名冲突、种子文件错误）。</summary>
    public void AddGlobal(string message) => _errors.Add(message);

    public void Print()
    {
        foreach (var e in _errors)
            Console.Error.WriteLine("[错误] " + e);
        Console.Error.WriteLine($"共 {_errors.Count} 个错误。");
    }
}
