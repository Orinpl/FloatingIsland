using FloatingIsLand.App;
using FloatingIsLand.Config;
using FloatingIsLand.Domain.Build;
using FloatingIsLand.Domain.Map;
using UnityEngine;

namespace FloatingIsLand.View
{
    /// <summary>
    /// 局内世界表现：关卡岛屿模型 + 地图元素 + 已落地建筑。
    ///
    /// 只订阅领域事件、只做实例化，不参与任何规则判定（GRID_INTEGRATION §1：EGB 与表现层永远不裁决）。
    /// 岛屿按 Stage.islandCellSpan 缩放到设计的可玩跨度并居中——美术给的是单位尺度模型，
    /// 不缩放的话整座岛还没一个格子大。
    /// </summary>
    public sealed class WorldRenderer : MonoBehaviour
    {
        [Tooltip("实现 IGridPresenter 的组件（当前是 EGBGridPresenter）")]
        [SerializeField] private MonoBehaviour gridPresenterBehaviour;

        [Tooltip("岛屿模型顶面对齐到的世界高度。地形 overlay 画在 y=0，岛面略低一点避免 Z-fighting")]
        [SerializeField] private float islandTopY = -0.05f;

        private IGridPresenter _presenter;
        private Transform _islandRoot;
        private Transform _elementRoot;
        private Transform _buildingRoot;
        private GameSession _session;

        private void Awake()
        {
            _presenter = gridPresenterBehaviour as IGridPresenter;
            _islandRoot = new GameObject("Island").transform;
            _elementRoot = new GameObject("MapElements").transform;
            _buildingRoot = new GameObject("Buildings").transform;
            _islandRoot.SetParent(transform, false);
            _elementRoot.SetParent(transform, false);
            _buildingRoot.SetParent(transform, false);
        }

        /// <summary>
        /// 建造链路就绪后调一次：摆岛、摆元素，并挂上建筑落地的订阅。
        /// </summary>
        public void Bind(GameSession session)
        {
            if (session == null || !session.IsBuildReady)
            {
                return;
            }
            if (_presenter == null)
            {
                Debug.LogError("[表现] WorldRenderer 的 gridPresenterBehaviour 未指定或没实现 IGridPresenter。", this);
                return;
            }

            _session = session;
            SpawnIsland(session.Board.Map);
            SpawnElements(session.Board);

            session.Board.BuildingPlaced += OnBuildingPlaced;
            // 注入前已经落过的建筑（正常流程为空，读档/重绑时才有）补画一遍
            for (int i = 0; i < session.Board.Buildings.Count; i++)
            {
                OnBuildingPlaced(session.Board.Buildings[i]);
            }
        }

        private void OnDestroy()
        {
            if (_session != null && _session.Board != null)
            {
                _session.Board.BuildingPlaced -= OnBuildingPlaced;
            }
        }

        private void SpawnIsland(MapSnapshot map)
        {
            StageRow stage = Tables.Stage.GetOrNull(map.StageId);
            if (stage == null || string.IsNullOrEmpty(stage.prefabPath))
            {
                Debug.LogWarning($"[表现] 第 {map.StageId} 关没有配岛屿模型（Stage.prefabPath 为空），跳过岛屿表现。");
                return;
            }

            var prefab = Resources.Load<GameObject>(stage.prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[表现] 岛屿模型 '{stage.prefabPath}' 不在 Resources 里，跳过岛屿表现。");
                return;
            }

            GameObject island = Instantiate(prefab, _islandRoot);
            island.name = $"stage_{map.StageId:00}";
            FitIslandToGrid(island, map, stage.islandCellSpan);
        }

        /// <summary>
        /// 把岛屿等比缩放到 islandCellSpan 格跨度、居中到地图中心、顶面对齐到 <see cref="islandTopY"/>。
        /// 缩放取 XZ 长边，保证不论模型是长条还是方形都刚好落在设计跨度里。
        /// </summary>
        private void FitIslandToGrid(GameObject island, MapSnapshot map, int cellSpan)
        {
            Bounds bounds;
            if (!TryGetWorldBounds(island, out bounds))
            {
                Debug.LogWarning("[表现] 岛屿模型没有任何 Renderer，无法缩放对齐。", island);
                return;
            }

            float longestSide = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longestSide <= Mathf.Epsilon)
            {
                Debug.LogWarning("[表现] 岛屿模型 XZ 包围盒为零，无法缩放对齐。", island);
                return;
            }

            int span = cellSpan > 0 ? cellSpan : 40;
            float targetMeters = span * _presenter.CellSize;
            float scale = targetMeters / longestSide;
            island.transform.localScale *= scale;

            // 缩放会改变包围盒，重新取一次再对位
            if (!TryGetWorldBounds(island, out bounds))
            {
                return;
            }

            GridGeometry geometry = _presenter.Geometry;
            Vector3 mapCenter = geometry.CellCorner(map.Width / 2, map.Length / 2, 0);
            Vector3 offset = new Vector3(
                mapCenter.x - bounds.center.x,
                islandTopY - bounds.max.y,
                mapCenter.z - bounds.center.z);
            island.transform.position += offset;

            Debug.Log($"[表现] 岛屿模型已对齐：缩放 ×{scale:0.###} → {span} 格跨度（{targetMeters:0.#} m），" +
                      $"顶面 y={islandTopY:0.##}，中心对到地图中心 ({map.Width / 2}, {map.Length / 2})。");
        }

        private void SpawnElements(BuildBoard board)
        {
            float cellSize = _presenter.CellSize;
            GridGeometry geometry = _presenter.Geometry;

            int spawned = 0;
            for (int i = 0; i < board.Elements.Count; i++)
            {
                PlacedElement element = board.Elements[i];
                MapElementDef def = element.Def;

                // 没配 prefabPath 的元素是「本来就没有独立模型」（风源靠风系统表现），
                // 不能像建筑那样退化成白模——否则岛上会平白多出几个白方块。
                if (string.IsNullOrEmpty(def.PrefabPath))
                {
                    continue;
                }

                Vector3 corner = geometry.CellCorner(element.X, element.Z, element.Layer);
                ModelSpawner.Spawn(
                    def.PrefabPath, corner, element.Rotation, _elementRoot,
                    $"{def.ElementId}_{element.Id}",
                    def.Footprint.SpanX(element.Rotation),
                    def.Footprint.SpanZ(element.Rotation),
                    cellSize);
                spawned++;
            }

            Debug.Log($"[表现] 已生成 {spawned} 个地图元素模型（共 {board.Elements.Count} 个元素，其余无独立模型）。");
        }

        private void OnBuildingPlaced(PlacedBuilding building)
        {
            GridGeometry geometry = _presenter.Geometry;
            Vector3 corner = geometry.CellCorner(building.X, building.Z, building.Layer);

            ModelSpawner.Spawn(
                building.Blueprint.PrefabPath, corner, building.Rotation, _buildingRoot,
                $"{building.Blueprint.VariantId}_{building.Id}",
                building.Blueprint.Footprint.SpanX(building.Rotation),
                building.Blueprint.Footprint.SpanZ(building.Rotation),
                _presenter.CellSize);
        }

        private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return any;
        }
    }
}
