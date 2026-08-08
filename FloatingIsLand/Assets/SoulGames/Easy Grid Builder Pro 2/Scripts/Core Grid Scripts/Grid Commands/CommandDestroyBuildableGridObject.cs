using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandDestroyBuildableGridObject : ICommand
    {
        private string uniqueID;

        private BuildableGridObject buildableGridObject;
        private bool byPassEventsAndMessages;
        private bool detachInstead;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector2Int originCellPosition;
        private Vector3 worldPosition;
        private BuildableGridObjectSO buildableGridObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private int verticalGridIndex;

        private bool isDestructionSuccessful;

        public CommandDestroyBuildableGridObject(BuildableGridObject buildableGridObject, bool byPassEventsAndMessages, bool detachInstead)
        {
            this.uniqueID = buildableGridObject.GetUniqueID();
            this.buildableGridObject = buildableGridObject;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.detachInstead = detachInstead;

            easyGridBuilderPro = buildableGridObject.GetOccupiedGridSystem();
            originCellPosition = buildableGridObject.GetObjectOriginCellPosition(out _);
            worldPosition = buildableGridObject.GetObjectOriginWorldPosition();
            buildableGridObjectSO = (BuildableGridObjectSO)buildableGridObject.GetBuildableObjectSO();
            fourDirectionalDirection = buildableGridObject.GetObjectFourDirectionalRotation();
            buildableObjectSORandomPrefab = buildableGridObject.GetBuildableObjectSORandomPrefab();
            verticalGridIndex = buildableGridObject.GetOccupiedVerticalGridIndex();
        }
        
        public void Execute()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            if (buildableObjectDestroyer.TryDestroyBuildableGridObjectByUniqueID(uniqueID, byPassEventsAndMessages, detachInstead)) isDestructionSuccessful = true;
        }

        public void Undo()
        {
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableGridObjectSinglePlacement(originCellPosition, worldPosition, buildableGridObjectSO, fourDirectionalDirection, ref tempBuildableObjectSORandomPrefab,
                true, true, verticalGridIndex, true, out BuildableGridObject restoredObject, buildableGridObject);
            if (restoredObject) restoredObject.SetUniqueID(uniqueID);
        }

        public void Redo()
        {
            Execute();
        }

        public bool GetIsDestructionSuccessful() => isDestructionSuccessful;
    }
}