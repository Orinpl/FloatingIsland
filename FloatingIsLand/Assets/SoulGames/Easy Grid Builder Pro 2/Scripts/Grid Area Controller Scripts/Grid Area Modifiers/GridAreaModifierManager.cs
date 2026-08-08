using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Area Modifier Manager", 5)]
    [RequireComponent(typeof(GridManager))]
    public class GridAreaModifierManager : MonoBehaviour
    {
        public static event OnGridAreaModifierManagerUpdatedDelegate OnGridAreaModifierManagerUpdated;
        public delegate void OnGridAreaModifierManagerUpdatedDelegate(GridModifierSO gridModifierSO, Dictionary<Vector2Int, float> modifiedCellValuesDictionary, Grid grid, bool affectAllVerticalGrids);

        private Dictionary<GridAreaModifier, GridAreaModifierData> gridAreaModifierDataDictionary;
        //private List<Vector2Int> currentOccupiedGridAreaModifierCellPositionList;

        void Start()
        {
            gridAreaModifierDataDictionary = new Dictionary<GridAreaModifier, GridAreaModifierData>();
            //currentOccupiedGridAreaModifierCellPositionList = new List<Vector2Int>();

            GridAreaModifier.OnGridAreaModifierInitialized += HandleGridAreaModifierInitialized;
            GridAreaModifier.OnGridAreaModifierUpdated += HandleGridAreaModifierUpdated;
            GridAreaModifier.OnGridAreaModifierDisabled += HandleGridAreaModifierDisabled;
        }

        private void OnDestroy()
        {
            GridAreaModifier.OnGridAreaModifierInitialized -= HandleGridAreaModifierInitialized;
            GridAreaModifier.OnGridAreaModifierUpdated -= HandleGridAreaModifierUpdated;
            GridAreaModifier.OnGridAreaModifierDisabled -= HandleGridAreaModifierDisabled;
        }

        private void HandleGridAreaModifierInitialized(GridAreaModifier gridAreaModifier, GridAreaModifierData gridAreaModifierData)
        {
            InitializeGridAreaModifierData(gridAreaModifier, gridAreaModifierData);
            UpdateGridAreaModifierData(gridAreaModifier, true);
        }

        private void HandleGridAreaModifierUpdated(GridAreaModifier gridAreaModifier)
        {
            UpdateGridAreaModifierData(gridAreaModifier, false);
        }

        private void HandleGridAreaModifierDisabled(GridAreaModifier gridAreaModifier)
        {
            ResetModifiedHeatMapCellsValues(gridAreaModifier);
        }

        private void InitializeGridAreaModifierData(GridAreaModifier gridAreaModifier, GridAreaModifierData gridAreaModifierData)
        {
            if (!gridAreaModifierDataDictionary.ContainsKey(gridAreaModifier)) gridAreaModifierDataDictionary[gridAreaModifier] = gridAreaModifierData;
        }

        private void UpdateGridAreaModifierData(GridAreaModifier gridAreaModifier, bool isCalledFromInitialize)
        {
            CalculateModifiedCellsColors(gridAreaModifier, isCalledFromInitialize);
        }

        private void CalculateModifiedCellsColors(GridAreaModifier gridAreaModifier, bool isCalledFromInitialize)
        {
            if (gridAreaModifierDataDictionary.TryGetValue(gridAreaModifier, out GridAreaModifierData gridAreaModifierData))
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaModifierData.GridAreaDataDictionary)
                {
                    GridAreaData data = gridAreaData.Value;
                    if (data.currentOccupiedEasyGridBuilderPro == null) continue;
                    if (data.initializedAsGridAreaModifier && isCalledFromInitialize) continue;

                    data.initializedAsGridAreaModifier = true;
                    List<float> cellValues = CalculateCellValues(gridAreaModifierData, data);
                    ModifyGridCellValues(cellValues, gridAreaModifierData, data);
                }
            }
        }

        private List<float> CalculateCellValues(GridAreaModifierData gridAreaModifierData, GridAreaData gridAreaData)
        {
            List<float> values = new List<float>();

            if (gridAreaModifierData.enableValueFallOff)
            {
                Vector2Int center = CalculateCenter(gridAreaData.currentOccupiedCellPositionList);
                float maxDistance = CalculateMaxDistance(center, gridAreaData.currentOccupiedCellPositionList);
                
                foreach (Vector2Int cellPosition in gridAreaData.currentOccupiedCellPositionList)
                {
                    float distance = Vector2Int.Distance(cellPosition, center);
                    float calculatedValue = gridAreaModifierData.modifierValueChangeAmount - ((gridAreaModifierData.modifierValueChangeAmount - gridAreaModifierData.gridModifierSO.minimumValue) * (distance / maxDistance));
                    values.Add(calculatedValue);
                }
            }
            else
            {
                foreach (Vector2Int cellPosition in gridAreaData.currentOccupiedCellPositionList)
                {
                    values.Add(gridAreaModifierData.modifierValueChangeAmount);
                }
            }

            return values;
        }

        Vector2Int CalculateCenter(List<Vector2Int> cellPositions)
        {
            int sumX = 0, sumY = 0;
            foreach (Vector2Int cell in cellPositions)
            {
                sumX += cell.x;
                sumY += cell.y;
            }
            return new Vector2Int(sumX / cellPositions.Count, sumY / cellPositions.Count);
        }

        float CalculateMaxDistance(Vector2Int center, List<Vector2Int> cellPositions)
        {
            float maxDistance = 0f;
            foreach (Vector2Int cell in cellPositions)
            {
                float distance = Vector2Int.Distance(cell, center);
                if (distance > maxDistance) maxDistance = distance;
            }
            return maxDistance;
        }

        private void ModifyGridCellValues(List<float> cellValues, GridAreaModifierData gridAreaModifierData, GridAreaData gridAreaData)
        {
            Dictionary<Vector2Int, float> modifiedCellValuesDictionary = new Dictionary<Vector2Int, float>();

            for (int i = 0; i < gridAreaData.currentOccupiedCellPositionList.Count; i++)
            {
                if (!gridAreaData.currentOccupiedEasyGridBuilderPro.IsWithinActiveGridBounds(gridAreaData.currentOccupiedCellPositionList[i])) continue;
                float currentValue = gridAreaData.currentOccupiedEasyGridBuilderPro.GetActiveGridCustomModifierValue(gridAreaData.currentOccupiedCellPositionList[i], gridAreaModifierData.gridModifierSO);
                
                switch (gridAreaModifierData.valueChangeType)
                {
                    case ValueChangeType.Fixed: currentValue = cellValues[i]; break;
                    case ValueChangeType.Addition: currentValue += cellValues[i]; break;
                    case ValueChangeType.Multiplication: currentValue *= gridAreaModifierData.modifierValueChangeAmount; break;
                }

                currentValue = Mathf.Clamp(currentValue, gridAreaModifierData.gridModifierSO.minimumValue, gridAreaModifierData.gridModifierSO.maximumValue);
                gridAreaData.currentOccupiedEasyGridBuilderPro.SetActiveGridCustomModifierValue(gridAreaData.currentOccupiedCellPositionList[i], gridAreaModifierData.gridModifierSO, currentValue);
                
                modifiedCellValuesDictionary.Add(gridAreaData.currentOccupiedCellPositionList[i], currentValue);
            }
            
            if (gridAreaData.affectAllVerticalGrids) OnGridAreaModifierManagerUpdated?.Invoke(gridAreaModifierData.gridModifierSO, modifiedCellValuesDictionary, gridAreaData.currentOccupiedGrid, true);
            else OnGridAreaModifierManagerUpdated?.Invoke(gridAreaModifierData.gridModifierSO, modifiedCellValuesDictionary, gridAreaData.currentOccupiedGrid, false);
        }

        private void ResetModifiedHeatMapCellsValues(GridAreaModifier gridAreaModifier)
        {
            if (gridAreaModifierDataDictionary.TryGetValue(gridAreaModifier, out GridAreaModifierData gridAreaModifierData))
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaModifierData.GridAreaDataDictionary)
                {
                    GridAreaData data = gridAreaData.Value;
                    if (data.currentOccupiedEasyGridBuilderPro == null) continue;

                    List<float> cellValues = CalculateCellValues(gridAreaModifierData, data);
                    ResetModifiedGridCellValues(cellValues, gridAreaModifierData, data);
                }
            }
        }

        private void ResetModifiedGridCellValues(List<float> cellValues, GridAreaModifierData gridAreaModifierData, GridAreaData gridAreaData)
        {
            Dictionary<Vector2Int, float> modifiedCellValuesDictionary = new Dictionary<Vector2Int, float>();

            for (int i = 0; i < gridAreaData.currentOccupiedCellPositionList.Count; i++)
            {
                if (!gridAreaData.currentOccupiedEasyGridBuilderPro.IsWithinActiveGridBounds(gridAreaData.currentOccupiedCellPositionList[i])) continue;
                float currentValue = gridAreaData.currentOccupiedEasyGridBuilderPro.GetActiveGridCustomModifierValue(gridAreaData.currentOccupiedCellPositionList[i], gridAreaModifierData.gridModifierSO);
                
                switch (gridAreaModifierData.valueChangeType)
                {
                    case ValueChangeType.Fixed: currentValue -= cellValues[i]; break;
                    case ValueChangeType.Addition: currentValue -= cellValues[i]; break;
                    case ValueChangeType.Multiplication: currentValue /= gridAreaModifierData.modifierValueChangeAmount; break;
                }

                currentValue = Mathf.Clamp(currentValue, gridAreaModifierData.gridModifierSO.minimumValue, gridAreaModifierData.gridModifierSO.maximumValue);
                gridAreaData.currentOccupiedEasyGridBuilderPro.SetActiveGridCustomModifierValue(gridAreaData.currentOccupiedCellPositionList[i], gridAreaModifierData.gridModifierSO, currentValue);
                
                modifiedCellValuesDictionary.Add(gridAreaData.currentOccupiedCellPositionList[i], currentValue);
            }
            
            if (gridAreaData.affectAllVerticalGrids) OnGridAreaModifierManagerUpdated?.Invoke(gridAreaModifierData.gridModifierSO, modifiedCellValuesDictionary, gridAreaData.currentOccupiedGrid, true);
            else OnGridAreaModifierManagerUpdated?.Invoke(gridAreaModifierData.gridModifierSO, modifiedCellValuesDictionary, gridAreaData.currentOccupiedGrid, false);
        }
    }
}