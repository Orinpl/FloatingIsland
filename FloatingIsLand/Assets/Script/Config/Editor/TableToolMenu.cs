using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.Config.EditorTools
{
    /// <summary>
    /// Unity 菜单入口：不离开编辑器就能跑转表工具（Tools/TableTool）。
    /// 等价命令行：dotnet run --project Tools/TableTool -- convert|check
    /// 需要机器上装了 .NET SDK（命令行 dotnet --version 有输出）。
    /// </summary>
    public static class TableToolMenu
    {
        private const string ToolProject = "Tools/TableTool";

        [MenuItem("Tools/配表/转表（Excel → JSON + Tables.g.cs）", false, 1)]
        public static void Convert()
        {
            if (RunTool("convert"))
            {
                AssetDatabase.Refresh();
            }
        }

        [MenuItem("Tools/配表/只校验不落盘", false, 2)]
        public static void Check()
        {
            RunTool("check");
        }

        [MenuItem("Tools/配表/打开 Tables 目录（Excel 正本）", false, 20)]
        public static void OpenTablesFolder()
        {
            EditorUtility.RevealInFinder(Path.Combine(ProjectRoot, "Tables") + Path.DirectorySeparatorChar);
        }

        /// <summary>Unity 工程根（Assets 的上一级），也是转表工具认的 root。</summary>
        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        /// <summary>同步跑一次转表工具；成功返回 true。输出原样转到 Console。</summary>
        private static bool RunTool(string command)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"" + ToolProject + "\" -- " + command,
                WorkingDirectory = ProjectRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            string stdout;
            string stderr;
            int exitCode;
            try
            {
                EditorUtility.DisplayProgressBar("配表", "正在执行 dotnet run -- " + command + "（首次会还原依赖，稍慢）…", 0.5f);
                using (var process = System.Diagnostics.Process.Start(psi))
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    exitCode = process.ExitCode;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[配表] 启动 dotnet 失败：" + e.Message + "\n（机器上装了 .NET SDK 吗？命令行跑 dotnet --version 确认）");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            string log = (stdout + stderr).TrimEnd();
            if (exitCode == 0)
            {
                Debug.Log("[配表] " + command + " 成功\n" + log);
                return true;
            }

            Debug.LogError("[配表] " + command + " 失败（退出码 " + exitCode + "）\n" + log);
            EditorUtility.DisplayDialog("配表失败",
                "转表未通过，详见 Console（错误坐标格式：文件!Sheet!单元格）。\n改完 Excel 再跑一次。", "知道了");
            return false;
        }
    }
}
