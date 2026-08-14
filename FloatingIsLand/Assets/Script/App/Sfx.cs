using System.Collections.Generic;
using UnityEngine;

namespace FloatingIsLand.App
{
    /// <summary>
    /// 全局一次性音效：懒创建一个常驻的 2D AudioSource，剪辑按名字从 Resources/Audio 加载并缓存。
    /// 缺资源只警告一次不抛错——音效缺失不该让游戏起不来（与 <see cref="UI.UISkin"/> 缺皮肤图同策略）。
    /// 全局唯一 AudioListener 在 Boot 场景的 MenuCamera 上，这里播的都是无空间化的 UI/反馈音。
    /// </summary>
    public static class Sfx
    {
        private const string ResourceDir = "Audio/";

        /// <summary>UI 按钮点击音（由 UI.UIButtonSound 统一触发）。</summary>
        public const string UiClick = "ui_click";
        /// <summary>建筑确认落地音（TryPlaceSelected 成功那一刻）。</summary>
        public const string BuildPlace = "build_place";

        // 缓存里存 null 表示"已确认缺失"，避免每次点击都重复 Load + 刷警告
        private static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private static AudioSource _source;

        public static void Play(string key)
        {
            AudioClip clip;
            if (!_clips.TryGetValue(key, out clip))
            {
                clip = Resources.Load<AudioClip>(ResourceDir + key);
                if (clip == null)
                {
                    Debug.LogWarning($"[音效] 缺少 Resources/{ResourceDir}{key}，该音效跳过播放。");
                }
                _clips.Add(key, clip);
            }

            if (clip == null)
            {
                return;
            }
            EnsureSource().PlayOneShot(clip);
        }

        private static AudioSource EnsureSource()
        {
            if (_source == null)
            {
                var go = new GameObject("SfxPlayer");
                Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f; // 纯 2D，不随相机位置衰减
            }
            return _source;
        }
    }
}
