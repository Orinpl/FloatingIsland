using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandDestroyBuildableCornerObject : ICommand
    {
        private string uniqueID;

        private BuildableCornerObject buildableCornerObject;
        private bool byPassEventsAndMessages;
        private bool detachInstead;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector2Int originCellPosition;
        private Vector3 objectOffset;
        private Vector3 worldPosition;
        private BuildableCornerObjectSO buildableCornerObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private EightDirectionalRotation eightDirectionalDirection;
        private float freeRotation;
        private CornerObjectCellDirection cornerObjectOriginCellDirection;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private int verticalGridIndex;
        private bool invokedAsSecondaryPlacement;

        private bool isDestructionSuccessful;

        public CommandDestroyBuildableCornerObject(BuildableCornerObject buildableCornerObject, bool byPassEventsAndMessages, bool detachInstead)
        {
            this.uniqueID = buildableCornerObject.GetUniqueID();
            this.buildableCornerObject = buildableCornerObject;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.detachInstead = detachInstead;

            easyGridBuilderPro = buildableCornerObject.GetOccupiedGridSystem();
            originCellPosition = buildableCornerObject.GetObjectOriginCellPosition(out _);
            objectOffset = buildableCornerObject.GetObjectOffset();
            worldPosition = buildableCornerObject.GetObjectOriginWorldPosition();
            buildableCornerObjectSO = (BuildableCornerObjectSO)buildableCornerObject.GetBuildableObjectSO();
            fourDirectionalDirection = buildableCornerObject.GetObjectFourDirectionalRotation();
            eightDirectionalDirection = buildableCornerObject.GetObjectEightDirectionalRotation();
            freeRotation = buildableCornerObject.GetObjectFreeRotation();
            cornerObjectOriginCellDirection = buildableCornerObject.GetCornerObjectOriginCellDirection();
            buildableObjectSORandomPrefab = buildableCornerObject.GetBuildableObjectSORandomPrefab();
            verticalGridIndex = buildableCornerObject.GetOccupiedVerticalGridIndex();
            invokedAsSecondaryPlacement = buildableCornerObject.GetIsObjectInvokedAsSecondaryPlacement();
        }
        
        public void Execute()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            if (buildableObjectDestroyer.TryDestroyBuildableCornerObjectByUniqueID(uniqueID, byPassEventsAndMessages, detachInstead)) isDestructionSuccessful = true;
        }

        public void Undo()
        {
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableCornerObjectSinglePlacement(originCellPosition, buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation,
                ref tempBuildableObjectSORandomPrefab, worldPosition, cornerObjectOriginCellDirection, true, true, verticalGridIndex, true, invokedAsSecondaryPlacement, out BuildableCornerObject restoredObject, 
                objectOffset, buildableCornerObject);
            if (restoredObject) restoredObject.SetUniqueID(uniqueID);
        }

        public void Redo()
        {
            Execute();
        }

        public bool GetIsDestructionSuccessful() => isDestructionSuccessful;
    }
}