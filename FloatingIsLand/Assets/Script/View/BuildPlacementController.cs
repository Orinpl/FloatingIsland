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
    /// 把结果画成绿/红 ghost。
    /// </summary>
    public sealed class BuildPlacementController : MonoBehaviour
    {
        [Tooltip("实现 IGridPresenter 的组件（当前是 EGBGridPresenter）")]
        [SerializeField] private MonoBehaviour gridPresenterBehaviour;

        [Tooltip("可摆放时的 ghost 颜色")]
        [SerializeField] private Color validTint = new Color(0.3f, 1f, 0.4f, 0.45f);

        [Tooltip("不可摆放时的 ghost 颜色")]
        [SerializeField] private Color invalidTint = new Color(1f, 0.3f, 0.3f, 0.45f);

        private IGridPresenter _presenter;
        private GameSession _session;

        private GameObject _ghost;
        private string _ghostVariantId;
        private Rotation _rotation = Rotation.Deg0;

        private bool _hasHover;
        private int _hoverX;
        private int _hoverZ;
        private int _hoverLayer;
        private bool _hoverValid;

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
        }

        private void OnDestroy()
        {
            Unbind();
            InputArbiter.Reset();
            DestroyGhost();
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
                return;
            }

            EnsureGhost(blueprint);
            HandleRotation();
            UpdateHover(blueprint);
            HandleClicks();
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
                if (_ghost != null)
                {
                    _ghost.SetActive(false);
                }
                return;
            }

            // ghost 的锚点要跟着旋转后的跨度走，否则转 90° 后模型会整体偏出光标所在格
            PlacementCheck check = _session.CheckSelectedPlacement(_hoverX, _hoverZ, _hoverLayer, _rotation);
            _hoverValid = check.IsValid;

            if (check.IsValid)
            {
                ScoreBreakdown preview = _session.PreviewSelectedScore(_hoverX, _hoverZ, _hoverLayer, _rotation);
                HoverMessage = preview != null ? $"预计得分 {preview.Total}" : string.Empty;
            }
            else
            {
                HoverMessage = check.Reason;
            }

            if (_ghost != null)
            {
                _ghost.SetActive(true);
                Vector3 corner = _presenter.Geometry.CellCorner(_hoverX, _hoverZ, _hoverLayer);
                _ghost.transform.SetPositionAndRotation(corner, Quaternion.Euler(0f, _rotation.ToDegrees(), 0f));
                ModelSpawner.ApplyGhostAppearance(_ghost, _hoverValid ? validTint : invalidTint);
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
            if (_session.TryPlaceSelected(_hoverX, _hoverZ, _hoverLayer, _rotation, out check, out breakdown))
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
                blueprint.Footprint.SpanX(_rotation),
                blueprint.Footprint.SpanZ(_rotation),
                _presenter.CellSize);
            ModelSpawner.ApplyGhostAppearance(_ghost, validTint);
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
