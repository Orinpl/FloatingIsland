using System;
using UnityEngine;
using SoulGames.Utilities;

namespace SoulGames.EasyGridBuilderPro
{
    public abstract class BuildableObjectSO : ScriptableObject
    {
        public string objectName;
        public Sprite objectIcon;
        public BuildableObjectUICategorySO buildableObjectUICategorySO;

        [Serializable] public class RandomPrefabs
        {
            public Transform objectPrefab;
            public Transform ghostObjectPrefab;
            [Range(0, 100)] public float probability;
        }
        [Space]
        public RandomPrefabs[] randomPrefabs;
        public bool eachPlacementUseRandomPrefab = true;
        public Material validPlacementMaterial;
        public Material invalidPlacementMaterial;
        public bool setSpawnedObjectLayer = false;
        public LayerMask spawnedObjectLayer;

        public bool setGridSystemAsParent;

        public bool useObjectGridCustomAlphaMaskScale;
        public Vector2 objectGridCustomAlphaMaskScale;

        [Space]
        public LayerMask customSurfaceLayerMask;

        [Space]
        public bool raiseObjectWithVerticalGrids = true;

        // [Space]
        // public bool useCustomBuildConditions;
        // public List<CustomBuildConditionsSO> customBuildConditionsSOList;

        // [Space]
        // public bool useCustomPostBuildBehaviors;
        // public List<CustomBuildConditionsSO> customPostBuildBehaviorsSOList;
        
        [Space]
        public bool enablePlaceAndDeselect;

        [Space]
        public bool affectByBasicAreaDisablers = true;
        public bool affectByAreaDisablers = true;
        public bool affectByBasicAreaEnablers = true;
        public bool affectByAreaEnablers = true;

        [Space]
        public bool isObjectReplacable;
        // Not Added to the Custom Editor yet
        public bool replacingObjectIgnoreCustomConditions = true;
        public bool isObjectSelectable = true;
        public bool isSelectedObjectRotatable = true;
        public bool isObjectDestructable = true;
        public bool isObjectMovable = true;

        [Space]
        public bool enableTerrainInteractions;
        public bool flattenTerrain;
        public float flattenerSize;

        [Space]
        public bool removeTerrainDetails;
        public float detailsRemoverSize;

        [Space]
        public bool paintTerrainTexture;
        public int terrainTextureIndex = 0;
        public float painterBrushSize;
        public TerrainInteractionUtilities.BrushType painterBrushType = TerrainInteractionUtilities.BrushType.Hard;
        [Range(0f, 1f)] public float painterBrushOpacity = 1f;
        [Range(0f, 1f)] public float painterBrushFallOff = 0f;
    }
}