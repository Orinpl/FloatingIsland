using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [Serializable]
    public class BuiltObjectSaveData
    {
        public string buildableObjectUniqueID;

        public GridAxis gridAxis = GridAxis.XZ;
        public bool activateGizmos = true;
        public bool lockAutoGenerationAndValues;
        public Vector3 objectScale;
        public Vector3 objectCenter;
        public Vector3 objectCustomPivot;
        public float debugCellSize;
        public Vector2Int objectSizeRelativeToCellSize;
        public bool activeSceneObject;
        public BuildableObjectSO sceneObjectBuildableCornerObjectSO;
        public FourDirectionalRotation sceneObjectFourDirectionalRotation;
        public EightDirectionalRotation sceneObjectEightDirectionalRotation;
        public float sceneObjectFreeRotation;
        public int verticalGridIndex;

        public bool isInstantiatedByGhostObject;

        public string occupiedGridSystemUniqueID;
        public int occupiedVerticalGridIndex;
        public float occupiedCellSize;
        public BuildableObjectSO buildableObjectSO;
        public BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        public Vector3 objectOriginWorldPosition;
        public Vector3 objectModifiedOriginWorldPosition;
        public Vector3 objectOffset;
        public Vector2Int objectOriginCellPosition;
        public List<Vector2Int> objectCellPositionList;
        public CornerObjectCellDirection cornerObjectOriginCellDirection;
        public FourDirectionalRotation objectFourDirectionalRotation;
        public EightDirectionalRotation objectEightDirectionalRotation;
        public float objectFreeRotation;
        public bool isObjectFlipped;
        public Vector3 hitNormals;
        public bool objectPlacementInvokedAsSecondaryPlacement;
    }
}