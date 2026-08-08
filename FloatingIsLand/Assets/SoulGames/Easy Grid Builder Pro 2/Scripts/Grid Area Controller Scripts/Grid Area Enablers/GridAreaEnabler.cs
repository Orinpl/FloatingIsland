using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Grid Area Enabler", 4)]
    public class GridAreaEnabler : MonoBehaviour
    {
        public static event OnGridAreaEnablerInitializedDelegate OnGridAreaEnablerInitialized;
        public delegate void OnGridAreaEnablerInitializedDelegate(GridAreaEnabler gridAreaEnabler, GridAreaEnablerData gridAreaEnablerData);

        public static event OnGridAreaEnablerUpdatedDelegate OnGridAreaEnablerUpdated;
        public delegate void OnGridAreaEnablerUpdatedDelegate(GridAreaEnabler gridAreaEnabler);

        public static event OnGridAreaEnablerDisabledDelegate OnGridAreaEnablerDisabled;
        public delegate void OnGridAreaEnablerDisabledDelegate(GridAreaEnabler gridAreaEnabler);

        [SerializeField] private List<GridArea> gridAreaList;

        [SerializeField] private bool enableAllGridObjects;
        [SerializeField] private List<BuildableGridObjectCategorySO> enableGridObjectCategoriesList;
        [SerializeField] private List<BuildableGridObjectSO> enableGridObjectsList;

        [SerializeField] private bool enableAllEdgeObjects;
        [SerializeField] private List<BuildableEdgeObjectCategorySO> enableEdgeObjectCategoriesList;
        [SerializeField] private List<BuildableEdgeObjectSO> enableEdgeObjectsList;

        [SerializeField] private bool enableAllCornerObjects;
        [SerializeField] private List<BuildableCornerObjectCategorySO> enableCornerObjectCategoriesList;
        [SerializeField] private List<BuildableCornerObjectSO> enableCornerObjectsList;

        [SerializeField] private bool enableAllFreeObjects;
        [SerializeField] private List<BuildableFreeObjectCategorySO> enableFreeObjectCategoriesList;
        [SerializeField] private List<BuildableFreeObjectSO> enableFreeObjectsList;

        [SerializeField] private bool changeBlockedCellColor;
        [SerializeField] private Color enabledCellHighlightColor;

        public Dictionary<GridArea, GridAreaData> GridAreaDataDictionary;
        private GridAreaEnablerData gridAreaEnablerData;
        private bool isInstantiatedByGhostObject;

        private void Start()
        {
            BuildableObject buildableObject = GetComponentInParent<BuildableObject>();
            if (buildableObject) isInstantiatedByGhostObject = buildableObject.GetIsInstantiatedByGhostObject();
            if (isInstantiatedByGhostObject) return;

            GridAreaDataDictionary = new Dictionary<GridArea, GridAreaData>();
            gridAreaEnablerData = new GridAreaEnablerData();

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

            OnGridAreaEnablerInitialized?.Invoke(this, gridAreaEnablerData);
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
            OnGridAreaEnablerDisabled?.Invoke(this);
        }

        private void OnGridAreaInitialized(GridArea gridArea, GridAreaData gridAreaData)
        {
            InitializeGridAreaEnablerData(gridArea, gridAreaData);
            PopulateGridAreaEnablerData();
            OnGridAreaEnablerInitialized?.Invoke(this, gridAreaEnablerData);
        }

        private void OnGridAreaUpdated(GridArea gridArea)
        {
            UpdateGridAreaEnablerData(gridArea);
            PopulateGridAreaEnablerData();
            OnGridAreaEnablerUpdated?.Invoke(this);
        }

        private void InitializeGridAreaEnablerData(GridArea gridArea, GridAreaData gridAreaData)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            if (!GridAreaDataDictionary.ContainsKey(gridArea)) GridAreaDataDictionary[gridArea] = gridAreaData;
        }

        private void UpdateGridAreaEnablerData(GridArea gridArea)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            PopulateGridAreaEnablerData();
        }

        private void PopulateGridAreaEnablerData()
        {
            gridAreaEnablerData.GridAreaDataDictionary = GridAreaDataDictionary ?? new Dictionary<GridArea, GridAreaData>();

            gridAreaEnablerData.enableAllGridObjects = enableAllGridObjects;
            gridAreaEnablerData.enableGridObjectCategoriesList = enableGridObjectCategoriesList ?? new List<BuildableGridObjectCategorySO>();
            gridAreaEnablerData.enableGridObjectsList = enableGridObjectsList ?? new List<BuildableGridObjectSO>();

            gridAreaEnablerData.enableAllEdgeObjects = enableAllEdgeObjects;
            gridAreaEnablerData.enableEdgeObjectCategoriesList = enableEdgeObjectCategoriesList ?? new List<BuildableEdgeObjectCategorySO>();
            gridAreaEnablerData.enableEdgeObjectsList = enableEdgeObjectsList ?? new List<BuildableEdgeObjectSO>();

            gridAreaEnablerData.enableAllCornerObjects = enableAllCornerObjects;
            gridAreaEnablerData.enableCornerObjectCategoriesList = enableCornerObjectCategoriesList ?? new List<BuildableCornerObjectCategorySO>();
            gridAreaEnablerData.enableCornerObjectsList = enableCornerObjectsList ?? new List<BuildableCornerObjectSO>();

            gridAreaEnablerData.enableAllFreeObjects = enableAllFreeObjects;
            gridAreaEnablerData.enableFreeObjectCategoriesList = enableFreeObjectCategoriesList ?? new List<BuildableFreeObjectCategorySO>();
            gridAreaEnablerData.enableFreeObjectsList = enableFreeObjectsList ?? new List<BuildableFreeObjectSO>();

            gridAreaEnablerData.changeBlockedCellColor = changeBlockedCellColor;
            gridAreaEnablerData.enabledCellHighlightColor = enabledCellHighlightColor;
        }
    }
}