using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandPlaceBuildableFreeObject : ICommand
    {
        private string uniqueID;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector3 worldPosition;
        private BuildableFreeObjectSO buildableFreeObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private EightDirectionalRotation eightDirectionalDirection;
        private float freeRotation;
        private Vector3 hitNormals;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private bool ignoreCustomConditions;
        private int verticalGridIndex;
        private bool byPassEventsAndMessages;
        private BuildableFreeObject buildableFreeObject;
        private BuildableFreeObject originalBuildableFreeObject;

        private BuildableObjectSO.RandomPrefabs firstObjectSORandomPrefab;

        private EasyGridBuilderPro originalObjectEasyGridBuilderPro;
        private Vector3 originalObjectWorldPosition;
        private BuildableFreeObjectSO originalObjectBuildableFreeObjectSO;
        private FourDirectionalRotation originalObjectFourDirectionalDirection;
        private EightDirectionalRotation originalObjectEightDirectionalDirection;
        private float originalObjectFreeRotation;
        private Vector3 originalObjectHitNormals;
        private BuildableObjectSO.RandomPrefabs originalObjectBuildableObjectSORandomPrefab;
        private int originalObjectVerticalGridIndex;

        public CommandPlaceBuildableFreeObject(EasyGridBuilderPro easyGridBuilderPro, Vector3 worldPosition, BuildableFreeObjectSO buildableFreeObjectSO, 
            FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, 
            bool ignoreCustomConditions, int verticalGridIndex, bool byPassEventsAndMessages, BuildableFreeObject originalBuildableFreeObject = null)
        {
            this.easyGridBuilderPro = easyGridBuilderPro;
            this.worldPosition = worldPosition;
            this.buildableFreeObjectSO = buildableFreeObjectSO;
            this.fourDirectionalDirection = fourDirectionalDirection;
            this.eightDirectionalDirection = eightDirectionalDirection;
            this.freeRotation = freeRotation;
            this.hitNormals = hitNormals;
            this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.firstObjectSORandomPrefab = buildableObjectSORandomPrefab;
            this.ignoreCustomConditions = ignoreCustomConditions;
            this.verticalGridIndex = verticalGridIndex;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.originalBuildableFreeObject = originalBuildableFreeObject;

            if (originalBuildableFreeObject != null)
            {
                originalObjectEasyGridBuilderPro = originalBuildableFreeObject.GetOccupiedGridSystem();
                originalObjectWorldPosition = originalBuildableFreeObject.GetObjectOriginWorldPosition();
                originalObjectBuildableFreeObjectSO = (BuildableFreeObjectSO)originalBuildableFreeObject.GetBuildableObjectSO();
                originalObjectFourDirectionalDirection = originalBuildableFreeObject.GetObjectFourDirectionalRotation();
                originalObjectEightDirectionalDirection = originalBuildableFreeObject.GetObjectEightDirectionalRotation();
                originalObjectFreeRotation = originalBuildableFreeObject.GetObjectFreeRotation();
                originalObjectHitNormals = originalBuildableFreeObject.GetObjectHitNormals();
                originalObjectBuildableObjectSORandomPrefab = originalBuildableFreeObject.GetBuildableObjectSORandomPrefab();
                originalObjectVerticalGridIndex = originalBuildableFreeObject.GetOccupiedVerticalGridIndex();
            }
         }

        public void Execute()
        {
            easyGridBuilderPro.InvokeTryPlaceBuildableFreeObjectSinglePlacement(buildableFreeObjectSO, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals,
                ignoreCustomConditions, verticalGridIndex, byPassEventsAndMessages, out buildableFreeObject, buildableObjectSORandomPrefab, originalBuildableFreeObject);
            if (buildableFreeObject) uniqueID = buildableFreeObject.GetUniqueID();
        }

        public void Undo()
        { 
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            
            if (!originalObjectEasyGridBuilderPro) //If previously had an originalBuildableGridObject
            {
                buildableObjectDestroyer.TryDestroyBuildableFreeObjectByUniqueID(uniqueID, true, false);
            }
            else
            {
                buildableObjectDestroyer.TryDestroyBuildableFreeObjectByUniqueID(uniqueID, true, false);
                
                BuildableObjectSO.RandomPrefabs tempOriginalObjectBuildableObjectSORandomPrefab = originalObjectBuildableObjectSORandomPrefab;
                originalObjectEasyGridBuilderPro.InvokeTryPlaceBuildableFreeObjectSinglePlacement(originalObjectBuildableFreeObjectSO, originalObjectWorldPosition, originalObjectFourDirectionalDirection, 
                    originalObjectEightDirectionalDirection, originalObjectFreeRotation, originalObjectHitNormals, true, originalObjectVerticalGridIndex, true, out BuildableFreeObject restoredObject, 
                    tempOriginalObjectBuildableObjectSORandomPrefab);

                if (restoredObject) restoredObject.SetUniqueID(uniqueID);
            }
        }

        public void Redo()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;

            buildableObjectDestroyer.TryDestroyBuildableFreeObjectByUniqueID(uniqueID, true, false);
            
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = firstObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableFreeObjectSinglePlacement(buildableFreeObjectSO, worldPosition , fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals,
                ignoreCustomConditions, verticalGridIndex, byPassEventsAndMessages, out buildableFreeObject,  tempBuildableObjectSORandomPrefab, originalBuildableFreeObject);
            if (buildableFreeObject) buildableFreeObject.SetUniqueID(uniqueID);
        }

        public BuildableFreeObject GetBuildableFreeObject() => buildableFreeObject;

        public BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;
    }
}