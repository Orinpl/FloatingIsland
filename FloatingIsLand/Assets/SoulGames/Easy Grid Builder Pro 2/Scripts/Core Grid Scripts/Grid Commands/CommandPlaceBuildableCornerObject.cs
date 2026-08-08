using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandPlaceBuildableCornerObject : ICommand
    {
        private string uniqueID;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector2Int originCellPosition;
        private Vector3 worldPosition;
        private Vector3 objectOffset;
        private BuildableCornerObjectSO buildableCornerObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private EightDirectionalRotation eightDirectionalDirection;
        private float freeRotation;
        private CornerObjectCellDirection cornerObjectOriginCellDirection;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private bool ignoreCustomConditions;
        private bool ignoreReplacement;
        private int verticalGridIndex;
        private bool byPassEventsAndMessages;
        private BuildableCornerObject buildableCornerObject;
        private BuildableCornerObject originalBuildableCornerObject;
        private bool invokedAsSecondaryPlacement;

        private BuildableObjectSO.RandomPrefabs firstObjectSORandomPrefab;

        private EasyGridBuilderPro originalObjectEasyGridBuilderPro;
        private Vector2Int originalObjectOriginCellPosition;
        private Vector3 originalObjectWorldPosition;
        private Vector3 originalObjectOffset;
        private BuildableCornerObjectSO originalObjectBuildableCornerObjectSO;
        private FourDirectionalRotation originalObjectFourDirectionalDirection;
        private EightDirectionalRotation originalObjectEightDirectionalDirection;
        private float originalObjectfreeRotation;
        private CornerObjectCellDirection originalCornerObjectOriginCellDirection;
        private BuildableObjectSO.RandomPrefabs originalObjectBuildableObjectSORandomPrefab;
        private int originalObjectVerticalGridIndex;
        private bool originalObjectInvokedAsSecondaryPlacement;

        public CommandPlaceBuildableCornerObject(EasyGridBuilderPro easyGridBuilderPro, Vector2Int originCellPosition, Vector3 worldPosition, Vector3 objectOffset, BuildableCornerObjectSO buildableCornerObjectSO, 
            FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation, CornerObjectCellDirection cornerObjectOriginCellDirection, 
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement,
            BuildableCornerObject originalBuildableCornerObject = null)
        {
            this.easyGridBuilderPro = easyGridBuilderPro;
            this.originCellPosition = originCellPosition;
            this.worldPosition = worldPosition;
            this.objectOffset = objectOffset;
            this.buildableCornerObjectSO = buildableCornerObjectSO;
            this.fourDirectionalDirection = fourDirectionalDirection;
            this.eightDirectionalDirection = eightDirectionalDirection;
            this.freeRotation = freeRotation;
            this.cornerObjectOriginCellDirection = cornerObjectOriginCellDirection;
            this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.firstObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.ignoreCustomConditions = ignoreCustomConditions;
            this.ignoreReplacement = ignoreReplacement;
            this.verticalGridIndex = verticalGridIndex;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.originalBuildableCornerObject = originalBuildableCornerObject;
            this.invokedAsSecondaryPlacement = invokedAsSecondaryPlacement;

            if (originalBuildableCornerObject != null)
            {
                originalObjectEasyGridBuilderPro = originalBuildableCornerObject.GetOccupiedGridSystem();
                originalObjectOriginCellPosition = originalBuildableCornerObject.GetObjectOriginCellPosition(out _);
                originalObjectWorldPosition = originalBuildableCornerObject.GetObjectOriginWorldPosition();
                originalObjectOffset = originalBuildableCornerObject.GetObjectOffset();
                originalObjectBuildableCornerObjectSO = (BuildableCornerObjectSO)originalBuildableCornerObject.GetBuildableObjectSO();
                originalObjectFourDirectionalDirection = originalBuildableCornerObject.GetObjectFourDirectionalRotation();
                originalObjectEightDirectionalDirection = originalBuildableCornerObject.GetObjectEightDirectionalRotation();
                originalObjectfreeRotation = originalBuildableCornerObject.GetObjectFreeRotation();
                originalCornerObjectOriginCellDirection = originalBuildableCornerObject.GetCornerObjectOriginCellDirection();
                originalObjectBuildableObjectSORandomPrefab = originalBuildableCornerObject.GetBuildableObjectSORandomPrefab();
                originalObjectVerticalGridIndex = originalBuildableCornerObject.GetOccupiedVerticalGridIndex();
                originalObjectInvokedAsSecondaryPlacement = originalBuildableCornerObject.GetIsObjectInvokedAsSecondaryPlacement();
            }
         }

        public void Execute()
        {
            easyGridBuilderPro.InvokeTryPlaceBuildableCornerObjectSinglePlacement(originCellPosition, buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation,
                ref buildableObjectSORandomPrefab, worldPosition, cornerObjectOriginCellDirection, true, true, verticalGridIndex, true, invokedAsSecondaryPlacement, out buildableCornerObject, 
                objectOffset, originalBuildableCornerObject);
            if (buildableCornerObject) uniqueID = buildableCornerObject.GetUniqueID();
        }

        public void Undo()
        { 
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            
            if (!originalObjectEasyGridBuilderPro) //If previously had an originalBuildableGridObject
            {
                buildableObjectDestroyer.TryDestroyBuildableCornerObjectByUniqueID(uniqueID, true, false);
            }
            else
            {
                buildableObjectDestroyer.TryDestroyBuildableCornerObjectByUniqueID(uniqueID, true, false);
                
                BuildableObjectSO.RandomPrefabs tempOriginalObjectBuildableObjectSORandomPrefab = originalObjectBuildableObjectSORandomPrefab;
                originalObjectEasyGridBuilderPro.InvokeTryPlaceBuildableCornerObjectSinglePlacement(originalObjectOriginCellPosition, originalObjectBuildableCornerObjectSO, originalObjectFourDirectionalDirection, 
                    originalObjectEightDirectionalDirection, originalObjectfreeRotation, ref tempOriginalObjectBuildableObjectSORandomPrefab, originalObjectWorldPosition, originalCornerObjectOriginCellDirection, 
                    true, true, originalObjectVerticalGridIndex, true, originalObjectInvokedAsSecondaryPlacement, out BuildableCornerObject restoredObject, originalObjectOffset);

                if (restoredObject) restoredObject.SetUniqueID(uniqueID);
            }
        }

        public void Redo()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;

            buildableObjectDestroyer.TryDestroyBuildableCornerObjectByUniqueID(uniqueID, true, false);
            
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = firstObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableCornerObjectSinglePlacement(originCellPosition, buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, 
                ref tempBuildableObjectSORandomPrefab, worldPosition, cornerObjectOriginCellDirection, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages,
                invokedAsSecondaryPlacement, out buildableCornerObject, objectOffset, originalBuildableCornerObject);
            if (buildableCornerObject) buildableCornerObject.SetUniqueID(uniqueID);
        }

        public BuildableCornerObject GetBuildableCornerObject() => buildableCornerObject;

        public BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;
    }
}