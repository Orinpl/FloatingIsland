using System.IO;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 打包工具链的机器相关配置：安卓 JDK / SDK / NDK 在哪，产物往哪放。
    ///
    /// 全部存 EditorPrefs（按机器走，不进版本库）——同一个仓库在别人机器上路径不一样，
    /// 写进 ProjectSettings 只会让两个人来回打架。默认值指向本机现有的一套
    /// （D:\Unity\{OpenJDK,SDK,NDK}，与 MobaLite 同一套），换机器在打包窗口里改。
    ///
    /// 这里**不弹任何对话框**。本项目踩过：EditorUtility.DisplayDialog 会阻塞主线程，
    /// 一旦在自动化流程里弹出来，Unity 整个失去响应且程序侧点不掉，只能人工点。
    /// 所有结果一律 Debug.Log / LogWarning / LogError。
    /// </summary>
    public static class BuildToolchain
    {
        // Unity 存安卓工具链路径用的 EditorPrefs key（2022.3）。
        // 每条都配一个 *UseEmbedded 开关：Unity 默认优先用自带的那套，
        // 而本机的 Android 模块装的是"不含 SDK/NDK/JDK"的版本，不关掉开关，
        // 下面写的路径会被无视，打包时报 "Android SDK not found"。
        private const string JdkPathKey = "JdkPath";
        private const string JdkEmbeddedKey = "JdkUseEmbedded";
        private const string SdkPathKey = "AndroidSdkRoot";
        private const string SdkEmbeddedKey = "SdkUseEmbedded";

        /// <summary>Unity 2022.3 认的是 r23 专用 key；老版本读 AndroidNdkRoot，两个都写省得判版本。</summary>
        private const string NdkPathKey = "AndroidNdkRootR23";
        private const string NdkLegacyPathKey = "AndroidNdkRoot";
        private const string NdkEmbeddedKey = "NdkUseEmbedded";

        private const string PrefJdk = "FloatingIsLand.Build.Jdk";
        private const string PrefSdk = "FloatingIsLand.Build.Sdk";
        private const string PrefNdk = "FloatingIsLand.Build.Ndk";
        private const string PrefOutput = "FloatingIsLand.Build.OutputRoot";

        private const string DefaultJdk = @"D:\Unity\OpenJDK";
        private const string DefaultSdk = @"D:\Unity\SDK";
        private const string DefaultNdk = @"D:\Unity\NDK";

        public static string JdkPath
        {
            get { return EditorPrefs.GetString(PrefJdk, DefaultJdk); }
            set { EditorPrefs.SetString(PrefJdk, value); }
        }

        public static string SdkPath
        {
            get { return EditorPrefs.GetString(PrefSdk, DefaultSdk); }
            set { EditorPrefs.SetString(PrefSdk, value); }
        }

        public static string NdkPath
        {
            get { return EditorPrefs.GetString(PrefNdk, DefaultNdk); }
            set { EditorPrefs.SetString(PrefNdk, value); }
        }

        /// <summary>产物根目录。默认放在工程目录旁边的 Build/（在 Assets 外，不会被 Unity 导入）。</summary>
        public static string OutputRoot
        {
            get { return EditorPrefs.GetString(PrefOutput, DefaultOutputRoot()); }
            set { EditorPrefs.SetString(PrefOutput, value); }
        }

        public static string DefaultOutputRoot()
        {
            // Application.dataPath = <工程>/Assets，往上两级到工程的父目录
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Build").Replace('\\', '/');
        }

        /// <summary>三条路径是否都存在。缺一条安卓就打不出来，打包前先问这个。</summary>
        public static bool IsAndroidToolchainPresent
        {
            get
            {
                return Directory.Exists(JdkPath)
                       && Directory.Exists(SdkPath)
                       && Directory.Exists(NdkPath);
            }
        }

        // 菜单名里不能出现 '/'：Unity 拿它当子菜单分隔符，写「JDK / SDK / NDK」会被拆成三层空菜单
        [MenuItem("Tools/打包/配置安卓工具链（JDK · SDK · NDK）", priority = 200)]
        public static void ApplyAndroidToolchain()
        {
            Apply(verbose: true);
        }

        /// <summary>
        /// 把路径写进 EditorPrefs 并关掉"用内置"。安卓打包前会自动调一次，
        /// 所以正常情况下不需要手动点菜单。
        /// </summary>
        public static void Apply(bool verbose)
        {
            bool ok = true;
            ok &= WritePath("JDK", JdkPath, JdkPathKey, JdkEmbeddedKey, null);
            ok &= WritePath("SDK", SdkPath, SdkPathKey, SdkEmbeddedKey, null);
            ok &= WritePath("NDK", NdkPath, NdkPathKey, NdkEmbeddedKey, NdkLegacyPathKey);

            if (!ok)
            {
                Debug.LogError(
                    "[打包] 安卓工具链路径不完整，安卓包会打不出来。" +
                    "去 Tools/打包/打包设置… 改成本机实际路径，或装带 SDK/NDK 的 Android Build Support。");
                return;
            }

            // Gradle 留空 = 用 Unity 内置的那份，这个是随模块一起装的，不缺
            if (verbose)
            {
                Debug.Log($"[打包] 安卓工具链已配置：\n  JDK = {JdkPath}\n  SDK = {SdkPath}\n  NDK = {NdkPath}\n  Gradle = Unity 内置");
            }
        }

        private static bool WritePath(string label, string path, string key, string embeddedKey, string legacyKey)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                Debug.LogWarning($"[打包] 安卓 {label} 路径不存在：{(string.IsNullOrEmpty(path) ? "(空)" : path)}");
                return false;
            }

            EditorPrefs.SetString(key, path);
            EditorPrefs.SetBool(embeddedKey, false);
            if (!string.IsNullOrEmpty(legacyKey))
            {
                EditorPrefs.SetString(legacyKey, path);
            }
            return true;
        }
    }
}
