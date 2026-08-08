using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandPlaceBuildableGridObject : ICommand
    {
        private string uniqueID;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector2Int originCellPosition;
        private Vector3 worldPosition;
        private BuildableGridObjectSO buildableGridObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private bool ignoreCustomConditions;
        private bool ignoreReplacement;
        private int verticalGridIndex;
        private bool byPassEventsAndMessages;
        private BuildableGridObject buildableGridObject;
        private BuildableGridObject originalBuildableGridObject;

        private BuildableObjectSO.RandomPrefabs firstObjectSORandomPrefab;

        private EasyGridBuilderPro originalObjectEasyGridBuilderPro;
        private Vector2Int originalObjectOriginCellPosition;
        private Vector3 originalObjectWorldPosition;
        private BuildableGridObjectSO originalObjectBuildableGridObjectSO;
        private FourDirectionalRotation originalObjectFourDirectionalDirection;
        private BuildableObjectSO.RandomPrefabs originalObjectBuildableObjectSORandomPrefab;
        private int originalObjectVerticalGridIndex;

        public CommandPlaceBuildableGridObject(EasyGridBuilderPro easyGridBuilderPro, Vector2Int originCellPosition, Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, 
            FourDirectionalRotation fourDirectionalDirection, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex,
            bool byPassEventsAndMessages, BuildableGridObject originalBuildableGridObject = null)
        {
            this.easyGridBuilderPro = easyGridBuilderPro;
            this.originCellPosition = originCellPosition;
            this.worldPosition = worldPosition;
            this.buildableGridObjectSO = buildableGridObjectSO;
            this.fourDirectionalDirection = fourDirectionalDirection;
            this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.firstObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.ignoreCustomConditions = ignoreCustomConditions;
            this.ignoreReplacement = ignoreReplacement;
            this.verticalGridIndex = verticalGridIndex;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.originalBuildableGridObject = originalBuildableGridObject;

            if (originalBuildableGridObject != null)
            {
                originalObjectEasyGridBuilderPro = originalBuildableGridObject.GetOccupiedGridSystem();
                originalObjectOriginCellPosition = originalBuildableGridObject.GetObjectOriginCellPosition(out _);
                originalObjectWorldPosition = originalBuildableGridObject.GetObjectOriginWorldPosition();
                originalObjectBuildableGridObjectSO = (BuildableGridObjectSO)originalBuildableGridObject.GetBuildableObjectSO();
                originalObjectFourDirectionalDirection = originalBuildableGridObject.GetObjectFourDirectionalRotation();
                originalObjectBuildableObjectSORandomPrefab = originalBuildableGridObject.GetBuildableObjectSORandomPrefab();
                originalObjectVerticalGridIndex = originalBuildableGridObject.GetOccupiedVerticalGridIndex();
            }
         }

        public void Execute()
        {
            easyGridBuilderPro.InvokeTryPlaceBuildableGridObjectSinglePlacement(originCellPosition, worldPosition, buildableGridObjectSO, fourDirectionalDirection, ref buildableObjectSORandomPrefab, 
                ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, out buildableGridObject, originalBuildableGridObject);
            if (buildableGridObject) uniqueID = buildableGridObject.GetUniqueID();
        }

        public void Undo()
        { 
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            
            if (!originalObjectEasyGridBuilderPro) //If previously had an originalBuildableGridObject
            {
                buildableObjectDestroyer.TryDestroyBuildableGridObjectByUniqueID(uniqueID, true, false);
            }
            else
            {
                buildableObjectDestroyer.TryDestroyBuildableGridObjectByUniqueID(uniqueID, true, false);
                
                BuildableObjectSO.RandomPrefabs tempOriginalObjectBuildableObjectSORandomPrefab = originalObjectBuildableObjectSORandomPrefab;
                originalObjectEasyGridBuilderPro.InvokeTryPlaceBuildableGridObjectSinglePlacement(originalObjectOriginCellPosition, originalObjectWorldPosition, originalObjectBuildableGridObjectSO, 
                    originalObjectFourDirectionalDirection, ref tempOriginalObjectBuildableObjectSORandomPrefab, true, true, originalObjectVerticalGridIndex, true, out BuildableGridObject restoredObject);

                if (restoredObject) restoredObject.SetUniqueID(uniqueID);
            }
        }

        public void Redo()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;

            buildableObjectDestroyer.TryDestroyBuildableGridObjectByUniqueID(uniqueID, true, false);
            
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = firstObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableGridObjectSinglePlacement(originCellPosition, worldPosition, buildableGridObjectSO, fourDirectionalDirection, ref tempBuildableObjectSORandomPrefab, 
                ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, out buildableGridObject, originalBuildableGridObject);
            if (buildableGridObject) buildableGridObject.SetUniqueID(uniqueID);
        }

        public BuildableGridObject GetBuildableGridObject() => buildableGridObject;

        public BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;
    }
}