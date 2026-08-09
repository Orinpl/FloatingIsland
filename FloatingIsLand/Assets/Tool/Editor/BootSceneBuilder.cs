using System.IO;
using FloatingIsLand.App;
using FloatingIsLand.GameInput;
using FloatingIsLand.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 一键生成 Boot / Main 双场景与 UI 骨架（场景组织见 Docs/BOOT_FRAMEWORK.md）。
    /// 生成物全部是普通场景对象，生成后可自由手改；重跑会整体覆盖两个场景文件（有确认弹窗）。
    /// 骨架 UI 用传统 Text（动态字体走系统回退保证中文可显示）；正式 UI 阶段换 TMP + 中文字体资产。
    /// </summary>
    public static class BootSceneBuilder
    {
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string MainScenePath = "Assets/Scenes/Main.unity";

        private static readonly Color PanelDark = new Color(0.10f, 0.12f, 0.18f, 1f);
        private static readonly Color PanelDarker = new Color(0.06f, 0.07f, 0.10f, 1f);
        private static readonly Color ButtonBlue = new Color(0.22f, 0.45f, 0.75f, 1f);
        private static readonly Color TextWhite = new Color(0.92f, 0.94f, 0.97f, 1f);

        [MenuItem("Tools/框架/生成启动场景（Boot + Main）", false, 1)]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if ((File.Exists(BootScenePath) || File.Exists(MainScenePath)) &&
                !EditorUtility.DisplayDialog("生成启动场景",
                    "Boot.unity / Main.unity 已存在，重新生成会整体覆盖（手改内容会丢失）。继续？", "覆盖", "取消"))
            {
                return;
            }

            BuildMainScene();
            BuildBootScene();
            RegisterBuildScenes();
            EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            Debug.Log("[框架] Boot/Main 场景生成完毕，Boot 已设为 Build 首场景。直接 Play 即可跑通：初始化 → 主界面 → 加载 → 局内 → 结算 → 下一关/回主界面。");
        }

        /// <summary>给现有 Main 场景的相机补挂自由相机控制（不重生成场景，不影响 EGB 接线等手工内容）。</summary>
        [MenuItem("Tools/框架/给 Main 场景挂相机控制", false, 3)]
        public static void AttachCameraController()
        {
            if (!System.IO.File.Exists(MainScenePath))
            {
                EditorUtility.DisplayDialog("相机控制",
                    "找不到 Assets/Scenes/Main.unity，请先跑 Tools → 框架 → 生成启动场景（Boot + Main）。", "知道了");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            Camera camera = null;
            foreach (GameObject rootGo in scene.GetRootGameObjects())
            {
                camera = rootGo.GetComponentInChildren<Camera>(true);
                if (camera != null)
                {
                    break;
                }
            }
            if (camera == null)
            {
                Debug.LogError("[框架] Main 场景里找不到任何 Camera，无法挂相机控制。");
                return;
            }

            if (camera.GetComponent<GameplayCameraController>() != null)
            {
                Debug.Log("[框架] " + camera.name + " 已挂 GameplayCameraController，无需重复。", camera);
                return;
            }
            camera.gameObject.AddComponent<GameplayCameraController>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[框架] 已给 " + camera.name + " 挂上 GameplayCameraController：WASD 平移、Shift/Ctrl 升降、滚轮缩放、右键旋转、中键平移。", camera);
        }

        // ---------- Main：玩法场景（每关整体重载） ----------

        private static void BuildMainScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 主相机：不带 AudioListener（全局唯一 Listener 在 Boot 的 MenuCamera 上，避免双 Listener 警告）。
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.depth = 0f; // 高于 MenuCamera(-10)，进局后自然盖过菜单背景
            cameraGo.AddComponent<GameplayCameraController>(); // 编辑器式自由相机（WASD/升降/滚轮/右键旋转/中键平移）
            cameraGo.transform.position = new Vector3(0f, 12f, -12f);
            cameraGo.transform.rotation = Quaternion.Euler(45f, 0f, 0f);

            var lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 玩法根节点：M1 起地图渲染 / 建筑白模都挂这里，随场景卸载整体销毁。
            var gameplayRoot = new GameObject("GameplayRoot");
            gameplayRoot.transform.position = Vector3.zero;

            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        // ---------- Boot：常驻场景（AppRoot + UIRoot + EventSystem） ----------

        private static void BuildBootScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var appRoot = new GameObject("AppRoot");
            appRoot.AddComponent<GameFlow>();

            // 菜单相机：只当纯色背景用（cullingMask=0 不画任何物体）；全局唯一 AudioListener 挂这里。
            var menuCameraGo = new GameObject("MenuCamera");
            Camera menuCamera = menuCameraGo.AddComponent<Camera>();
            menuCamera.clearFlags = CameraClearFlags.SolidColor;
            menuCamera.backgroundColor = new Color(0.09f, 0.11f, 0.16f, 1f);
            menuCamera.cullingMask = 0;
            menuCamera.depth = -10f;
            menuCameraGo.AddComponent<AudioListener>();

            // UIRoot：Canvas + UIManager + FlowUIAdapter。
            var uiRootGo = new GameObject("UIRoot");
            Canvas canvas = uiRootGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = uiRootGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            uiRootGo.AddComponent<GraphicRaycaster>();
            uiRootGo.AddComponent<UIManager>();
            uiRootGo.AddComponent<FlowUIAdapter>();
            uiRootGo.AddComponent<BuildHudAdapter>(); // 局内建造 HUD 的桥接（计分区 / 手牌 / 二选一）

            BuildMainMenuPanel(uiRootGo.transform);
            BuildLoadingPanel(uiRootGo.transform);
            BuildHudPanel(uiRootGo.transform);
            BuildSettlementPanel(uiRootGo.transform);

            var eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<StandaloneInputModule>();

            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void BuildMainMenuPanel(Transform uiRoot)
        {
            MainMenuPanel panel = CreatePanel<MainMenuPanel>(uiRoot, "MainMenuPanel", PanelDark);
            CreateText(panel.transform, "Title", "浮空风岛", 64, new Vector2(0f, 200f), new Vector2(800f, 100f));
            panel.startButton = CreateButton(panel.transform, "StartButton", "开始游戏", new Vector2(0f, -20f), new Vector2(320f, 72f));
            panel.quitButton = CreateButton(panel.transform, "QuitButton", "退出游戏", new Vector2(0f, -130f), new Vector2(320f, 72f));
        }

        private static void BuildLoadingPanel(Transform uiRoot)
        {
            LoadingPanel panel = CreatePanel<LoadingPanel>(uiRoot, "LoadingPanel", PanelDarker);
            panel.messageText = CreateText(panel.transform, "Message", "加载中…", 32, Vector2.zero, new Vector2(1400f, 500f));
            panel.gameObject.SetActive(false);
        }

        private static void BuildHudPanel(Transform uiRoot)
        {
            HudPanel panel = CreatePanel<HudPanel>(uiRoot, "HudPanel", Color.clear);
            panel.GetComponent<Image>().raycastTarget = false; // 全屏透明底不挡 3D 拾取

            Text info = CreateText(panel.transform, "RunInfo", "", 26, new Vector2(0f, -34f), new Vector2(1200f, 50f));
            SetAnchor(info.rectTransform, new Vector2(0.5f, 1f));
            panel.runInfoText = info;

            Button endButton = CreateButton(panel.transform, "EndRunButton", "结束本局（占位）", new Vector2(-30f, -34f), new Vector2(240f, 52f));
            SetAnchor((RectTransform)endButton.transform, new Vector2(1f, 1f));
            panel.endRunButton = endButton;

            BuildScoreboard(panel);
            BuildHandBar(panel);
            BuildOfferBar(panel);

            Text hint = CreateText(panel.transform, "Hint", "", 24, new Vector2(0f, 210f), new Vector2(900f, 40f));
            SetAnchor(hint.rectTransform, new Vector2(0.5f, 0f));
            panel.hintText = hint;

            panel.gameObject.SetActive(false);
        }

        /// <summary>左下角计分区：总分 / 金币 / 等级 + 解锁下一组建筑按钮（GAME_DESIGN §7.4）。</summary>
        private static void BuildScoreboard(HudPanel panel)
        {
            GameObject boardGo = CreateUIObject("Scoreboard", panel.transform);
            var boardRt = (RectTransform)boardGo.transform;
            boardRt.sizeDelta = new Vector2(360f, 230f);
            boardRt.anchoredPosition = new Vector2(24f, 24f);
            SetAnchor(boardRt, new Vector2(0f, 0f));
            Image boardBg = boardGo.AddComponent<Image>();
            boardBg.color = new Color(0.06f, 0.07f, 0.10f, 0.72f);

            panel.scoreText = CreateLeftText(boardGo.transform, "Score", "总分 0", 34, new Vector2(18f, -18f), new Vector2(320f, 44f));
            panel.goldText = CreateLeftText(boardGo.transform, "Gold", "金币 0", 28, new Vector2(18f, -64f), new Vector2(320f, 38f));
            panel.levelText = CreateLeftText(boardGo.transform, "Level", "等级 0 / 20", 24, new Vector2(18f, -104f), new Vector2(320f, 34f));

            Button next = CreateButton(boardGo.transform, "NextGroupButton", "解锁下一组", new Vector2(18f, -148f), new Vector2(324f, 62f));
            SetAnchor((RectTransform)next.transform, new Vector2(0f, 1f));
            panel.nextGroupButton = next;
            panel.nextGroupButtonLabel = next.GetComponentInChildren<Text>(true);
        }

        /// <summary>中下建筑列表（手牌）：横向排布，点一张进入摆放模式。</summary>
        private static void BuildHandBar(HudPanel panel)
        {
            GameObject barGo = CreateUIObject("HandBar", panel.transform);
            var barRt = (RectTransform)barGo.transform;
            barRt.sizeDelta = new Vector2(1200f, 130f);
            barRt.anchoredPosition = new Vector2(0f, 30f);
            SetAnchor(barRt, new Vector2(0.5f, 0f));

            var layout = barGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            panel.handRoot = barRt;
            panel.handItemTemplate = CreateListItemTemplate(barGo.transform, "HandItemTemplate", new Vector2(150f, 108f));
        }

        /// <summary>屏幕中部的建筑组二选一。没有待选组时整块隐藏。</summary>
        private static void BuildOfferBar(HudPanel panel)
        {
            GameObject barGo = CreateUIObject("OfferBar", panel.transform);
            var barRt = (RectTransform)barGo.transform;
            barRt.sizeDelta = new Vector2(1000f, 120f);
            barRt.anchoredPosition = new Vector2(0f, 0f);
            SetAnchor(barRt, new Vector2(0.5f, 0.5f));

            var layout = barGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            panel.offerRoot = barRt;
            panel.offerItemTemplate = CreateListItemTemplate(barGo.transform, "OfferItemTemplate", new Vector2(760f, 76f));
            barGo.SetActive(false);
        }

        /// <summary>
        /// 列表条目模板：本体常驻隐藏，运行时按数量克隆。
        /// 样式集中在这里改，HudPanel 里就不用手搓 RectTransform。
        /// </summary>
        private static Button CreateListItemTemplate(Transform parent, string name, Vector2 size)
        {
            Button button = CreateButton(parent, name, "", Vector2.zero, size);
            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = size.x;
            layoutElement.preferredHeight = size.y;
            button.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.75f);

            Text label = button.GetComponentInChildren<Text>(true);
            label.color = new Color(0.08f, 0.09f, 0.13f, 1f);
            label.fontSize = 26;

            button.gameObject.SetActive(false);
            return button;
        }

        /// <summary>左上角对齐的文本（计分区逐行排布用）。</summary>
        private static Text CreateLeftText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size)
        {
            Text text = CreateText(parent, name, content, fontSize, anchoredPos, size);
            text.alignment = TextAnchor.MiddleLeft;
            SetAnchor(text.rectTransform, new Vector2(0f, 1f));
            return text;
        }

        private static void BuildSettlementPanel(Transform uiRoot)
        {
            SettlementPanel panel = CreatePanel<SettlementPanel>(uiRoot, "SettlementPanel", new Color(0.08f, 0.09f, 0.13f, 0.98f));
            panel.summaryText = CreateText(panel.transform, "Summary", "", 36, new Vector2(0f, 100f), new Vector2(1400f, 400f));
            panel.nextRunButton = CreateButton(panel.transform, "NextRunButton", "下一关", new Vector2(-180f, -180f), new Vector2(300f, 70f));
            panel.menuButton = CreateButton(panel.transform, "MenuButton", "回主界面", new Vector2(180f, -180f), new Vector2(300f, 70f));
            panel.gameObject.SetActive(false);
        }

        // ---------- uGUI 构建小工具 ----------

        private static T CreatePanel<T>(Transform uiRoot, string name, Color background) where T : UIPanel
        {
            GameObject go = CreateUIObject(name, uiRoot);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = background;
            return go.AddComponent<T>();
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.text = content;
            text.color = TextWhite;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = CreateUIObject(name, parent);
            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            Image image = go.AddComponent<Image>();
            image.color = ButtonBlue;
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(go.transform, "Text", label, 28, Vector2.zero, size);
            RectTransform textRt = text.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            return button;
        }

        private static GameObject CreateUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>把锚点/枢轴一起设到指定位置（anchoredPosition 不变，相对新锚点生效）。</summary>
        private static void SetAnchor(RectTransform rt, Vector2 anchor)
        {
            Vector2 pos = rt.anchoredPosition;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
        }

        private static void RegisterBuildScenes()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
            };
        }
    }
}
