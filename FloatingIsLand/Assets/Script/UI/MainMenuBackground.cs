using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 主界面的开场封面背景：静帧封面打底，视频准备好后无缝换成循环播放的空岛动画。
    ///
    /// 为什么是「运行时现建」而不是改 BootSceneBuilder：
    /// 和 <see cref="UISkin"/> 同一个理由——Boot 场景是编辑器菜单生成的，改生成器就得重跑
    /// 那条带模态弹窗的流程（见 Docs/BOOT_FRAMEWORK.md）。这里由 <see cref="MainMenuPanel"/>
    /// 在 Awake 里挂上来，已生成的场景一行都不用动。
    ///
    /// 先贴封面再等视频，是为了避开 <see cref="VideoPlayer"/> 的准备期：
    /// Prepare 要读文件、解首帧，这期间 RenderTexture 是黑的，直接挂上去开场会黑一下。
    /// 封面用的就是视频的首帧原图，所以切换的那一刻画面是连续的。
    /// </summary>
    public sealed class MainMenuBackground : MonoBehaviour
    {
        /// <summary>封面静帧（Resources 相对路径，无扩展名）。缺图时背景保持面板原来的纯色底。</summary>
        private const string CoverPath = "UI/main_menu_cover";

        /// <summary>循环背景视频。缺资源时只显示封面静帧，不报错。</summary>
        private const string ClipPath = "Video/main_menu_island";

        private RawImage _image;
        private VideoPlayer _player;
        private RenderTexture _target;
        private Texture2D _cover;

        /// <summary>挂到面板上（幂等）。重复调用只会拿到已有的那一个。</summary>
        public static MainMenuBackground AttachTo(GameObject panel)
        {
            if (panel == null)
            {
                return null;
            }

            MainMenuBackground existing = panel.GetComponent<MainMenuBackground>();
            return existing != null ? existing : panel.AddComponent<MainMenuBackground>();
        }

        private void Awake()
        {
            BuildImage();
            ShowCover();
            StartVideo();
        }

        /// <summary>
        /// 建一个铺满面板的 RawImage，并且必须是第一个子节点——标题和按钮是面板的其它子节点，
        /// UGUI 按层级顺序绘制，排在最前才会被它们盖住而不是盖住它们。
        /// </summary>
        private void BuildImage()
        {
            var go = new GameObject("Background", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            go.transform.SetSiblingIndex(0);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            _image = go.AddComponent<RawImage>();
            _image.raycastTarget = false; // 纯背景，不该吃掉按钮的点击

            // EnvelopeParent = 等比放大到铺满，宁可裁掉画面边缘也不留黑边、不拉变形。
            // 画面主体（岛）在中间，裁边裁掉的是天空和云，安全。
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = 16f / 9f;
        }

        private void ShowCover()
        {
            _cover = Resources.Load<Texture2D>(CoverPath);
            if (_cover == null)
            {
                Debug.LogWarning($"[UI] 缺少主界面封面 Resources/{CoverPath}.png，背景保持纯色。");
                return;
            }

            _image.texture = _cover;
            SetAspect(_cover.width, _cover.height);
        }

        private void StartVideo()
        {
            VideoClip clip = Resources.Load<VideoClip>(ClipPath);
            if (clip == null)
            {
                // 视频是锦上添花：没有就停在封面静帧，不该让主界面起不来。
                Debug.LogWarning($"[UI] 缺少主界面背景视频 Resources/{ClipPath}.mp4，只显示封面静帧。");
                return;
            }

            int width = clip.width > 0 ? (int)clip.width : 1920;
            int height = clip.height > 0 ? (int)clip.height : 1080;

            _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _target.name = "MainMenuBackgroundRT";

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.clip = clip;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.targetTexture = _target;
            _player.isLooping = true;
            _player.waitForFirstFrame = true;
            _player.audioOutputMode = VideoAudioOutputMode.None; // 无声素材，也避免抢 AudioListener
            _player.prepareCompleted += OnPrepared;
            _player.errorReceived += OnVideoError;
            _player.Prepare();
        }

        private void OnPrepared(VideoPlayer player)
        {
            // 首帧已经解出来了，这时候换纹理才不会闪黑。
            _image.texture = _target;
            SetAspect((int)player.width, (int)player.height);
            player.Play();
        }

        private void OnVideoError(VideoPlayer player, string message)
        {
            // 解码失败（平台不支持该编码等）就退回封面静帧，主界面照常可用。
            Debug.LogWarning($"[UI] 主界面背景视频播放失败，回退为封面静帧：{message}");
            if (_cover != null)
            {
                _image.texture = _cover;
                SetAspect(_cover.width, _cover.height);
            }
        }

        private void SetAspect(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            var fitter = _image.GetComponent<AspectRatioFitter>();
            if (fitter != null)
            {
                fitter.aspectRatio = (float)width / height;
            }
        }

        // 面板被 UIManager 隐藏时（进游戏/看排行榜）没必要继续解码，省一份 CPU。
        private void OnDisable()
        {
            if (_player != null && _player.isPlaying)
            {
                _player.Pause();
            }
        }

        private void OnEnable()
        {
            if (_player != null && _player.isPrepared && !_player.isPlaying)
            {
                _player.Play();
            }
        }

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.prepareCompleted -= OnPrepared;
                _player.errorReceived -= OnVideoError;
            }

            if (_target != null)
            {
                _target.Release();
                Destroy(_target);
                _target = null;
            }
        }
    }
}
