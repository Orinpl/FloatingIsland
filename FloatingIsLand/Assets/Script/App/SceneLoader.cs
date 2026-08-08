using System.Collections;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.App
{
    /// <summary>
    /// Main 场景的加载/卸载。场景组织为"Boot 常驻 + Main 附加"（见 Docs/BOOT_FRAMEWORK.md）：
    /// Boot 场景永不卸载（承载 AppRoot / UIRoot），每局开始时整体重载 Main，天然避免跨局残留。
    /// </summary>
    public static class SceneLoader
    {
        public const string MainSceneName = "Main";

        public static bool IsMainLoaded
        {
            get { return SceneManager.GetSceneByName(MainSceneName).isLoaded; }
        }

        /// <summary>（重新）加载 Main：已加载则先卸载，保证每局全新场景；完成后把 Main 设为 ActiveScene（光照/新物体归属）。</summary>
        public static IEnumerator ReloadMainAsync()
        {
            yield return UnloadMainAsync();
            yield return SceneManager.LoadSceneAsync(MainSceneName, LoadSceneMode.Additive);
            SceneManager.SetActiveScene(SceneManager.GetSceneByName(MainSceneName));
        }

        /// <summary>卸载 Main（未加载则直接返回）。</summary>
        public static IEnumerator UnloadMainAsync()
        {
            Scene main = SceneManager.GetSceneByName(MainSceneName);
            if (main.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(main);
            }
        }
    }
}
