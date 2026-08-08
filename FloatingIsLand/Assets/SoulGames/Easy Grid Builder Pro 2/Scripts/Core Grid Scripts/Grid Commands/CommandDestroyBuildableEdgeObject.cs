using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class CommandDestroyBuildableEdgeObject : ICommand
    {
        private string uniqueID;

        private BuildableEdgeObject buildableEdgeObject;
        private bool byPassEventsAndMessages;
        private bool detachInstead;

        private EasyGridBuilderPro easyGridBuilderPro;
        private Vector2Int originCellPosition;
        private Vector3 objectOffset;
        private Vector3 worldPosition;
        private BuildableEdgeObjectSO buildableEdgeObjectSO;
        private FourDirectionalRotation fourDirectionalDirection;
        private bool isObjectFlipped;
        private CornerObjectCellDirection cornerObjectOriginCellDirection;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private int verticalGridIndex;

        private bool isDestructionSuccessful;

        public CommandDestroyBuildableEdgeObject(BuildableEdgeObject buildableEdgeObject, bool byPassEventsAndMessages, bool detachInstead)
        {
            this.uniqueID = buildableEdgeObject.GetUniqueID();
            this.buildableEdgeObject = buildableEdgeObject;
            this.byPassEventsAndMessages = byPassEventsAndMessages;
            this.detachInstead = detachInstead;

            easyGridBuilderPro = buildableEdgeObject.GetOccupiedGridSystem();
            originCellPosition = buildableEdgeObject.GetObjectOriginCellPosition(out _);
            objectOffset = buildableEdgeObject.GetObjectOffset();
            worldPosition = buildableEdgeObject.GetObjectOriginWorldPosition();
            buildableEdgeObjectSO = (BuildableEdgeObjectSO)buildableEdgeObject.GetBuildableObjectSO();
            fourDirectionalDirection = buildableEdgeObject.GetObjectFourDirectionalRotation();
            isObjectFlipped = buildableEdgeObject.GetIsObjectFlipped();
            cornerObjectOriginCellDirection = buildableEdgeObject.GetCornerObjectOriginCellDirection();
            buildableObjectSORandomPrefab = buildableEdgeObject.GetBuildableObjectSORandomPrefab();
            verticalGridIndex = buildableEdgeObject.GetOccupiedVerticalGridIndex();
        }
        
        public void Execute()
        {
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            if (buildableObjectDestroyer.TryDestroyBuildableEdgeObjectByUniqueID(uniqueID, byPassEventsAndMessages, detachInstead)) isDestructionSuccessful = true;
        }

        public void Undo()
        {
            BuildableObjectSO.RandomPrefabs tempBuildableObjectSORandomPrefab = buildableObjectSORandomPrefab;
            easyGridBuilderPro.InvokeTryPlaceBuildableEdgeObjectSinglePlacement(originCellPosition, buildableEdgeObjectSO, fourDirectionalDirection, isObjectFlipped, ref tempBuildableObjectSORandomPrefab,
                    worldPosition, cornerObjectOriginCellDirection, true, true, verticalGridIndex, true, out BuildableEdgeObject restoredObject, objectOffset, buildableEdgeObject);
            if (restoredObject) restoredObject.SetUniqueID(uniqueID);
        }

        public void Redo()
        {
            Execute();
        }

        public bool GetIsDestructionSuccessful() => isDestructionSuccessful;
    }
}