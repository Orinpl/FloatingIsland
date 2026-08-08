using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class GridAreaDisablerData
    {
        public Dictionary<GridArea, GridAreaData> GridAreaDataDictionary;

        public bool blockAllGridObjects;
        public List<BuildableGridObjectCategorySO> blockGridObjectCategoriesList;
        public List<BuildableGridObjectSO> blockGridObjectsList;

        public bool blockAllEdgeObjects;
        public List<BuildableEdgeObjectCategorySO> blockEdgeObjectCategoriesList;
        public List<BuildableEdgeObjectSO> blockEdgeObjectsList;

        public bool blockAllCornerObjects;
        public List<BuildableCornerObjectCategorySO> blockCornerObjectCategoriesList;
        public List<BuildableCornerObjectSO> blockCornerObjectsList;

        public bool blockAllFreeObjects;
        public List<BuildableFreeObjectCategorySO> blockFreeObjectCategoriesList;
        public List<BuildableFreeObjectSO> blockFreeObjectsList;

        public bool changeBlockedCellColor;
        public Color blockedCellHighlightColor;

        public GridAreaDisablerData()
        {
            this.GridAreaDataDictionary = new Dictionary<GridArea, GridAreaData>();

            this.blockAllGridObjects = false;
            this.blockGridObjectCategoriesList = new List<BuildableGridObjectCategorySO>();
            this.blockGridObjectsList = new List<BuildableGridObjectSO>();

            this.blockAllEdgeObjects = false;
            this.blockEdgeObjectCategoriesList = new List<BuildableEdgeObjectCategorySO>();
            this.blockEdgeObjectsList = new List<BuildableEdgeObjectSO>();

            this.blockAllCornerObjects = false;
            this.blockCornerObjectCategoriesList = new List<BuildableCornerObjectCategorySO>();
            this.blockCornerObjectsList = new List<BuildableCornerObjectSO>();

            this.blockAllFreeObjects = false;
            this.blockFreeObjectCategoriesList = new List<BuildableFreeObjectCategorySO>();
            this.blockFreeObjectsList = new List<BuildableFreeObjectSO>();

            this.changeBlockedCellColor = false;
            this.blockedCellHighlightColor = default;
        }

        public GridAreaDisablerData(Dictionary<GridArea, GridAreaData> GridAreaDataDictionary,
                                    bool blockAllGridObjects, List<BuildableGridObjectCategorySO> blockGridObjectCategoriesList, List<BuildableGridObjectSO> blockGridObjectsList,
                                    bool blockAllEdgeObjects, List<BuildableEdgeObjectCategorySO> blockEdgeObjectCategoriesList, List<BuildableEdgeObjectSO> blockEdgeObjectsList,
                                    bool blockAllCornerObjects, List<BuildableCornerObjectCategorySO> blockCornerObjectCategoriesList, List<BuildableCornerObjectSO> blockCornerObjectsList,
                                    bool blockAllFreeObjects, List<BuildableFreeObjectCategorySO> blockFreeObjectCategoriesList, List<BuildableFreeObjectSO> blockFreeObjectsList,
                                    bool changeBlockedCellColor, Color blockedCellHighlightColor) 
        {
            this.GridAreaDataDictionary = GridAreaDataDictionary ?? new Dictionary<GridArea, GridAreaData>();

            this.blockAllGridObjects = blockAllGridObjects;
            this.blockGridObjectCategoriesList = blockGridObjectCategoriesList ?? new List<BuildableGridObjectCategorySO>();
            this.blockGridObjectsList = blockGridObjectsList ?? new List<BuildableGridObjectSO>();

            this.blockAllEdgeObjects = blockAllEdgeObjects;
            this.blockEdgeObjectCategoriesList = blockEdgeObjectCategoriesList ?? new List<BuildableEdgeObjectCategorySO>();
            this.blockEdgeObjectsList = blockEdgeObjectsList ?? new List<BuildableEdgeObjectSO>();

            this.blockAllCornerObjects = blockAllCornerObjects;
            this.blockCornerObjectCategoriesList = blockCornerObjectCategoriesList ?? new List<BuildableCornerObjectCategorySO>();
            this.blockCornerObjectsList = blockCornerObjectsList ?? new List<BuildableCornerObjectSO>();

            this.blockAllFreeObjects = blockAllFreeObjects;
            this.blockFreeObjectCategoriesList = blockFreeObjectCategoriesList ?? new List<BuildableFreeObjectCategorySO>();
            this.blockFreeObjectsList = blockFreeObjectsList ?? new List<BuildableFreeObjectSO>();

            this.changeBlockedCellColor = changeBlockedCellColor;
            this.blockedCellHighlightColor = blockedCellHighlightColor;
        }
    }
}