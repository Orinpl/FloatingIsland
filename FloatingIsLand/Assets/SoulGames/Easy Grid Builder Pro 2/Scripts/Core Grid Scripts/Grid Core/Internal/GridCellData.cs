using System.Collections.Generic;

namespace SoulGames.EasyGridBuilderPro
{
    public struct GridCellData
    {
        private Dictionary<BuildableGridObjectCategorySO, BuildableGridObject> buildableGridObject;
        private Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectNorth;
        private Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectEast;
        private Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectSouth;
        private Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectWest;
        private Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectNorthEast;
        private Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectSouthEast;
        private Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectSouthWest;
        private Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectNorthWest;
        private List<BuildableFreeObject> buildableFreeObject;
        private Dictionary<GridModifierSO, float> customModifier;

        public GridCellData (Dictionary<BuildableGridObjectCategorySO, BuildableGridObject> buildableGridObject,
                            Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectNorth,
                            Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectEast,
                            Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectSouth,
                            Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjectWest,
                            Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectNorthEast,
                            Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectSouthEast,
                            Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectSouthWest,
                            Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjectNorthWest,
                            List<BuildableFreeObject> buildableFreeObject,
                            Dictionary<GridModifierSO, float> customModifier)
        {
            this.buildableGridObject = buildableGridObject;
            this.buildableEdgeObjectNorth = buildableEdgeObjectNorth;
            this.buildableEdgeObjectEast = buildableEdgeObjectEast;
            this.buildableEdgeObjectSouth = buildableEdgeObjectSouth;
            this.buildableEdgeObjectWest = buildableEdgeObjectWest;
            this.buildableCornerObjectNorthEast = buildableCornerObjectNorthEast;
            this.buildableCornerObjectSouthEast = buildableCornerObjectSouthEast;
            this.buildableCornerObjectSouthWest = buildableCornerObjectSouthWest;
            this.buildableCornerObjectNorthWest = buildableCornerObjectNorthWest;
            this.buildableFreeObject = buildableFreeObject;
            this.customModifier = customModifier;
        }
        
        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///

        public Dictionary<BuildableGridObjectCategorySO, BuildableGridObject> GetBuildableGridObjectData()
        {
            if (buildableGridObject == null) buildableGridObject = new Dictionary<BuildableGridObjectCategorySO, BuildableGridObject>();
            return buildableGridObject;
        }

        public Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> GetBuildableEdgeObjectNorthData()
        {
            if (buildableEdgeObjectNorth == null) buildableEdgeObjectNorth = new Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject>();
            return buildableEdgeObjectNorth;
        }

        public Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> GetBuildableEdgeObjectEastData()
        {
            if (buildableEdgeObjectEast == null) buildableEdgeObjectEast = new Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject>();
            return buildableEdgeObjectEast;
        }

        public Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> GetBuildableEdgeObjectSouthData()
        {
            if (buildableEdgeObjectSouth == null) buildableEdgeObjectSouth = new Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject>();
            return buildableEdgeObjectSouth;
        }

        public Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> GetBuildableEdgeObjectWestData()
        {
            if (buildableEdgeObjectWest == null) buildableEdgeObjectWest = new Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject>();
            return buildableEdgeObjectWest;
        }

        public Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> GetBuildableCornerObjectNorthEastData()
        {
            if (buildableCornerObjectNorthEast == null) buildableCornerObjectNorthEast = new Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject>();
            return buildableCornerObjectNorthEast;
        }

        public Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> GetBuildableCornerObjectSouthEastData()
        {
            if (buildableCornerObjectSouthEast == null) buildableCornerObjectSouthEast = new Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject>();
            return buildableCornerObjectSouthEast;
        }

        public Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> GetBuildableCornerObjectSouthWestData()
        {
            if (buildableCornerObjectSouthWest == null) buildableCornerObjectSouthWest = new Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject>();
            return buildableCornerObjectSouthWest;
        }

        public Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> GetBuildableCornerObjectNorthWestData()
        {
            if (buildableCornerObjectNorthWest == null) buildableCornerObjectNorthWest = new Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject>();
            return buildableCornerObjectNorthWest;
        }

        public List<BuildableFreeObject> GetBuildableFreeObjectData()
        {
            if (buildableFreeObject == null) buildableFreeObject = new List<BuildableFreeObject>();
            return buildableFreeObject;
        }

        public float GetCustomModifierValue(GridModifierSO gridModifierSO)
        {
            if (customModifier == null) customModifier = new Dictionary<GridModifierSO, float>();

            if (customModifier.ContainsKey(gridModifierSO)) return customModifier[gridModifierSO];
            return default;
        }

        public Dictionary<GridModifierSO, float> GetCustomModifierData()
        {
            if (customModifier == null) customModifier = new Dictionary<GridModifierSO, float>();

            return customModifier;
        }
    }
}