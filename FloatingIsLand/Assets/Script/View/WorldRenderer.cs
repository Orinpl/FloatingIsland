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

        private IGridPresenter _presenter;
        private Transform _islandRoot;
        private Transform _elementRoot;
        private Transform _buildingRoot;
        private GameSession _session;
        private readonly WorldInstanceIndex _instances = new WorldInstanceIndex();

        /// <summary>领域实例 Id → 场景模型。计分高亮靠它把归因落到具体那栋楼上。</summary>
        public WorldInstanceIndex Instances
        {
            get { return _instances; }
        }

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

            // 重复 Bind 会把上一局的订阅与索引留着——加了实例索引之后，这从「多一个模型」
            // 升级成「字典撞键、辉光打到已销毁的对象上」，必须先解绑
            Unbind();

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
            Unbind();
        }

        private void Unbind()
        {
            if (_session != null && _session.Board != null)
            {
                _session.Board.BuildingPlaced -= OnBuildingPlaced;
            }
            _session = null;
            _instances.Clear();
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
        /// 缩放 + 居中 + 把可建造平台面对到网格平面。算法与编辑器描摹地形共用
        /// <see cref="IslandFitter"/> 那一份——两边一旦分家，刷出来的地形就会和玩家看到的岛错开。
        /// </summary>
        private void FitIslandToGrid(GameObject island, MapSnapshot map, int cellSpan)
        {
            System.Collections.Generic.Dictionary<Vector3Int, float> heights =
                IslandFitter.Fit(island, _presenter.Geometry, cellSpan);

            Debug.Log($"[表现] 岛屿模型已对位：{cellSpan} 格跨度，平台面对到 y={IslandFitter.SurfaceY:0.##}，" +
                      $"中心对到地图中心 ({map.Width / 2}, {map.Length / 2})，测得 {heights.Count} 格岛面。");
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
                GameObject instance = ModelSpawner.Spawn(
                    def.PrefabPath, corner, element.Rotation, _elementRoot,
                    $"{def.ElementId}_{element.Id}",
                    def.Footprint, cellSize);
                _instances.RegisterElement(element.Id, instance);
                spawned++;
            }

            Debug.Log($"[表现] 已生成 {spawned} 个地图元素模型（共 {board.Elements.Count} 个元素，其余无独立模型）。");
        }

        private void OnBuildingPlaced(PlacedBuilding building)
        {
            GridGeometry geometry = _presenter.Geometry;
            Vector3 corner = geometry.CellCorner(building.X, building.Z, building.Layer);

            GameObject instance = ModelSpawner.Spawn(
                building.Blueprint.PrefabPath, corner, building.Rotation, _buildingRoot,
                $"{building.Blueprint.VariantId}_{building.Id}",
                building.Blueprint.Footprint, _presenter.CellSize);
            _instances.RegisterBuilding(building.Id, instance);
        }

    }
}
