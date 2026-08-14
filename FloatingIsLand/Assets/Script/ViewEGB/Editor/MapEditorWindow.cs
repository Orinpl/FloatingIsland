using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;
using FloatingIsLand.View;
using SoulGames.EasyGridBuilderPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FloatingIsLand.ViewEGB.EditorTools
{
    /// <summary>
    /// 地图编辑器（原「地形刷子」扩展版）：在 Scene 视图里编辑关卡地图，存成
    /// Resources/Maps/stage_{id}.json。请在独立的地图编辑场景里用（Tools → 地图 → 打开地图编辑场景），
    /// Main 只作为游戏加载的场景。
    ///
    /// 能编辑的三层：
    ///   · 地形层（cells）——「笔刷」工具选地形行，逐格刷/擦，与旧刷子一致；
    ///   · 地图元素（矿藏/锚点/巨型风车等非地形行）——「笔刷」工具选元素行，幽灵 Prefab 悬浮
    ///     在鼠标处（与游戏内建造同一份摆放口径），占地按配表 footprint 校验，点击落位、
    ///     可选中/转向/删除。写的全是地图 JSON 数据，运行时 WorldRenderer 再按数据实例化；
    ///   · 风源（elements 里的 windSource）——「风源」工具点击放置/选中，在面板上改
    ///     朝向/强度/长度，Scene 视图里用**运行时同一份 <see cref="WindSimulator"/>** 画风的传播预览。
    ///
    /// 只读的参考层：载入地图时自动实例化对应 Stage 的岛屿 Prefab 并用 <see cref="IslandFitter"/>
    /// 对位（HideAndDontSave 临时实例，编辑动不到 Prefab 资产，也不会存进场景）。
    ///
    /// 高度层：层数由本编辑器管理（正本随地图 JSON 的 layerCount，添加/删除顶层按钮增删），
    /// 不依赖 EGB 场景网格的 verticalGridsCount——坐标全走 GridGeometry，层高取网格 verticalGridHeight。
    /// 每层可单独勾选显隐，勾选的层才渲染（地形 overlay / 元素 / 风预览一并过滤）；笔刷与摆放只作用于当前编辑层。
    ///
    /// 为什么在 ViewEGB/Editor：编辑器必须读 EGB 组件的网格参数才能与运行时对齐，而
    /// using SoulGames 只允许出现在 ViewEGB/（GRID_INTEGRATION §4）。
    /// 坐标换算统一走 <see cref="GridGeometry"/>（EGB 的运行时 API 编辑器态不可用）。
    /// </summary>
    public sealed class MapEditorWindow : EditorWindow
    {
        private const string MapsFolder = "Assets/Resources/Maps";
        private const string StagePrefabFolder = "Assets/Resources/Prefab/Stage";
        private const string ResourcesPrefix = "Assets/Resources/";
        private const int MaxBrushSize = 15;

        /// <summary>手工风参数的兜底默认（配表没加载上时用）。</summary>
        private const int FallbackWindForce = 2;
        private const int FallbackWindLength = 12;

        /// <summary>Scene 视图绘制高度：占地框贴地，风预览抬高一点避免被岛面吃掉。</summary>
        private const float OutlineY = 0.08f;
        private const float WindMarkerY = 0.15f;
        private const float WindPathY = 0.6f;

        private const string IslandName = "__MapEditor_Island";
        private const string ElementsName = "__MapEditor_Elements";
        private const string GhostName = "__MapEditor_Ghost";

        private enum EditTool
        {
            None = 0,
            Terrain = 1,
            Wind = 2,
        }

        private static readonly string[] ToolLabels = { "浏览", "笔刷", "风源" };

        private static readonly string[] RotationLabels = { "0°", "90°", "180°", "270°" };

        /// <summary>八向标签，下标 = <see cref="Dir8"/> 数值（E 起逆时针）。</summary>
        private static readonly string[] DirLabels = { "东 →", "东北 ↗", "北 ↑", "西北 ↖", "西 ←", "西南 ↙", "南 ↓", "东南 ↘" };

        [SerializeField] private int stageId = 1;
        [SerializeField] private int layer;
        [SerializeField] private int brushSize = 1;
        [SerializeField] private string brushElementId;
        [SerializeField] private int toolIndex;

        /// <summary>岛屿参考用哪个 Prefab（Resources 相对路径）；空 = 按 Stage 表的 prefabPath。</summary>
        [SerializeField] private string islandPrefabOverride = string.Empty;

        /// <summary>Maps 文件夹扫出来的地图文件（文件名 / 对应关卡 Id，平行数组）。</summary>
        private string[] _mapFiles = Array.Empty<string>();
        private int[] _mapFileStageIds = Array.Empty<int>();

        /// <summary>Prefab/Stage 文件夹扫出来的岛屿 Prefab（Resources 相对路径）。</summary>
        private string[] _islandPrefabs = Array.Empty<string>();

        /// <summary>元素调色板行（isTerrain=FALSE 且非风源；风源走「风源」工具）。</summary>
        private List<MapElementRow> _elementRows = new List<MapElementRow>();

        /// <summary>选中的已落位元素（_elements 下标，非风源；-1 = 无）。</summary>
        private int _selectedElement = -1;

        /// <summary>元素摆放幽灵（HideAndDontSave 跟随鼠标；只是预览，落位才写数据）。</summary>
        private GameObject _ghost;
        private string _ghostElementId;

        /// <summary>幽灵朝向（0~3 = 0°/90°/180°/270°，R 键轮转）。</summary>
        [SerializeField] private int ghostRotation;

        /// <summary>
        /// 编辑中的高度层数。正本随地图 JSON（载入覆盖、存盘写回）；EGB 场景网格的
        /// verticalGridsCount 与此无关——层高取网格的 verticalGridHeight，坐标全走 GridGeometry。
        /// </summary>
        [SerializeField] private int editLayerCount = 1;

        /// <summary>逐层显隐（下标 = 层号）。勾选的层才渲染：地形 overlay、元素模型/占地框、风预览一并过滤。</summary>
        [SerializeField] private List<bool> layerVisible = new List<bool> { true };

        /// <summary>编辑中的地形层：(x, z, layer) → elementId。只有这里有的格子才会存盘。</summary>
        private readonly Dictionary<Vector3Int, string> _painted = new Dictionary<Vector3Int, string>();

        /// <summary>编辑中的地图元素（含风源），载入时原样接管、存盘时原样写回——不会再像旧刷子那样丢掉。</summary>
        private readonly List<MapElementPlacement> _elements = new List<MapElementPlacement>();

        /// <summary>选中的风源（_elements 下标；-1 = 无）。</summary>
        private int _selectedWind = -1;

        /// <summary>笔画级撤销栈：每次按下到抬起算一笔，存这笔改动前的原值（null = 原本没刷过）。</summary>
        private readonly List<Dictionary<Vector3Int, string>> _undoStack = new List<Dictionary<Vector3Int, string>>();
        private Dictionary<Vector3Int, string> _strokeUndo;

        private List<MapElementRow> _terrainRows;
        private EasyGridBuilderPro _grid;
        private TerrainOverlayRenderer _overlay;
        private BuildRuleSet _rules;
        private bool _dirty;
        private Vector2 _scroll;

        /// <summary>岛屿参考实例（HideAndDontSave，编辑不落盘）与状态说明。</summary>
        private GameObject _island;
        private string _islandStatus;

        /// <summary>元素模型参考实例的挂载根（HideAndDontSave）。</summary>
        private GameObject _elementRoot;

        /// <summary>风预览：手工风源跑真实模拟器的结果；下标对齐 <see cref="_windStreamElementIndex"/>。</summary>
        private WindField _windPreview;
        private readonly List<int> _windStreamElementIndex = new List<int>();

        private readonly List<CellCoord> _cellScratch = new List<CellCoord>(16);

        [MenuItem("Tools/地图/地图编辑器", false, 3)]
        public static void Open()
        {
            GetWindow<MapEditorWindow>("地图编辑器").Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            // 域重载会把引用清掉但 HideAndDontSave 实例可能残留成孤儿，先扫掉再说
            DestroyStaleByName(IslandName);
            DestroyStaleByName(ElementsName);
            DestroyStaleByName(GhostName);
            RefreshSceneRefs();
            RefreshTerrainRows();
            RefreshFileLists();
        }

        private void OnFocus()
        {
            // 窗口每次拿到焦点重扫一遍：新加/删除的地图文件与岛屿 Prefab 自动进下拉
            RefreshFileLists();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyIsland();
            DestroyElementModels();
            DestroyGhost();
        }

        // ---------- 场景/配表引用 ----------

        private void RefreshSceneRefs()
        {
            _grid = FindObjectOfType<EasyGridBuilderPro>();
            _overlay = FindObjectOfType<TerrainOverlayRenderer>();
        }

        /// <summary>扫两个文件夹填下拉：Maps 下的 stage_*.json 与 Prefab/Stage 下的岛屿 Prefab。</summary>
        private void RefreshFileLists()
        {
            var ids = new List<int>();
            var names = new List<string>();
            if (Directory.Exists(MapsFolder))
            {
                foreach (string file in Directory.GetFiles(MapsFolder, "*.json"))
                {
                    string name = Path.GetFileName(file);
                    Match m = Regex.Match(name, @"^stage_(\d+)\.json$");
                    int id;
                    if (m.Success && int.TryParse(m.Groups[1].Value, out id))
                    {
                        ids.Add(id);
                        names.Add(name);
                    }
                }
            }
            int[] idArray = ids.ToArray();
            string[] nameArray = names.ToArray();
            Array.Sort(idArray, nameArray);
            _mapFileStageIds = idArray;
            _mapFiles = nameArray;

            var prefabs = new List<string>();
            if (AssetDatabase.IsValidFolder(StagePrefabFolder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { StagePrefabFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.StartsWith(ResourcesPrefix, StringComparison.Ordinal))
                    {
                        continue;
                    }
                    // Resources.Load 用的相对路径：去掉 Assets/Resources/ 前缀和扩展名
                    string resPath = path.Substring(ResourcesPrefix.Length);
                    resPath = resPath.Substring(0, resPath.Length - Path.GetExtension(resPath).Length);
                    prefabs.Add(resPath);
                }
                prefabs.Sort(StringComparer.OrdinalIgnoreCase);
            }
            _islandPrefabs = prefabs.ToArray();
        }

        private bool EnsureTables()
        {
            if (TableLoader.IsLoaded)
            {
                return true;
            }
            try
            {
                UnityTableLoader.LoadFromResources();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("[地图编辑器] 配表加载失败：" + e.Message);
                return false;
            }
        }

        private BuildRuleSet Rules()
        {
            if (_rules != null)
            {
                return _rules;
            }
            if (!EnsureTables())
            {
                return null;
            }
            try
            {
                _rules = BuildRuleSetFactory.Create();
            }
            catch (Exception e)
            {
                Debug.LogError("[地图编辑器] 规则集构建失败（元素占地/风参数区间都来自它）：" + e.Message);
            }
            return _rules;
        }

        private void RefreshTerrainRows()
        {
            _terrainRows = new List<MapElementRow>();
            _elementRows = new List<MapElementRow>();
            if (!EnsureTables())
            {
                return;
            }

            // 调色板两组：isTerrain=TRUE 的地形（拖动连刷）与多格地图元素（点击落位）。
            // windSource 不进元素组——它带风参数，用专门的「风源」工具编辑。
            foreach (MapElementRow row in Tables.MapElement.All)
            {
                if (row.isTerrain)
                {
                    _terrainRows.Add(row);
                }
                else if (!string.Equals(row.elementId, BuildRuleSet.WindSourceElementId, StringComparison.Ordinal))
                {
                    _elementRows.Add(row);
                }
            }

            if (string.IsNullOrEmpty(brushElementId) && _terrainRows.Count > 0)
            {
                brushElementId = _terrainRows[0].elementId;
            }
        }

        /// <summary>笔刷当前选的是不是元素（而不是地形）。</summary>
        private MapElementRow FindElementRow(string elementId)
        {
            if (_elementRows == null || string.IsNullOrEmpty(elementId))
            {
                return null;
            }
            for (int i = 0; i < _elementRows.Count; i++)
            {
                if (_elementRows[i].elementId == elementId)
                {
                    return _elementRows[i];
                }
            }
            return null;
        }

        private bool TryGetGeometry(out GridGeometry geometry)
        {
            if (_grid == null)
            {
                geometry = default(GridGeometry);
                return false;
            }
            geometry = new GridGeometry(
                _grid.transform.position,
                _grid.GetGridWidth(),
                _grid.GetGridLength(),
                _grid.GetCellSize(),
                _grid.GetVerticalGridHeight(),
                _grid.GetGridOriginType() == GridOrigin.Center);
            return geometry.IsValid;
        }

        // ---------- 面板 ----------

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_grid == null || _overlay == null)
            {
                DrawSetupHelp();
                EditorGUILayout.EndScrollView();
                return;
            }

            GridGeometry geometry;
            if (!TryGetGeometry(out geometry))
            {
                EditorGUILayout.HelpBox("EGB 网格参数非法（宽/长/格大小有 0？），无法换算坐标。", MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            DrawStageSection(geometry);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("工具", EditorStyles.boldLabel);
            int pickedTool = GUILayout.Toolbar(toolIndex, ToolLabels);
            if (pickedTool != toolIndex)
            {
                toolIndex = pickedTool;
                DestroyGhost();
            }

            EditorGUILayout.Space(2);
            switch ((EditTool)toolIndex)
            {
                case EditTool.Terrain:
                    DrawBrushSection();
                    break;
                case EditTool.Wind:
                    DrawWindSection();
                    break;
                default:
                    EditorGUILayout.LabelField("浏览模式：左键点元素/风源可选中编辑，其余操作不拦截。", EditorStyles.miniLabel);
                    break;
            }

            // 选中面板不分工具：浏览/笔刷/风源模式下点中的东西都在这里编辑
            DrawSelectedElementPanel(Rules());
            DrawSelectedWindPanel();

            EditorGUILayout.Space();
            DrawFileSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSetupHelp()
        {
            EditorGUILayout.HelpBox(
                _grid == null
                    ? "当前场景里没有 EGB 网格系统。\n地图编辑请在独立的地图编辑场景里进行（不要用 Main）。"
                    : "场景里没有 TerrainOverlayRenderer（地形渲染 + 落笔预览都靠它）。\n重新生成一次地图编辑场景即可补上。",
                MessageType.Warning);

            if (GUILayout.Button("打开地图编辑场景（没有会自动生成）"))
            {
                MapEditorSceneSetup.OpenScene();
                RefreshSceneRefs();
            }
            if (GUILayout.Button("重新查找场景引用"))
            {
                RefreshSceneRefs();
            }
        }

        private void DrawStageSection(GridGeometry geometry)
        {
            EditorGUILayout.LabelField("关卡", EditorStyles.boldLabel);
            stageId = EditorGUILayout.IntField("关卡 Id（Stage.stageId）", Mathf.Max(1, stageId));

            EditorGUILayout.LabelField("场景网格尺寸", $"{geometry.Width} × {geometry.Length}，格大小 {geometry.CellSize}");

            // Stage 表写的是设计目标尺寸；场景 EGB 网格是实际可刷范围。两者不一致时刷出来的图会比设计小/大，
            // 存盘用的是场景尺寸（运行时按快照 BuildGrid），所以这里只提示不强制。
            StageRow stage = TableLoader.IsLoaded ? Tables.Stage.GetOrNull(stageId) : null;
            if (stage != null && (stage.mapWidth != geometry.Width || stage.mapHeight != geometry.Length))
            {
                EditorGUILayout.HelpBox(
                    $"Stage 表里第 {stageId} 关是 {stage.mapWidth}×{stage.mapHeight}，场景网格是 {geometry.Width}×{geometry.Length}。\n" +
                    "存盘记录的是场景尺寸（运行时按快照建格）。要按表来就点下面的按钮。",
                    MessageType.Info);
                if (GUILayout.Button($"把场景网格调成 {stage.mapWidth}×{stage.mapHeight}"))
                {
                    ResizeSceneGrid(stage.mapWidth, stage.mapHeight);
                }
            }

            DrawLayerSection();

            // 岛屿参考：只读的对位实例，编辑动不到 Prefab。下拉列出 Prefab/Stage 下扫到的全部 Prefab
            using (new EditorGUILayout.HorizontalScope())
            {
                var options = new string[_islandPrefabs.Length + 1];
                options[0] = "按 Stage 表";
                int selected = 0;
                for (int i = 0; i < _islandPrefabs.Length; i++)
                {
                    // Popup 里的 '/' 会变子菜单，显示只留文件名；值仍是完整 Resources 路径
                    options[i + 1] = Path.GetFileName(_islandPrefabs[i]);
                    if (_islandPrefabs[i] == islandPrefabOverride)
                    {
                        selected = i + 1;
                    }
                }

                EditorGUI.BeginChangeCheck();
                selected = EditorGUILayout.Popup("岛屿参考", selected, options);
                if (EditorGUI.EndChangeCheck())
                {
                    islandPrefabOverride = selected == 0 ? string.Empty : _islandPrefabs[selected - 1];
                    GridGeometry g;
                    if (TryGetGeometry(out g))
                    {
                        LoadIslandReference(g);
                    }
                }

                if (GUILayout.Button(_island != null ? "重新对齐" : "加载岛屿", GUILayout.Width(80f)))
                {
                    GridGeometry g;
                    if (TryGetGeometry(out g))
                    {
                        LoadIslandReference(g);
                    }
                }
            }
            if (!string.IsNullOrEmpty(_islandStatus))
            {
                EditorGUILayout.LabelField(" ", _islandStatus, EditorStyles.miniLabel);
            }
        }

        // ---------- 高度层 ----------

        /// <summary>层数与 layerVisible 列表对齐，编辑层夹进合法区间。</summary>
        private void EnsureLayerListSize()
        {
            editLayerCount = Mathf.Max(1, editLayerCount);
            while (layerVisible.Count < editLayerCount)
            {
                layerVisible.Add(true);
            }
            if (layerVisible.Count > editLayerCount)
            {
                layerVisible.RemoveRange(editLayerCount, layerVisible.Count - editLayerCount);
            }
            layer = Mathf.Clamp(layer, 0, editLayerCount - 1);
        }

        private bool IsLayerVisible(int l)
        {
            return l >= 0 && l < layerVisible.Count && layerVisible[l];
        }

        private void DrawLayerSection()
        {
            EnsureLayerListSize();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("高度层", $"{editLayerCount} 层（层高 {_grid.GetVerticalGridHeight()} 米）");
                if (GUILayout.Button("添加一层", GUILayout.Width(70f)))
                {
                    editLayerCount++;
                    EnsureLayerListSize();
                    layerVisible[editLayerCount - 1] = true;
                    layer = editLayerCount - 1;
                    _dirty = true;
                    RefreshOverlay();
                }
                using (new EditorGUI.DisabledScope(editLayerCount <= 1))
                {
                    if (GUILayout.Button("删除顶层", GUILayout.Width(70f)))
                    {
                        RemoveTopLayer();
                    }
                }
            }

            using (new EditorGUI.DisabledScope(editLayerCount <= 1))
            {
                EditorGUI.BeginChangeCheck();
                layer = EditorGUILayout.IntSlider("编辑层", layer, 0, editLayerCount - 1);
                if (EditorGUI.EndChangeCheck())
                {
                    // 正在编辑的层必须看得见，否则刷了也不知道刷哪了
                    layerVisible[layer] = true;
                    RefreshDisplayForLayers();
                }
            }

            // 逐层显隐勾选：勾上的层才渲染
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("显示", GUILayout.Width(EditorGUIUtility.labelWidth - 4f));
                bool changed = false;
                for (int l = 0; l < editLayerCount; l++)
                {
                    bool visible = GUILayout.Toggle(layerVisible[l], $"第{l}层", EditorStyles.miniButton);
                    if (visible != layerVisible[l])
                    {
                        layerVisible[l] = visible;
                        changed = true;
                    }
                }
                if (changed)
                {
                    RefreshDisplayForLayers();
                }
            }
        }

        /// <summary>删掉最顶层：层上的地形与元素一并移除（有内容时先确认）。</summary>
        private void RemoveTopLayer()
        {
            int top = editLayerCount - 1;

            int cellCount = 0;
            foreach (KeyValuePair<Vector3Int, string> kv in _painted)
            {
                if (kv.Key.z == top)
                {
                    cellCount++;
                }
            }
            int elementCount = 0;
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i].Layer == top)
                {
                    elementCount++;
                }
            }

            if ((cellCount > 0 || elementCount > 0)
                && !EditorUtility.DisplayDialog("地图编辑器",
                    $"第 {top} 层还有 {cellCount} 格地形 / {elementCount} 个元素，删除顶层会一并移除。继续？", "删除", "取消"))
            {
                return;
            }

            var doomed = new List<Vector3Int>();
            foreach (KeyValuePair<Vector3Int, string> kv in _painted)
            {
                if (kv.Key.z == top)
                {
                    doomed.Add(kv.Key);
                }
            }
            foreach (Vector3Int key in doomed)
            {
                _painted.Remove(key);
            }
            _elements.RemoveAll(e => e.Layer == top);
            _selectedWind = -1;
            _selectedElement = -1;

            // 撤销栈里可能有已删除层的地块，恢复出来会让快照越界——直接清掉
            _undoStack.Clear();

            editLayerCount--;
            EnsureLayerListSize();
            _dirty = true;
            MarkElementsDirty();
            RefreshOverlay();
        }

        /// <summary>层显隐变化后的全量刷新：overlay、元素模型、Scene 视图。</summary>
        private void RefreshDisplayForLayers()
        {
            RefreshOverlay();
            GridGeometry geometry;
            if (TryGetGeometry(out geometry))
            {
                RebuildElementModels(geometry);
            }
            SceneView.RepaintAll();
        }

        private void DrawBrushSection()
        {
            EditorGUILayout.LabelField("笔刷", EditorStyles.boldLabel);

            if ((_terrainRows == null || _terrainRows.Count == 0) && (_elementRows == null || _elementRows.Count == 0))
            {
                EditorGUILayout.HelpBox(
                    "配表 MapElement 是空的，调色板没内容。\n改 Tables/FloatingIsland.xlsx 后跑 Tools → 配表 → 转表。",
                    MessageType.Warning);
                if (GUILayout.Button("重新读取配表"))
                {
                    _rules = null;
                    RefreshTerrainRows();
                }
                return;
            }

            BuildRuleSet rules = Rules();

            EditorGUILayout.LabelField("地形（左键拖动连刷 / Shift+左键擦）", EditorStyles.miniLabel);
            foreach (MapElementRow row in _terrainRows)
            {
                bool selected = row.elementId == brushElementId;
                var content = new GUIContent($"{row.nameCn}  ({row.elementId})");
                if (GUILayout.Toggle(selected, content, EditorStyles.miniButton) && !selected)
                {
                    brushElementId = row.elementId;
                    DestroyGhost();
                }
            }

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("地图元素（左键落位 / R 转向 / 点已有的选中 / Shift+左键删除）", EditorStyles.miniLabel);
            foreach (MapElementRow row in _elementRows)
            {
                bool selected = row.elementId == brushElementId;
                MapElementDef def = rules != null ? rules.GetElementOrNull(row.elementId) : null;
                string size = def != null ? $"{def.Footprint.Columns}×{def.Footprint.Rows}" : "?";
                var content = new GUIContent($"{row.nameCn}  ({row.elementId}, {size})");
                if (GUILayout.Toggle(selected, content, EditorStyles.miniButton) && !selected)
                {
                    brushElementId = row.elementId;
                    DestroyGhost();
                }
            }

            EditorGUILayout.Space(2);
            if (FindElementRow(brushElementId) != null)
            {
                EditorGUILayout.LabelField("摆放朝向", RotationLabels[ghostRotation & 3] + "（Scene 视图按 R 轮转）");
            }
            else
            {
                brushSize = EditorGUILayout.IntSlider("笔刷边长（格）", Mathf.Clamp(brushSize, 1, MaxBrushSize), 1, MaxBrushSize);
                EditorGUILayout.LabelField("已刷地块", $"{_painted.Count} 格" + (_dirty ? "（未保存）" : string.Empty));
                using (new EditorGUI.DisabledScope(_undoStack.Count == 0))
                {
                    if (GUILayout.Button($"撤销上一笔（{_undoStack.Count}）"))
                    {
                        UndoStroke();
                    }
                }
            }
        }

        /// <summary>选中的已落位元素：信息 + 转向 + 删除（Prefab 只是预览，改的全是 JSON 数据）。</summary>
        private void DrawSelectedElementPanel(BuildRuleSet rules)
        {
            if (_selectedElement < 0 || _selectedElement >= _elements.Count
                || IsWindSource(_elements[_selectedElement]))
            {
                _selectedElement = -1;
                return;
            }

            MapElementPlacement el = _elements[_selectedElement];
            MapElementDef def = rules != null ? rules.GetElementOrNull(el.ElementId) : null;

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("选中元素", $"{(def != null ? def.NameCn : el.ElementId)} ({el.X}, {el.Z}) 层 {el.Layer}");

            EditorGUI.BeginChangeCheck();
            int rotation = EditorGUILayout.Popup("朝向", (int)el.Rotation, RotationLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _elements[_selectedElement] = new MapElementPlacement(
                    el.ElementId, el.X, el.Z, el.Layer, (Rotation)rotation, el.WindDir, el.WindForce, el.WindLength);
                MarkElementsDirty();
            }

            if (GUILayout.Button("删除选中元素"))
            {
                RemovePlacedElement(_selectedElement);
            }
        }

        private void DrawWindSection()
        {
            EditorGUILayout.LabelField("风源（Scene 视图左键放置/选中 / Shift+左键删除 / Esc 取消选中）", EditorStyles.boldLabel);

            int windCount = 0;
            int authoredCount = 0;
            for (int i = 0; i < _elements.Count; i++)
            {
                if (IsWindSource(_elements[i]))
                {
                    windCount++;
                    if (_elements[i].HasWindParams)
                    {
                        authoredCount++;
                    }
                }
            }
            EditorGUILayout.LabelField("风源数量", $"{windCount} 个（手工参数 {authoredCount} 个，其余运行时随机）");

            if (_selectedWind < 0)
            {
                EditorGUILayout.HelpBox("在 Scene 视图里点一个风源以编辑；点空格子会放置一个新风源。", MessageType.Info);
            }
        }

        /// <summary>选中的风源：参数编辑（任何工具模式下都显示，浏览模式点中的也在这里改）。</summary>
        private void DrawSelectedWindPanel()
        {
            if (_selectedWind < 0 || _selectedWind >= _elements.Count || !IsWindSource(_elements[_selectedWind]))
            {
                _selectedWind = -1;
                return;
            }

            MapElementPlacement el = _elements[_selectedWind];
            BuildRuleSet rules = Rules();
            int maxLevel = rules != null ? rules.MaxWindLevel : 5;

            int dir = el.HasWindParams ? el.WindDir : (int)Dir8.E;
            int force = el.HasWindParams ? el.WindForce : DefaultWindForce(rules);
            int length = el.HasWindParams ? el.WindLength : DefaultWindLength(rules);

            EditorGUILayout.LabelField("选中风源", $"({el.X}, {el.Z}) 层 {el.Layer}");
            if (!el.HasWindParams)
            {
                EditorGUILayout.HelpBox("此风源还没有手工参数（运行时按局种子随机）。改动任一项即固化为手工风。", MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            dir = EditorGUILayout.Popup("朝向", Mathf.Clamp(dir, 0, 7), DirLabels);
            force = EditorGUILayout.IntSlider("强度（风力）", Mathf.Clamp(force, 1, maxLevel), 1, maxLevel);
            length = Mathf.Clamp(EditorGUILayout.IntField("长度（格）", length), 1, 999);
            if (EditorGUI.EndChangeCheck())
            {
                _elements[_selectedWind] = new MapElementPlacement(
                    el.ElementId, el.X, el.Z, el.Layer, el.Rotation, dir, force, length);
                MarkWindDirty();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("删除选中风源"))
                {
                    RemoveWindSource(_selectedWind);
                }
                if (el.HasWindParams && GUILayout.Button("还原为随机风"))
                {
                    _elements[_selectedWind] = new MapElementPlacement(el.ElementId, el.X, el.Z, el.Layer, el.Rotation);
                    MarkWindDirty();
                }
            }
        }

        private void DrawFileSection()
        {
            EditorGUILayout.LabelField("存读", EditorStyles.boldLabel);

            // 地图文件下拉：Maps 文件夹扫出来的 stage_*.json，选中即载入；当前关没有文件时多列一项「未创建」
            int current = Array.IndexOf(_mapFileStageIds, stageId);
            bool virtualEntry = current < 0;
            var options = new string[_mapFiles.Length + (virtualEntry ? 1 : 0)];
            Array.Copy(_mapFiles, options, _mapFiles.Length);
            if (virtualEntry)
            {
                current = options.Length - 1;
                options[current] = $"stage_{stageId}.json（未创建）";
            }

            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("文件", current, options);
            if (EditorGUI.EndChangeCheck() && picked < _mapFileStageIds.Length && _mapFileStageIds[picked] != stageId)
            {
                int previous = stageId;
                stageId = _mapFileStageIds[picked];
                if (!Load())
                {
                    // 用户取消/载入失败：回退关卡号，防止把旧数据存进错误的文件
                    stageId = previous;
                }
            }
            EditorGUILayout.LabelField(" ", AssetPath(stageId), EditorStyles.miniLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存"))
                {
                    Save();
                }
                using (new EditorGUI.DisabledScope(!File.Exists(AssetPath(stageId))))
                {
                    if (GUILayout.Button("载入"))
                    {
                        Load();
                    }
                }
                if (GUILayout.Button("清空地形"))
                {
                    if (EditorUtility.DisplayDialog("地图编辑器",
                            $"清空第 {stageId} 关当前编辑的全部 {_painted.Count} 个地块？（元素与风源保留，不影响已保存的文件）", "清空", "取消"))
                    {
                        BeginStroke();
                        foreach (KeyValuePair<Vector3Int, string> kv in _painted)
                        {
                            _strokeUndo[kv.Key] = kv.Value;
                        }
                        _painted.Clear();
                        EndStroke();
                        MarkDirtyAndRefresh();
                    }
                }
            }
        }

        // ---------- Scene 视图交互 ----------

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_grid == null)
            {
                return;
            }

            GridGeometry geometry;
            if (!TryGetGeometry(out geometry))
            {
                return;
            }

            // 参考层永远画：刷格子需要看得见建筑与风
            DrawElementOverlays(geometry);
            DrawWindOverlays(geometry);

            var tool = (EditTool)toolIndex;
            if (tool == EditTool.None)
            {
                HandleBrowseSelection(geometry, Event.current);
                return;
            }

            // 吃掉默认的点选/框选，否则左键会去选场景物体而不是编辑格子
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int hx, hz;
            bool onGrid = geometry.RaycastCell(ray, layer, out hx, out hz);

            if (tool == EditTool.Terrain)
            {
                HandleBrushTool(geometry, e, onGrid, hx, hz);
            }
            else
            {
                HandleWindTool(geometry, e, onGrid, hx, hz);
            }

            sceneView.Repaint();
        }

        /// <summary>
        /// 浏览模式的点选：不 AddDefaultControl——只有真点到元素/风源才吃掉事件，
        /// 其余点击照常走 Unity 的场景选择与导航。从最高可见层往低层探，点得到高台上的东西。
        /// </summary>
        private void HandleBrowseSelection(GridGeometry geometry, Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                for (int probe = editLayerCount - 1; probe >= 0; probe--)
                {
                    int hx, hz;
                    if (!IsLayerVisible(probe) || !geometry.RaycastCell(ray, probe, out hx, out hz))
                    {
                        continue;
                    }
                    int wind = FindWindSourceAt(hx, hz, probe);
                    if (wind >= 0)
                    {
                        _selectedWind = wind;
                        _selectedElement = -1;
                        Repaint();
                        e.Use();
                        return;
                    }
                    int hit = FindPlacedElementAt(hx, hz, probe);
                    if (hit >= 0)
                    {
                        _selectedElement = hit;
                        _selectedWind = -1;
                        Repaint();
                        e.Use();
                        return;
                    }
                }
            }
            else if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape
                     && (_selectedWind >= 0 || _selectedElement >= 0))
            {
                _selectedWind = -1;
                _selectedElement = -1;
                Repaint();
                e.Use();
            }
        }

        /// <summary>笔刷工具分流：调色板选的是元素 → 摆放模式；选的是地形 → 连刷模式。</summary>
        private void HandleBrushTool(GridGeometry geometry, Event e, bool onGrid, int hx, int hz)
        {
            MapElementRow elementRow = FindElementRow(brushElementId);
            if (elementRow != null)
            {
                HandleElementPlacement(geometry, e, onGrid, hx, hz);
                return;
            }
            if (_ghost != null)
            {
                DestroyGhost();
            }
            HandleTerrainTool(geometry, e, onGrid, hx, hz);
        }

        private void HandleTerrainTool(GridGeometry geometry, Event e, bool onGrid, int hx, int hz)
        {
            if (onGrid)
            {
                DrawBrushGizmo(geometry, hx, hz);
            }

            bool erase = e.shift;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && onGrid)
                    {
                        BeginStroke();
                        ApplyBrush(geometry, hx, hz, erase);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && !e.alt && onGrid && _strokeUndo != null)
                    {
                        ApplyBrush(geometry, hx, hz, erase);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (e.button == 0 && _strokeUndo != null)
                    {
                        EndStroke();
                        e.Use();
                    }
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Escape)
                    {
                        toolIndex = (int)EditTool.None;
                        Repaint();
                        e.Use();
                    }
                    break;
            }
        }

        // ---------- 元素摆放（同游戏内建造：幽灵悬浮 + 占地校验；只写 JSON，不动任何 Prefab） ----------

        private void HandleElementPlacement(GridGeometry geometry, Event e, bool onGrid, int hx, int hz)
        {
            BuildRuleSet rules = Rules();
            MapElementDef def = rules != null ? rules.GetElementOrNull(brushElementId) : null;
            if (def == null)
            {
                return;
            }

            var rotation = (Rotation)(ghostRotation & 3);
            bool valid = onGrid && CanPlaceElement(geometry, def, hx, hz, rotation);

            if (onGrid)
            {
                // 幽灵跟随鼠标：与运行时同一份摆放口径（PlaceAt），合法性染绿/红
                EnsureGhost(def);
                if (_ghost != null)
                {
                    _ghost.SetActive(true);
                    ModelSpawner.PlaceAt(_ghost, geometry.CellCorner(hx, hz, layer), rotation, def.Footprint, geometry.CellSize);
                    ModelSpawner.ApplyGhostAppearance(
                        _ghost, valid ? new Color(0.35f, 1f, 0.45f, 1f) : new Color(1f, 0.35f, 0.3f, 1f));
                }
                DrawElementGhostRect(geometry, def, hx, hz, rotation, valid);
            }
            else if (_ghost != null)
            {
                _ghost.SetActive(false);
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && onGrid)
                    {
                        int hit = FindPlacedElementAt(hx, hz, layer);
                        if (e.shift)
                        {
                            if (hit >= 0)
                            {
                                RemovePlacedElement(hit);
                            }
                        }
                        else if (hit >= 0)
                        {
                            _selectedElement = hit;
                            _selectedWind = -1;
                            Repaint();
                        }
                        else if (valid)
                        {
                            _elements.Add(new MapElementPlacement(def.ElementId, hx, hz, layer, rotation));
                            _selectedElement = _elements.Count - 1;
                            _selectedWind = -1;
                            MarkElementsDirty();
                        }
                        e.Use();
                    }
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.R)
                    {
                        ghostRotation = (ghostRotation + 1) & 3;
                        Repaint();
                        e.Use();
                    }
                    else if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                    {
                        if (_selectedElement >= 0)
                        {
                            RemovePlacedElement(_selectedElement);
                            e.Use();
                        }
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        if (_selectedElement >= 0)
                        {
                            _selectedElement = -1;
                        }
                        else
                        {
                            toolIndex = (int)EditTool.None;
                            DestroyGhost();
                        }
                        Repaint();
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawElementGhostRect(
            GridGeometry geometry, MapElementDef def, int x, int z, Rotation rotation, bool valid)
        {
            Vector3 corner = geometry.CellCorner(x, z, layer) + new Vector3(0f, 0.05f, 0f);
            float spanX = def.Footprint.SpanX(rotation) * geometry.CellSize;
            float spanZ = def.Footprint.SpanZ(rotation) * geometry.CellSize;
            var verts = new[]
            {
                corner,
                corner + new Vector3(0f, 0f, spanZ),
                corner + new Vector3(spanX, 0f, spanZ),
                corner + new Vector3(spanX, 0f, 0f),
            };
            Color outline = valid ? new Color(0.35f, 1f, 0.45f, 0.9f) : new Color(1f, 0.35f, 0.3f, 0.9f);
            Handles.DrawSolidRectangleWithOutline(verts, new Color(outline.r, outline.g, outline.b, 0.12f), outline);

            Handles.BeginGUI();
            Vector2 screen = HandleUtility.WorldToGUIPoint(corner + new Vector3(spanX * 0.5f, 0f, spanZ * 0.5f));
            GUI.Label(new Rect(screen.x + 10f, screen.y - 10f, 240f, 18f),
                $"({x}, {z}) {def.NameCn} {RotationLabels[(int)rotation]}" + (valid ? string.Empty : "（放不下）"));
            Handles.EndGUI();
        }

        /// <summary>占地合法性：整个 footprint 在网格内，且不压任何已落位元素（含风源）。</summary>
        private bool CanPlaceElement(GridGeometry geometry, MapElementDef def, int x, int z, Rotation rotation)
        {
            def.Footprint.GetCells(x, z, rotation, _cellScratch);
            for (int i = 0; i < _cellScratch.Count; i++)
            {
                if (!geometry.Contains(_cellScratch[i].X, _cellScratch[i].Z))
                {
                    return false;
                }
            }

            BuildRuleSet rules = Rules();
            var occupied = new HashSet<long>();
            for (int i = 0; i < _cellScratch.Count; i++)
            {
                occupied.Add(CellKey(_cellScratch[i].X, _cellScratch[i].Z));
            }

            var otherCells = new List<CellCoord>(16);
            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (el.Layer != layer)
                {
                    continue;
                }
                MapElementDef otherDef = rules != null ? rules.GetElementOrNull(el.ElementId) : null;
                if (otherDef == null || otherDef.IsTerrain)
                {
                    continue;
                }
                otherDef.Footprint.GetCells(el.X, el.Z, el.Rotation, otherCells);
                for (int c = 0; c < otherCells.Count; c++)
                {
                    if (occupied.Contains(CellKey(otherCells[c].X, otherCells[c].Z)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>该格被哪个已落位的非风源元素占着（按 footprint 判定，不只锚点格）。</summary>
        private int FindPlacedElementAt(int x, int z, int searchLayer)
        {
            BuildRuleSet rules = Rules();
            if (rules == null)
            {
                return -1;
            }
            var cells = new List<CellCoord>(16);
            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (el.Layer != searchLayer || IsWindSource(el))
                {
                    continue;
                }
                MapElementDef def = rules.GetElementOrNull(el.ElementId);
                if (def == null || def.IsTerrain)
                {
                    continue;
                }
                def.Footprint.GetCells(el.X, el.Z, el.Rotation, cells);
                for (int c = 0; c < cells.Count; c++)
                {
                    if (cells[c].X == x && cells[c].Z == z)
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private static long CellKey(int x, int z)
        {
            return ((long)z << 32) | (uint)x;
        }

        private void RemovePlacedElement(int index)
        {
            if (index < 0 || index >= _elements.Count || IsWindSource(_elements[index]))
            {
                return;
            }
            RemoveElementAt(index);
            MarkElementsDirty();
        }

        /// <summary>统一的删除入口：两个选中下标都要修正（删除会让后面的下标整体前移）。</summary>
        private void RemoveElementAt(int index)
        {
            _elements.RemoveAt(index);
            _selectedWind = FixupIndex(_selectedWind, index);
            _selectedElement = FixupIndex(_selectedElement, index);
        }

        private static int FixupIndex(int selection, int removed)
        {
            if (selection == removed)
            {
                return -1;
            }
            return selection > removed ? selection - 1 : selection;
        }

        /// <summary>元素列表变了：模型参考、风预览（风流下标映射会因删除移位）一起重建。</summary>
        private void MarkElementsDirty()
        {
            _dirty = true;
            GridGeometry geometry;
            if (TryGetGeometry(out geometry))
            {
                RebuildElementModels(geometry);
            }
            RebuildWindPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        private void EnsureGhost(MapElementDef def)
        {
            if (_ghost != null && _ghostElementId == def.ElementId)
            {
                return;
            }
            DestroyGhost();
            _ghostElementId = def.ElementId;

            GameObject prefab = string.IsNullOrEmpty(def.PrefabPath) ? null : Resources.Load<GameObject>(def.PrefabPath);
            if (prefab == null)
            {
                return; // 没模型的元素只画占地框
            }

            _ghost = Object.Instantiate(prefab);
            _ghost.name = GhostName;
            foreach (Transform node in _ghost.GetComponentsInChildren<Transform>(true))
            {
                node.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                Object.DestroyImmediate(_ghost);
                _ghost = null;
            }
            _ghostElementId = null;
        }

        private void HandleWindTool(GridGeometry geometry, Event e, bool onGrid, int hx, int hz)
        {
            if (onGrid)
            {
                Vector3 corner = geometry.CellCorner(hx, hz, layer) + new Vector3(0f, 0.05f, 0f);
                float span = geometry.CellSize;
                var verts = new[]
                {
                    corner,
                    corner + new Vector3(0f, 0f, span),
                    corner + new Vector3(span, 0f, span),
                    corner + new Vector3(span, 0f, 0f),
                };
                int hovered = FindWindSourceAt(hx, hz, layer);
                Color outline = e.shift
                    ? new Color(1f, 0.4f, 0.4f, 0.9f)
                    : (hovered >= 0 ? new Color(1f, 0.9f, 0.3f, 0.9f) : new Color(0.4f, 0.8f, 1f, 0.9f));
                Handles.DrawSolidRectangleWithOutline(verts, new Color(outline.r, outline.g, outline.b, 0.12f), outline);

                Handles.BeginGUI();
                Vector2 screen = HandleUtility.WorldToGUIPoint(corner + new Vector3(span * 0.5f, 0f, span * 0.5f));
                string label = e.shift ? "删除风源" : (hovered >= 0 ? "选中风源" : "放置风源");
                GUI.Label(new Rect(screen.x + 10f, screen.y - 10f, 220f, 18f), $"({hx}, {hz}) {label}");
                Handles.EndGUI();
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && onGrid)
                    {
                        int hit = FindWindSourceAt(hx, hz, layer);
                        if (e.shift)
                        {
                            if (hit >= 0)
                            {
                                RemoveWindSource(hit);
                            }
                        }
                        else if (hit >= 0)
                        {
                            _selectedWind = hit;
                            _selectedElement = -1;
                            Repaint();
                        }
                        else
                        {
                            PlaceWindSource(hx, hz);
                        }
                        e.Use();
                    }
                    break;

                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
                    {
                        if (_selectedWind >= 0)
                        {
                            RemoveWindSource(_selectedWind);
                            e.Use();
                        }
                    }
                    else if (e.keyCode == KeyCode.Escape)
                    {
                        if (_selectedWind >= 0)
                        {
                            _selectedWind = -1;
                        }
                        else
                        {
                            toolIndex = (int)EditTool.None;
                        }
                        Repaint();
                        e.Use();
                    }
                    break;
            }
        }

        private void DrawBrushGizmo(GridGeometry geometry, int centerX, int centerZ)
        {
            int half = brushSize / 2;
            int minX = centerX - half;
            int minZ = centerZ - half;
            Vector3 corner = geometry.CellCorner(minX, minZ, layer) + new Vector3(0f, 0.05f, 0f);
            float span = geometry.CellSize * brushSize;

            var verts = new[]
            {
                corner,
                corner + new Vector3(0f, 0f, span),
                corner + new Vector3(span, 0f, span),
                corner + new Vector3(span, 0f, 0f),
            };

            bool erase = Event.current.shift;
            Color fill = erase ? new Color(1f, 0.3f, 0.3f, 0.15f) : new Color(1f, 1f, 1f, 0.15f);
            Color outline = erase ? new Color(1f, 0.4f, 0.4f, 0.9f) : new Color(1f, 1f, 1f, 0.9f);
            Handles.DrawSolidRectangleWithOutline(verts, fill, outline);

            Handles.BeginGUI();
            Vector2 screen = HandleUtility.WorldToGUIPoint(corner + new Vector3(span * 0.5f, 0f, span * 0.5f));
            string label = erase ? "擦除" : brushElementId;
            GUI.Label(new Rect(screen.x + 10f, screen.y - 10f, 220f, 18f), $"({centerX}, {centerZ}) {label}");
            Handles.EndGUI();
        }

        // ---------- 参考层绘制（元素占地框 + 风预览） ----------

        /// <summary>非地形元素（矿藏/锚点/巨型风车）的占地框与名字。风源另有专门的标记。</summary>
        private void DrawElementOverlays(GridGeometry geometry)
        {
            BuildRuleSet rules = Rules();
            if (rules == null)
            {
                return;
            }

            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (IsWindSource(el) || !IsLayerVisible(el.Layer))
                {
                    continue;
                }
                MapElementDef def = rules.GetElementOrNull(el.ElementId);
                if (def == null || def.IsTerrain)
                {
                    continue;
                }

                Vector3 corner = geometry.CellCorner(el.X, el.Z, el.Layer) + new Vector3(0f, OutlineY, 0f);
                float spanX = def.Footprint.SpanX(el.Rotation) * geometry.CellSize;
                float spanZ = def.Footprint.SpanZ(el.Rotation) * geometry.CellSize;
                var verts = new[]
                {
                    corner,
                    corner + new Vector3(0f, 0f, spanZ),
                    corner + new Vector3(spanX, 0f, spanZ),
                    corner + new Vector3(spanX, 0f, 0f),
                };

                Color color = i == _selectedElement
                    ? new Color(1f, 0.9f, 0.2f, 1f)
                    : ElementColor(el.ElementId);
                Handles.DrawSolidRectangleWithOutline(
                    verts, new Color(color.r, color.g, color.b, i == _selectedElement ? 0.18f : 0.08f), color);

                Handles.BeginGUI();
                Vector2 screen = HandleUtility.WorldToGUIPoint(corner + new Vector3(spanX * 0.5f, 0f, spanZ * 0.5f));
                GUI.Label(new Rect(screen.x - 40f, screen.y - 8f, 120f, 16f), def.NameCn, EditorStyles.whiteMiniLabel);
                Handles.EndGUI();
            }
        }

        private static Color ElementColor(string elementId)
        {
            switch (elementId)
            {
                case "ore": return new Color(1f, 0.6f, 0.1f, 0.9f);
                case "anchor": return new Color(0.2f, 0.9f, 0.9f, 0.9f);
                case "giantWindmill": return new Color(0.8f, 0.5f, 1f, 0.9f);
                default: return new Color(0.8f, 0.8f, 0.8f, 0.9f);
            }
        }

        /// <summary>风源标记 + 手工风的传播路径（真实 <see cref="WindSimulator"/> 结果）。</summary>
        private void DrawWindOverlays(GridGeometry geometry)
        {
            BuildRuleSet rules = Rules();
            int maxLevel = rules != null ? rules.MaxWindLevel : 5;

            // 每个风源一个标记（只画勾选层上的）
            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (!IsWindSource(el) || !IsLayerVisible(el.Layer))
                {
                    continue;
                }

                Vector3 center = geometry.CellCenter(el.X, el.Z, el.Layer) + new Vector3(0f, WindMarkerY, 0f);
                float radius = geometry.CellSize * 0.55f;
                bool selected = i == _selectedWind;

                Color color = el.HasWindParams
                    ? WindColor(el.WindForce, maxLevel)
                    : new Color(0.6f, 0.6f, 0.6f, 0.9f);
                Handles.color = color;
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.up, radius * 0.15f);

                if (el.HasWindParams)
                {
                    Vector3 dir = DirVector(el.WindDir);
                    Handles.ArrowHandleCap(0, center,
                        Quaternion.LookRotation(dir), geometry.CellSize * 1.4f, EventType.Repaint);
                }

                if (selected)
                {
                    Handles.color = new Color(1f, 0.9f, 0.2f, 1f);
                    Handles.DrawWireDisc(center, Vector3.up, radius * 1.25f);
                }

                Handles.BeginGUI();
                Vector2 screen = HandleUtility.WorldToGUIPoint(center);
                string label = el.HasWindParams
                    ? $"风源 {DirLabels[Mathf.Clamp(el.WindDir, 0, 7)]}  力{el.WindForce} 长{el.WindLength}"
                    : "风源（运行时随机）";
                GUI.Label(new Rect(screen.x + 12f, screen.y - 24f, 220f, 18f), label, EditorStyles.whiteMiniLabel);
                Handles.EndGUI();
            }

            // 手工风的传播路径
            if (_windPreview == null)
            {
                return;
            }
            IReadOnlyList<WindStream> streams = _windPreview.Streams;
            for (int s = 0; s < streams.Count; s++)
            {
                WindStream stream = streams[s];
                if (!IsLayerVisible(stream.Layer))
                {
                    continue;
                }
                bool selected = s < _windStreamElementIndex.Count && _windStreamElementIndex[s] == _selectedWind;

                var points = new Vector3[stream.Path.Count];
                for (int p = 0; p < stream.Path.Count; p++)
                {
                    WindPathStep step = stream.Path[p];
                    points[p] = geometry.CellCenter(step.Cell.X, step.Cell.Z, stream.Layer)
                                + new Vector3(0f, WindPathY, 0f);
                }

                Handles.color = selected ? new Color(1f, 0.9f, 0.2f, 1f) : WindColor(stream.Force, maxLevel);
                Handles.DrawAAPolyLine(selected ? 7f : 4f, points);

                // 末端箭头：沿最后一步的出向
                if (stream.Path.Count > 0)
                {
                    WindPathStep last = stream.Path[stream.Path.Count - 1];
                    Vector3 dir = DirVector((int)last.DirOut);
                    Vector3 tip = points[points.Length - 1] + dir * geometry.CellSize * 0.4f;
                    Handles.ConeHandleCap(0, tip, Quaternion.LookRotation(dir), geometry.CellSize * 0.7f, EventType.Repaint);
                }
            }
        }

        private static Color WindColor(int force, int maxLevel)
        {
            float t = maxLevel <= 1 ? 1f : Mathf.Clamp01((force - 1f) / (maxLevel - 1f));
            return Color.Lerp(new Color(0.55f, 0.85f, 1f, 0.95f), new Color(0.1f, 0.35f, 1f, 0.95f), t);
        }

        private static Vector3 DirVector(int dir)
        {
            int d = dir & 7;
            return new Vector3(WindMath.StepX[d], 0f, WindMath.StepZ[d]).normalized;
        }

        // ---------- 风源编辑 ----------

        private static bool IsWindSource(MapElementPlacement el)
        {
            return string.Equals(el.ElementId, BuildRuleSet.WindSourceElementId, StringComparison.Ordinal);
        }

        private int FindWindSourceAt(int x, int z, int searchLayer)
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (IsWindSource(el) && el.X == x && el.Z == z && el.Layer == searchLayer)
                {
                    return i;
                }
            }
            return -1;
        }

        private int DefaultWindForce(BuildRuleSet rules)
        {
            if (rules == null)
            {
                return FallbackWindForce;
            }
            int mid = (rules.InitialWindLevelMin + rules.InitialWindLevelMax) / 2;
            return Mathf.Clamp(mid, 1, rules.MaxWindLevel);
        }

        private int DefaultWindLength(BuildRuleSet rules)
        {
            if (rules == null)
            {
                return FallbackWindLength;
            }
            return Mathf.Max(1, (rules.InitialWindLengthMin + rules.InitialWindLengthMax) / 2);
        }

        private void PlaceWindSource(int x, int z)
        {
            BuildRuleSet rules = Rules();
            _elements.Add(new MapElementPlacement(
                BuildRuleSet.WindSourceElementId, x, z, layer, Rotation.Deg0,
                (int)Dir8.E, DefaultWindForce(rules), DefaultWindLength(rules)));
            _selectedWind = _elements.Count - 1;
            _selectedElement = -1;
            MarkWindDirty();
        }

        private void RemoveWindSource(int index)
        {
            if (index < 0 || index >= _elements.Count || !IsWindSource(_elements[index]))
            {
                return;
            }
            RemoveElementAt(index);
            MarkWindDirty();
        }

        private void MarkWindDirty()
        {
            _dirty = true;
            RebuildWindPreview();
            Repaint();
            SceneView.RepaintAll();
        }

        /// <summary>用运行时同一份 <see cref="WindSimulator"/> 重算手工风的传播——预览即真相。</summary>
        private void RebuildWindPreview()
        {
            _windPreview = null;
            _windStreamElementIndex.Clear();

            GridGeometry geometry;
            if (!TryGetGeometry(out geometry))
            {
                return;
            }
            BuildRuleSet rules = Rules();
            if (rules == null)
            {
                return;
            }

            var seeds = new List<WindSeed>(4);
            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (!IsWindSource(el) || !el.HasWindParams || !geometry.Contains(el.X, el.Z))
                {
                    continue;
                }
                seeds.Add(new WindSeed(new CellCoord(el.X, el.Z), el.Layer, (Dir8)el.WindDir, el.WindForce, el.WindLength));
                _windStreamElementIndex.Add(i);
            }
            if (seeds.Count == 0)
            {
                return;
            }

            MapSnapshot snapshot;
            try
            {
                snapshot = BuildSnapshot(geometry);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[地图编辑器] 风预览跳过（快照非法，通常是场景网格与存档尺寸不一致）：" + e.Message);
                return;
            }

            try
            {
                _windPreview = WindSimulator.Recompute(snapshot, seeds, NoBuildings.Instance, rules);
            }
            catch (Exception e)
            {
                Debug.LogError("[地图编辑器] 风预览模拟失败：" + e.Message);
            }
        }

        /// <summary>编辑器里没有任何建筑：风帆/物流点查询恒为空。</summary>
        private sealed class NoBuildings : IWindBuildingQuery
        {
            public static readonly NoBuildings Instance = new NoBuildings();

            public bool TryGetSailTurn(int x, int z, int layer, out TurnDir turn)
            {
                turn = TurnDir.Left;
                return false;
            }

            public bool TryGetLogisticsPoint(int x, int z, int layer, out int pointId)
            {
                pointId = 0;
                return false;
            }
        }

        // ---------- 刷 / 擦 ----------

        private void ApplyBrush(GridGeometry geometry, int centerX, int centerZ, bool erase)
        {
            if (!erase && string.IsNullOrEmpty(brushElementId))
            {
                return;
            }

            int half = brushSize / 2;
            bool changed = false;

            for (int dz = 0; dz < brushSize; dz++)
            {
                for (int dx = 0; dx < brushSize; dx++)
                {
                    int x = centerX - half + dx;
                    int z = centerZ - half + dz;
                    if (!geometry.Contains(x, z))
                    {
                        continue; // 笔刷压到网格外的部分直接丢弃，不越界写数据
                    }

                    var key = new Vector3Int(x, z, layer);
                    string current;
                    bool has = _painted.TryGetValue(key, out current);

                    if (erase)
                    {
                        if (!has) continue;
                        RecordUndo(key, current);
                        _painted.Remove(key);
                        changed = true;
                    }
                    else
                    {
                        if (has && current == brushElementId) continue;
                        RecordUndo(key, has ? current : null);
                        _painted[key] = brushElementId;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                MarkDirtyAndRefresh();
            }
        }

        private void BeginStroke()
        {
            _strokeUndo = new Dictionary<Vector3Int, string>();
        }

        private void EndStroke()
        {
            if (_strokeUndo != null && _strokeUndo.Count > 0)
            {
                _undoStack.Add(_strokeUndo);
                // 撤销栈封顶，防长时间刷图吃内存
                if (_undoStack.Count > 64)
                {
                    _undoStack.RemoveAt(0);
                }
            }
            _strokeUndo = null;
            Repaint();
        }

        /// <summary>只记这一笔里每个格子的**首次**原值，同一笔反复经过同一格不覆盖。</summary>
        private void RecordUndo(Vector3Int key, string previous)
        {
            if (_strokeUndo != null && !_strokeUndo.ContainsKey(key))
            {
                _strokeUndo[key] = previous;
            }
        }

        private void UndoStroke()
        {
            if (_undoStack.Count == 0)
            {
                return;
            }
            Dictionary<Vector3Int, string> last = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);

            foreach (KeyValuePair<Vector3Int, string> kv in last)
            {
                if (kv.Value == null)
                {
                    _painted.Remove(kv.Key);
                }
                else
                {
                    _painted[kv.Key] = kv.Value;
                }
            }
            MarkDirtyAndRefresh();
        }

        // ---------- 预览刷新 ----------

        private void MarkDirtyAndRefresh()
        {
            _dirty = true;
            RefreshOverlay();
            Repaint();
        }

        /// <summary>把当前编辑内容推给场景里的 TerrainOverlayRenderer（与运行时同一个组件；只画勾选的层）。</summary>
        private void RefreshOverlay()
        {
            GridGeometry geometry;
            if (_overlay == null || !TryGetGeometry(out geometry))
            {
                return;
            }
            try
            {
                _overlay.Rebuild(BuildDisplaySnapshot(geometry), geometry);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[地图编辑器] 地形预览刷新失败（通常是场景网格与存档尺寸不一致，先点上面的调整按钮）：" + e.Message);
                return;
            }
            SceneView.RepaintAll();
        }

        /// <summary>当前编辑内容 → 快照（地形 + 元素一并带上；坐标越界会抛，调用方兜住提示）。存盘/风模拟用，不过滤显隐。</summary>
        private MapSnapshot BuildSnapshot(GridGeometry geometry)
        {
            EnsureLayerListSize();
            var cells = new List<MapCell>(_painted.Count);
            foreach (KeyValuePair<Vector3Int, string> kv in _painted)
            {
                cells.Add(new MapCell(kv.Key.x, kv.Key.y, kv.Key.z, kv.Value));
            }
            return new MapSnapshot(
                stageId, geometry.Width, geometry.Length, editLayerCount, cells, _elements);
        }

        /// <summary>显示用快照：只含勾选层的地形（overlay 只画这份；元素不进来，overlay 不用它们）。</summary>
        private MapSnapshot BuildDisplaySnapshot(GridGeometry geometry)
        {
            EnsureLayerListSize();
            var cells = new List<MapCell>(_painted.Count);
            foreach (KeyValuePair<Vector3Int, string> kv in _painted)
            {
                if (IsLayerVisible(kv.Key.z))
                {
                    cells.Add(new MapCell(kv.Key.x, kv.Key.y, kv.Key.z, kv.Value));
                }
            }
            return new MapSnapshot(stageId, geometry.Width, geometry.Length, editLayerCount, cells);
        }

        // ---------- 岛屿 / 元素参考实例 ----------

        /// <summary>
        /// 实例化对应 Stage 的岛屿 Prefab 并用 <see cref="IslandFitter"/> 缩放对位——格子随即与岛面对齐。
        /// 整棵树 HideAndDontSave：不进 Hierarchy、不存场景、NotEditable，编辑器永远动不到 Prefab 资产。
        /// </summary>
        private void LoadIslandReference(GridGeometry geometry)
        {
            DestroyIsland();
            _islandStatus = null;

            StageRow stage = EnsureTables() ? Tables.Stage.GetOrNull(stageId) : null;

            // 下拉里选了就用选的；没选（按 Stage 表）才回落到配表 prefabPath
            string prefabPath = !string.IsNullOrEmpty(islandPrefabOverride)
                ? islandPrefabOverride
                : (stage != null ? stage.prefabPath : null);

            if (string.IsNullOrEmpty(prefabPath))
            {
                _islandStatus = stage == null
                    ? $"Stage 表里没有第 {stageId} 关，也没在下拉里选 Prefab"
                    : "Stage.prefabPath 为空，可在下拉里直接选一个 Prefab";
                return;
            }
            var prefab = Resources.Load<GameObject>(prefabPath);
            if (prefab == null)
            {
                _islandStatus = $"Resources 里没有 '{prefabPath}'（跑一次 Tools/美术/生成白模 Prefab？）";
                return;
            }

            _island = Object.Instantiate(prefab);
            _island.name = IslandName;
            foreach (Transform node in _island.GetComponentsInChildren<Transform>(true))
            {
                node.gameObject.hideFlags = HideFlags.HideAndDontSave;
            }

            // 跨度仍按本关配表（IslandFitter 对 0 会退默认值）
            int cellSpan = stage != null ? stage.islandCellSpan : 0;
            Dictionary<Vector3Int, float> heights = IslandFitter.Fit(_island, geometry, cellSpan);
            _islandStatus = heights.Count > 0
                ? $"'{prefabPath}'，跨度 {cellSpan} 格，平台面 y={IslandFitter.SurfaceY:0.##}"
                : $"'{prefabPath}' 已加载但一格都没测到高度（模型缺 MeshCollider？）";
            SceneView.RepaintAll();
        }

        /// <summary>把非地形元素的模型实例出来当参考（与运行时同一份摆放口径 <see cref="ModelSpawner.PlaceAt"/>）。</summary>
        private void RebuildElementModels(GridGeometry geometry)
        {
            DestroyElementModels();
            BuildRuleSet rules = Rules();
            if (rules == null || _elements.Count == 0)
            {
                return;
            }

            _elementRoot = new GameObject(ElementsName) { hideFlags = HideFlags.HideAndDontSave };

            int spawned = 0;
            for (int i = 0; i < _elements.Count; i++)
            {
                MapElementPlacement el = _elements[i];
                if (!IsLayerVisible(el.Layer))
                {
                    continue; // 没勾选的层连模型一起藏
                }
                MapElementDef def = rules.GetElementOrNull(el.ElementId);
                if (def == null || def.IsTerrain || string.IsNullOrEmpty(def.PrefabPath))
                {
                    continue; // 风源等无独立模型的元素靠 Scene 视图标记表现
                }
                var prefab = Resources.Load<GameObject>(def.PrefabPath);
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = Object.Instantiate(prefab, _elementRoot.transform);
                instance.name = $"{el.ElementId}_{i}";
                ModelSpawner.PlaceAt(
                    instance, geometry.CellCorner(el.X, el.Z, el.Layer), el.Rotation, def.Footprint, geometry.CellSize);
                foreach (Transform node in instance.GetComponentsInChildren<Transform>(true))
                {
                    node.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }
                spawned++;
            }
            SceneView.RepaintAll();
        }

        private void DestroyIsland()
        {
            if (_island != null)
            {
                Object.DestroyImmediate(_island);
                _island = null;
            }
        }

        private void DestroyElementModels()
        {
            if (_elementRoot != null)
            {
                Object.DestroyImmediate(_elementRoot);
                _elementRoot = null;
            }
        }

        /// <summary>域重载后残留的 HideAndDontSave 孤儿按名字清一遍（引用已丢，只能这么找）。</summary>
        private static void DestroyStaleByName(string name)
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go != null && go.name == name && !EditorUtility.IsPersistent(go)
                    && go.transform.parent == null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        // ---------- 存读 ----------

        private static string AssetPath(int stageId)
        {
            return $"{MapsFolder}/stage_{stageId}.json";
        }

        private void Save()
        {
            GridGeometry geometry;
            if (!TryGetGeometry(out geometry))
            {
                return;
            }

            MapSnapshot snapshot;
            try
            {
                snapshot = BuildSnapshot(geometry);
            }
            catch (Exception e)
            {
                Debug.LogError("[地图编辑器] 保存失败（数据越界？先把场景网格调成与存档一致再存）：" + e.Message);
                return;
            }

            if (!Directory.Exists(MapsFolder))
            {
                Directory.CreateDirectory(MapsFolder);
            }

            string path = AssetPath(stageId);
            File.WriteAllText(path, MapJson.Save(snapshot));
            AssetDatabase.ImportAsset(path);

            _dirty = false;
            RefreshFileLists(); // 新建的图立刻进文件下拉
            Debug.Log($"[地图编辑器] 第 {stageId} 关已保存：{editLayerCount} 层，地形 {_painted.Count} 格 + 元素 {_elements.Count} 个（含风源） → {path}");
        }

        private bool Load()
        {
            string path = AssetPath(stageId);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("地图编辑器", $"文件不存在：{path}", "知道了");
                return false;
            }

            if (_dirty && !EditorUtility.DisplayDialog("地图编辑器",
                    $"当前有未保存的改动（地形 {_painted.Count} 格 / 元素 {_elements.Count} 个），载入会丢弃它们。继续？", "载入", "取消"))
            {
                return false;
            }

            MapSnapshot snapshot = MapJson.Load($"stage_{stageId}", File.ReadAllText(path));

            _painted.Clear();
            _undoStack.Clear();
            _selectedWind = -1;
            _selectedElement = -1;

            // 层数以存档为准，显隐全部重置为可见
            editLayerCount = Mathf.Max(1, snapshot.LayerCount);
            layerVisible.Clear();
            for (int i = 0; i < editLayerCount; i++)
            {
                layerVisible.Add(true);
            }
            layer = Mathf.Clamp(layer, 0, editLayerCount - 1);

            foreach (MapCell cell in snapshot.Cells)
            {
                _painted[new Vector3Int(cell.X, cell.Z, cell.Layer)] = cell.ElementId;
            }

            _elements.Clear();
            for (int i = 0; i < snapshot.Elements.Count; i++)
            {
                _elements.Add(snapshot.Elements[i]);
            }

            GridGeometry geometry;
            bool hasGeometry = TryGetGeometry(out geometry);
            if (hasGeometry && (snapshot.Width != geometry.Width || snapshot.Length != geometry.Length))
            {
                Debug.LogWarning($"[地图编辑器] 存档是 {snapshot.Width}×{snapshot.Length}，当前场景网格是 {geometry.Width}×{geometry.Length}。" +
                                 "超出场景网格的地块仍在数据里，但编辑器改不到它们——建议先把场景网格调成一致再编辑。");
            }

            _dirty = false;
            if (hasGeometry)
            {
                LoadIslandReference(geometry);
                RebuildElementModels(geometry);
            }
            RebuildWindPreview();
            RefreshOverlay();
            Repaint();

            int windCount = 0;
            for (int i = 0; i < _elements.Count; i++)
            {
                if (IsWindSource(_elements[i]))
                {
                    windCount++;
                }
            }
            Debug.Log($"[地图编辑器] 第 {stageId} 关已载入：{editLayerCount} 层，地形 {_painted.Count} 格 + 元素 {_elements.Count} 个（风源 {windCount}） ← {path}");
            return true;
        }

        private void ResizeSceneGrid(int width, int height)
        {
            var so = new SerializedObject(_grid);
            SerializedProperty w = so.FindProperty("gridWidth");
            SerializedProperty l = so.FindProperty("gridLength");
            if (w == null || l == null)
            {
                Debug.LogWarning("[地图编辑器] EasyGridBuilderPro 上找不到 gridWidth/gridLength（插件版本变了？）。", _grid);
                return;
            }
            w.intValue = width;
            l.intValue = height;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_grid);

            // 网格变了 → 对位、模型、风预览全部重来
            GridGeometry geometry;
            if (TryGetGeometry(out geometry))
            {
                if (_island != null)
                {
                    LoadIslandReference(geometry);
                }
                RebuildElementModels(geometry);
            }
            RebuildWindPreview();
            RefreshOverlay();
        }
    }
}
