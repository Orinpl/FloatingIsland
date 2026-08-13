using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>安卓产物形态。调试装机用 APK，上架 Google Play 用 AAB。</summary>
    public enum AndroidPackage
    {
        Apk = 0,
        Aab = 1,
    }

    /// <summary>
    /// 出包：Windows x64 与 Android，可以一键连着出两个。
    ///
    /// 本工程**不用 AssetBundle**（资源走 Resources / 直接引用），所以这里没有任何 AB 环节——
    /// 参考的 MobaLite 那套 BuildScript 里一半篇幅是打 AB 与复制到 StreamingAssets，全部略去。
    ///
    /// 三条硬约束，改这个文件时别破坏：
    ///
    /// 1. **不弹对话框。** DisplayDialog 会阻塞 Unity 主线程，弹出来之后程序侧点不掉（本项目踩过），
    ///    自动化流程会整个卡死。所有输出走 Debug.Log。
    /// 2. **打完还原原来的目标平台。** 切平台会触发全量重导入（几分钟起步），
    ///    出完安卓不切回去，下次进 Play 还得再等一轮。
    /// 3. **设置改动全部先 Log 再改。** 打包顺手改 PlayerSettings 是必要的（ARM64 要 IL2CPP 等），
    ///    但不留痕地改工程设置，下次别人打开工程会莫名其妙。
    /// </summary>
    public static class PlayerBuildPipeline
    {
        /// <summary>产物名。故意不带空格与中文：adb / gradle / CI 脚本对这两样都不友好。</summary>
        private const string ProductFileName = "FloatingIsLand";

        /// <summary>安卓包名。Play 上架后不可更改，这里定死。</summary>
        private const string AndroidApplicationId = "com.floatingisland.game";

        /// <summary>
        /// 安卓最低系统版本。22（Android 5.1）太老，Google Play 早已要求 23+，
        /// 且 IL2CPP + ARM64 在更老的设备上意义不大。定 24（Android 7.0）。
        /// </summary>
        private const AndroidSdkVersions AndroidMinSdk = AndroidSdkVersions.AndroidApiLevel24;

        /// <summary>
        /// PC 端初次启动的窗口大小。取 1600×900 而不是 1920×1080：后者在 1080p 桌面上
        /// 连标题栏带任务栏根本放不下，Windows 会把窗口挤到屏幕外，第一眼就像坏了。
        /// 16:9 与 UI 参考分辨率同比，起始不会有任何缩放误差。
        /// </summary>
        private const int WindowsDefaultWidth = 1600;
        private const int WindowsDefaultHeight = 900;

        // ── 菜单 ──────────────────────────────────────────────────────────────

        [MenuItem("Tools/打包/同时打包 Windows + Android", priority = 0)]
        public static void BuildAll()
        {
            TargetSnapshot original = TargetSnapshot.Capture();
            var lines = new List<string>();

            string windowsPath;
            bool windowsOk = BuildWindows(out windowsPath);
            lines.Add(windowsOk ? $"  Windows ✔ {windowsPath}" : "  Windows ✘ 失败（见上方日志）");

            string androidPath;
            bool androidOk = BuildAndroid(BuildSettings.AndroidFormat, out androidPath);
            lines.Add(androidOk ? $"  Android ✔ {androidPath}" : "  Android ✘ 失败（见上方日志）");

            RestoreBuildTarget(original);

            string summary = "[打包] 双端出包结束：\n" + string.Join("\n", lines);
            if (windowsOk && androidOk)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }
        }

        [MenuItem("Tools/打包/只打 Windows x64", priority = 1)]
        public static void BuildWindowsMenu()
        {
            TargetSnapshot original = TargetSnapshot.Capture();
            string path;
            BuildWindows(out path);
            RestoreBuildTarget(original);
        }

        [MenuItem("Tools/打包/只打 Android APK（装机调试）", priority = 2)]
        public static void BuildAndroidApkMenu()
        {
            TargetSnapshot original = TargetSnapshot.Capture();
            string path;
            BuildAndroid(AndroidPackage.Apk, out path);
            RestoreBuildTarget(original);
        }

        [MenuItem("Tools/打包/只打 Android AAB（上架）", priority = 3)]
        public static void BuildAndroidAabMenu()
        {
            TargetSnapshot original = TargetSnapshot.Capture();
            string path;
            BuildAndroid(AndroidPackage.Aab, out path);
            RestoreBuildTarget(original);
        }

        [MenuItem("Tools/打包/打开产物目录", priority = 100)]
        public static void RevealOutput()
        {
            string root = BuildToolchain.OutputRoot;
            Directory.CreateDirectory(root);
            EditorUtility.RevealInFinder(root);
        }

        // ── Windows ───────────────────────────────────────────────────────────

        /// <summary>
        /// 出 Windows 64 位包。脚本后端用 Mono——本机只装了 Mono 变体
        /// （PlaybackEngines/windowsstandalonesupport/Variations 下没有 il2cpp），
        /// 强行选 IL2CPP 只会在打包中途报"缺少模块"。
        /// </summary>
        public static bool BuildWindows(out string outputPath)
        {
            outputPath = Path.Combine(BuildToolchain.OutputRoot, "Windows", ProductFileName + ".exe")
                .Replace('\\', '/');

            string[] scenes;
            if (!TryGetScenes(out scenes))
            {
                return false;
            }
            if (!EnsureTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64, "Windows"))
            {
                return false;
            }

            ApplyWindowsPlayerSettings();

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            SwitchTo(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildSettings.Development ? BuildOptions.Development : BuildOptions.None,
            };

            return Run("Windows x64", options);
        }

        /// <summary>
        /// PC 端的窗口形态：**有边框窗口 + 可拖边调整大小**，而不是独占全屏。
        ///
        /// 这几项是一整套，缺一项就不成立：
        /// <list type="bullet">
        /// <item>fullScreenMode = Windowed —— 默认的 FullScreenWindow 是无边框全屏，
        ///       没有窗口边缘可拖，resizableWindow 勾了也白勾。</item>
        /// <item>resizableWindow = true —— 才有可拖的边框与最大化按钮。</item>
        /// <item>defaultIsNativeResolution = false —— 开着的话初始窗口按显示器分辨率来，
        ///       在 1080p 桌面上会得到一个比屏幕还大的窗口；关掉才用下面这对默认宽高。</item>
        /// <item>allowFullscreenSwitch = true —— 保留 Alt+Enter 切全屏，玩家想全屏还是能全屏。</item>
        /// </list>
        ///
        /// 窗口能被拉成任意宽高比，UI 侧靠 Boot 场景 CanvasScaler 的 Expand 匹配模式兜住
        /// （见 BootSceneBuilder，1920×1080 参考分辨率按较小边缩放，保证不裁切）。
        /// </summary>
        private static void ApplyWindowsPlayerSettings()
        {
            if (PlayerSettings.fullScreenMode != FullScreenMode.Windowed)
            {
                Debug.Log($"[打包] PC 窗口模式：{PlayerSettings.fullScreenMode} → Windowed（带边框窗口）");
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            }

            if (!PlayerSettings.resizableWindow)
            {
                Debug.Log("[打包] PC 窗口可调整大小：关 → 开（窗口边缘可拖拽）");
                PlayerSettings.resizableWindow = true;
            }

            if (PlayerSettings.defaultIsNativeResolution)
            {
                Debug.Log("[打包] PC 默认用显示器原生分辨率：开 → 关（改用下面的默认窗口尺寸）");
                PlayerSettings.defaultIsNativeResolution = false;
            }

            if (PlayerSettings.defaultScreenWidth != WindowsDefaultWidth ||
                PlayerSettings.defaultScreenHeight != WindowsDefaultHeight)
            {
                Debug.Log(
                    $"[打包] PC 初始窗口尺寸：{PlayerSettings.defaultScreenWidth}×{PlayerSettings.defaultScreenHeight} → " +
                    $"{WindowsDefaultWidth}×{WindowsDefaultHeight}");
                PlayerSettings.defaultScreenWidth = WindowsDefaultWidth;
                PlayerSettings.defaultScreenHeight = WindowsDefaultHeight;
            }

            if (!PlayerSettings.allowFullscreenSwitch)
            {
                Debug.Log("[打包] PC 允许 Alt+Enter 切全屏：关 → 开");
                PlayerSettings.allowFullscreenSwitch = true;
            }

            AssetDatabase.SaveAssets();
        }

        // ── Android ───────────────────────────────────────────────────────────

        public static bool BuildAndroid(AndroidPackage package, out string outputPath)
        {
            bool aab = package == AndroidPackage.Aab;
            string extension = aab ? ".aab" : ".apk";
            outputPath = Path.Combine(
                    BuildToolchain.OutputRoot, "Android",
                    $"{ProductFileName}-v{PlayerSettings.bundleVersion}{extension}")
                .Replace('\\', '/');

            string[] scenes;
            if (!TryGetScenes(out scenes))
            {
                return false;
            }
            if (!EnsureTargetSupported(BuildTargetGroup.Android, BuildTarget.Android, "Android"))
            {
                return false;
            }
            if (!BuildToolchain.IsAndroidToolchainPresent)
            {
                Debug.LogError(
                    "[打包] 安卓工具链路径不完整（JDK / SDK / NDK），先去 Tools/打包/打包设置… 配好再打。");
                return false;
            }
            BuildToolchain.Apply(verbose: false);

            ApplyAndroidPlayerSettings(aab);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            SwitchTo(BuildTargetGroup.Android, BuildTarget.Android);
            EditorUserBuildSettings.buildAppBundle = aab;

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildSettings.Development ? BuildOptions.Development : BuildOptions.None,
            };

            return Run(aab ? "Android AAB" : "Android APK", options);
        }

        /// <summary>
        /// 把安卓侧非改不可的工程设置摆正，每一处都先说明再改。
        /// 「非改不可」的判据是：不改就打不出能装/能玩/能上架的包。纯审美类的（图标、启动图）
        /// 不在这里动——那是策划美术的决定，藏在打包流程里改只会让人找不着。
        ///
        /// **横屏是例外，从审美挪进了这里**：本作已定为横屏游戏（UI 参考分辨率 1920×1080、
        /// PC 端窗口 16:9、局内相机手势按横向布局调的手感），竖起来玩不是"另一种审美"而是坏的。
        /// 它又恰好是最容易在换机器 / 重导工程后被悄悄改回默认的一项，所以钉在出包流程里每次强制摆正。
        /// </summary>
        private static void ApplyAndroidPlayerSettings(bool aab)
        {
            ApplyLandscapeOrientation();

            string identifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            if (!string.Equals(identifier, AndroidApplicationId, StringComparison.Ordinal))
            {
                Debug.Log($"[打包] 安卓包名：{identifier} → {AndroidApplicationId}");
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidApplicationId);
            }

            // ARM64 只有 IL2CPP 支持；而 Google Play 从 2019 年起强制 64 位。
            // 两条合起来的结论是：安卓包没得选，必须 IL2CPP。
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
            if (backend != ScriptingImplementation.IL2CPP)
            {
                Debug.Log($"[打包] 安卓脚本后端：{backend} → IL2CPP（ARM64 的前提，首次编译会慢很多）");
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            }

            const AndroidArchitecture wanted = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            if (PlayerSettings.Android.targetArchitectures != wanted)
            {
                Debug.Log($"[打包] 安卓 ABI：{PlayerSettings.Android.targetArchitectures} → ARMv7 + ARM64");
                PlayerSettings.Android.targetArchitectures = wanted;
            }

            if (PlayerSettings.Android.minSdkVersion < AndroidMinSdk)
            {
                Debug.Log($"[打包] 安卓最低版本：API {(int)PlayerSettings.Android.minSdkVersion} → API {(int)AndroidMinSdk}");
                PlayerSettings.Android.minSdkVersion = AndroidMinSdk;
            }

            if (BuildSettings.IncrementVersionCode)
            {
                PlayerSettings.Android.bundleVersionCode++;
                Debug.Log($"[打包] versionCode 自增 → {PlayerSettings.Android.bundleVersionCode}");
            }

            if (aab && !PlayerSettings.Android.useCustomKeystore)
            {
                // 不拦着打，只是说清楚：debug 签名的包能装能测，就是传不上 Play
                Debug.LogWarning(
                    "[打包] 当前没有配置正式签名（Player Settings → Publishing Settings），" +
                    "这次出的 AAB 用的是调试签名，能装机但无法上架 Google Play。");
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 锁横屏（左右横都允许，竖屏两个方向都禁掉）。
        ///
        /// 用 AutoRotation + 只放行两个横向，而不是直接钉死 LandscapeLeft：钉死单一方向的话，
        /// 玩家把手机转 180° 画面就上下颠倒，而这两个方向对本作没有任何区别。
        /// 赋值顺序也不能反——必须先放行横屏再禁竖屏，中间一旦出现"四个方向全 false"
        /// 的瞬间，Unity 会当成非法配置。
        /// </summary>
        private static void ApplyLandscapeOrientation()
        {
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation)
            {
                Debug.Log($"[打包] 屏幕方向：{PlayerSettings.defaultInterfaceOrientation} → AutoRotation（仅横屏）");
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            }

            if (!PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                Debug.Log("[打包] 放行横屏左 / 横屏右");
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            }

            if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
            {
                Debug.Log("[打包] 禁用竖屏（正 / 倒）：本作按 16:9 横屏布局，竖起来 UI 会挤成一团");
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            }
        }

        // ── 公共环节 ──────────────────────────────────────────────────────────

        private static bool Run(string label, BuildPlayerOptions options)
        {
            Debug.Log($"[打包] 开始出 {label} → {options.locationPathName}");

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                Debug.LogError($"[打包] {label} 抛异常中止：{e.Message}");
                return false;
            }

            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError(
                    $"[打包] {label} 失败（{summary.result}）：错误 {summary.totalErrors} 条，用时 {summary.totalTime}。\n" +
                    DescribeFailures(report));
                return false;
            }

            double megabytes = summary.totalSize / 1024.0 / 1024.0;
            Debug.Log($"[打包] {label} 完成：{options.locationPathName}（{megabytes:F1} MB，用时 {summary.totalTime}）");
            return true;
        }

        /// <summary>
        /// 把 BuildReport 里的错误行摘出来，直接贴进这条 LogError。
        /// 不这么做的话报错只会说"原因在上面的日志里"，而真正的原因（比如
        /// "script class layout is incompatible between the editor and the player"）
        /// 埋在几 GB 的 Editor.log 中间，每次都得去刨。
        /// </summary>
        private static string DescribeFailures(BuildReport report)
        {
            var lines = new List<string>();
            BuildStep[] steps = report.steps;
            for (int i = 0; i < steps.Length; i++)
            {
                BuildStepMessage[] messages = steps[i].messages;
                for (int j = 0; j < messages.Length; j++)
                {
                    LogType type = messages[j].type;
                    if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
                    {
                        continue;
                    }
                    lines.Add($"  [{steps[i].name}] {messages[j].content.Trim()}");
                    if (lines.Count >= 20)
                    {
                        lines.Add("  …（错误过多，其余见 Editor.log）");
                        return string.Join("\n", lines);
                    }
                }
            }

            if (lines.Count == 0)
            {
                return "  BuildReport 里没有错误条目，具体原因看上面的编译 / gradle 日志。";
            }
            return string.Join("\n", lines);
        }

        /// <summary>Build Settings 里勾上的场景。顺序就是启动顺序，第 0 个是启动场景。</summary>
        private static bool TryGetScenes(out string[] scenes)
        {
            var list = new List<string>();
            EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].enabled)
                {
                    list.Add(all[i].path);
                }
            }

            scenes = list.ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[打包] Build Settings 里一个启用的场景都没有，出来的包会是黑屏。先把 Boot 场景加进去。");
                return false;
            }
            return true;
        }

        private static bool EnsureTargetSupported(BuildTargetGroup group, BuildTarget target, string label)
        {
            if (BuildPipeline.IsBuildTargetSupported(group, target))
            {
                return true;
            }
            Debug.LogError(
                $"[打包] 当前 Unity 没装 {label} 平台模块，打不了。" +
                "用 Unity Hub 给这个版本加装对应的 Build Support 再试。");
            return false;
        }

        private static void SwitchTo(BuildTargetGroup group, BuildTarget target)
        {
            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                return;
            }
            Debug.Log($"[打包] 切换目标平台 → {target}（会触发一次全量重导入，慢是正常的）");
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
        }

        /// <summary>
        /// 打包前的平台。连组一起记：还原时要用 SwitchActiveBuildTarget(组, 平台)，
        /// 而从 BuildTarget 反查组的那个 API 在不同 Unity 版本里可见性不一样，不如自己存着。
        /// </summary>
        private readonly struct TargetSnapshot
        {
            public readonly BuildTargetGroup Group;
            public readonly BuildTarget Target;

            private TargetSnapshot(BuildTargetGroup group, BuildTarget target)
            {
                Group = group;
                Target = target;
            }

            public static TargetSnapshot Capture()
            {
                return new TargetSnapshot(
                    EditorUserBuildSettings.selectedBuildTargetGroup,
                    EditorUserBuildSettings.activeBuildTarget);
            }
        }

        /// <summary>
        /// 切回打包前的平台。不还原的话，出完安卓包后工程停在 Android 平台上，
        /// 下次进 Play 或改资源又要等一轮重导入——这个坑一天能踩三次。
        /// </summary>
        private static void RestoreBuildTarget(TargetSnapshot original)
        {
            if (EditorUserBuildSettings.activeBuildTarget == original.Target)
            {
                return;
            }
            Debug.Log($"[打包] 还原目标平台 → {original.Target}");
            EditorUserBuildSettings.SwitchActiveBuildTarget(original.Group, original.Target);
        }

        // ── 命令行入口（CI / 不开编辑器出包） ─────────────────────────────────

        /// <summary>
        /// 命令行出双端包：
        /// <c>Unity.exe -quit -batchmode -projectPath &lt;工程&gt; -executeMethod FloatingIsLand.EditorTools.PlayerBuildPipeline.BuildAllCommandLine</c>
        /// 可选参数：<c>-outputRoot &lt;目录&gt;</c>、<c>-aab</c>、<c>-development</c>。
        /// 失败时用非 0 退出码结束，否则 CI 会把失败的构建当成成功。
        /// </summary>
        public static void BuildAllCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "-outputRoot", StringComparison.Ordinal) && i + 1 < args.Length)
                {
                    BuildToolchain.OutputRoot = args[i + 1];
                }
                else if (string.Equals(args[i], "-aab", StringComparison.Ordinal))
                {
                    BuildSettings.AndroidFormat = AndroidPackage.Aab;
                }
                else if (string.Equals(args[i], "-development", StringComparison.Ordinal))
                {
                    BuildSettings.Development = true;
                }
            }

            TargetSnapshot original = TargetSnapshot.Capture();
            string ignored;
            bool ok = BuildWindows(out ignored);
            ok &= BuildAndroid(BuildSettings.AndroidFormat, out ignored);
            RestoreBuildTarget(original);

            if (!ok)
            {
                EditorApplication.Exit(1);
            }
        }
    }

    /// <summary>
    /// 打包选项。同样存 EditorPrefs：这些是"这台机器这次想怎么打"，不是工程属性。
    /// </summary>
    public static class BuildSettings
    {
        private const string PrefDevelopment = "FloatingIsLand.Build.Development";
        private const string PrefPackage = "FloatingIsLand.Build.AndroidPackage";
        private const string PrefIncrement = "FloatingIsLand.Build.IncrementVersionCode";

        /// <summary>开发包：带 Profiler 连接与脚本调试，体积更大、跑得更慢，只用来定位问题。</summary>
        public static bool Development
        {
            get { return EditorPrefs.GetBool(PrefDevelopment, false); }
            set { EditorPrefs.SetBool(PrefDevelopment, value); }
        }

        /// <summary>「同时打包」时安卓出哪种形态。</summary>
        public static AndroidPackage AndroidFormat
        {
            get { return (AndroidPackage)EditorPrefs.GetInt(PrefPackage, (int)AndroidPackage.Apk); }
            set { EditorPrefs.SetInt(PrefPackage, (int)value); }
        }

        /// <summary>每次安卓出包 versionCode +1。同一个 versionCode 的包装不上同一台机器，调试时很容易踩。</summary>
        public static bool IncrementVersionCode
        {
            get { return EditorPrefs.GetBool(PrefIncrement, true); }
            set { EditorPrefs.SetBool(PrefIncrement, value); }
        }
    }
}
