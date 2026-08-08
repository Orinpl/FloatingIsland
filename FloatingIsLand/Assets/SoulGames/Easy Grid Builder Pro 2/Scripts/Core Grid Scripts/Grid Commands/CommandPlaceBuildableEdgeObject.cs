using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandPlaceBuildableEdgeObject : ICommand
    {
        private string uniqueID;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector2Int originCellPosition;
        private Vector3 worldPosition;
        private Vector3 objectOffset;
        private BuildableEdgeObjectSO buildableEdgeObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private bool isObjectFlipped;
        private CornerObjectCellDirection cornerObjectOriginCellDirection;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private bool ignoreCustomConditions;
        private bool ignoreReplacement;
        private int verticalGridIndex;
        private bool byPassEventsAndMessages;
        private BuildableEdgeObject buildableEdgeObject;
        private BuildableEdgeObject originalBuildableEdgeObject;

        private BuildableObjectSO.RandomPrefabs firstObjectSORandomPrefab;

        private EasyGridBuilderPro originalObjectEasyGridBuilderPro;
        private Vector2Int originalObjectOriginCellPosition;
        private Vector3 originalObjectWorldPosition;
        private Vector3 originalObjectOffset;
        private BuildableEdgeObjectSO originalObjectBuildableEdgeObjectSO;
        private FourDirectionalRotation originalObjectFourDirectionalDirection;
        private bool isOriginalObjectFlipped;
        private CornerObjectCellDirection originalCornerObjectOriginCellDirection;
        private BuildableObjectSO.RandomPrefabs originalObjectBuildableObjectSORandomPrefab;
        private int originalObjectVerticalGridIndex;

        public CommandPlaceBuildableEdgeObject(EasyGridBuilderPro easyGridBuilderPro, Vector2Int originCellPosition, Vector3 worldPosition, Vector3 objectOffset, BuildableEdgeObjectSO buildableEdgeObjectSO, 
            FourDirectionalRotation fourDirectionalDirection, bool isObjectFlipped, CornerObjectCellDirection cornerObjectOriginCellDirection, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, 
            bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, BuildableEdgeObject originalBuildableEdgeObject = null)
        {
            this.easyGridBuilderPro = easyGridBuilderPro;
            this.originCellPosition = originCellPosition;
            this.worldPosition = worldPosition;
            this.objectOffset = objectOffset;
            this.buildableEdgeObjectSO = buildableEdgeObjectSO;
            this.fourDirectionalDirection = fourDirectionalDirection;
            this.isObjectFlipped = isObjectFlipped;
            this.cornerObjectOriginCellDirection = cornerObjectOriginCellDirection;
            this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.firstObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.ignoreCustomConditions = ignoreCustomConditions;
            this.ignoreReplacement = ignoreReplacement;
            this.verticalGridIndex = verticalGridIndex;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.originalBuildableEdgeObject = originalBuildableEdgeObject;

            if (originalBuildableEdgeObject != null)
            {
                originalObjectEasyGridBuilderPro = originalBuildableEdgeObject.GetOccupiedGridSystem();
                originalObjectOriginCellPosition = originalBuildableEdgeObject.GetObjectOriginCellPosition(out _);
                originalObjectWorldPosition = originalBuildableEdgeObject.GetObjectOriginWorldPosition();
                originalObjectOffset = originalBuildableEdgeObject.GetObjectOffset();
                originalObjectBuildableEdgeObjectSO = (BuildableEdgeObjectSO)originalBuildableEdgeObject.GetBuildableObjectSO();
                originalObjectFourDirectionalDirection = originalBuildableEdgeObject.GetObjectFourDirectionalRotation();
                isOriginalObjectFlipped = originalBuildableEdgeObject.GetIsObjectFlipped();
                originalCornerObjectOriginCellDirection = originalBuildableEdgeObject.GetCornerObjectOriginCellDirection();
                originalObjectBuildableObjectSORandomPrefab = originalBuildableEdgeObject.GetBuildableObjectSORandomPrefab();
                originalObjectVerticalGridIndex = originalBuildableEdgeObject.GetOccupiedVerticalGridIndex();
            }
         }

        public void Execute()
        {
            easyGridBuilderPro.InvokeTryPlaceBuildableEdgeObjectSinglePlacement(originCellPosition, buildableEdgeObjectSO, fourDirectionalDirection, isObjectFlipped, ref buildableObjectSORandomPrefab, 
                worldPosition, cornerObjectOriginCellDirection, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, out buildableEdgeObject, objectOffset, 
                originalBuildableEdgeObject);
            if (buildableEdgeObject) uniqueID = buildableEdgeObject.GetUniqueID();
        }

        public void Undo()
        { 
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            
            if (!originalObjectEasyGridBuilderPro) //If previously had an originalBuildableGridObject
            {
                buildableObjectDestroyer.TryDestroyBuildableEdgeObjectByUniqueID(uniqueID, true, false);
            }
            else
            {
                buildableObjectDestroyer.TryDestroyBuildableEdgeObjectByUniqueID(uniqueID, true, false);
                
                BuildableObjectSO.RandomPrefabs tempOriginalObjectBuildableObjectSORandomPrefab = originalObjectBuildableObjectSORandomPrefab;
                originalObjectEasyGridBuilderPro.InvokeTryPlaceBuildableEdgeObjectSinglePlacement(originalObjectOriginCellPosition, originalObjectBuildableEdgeObjectSO, originalObjectFourDirectionalDirection, 
                    isOriginalObjectFlipped, ref tempOriginalObjectBuildableObjectSORandomPrefab, originalObjectWorldPosition, originalCornerObjectOriginCellDirection, true, true, originalObjectVerticalGridIndex, 
                    true, out BuildableEdgeObject restoredObject, objectOffset);

                if (restoredObject) restoredObject.SetUniqueID(uniqueID);
            }
        }

        public void Redo()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;

            buildableObjectDestroyer.TryDestroyBuildableEdgeObjectByUniqueID(uniqueID, true, false);
            
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = firstObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableEdgeObjectSinglePlacement(originCellPosition, buildableEdgeObjectSO, fourDirectionalDirection, isObjectFlipped, ref tempBuildableObjectSORandomPrefab, 
                worldPosition, cornerObjectOriginCellDirection, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, out buildableEdgeObject, objectOffset, 
                originalBuildableEdgeObject);
            if (buildableEdgeObject) buildableEdgeObject.SetUniqueID(uniqueID);
        }

        public BuildableEdgeObject GetBuildableEdgeObject() => buildableEdgeObject;

        public BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;
    }
}