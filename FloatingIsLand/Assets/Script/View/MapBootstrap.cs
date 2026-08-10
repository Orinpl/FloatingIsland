using System.Collections;
using FloatingIsLand.App;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Map;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 进入局内时装载关卡地图：读 Resources/Maps/stage_{id}.json → 按快照尺寸建格 → 画地形 overlay。
    ///
    /// 「只加载之前保存的地块」就落在这里：快照是稀疏的，没刷过的坐标压根不在数据里，
    /// 既不会被建格逻辑看到，也不会生成任何顶点。
    ///
    /// 本组件在 Game.View，只认 <see cref="IGridPresenter"/>，不知道 EGB 的存在（GRID_INTEGRATION §4）。
    /// </summary>
    public sealed class MapBootstrap : MonoBehaviour
    {
        [Tooltip("实现 IGridPresenter 的组件（当前是 EGBGridPresenter）。Unity 不能序列化接口，所以用 MonoBehaviour 接收")]
        [SerializeField] private MonoBehaviour gridPresenterBehaviour;

        [SerializeField] private TerrainOverlayRenderer terrainOverlay;

        [Tooltip("岛屿/元素/建筑的表现渲染器。留空则本局不生成任何模型（只有地形 overlay）")]
        [SerializeField] private WorldRenderer worldRenderer;

        [Tooltip("建造摆放交互。留空则不可建造（纯看图模式）")]
        [SerializeField] private BuildPlacementController placementController;

        [Tooltip("拿不到 GameFlow.CurrentRun 时用的关卡号（直接从 Main 场景 Play 调试地图时会走这条）")]
        [SerializeField] private int fallbackStageId = 1;

        [Tooltip("找不到地图文件时是否报错。手工图还没刷完的关卡可以先关掉")]
        [SerializeField] private bool errorWhenMapMissing = true;

        /// <summary>等局内会话出现的最多帧数（LoadingState 在场景加载完才创建它）。超时按「无局外流程」处理。</summary>
        private const int SessionWaitFrames = 30;

        private IGridPresenter _presenter;
        private WindFieldView _windFieldView;

        /// <summary>当前局内地图；未装载完成时为 null。领域层规则校验后续从这里取地形。</summary>
        public MapSnapshot Snapshot { get; private set; }

        /// <summary>实际装载的关卡号。</summary>
        public int StageId { get; private set; }

        private IEnumerator Start()
        {
            _presenter = gridPresenterBehaviour as IGridPresenter;
            if (_presenter == null)
            {
                Debug.LogError("[地图] MapBootstrap 的 gridPresenterBehaviour 未指定或没实现 IGridPresenter" +
                               "（可用菜单 Tools/框架/给 Main 场景接入 EGB 网格 重新接线）。", this);
                yield break;
            }
            if (terrainOverlay == null)
            {
                Debug.LogError("[地图] MapBootstrap 未指定 terrainOverlay，地形不会显示。", this);
                yield break;
            }

            StageId = ResolveStageId();

            MapSnapshot snapshot;
            if (!TryLoad(StageId, out snapshot))
            {
                yield break;
            }

            // EGB 内部格子列表要等它自己的 Start 建完，早一帧调 BuildGrid 会 NRE
            while (!_presenter.IsReady)
            {
                yield return null;
            }

            // 先按快照尺寸建格，再取 Geometry——BuildGrid 会改变网格原点
            _presenter.BuildGrid(snapshot.Width, snapshot.Length);
            Snapshot = snapshot;
            terrainOverlay.Rebuild(snapshot, _presenter.Geometry);

            Debug.Log($"[地图] 第 {StageId} 关地图已装载：{snapshot.Width}×{snapshot.Length}，已刷 {snapshot.PaintedCount} 格" +
                      $"（其余为虚空，不渲染不可建造），地图元素 {snapshot.Elements.Count} 个。");

            yield return AttachToSession(snapshot);
        }

        /// <summary>
        /// 把地图交给局内会话，建造链路由此就绪，再把表现层挂上去。
        ///
        /// 地图读取在 View（要用 Unity 的 Resources），领域规则在 App/Domain，两边的接缝就在这一步：
        /// 会话不碰 Unity 资源，View 不做规则判定。
        ///
        /// 要等几帧：LoadingState 是在 Main 场景**加载完成之后**才 new GameSession，
        /// 而本组件的 Start 在场景加载时就跑了——两者是赛跑关系，直接读 CurrentSession 可能拿到 null。
        /// </summary>
        private IEnumerator AttachToSession(MapSnapshot snapshot)
        {
            GameSession session = null;
            for (int frame = 0; frame < SessionWaitFrames; frame++)
            {
                GameFlow flow = GameFlow.Instance;
                session = flow != null ? flow.CurrentSession : null;
                if (session != null)
                {
                    break;
                }
                yield return null;
            }

            if (session == null)
            {
                // 直接从 Main 场景 Play 调地图时没有局外流程，这条是预期路径，不是错误
                Debug.LogWarning("[地图] 当前没有局内会话（多半是直接从 Main 场景 Play 调试地图），建造链路未启动。");
                yield break;
            }

            try
            {
                session.AttachMap(snapshot);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[地图] 建造链路启动失败：{e.Message}", this);
                yield break;
            }

            if (worldRenderer != null)
            {
                worldRenderer.Bind(session);
            }

            // 风路流线：运行时现建（同 BuildPlacementController 的表现节点——场景是编辑器菜单
            // 生成的，往里加节点得重跑那条有模态弹窗的流程，而它没有需要美术调的序列化引用）
            if (_windFieldView == null)
            {
                var windGo = new GameObject("WindFieldView");
                windGo.transform.SetParent(transform, false);
                _windFieldView = windGo.AddComponent<WindFieldView>();
            }
            _windFieldView.Bind(session, _presenter.Geometry);

            if (placementController != null)
            {
                placementController.Bind(session);
                placementController.BindWindView(_windFieldView);
                // 网格交给摆放交互全权控制：不进建造模式就不显示（用户要求 3）。
                // 没有 placementController 的纯看图 / 刷图模式走不到这里，overlay 保持常显。
                placementController.BindTerrainOverlay(terrainOverlay);
                // 必须排在 worldRenderer.Bind 之后：实例索引是在那一步填起来的
                placementController.BindWorld(worldRenderer);
            }
        }

        /// <summary>建造模式进出时切地形 overlay 的显隐。未刷的格子在任何模式下都不显示（它们没有顶点）。</summary>
        public void SetTerrainVisible(bool visible)
        {
            if (terrainOverlay != null)
            {
                terrainOverlay.SetVisible(visible);
            }
        }

        /// <summary>该坐标是否刷过地形。未刷 = 虚空 = 不可建造（完整的可建造判定在 M1 的领域层校验器）。</summary>
        public bool IsPainted(int x, int z, int layer)
        {
            return Snapshot != null && Snapshot.IsPainted(x, z, layer);
        }

        private int ResolveStageId()
        {
            GameFlow flow = GameFlow.Instance;
            if (flow != null && flow.CurrentRun != null)
            {
                return flow.CurrentRun.RunIndex;
            }
            return fallbackStageId;
        }

        private bool TryLoad(int stageId, out MapSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                if (UnityMapLoader.TryLoadFromResources(stageId, out snapshot))
                {
                    return true;
                }
            }
            catch (System.Exception e)
            {
                // 文件存在但内容坏了——这是产线事故，永远要报，不受 errorWhenMapMissing 影响
                Debug.LogError($"[地图] 第 {stageId} 关地图数据非法：{e.Message}", this);
                return false;
            }

            string message = $"[地图] 第 {stageId} 关还没有地图（Resources/{UnityMapLoader.ResourcePath(stageId)}.json 不存在）。" +
                             "用 Tools → 地图 → 地形刷子 刷一张并保存。";
            if (errorWhenMapMissing)
            {
                Debug.LogError(message, this);
            }
            else
            {
                Debug.LogWarning(message, this);
            }
            return false;
        }
    }
}
