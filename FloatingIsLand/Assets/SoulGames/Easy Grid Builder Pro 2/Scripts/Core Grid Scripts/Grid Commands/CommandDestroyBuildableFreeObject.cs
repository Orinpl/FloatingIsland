using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandDestroyBuildableFreeObject : ICommand
    {
        private string uniqueID;

        private BuildableFreeObject buildableFreeObject;
        private bool byPassEventsAndMessages;
        private bool detachInstead;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector3 worldPosition;
        private BuildableFreeObjectSO buildableFreeObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private EightDirectionalRotation eightDirectionalDirection;
        private float freeRotation;
        private Vector3 hitNormals;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private int verticalGridIndex;

        private bool isDestructionSuccessful;

        public CommandDestroyBuildableFreeObject(BuildableFreeObject buildableFreeObject, bool byPassEventsAndMessages, bool detachInstead)
        {
            this.uniqueID = buildableFreeObject.GetUniqueID();
            this.buildableFreeObject = buildableFreeObject;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.detachInstead = detachInstead;

            easyGridBuilderPro = buildableFreeObject.GetOccupiedGridSystem();
            worldPosition = buildableFreeObject.GetObjectOriginWorldPosition();
            buildableFreeObjectSO = (BuildableFreeObjectSO)buildableFreeObject.GetBuildableObjectSO();
            fourDirectionalDirection = buildableFreeObject.GetObjectFourDirectionalRotation();
            eightDirectionalDirection = buildableFreeObject.GetObjectEightDirectionalRotation();
            freeRotation = buildableFreeObject.GetObjectFreeRotation();
            hitNormals = buildableFreeObject.GetObjectHitNormals();
            buildableObjectSORandomPrefab = buildableFreeObject.GetBuildableObjectSORandomPrefab();
            verticalGridIndex = buildableFreeObject.GetOccupiedVerticalGridIndex();
        }
        
        public void Execute()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            if (buildableObjectDestroyer.TryDestroyBuildableFreeObjectByUniqueID(uniqueID, byPassEventsAndMessages, detachInstead)) isDestructionSuccessful = true;
        }

        public void Undo()
        {
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableFreeObjectSinglePlacement(buildableFreeObjectSO, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals,
                true, verticalGridIndex, true, out BuildableFreeObject restoredObject, tempBuildableObjectSORandomPrefab, buildableFreeObject);
            if (restoredObject) restoredObject.SetUniqueID(uniqueID);
        }

        public void Redo()
        {
            Execute();
        }

        public bool GetIsDestructionSuccessful() => isDestructionSuccessful;
    }
}