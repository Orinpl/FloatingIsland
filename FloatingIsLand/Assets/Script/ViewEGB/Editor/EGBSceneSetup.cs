using SoulGames.EasyGridBuilderPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.ViewEGB.EditorTools
{
    /// <summary>
    /// 向 Main 场景注入 EGB 网格最小组件集（结构见 Docs/GRID_INTEGRATION.md §5）。
    /// 用插件自带的成品 prefab（Grid Managers + EGB Pro 2 Grid XZ）当模板——它们预配好了
    /// 可视化设置、ghost/模块引用与 InputActionAsset，裸 AddComponent 会缺一堆序列化默认值（NRE + 无网格显示）。
    /// 本文件在 Assembly-CSharp-Editor（ViewEGB/Editor 无 asmdef），可同时访问 SoulGames 与 Game.* 程序集。
    /// 注意：Tools/框架/生成启动场景 会整体覆盖 Main.unity，重生成后需重跑本菜单。
    /// </summary>
    public static class EGBSceneSetup
    {
        private const string MainScenePath = "Assets/Scenes/Main.unity";
        private const string RootName = "GridSystems";

        private const string ManagersPrefabPath = "Assets/SoulGames/Easy Grid Builder Pro 2/Prefabs/Grid Managers.prefab";
        private const string GridPrefabPath = "Assets/SoulGames/Easy Grid Builder Pro 2/Prefabs/EGB Pro 2 Grid XZ.prefab";

        // 骨架默认值：只覆盖尺寸与层数；cellSize / verticalGridHeight 沿用 prefab 预配值（2 / 2，
        // 即层高折算系数 k=1 格——正式数值定下后与领域层同步改，GRID_INTEGRATION.md §3）
        private const int DefaultWidth = 32;
        private const int DefaultLength = 32;

        // 垂直层数先保持 1：风跨高度层规则未定（GAME_DESIGN §20），MVP 单层回避；
        // 高台设计闭合后升层数即可（每层会各生成一块 Object Grid 可视化面片，非活动层靠 gridHideColor alpha=0 淡出）。
        private const int DefaultLayerCount = 1;

        // EGB 网格 prefab 出厂在 Layer 31（工程未命名层）；Grid Managers prefab 出厂 mask 却是 Nothing，
        // 导致 GridManager 鼠标射线永远检测不到网格（GridManager.cs:214）——接线时必须把 mask 指到该层。
        private const int GridSystemLayer = 31;

        [MenuItem("Tools/框架/给 Main 场景接入 EGB 网格", false, 2)]
        public static void Setup()
        {
            if (!System.IO.File.Exists(MainScenePath))
            {
                EditorUtility.DisplayDialog("EGB 网格接入",
                    "找不到 Assets/Scenes/Main.unity，请先跑 Tools → 框架 → 生成启动场景（Boot + Main）。", "知道了");
                return;
            }

            var managersPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagersPrefabPath);
            var gridPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GridPrefabPath);
            if (managersPrefab == null || gridPrefab == null)
            {
                EditorUtility.DisplayDialog("EGB 网格接入",
                    "找不到插件成品 prefab：\n" + ManagersPrefabPath + "\n" + GridPrefabPath + "\n（EGB 插件被移动或删除？）", "知道了");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);

            GameObject existing = GameObject.Find(RootName);
            if (existing != null)
            {
                if (!EditorUtility.DisplayDialog("EGB 网格接入",
                    "Main 场景已有 " + RootName + " 节点，重新接线会删除重建（其上手改内容会丢失）。继续？", "重建", "取消"))
                {
                    return;
                }
                Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);

            // 管理器全家桶（GridManager + 各 Ghost/Selector/Destroyer/Mover + InputActionAsset 已内配）。
            // 浅接入下其 GridInputManager 的原生按键（进建造模式等）暂保留：未注册任何 BuildableObjectSO 时无实际效果，
            // M1 接自研输入时再决定禁用策略（GRID_INTEGRATION.md §2）。
            var managers = (GameObject)PrefabUtility.InstantiatePrefab(managersPrefab, scene);
            managers.transform.SetParent(root.transform, false);

            // 修正出厂 mask=Nothing 的坑：射线检测层对齐网格所在 Layer，顺带给未命名层补个名字方便 Inspector 阅读
            EnsureGridLayerNamed();
            var gridManager = managers.GetComponentInChildren<GridManager>();
            if (gridManager != null)
            {
                SetSerializedInt(gridManager, "gridSystemLayerMask", 1 << GridSystemLayer);
            }
            else
            {
                Debug.LogWarning("[网格] Grid Managers prefab 里找不到 GridManager 组件（插件版本变了？）。", managers);
            }

            // 网格系统（预配 Object Grid 可视化，displayOnDefaultMode=true 非建造模式也常显网格线）
            var gridGo = (GameObject)PrefabUtility.InstantiatePrefab(gridPrefab, scene);
            gridGo.transform.SetParent(root.transform, false);
            var grid = gridGo.GetComponentInChildren<EasyGridBuilderPro>();
            if (grid == null)
            {
                Debug.LogError("[网格] " + GridPrefabPath + " 上找不到 EasyGridBuilderPro 组件（插件版本变了？）。", gridGo);
                return;
            }
            OverrideGridSize(grid);

            var presenter = root.AddComponent<EGBGridPresenter>();
            SetSerializedReference(presenter, "gridSystem", grid);
            SetSerializedInt(presenter, "defaultWidth", DefaultWidth);
            SetSerializedInt(presenter, "defaultLength", DefaultLength);

            var hoverDebug = root.AddComponent<EGBHoverDebug>();
            SetSerializedReference(hoverDebug, "presenter", presenter);

            var highlighter = root.AddComponent<GridCellHighlighter>();
            SetSerializedReference(highlighter, "presenter", presenter);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[网格] EGB 已接入 Main 场景（成品 prefab 模板）：GridSystems = Grid Managers + EGB Pro 2 Grid XZ " +
                      $"{DefaultWidth}×{DefaultLength}×{DefaultLayerCount}层 + Presenter，射线检测层已对齐 Layer {GridSystemLayer}。" +
                      "Play 进入 Main 后应能看到半透明网格，左上角显示悬停格子坐标。");
        }

        private static void OverrideGridSize(EasyGridBuilderPro grid)
        {
            var so = new SerializedObject(grid);
            SetInt(so, "gridWidth", DefaultWidth);
            SetInt(so, "gridLength", DefaultLength);
            SetInt(so, "verticalGridsCount", DefaultLayerCount);

            // prefab 出厂的网格显示色是"黑色 + alpha 1"（不透明实心板，看起来一片白/黑）；
            // 覆盖成插件 3D 示例场景（3D City Builder Demo）验证过的半透明灰叠加配色。
            SetColor(so, "objectGridSettings.gridShowColor", new Color(0.5f, 0.5f, 0.5f, 0.5882353f));
            SetColor(so, "objectGridSettings.gridHideColor", new Color(0.47058824f, 0.47058824f, 0.47058824f, 0f));
            SetColor(so, "objectGridSettings.gridShowColorHDR", new Color(0f, 0f, 0f, 0f));
            SetColor(so, "objectGridSettings.gridHideColorHDR", new Color(0f, 0f, 0f, 0f));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- SerializedObject 小工具（字段名对不上时给警告而不是静默失败，防插件升级后字段改名） ----------

        private static void SetInt(SerializedObject so, string field, int value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { WarnMissing(so, field); return; }
            p.intValue = value;
        }

        private static void SetColor(SerializedObject so, string field, Color value)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { WarnMissing(so, field); return; }
            p.colorValue = value;
        }

        private static void SetSerializedInt(Component target, string field, int value)
        {
            var so = new SerializedObject(target);
            SetInt(so, field, value);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedReference(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { WarnMissing(so, field); return; }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WarnMissing(SerializedObject so, string field)
        {
            Debug.LogWarning($"[网格] {so.targetObject.GetType().Name} 上找不到序列化字段 '{field}'（EGB 插件版本变了？请在 Inspector 手动补配）。", so.targetObject);
        }

        /// <summary>Layer 31 在本工程未命名；补上名字，让 Inspector 里 mask/Layer 可读。已有名字则不动。</summary>
        private static void EnsureGridLayerNamed()
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                return;
            }
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null || layers.arraySize <= GridSystemLayer)
            {
                return;
            }
            SerializedProperty slot = layers.GetArrayElementAtIndex(GridSystemLayer);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = "EGB Grid System";
                tagManager.ApplyModifiedProperties();
            }
        }
    }
}
