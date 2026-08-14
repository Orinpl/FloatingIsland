using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace FloatingIsLand.UI
{
    /// <summary>
    /// 给全屏面板铺一层「静帧封面 + 循环视频」背景：先贴封面，视频准备好后无缝换成播放中的画面。
    ///
    /// 为什么是「运行时现建」而不是改 BootSceneBuilder：
    /// 和 <see cref="UISkin"/> 同一个理由——Boot 场景是编辑器菜单生成的，改生成器就得重跑
    /// 那条带模态弹窗的流程（见 Docs/BOOT_FRAMEWORK.md）。这里由面板自己在 Awake 里挂上来，
    /// 已生成的场景一行都不用动。
    ///
    /// 先贴封面再等视频，是为了避开 <see cref="VideoPlayer"/> 的准备期：
    /// Prepare 要读文件、解首帧，这期间 RenderTexture 是黑的，直接挂上去开场会黑一下。
    /// 约定封面图就是视频的首帧，所以切换的那一刻画面是连续的。
    ///
    /// 目前两处在用：主界面（空岛循环背景）与启动页（风脉城主视觉）。
    /// </summary>
    public sealed class PanelVideoBackground : MonoBehaviour
    {
        private string _coverPath;
        private string _clipPath;

        private RawImage _image;
        private VideoPlayer _player;
        private RenderTexture _target;
        private Texture2D _cover;

        /// <summary>调用方要不要这层背景。面板本身可见、但背景被关掉时，视频也不该继续解码。</summary>
        private bool _wantVisible = true;

        /// <summary>
        /// 挂到面板上（幂等，重复调用只会拿到已有的那一个）。
        /// </summary>
        /// <param name="panel">面板物体，背景会作为它的第一个子节点铺满。</param>
        /// <param name="coverPath">封面静帧的 Resources 路径（不含扩展名）；缺图则保持面板原来的纯色底。</param>
        /// <param name="clipPath">循环视频的 Resources 路径（不含扩展名）；缺资源则只显示封面。</param>
        public static PanelVideoBackground AttachTo(GameObject panel, string coverPath, string clipPath)
        {
            if (panel == null)
            {
                return null;
            }

            PanelVideoBackground existing = panel.GetComponent<PanelVideoBackground>();
            if (existing != null)
            {
                return existing;
            }

            // 先塞路径再 AddComponent 会拿不到实例，所以反过来：加组件后立刻初始化。
            // AddComponent 会同步跑 Awake，因此初始化逻辑不能放在 Awake 里。
            var bg = panel.AddComponent<PanelVideoBackground>();
            bg._coverPath = coverPath;
            bg._clipPath = clipPath;
            bg.Build();
            return bg;
        }

        private void Build()
        {
            BuildImage();
            ShowCover();
            StartVideo();
        }

        /// <summary>
        /// 开关这层背景。用于一块面板被多个流程状态复用、但只有其中一个状态该显示背景的情况
        /// （<see cref="LoadingPanel"/> 同时服务 Boot 与进关加载，只有 Boot 要放启动动画）。
        /// 关掉时连同视频一起暂停，不留后台解码。
        /// </summary>
        public void SetVisible(bool visible)
        {
            _wantVisible = visible;

            if (_image != null)
            {
                _image.gameObject.SetActive(visible);
            }

            if (_player == null)
            {
                return;
            }

            if (visible)
            {
                if (_player.isPrepared && !_player.isPlaying)
                {
                    _player.Play();
                }
            }
            else if (_player.isPlaying)
            {
                _player.Pause();
            }
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

            // FitInParent = 等比缩到完整可见，画面绝不裁切；屏幕比例比素材宽就让出左右边，
            // 比素材窄就让出上下边。素材的标题压在画面最底部，用 EnvelopeParent 铺满会被切掉。
            // 这里不假设任何屏幕分辨率：aspectRatio 填的是**内容自身**的比例（见 SetAspect，
            // 由封面/视频的实际像素算出），FitInParent 再按父节点的实时尺寸适配，设备比例随便变。
            // 让出来的边保持面板本身的底色（深色），不平涂天空色——原图左右边缘的天空
            // 沿高度有明显渐变（分段中位 122~188），固定色反而会在交界处出现色带。
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            // 仅在封面与视频都缺失时才会留着这个值（那种情况下也没有画面可显示）；
            // 正常路径上 ShowCover / OnPrepared 会在同一帧用真实内容比例覆盖它。
            fitter.aspectRatio = 1f;
        }

        private void ShowCover()
        {
            _cover = Resources.Load<Texture2D>(_coverPath);
            if (_cover == null)
            {
                Debug.LogWarning($"[UI] 缺少封面 Resources/{_coverPath}.png，背景保持纯色。");
                return;
            }

            _image.texture = _cover;
            SetAspect(_cover.width, _cover.height);
        }

        private void StartVideo()
        {
            VideoClip clip = Resources.Load<VideoClip>(_clipPath);
            if (clip == null)
            {
                // 视频是锦上添花：没有就停在封面静帧，不该让界面起不来。
                Debug.LogWarning($"[UI] 缺少背景视频 Resources/{_clipPath}.mp4，只显示封面静帧。");
                return;
            }

            int width = clip.width > 0 ? (int)clip.width : 1920;
            int height = clip.height > 0 ? (int)clip.height : 1080;

            _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _target.name = "PanelVideoBackgroundRT";

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
            if (_wantVisible)
            {
                player.Play();
            }
        }

        private void OnVideoError(VideoPlayer player, string message)
        {
            // 解码失败（平台不支持该编码等）就退回封面静帧，界面照常可用。
            Debug.LogWarning($"[UI] 背景视频播放失败，回退为封面静帧：{message}");
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

        // 面板被 UIManager 隐藏时没必要继续解码，省一份 CPU。
        private void OnDisable()
        {
            if (_player != null && _player.isPlaying)
            {
                _player.Pause();
            }
        }

        private void OnEnable()
        {
            if (_wantVisible && _player != null && _player.isPrepared && !_player.isPlaying)
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
