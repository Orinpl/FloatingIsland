using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Save And Load Manager", 3)]
    [RequireComponent(typeof(GridManager))]
    public class GridSaveAndLoadManager : MonoBehaviour
    {
        [SerializeField] private string localSavePath = "/SoulGames/Easy Grid Builder Pro 2/EGB Pro 2 Local Saves";
        [SerializeField] private string saveFileName = "/EGB Pro 2 Save";
        [SerializeField] private string saveExtention = ".txt";

        [SerializeField] private bool saveAndLoadGridSystemData = true;
        [SerializeField] private bool saveAndLoadBuiltObjectsData = true;

        public void SetInputSave()
        {
            EasyGridBuilderProSaveSystem.Save();
        }

        public void SetInputLoad()
        {
            EasyGridBuilderProSaveSystem.Load();
        }

        public void Save(ref GridSystemsSaveData easyGridBuilderProSystemsSaveData, ref BuildableObjectsSaveData buildableObjectsSaveData)
        {
            if (saveAndLoadGridSystemData) easyGridBuilderProSystemsSaveData.gridSaveDataList = HandleSaveGridSystemData();
            if (saveAndLoadBuiltObjectsData) buildableObjectsSaveData.builtObjectSaveDataList = HandleSaveBuiltObjectsData();

        }
        
        public void Load(GridSystemsSaveData easyGridBuilderProSystemsSaveData, BuildableObjectsSaveData buildableObjectsSaveData)
        {
            if (saveAndLoadGridSystemData) HandleLoadGridSystemData(easyGridBuilderProSystemsSaveData);
            if (saveAndLoadBuiltObjectsData) HandleLoadBuiltObjectsData(buildableObjectsSaveData);
        }

        private List<GridSaveData> HandleSaveGridSystemData()
        {
            List<GridSaveData> gridSaveDataList = new List<GridSaveData>();
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                GridSaveData gridSaveData = new GridSaveData();

                gridSaveData.gridUniqueID = easyGridBuilderPro.GetGridUniqueID();
                gridSaveData.gridPosition = easyGridBuilderPro.transform.position;
                gridSaveData.gridWidth = easyGridBuilderPro.GetGridWidth();
                gridSaveData.gridLength = easyGridBuilderPro.GetGridLength();
                gridSaveData.cellSize = easyGridBuilderPro.GetCellSize();
                gridSaveData.updateGridPositionRuntime = easyGridBuilderPro.GetIsUpdateGridPositionRuntime();
                gridSaveData.updateGridWidthAndLengthRuntime = easyGridBuilderPro.GetIsUpdateGridWidthAndLengthRuntime();
                gridSaveData.gridOriginType = easyGridBuilderPro.GetGridOriginType();

                gridSaveData.buildableGridObjectSOList = easyGridBuilderPro.GetBuildableGridObjectSOList();
                gridSaveData.buildableEdgeObjectSOList = easyGridBuilderPro.GetBuildableEdgeObjectSOList();
                gridSaveData.buildableCornerObjectSOList = easyGridBuilderPro.GetBuildableCornerObjectSOList();
                gridSaveData.buildableFreeObjectSOList = easyGridBuilderPro.GetBuildableFreeObjectSOList();

                gridSaveDataList.Add(gridSaveData);
            }

            return gridSaveDataList;
        }

        private void HandleLoadGridSystemData(GridSystemsSaveData gridSystemsSaveData)
        {
            foreach (GridSaveData gridSaveData in gridSystemsSaveData.gridSaveDataList)
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    if (easyGridBuilderPro.GetGridUniqueID() != gridSaveData.gridUniqueID) return;

                    easyGridBuilderPro.transform.position = gridSaveData.gridPosition;
                    easyGridBuilderPro.SetGridWidthAndLength(gridSaveData.gridWidth, gridSaveData.gridLength);
                    easyGridBuilderPro.SetCellSize(gridSaveData.cellSize);
                    easyGridBuilderPro.SetUpdateGridPositionRuntime(gridSaveData.updateGridPositionRuntime);
                    easyGridBuilderPro.SetUpdateGridWidthAndLengthRuntime(gridSaveData.updateGridWidthAndLengthRuntime);
                    easyGridBuilderPro.SetGridOriginType(gridSaveData.gridOriginType);

                    easyGridBuilderPro.SetBuildableObjectSOLists(gridSaveData.buildableGridObjectSOList, gridSaveData.buildableEdgeObjectSOList, gridSaveData.buildableCornerObjectSOList, 
                        gridSaveData.buildableFreeObjectSOList);
                }
            }
        }

        private List<BuiltObjectSaveData> HandleSaveBuiltObjectsData()
        {
            List<BuiltObjectSaveData> builtObjectsSaveDataList = new List<BuiltObjectSaveData>();
            GridManager.Instance.TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager);

            foreach (BuildableObject buildableObject in gridBuiltObjectsManager.GetBuiltObjectsList())
            {
                BuiltObjectSaveData builtObjectsSaveData = new BuiltObjectSaveData();

                builtObjectsSaveData.buildableObjectUniqueID = buildableObject.GetUniqueID();

                builtObjectsSaveData.gridAxis = buildableObject.GetGridAxis();
                builtObjectsSaveData.activateGizmos = buildableObject.GetIsActivateGizmos();
                builtObjectsSaveData.lockAutoGenerationAndValues = buildableObject.GetLockAutoGenerationAndValues();
                builtObjectsSaveData.objectScale = buildableObject.GetObjectScale();
                builtObjectsSaveData.objectCenter = buildableObject.GetObjectCenter();
                builtObjectsSaveData.objectCustomPivot = buildableObject.GetObjectCustomPivot();
                builtObjectsSaveData.debugCellSize = buildableObject.GetDebugCellSize();
                builtObjectsSaveData.objectSizeRelativeToCellSize = buildableObject.GetObjectSizeRelativeToCellSize();
                builtObjectsSaveData.activeSceneObject = buildableObject.GetIsActiveSceneObject(out BuildableObjectSO buildableObjectSO);
                builtObjectsSaveData.sceneObjectBuildableCornerObjectSO = buildableObjectSO;
                buildableObject.GetSceneObjectRotation(out FourDirectionalRotation fourDirectionalRotation, out EightDirectionalRotation eightDirectionalRotation, out float freeRotation);
                builtObjectsSaveData.sceneObjectFourDirectionalRotation = fourDirectionalRotation;
                builtObjectsSaveData.sceneObjectEightDirectionalRotation = eightDirectionalRotation;
                builtObjectsSaveData.sceneObjectFreeRotation = freeRotation;
                builtObjectsSaveData.verticalGridIndex = buildableObject.GetSceneObjectVerticalGridIndex();

                builtObjectsSaveData.isInstantiatedByGhostObject = buildableObject.GetIsInstantiatedByGhostObject();

                builtObjectsSaveData.occupiedGridSystemUniqueID = buildableObject.GetOccupiedGridSystem().GetGridUniqueID();
                builtObjectsSaveData.occupiedVerticalGridIndex = buildableObject.GetOccupiedVerticalGridIndex();
                builtObjectsSaveData.occupiedCellSize = buildableObject.GetOccupiedCellSize();
                builtObjectsSaveData.buildableObjectSO = buildableObject.GetBuildableObjectSO();
                builtObjectsSaveData.buildableObjectSORandomPrefab = buildableObject.GetBuildableObjectSORandomPrefab();
                builtObjectsSaveData.objectOriginWorldPosition = buildableObject.GetObjectOriginWorldPosition();
                builtObjectsSaveData.objectModifiedOriginWorldPosition = buildableObject.GetObjectModifiedOriginWorldPosition();
                builtObjectsSaveData.objectOffset = buildableObject.GetObjectOffset();
                builtObjectsSaveData.objectOriginCellPosition = buildableObject.GetObjectOriginCellPosition(out List<Vector2Int> objectCellPositionList);
                builtObjectsSaveData.objectCellPositionList = objectCellPositionList;
                builtObjectsSaveData.cornerObjectOriginCellDirection = buildableObject.GetCornerObjectOriginCellDirection();
                builtObjectsSaveData.objectFourDirectionalRotation = buildableObject.GetObjectFourDirectionalRotation();
                builtObjectsSaveData.objectEightDirectionalRotation = buildableObject.GetObjectEightDirectionalRotation();
                builtObjectsSaveData.objectFreeRotation = buildableObject.GetObjectFreeRotation();
                builtObjectsSaveData.isObjectFlipped = buildableObject.GetIsObjectFlipped();
                builtObjectsSaveData.hitNormals = buildableObject.GetObjectHitNormals();
                builtObjectsSaveData.objectPlacementInvokedAsSecondaryPlacement = buildableObject.GetIsObjectInvokedAsSecondaryPlacement();

                builtObjectsSaveDataList.Add(builtObjectsSaveData);
            }

            return builtObjectsSaveDataList;
        }

        private void HandleLoadBuiltObjectsData(BuildableObjectsSaveData buildableObjectsSaveData)
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            buildableObjectDestroyer.SetInputDestroyAllBuildableObjectInScene(true, true);
            
            BuildableObject buildableObject = default;
            foreach (BuiltObjectSaveData builtObjectsSaveData in buildableObjectsSaveData.builtObjectSaveDataList)
            {
                EasyGridBuilderPro gridSystem = default;
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    if (easyGridBuilderPro.GetGridUniqueID() != builtObjectsSaveData.occupiedGridSystemUniqueID) continue;
                    else gridSystem = easyGridBuilderPro;
                }
                if (gridSystem == default) continue;

                switch (builtObjectsSaveData.buildableObjectSO)
                {
                    case BuildableGridObjectSO buildableGridObjectSO: 
                        gridSystem.TryInitializeBuildableGridObjectSinglePlacement(builtObjectsSaveData.objectModifiedOriginWorldPosition, buildableGridObjectSO, 
                            builtObjectsSaveData.objectFourDirectionalRotation, true, true, builtObjectsSaveData.occupiedVerticalGridIndex, true, out BuildableGridObject buildableGridObject, 
                            builtObjectsSaveData.buildableObjectSORandomPrefab);
                        buildableObject = buildableGridObject;
                    break;
                    case BuildableEdgeObjectSO buildableEdgeObjectSO: 
                        gridSystem.TryInitializeBuildableEdgeObjectSinglePlacement(builtObjectsSaveData.objectModifiedOriginWorldPosition, buildableEdgeObjectSO, 
                            builtObjectsSaveData.objectFourDirectionalRotation, builtObjectsSaveData.isObjectFlipped, true, true, builtObjectsSaveData.occupiedVerticalGridIndex, true, 
                            out BuildableEdgeObject buildableEdgeObject, builtObjectsSaveData.buildableObjectSORandomPrefab);
                        buildableObject = buildableEdgeObject;
                    break;
                    case BuildableCornerObjectSO buildableCornerObjectSO: 
                        gridSystem.TryInitializeBuildableCornerObjectSinglePlacement(builtObjectsSaveData.objectModifiedOriginWorldPosition, buildableCornerObjectSO, 
                            builtObjectsSaveData.objectFourDirectionalRotation, builtObjectsSaveData.objectEightDirectionalRotation, builtObjectsSaveData.objectFreeRotation, true, true, 
                            builtObjectsSaveData.occupiedVerticalGridIndex, true, out BuildableCornerObject buildableCornerObject, builtObjectsSaveData.buildableObjectSORandomPrefab);
                        buildableObject = buildableCornerObject;
                    break;
                    case BuildableFreeObjectSO buildableFreeObjectSO: 
                        gridSystem.TryInitializeBuildableFreeObjectSinglePlacement(builtObjectsSaveData.objectModifiedOriginWorldPosition, buildableFreeObjectSO, 
                            builtObjectsSaveData.objectFourDirectionalRotation, builtObjectsSaveData.objectEightDirectionalRotation, builtObjectsSaveData.objectFreeRotation, builtObjectsSaveData.hitNormals,
                            true, builtObjectsSaveData.occupiedVerticalGridIndex, true, out BuildableFreeObject buildableFreeObject, builtObjectsSaveData.buildableObjectSORandomPrefab);
                        buildableObject = buildableFreeObject;
                    break;
                }

                if (buildableObject == null) continue;

                buildableObject.SetUniqueID(builtObjectsSaveData.buildableObjectUniqueID);

                buildableObject.SetGridAxis(builtObjectsSaveData.gridAxis);
                buildableObject.SetIsActivateGizmos(builtObjectsSaveData.activateGizmos);
                buildableObject.SetLockAutoGenerationAndValues(builtObjectsSaveData.lockAutoGenerationAndValues);
                buildableObject.SetObjectScale(builtObjectsSaveData.objectScale);
                buildableObject.SetObjectCenter(builtObjectsSaveData.objectCenter);
                buildableObject.SetObjectCustomPivot(builtObjectsSaveData.objectCustomPivot);
                buildableObject.SetDebugCellSize(builtObjectsSaveData.debugCellSize);
                buildableObject.SetObjectSizeRelativeToCellSize(builtObjectsSaveData.objectSizeRelativeToCellSize);
                buildableObject.SetIsActiveSceneObject(builtObjectsSaveData.activeSceneObject);
                buildableObject.SetSceneObjectBuildableObjectSO(builtObjectsSaveData.sceneObjectBuildableCornerObjectSO);
                buildableObject.SetObjectFourDirectionalRotation(builtObjectsSaveData.sceneObjectFourDirectionalRotation);
                buildableObject.SetObjectEightDirectionalRotation(builtObjectsSaveData.sceneObjectEightDirectionalRotation);
                buildableObject.SetObjectFreeRotation(builtObjectsSaveData.sceneObjectFreeRotation);
                buildableObject.SetSceneObjectVerticalGridIndex(builtObjectsSaveData.verticalGridIndex);

                buildableObject.SetIsInstantiatedByGhostObject(builtObjectsSaveData.isInstantiatedByGhostObject);

                buildableObject.SetOccupiedGridSystem(gridSystem);
                buildableObject.SetOccupiedVerticalGridIndex(builtObjectsSaveData.occupiedVerticalGridIndex);
                buildableObject.SetOccupiedCellSize(builtObjectsSaveData.occupiedCellSize);
                buildableObject.SetBuildableObjectSO(builtObjectsSaveData.buildableObjectSO);
                buildableObject.SetBuildableObjectSORandomPrefab(builtObjectsSaveData.buildableObjectSORandomPrefab);
                buildableObject.SetObjectOriginWorldPosition(builtObjectsSaveData.objectOriginWorldPosition);
                buildableObject.SetObjectModifiedOriginWorldPosition(builtObjectsSaveData.objectModifiedOriginWorldPosition);
                buildableObject.SetObjectOffset(builtObjectsSaveData.objectOffset);
                buildableObject.SetObjectOriginCellPosition(builtObjectsSaveData.objectOriginCellPosition);
                buildableObject.SetObjectCellPositionList(builtObjectsSaveData.objectCellPositionList);
                buildableObject.SetCornerObjectOriginCellDirection(builtObjectsSaveData.cornerObjectOriginCellDirection);
                buildableObject.SetObjectFourDirectionalRotation(builtObjectsSaveData.objectFourDirectionalRotation);
                buildableObject.SetObjectEightDirectionalRotation(builtObjectsSaveData.objectEightDirectionalRotation);
                buildableObject.SetObjectFreeRotation(builtObjectsSaveData.objectFreeRotation);
                buildableObject.SetIsObjectFlipped(builtObjectsSaveData.isObjectFlipped);
                buildableObject.SetObjectHitNormals(builtObjectsSaveData.hitNormals);
                buildableObject.SetIsObjectInvokedAsSecondaryPlacement(builtObjectsSaveData.objectPlacementInvokedAsSecondaryPlacement);
            }

            GridManager.Instance.ClearUndoCommandLinkedList();
            GridManager.Instance.ClearRedoCommandStack();
        }
        
        public string GetLocalSavePath() => localSavePath;
        public string GetSaveFileName() => saveFileName;
        public string GetSaveExtention() => saveExtention;
    }

    [Serializable]
    public struct GridSystemsSaveData
    {
        public List<GridSaveData> gridSaveDataList;
    }

    [Serializable]
    public struct BuildableObjectsSaveData
    {
        public List<BuiltObjectSaveData> builtObjectSaveDataList;
    }
}