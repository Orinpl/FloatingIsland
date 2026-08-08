using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Area Disabler Manager", 6)]
    [RequireComponent(typeof(GridManager))]
    public class GridAreaDisablerManager : MonoBehaviour
    {
        private Dictionary<GridAreaDisabler, GridAreaDisablerData> gridAreaDisablerDataDictionary;
        private List<Vector2Int> currentOccupiedGridAreaDisablersCellPositionList;
        private bool isBasicGridAreaDisablerOccupied;

        void Start()
        {
            gridAreaDisablerDataDictionary = new Dictionary<GridAreaDisabler, GridAreaDisablerData>();
            currentOccupiedGridAreaDisablersCellPositionList = new List<Vector2Int>();
            
            BasicGridAreaTrigger.OnGhostObjectEnterBasicGridAreaDisabler += OnGhostObjectEnterBasicGridAreaDisabler;
            BasicGridAreaTrigger.OnGhostObjectExitBasicGridAreaDisabler += OnGhostObjectExitBasicGridAreaDisabler;

            GridAreaDisabler.OnGridAreaDisablerInitialized += HandleGridAreaDisablerInitialized;
            GridAreaDisabler.OnGridAreaDisablerUpdated += HandleGridAreaDisablerUpdated;
            GridAreaDisabler.OnGridAreaDisablerDisabled += HandleGridAreaDisablerDisabled;
        }

        private void OnDestroy()
        {
            BasicGridAreaTrigger.OnGhostObjectEnterBasicGridAreaDisabler -= OnGhostObjectEnterBasicGridAreaDisabler;
            BasicGridAreaTrigger.OnGhostObjectExitBasicGridAreaDisabler -= OnGhostObjectExitBasicGridAreaDisabler;

            GridAreaDisabler.OnGridAreaDisablerInitialized -= HandleGridAreaDisablerInitialized;
            GridAreaDisabler.OnGridAreaDisablerUpdated -= HandleGridAreaDisablerUpdated;
            GridAreaDisabler.OnGridAreaDisablerDisabled -= HandleGridAreaDisablerDisabled;
        }

        private void OnGhostObjectEnterBasicGridAreaDisabler() => isBasicGridAreaDisablerOccupied = true;
        
        private void OnGhostObjectExitBasicGridAreaDisabler() => isBasicGridAreaDisablerOccupied = false;

        private void HandleGridAreaDisablerInitialized(GridAreaDisabler gridAreaDisabler, GridAreaDisablerData gridAreaDisablerData)
        {
            InitializeGridAreaDisablerData(gridAreaDisabler, gridAreaDisablerData);
            UpdateGridAreaDisablerData(gridAreaDisabler);
        }

        private void HandleGridAreaDisablerUpdated(GridAreaDisabler gridAreaDisabler)
        {
            UpdateGridAreaDisablerData(gridAreaDisabler);
        }

        private void HandleGridAreaDisablerDisabled(GridAreaDisabler gridAreaDisabler)
        {
            CalculateBlockedCellsColors(gridAreaDisabler, true);
        }

        private void InitializeGridAreaDisablerData(GridAreaDisabler gridAreaDisabler, GridAreaDisablerData gridAreaDisablerData)
        {
            if (!gridAreaDisablerDataDictionary.ContainsKey(gridAreaDisabler)) gridAreaDisablerDataDictionary[gridAreaDisabler] = gridAreaDisablerData;
        }

        private void UpdateGridAreaDisablerData(GridAreaDisabler gridAreaDisabler)
        {
            CalculateBlockedCellsColors(gridAreaDisabler, false);
        }

        private void CalculateBlockedCellsColors(GridAreaDisabler gridAreaDisabler, bool isCalledFromOnDisable)
        {
            if (gridAreaDisablerDataDictionary.TryGetValue(gridAreaDisabler, out GridAreaDisablerData gridAreaDisablerData))
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaDisablerData.GridAreaDataDictionary)
                {
                    GridAreaData data = gridAreaData.Value;
                    if (data.currentOccupiedEasyGridBuilderPro == null) continue;
                    
                    if (gridAreaDisablerData.changeBlockedCellColor)
                    {
                        if (isCalledFromOnDisable)
                        {
                            SetGeneratedTextureCellsToDefault(data.currentOccupiedEasyGridBuilderPro, data.currentOccupiedGrid, data.affectAllVerticalGrids, data.currentOccupiedCellPositionList);
                            continue;
                        }

                        ResetBlockedCellsColor(data.currentOccupiedEasyGridBuilderPro, data.previousOccupiedEasyGridBuilderPro, data.currentOccupiedGrid, data.previousOccupiedGrid,
                                                ref data.currentGridChangedDynamicTrigger, ref data.currentEasyGridBuilderProChangedDynamicTrigger,
                                                data.affectAllVerticalGrids, data.previousGridAllOccupiedCellPositionList, data.previousOccupiedCellPositionList);

                        SetBlockedCellsColor(data.currentOccupiedEasyGridBuilderPro, data.currentOccupiedGrid, data.affectAllVerticalGrids, data.currentOccupiedCellPositionList,
                                            gridAreaDisablerData.blockedCellHighlightColor);
                    }
                }
            }
        }
        
        private void SetBlockedCellsColor(EasyGridBuilderPro easyGridBuilderPro, Grid grid, bool blockAllVerticalGrids, List<Vector2Int> blockedCellPositions, Color color)
        {
            if (!easyGridBuilderPro.GetIsDisplayObjectGrid()) return;

            foreach (Vector2Int cellPosition in blockedCellPositions)
            {
                if (GridManager.Instance.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
                {
                    if (gridAreaEnablerManager.GetCurrentOccupiedGridAreaEnablersCellPositionList().Contains(cellPosition)) continue;
                }
                easyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellColor(cellPosition, color, blockAllVerticalGrids, grid);
                if (!currentOccupiedGridAreaDisablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaDisablersCellPositionList.Add(cellPosition);
            }
        }

        private void ResetBlockedCellsColor(EasyGridBuilderPro currentEasyGridBuilderPro, EasyGridBuilderPro previousEasyGridBuilderPro, Grid grid, Grid previousGrid,
                                            ref bool currentGridChangedDynamicTrigger, ref bool currentEasyGridBuilderProChangedDynamicTrigger, bool blockAllVerticalGrids,
                                            List<Vector2Int> previousGridAllOccupiedCellPositionList, List<Vector2Int> previousBlockedCellPositions)
        {
            if (!currentEasyGridBuilderPro.GetIsDisplayObjectGrid()) return;

            foreach (Vector2Int cellPosition in previousBlockedCellPositions)
            {
                if (GridManager.Instance.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
                {
                    if (gridAreaEnablerManager.GetCurrentOccupiedGridAreaEnablersCellPositionList().Contains(cellPosition)) continue;
                }
                currentEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, blockAllVerticalGrids, grid);
                if (currentOccupiedGridAreaDisablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaDisablersCellPositionList.Remove(cellPosition);
            }

            if (previousGrid != grid && currentGridChangedDynamicTrigger == true)
            {
                currentGridChangedDynamicTrigger = false;
                foreach (Vector2Int cellPosition in previousGridAllOccupiedCellPositionList)
                {
                    if (GridManager.Instance.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
                    {
                        if (gridAreaEnablerManager.GetCurrentOccupiedGridAreaEnablersCellPositionList().Contains(cellPosition)) continue;
                    }
                    currentEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, blockAllVerticalGrids, previousGrid);
                    if (currentOccupiedGridAreaDisablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaDisablersCellPositionList.Remove(cellPosition);
                }
            }

            if (previousEasyGridBuilderPro != currentEasyGridBuilderPro && currentEasyGridBuilderProChangedDynamicTrigger == true)
            {
                currentEasyGridBuilderProChangedDynamicTrigger = false;
                foreach (Vector2Int cellPosition in previousGridAllOccupiedCellPositionList)
                {
                    if (GridManager.Instance.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
                    {
                        if (gridAreaEnablerManager.GetCurrentOccupiedGridAreaEnablersCellPositionList().Contains(cellPosition)) continue;
                    }
                    previousEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, blockAllVerticalGrids, previousGrid);
                    if (currentOccupiedGridAreaDisablersCellPositionList.Contains(cellPosition)) currentOccupiedGridAreaDisablersCellPositionList.Remove(cellPosition);
                }
            }
        }

        private void SetGeneratedTextureCellsToDefault(EasyGridBuilderPro currentEasyGridBuilderPro, Grid grid, bool blockAllVerticalGrids, List<Vector2Int> blockedCellPositions)
        {
            if (!currentEasyGridBuilderPro.GetIsDisplayObjectGrid()) return;

            foreach (Vector2Int cellPosition in blockedCellPositions)
            {
                if (GridManager.Instance.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
                {
                    if (gridAreaEnablerManager.GetCurrentOccupiedGridAreaEnablersCellPositionList().Contains(cellPosition)) continue;
                }
                currentEasyGridBuilderPro.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition, blockAllVerticalGrids, grid);
            }
        }

        public bool IsBuildableGridObjectBlockedByGridAreaDisablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableGridObjectSO buildableGridObjectSO, Vector2Int cellPosition)
        {
            BuildableGridObjectCategorySO buildableGridObjectCategorySO = buildableGridObjectSO.buildableGridObjectCategorySO;

            foreach (KeyValuePair<GridAreaDisabler, GridAreaDisablerData> gridAreaDisablerData in gridAreaDisablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaDisablerData.Value.GridAreaDataDictionary)
                {
                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
                    {
                        if (gridAreaDisablerData.Value.blockAllGridObjects) return true;
                        else if (gridAreaDisablerData.Value.blockGridObjectCategoriesList.Contains(buildableGridObjectCategorySO)) return true;
                        else if (gridAreaDisablerData.Value.blockGridObjectsList.Contains(buildableGridObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableEdgeObjectBlockedByGridAreaDisablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableEdgeObjectSO buildableEdgeObjectSO, Vector2Int cellPosition, EdgeObjectCellDirection edgeObjectCellDirection)
        {
            BuildableEdgeObjectCategorySO buildableEdgeObjectCategorySO = buildableEdgeObjectSO.buildableEdgeObjectCategorySO;
            
            foreach (KeyValuePair<GridAreaDisabler, GridAreaDisablerData> gridAreaDisablerData in gridAreaDisablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaDisablerData.Value.GridAreaDataDictionary)
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

                        if (gridAreaDisablerData.Value.blockAllGridObjects) return true;
                        else if (gridAreaDisablerData.Value.blockEdgeObjectCategoriesList.Contains(buildableEdgeObjectCategorySO)) return true;
                        else if (gridAreaDisablerData.Value.blockEdgeObjectsList.Contains(buildableEdgeObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableCornerObjectBlockedByGridAreaDisablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableCornerObjectSO buildableCornerObjectSO, Vector2Int cellPosition, CornerObjectCellDirection cornerObjectCellDirection)
        {
            BuildableCornerObjectCategorySO buildableCornerObjectCategorySO = buildableCornerObjectSO.buildableCornerObjectCategorySO;

            foreach (KeyValuePair<GridAreaDisabler, GridAreaDisablerData> gridAreaDisablerData in gridAreaDisablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaDisablerData.Value.GridAreaDataDictionary)
                {
                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
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

                        if (gridAreaDisablerData.Value.blockAllGridObjects) return true;
                        else if (gridAreaDisablerData.Value.blockCornerObjectCategoriesList.Contains(buildableCornerObjectCategorySO)) return true;
                        else if (gridAreaDisablerData.Value.blockCornerObjectsList.Contains(buildableCornerObjectSO)) return true;
                    }
                }
            }
            return false;
        }


        public bool IsBuildableFreeObjectBlockedByGridAreaDisablers(EasyGridBuilderPro easyGridBuilderPro, Grid grid, BuildableFreeObjectSO buildableFreeObjectSO, Vector2Int cellPosition)
        {
            BuildableFreeObjectCategorySO buildableFreeObjectCategorySO = buildableFreeObjectSO.buildableFreeObjectCategorySO;

            foreach (KeyValuePair<GridAreaDisabler, GridAreaDisablerData> gridAreaDisablerData in gridAreaDisablerDataDictionary)
            {
                foreach (KeyValuePair<GridArea, GridAreaData> gridAreaData in gridAreaDisablerData.Value.GridAreaDataDictionary)
                {
                    if (easyGridBuilderPro == gridAreaData.Value.currentOccupiedEasyGridBuilderPro && grid == gridAreaData.Value.currentOccupiedGrid && gridAreaData.Value.currentOccupiedCellPositionList.Contains(cellPosition))
                    {
                        if (gridAreaDisablerData.Value.blockAllFreeObjects) return true;
                        else if (gridAreaDisablerData.Value.blockFreeObjectCategoriesList.Contains(buildableFreeObjectCategorySO)) return true;
                        else if (gridAreaDisablerData.Value.blockFreeObjectsList.Contains(buildableFreeObjectSO)) return true;
                    }
                }
            }
            return false;
        }

        public bool IsBuildableObjectBlockedByBasicGridAreaDisablers() => isBasicGridAreaDisablerOccupied;

        public List<Vector2Int> GetCurrentOccupiedGridAreaDisablersCellPositionList() => currentOccupiedGridAreaDisablersCellPositionList;
    }
}