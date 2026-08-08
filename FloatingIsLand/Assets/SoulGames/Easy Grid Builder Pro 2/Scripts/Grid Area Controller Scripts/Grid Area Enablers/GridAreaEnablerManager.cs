using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Area Enabler Manager", 7)]
    [RequireComponent(typeof(GridManager))]
    public class GridAreaEnablerManager : MonoBehaviour
    {
        private Dictionary<GridAreaEnabler, GridAreaEnablerData> gridAreaEnablerDataDictionary;
        private List<Vector2Int> currentOccupiedGridAreaEnablersCellPositionList;
        private bool isBasicGridAreaEnablerOccupied;

        void Start()
        {
            gridAreaEnablerDataDictionary = new Dictionary<GridAreaEnabler, GridAreaEnablerData>();
            currentOccupiedGridAreaEnablersCellPositionList = new List<Vector2Int>();
            
            BasicGridAreaTrigger.OnGhostObjectEnterBasicGridAreaEnabler += OnGhostObjectEnterBasicGridAreaEnabler;
            BasicGridAreaTrigger.OnGhostObjectExitBasicGridAreaEnabler += OnGhostObjectExitBasicGridAreaEnabler;

            GridAreaEnabler.OnGridAreaEnablerInitialized += HandleGridAreaEnablerInitialized;
            GridAreaEnabler.OnGridAreaEnablerUpdated += HandleGridAreaEnablerUpdated;
            GridAreaEnabler.OnGridAreaEnablerDisabled += HandleGridAreaEnablerDisabled;
        }

        private void OnDestroy()
        {
            BasicGridAreaTrigger.OnGhostObjectEnterBasicGridAreaEnabler -= OnGhostObjectEnterBasicGridAreaEnabler;
            BasicGridAreaTrigger.OnGhostObjectExitBasicGridAreaEnabler -= OnGhostObjectExitBasicGridAreaEnabler;

            GridAreaEnabler.OnGridAreaEnablerInitialized -= HandleGridAreaEnablerInitialized;
            GridAreaEnabler.OnGridAreaEnablerUpdated -= HandleGridAreaEnablerUpdated;
            GridAreaEnabler.OnGridAreaEnablerDisabled -= HandleGridAreaEnablerDisabled;
        }

        private void OnGhostObjectEnterBasicGridAreaEnabler() => isBasicGridAreaEnablerOccupied = true;
        
        private void OnGhostObjectExitBasicGridAreaEnabler() => isBasicGridAreaEnablerOccupied = false;

        private void HandleGridAreaEnablerInitialized(GridAreaEnabler gridAreaEnabler, GridAreaEnablerData gridAreaEnablerData)
        {
            InitializeGridAreaEnablerData(gridAreaEnabler, gridAreaEnablerData);
            UpdateGridAreaEnablerData(gridAreaEnabler);
        }

        private void HandleGridAreaEnablerUpdated(GridAreaEnabler gridAreaEnabler)
        {
            UpdateGridAreaEnablerData(gridAreaEnabler);
        }

        private void HandleGridAreaEnablerDisabled(GridAreaEnabler gridAreaEnabler)
        {
            CalculateEnabledCellsColors(gridAreaEnabler, true);
        }

        private void InitializeGridAreaEnablerData(GridAreaEnabler gridAreaEnabler, GridAreaEnablerData gridAreaEnablerData)
        {
            if (!gridAreaEnablerDataDictionary.ContainsKey(gridAreaEnabler)) gridAreaEnablerDataDictionary[gridAreaEnabler] = gridAreaEnablerData;
        }

        private void UpdateGridAreaEnablerData(GridAreaEnabler gridAreaEnabler)
        {
            CalculateEnabledCellsColors(gridAreaEnabler, false);
        }

        private void CalculateEnabledCellsColors(GridAreaEnabler gridAreaEnabler, bool isCalledFromOnDisable)
        {
            if (gridAreaEnablerDataDictionary.TryGetValue(gridAreaEnabler, out GridAreaEnablerData gridAreaEnablerData))
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaEnablerData.GridAreaDataDictionary)
                {
                    GridAreaData data = gridAreaData.Value;
                    if (data.currentOccupiedEasyGridBuilderPro == null) continue;

                    if (gridAreaEnablerData.changeBlockedCellColor)
                    {
                        if (isCalledFromOnDisable)
                        {
                            SetGeneratedTextureCellsToDefault(data.currentOccupiedEasyGridBuilderPro, data.currentOccupiedGrid, data.affectAllVerticalGrids, data.currentOccupiedCellPositionList);
                            continue;
                        }

                        ResetEnabledCellsColor(data.currentOccupiedEasyGridBuilderPro, data.previousOccupiedEasyGridBuilderPro, data.currentOccupiedGrid, data.previousOccupiedGrid,
                                                ref data.currentGridChangedDynamicTrigger, ref data.currentEasyGridBuilderProChangedDynamicTrigger,
                                                data.affectAllVerticalGrids, data.previousGridAllOccupiedCellPositionList, data.previousOccupiedCellPositionList);

                        SetEnabledCellsColor(data.currentOccupiedEasyGridBuilderPro, data.currentOccupiedGrid, data.affectAllVerticalGrids, data.currentOccupiedCellPositionList,
                                            gridAreaEnablerData.enabledCellHighlightColor);
                    }
                }
            }
        }
        
        private void SetEnabledCellsColor(EasyGridBuilderPro easyGridBuilderPro, Grid grid, bool affectAllVerticalGrids, List<Vector2Int> enabledCellPositions, Color color)
        {
            if (!easyGridBuilderPro.GetIsDisplayObjectGrid()) return;

            foreach (Vector2Int cellPosition in enabledCellPositions)
            {
                easyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellColor(cellPosition, color, affectAllVerticalGrids, grid);
                if (!currentOccupiedGridAreaEnablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaEnablersCellPositionList.Add(cellPosition);
            }
        }

        private void ResetEnabledCellsColor(EasyGridBuilderPro currentEasyGridBuilderPro, EasyGridBuilderPro previousEasyGridBuilderPro, Grid grid, Grid previousGrid,
                                            ref bool currentGridChangedDynamicTrigger, ref bool currentEasyGridBuilderProChangedDynamicTrigger, bool affectAllVerticalGrids,
                                            List<Vector2Int> previousGridAllOccupiedCellPositionList, List<Vector2Int> previousEnabledellPositions)
        {
            if (!currentEasyGridBuilderPro.GetIsDisplayObjectGrid()) return;

            foreach (Vector2Int cellPosition in previousEnabledellPositions)
            {
                currentEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, affectAllVerticalGrids, grid);
                if (currentOccupiedGridAreaEnablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaEnablersCellPositionList.Remove(cellPosition);
            }

            if (previousGrid != grid && currentGridChangedDynamicTrigger == true)
            {
                currentGridChangedDynamicTrigger = false;
                foreach (Vector2Int cellPosition in previousGridAllOccupiedCellPositionList)
                {
                    currentEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, affectAllVerticalGrids, previousGrid);
                    if (currentOccupiedGridAreaEnablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaEnablersCellPositionList.Remove(cellPosition);
                }
            }

            if (previousEasyGridBuilderPro != currentEasyGridBuilderPro && currentEasyGridBuilderProChangedDynamicTrigger == true)
            {
                currentEasyGridBuilderProChangedDynamicTrigger = false;
                foreach (Vector2Int cellPosition in previousGridAllOccupiedCellPositionList)
                {
                    previousEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, affectAllVerticalGrids, previousGrid);
                    if (currentOccupiedGridAreaEnablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaEnablersCellPositionList.Remove(cellPosition);
                }
            }
        }

        private void SetGeneratedTextureCellsToDefault(EasyGridBuilderPro currentEasyGridBuilderPro, Grid grid, bool blockAllVerticalGrids, List<Vector2Int> blockedCellPositions)
        {
            if (!currentEasyGridBuilderPro.GetIsDisplayObjectGrid()) return;

            foreach (Vector2Int cellPosition in blockedCellPositions)
            {
                currentEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, blockAllVerticalGrids, grid);
            }
        }

        public bool IsBuildableGridObjectEnabledByGridAreaEnablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableGridObjectSO buildableGridObjectSO, Vector2Int cellPosition)
        {
            BuildableGridObjectCategorySO buildableGridObjectCategorySO = buildableGridObjectSO.buildableGridObjectCategorySO;

            foreach (KeyValuePair<GridAreaEnabler, GridAreaEnablerData> gridAreaEnablerData in gridAreaEnablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaEnablerData.Value.GridAreaDataDictionary)
                {
                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
                    {
                        if (gridAreaEnablerData.Value.enableAllGridObjects) return true;
                        else if (gridAreaEnablerData.Value.enableGridObjectCategoriesList.Contains(buildableGridObjectCategorySO)) return true;
                        else if (gridAreaEnablerData.Value.enableGridObjectsList.Contains(buildableGridObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableEdgeObjectEnabledByGridAreaEnablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableEdgeObjectSO buildableEdgeObjectSO, Vector2Int cellPosition, EdgeObjectCellDirection edgeObjectCellDirection)
        {
            BuildableEdgeObjectCategorySO buildableEdgeObjectCategorySO = buildableEdgeObjectSO.buildableEdgeObjectCategorySO;

            foreach (KeyValuePair<GridAreaEnabler, GridAreaEnablerData> gridAreaEnablerData in gridAreaEnablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaEnablerData.Value.GridAreaDataDictionary)
                {
                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
                    {
                        switch (edgeObjectCellDirection)
                        {
                            case EdgeObjectCellDirection.North: if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x, cellPosition.y + 1))) continue; break;
                            case EdgeObjectCellDirection.East: if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x + 1, cellPosition.y))) continue; break;
                            case EdgeObjectCellDirection.South: if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x, cellPosition.y - 1))) continue; break;
                            case EdgeObjectCellDirection.West: if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x - 1, cellPosition.y))) continue; break;
                        }

                        if (gridAreaEnablerData.Value.enableAllGridObjects) return true;
                        else if (gridAreaEnablerData.Value.enableEdgeObjectCategoriesList.Contains(buildableEdgeObjectCategorySO)) return true;
                        else if (gridAreaEnablerData.Value.enableEdgeObjectsList.Contains(buildableEdgeObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableCornerObjectEnabledByGridAreaEnablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableCornerObjectSO buildableCornerObjectSO, Vector2Int cellPosition, CornerObjectCellDirection cornerObjectCellDirection)
        {
            BuildableCornerObjectCategorySO buildableCornerObjectCategorySO = buildableCornerObjectSO.buildableCornerObjectCategorySO;

            foreach (KeyValuePair<GridAreaEnabler, GridAreaEnablerData> gridAreaEnablerData in gridAreaEnablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaEnablerData.Value.GridAreaDataDictionary)
                {
                        switch (cornerObjectCellDirection)
                        {
                            case CornerObjectCellDirection.NorthEast: 
                                if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x, cellPosition.y + 1)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x + 1, cellPosition.y + 1)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x + 1, cellPosition.y))) continue; 
                            break;
                            case CornerObjectCellDirection.SouthEast: 
                                if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x + 1, cellPosition.y)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x + 1, cellPosition.y - 1)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x, cellPosition.y - 1))) continue; 
                            break;
                            case CornerObjectCellDirection.SouthWest:                                 
                                if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x, cellPosition.y - 1)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x - 1, cellPosition.y - 1)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x - 1, cellPosition.y))) continue; 
                            break;
                            case CornerObjectCellDirection.NorthWest: 
                                if (!gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x - 1, cellPosition.y)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x - 1, cellPosition.y + 1)) ||
                                    !gridAreaData.Value.currentOccupiedCellPositionList.Contains(new Vector2Int(cellPosition.x, cellPosition.y + 1))) continue; 
                            break;
                        }

                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
                    {
                        if (gridAreaEnablerData.Value.enableAllGridObjects) return true;
                        else if (gridAreaEnablerData.Value.enableCornerObjectCategoriesList.Contains(buildableCornerObjectCategorySO)) return true;
                        else if (gridAreaEnablerData.Value.enableCornerObjectsList.Contains(buildableCornerObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableFreeObjectEnabledByGridAreaEnablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableFreeObjectSO buildableFreeObjectSO, Vector2Int cellPosition)
        {
            BuildableFreeObjectCategorySO buildableFreeObjectCategorySO = buildableFreeObjectSO.buildableFreeObjectCategorySO;

            foreach (KeyValuePair<GridAreaEnabler, GridAreaEnablerData> gridAreaEnablerData in gridAreaEnablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaEnablerData.Value.GridAreaDataDictionary)
                {
                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
                    {
                        if (gridAreaEnablerData.Value.enableAllFreeObjects) return true;
                        else if (gridAreaEnablerData.Value.enableFreeObjectCategoriesList.Contains(buildableFreeObjectCategorySO)) return true;
                        else if (gridAreaEnablerData.Value.enableFreeObjectsList.Contains(buildableFreeObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableObjectEnabledByBasicGridAreaEnablers() => isBasicGridAreaEnablerOccupied;

        public List<Vector2Int> GetCurrentOccupiedGridAreaEnablersCellPositionList() => currentOccupiedGridAreaEnablersCellPositionList;
    }
}