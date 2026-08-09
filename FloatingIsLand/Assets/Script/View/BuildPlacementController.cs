using System.Collections.Generic;
using FloatingIsLand.App;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 建造模式的摆放交互：选中手牌后，建筑 ghost 跟随鼠标吸附到格子，
    /// 滚轮旋转 90° 步进，左键落地，右键 / Esc 取消选中。
    ///
    /// 相机操作保持原有逻辑不变（WASD 平移、右键拖旋转、中键拖平移、Shift/Ctrl 升降）；
    /// 只有滚轮在建造模式下让位给建筑旋转——通过 <see cref="InputArbiter"/> 声明占用，
    /// 相机控制器读同一个标志位跳过缩放。
    ///
    /// 合法性与得分全部回领域层问（<see cref="GameSession"/> 干跑），这里只负责把鼠标位置换成格子、
    /// 把结果画出来。预览分两层，各管一件事：
    ///
    /// - **落点格标记**（<see cref="PlacementFootprintRenderer"/>）——绿/红画在**地上**，
    ///   逐格反映「这一格能不能建」，跟着朝向一起转；
    /// - **建筑虚影**（<see cref="GhostAppearance"/>）——保留原材质，叠一层虚影 shader 再染色，
    ///   玩家仍然认得出摆的是哪栋楼、转到了哪个朝向。
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        [Tooltip("实现 IGridPresenter 的组件（当前是 EGBGridPresenter）")]
        [SerializeField] private MonoBehaviour gridPresenterBehaviour;

        [Header("落点格标记")]
        [Tooltip("整体可摆时，每个落点格的颜色")]
        [SerializeField] private Color cellValidColor = new Color(0.25f, 1f, 0.45f, 0.45f);

        [Tooltip("整体摆不下、但这一格自身没问题时的颜色（淡红：问题不在这格）")]
        [SerializeField] private Color cellInvalidColor = new Color(1f, 0.55f, 0.25f, 0.30f);

        [Tooltip("这一格自身就是障碍（虚空 / 已被占 / 压到元素 / 地形不符）时的颜色")]
        [SerializeField] private Color cellBlockedColor = new Color(1f, 0.15f, 0.15f, 0.62f);

        [Header("建筑虚影染色")]
        [Tooltip("可摆放时叠在原材质上的虚影染色")]
        [SerializeField] private Color ghostValidTint = new Color(0.35f, 1f, 0.55f, 1f);

        [Tooltip("不可摆放时叠在原材质上的虚影染色")]
        [SerializeField] private Color ghostInvalidTint = new Color(1f, 0.32f, 0.28f, 1f);

        private IGridPresenter _presenter;
        private GameSession _session;
        private TerrainOverlayRenderer _terrainOverlay;
        private PlacementFootprintRenderer _footprintRenderer;

        /// <summary>逐格判定的复用缓冲，避免每帧分配。</summary>
        private readonly List<CellPlacement> _cellChecks = new List<CellPlacement>(36);

        private GameObject _ghost;
        private string _ghostVariantId;
        private Rotation _rotation = Rotation.Deg0;

        private bool _hasHover;
        private int _hoverX;
        private int _hoverZ;
        private int _hoverLayer;
        private bool _hoverValid;

        // 光标格 = 建筑中心；锚点格 = 旋转后占地的最小角（领域层与摆放口径都按锚点算）。
        // 两者必须分开存：光标格来自鼠标，锚点格随朝向变，混用就会出现"预览和落点差一截"。
        private int _anchorX;
        private int _anchorZ;

        /// <summary>当前 ghost 的朝向。</summary>
        public Rotation CurrentRotation
        {
            get { return _rotation; }
        }

        /// <summary>最近一次干跑的合法性；没有悬停格时为 false。</summary>
        public bool HoverIsValid
        {
            get { return _hasHover && _hoverValid; }
        }

        /// <summary>最近一次干跑的失败原因 / 得分预览，供 UI 显示。</summary>
        public string HoverMessage { get; private set; } = string.Empty;

        private void Awake()
        {
            _presenter = gridPresenterBehaviour as IGridPresenter;
        }

        /// <summary>绑定本局会话。由 MapBootstrap 在建造链路就绪后调用。</summary>
        public void Bind(GameSession session)
        {
            Unbind();
            _session = session;
            if (_session != null)
            {
                _session.SelectionChanged += OnSelectionChanged;
            }
            OnSelectionChanged();
        }

        /// <summary>
        /// 接管地形 overlay 的显隐。绑定后网格不再常显：只有进入建造模式、且光标确实落在某个格子上时
        /// 才亮起，并且只亮光标附近几圈（渐隐由 <see cref="TerrainOverlayRenderer.SetFocus"/> 负责）。
        /// 由 MapBootstrap 在建造链路就绪后调用；不调用则 overlay 维持原来的常显行为。
        /// </summary>
        public void BindTerrainOverlay(TerrainOverlayRenderer overlay)
        {
            _terrainOverlay = overlay;
            HideTerrainOverlay();
        }

        private void Unbind()
        {
            if (_session != null)
            {
                _session.SelectionChanged -= OnSelectionChanged;
            }
            _session = null;
        }

        private void OnDisable()
        {
            // 失活时必须归还滚轮，否则相机永远缩放不了
            InputArbiter.ScrollConsumedByGameplay = false;
            HideTerrainOverlay();
            HideFootprint();
        }

        private void OnDestroy()
        {
            Unbind();
            InputArbiter.Reset();
            DestroyGhost();
        }

        private void HideTerrainOverlay()
        {
            if (_terrainOverlay != null)
            {
                _terrainOverlay.ClearFocus();
                _terrainOverlay.SetVisible(false);
            }
        }

        private void HideFootprint()
        {
            if (_footprintRenderer != null)
            {
                _footprintRenderer.Hide();
            }
        }

        /// <summary>
        /// 落点格标记的渲染器按需现建。场景是编辑器菜单生成的，往里加一个节点就得重跑生成流程；
        /// 而这东西没有任何需要美术调的序列化引用（配色在本组件上），运行时建更省事。
        /// </summary>
        private PlacementFootprintRenderer EnsureFootprintRenderer()
        {
            if (_footprintRenderer == null)
            {
                var go = new GameObject(
                    "PlacementFootprint",
                    typeof(MeshFilter), typeof(MeshRenderer), typeof(PlacementFootprintRenderer));
                go.transform.SetParent(transform, false);
                // 顶点是**世界坐标**（GridGeometry 算出来的），所以自身位姿必须是单位变换，
                // 否则挂到一个有偏移的父节点下，整片标记会跟着漂走
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                _footprintRenderer = go.GetComponent<PlacementFootprintRenderer>();
            }
            return _footprintRenderer;
        }

        private void Update()
        {
            if (_session == null || !_session.IsBuildReady || _presenter == null)
            {
                return;
            }

            BuildingBlueprint blueprint = _session.SelectedBlueprint;
            bool placing = blueprint != null;

            // 滚轮归属只在建造模式下抢，其它时候一律还给相机
            InputArbiter.ScrollConsumedByGameplay = placing;

            if (!placing)
            {
                _hasHover = false;
                HoverMessage = string.Empty;
                DestroyGhost();
                HideTerrainOverlay();
                HideFootprint();
                return;
            }

            EnsureGhost(blueprint);
            HandleRotation();
            UpdateHover(blueprint);
            UpdateTerrainFocus();
            HandleClicks();
        }

        /// <summary>
        /// 建造模式下的网格显示：跟着光标走，只亮附近几圈。
        /// 光标不在格子上（悬在 UI 上或指向天空）时整块收起，而不是退回全图常显——
        /// 那样一移开鼠标满屏格子会突然全亮，比不显示还刺眼。
        /// </summary>
        private void UpdateTerrainFocus()
        {
            if (_terrainOverlay == null)
            {
                return;
            }

            if (!_hasHover)
            {
                HideTerrainOverlay();
                return;
            }

            _terrainOverlay.SetVisible(true);
            _terrainOverlay.SetFocus(_hoverX, _hoverZ, _hoverLayer);
        }

        /// <summary>滚轮 → 90° 步进旋转。Windows 原始值 ±120/格，与相机控制器同一套折算口径。</summary>
        private void HandleRotation()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) <= 0.01f)
            {
                return;
            }

            float notches = Mathf.Abs(scroll) > 10f ? scroll / 120f : scroll;
            int steps = Mathf.RoundToInt(notches);
            if (steps == 0)
            {
                steps = notches > 0f ? 1 : -1;
            }
            _rotation = _rotation.Step(steps);
        }

        private void UpdateHover(BuildingBlueprint blueprint)
        {
            _hasHover = !IsPointerOverUI() && _presenter.TryGetHoveredCell(out _hoverX, out _hoverZ, out _hoverLayer);
            if (!_hasHover)
            {
                _hoverValid = false;
                HoverMessage = string.Empty;
                HideFootprint();
                if (_ghost != null)
                {
                    _ghost.SetActive(false);
                }
                return;
            }

            // 光标格是建筑**中心**，领域层要的是锚点格（占地最小角）——换算随朝向变，每帧重算
            blueprint.Footprint.AnchorFromCenter(_hoverX, _hoverZ, _rotation, out _anchorX, out _anchorZ);

            PlacementCheck check = _session.CheckSelectedPlacement(_anchorX, _anchorZ, _hoverLayer, _rotation);
            _hoverValid = check.IsValid;

            if (check.IsValid)
            {
                ScoreBreakdown preview = _session.PreviewSelectedScore(_anchorX, _anchorZ, _hoverLayer, _rotation);
                HoverMessage = preview != null ? $"预计得分 {preview.Total}" : string.Empty;
            }
            else
            {
                HoverMessage = check.Reason;
            }

            // 落点格逐格标绿 / 标红。格子由 Footprint.GetCells 展开，**天然跟着朝向转**——
            // 转 90° 时占地矩形跟着转，不是只有模型在转；异形占地也是逐格画，不退化成外接矩形。
            _session.CheckSelectedCells(_anchorX, _anchorZ, _hoverLayer, _rotation, _cellChecks);
            EnsureFootprintRenderer().Show(
                _cellChecks, _hoverLayer, _presenter.Geometry, _hoverValid,
                cellValidColor, cellInvalidColor, cellBlockedColor);

            if (_ghost != null)
            {
                _ghost.SetActive(true);
                Vector3 corner = _presenter.Geometry.CellCorner(_anchorX, _anchorZ, _hoverLayer);
                // 位姿口径必须和落地时同一份，否则预览和实际落点会差一截
                ModelSpawner.PlaceAt(_ghost, corner, _rotation, blueprint.Footprint, _presenter.CellSize);
                ModelSpawner.ApplyGhostAppearance(_ghost, _hoverValid ? ghostValidTint : ghostInvalidTint);
            }
        }

        private void HandleClicks()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _session.ClearSelection();
                return;
            }

            if (mouse == null)
            {
                return;
            }

            // 右键是相机旋转的按住键，只在「没有拖动」的单击上取消选中会引起误判，
            // 所以取消统一交给 Esc 与 UI 上的再次点击，右键不参与建造。
            if (!mouse.leftButton.wasPressedThisFrame || !_hasHover || IsPointerOverUI())
            {
                return;
            }

            PlacementCheck check;
            ScoreBreakdown breakdown;
            // 用 UpdateHover 算好的锚点格，不能拿光标格：那样落点会和刚才预览的位置差半栋楼
            if (_session.TryPlaceSelected(_anchorX, _anchorZ, _hoverLayer, _rotation, out check, out breakdown))
            {
                return;
            }

            Debug.Log($"[建造] 无法摆放：{check.Reason}");
        }

        private void EnsureGhost(BuildingBlueprint blueprint)
        {
            if (_ghost != null && string.Equals(_ghostVariantId, blueprint.VariantId))
            {
                return;
            }

            DestroyGhost();
            _ghostVariantId = blueprint.VariantId;
            _ghost = ModelSpawner.Spawn(
                blueprint.PrefabPath, Vector3.zero, _rotation, transform,
                $"Ghost_{blueprint.VariantId}",
                blueprint.Footprint, _presenter.CellSize);
            ModelSpawner.ApplyGhostAppearance(_ghost, ghostValidTint);
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
            }
            _ghost = null;
            _ghostVariantId = null;
        }

        private void OnSelectionChanged()
        {
            // 每次换建筑都从 0° 起手，避免上一栋的朝向莫名其妙带到下一栋
            _rotation = Rotation.Deg0;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
