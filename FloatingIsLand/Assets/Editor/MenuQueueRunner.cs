using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 文件队列驱动的菜单执行器：外部把菜单路径逐行写进 Temp/menu_queue.txt，
/// 编辑器空闲时按行顺序执行，结果写 Temp/menu_queue_result.txt。
///
/// 为什么不用 MCP 的 execute_menu_item：提取材质 / 生成 Prefab 这类菜单会把主线程
/// 阻塞几分钟，桥的 WebSocket 心跳超时被 hub 判掉线，调用方只能拿到
/// "disconnected while awaiting command_result"。文件队列不依赖任何连接的生命周期，
/// 长任务跑多久都无所谓，完成与否看结果文件。
///
/// 约定：
/// - 队列文件读到后先删再执行（防重复触发）；逐行同步执行，天然串行
/// - 结果文件每行：ok|fail\t菜单路径\t耗时ms（全部执行完后追加一行 === done ===）
/// - 轮询靠 EditorApplication.update 节流到 ~2s 一次，不碰 AssetDatabase
/// </summary>
[InitializeOnLoad]
public static class MenuQueueRunner
{
    private static readonly string QueueFile;
    private static readonly string ResultFile;
    private static double _nextPollAt;

    static MenuQueueRunner()
    {
        string temp = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Temp");
        QueueFile = Path.Combine(temp, "menu_queue.txt");
        ResultFile = Path.Combine(temp, "menu_queue_result.txt");
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < _nextPollAt)
        {
            return;
        }
        _nextPollAt = EditorApplication.timeSinceStartup + 2.0;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            return;
        }
        if (!File.Exists(QueueFile))
        {
            return;
        }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(QueueFile);
            File.Delete(QueueFile);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[MenuQueue] 读队列失败：{e.Message}");
            return;
        }

        try { File.Delete(ResultFile); } catch { }

        foreach (string raw in lines)
        {
            string menu = raw.Trim();
            if (menu.Length == 0 || menu.StartsWith("#"))
            {
                continue;
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok;
            try
            {
                ok = EditorApplication.ExecuteMenuItem(menu);
            }
            catch (Exception e)
            {
                ok = false;
                Debug.LogError($"[MenuQueue] {menu} 抛异常：{e}");
            }
            sw.Stop();
            Append($"{(ok ? "ok" : "fail")}\t{menu}\t{sw.ElapsedMilliseconds}ms");
            Debug.Log($"[MenuQueue] {(ok ? "ok" : "fail")} {menu}（{sw.ElapsedMilliseconds}ms）");
        }
        Append("=== done ===");
    }

    private static void Append(string line)
    {
        try { File.AppendAllText(ResultFile, line + Environment.NewLine); } catch { }
    }
}
