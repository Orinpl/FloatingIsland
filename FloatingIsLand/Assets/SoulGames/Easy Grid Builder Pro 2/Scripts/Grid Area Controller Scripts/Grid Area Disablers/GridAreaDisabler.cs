using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Grid Area Disabler", 2)]
    public class GridAreaDisabler : MonoBehaviour
    {
        public static event OnGridAreaDisablerInitializedDelegate OnGridAreaDisablerInitialized;
        public delegate void OnGridAreaDisablerInitializedDelegate(GridAreaDisabler gridAreaDisabler, GridAreaDisablerData gridAreaDisablerData);

        public static event OnGridAreaDisablerUpdatedDelegate OnGridAreaDisablerUpdated;
        public delegate void OnGridAreaDisablerUpdatedDelegate(GridAreaDisabler gridAreaDisabler);

        public static event OnGridAreaDisablerDisabledDelegate OnGridAreaDisablerDisabled;
        public delegate void OnGridAreaDisablerDisabledDelegate(GridAreaDisabler gridAreaDisabler);

        [SerializeField] private List<GridArea> gridAreaList;

        [SerializeField] private bool blockAllGridObjects;
        [SerializeField] private List<BuildableGridObjectCategorySO> blockGridObjectCategoriesList;
        [SerializeField] private List<BuildableGridObjectSO> blockGridObjectsList;

        [SerializeField] private bool blockAllEdgeObjects;
        [SerializeField] private List<BuildableEdgeObjectCategorySO> blockEdgeObjectCategoriesList;
        [SerializeField] private List<BuildableEdgeObjectSO> blockEdgeObjectsList;

        [SerializeField] private bool blockAllCornerObjects;
        [SerializeField] private List<BuildableCornerObjectCategorySO> blockCornerObjectCategoriesList;
        [SerializeField] private List<BuildableCornerObjectSO> blockCornerObjectsList;

        [SerializeField] private bool blockAllFreeObjects;
        [SerializeField] private List<BuildableFreeObjectCategorySO> blockFreeObjectCategoriesList;
        [SerializeField] private List<BuildableFreeObjectSO> blockFreeObjectsList;

        [SerializeField] private bool changeBlockedCellColor;
        [SerializeField] private Color blockedCellHighlightColor;

        public Dictionary<GridArea, GridAreaData> GridAreaDataDictionary;
        private GridAreaDisablerData gridAreaDisablerData;
        private bool isInstantiatedByGhostObject;

        private void Start()
        {
            BuildableObject buildableObject = GetComponentInParent<BuildableObject>();
            if (buildableObject) isInstantiatedByGhostObject = buildableObject.GetIsInstantiatedByGhostObject();
            if (isInstantiatedByGhostObject) return;

            GridAreaDataDictionary = new Dictionary<GridArea, GridAreaData>();
            gridAreaDisablerData = new GridAreaDisablerData();

            GridArea.OnGridAreaInitialized += OnGridAreaInitialized;
            GridArea.OnGridAreaUpdated += OnGridAreaUpdated;
        }
        
        private void OnEnable()
        {
            StartCoroutine(LateOnEnable());
        }

        private IEnumerator LateOnEnable()
        {
            yield return new WaitForEndOfFrame();
            if (isInstantiatedByGhostObject) yield break;
            
            OnGridAreaDisablerInitialized?.Invoke(this, gridAreaDisablerData);
        }

        private void OnDestroy()
        {
            if (isInstantiatedByGhostObject) return;

            GridArea.OnGridAreaInitialized -= OnGridAreaInitialized;
            GridArea.OnGridAreaUpdated -= OnGridAreaUpdated;
        }

        private void OnDisable()
        {
            if (isInstantiatedByGhostObject) return;
            OnGridAreaDisablerDisabled?.Invoke(this);
        }

        private void OnGridAreaInitialized(GridArea gridArea, GridAreaData gridAreaData)
        {
            InitializeGridAreaDisablerData(gridArea, gridAreaData);
            PopulateGridAreaDisablerData();
            OnGridAreaDisablerInitialized?.Invoke(this, gridAreaDisablerData);
        }

        private void OnGridAreaUpdated(GridArea gridArea)
        {
            UpdateGridAreaDisablerData(gridArea);
            PopulateGridAreaDisablerData();
            OnGridAreaDisablerUpdated?.Invoke(this);
        }

        private void InitializeGridAreaDisablerData(GridArea gridArea, GridAreaData gridAreaData)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            if (!GridAreaDataDictionary.ContainsKey(gridArea)) GridAreaDataDictionary[gridArea] = gridAreaData;
        }

        private void UpdateGridAreaDisablerData(GridArea gridArea)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            PopulateGridAreaDisablerData();
        }

        private void PopulateGridAreaDisablerData()
        {
            gridAreaDisablerData.GridAreaDataDictionary = GridAreaDataDictionary ?? new Dictionary<GridArea, GridAreaData>();

            gridAreaDisablerData.blockAllGridObjects = blockAllGridObjects;
            gridAreaDisablerData.blockGridObjectCategoriesList = blockGridObjectCategoriesList ?? new List<BuildableGridObjectCategorySO>();
            gridAreaDisablerData.blockGridObjectsList = blockGridObjectsList ?? new List<BuildableGridObjectSO>();

            gridAreaDisablerData.blockAllEdgeObjects = blockAllEdgeObjects;
            gridAreaDisablerData.blockEdgeObjectCategoriesList = blockEdgeObjectCategoriesList ?? new List<BuildableEdgeObjectCategorySO>();
            gridAreaDisablerData.blockEdgeObjectsList = blockEdgeObjectsList ?? new List<BuildableEdgeObjectSO>();
            
            gridAreaDisablerData.blockAllCornerObjects = blockAllCornerObjects;
            gridAreaDisablerData.blockCornerObjectCategoriesList = blockCornerObjectCategoriesList ?? new List<BuildableCornerObjectCategorySO>();
            gridAreaDisablerData.blockCornerObjectsList = blockCornerObjectsList ?? new List<BuildableCornerObjectSO>();

            gridAreaDisablerData.blockAllFreeObjects = blockAllFreeObjects;
            gridAreaDisablerData.blockFreeObjectCategoriesList = blockFreeObjectCategoriesList ?? new List<BuildableFreeObjectCategorySO>();
            gridAreaDisablerData.blockFreeObjectsList = blockFreeObjectsList ?? new List<BuildableFreeObjectSO>();

            gridAreaDisablerData.changeBlockedCellColor = changeBlockedCellColor;
            gridAreaDisablerData.blockedCellHighlightColor = blockedCellHighlightColor;
        }
    }
}