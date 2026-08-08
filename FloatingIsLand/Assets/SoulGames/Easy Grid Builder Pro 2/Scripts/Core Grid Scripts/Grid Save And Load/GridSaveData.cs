using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [Serializable]
    public class GridSaveData
    {
        public string gridUniqueID;

        public Vector3 gridPosition;
        public int gridWidth;
        public int gridLength;
        public float cellSize;
        public bool updateGridPositionRuntime;
        public bool updateGridWidthAndLengthRuntime;
        public GridOrigin gridOriginType;

        public List<BuildableGridObjectSO> buildableGridObjectSOList;
        public List<BuildableEdgeObjectSO> buildableEdgeObjectSOList;
        public List<BuildableCornerObjectSO> buildableCornerObjectSOList;
        public List<BuildableFreeObjectSO> buildableFreeObjectSOList;
    }
}