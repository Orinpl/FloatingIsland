using System.Collections;
using System.Collections.Generic;
using SoulGames.Utilities;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Grid Area Modifier", 6)]
    public class GridAreaModifier : MonoBehaviour
    {
        public static event OnGridAreaModifierInitializedDelegate OnGridAreaModifierInitialized;
        public delegate void OnGridAreaModifierInitializedDelegate(GridAreaModifier gridAreaModifier, GridAreaModifierData gridAreaModifierData);

        public static event OnGridAreaModifierUpdatedDelegate OnGridAreaModifierUpdated;
        public delegate void OnGridAreaModifierUpdatedDelegate(GridAreaModifier gridAreaModifier);

        public static event OnGridAreaModifierDisabledDelegate OnGridAreaModifierDisabled;
        public delegate void OnGridAreaModifierDisabledDelegate(GridAreaModifier gridAreaModifier);

        [SerializeField] private List<GridArea> gridAreaList;
        
        [SerializeField] private GridModifierSO gridModifierSO;
        [SerializeField] private float modifierValueChangeAmount;
        [SerializeField] private ValueChangeType valueChangeType = ValueChangeType.Fixed;
        [SerializeField] private bool enableValueFallOff;
        [SerializeField] private bool resetValuesOnDestroy = false;

        [SerializeField] private bool updateWhenTransformChange = false;
        [SerializeField] private bool updateConstantly = false;
        [SerializeField] private float updateInterval = 0.2f;

        public Dictionary<GridArea, GridAreaData> GridAreaDataDictionary;
        private GridAreaModifierData gridAreaModifierData;
        private bool isInstantiatedByGhostObject;
        private int tickCount;
        private const float UPDATE_INTERVAL_MULTIPLIER = 10f; // Global Tick trigger every 0.1 second, Multiplying the value by 10 provides a second.
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            updateInterval = Mathf.Round(updateInterval * 10f) / 10f;
        }
        #endif

        private void Start()
        {
            BuildableObject buildableObject = GetComponentInParent<BuildableObject>();
            if (buildableObject) isInstantiatedByGhostObject = buildableObject.GetIsInstantiatedByGhostObject();
            if (isInstantiatedByGhostObject) return;

            GridAreaDataDictionary = new Dictionary<GridArea, GridAreaData>();
            gridAreaModifierData = new GridAreaModifierData();

            GridArea.OnGridAreaInitialized += OnGridAreaInitialized;
            GridArea.OnGridAreaUpdated += OnGridAreaUpdated;
            GlobalTimeTickManager.OnGlobalTimeTickManagerTick += OnGlobalTimeTickManagerTick;
        }

        private void OnEnable()
        {
            StartCoroutine(LateOnEnable());
        }

        private IEnumerator LateOnEnable()
        {
            yield return new WaitForEndOfFrame();
            if (isInstantiatedByGhostObject) yield break;

            foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in GridAreaDataDictionary)
            {
                gridAreaData.Value.initializedAsGridAreaModifier = false;
            }
            OnGridAreaModifierInitialized?.Invoke(this, gridAreaModifierData);
        }

        private void OnDestroy()
        {
            if (isInstantiatedByGhostObject) return;

            GridArea.OnGridAreaInitialized -= OnGridAreaInitialized;
            GridArea.OnGridAreaUpdated -= OnGridAreaUpdated;
            GlobalTimeTickManager.OnGlobalTimeTickManagerTick -= OnGlobalTimeTickManagerTick;
        }

        private void OnGlobalTimeTickManagerTick()
        {
            tickCount ++;
            if (updateConstantly)
            {
                if (tickCount < updateInterval * UPDATE_INTERVAL_MULTIPLIER) return;
                
                OnGridAreaModifierUpdated?.Invoke(this);
                tickCount = 0;
            }
        }

        private void OnDisable()
        {
            if (isInstantiatedByGhostObject) return;
            if (resetValuesOnDestroy) OnGridAreaModifierDisabled?.Invoke(this);
        }

        private void OnGridAreaInitialized(GridArea gridArea, GridAreaData gridAreaData)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            
            InitializeGridAreaModifierData(gridArea, gridAreaData);
            PopulateGridAreaModifierData();
            OnGridAreaModifierInitialized?.Invoke(this, gridAreaModifierData);
        }

        private void OnGridAreaUpdated(GridArea gridArea)
        {
            if (!gridAreaList.Contains(gridArea)) return;

            if (updateWhenTransformChange)
            {
                if (tickCount < updateInterval * UPDATE_INTERVAL_MULTIPLIER) return;

                UpdateGridAreaModifierData(gridArea);
                PopulateGridAreaModifierData();
                OnGridAreaModifierUpdated?.Invoke(this);

                tickCount = 0;
            }
        }

        private void InitializeGridAreaModifierData(GridArea gridArea, GridAreaData gridAreaData)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            if (!GridAreaDataDictionary.ContainsKey(gridArea)) GridAreaDataDictionary[gridArea] = gridAreaData;
        }

        private void UpdateGridAreaModifierData(GridArea gridArea)
        {
            if (!gridAreaList.Contains(gridArea)) return;
            PopulateGridAreaModifierData();
        }

        private void PopulateGridAreaModifierData()
        {
            gridAreaModifierData.GridAreaDataDictionary = GridAreaDataDictionary ?? new Dictionary<GridArea, GridAreaData>();

            gridAreaModifierData.gridModifierSO = gridModifierSO;
            gridAreaModifierData.modifierValueChangeAmount = modifierValueChangeAmount;
            gridAreaModifierData.valueChangeType = valueChangeType;
            gridAreaModifierData.enableValueFallOff = enableValueFallOff;
            gridAreaModifierData.resetValuesOnDestroy = resetValuesOnDestroy;
        }
    }
}