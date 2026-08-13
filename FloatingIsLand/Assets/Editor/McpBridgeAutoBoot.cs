using System;
using System.IO;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MCP 桥自动引导：域重载后若 HTTP 桥没在跑就拉起来（连本机 hub）。
///
/// 为什么需要它：包自带的 HttpBridgeReloadHandler 只「恢复」重载前正在运行的桥，
/// 编辑器冷启动或桥从未启动时没有免点击的入口。无人值守跑美术流水线需要桥常在。
///
/// 实现抄 HttpBridgeReloadHandler 的姿势：TransportManager.StartAsync + TaskScheduler.Default
/// 上的 ContinueWith——别在 delayCall 里 await 这条链，编辑器主线程上下文会死锁。
/// 过程与结果追加写 Temp/mcp_bridge_boot.txt（Editor.log 被多开实例撑爆过，不依赖它）。
/// </summary>
[InitializeOnLoad]
public static class McpBridgeAutoBoot
{
    private static readonly string Report = Path.Combine(
        Directory.GetParent(Application.dataPath).FullName, "Temp", "mcp_bridge_boot.txt");

    static McpBridgeAutoBoot()
    {
        Log($"ctor {DateTime.Now:HH:mm:ss}");
        EditorApplication.delayCall += Kick;
    }

    private static void Kick()
    {
        Log($"kick {DateTime.Now:HH:mm:ss}");
        try
        {
            Task<bool> task = MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Log($"faulted: {t.Exception?.GetBaseException()?.Message}");
                }
                else
                {
                    Log($"started={t.Result} {DateTime.Now:HH:mm:ss}");
                }
            }, TaskScheduler.Default);
        }
        catch (Exception e)
        {
            Log("EX " + e);
        }
    }

    private static void Log(string line)
    {
        try { File.AppendAllText(Report, line + Environment.NewLine); } catch { }
    }
}
