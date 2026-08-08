using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    public class GridAreaEnablerData
    {
        public Dictionary<GridArea, GridAreaData> GridAreaDataDictionary;

        public bool enableAllGridObjects;
        public List<BuildableGridObjectCategorySO> enableGridObjectCategoriesList;
        public List<BuildableGridObjectSO> enableGridObjectsList;

        public bool enableAllEdgeObjects;
        public List<BuildableEdgeObjectCategorySO> enableEdgeObjectCategoriesList;
        public List<BuildableEdgeObjectSO> enableEdgeObjectsList;

        public bool enableAllCornerObjects;
        public List<BuildableCornerObjectCategorySO> enableCornerObjectCategoriesList;
        public List<BuildableCornerObjectSO> enableCornerObjectsList;

        public bool enableAllFreeObjects;
        public List<BuildableFreeObjectCategorySO> enableFreeObjectCategoriesList;
        public List<BuildableFreeObjectSO> enableFreeObjectsList;

        public bool changeBlockedCellColor;
        public Color enabledCellHighlightColor;

        public GridAreaEnablerData()
        {
            this.GridAreaDataDictionary = new Dictionary<GridArea, GridAreaData>();

            this.enableAllGridObjects = false;
            this.enableGridObjectCategoriesList = new List<BuildableGridObjectCategorySO>();
            this.enableGridObjectsList = new List<BuildableGridObjectSO>();

            this.enableAllEdgeObjects = false;
            this.enableEdgeObjectCategoriesList = new List<BuildableEdgeObjectCategorySO>();
            this.enableEdgeObjectsList = new List<BuildableEdgeObjectSO>();

            this.enableAllCornerObjects = false;
            this.enableCornerObjectCategoriesList = new List<BuildableCornerObjectCategorySO>();
            this.enableCornerObjectsList = new List<BuildableCornerObjectSO>();

            this.enableAllFreeObjects = false;
            this.enableFreeObjectCategoriesList = new List<BuildableFreeObjectCategorySO>();
            this.enableFreeObjectsList = new List<BuildableFreeObjectSO>();

            this.changeBlockedCellColor = false;
            this.enabledCellHighlightColor = default;
        }

        public GridAreaEnablerData(Dictionary<GridArea, GridAreaData> GridAreaDataDictionary,
                                    bool enableAllGridObjects, List<BuildableGridObjectCategorySO> enableGridObjectCategoriesList, List<BuildableGridObjectSO> enableGridObjectsList,
                                    bool enableAllEdgeObjects, List<BuildableEdgeObjectCategorySO> enableEdgeObjectCategoriesList, List<BuildableEdgeObjectSO> enableEdgeObjectsList,
                                    bool enableAllCornerObjects, List<BuildableCornerObjectCategorySO> enableCornerObjectCategoriesList, List<BuildableCornerObjectSO> enableCornerObjectsList,
                                    bool enableAllFreeObjects, List<BuildableFreeObjectCategorySO> enableFreeObjectCategoriesList, List<BuildableFreeObjectSO> enableFreeObjectsList,
                                    bool changeBlockedCellColor, Color enabledCellHighlightColor) 
        {
            this.GridAreaDataDictionary = GridAreaDataDictionary ?? new Dictionary<GridArea, GridAreaData>();

            this.enableAllGridObjects = enableAllGridObjects;
            this.enableGridObjectCategoriesList = enableGridObjectCategoriesList ?? new List<BuildableGridObjectCategorySO>();
            this.enableGridObjectsList = enableGridObjectsList ?? new List<BuildableGridObjectSO>();

            this.enableAllEdgeObjects = enableAllEdgeObjects;
            this.enableEdgeObjectCategoriesList = enableEdgeObjectCategoriesList ?? new List<BuildableEdgeObjectCategorySO>();
            this.enableEdgeObjectsList = enableEdgeObjectsList ?? new List<BuildableEdgeObjectSO>();

            this.enableAllCornerObjects = enableAllCornerObjects;
            this.enableCornerObjectCategoriesList = enableCornerObjectCategoriesList ?? new List<BuildableCornerObjectCategorySO>();
            this.enableCornerObjectsList = enableCornerObjectsList ?? new List<BuildableCornerObjectSO>();

            this.enableAllFreeObjects = enableAllFreeObjects;
            this.enableFreeObjectCategoriesList = enableFreeObjectCategoriesList ?? new List<BuildableFreeObjectCategorySO>();
            this.enableFreeObjectsList = enableFreeObjectsList ?? new List<BuildableFreeObjectSO>();

            this.changeBlockedCellColor = changeBlockedCellColor;
            this.enabledCellHighlightColor = enabledCellHighlightColor;
        }
    }
}