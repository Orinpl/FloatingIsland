using System.Collections.Generic;
using FloatingIsLand.App;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using FloatingIsLand.Domain.Wind;
using FloatingIsLand.GameInput;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 建造模式的摆放交互：选中手牌后，建筑 ghost 跟随鼠标吸附到格子，
    /// 滚轮旋转 90° 步进，左键落地，右键 / Esc 取消选中。
    ///
    /// 手机端是同一套流程、另一组动作（触屏没有悬停也没有滚轮）：
    /// 点一下格子 = 把落点挪过去（手指抬起后预览还在，不挡视线），
    /// **再点同一格** = 落地；旋转 / 建造 / 取消走 HUD 触屏工具条，经
    /// <see cref="BuildCommandBus"/> 传进来。落地、算分、预览全部还是同一份代码，
    /// 两端唯一的区别只有「怎么选中这一格」和「怎么说确认」。
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
        private RangeRingRenderer _rangeRing;
        private ScoreHighlightPresenter _scoreHighlight;
        private WindFieldView _windView;

        /// <summary>落地时风变更结算出的一次性得分（事件在 TryPlaceSelected 内部触发，先存后展示）。</summary>
        private IReadOnlyList<WindAward> _pendingWindAwards;

        /// <summary>逐格判定的复用缓冲，避免每帧分配。</summary>
        private readonly List<CellPlacement> _cellChecks = new List<CellPlacement>(36);

        /// <summary>占用格的复用缓冲（范围环用，口径与领域层同一份 GetCells）。</summary>
        private readonly List<CellCoord> _previewCells = new List<CellCoord>(36);

        // 预览算分不便宜（要扫全场建筑与元素），而光标在同一格里会连着停很多帧。
        // 用「变体 + 锚点 + 层 + 朝向」当键，只在真的换了落点时才重算。
        private string _previewKeyVariant;
        private int _previewKeyX = int.MinValue;
        private int _previewKeyZ = int.MinValue;
        private int _previewKeyLayer = int.MinValue;
        private Rotation _previewKeyRotation;
        private bool _previewKeyValid;

        private GameObject _ghost;
        private string _ghostVariantId;
        private Rotation _rotation = Rotation.Deg0;

        /// <summary>调试钉住：跳过鼠标查询，用外部给定的悬停格（见 DebugPinPreview）。</summary>
        private bool _debugPinned;

        private bool _hasHover;
        private int _hoverX;
        private int _hoverZ;
        private int _hoverLayer;
        private bool _hoverValid;

        // 触屏的二次确认：上一次点出来的落点。再点同一格才落地，
        // 否则手指点哪就建哪——手机上误触代价太高，而且点下去的瞬间手指正好挡着要看的分数。
        private bool _touchArmed;
        private int _touchArmedX;
        private int _touchArmedZ;
        private int _touchArmedLayer;

        /// <summary>本帧 HUD 触屏工具条按了「建造」。按钮回调和 Update 不同步，先记下来到 HandleClicks 里统一处理。</summary>
        private bool _placeCommandPending;

        // 光标格 = 建筑中心；锚点格 = 旋转后占地的最小角（领域层与摆放口径都按锚点算）。
        // 两者必须分开存：光标格来自鼠标，锚点格随朝向变，混用就会出现"预览和落点差一截"。
        private int _anchorX;
        private int _anchorZ;

        /// <summary>被摆建筑自己的飘字锚点（占地中心上方），随悬停更新，落地时沿用。</summary>
        private Vector3 _selfLabelAnchor;

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

        private void OnEnable()
        {
            BuildCommandBus.RotateRequested += OnRotateCommand;
            BuildCommandBus.PlaceRequested += OnPlaceCommand;
            BuildCommandBus.CancelRequested += OnCancelCommand;
        }

        /// <summary>HUD「旋转」：触屏没有滚轮，转 90° 只能靠按钮。</summary>
        private void OnRotateCommand()
        {
            _rotation = _rotation.Step(1);
        }

        /// <summary>HUD「建造」：按钮回调不在 Update 里，先立旗子，落地统一在 HandleClicks 走同一条路径。</summary>
        private void OnPlaceCommand()
        {
            _placeCommandPending = true;
        }

        /// <summary>HUD「取消」：等价于 PC 上的 Esc。</summary>
        private void OnCancelCommand()
        {
            if (_session != null)
            {
                _session.ClearSelection();
            }
        }

        /// <summary>绑定本局会话。由 MapBootstrap 在建造链路就绪后调用。</summary>
        public void Bind(GameSession session)
        {
            Unbind();
            // 新一局从干净的输入状态起手：上一局拖到一半的手势、点出来的黏性落点都不该带过来
            PointerInput.Reset();
            _session = session;
            if (_session != null)
            {
                _session.SelectionChanged += OnSelectionChanged;
                _session.WindAwardsGranted += OnWindAwardsGranted;
            }
            OnSelectionChanged();
        }

        /// <summary>接上风路流线视图，摆风帆/物流点时能预览风路变化。由 MapBootstrap 调用。</summary>
        public void BindWindView(WindFieldView windView)
        {
            _windView = windView;
        }

        private void OnWindAwardsGranted(IReadOnlyList<WindAward> awards)
        {
            // 事件在 TryPlaceSelected 内部同步触发，此刻 PlayPlaced 还没跑；
            // 先存下来，等落地余晖摆好后再叠加展示（顺序反了会被 Begin 清掉）
            _pendingWindAwards = awards;
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

        /// <summary>
        /// 接上世界实例索引，计分高亮才能把「3 号居民区给你 +8」落到那栋楼身上。
        /// 由 MapBootstrap 在建造链路就绪后调用；不调用则只有范围环、没有辉光与飘分。
        /// </summary>
        public void BindWorld(WorldRenderer world)
        {
            EnsureScoreHighlight().Bind(world);
        }

        private void Unbind()
        {
            if (_session != null)
            {
                _session.SelectionChanged -= OnSelectionChanged;
                _session.WindAwardsGranted -= OnWindAwardsGranted;
            }
            _session = null;
        }

        private void OnDisable()
        {
            BuildCommandBus.RotateRequested -= OnRotateCommand;
            BuildCommandBus.PlaceRequested -= OnPlaceCommand;
            BuildCommandBus.CancelRequested -= OnCancelCommand;
            _placeCommandPending = false;

            // 失活时必须归还滚轮，否则相机永远缩放不了
            InputArbiter.ScrollConsumedByGameplay = false;
            HideTerrainOverlay();
            HideFootprint();
            HidePreviewOverlays();
            DisarmTouch();
            BuildPreviewState.Clear();
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

        /// <summary>收起范围环与计分高亮，并让下一次悬停必定重算预览。</summary>
        private void HidePreviewOverlays()
        {
            if (_rangeRing != null)
            {
                _rangeRing.Hide();
            }
            if (_scoreHighlight != null)
            {
                _scoreHighlight.ClearPreview();
            }
            if (_windView != null)
            {
                _windView.ClearPreview();
            }
            InvalidatePreviewKey();
        }

        private void InvalidatePreviewKey()
        {
            _previewKeyVariant = null;
            _previewKeyX = int.MinValue;
            _previewKeyZ = int.MinValue;
            _previewKeyLayer = int.MinValue;
        }

        private RangeRingRenderer EnsureRangeRing()
        {
            if (_rangeRing == null)
            {
                _rangeRing = CreateChild<RangeRingRenderer>(
                    "RangeRing", typeof(MeshFilter), typeof(MeshRenderer), typeof(RangeRingRenderer));
            }
            return _rangeRing;
        }

        private ScoreHighlightPresenter EnsureScoreHighlight()
        {
            if (_scoreHighlight == null)
            {
                _scoreHighlight = CreateChild<ScoreHighlightPresenter>(
                    "ScoreHighlight",
                    typeof(HighlightGlowRenderer), typeof(FloatingScoreLabels), typeof(ScoreHighlightPresenter));
            }
            return _scoreHighlight;
        }

        /// <summary>
        /// 这些表现节点全部运行时现建：场景是编辑器菜单生成的，往里加节点就得重跑生成流程
        /// （那条路有模态弹窗），而它们没有任何需要美术在 Inspector 里调的序列化引用。
        /// 顶点用的是世界坐标，所以自身位姿必须归零，否则挂到有偏移的父节点下整片会漂走。
        /// </summary>
        private T CreateChild<T>(string name, params System.Type[] components) where T : Component
        {
            var go = new GameObject(name, components);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            return go.GetComponent<T>();
        }

        private PlacementFootprintRenderer EnsureFootprintRenderer()
        {
            if (_footprintRenderer == null)
            {
                _footprintRenderer = CreateChild<PlacementFootprintRenderer>(
                    "PlacementFootprint",
                    typeof(MeshFilter), typeof(MeshRenderer), typeof(PlacementFootprintRenderer));
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
                _placeCommandPending = false;
                DisarmTouch();
                DestroyGhost();
                HideTerrainOverlay();
                HideFootprint();
                HidePreviewOverlays();
                BuildPreviewState.Clear();
                return;
            }

            EnsureGhost(blueprint);
            HandleRotation();
            UpdateHover(blueprint);
            UpdateTerrainFocus();
            PublishPreviewState();
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

        /// <summary>
        /// 编辑器调试用：把预览钉在指定落点上，不再跟随鼠标。
        ///
        /// 存在的理由和 GameplayDebugMenu 一样——范围环 / 辉光 / 飘分这三层表现只有在
        /// 「光标悬在某个格子上」时才出现，而鼠标位置没法脚本化。没有这个入口，这几层就只能靠人眼试。
        /// </summary>
        public void DebugPinPreview(int hoverX, int hoverZ, int layer)
        {
            _debugPinned = true;
            _hoverX = hoverX;
            _hoverZ = hoverZ;
            _hoverLayer = layer;
        }

        /// <summary>解除调试钉住，交还给鼠标。</summary>
        public void DebugUnpinPreview()
        {
            _debugPinned = false;
        }

        private void UpdateHover(BuildingBlueprint blueprint)
        {
            _hasHover = _debugPinned
                        || (!IsPointerOverUI() && _presenter.TryGetHoveredCell(out _hoverX, out _hoverZ, out _hoverLayer));
            if (!_hasHover)
            {
                _hoverValid = false;
                HoverMessage = string.Empty;
                HideFootprint();
                HidePreviewOverlays();
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

            // 落点格逐格标绿 / 标红。格子由 Footprint.GetCells 展开，**天然跟着朝向转**——
            // 转 90° 时占地矩形跟着转，不是只有模型在转；异形占地也是逐格画，不退化成外接矩形。
            _session.CheckSelectedCells(_anchorX, _anchorZ, _hoverLayer, _rotation, _cellChecks);
            EnsureFootprintRenderer().Show(
                _cellChecks, _hoverLayer, _presenter.Geometry, _hoverValid,
                cellValidColor, cellInvalidColor, cellBlockedColor);

            // 作用范围环：跟合法性无关——范围是建筑自带的属性，摆不下也该让玩家看到能罩住多大
            blueprint.Footprint.GetCells(_anchorX, _anchorZ, _rotation, _previewCells);
            EnsureRangeRing().Show(_previewCells, _hoverLayer, blueprint.Radius, _presenter.Geometry);

            _selfLabelAnchor = ComputeSelfLabelAnchor(_previewCells, _hoverLayer);

            UpdateScorePreview(blueprint, check);

            if (_ghost != null)
            {
                _ghost.SetActive(true);
                Vector3 corner = _presenter.Geometry.CellCorner(_anchorX, _anchorZ, _hoverLayer);
                // 位姿口径必须和落地时同一份，否则预览和实际落点会差一截
                ModelSpawner.PlaceAt(_ghost, corner, _rotation, blueprint.Footprint, _presenter.CellSize);
                ModelSpawner.ApplyGhostAppearance(_ghost, _hoverValid ? ghostValidTint : ghostInvalidTint);
            }
        }

        /// <summary>
        /// 算这次摆放的预计得分，并把「分是谁给的」画到那些建筑 / 元素身上。
        ///
        /// 只在落点真的变了才重算：算分要扫全场建筑与元素，而光标在同一格里往往连停几十帧。
        /// 键里带朝向——异形占地转 90° 后覆盖的邻居会变，不带朝向会看到"转了但数字没变"。
        /// </summary>
        private void UpdateScorePreview(BuildingBlueprint blueprint, PlacementCheck check)
        {
            bool sameAsLastFrame = _previewKeyVariant == blueprint.VariantId
                                   && _previewKeyX == _anchorX
                                   && _previewKeyZ == _anchorZ
                                   && _previewKeyLayer == _hoverLayer
                                   && _previewKeyRotation == _rotation
                                   && _previewKeyValid == _hoverValid;
            if (sameAsLastFrame)
            {
                return;
            }

            _previewKeyVariant = blueprint.VariantId;
            _previewKeyX = _anchorX;
            _previewKeyZ = _anchorZ;
            _previewKeyLayer = _hoverLayer;
            _previewKeyRotation = _rotation;
            _previewKeyValid = _hoverValid;

            if (!check.IsValid)
            {
                // 摆不下的位置算分没有意义，提示改成给原因
                HoverMessage = check.Reason;
                EnsureScoreHighlight().ClearPreview();
                if (_windView != null)
                {
                    _windView.ClearPreview();
                }
                return;
            }

            ScoreBreakdown preview = _session.PreviewSelectedScore(_anchorX, _anchorZ, _hoverLayer, _rotation);

            // 风路预览：摆的是风帆/物流点时干跑新风场（其余建筑返回 null，等价于清预览），
            // 并预演落地会触发的一次性分（物流风覆盖/互联）——预览看到的数字与落地入账完全一致。
            // 与得分预览共用同一个「落点变了才重算」缓存键，滚轮切换风帆左/右转向会改 rotation，
            // 自然触发重算——玩家立刻看到风往哪边拐、会给谁发分。
            WindField windPreview = _session.PreviewSelectedWind(_anchorX, _anchorZ, _hoverLayer, _rotation);
            IReadOnlyList<WindAward> windAwards = windPreview != null
                ? _session.PreviewSelectedWindAwards(windPreview, _anchorX, _anchorZ, _hoverLayer, _rotation)
                : null;

            int awardTotal = 0;
            if (windAwards != null)
            {
                for (int i = 0; i < windAwards.Count; i++)
                {
                    awardTotal += windAwards[i].Score;
                }
            }
            HoverMessage = preview != null ? $"预计得分 {preview.Total + awardTotal}" : string.Empty;

            EnsureScoreHighlight().ShowPreview(preview, _selfLabelAnchor, windAwards);

            if (_windView != null)
            {
                if (windPreview != null)
                {
                    _windView.ShowPreview(windPreview);
                }
                else
                {
                    _windView.ClearPreview();
                }
            }
        }

        private void HandleClicks()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                _session.ClearSelection();
                return;
            }

            if (!ConsumePlaceRequest())
            {
                return;
            }

            PlacementCheck check;
            ScoreBreakdown breakdown;
            _pendingWindAwards = null;
            // 用 UpdateHover 算好的锚点格，不能拿光标格：那样落点会和刚才预览的位置差半栋楼
            if (_session.TryPlaceSelected(_anchorX, _anchorZ, _hoverLayer, _rotation, out check, out breakdown))
            {
                // 落地余晖：同一批高亮再留一会儿。落地这一刻正是「按范围内的邻居算分」的时刻，
                // 高亮多停一拍，玩家才看得清这一下的分是靠谁拿的
                EnsureScoreHighlight().PlayPlaced(breakdown, _selfLabelAnchor);
                if (_pendingWindAwards != null)
                {
                    // 风变更吹出的一次性收益（物流风覆盖/互联）叠加在同一批余晖上
                    EnsureScoreHighlight().PlayWindAwards(_pendingWindAwards);
                    _pendingWindAwards = null;
                }
                return;
            }

            Debug.Log($"[建造] 无法摆放：{check.Reason}");
        }

        /// <summary>
        /// 这一帧玩家有没有要求落地。三个来源：HUD 的「建造」按钮、触屏的二次点击、PC 的左键。
        /// 无论从哪来，往下都走同一条落地路径——两端的落点、算分、余晖不会分叉。
        /// </summary>
        private bool ConsumePlaceRequest()
        {
            bool commanded = _placeCommandPending;
            _placeCommandPending = false;
            if (commanded)
            {
                // 指令来自 HUD 按钮，它本来就在 UI 上，不必再查 UI 遮挡；但没有落点就没得建
                return _hasHover;
            }

            if (PointerInput.IsTouchMode)
            {
                return ConsumeTouchTap();
            }

            Mouse mouse = Mouse.current;
            // 右键是相机旋转的按住键，只在「没有拖动」的单击上取消选中会引起误判，
            // 所以取消统一交给 Esc 与 UI 上的再次点击，右键不参与建造。
            return mouse != null
                   && mouse.leftButton.wasPressedThisFrame
                   && _hasHover
                   && !IsPointerOverUI();
        }

        /// <summary>
        /// 触屏的二次确认：第一次点把落点挪过去，再点同一格才落地。
        ///
        /// 不做成「点哪建哪」是因为手机上这一下代价太高——手指按下的瞬间正好盖住要看的东西
        /// （范围环、邻居身上的 +3），玩家等于闭着眼睛在建；而且误触没有 Esc 可以救。
        /// 分成两下之后，第一下抬手就能看清这一摆值多少分，第二下才是承诺。
        /// </summary>
        private bool ConsumeTouchTap()
        {
            if (!PointerInput.TapThisFrame || PointerInput.IsOverUI(PointerInput.TapPosition))
            {
                return false;
            }

            if (!_hasHover)
            {
                // 点到了网格外（天空 / 虚空），当作撤销预选，免得下一次点回来直接就建了
                DisarmTouch();
                return false;
            }

            if (_touchArmed
                && _touchArmedX == _hoverX
                && _touchArmedZ == _hoverZ
                && _touchArmedLayer == _hoverLayer)
            {
                return true;
            }

            _touchArmed = true;
            _touchArmedX = _hoverX;
            _touchArmedZ = _hoverZ;
            _touchArmedLayer = _hoverLayer;
            return false;
        }

        /// <summary>撤销触屏的预选，让下一次点击回到「先选位置」而不是「直接落地」。</summary>
        private void DisarmTouch()
        {
            _touchArmed = false;
        }

        /// <summary>
        /// 把当帧预览结论交给 HUD（见 <see cref="BuildPreviewState"/>）。
        /// 手机上「建造」是个按钮，玩家得能看出它为什么是灰的；PC 上这条顺带把
        /// 一直算着却没人显示的「预计得分 N」摆到了提示栏里。
        /// </summary>
        private void PublishPreviewState()
        {
            BuildPreviewState.HasTarget = _hasHover;
            BuildPreviewState.CanPlace = _hasHover && _hoverValid;
            BuildPreviewState.Message = HoverMessage;
        }

        /// <summary>
        /// 被摆建筑自己的飘字锚点：占地各格中心的均值再抬高约一栋楼。
        /// 不取 ghost 包围盒——那要每帧扫 Renderer；几何锚点便宜且预览/落地口径天然一致。
        /// </summary>
        private Vector3 ComputeSelfLabelAnchor(List<CellCoord> cells, int layer)
        {
            GridGeometry geometry = _presenter.Geometry;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < cells.Count; i++)
            {
                sum += geometry.CellCenter(cells[i].X, cells[i].Z, layer);
            }
            Vector3 center = cells.Count > 0 ? sum / cells.Count : Vector3.zero;
            return center + Vector3.up * (geometry.CellSize * 1.6f);
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
            // 换了建筑就得重新选一次落点：上一栋预选的格子对这一栋未必放得下
            DisarmTouch();
        }

        private static bool IsPointerOverUI()
        {
            return PointerInput.IsHoverOverUI();
        }
    }
}
