using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class  GridAreaData
    {
        public bool affectAllVerticalGrids;

        public EasyGridBuilderPro currentOccupiedEasyGridBuilderPro;
        public EasyGridBuilderPro previousOccupiedEasyGridBuilderPro;

        public Grid currentOccupiedGrid;
        public Grid previousOccupiedGrid;

        public List<Vector2Int> currentOccupiedCellPositionList;
        public List<Vector2Int> previousOccupiedCellPositionList;
        
        public List<Vector2Int> currentGridAllOccupiedCellPositionList;
        public List<Vector2Int> previousGridAllOccupiedCellPositionList;

        public bool currentEasyGridBuilderProChangedDynamicTrigger;
        public bool currentGridChangedDynamicTrigger;
        public bool initializedAsGridAreaModifier;

        public GridAreaData()
        {
            this.affectAllVerticalGrids = false;

            this.currentOccupiedEasyGridBuilderPro = null;
            this.previousOccupiedEasyGridBuilderPro = null;

            this.currentOccupiedGrid = null;
            this.previousOccupiedGrid = null;

            this.currentOccupiedCellPositionList = new List<Vector2Int>();
            this.previousOccupiedCellPositionList = new List<Vector2Int>();

            this.currentGridAllOccupiedCellPositionList = new List<Vector2Int>();
            this.previousGridAllOccupiedCellPositionList = new List<Vector2Int>();

            this.currentEasyGridBuilderProChangedDynamicTrigger = true;
            this.currentGridChangedDynamicTrigger = true;
            this.initializedAsGridAreaModifier = false;
        }

        public GridAreaData(bool affectAllVerticalGrids, EasyGridBuilderPro currentOccupiedEasyGridBuilderPro, EasyGridBuilderPro previousOccupiedEasyGridBuilderPro,
                            Grid currentOccupiedGrid, Grid previousOccupiedGrid, List<Vector2Int> currentOccupiedCellPositionList, List<Vector2Int> previousOccupiedCellPositionList, 
                            List<Vector2Int> currentGridAllOccupiedCellPositionList, List<Vector2Int> previousGridAllOccupiedCellPositionList,
                            bool currentEasyGridBuilderProChangedDynamicTrigger, bool currentGridChangedDynamicTrigger, bool initializedAsGridAreaModifier)
        {
            this.affectAllVerticalGrids = affectAllVerticalGrids;

            this.currentOccupiedEasyGridBuilderPro = currentOccupiedEasyGridBuilderPro;
            this.previousOccupiedEasyGridBuilderPro = previousOccupiedEasyGridBuilderPro;

            this.currentOccupiedGrid = currentOccupiedGrid;
            this.previousOccupiedGrid = previousOccupiedGrid;

            this.currentOccupiedCellPositionList = currentOccupiedCellPositionList ?? new List<Vector2Int>();
            this.previousOccupiedCellPositionList = previousOccupiedCellPositionList ?? new List<Vector2Int>();

            this.currentGridAllOccupiedCellPositionList = currentGridAllOccupiedCellPositionList ?? new List<Vector2Int>();
            this.previousGridAllOccupiedCellPositionList = previousGridAllOccupiedCellPositionList ?? new List<Vector2Int>();

            this.currentEasyGridBuilderProChangedDynamicTrigger = currentEasyGridBuilderProChangedDynamicTrigger;
            this.currentGridChangedDynamicTrigger = currentGridChangedDynamicTrigger;
            this.initializedAsGridAreaModifier = initializedAsGridAreaModifier;
        }
    }
}