using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 循环 BGM：随 <see cref="GameFlow.ChangeState"/> 的状态切换曲目（映射见 GameFlow.ApplyBgm）。
    /// 双 AudioSource 交叉淡入淡出，同曲目重复请求是空操作（下一关 Loading → Gameplay 不会重头播）。
    /// 缺资源只警告一次不抛错，与 <see cref="Sfx"/> 同策略；音效走 Sfx 的独立 AudioSource，互不打断。
    /// </summary>
    public static class Bgm
    {
        private const string ResourceDir = "Audio/";

        /// <summary>主界面曲（MainMenu / Leaderboard，Loading 期间延续）。</summary>
        public const string MainMenu = "bgm_main_menu";
        /// <summary>局内曲（Gameplay / Settlement）。</summary>
        public const string Gameplay = "bgm_gameplay";

        private static BgmPlayer _player;

        public static void Play(string key)
        {
            if (_player == null)
            {
                var go = new GameObject("BgmPlayer");
                Object.DontDestroyOnLoad(go);
                _player = go.AddComponent<BgmPlayer>();
            }
            _player.Play(key);
        }
    }

    /// <summary>
    /// <see cref="Bgm"/> 的执行体：不要手动挂到场景里，由 Bgm.Play 懒创建。
    /// 需要 MonoBehaviour 只为跑淡化协程；切歌进行中再切会从当前音量接着淡，不会跳变。
    /// </summary>
    internal sealed class BgmPlayer : MonoBehaviour
    {
        private const float CrossfadeSeconds = 1f;
        private const float TargetVolume = 1f;

        // 缓存里存 null 表示"已确认缺失"，避免重复 Load + 刷警告
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource _active;
        private AudioSource _standby;
        private string _activeKey;
        private Coroutine _fade;

        private void Awake()
        {
            _active = CreateSource();
            _standby = CreateSource();
        }

        private AudioSource CreateSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f; // 纯 2D，不随相机位置衰减
            source.volume = 0f;
            return source;
        }

        public void Play(string key)
        {
            if (key == _activeKey)
            {
                return;
            }

            AudioClip clip;
            if (!_clips.TryGetValue(key, out clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + key);
                if (clip == null)
                {
                    Debug.LogWarning($"[音效] 缺少 Resources/Audio/{key}，BGM 保持当前曲目。");
                }
                _clips.Add(key, clip);
            }
            if (clip == null)
            {
                return;
            }

            // 换曲：standby 从 0 淡入接管，原 active 淡出后停掉
            AudioSource incoming = _standby;
            _standby = _active;
            _active = incoming;
            _activeKey = key;

            incoming.clip = clip;
            incoming.Play();

            if (_fade != null)
            {
                StopCoroutine(_fade);
            }
            _fade = StartCoroutine(Crossfade(incoming, _standby));
        }

        private IEnumerator Crossfade(AudioSource fadeIn, AudioSource fadeOut)
        {
            // 从两个源的当前音量出发，切歌中途再切也不会音量跳变
            float inStart = fadeIn.volume;
            float outStart = fadeOut.volume;
            float elapsed = 0f;
            while (elapsed < CrossfadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / CrossfadeSeconds);
                fadeIn.volume = Mathf.Lerp(inStart, TargetVolume, t);
                fadeOut.volume = Mathf.Lerp(outStart, 0f, t);
                yield return null;
            }
            fadeOut.Stop();
            fadeOut.clip = null; // Streaming 曲目停掉就释放，别占着解码缓冲
            _fade = null;
        }
    }
}
