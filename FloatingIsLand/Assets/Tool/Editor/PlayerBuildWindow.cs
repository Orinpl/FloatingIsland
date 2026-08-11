using System.IO;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 打包面板：把 <see cref="PlayerBuildPipeline"/> 的开关摊开，按之前先看清楚这一下会做什么。
    ///
    /// 存在的意义是"没有暗箱"：安卓打包必须顺手改几项工程设置（IL2CPP、ARM64、包名、最低版本），
    /// 与其偷偷改，不如在这里列出来——点下去会发生什么，界面上写着。
    ///
    /// 全窗口只有"浏览…"一个原生弹窗，且必须由人点出来。其余一律不弹：
    /// 本项目踩过 DisplayDialog 阻塞主线程导致 Unity 整个失去响应、程序侧点不掉的坑。
    /// </summary>
    public sealed class PlayerBuildWindow : EditorWindow
    {
        private Vector2 _scroll;

        [MenuItem("Tools/打包/打包设置…", priority = 50)]
        public static void Open()
        {
            var window = GetWindow<PlayerBuildWindow>(false, "打包", true);
            window.minSize = new Vector2(460f, 460f);
            window.Show();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawOutput();
            EditorGUILayout.Space();
            DrawOptions();
            EditorGUILayout.Space();
            DrawAndroidToolchain();
            EditorGUILayout.Space();
            DrawScenes();
            EditorGUILayout.Space();
            DrawAndroidChangeNotice();
            EditorGUILayout.Space();
            DrawActions();

            EditorGUILayout.EndScrollView();
        }

        private void DrawOutput()
        {
            EditorGUILayout.LabelField("产物目录", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string root = EditorGUILayout.TextField(BuildToolchain.OutputRoot);
                if (root != BuildToolchain.OutputRoot)
                {
                    BuildToolchain.OutputRoot = root;
                }

                if (GUILayout.Button("浏览…", GUILayout.Width(64f)))
                {
                    // 全工具唯一的原生弹窗，且只在人点了这个按钮时出现——自动化路径上碰不到
                    string picked = EditorUtility.OpenFolderPanel("选择产物目录", BuildToolchain.OutputRoot, string.Empty);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        BuildToolchain.OutputRoot = picked;
                    }
                }
                if (GUILayout.Button("默认", GUILayout.Width(48f)))
                {
                    BuildToolchain.OutputRoot = BuildToolchain.DefaultOutputRoot();
                }
            }
            EditorGUILayout.LabelField(
                $"Windows → {BuildToolchain.OutputRoot}/Windows/FloatingIsLand.exe", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"Android → {BuildToolchain.OutputRoot}/Android/FloatingIsLand-v{PlayerSettings.bundleVersion}.apk",
                EditorStyles.miniLabel);
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("本次打包", EditorStyles.boldLabel);

            BuildSettings.AndroidFormat = (AndroidPackage)EditorGUILayout.EnumPopup(
                new GUIContent("安卓形态", "APK 直接装机调试；AAB 传 Google Play"), BuildSettings.AndroidFormat);

            BuildSettings.Development = EditorGUILayout.Toggle(
                new GUIContent("开发包", "带 Profiler 连接与脚本调试；体积更大、跑得更慢，只用来定位问题"),
                BuildSettings.Development);

            BuildSettings.IncrementVersionCode = EditorGUILayout.Toggle(
                new GUIContent("versionCode 自增", "关掉的话新包装不上已装同号包的机器"),
                BuildSettings.IncrementVersionCode);

            EditorGUILayout.LabelField(
                $"当前平台：{EditorUserBuildSettings.activeBuildTarget}（跨平台打包会切走再切回，各一次全量重导入）",
                EditorStyles.miniLabel);
        }

        private void DrawAndroidToolchain()
        {
            EditorGUILayout.LabelField("安卓工具链", EditorStyles.boldLabel);
            BuildToolchain.JdkPath = PathField("JDK", BuildToolchain.JdkPath);
            BuildToolchain.SdkPath = PathField("SDK", BuildToolchain.SdkPath);
            BuildToolchain.NdkPath = PathField("NDK", BuildToolchain.NdkPath);

            if (!BuildToolchain.IsAndroidToolchainPresent)
            {
                EditorGUILayout.HelpBox(
                    "上面标 ✘ 的路径不存在，安卓包打不出来。改成本机实际路径，" +
                    "或用 Unity Hub 装带 SDK/NDK 的 Android Build Support。",
                    MessageType.Warning);
            }
        }

        private static string PathField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool exists = !string.IsNullOrEmpty(value) && Directory.Exists(value);
                EditorGUILayout.LabelField(exists ? "✔" : "✘", GUILayout.Width(16f));
                return EditorGUILayout.TextField(label, value);
            }
        }

        private static void DrawScenes()
        {
            EditorGUILayout.LabelField("打进包里的场景（Build Settings 里勾的那些）", EditorStyles.boldLabel);
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int enabled = 0;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled)
                {
                    continue;
                }
                EditorGUILayout.LabelField($"  {enabled}. {scenes[i].path}", EditorStyles.miniLabel);
                enabled++;
            }
            if (enabled == 0)
            {
                EditorGUILayout.HelpBox("一个启用的场景都没有，打出来会是黑屏。", MessageType.Error);
            }
        }

        /// <summary>
        /// 明说安卓打包会改哪几项工程设置。这些改动是"不改就出不了能装/能上架的包"，
        /// 但偷偷改工程设置是最难排查的一类问题，所以摆在按钮正上方。
        /// </summary>
        private static void DrawAndroidChangeNotice()
        {
            EditorGUILayout.LabelField("安卓打包会顺手改这些工程设置", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("  · 包名 → com.floatingisland.game", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  · 脚本后端 → IL2CPP（ARM64 的前提）", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  · ABI → ARMv7 + ARM64（Play 强制 64 位）", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("  · 最低版本 → API 24", EditorStyles.miniLabel);
            if (BuildSettings.IncrementVersionCode)
            {
                EditorGUILayout.LabelField(
                    $"  · versionCode {PlayerSettings.Android.bundleVersionCode} → {PlayerSettings.Android.bundleVersionCode + 1}",
                    EditorStyles.miniLabel);
            }
            EditorGUILayout.LabelField("横竖屏 / 图标 / 签名不在这里改——那是策划美术的决定。", EditorStyles.miniLabel);
        }

        private static void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("同时打包 Windows + Android", GUILayout.Height(34f)))
                {
                    PlayerBuildPipeline.BuildAll();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("只打 Windows"))
                {
                    PlayerBuildPipeline.BuildWindowsMenu();
                }
                if (GUILayout.Button("只打 Android"))
                {
                    if (BuildSettings.AndroidFormat == AndroidPackage.Aab)
                    {
                        PlayerBuildPipeline.BuildAndroidAabMenu();
                    }
                    else
                    {
                        PlayerBuildPipeline.BuildAndroidApkMenu();
                    }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("配置安卓工具链"))
                {
                    BuildToolchain.ApplyAndroidToolchain();
                }
                if (GUILayout.Button("打开产物目录"))
                {
                    PlayerBuildPipeline.RevealOutput();
                }
            }
        }
    }
}
