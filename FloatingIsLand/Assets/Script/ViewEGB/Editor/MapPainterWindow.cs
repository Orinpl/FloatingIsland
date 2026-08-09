using System.Collections.Generic;
using System.IO;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.View;
using SoulGames.EasyGridBuilderPro;
using UnityEditor;
using UnityEngine;

namespace FloatingIsLand.ViewEGB.EditorTools
{
    /// <summary>
    /// 地形刷子：在 Scene 视图里手刷关卡地形，存成 Resources/Maps/stage_{id}.json（稀疏，只存刷过的格）。
    ///
    /// 为什么在 ViewEGB/Editor 而不是 Assets/Tool/Editor：刷子必须读 EGB 组件的
    /// gridWidth/gridLength/cellSize/gridOriginType 才能与运行时网格严丝合缝对齐，而
    /// using SoulGames 只允许出现在 ViewEGB/（GRID_INTEGRATION §4）。
    ///
    /// 坐标换算不走 IGridPresenter——EGB 的 GetCellWorldPosition 依赖运行时才建的 gridList，
    /// 编辑器态是 null。统一走 <see cref="GridGeometry"/>（它精确复刻了 EGB 的公式）。
    ///
    /// 落笔预览直接驱动场景里的 <see cref="TerrainOverlayRenderer"/>——与运行时是同一个组件、
    /// 同一段建 Mesh 代码，所见即所得。
    /// </summary>
    public sealed class MapPainterWindow : EditorWindow
    {
        private const string MapsFolder = "Assets/Resources/Maps";
        private const int MaxBrushSize = 15;

        [SerializeField] private int stageId = 1;
        [SerializeField] private int layer;
        [SerializeField] private int brushSize = 1;
        [SerializeField] private string brushElementId;
        [SerializeField] private bool paintingEnabled;

        /// <summary>编辑中的地图：(x, z, layer) → elementId。只有这里有的格子才会存盘。</summary>
        private readonly Dictionary<Vector3Int, string> _painted = new Dictionary<Vector3Int, string>();

        /// <summary>笔画级撤销栈：每次按下到抬起算一笔，存这笔改动前的原值（null = 原本没刷过）。</summary>
        private readonly List<Dictionary<Vector3Int, string>> _undoStack = new List<Dictionary<Vector3Int, string>>();
        private Dictionary<Vector3Int, string> _strokeUndo;

        private List<MapElementRow> _terrainRows;
        private EasyGridBuilderPro _grid;
        private TerrainOverlayRenderer _overlay;
        private bool _dirty;
        private Vector2 _scroll;

        [MenuItem("Tools/地图/地形刷子", false, 3)]
        public static void Open()
        {
            GetWindow<MapPainterWindow>("地形刷子").Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            RefreshSceneRefs();
            RefreshTerrainRows();
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        // ---------- 场景/配表引用 ----------

        private void RefreshSceneRefs()
        {
            _grid = FindObjectOfType<EasyGridBuilderPro>();
            _overlay = FindObjectOfType<TerrainOverlayRenderer>();
        }

        private void RefreshTerrainRows()
        {
            _terrainRows = new List<MapElementRow>();
            if (!TableLoader.IsLoaded)
            {
                try
                {
                    UnityTableLoader.LoadFromResources();
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[地形刷子] 配表加载失败，调色板会是空的：" + e.Message);
                    return;
                }
            }

            // 调色板只列 isTerrain=TRUE 的行（1×1 区域填充地形）。
            // ore/anchor/giantWindmill 是多格元素、windSource 是点元素，都走摆放流程不走刷子。
            foreach (MapElementRow row in Tables.MapElement.All)
            {
                if (row.isTerrain)
                {
                    _terrainRows.Add(row);
                }
            }

            if (string.IsNullOrEmpty(brushElementId) && _terrainRows.Count > 0)
            {
                brushElementId = _terrainRows[0].elementId;
            }
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
            DrawBrushSection(geometry);
            EditorGUILayout.Space();
            DrawFileSection();

            EditorGUILayout.EndScrollView();
        }

        private void DrawSetupHelp()
        {
            if (_grid == null)
            {
                EditorGUILayout.HelpBox(
                    "当前场景里没有 EGB 网格系统。\n请先打开 Main 场景，跑 Tools → 框架 → 给 Main 场景接入 EGB 网格。",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "场景里没有 TerrainOverlayRenderer（地形渲染 + 落笔预览都靠它）。\n重跑 Tools → 框架 → 给 Main 场景接入 EGB 网格 即可补上。",
                    MessageType.Warning);
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

            int layerCount = _grid.GetVerticalGridsCount();
            using (new EditorGUI.DisabledScope(layerCount <= 1))
            {
                layer = EditorGUILayout.IntSlider("高度层", Mathf.Clamp(layer, 0, layerCount - 1), 0, Mathf.Max(0, layerCount - 1));
            }
            if (layerCount <= 1)
            {
                EditorGUILayout.LabelField(" ", "网格只有 1 层（风跨层规则未定，GRID_INTEGRATION §7）", EditorStyles.miniLabel);
            }
        }

        private void DrawBrushSection(GridGeometry geometry)
        {
            EditorGUILayout.LabelField("刷子", EditorStyles.boldLabel);

            paintingEnabled = EditorGUILayout.ToggleLeft(
                paintingEnabled ? "刷子已启用（Scene 视图左键刷 / Shift+左键擦）" : "启用刷子",
                paintingEnabled);

            if (_terrainRows == null || _terrainRows.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "配表 MapElement 里没有 isTerrain=TRUE 的行，调色板是空的。\n改 Tables/FloatingIsland.xlsx 后跑 Tools → 配表 → 转表。",
                    MessageType.Warning);
                if (GUILayout.Button("重新读取配表"))
                {
                    RefreshTerrainRows();
                }
                return;
            }

            foreach (MapElementRow row in _terrainRows)
            {
                bool selected = row.elementId == brushElementId;
                var content = new GUIContent($"{row.nameCn}  ({row.elementId})");
                if (GUILayout.Toggle(selected, content, EditorStyles.miniButton) && !selected)
                {
                    brushElementId = row.elementId;
                }
            }

            brushSize = EditorGUILayout.IntSlider("笔刷边长（格）", Mathf.Clamp(brushSize, 1, MaxBrushSize), 1, MaxBrushSize);

            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("已刷地块", $"{_painted.Count} 格" + (_dirty ? "（未保存）" : string.Empty));

            using (new EditorGUI.DisabledScope(_undoStack.Count == 0))
            {
                if (GUILayout.Button($"撤销上一笔（{_undoStack.Count}）"))
                {
                    UndoStroke();
                }
            }
        }

        private void DrawFileSection()
        {
            EditorGUILayout.LabelField("存读", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("文件", AssetPath(stageId), EditorStyles.miniLabel);

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
                if (GUILayout.Button("清空"))
                {
                    if (EditorUtility.DisplayDialog("地形刷子", $"清空第 {stageId} 关当前编辑的全部 {_painted.Count} 个地块？（不影响已保存的文件）", "清空", "取消"))
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
            if (!paintingEnabled || _grid == null)
            {
                return;
            }

            GridGeometry geometry;
            if (!TryGetGeometry(out geometry))
            {
                return;
            }

            // 吃掉默认的点选/框选，否则左键会去选场景物体而不是刷格子
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            HandleUtility.AddDefaultControl(controlId);

            Event e = Event.current;
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            int hx, hz;
            bool onGrid = geometry.RaycastCell(ray, layer, out hx, out hz);

            if (onGrid)
            {
                DrawBrushGizmo(geometry, hx, hz);
            }

            bool erase = e.shift;
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && onGrid)
                    {
                        BeginStroke();
                        ApplyBrush(geometry, hx, hz, erase);
                        e.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (e.button == 0 && onGrid && _strokeUndo != null)
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
                        paintingEnabled = false;
                        Repaint();
                        e.Use();
                    }
                    break;
            }

            sceneView.Repaint();
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

        // ---------- 预览 ----------

        private void MarkDirtyAndRefresh()
        {
            _dirty = true;
            RefreshOverlay();
            Repaint();
        }

        /// <summary>把当前编辑内容推给场景里的 TerrainOverlayRenderer（与运行时同一个组件）。</summary>
        private void RefreshOverlay()
        {
            GridGeometry geometry;
            if (_overlay == null || !TryGetGeometry(out geometry))
            {
                return;
            }
            _overlay.Rebuild(BuildSnapshot(geometry), geometry);
            SceneView.RepaintAll();
        }

        private MapSnapshot BuildSnapshot(GridGeometry geometry)
        {
            var cells = new List<MapCell>(_painted.Count);
            foreach (KeyValuePair<Vector3Int, string> kv in _painted)
            {
                cells.Add(new MapCell(kv.Key.x, kv.Key.y, kv.Key.z, kv.Value));
            }
            return new MapSnapshot(stageId, geometry.Width, geometry.Length, Mathf.Max(1, _grid.GetVerticalGridsCount()), cells);
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

            if (!Directory.Exists(MapsFolder))
            {
                Directory.CreateDirectory(MapsFolder);
            }

            string path = AssetPath(stageId);
            File.WriteAllText(path, MapJson.Save(BuildSnapshot(geometry)));
            AssetDatabase.ImportAsset(path);

            _dirty = false;
            Debug.Log($"[地形刷子] 第 {stageId} 关已保存 {_painted.Count} 个地块 → {path}");
        }

        private void Load()
        {
            string path = AssetPath(stageId);
            if (!File.Exists(path))
            {
                EditorUtility.DisplayDialog("地形刷子", $"文件不存在：{path}", "知道了");
                return;
            }

            if (_dirty && !EditorUtility.DisplayDialog("地形刷子",
                    $"当前有 {_painted.Count} 个未保存的地块，载入会丢弃它们。继续？", "载入", "取消"))
            {
                return;
            }

            MapSnapshot snapshot = MapJson.Load($"stage_{stageId}", File.ReadAllText(path));

            _painted.Clear();
            _undoStack.Clear();
            foreach (MapCell cell in snapshot.Cells)
            {
                _painted[new Vector3Int(cell.X, cell.Z, cell.Layer)] = cell.ElementId;
            }

            GridGeometry geometry;
            if (TryGetGeometry(out geometry) && (snapshot.Width != geometry.Width || snapshot.Length != geometry.Length))
            {
                Debug.LogWarning($"[地形刷子] 存档是 {snapshot.Width}×{snapshot.Length}，当前场景网格是 {geometry.Width}×{geometry.Length}。" +
                                 "超出场景网格的地块仍在数据里，但刷子改不到它们——建议先把场景网格调成一致再编辑。");
            }

            _dirty = false;
            RefreshOverlay();
            Repaint();
            Debug.Log($"[地形刷子] 第 {stageId} 关已载入 {_painted.Count} 个地块 ← {path}");
        }

        private void ResizeSceneGrid(int width, int height)
        {
            var so = new SerializedObject(_grid);
            SerializedProperty w = so.FindProperty("gridWidth");
            SerializedProperty l = so.FindProperty("gridLength");
            if (w == null || l == null)
            {
                Debug.LogWarning("[地形刷子] EasyGridBuilderPro 上找不到 gridWidth/gridLength（插件版本变了？）。", _grid);
                return;
            }
            w.intValue = width;
            l.intValue = height;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(_grid);
            RefreshOverlay();
        }
    }
}
