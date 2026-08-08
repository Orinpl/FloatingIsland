using System;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{   
    [RequireComponent(typeof(GridDataHandler))]
    public abstract class EasyGridBuilderPro : MonoBehaviour
    {
        public static event OnGridSystemCreatedDelegate OnGridSystemCreated;
        public delegate void OnGridSystemCreatedDelegate(EasyGridBuilderPro easyGridBuilderPro);

        [SerializeField] private string gridUniqueID;
        [SerializeField] protected bool updateInEditor = true;

        [Space]
        [SerializeField] protected List<BuildableGridObjectSO> buildableGridObjectSOList = null;
        [SerializeField] protected List<BuildableEdgeObjectSO> buildableEdgeObjectSOList = null;
        [SerializeField] protected List<BuildableCornerObjectSO> buildableCornerObjectSOList = null;
        [SerializeField] protected List<BuildableFreeObjectSO> buildableFreeObjectSOList = null;
        
        [Space]
        [SerializeField] protected int gridWidth;
        [SerializeField] protected int gridLength;
        [SerializeField] protected float cellSize;
        [SerializeField] protected Vector3 activeGridOrigin;
        [SerializeField] protected bool updateGridPositionRuntime;
        [SerializeField] protected GridOrigin gridOriginType = GridOrigin.Center;
        [SerializeField] protected bool updateGridWidthAndLengthRuntime;

        [Space]
        [SerializeField] protected GridMode activeGridMode = GridMode.None;
        [SerializeField] protected CameraMode activeCameraMode = CameraMode.TopDown;

        [Space]
        [SerializeField] [Min(1)] protected int verticalGridsCount = 1;
        [SerializeField] [Min(0)] protected float verticalGridHeight = 2.5f;
        [SerializeField] protected bool switchVerticalGridWithInput = true;
        [SerializeField] protected bool autoDetectVerticalGrid;
        [SerializeField] protected LayerMask customSurfaceLayerMask;
        [SerializeField] protected LayerMask autoDetectionLayerMask;

        [Space]
        [SerializeField] [Range(0, 10)] protected float colliderSizeMultiplier = 5;
        [SerializeField] protected bool lockColliderAtBottomGrid = true;
        
        [Space]
        [SerializeField] protected bool displayCanvasGrid;
        [Serializable] public class CanvasGridSettings
        {
            [SerializeField] public Canvas canvasGridPrefab;
            [SerializeField] public Sprite cellImageSprite;
            [SerializeField] public Color gridShowColor = new Color32(255, 255, 255, 255);
            [SerializeField] public Color gridHideColor = new Color32(255, 255, 255, 0);
            [SerializeField] public float colorTransitionSpeed = 20f;
            [SerializeField] public bool displayOnDefaultMode = true;
            [SerializeField] public bool displayOnBuildMode = true;
            [SerializeField] public bool displayOnDestroyMode;
            [SerializeField] public bool displayOnSelectMode;
            [SerializeField] public bool displayOnMoveMode;
            [SerializeField] public bool onlyShowActiveVerticalGrid = true;
            [SerializeField] public bool alwaysLockAtFirstGrid;
        }
        [SerializeField] protected CanvasGridSettings canvasGridSettings;

        [Space]
        [SerializeField] protected bool displayObjectGrid;
        [Serializable] public class ObjectGridSettings
        {
            [SerializeField] public Transform objectGridPrefab;
            [SerializeField] public Texture cellImageTexture;
            [SerializeField] public Color gridShowColor = new Color32(255, 255, 255, 255);
            [SerializeField] public Color gridHideColor = new Color32(255, 255, 255, 0);
            [SerializeField] [ColorUsage(true, true)] public Color gridShowColorHDR = new Color32(0, 0, 0, 255);
            [SerializeField] [ColorUsage(true, true)] public Color gridHideColorHDR = new Color32(0, 0, 0, 0);
            [SerializeField] public float colorTransitionSpeed = 20f;
            [SerializeField] public bool displayOnDefaultMode = true;
            [SerializeField] public bool displayOnBuildMode = true;
            [SerializeField] public bool displayOnDestroyMode;
            [SerializeField] public bool displayOnSelectMode;
            [SerializeField] public bool displayOnMoveMode;
            [SerializeField] public bool onlyShowActiveVerticalGrid = true;
            [SerializeField] public bool alwaysLockAtFirstGrid;
            [Space]
            [SerializeField] public bool useAlphaMask;
            [SerializeField] public Texture alphaMaskSprite;
            [SerializeField] public float alphaMaskSize;
            [SerializeField] public Vector2 alphaMaskWorldPosition;
            [SerializeField] public bool alphaMaskFollowCursor;
            [SerializeField] public bool alphaMaskFollowGhostObject;
            [SerializeField] public bool alphaMaskAddGhostObjectScale;
            [SerializeField] public LayerMask customSurfaceLayerMask;
            [SerializeField] public bool activateOnDefaultMode = true;
            [SerializeField] public bool activateOnBuildMode = true;
            [SerializeField] public bool activateOnDestroyMode;
            [SerializeField] public bool activateOnSelectMode;
            [SerializeField] public bool activateOnMoveMode;
            [Space]
            [SerializeField] public bool useScrollingNoise;
            [SerializeField] public Texture noiseTexture;
            [SerializeField] public Vector2 textureTiling;
            [SerializeField] public Vector2 textureScrolling = new Vector2(0, 0.1f);
        }
        [SerializeField] protected ObjectGridSettings objectGridSettings;

        [Space]
        [SerializeField] protected bool displayRuntimeTextGrid;
        [Serializable] public class RuntimeTextGridSettings
        {
            [SerializeField] public Transform textPrefab;
            [SerializeField] public bool displayCellPositionText;
            [SerializeField] [Range(0, 200)] public float textSizePercentage = 80;
            [SerializeField] public Vector3 textGridCustomOffset;
            [SerializeField] public Color textColorOverride = new Color32(255, 255, 255, 255);
            [SerializeField] public bool displayOnDefaultMode = true;
            [SerializeField] public bool displayOnBuildMode = true;
            [SerializeField] public bool displayOnDestroyMode;
            [SerializeField] public bool displayOnSelectMode;
            [SerializeField] public bool displayOnMoveMode;
            [SerializeField] public bool onlyShowActiveVerticalGrid = true;
            [SerializeField] public bool onlySpawnFirstVerticalGrid = true;
            [SerializeField] public bool alwaysLockAtFirstGrid;
        }
        [SerializeField] protected RuntimeTextGridSettings runtimeTextGridSettings;
        
        [Space]
        [SerializeField] protected bool displayRuntimeProceduralGrid;
        [Serializable] public class RuntimeProceduralGridSettings
        {
            [SerializeField] public int seed;
            [SerializeField] public bool preserveSeedPostGeneration = false;

            [SerializeField] public bool usePerlinNoise;
            [SerializeField] public int scrollNoiseHorizontally;
            [SerializeField] public int scrollNoiseVertically;
            [SerializeField] [Range(1, 20)] public float noiseScale = 20;
            [SerializeField] public List<NodeObjects> nodeObjects;
            [SerializeField] public List<NodeObjects> borderNodeObjects;
            [SerializeField] [Min(0)] public int borderTilesAmount;

            [Serializable] public class NodeObjects
            {
                [SerializeField] public bool keepEmpty;
                
                [Serializable] public class NodePrefabs
                {
                    [SerializeField] public Transform nodePrefab;
                    [SerializeField] [Range(0, 100)] public float probability;
                }
                [SerializeField] public NodePrefabs[] nodePrefabs;

                [SerializeField] public Vector3 prefabTrueScale = Vector3.one;
                [SerializeField] [Range(0, 200)] public float nodeSizePercentage = 100;
                [SerializeField] public Vector2 randomLocalOffsetX;
                [SerializeField] public Vector2 randomLocalOffsetY;
                [SerializeField] public Vector2 randomLocalOffsetZ;
                [SerializeField] [Min(0.01f)] public float roundRandomValueTo = 0.01f;
            }

            [SerializeField] [Min(0)] public float delayBetweenSpawns = 0f;
            [SerializeField] public bool spawnRowByRow = false;

            [SerializeField] public bool displayOnDefaultMode = true;
            [SerializeField] public bool displayOnBuildMode = true;
            [SerializeField] public bool displayOnDestroyMode;
            [SerializeField] public bool displayOnSelectMode;
            [SerializeField] public bool displayOnMoveMode;
            [SerializeField] public bool onlyShowActiveVerticalGrid;
            [SerializeField] public bool onlySpawnFirstVerticalGrid = true;
            [SerializeField] public bool alwaysLockAtFirstGrid;
        }
        [SerializeField] protected RuntimeProceduralGridSettings runtimeProceduralGridSettings;

        ///-------------------------------------------------------------------------------///
        /// GRID EDITOR FUNCTIONS                                                         ///
        ///-------------------------------------------------------------------------------///

        #if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            UpdateEditorVisualHandlerComponent();
            UpdateSafetyChecks();
            UpdateGridOriginType();
            GenerateGridUniqueID();
        }

        protected abstract void UpdateEditorVisualHandlerComponent();
        protected abstract void UpdateSafetyChecks();

        public void GenerateGridUniqueID(bool forceRefreshID = false)
        {
            if (gridUniqueID.Length > 0 && !forceRefreshID) return;
            gridUniqueID = Guid.NewGuid().ToString();
        }
        #endif

        ///-------------------------------------------------------------------------------///
        /// GRID INITIALIZE FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///

        protected virtual void Start()
        {
            RunVisualGridsSafetyChecks();
            SetupVerticalGrids();
            SetupGridCollider();
            SetupGridVisuals();
        }
        
        protected abstract void RunVisualGridsSafetyChecks();
        protected abstract void SetupVerticalGrids();
        protected abstract void SetupGridCollider();
        protected abstract void SetupGridVisuals();

        ///-------------------------------------------------------------------------------///
        /// GRID UPDATE FUNCTIONS                                                         ///
        ///-------------------------------------------------------------------------------///
        
        protected virtual void Update()
        {
            UpdateGridOrigin();
            UpdateGriCollider();
            UpdateGridOriginType();
            UpdateGridWidthAndLength();
            UpdateGridVisuals();
            UpdateAutoDetectAndSetVerticalGrid();
            UpdatePlacementKeyHodling();
            UpdateRotationKeyHolding();
        }
        
        protected abstract void UpdateGridOrigin();
        protected abstract void UpdateGriCollider();
        protected abstract void UpdateGridOriginType();
        protected abstract void UpdateGridWidthAndLength();
        protected abstract void UpdateGridVisuals();
        protected abstract void UpdateAutoDetectAndSetVerticalGrid();
        protected abstract void UpdatePlacementKeyHodling();
        protected abstract void UpdateRotationKeyHolding();

        ///-------------------------------------------------------------------------------///
        /// INPUT HANDLING FUNCTIONS                                                      ///
        ///-------------------------------------------------------------------------------///
        
        public abstract void SetInputGridModeVariables(bool useBuildModeActivationKey, bool useDestroyModeActivationKey, bool useSelectModeActivationKey, bool useMoveModeActivationKey);
        public abstract void SetInputActiveGridMode(GridMode gridMode);
        public abstract void SetInputCycleThroughGridModes();
        public abstract void SetInputCycleThroughGridModes(Vector2 inputDirection);
        public abstract void SetInputGridModeReset();
        public abstract void SetInputPlacementReset(bool blockedByFreeObjectSplinePlacementComplete = false);

        public abstract void SetInputVerticalGrid(int newGridIndex);
        public abstract void SetInputMoveUpVerticalGrid();
        public abstract void SetInputMoveDownVerticalGrid();
        public abstract void SetInputCycleThroughVerticalGrids(Vector2 value);

        public abstract void SetInputActiveBuildableObjectSO(BuildableObjectSO buildableObjectSO, BuildableObjectSO.RandomPrefabs randomPrefab = null, bool onlySetBuildableExistInBuildablesList = false);
        public abstract void SetInputCycleThroughBuildableObjectsSOList(Vector2 inputDirection);
        public abstract void SetInputCycleThroughBuildableObjectsSOListUp();
        public abstract void SetInputCycleThroughBuildableObjectsSOListDown();
        public abstract void SetInputCycleThroughActiveBuildableObjectSOPlacementType(Vector2 inputDirection);
        public abstract void SetInputCycleThroughActiveBuildableObjectSOPlacementTypeUp();
        public abstract void SetInputCycleThroughActiveBuildableObjectSOPlacementTypeDown();

        public abstract void SetInputBuildableObjectRotationScroll(Vector2 inputDirection);
        public abstract void SetInputBuildableObjectRotationScrollComplete();
        public abstract void SetInputBuildableObjectCounterClockwiseRotation(bool invokeByUI = false);
        public abstract void SetInputBuildableObjectCounterClockwiseRotationComplete();
        public abstract void SetInputBuildableObjectClockwiseRotation(bool invokeByUI = false);
        public abstract void SetInputBuildableObjectClockwiseRotationComplete();
        public abstract void SetInputBuildableEdgeObjectFlip();

        public abstract void SetInputBuildableFreeObjectSplinePlacementComplete();
        public abstract void SetInputCycleThroughBuildableFreeObjectSplineSpacing(Vector2 inputDirection);
        public abstract void SetInputIncreaseBuildableFreeObjectSplineSpacing(float amount = 1f);
        public abstract void SetInputDecreaseBuildableFreeObjectSplineSpacing(float amount = 1f);

        public void SetInputBuildableObjectPlacement(BuildableObjectSO buildableObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection = FourDirectionalRotation.North, 
        EightDirectionalRotation eightDirectionalDirection = EightDirectionalRotation.North, float freeRotation = 0f, bool isBuildableEdgeObjectFlipped = false, int verticalGridIndex = -1) =>
        SetInputBuildableObjectPlacement(false, false, buildableObjectSO, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, isBuildableEdgeObjectFlipped, verticalGridIndex);

        public void SetInputBuildableGridObjectPlacement(BuildableGridObjectSO buildableGridObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection = FourDirectionalRotation.North,
        int verticalGridIndex = -1) =>
        SetInputBuildableObjectPlacement(false, false, buildableGridObjectSO, worldPosition, fourDirectionalDirection, activeVerticalGridIndex: verticalGridIndex);

        public void SetInputBuildableEdgeObjectPlacement(BuildableEdgeObjectSO buildableEdgeObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection = FourDirectionalRotation.North,
        bool isBuildableEdgeObjectFlipped = false, int verticalGridIndex = -1) =>
        SetInputBuildableObjectPlacement(false, false, buildableEdgeObjectSO, worldPosition, fourDirectionalDirection, isBuildableEdgeObjectFlipped: isBuildableEdgeObjectFlipped, activeVerticalGridIndex: verticalGridIndex);

        public void SetInputBuildableCornerObjectPlacement(BuildableCornerObjectSO buildableCornerObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection = FourDirectionalRotation.North, 
        EightDirectionalRotation eightDirectionalDirection = EightDirectionalRotation.North, float freeRotation = 0f, int verticalGridIndex = -1) =>
        SetInputBuildableObjectPlacement(false, false, buildableCornerObjectSO, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, activeVerticalGridIndex: verticalGridIndex);

        public abstract void SetInputBuildableObjectPlacement(bool isSceneObject = false, bool calledInternally = true ,BuildableObjectSO activeBuildableObjectSO = null, Vector3 worldPosition = default, 
        FourDirectionalRotation fourDirectionalDirection = FourDirectionalRotation.North, EightDirectionalRotation eightDirectionalDirection = EightDirectionalRotation.North, float freeRotation = 0f, 
        bool isBuildableEdgeObjectFlipped = false, int activeVerticalGridIndex = -1,  bool calledFromPlacementCancelled = false, bool forceFreeObjectRotation = false);

        public abstract bool TryInitializeBuildableGridObjectSinglePlacement(Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, 
        bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, out BuildableGridObject buildableGridObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null,
        BuildableGridObject originalBuildableGridObject = null);

        public abstract bool TryInitializeBuildableEdgeObjectSinglePlacement(Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
        bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, out BuildableEdgeObject buildableEdgeObject, 
        BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableEdgeObject originalBuildableEdgeObject = null);

        public abstract bool TryInitializeBuildableCornerObjectSinglePlacement(Vector3 worldPosition, BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
        EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
        out BuildableCornerObject buildableCornerObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableCornerObject originalBuildableCornerObject = null);

        public abstract bool TryInitializeBuildableFreeObjectSinglePlacement(Vector3 worldPosition, BuildableFreeObjectSO buildableFreeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
        EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, bool ignoreBuildConditions, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
        out BuildableFreeObject buildableFreeObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableFreeObject originalBuildableFreeObject = null);

        public abstract void InvokeTryPlaceBuildableGridObjectSinglePlacement(Vector2Int originCellPosition, Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, 
        ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, 
        out BuildableGridObject buildableGridObject, BuildableGridObject originalBuildableGridObject = null);

        public abstract void InvokeTryPlaceBuildableEdgeObjectSinglePlacement(Vector2Int buildableObjectOriginCellPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, 
        FourDirectionalRotation fourDirectionalDirection, bool isBuildableEdgeObjectFlipped, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, Vector3 cellWorldPosition, 
        CornerObjectCellDirection cornerObjectOriginCellDirection, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
        out BuildableEdgeObject buildableEdgeObject, Vector3 offset = default, BuildableEdgeObject originalBuildableEdgeObject = null);

        public abstract void InvokeTryPlaceBuildableCornerObjectSinglePlacement(Vector2Int buildableObjectOriginCellPosition, BuildableCornerObjectSO buildableCornerObjectSO, 
        FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, 
        Vector3 cellWorldPosition, CornerObjectCellDirection cornerObjectOriginCellDirection, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages,
        bool invokedAsSecondaryPlacement, out BuildableCornerObject buildableCornerObject, Vector3 offset = default, BuildableCornerObject originalBuildableCornerObject = null);

        public abstract void InvokeTryPlaceBuildableFreeObjectSinglePlacement(BuildableFreeObjectSO buildableFreeObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection, 
        EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, bool ignoreBuildConditions, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
        out BuildableFreeObject buildableFreeObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableFreeObject originalBuildableFreeObject = null);

        public abstract void SetInputBuildableObjectPlacementComplete(bool placeMovingObjectOnInputRelease = false);

        public abstract void SetInputToggleHeatMapMode();
        public abstract void SetInputEnableHeatMapMode();
        public abstract void SetInputDisableHeatMapMode();

        ///-------------------------------------------------------------------------------///
        /// GRID VALIDATION FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///

        public abstract bool IsWithinActiveGridBounds(Vector2Int cellPosition);
        public abstract bool IsWithinGridBounds(Vector2Int cellPosition, int verticalGridIndex);

        public abstract bool IsGridObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableGridObjectSO buildingGridObjectSO, Vector2Int cellPosition);
        public abstract bool IsEdgeObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableEdgeObjectSO buildingEdgeObjectSO, Vector2Int cellPosition, EdgeObjectCellDirection edgeObjectCellDirection);
        public abstract bool IsCornerObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableCornerObjectSO buildingCornerObjectSO, Vector2Int cellPosition, CornerObjectCellDirection cornerObjectCellDirection);
        public abstract bool IsFreeObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableFreeObjectSO buildingFreeObjectSO, Vector2Int cellPosition);
        public abstract bool IsBuildableObjectDisabledByBasicGridAreaDisablers();
        public abstract bool IsGridObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableGridObjectSO buildingGridObjectSO, Vector2Int cellPosition);
        public abstract bool IsEdgeObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableEdgeObjectSO buildingEdgeObjectSO, Vector2Int cellPosition, EdgeObjectCellDirection edgeObjectCellDirection);
        public abstract bool IsCornerObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableCornerObjectSO buildingCornerObjectSO, Vector2Int cellPosition, CornerObjectCellDirection cornerObjectCellDirection);
        public abstract bool IsFreeObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableFreeObjectSO buildingFreeObjectSO, Vector2Int cellPosition);
        public abstract bool IsBuildableObjectEnabledByBasicGridAreaEnablers();

        ///-------------------------------------------------------------------------------///
        /// MODULE SUPPORTER FUNCTIONS                                                    ///
        ///-------------------------------------------------------------------------------///

        public abstract bool CheckPlacementDistanceForGhostObject(Vector3 worldPosition);

        ///-------------------------------------------------------------------------------///
        /// MODULE SUPPORTER FUNCTIONS                                                    ///
        ///-------------------------------------------------------------------------------///

        public void GenerateRandomSeed()
        {
            System.Random random = new System.Random();
            runtimeProceduralGridSettings.seed = random.Next(int.MinValue, int.MaxValue);
        }

        ///-------------------------------------------------------------------------------///
        /// EVENT RAISER FUNCTIONS                                                        ///
        ///-------------------------------------------------------------------------------///

        protected void RaiseOnGridSystemCreated(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnGridSystemCreated?.Invoke(easyGridBuilderPro);
        }

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS - EXTERNAL                                            ///
        ///-------------------------------------------------------------------------------///

        public abstract bool GetIsUpdateInEditor();

        public abstract List<BuildableGridObjectSO> GetBuildableGridObjectSOList();
        public abstract List<BuildableEdgeObjectSO> GetBuildableEdgeObjectSOList();
        public abstract List<BuildableCornerObjectSO> GetBuildableCornerObjectSOList();
        public abstract List<BuildableFreeObjectSO> GetBuildableFreeObjectSOList();

        public abstract int GetGridWidth();
        public abstract int GetGridLength();
        public abstract float GetCellSize();
        public abstract bool GetIsUpdateGridPositionRuntime();
        public abstract GridOrigin GetGridOriginType();
        public abstract bool GetIsUpdateGridWidthAndLengthRuntime();

        public abstract GridMode GetActiveGridMode();
        public abstract CameraMode GetActiveCameraMode();
        
        public abstract int GetVerticalGridsCount();
        public abstract float GetVerticalGridHeight();
        public abstract bool GetIsSwitchVerticalGridWithInput();
        public abstract bool GetIsAutoDetectVerticalGrid();
        public abstract LayerMask GetVerticalGridAutoDetectionLayer();

        public abstract float GetColliderSizeMultiplier();
        public abstract bool GetIsLockColliderAtBottomGrid();
        
        public abstract bool GetIsDisplayCanvasGrid();
        public abstract CanvasGridSettings GetCanvasGridSettings();
        public abstract bool GetIsDisplayObjectGrid();
        public abstract ObjectGridSettings GetObjectGridSettings();
        public abstract bool GetIsDisplayRuntimeTextGrid();
        public abstract RuntimeTextGridSettings GetRuntimeTextGridSettings();
        public abstract bool GetIsDisplayRuntimeProceduralGrid();
        public abstract RuntimeProceduralGridSettings GetRuntimeProceduralGridSettings();

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS - INTERNAL                                            ///
        ///-------------------------------------------------------------------------------///

        public string GetGridUniqueID() => gridUniqueID;

        public abstract Vector3 GetActiveGridOrigin();
        public abstract Vector3 GetActiveGridOrigin(int activeVerticalGridIndex);

        public abstract BuildableObjectSO GetActiveBuildableObjectSO();
        public abstract BuildableObjectSO.RandomPrefabs GetActiveBuildableObjectSORandomPrefab();
        public abstract BuildableObjectSO.RandomPrefabs GetActiveSecondaryBuildableObjectSORandomPrefab();

        public abstract GridObjectPlacementType GetActiveBuildableGridObjectSOPlacementType();
        public abstract EdgeObjectPlacementType GetActiveBuildableEdgeObjectSOPlacementType();
        public abstract CornerObjectPlacementType GetActiveBuildableCornerObjectSOPlacementType();
        public abstract FreeObjectPlacementType GetActiveBuildableFreeObjectSOPlacementType();

        public abstract Grid GetActiveGrid();
        public abstract Grid GetNearestGrid(Vector3 worldPosition);
        public abstract FourDirectionalRotation GetActiveBuildableGridObjectRotation();
        public abstract FourDirectionalRotation GetActiveBuildableEdgeObjectRotation();
        public abstract bool GetActiveBuildableEdgeObjectFlipped();
        public abstract FourDirectionalRotation GetActiveBuildableCornerObjectFourDirectionalRotation();
        public abstract EightDirectionalRotation GetActiveBuildableCornerObjectEightDirectionalRotation();
        public abstract float GetActiveBuildableCornerObjectFreeRotation();
        public abstract FourDirectionalRotation GetActiveBuildableFreeObjectFourDirectionalRotation();
        public abstract EightDirectionalRotation GetActiveBuildableFreeObjectEightDirectionalRotation();
        public abstract float GetActiveBuildableFreeObjectFreeRotation();
        public abstract float GetActiveBuildableFreeObjectSplineSpacing();

        public abstract bool GetUseBuildModeActivationInput();
        public abstract bool GetUseDestroyModeActivationInput();
        public abstract bool GetUseSelectModeActivationInput();
        public abstract bool GetUseMoveModeActivationInput();

        public abstract int GetActiveVerticalGridIndex();
        public abstract int GetVerticalGridIndexOf(Grid grid);
        public abstract Grid GetVerticalGridIndexOf(int verticalGridIndex);

        public abstract Vector3 GetActiveGridCellWorldPosition(Vector2Int cellPosition);
        public abstract Vector3 GetCellWorldPosition(Vector2Int cellPosition, int verticalGridIndex);

        public abstract Vector2Int GetActiveGridCellPosition(Vector3 worldPosition);
        public abstract Vector2Int GetCellPosition(Vector3 worldPosition, int verticalGridIndex);

        public abstract GridCellData GetActiveGridCellData(Vector2Int cellPosition);
        public abstract GridCellData GetCellData(Vector2Int cellPosition, int verticalGridIndex);

        public abstract bool GetIsObjectMoving();
        public abstract BuildableObject GetMovingBuildableObject();

        public abstract bool GetRuntimeObjectGridHeatMapModeActiveSelf();
        public abstract float GetActiveGridCustomModifierValue(Vector2Int cellPosition, GridModifierSO gridModifierSO);
        public abstract Dictionary<GridModifierSO, float> GetActiveGridAllCustomModifierValue(Vector2Int cellPosition);

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS - EXTERNAL                                            ///
        ///-------------------------------------------------------------------------------///

        public abstract void SetUpdateInEditor(bool isUpdateInEditor);

        public abstract void SetGridWidth(int gridWidth);
        public abstract void SetGridLength(int gridLength);
        public abstract void SetCellSize(float cellSize);
        public abstract void SetUpdateGridPositionRuntime(bool isUpdateGridPositionRuntime);
        public abstract void SetUpdateGridWidthAndLengthRuntime(bool isUpdateGridWidthAndLengthRuntime);
        public abstract void SetGridOriginType(GridOrigin gridOriginType);
        
        public abstract void SetSwitchVerticalGridWithInput(bool isSwitchVerticalGridWithInput);
        public abstract void SetAutoDetectVerticalGrid(bool isAutoDetectVerticalGrid);
        public abstract void SetVerticalGridAutoDetectionLayer(LayerMask verticalGridAutoDetectionLayer);

        public abstract void SetColliderSizeMultiplier(float colliderSizeMultiplier);
        public abstract void SetLockColliderAtBottomGrid(bool isLockColliderAtBottomGrid);

        public abstract void SetDisplayCanvasGrid(bool isDisplayCanvasGrid);
        public abstract void SetCanvasGridSettings(CanvasGridSettings canvasGridSettings);
        public abstract void SetDisplayObjectGrid(bool isDisplayObjectGrid);
        public abstract void SetObjectGridSettings(ObjectGridSettings objectGridSettings);
        public abstract void SetDisplayRuntimeTextGrid(bool isDisplayRuntimeTextGrid);
        public abstract void SetRuntimeTextGridSettings(RuntimeTextGridSettings runtimeTextGridSettings);
        public abstract void SetDisplayRuntimeProceduralGrid(bool isDisplayRuntimeProceduralGrid);
        public abstract void SetRuntimeProceduralGridSettings(RuntimeProceduralGridSettings runtimeProceduralGridSettings);

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS - INTERNAL                                            ///
        ///-------------------------------------------------------------------------------///
        
        public abstract void SetActiveGridMode(GridMode gridMode, bool isAToggle = false);
        public abstract void SetActiveCameraMode(CameraMode cameraMode);

        public abstract void SetGridWidthAndLength(int gridWidth, int gridLength, bool byPassUpdateGridWidthAndLengthRuntime = false);

        public abstract void SetActiveVerticalGrid(int verticalGridIndex);
        public abstract void AddVerticalGrid();
        public abstract void RemoveVerticalGrid();

        public abstract void SetActiveGridCellData(Vector2Int cellPosition, GridCellData gridCellData);
        public abstract void SetCellData(Vector2Int cellPosition, int verticalGridIndex, GridCellData gridCellData);

        public abstract void SetIsObjectMoving(bool isObjectMoving);
        public abstract void SetMovingBuildableObject(BuildableObject buildableObject);
        
        public abstract void SetActiveBuildableGridObjectRotation(FourDirectionalRotation fourDirectionalRotation);
        public abstract void SetActiveBuildableEdgeObjectRotation(FourDirectionalRotation fourDirectionalRotation);
        public abstract void SetActiveBuildableEdgeObjectFlipped(bool isFlipped);
        public abstract void SetActiveBuildableCornerObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation);
        public abstract void SetActiveBuildableCornerObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation);
        public abstract void SetActiveBuildableCornerObjectFreeRotation(float freeRotation);
        public abstract void SetActiveBuildableFreeObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation);
        public abstract void SetActiveBuildableFreeObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation);
        public abstract void SetActiveBuildableFreeObjectFreeRotation(float freeRotation);

        public abstract void AddBuildableObjectSOToTheList(BuildableObjectSO buildableObjectSO);
        public abstract void RemoveBuildableObjectSOFromTheList(BuildableObjectSO buildableObjectSO);
        public abstract void SetBuildableObjectSOLists(List<BuildableGridObjectSO> buildableGridObjectSOList, List<BuildableEdgeObjectSO> buildableEdgeObjectSOList, 
            List<BuildableCornerObjectSO> buildableCornerObjectSOList, List<BuildableFreeObjectSO> buildableFreeObjectSOList);
        public abstract void ClearBuildableObjectSOLists();

        public abstract void SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType gridObjectPlacementType = GridObjectPlacementType.SinglePlacement, 
            EdgeObjectPlacementType edgeObjectPlacementType = EdgeObjectPlacementType.SinglePlacement, CornerObjectPlacementType cornerObjectPlacementType = CornerObjectPlacementType.SinglePlacement, 
            FreeObjectPlacementType freeObjectPlacementType = FreeObjectPlacementType.SinglePlacement);
        public abstract void SetActiveBuildableFreeObjectSplineConnectionMode(bool isClosed);
        public abstract void SetActiveBuildableFreeObjectSplineConnectionModeSwap();
        public abstract void SetActiveBuildableObjectOnlySpawnAtEndPoints(bool onlySpawnAtEndPoints);
        public abstract void SetActiveBuildableObjectOnlySpawnAtEndPointsModeSwap();

        public abstract void SetActiveBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs randomPrefab);
        public abstract void SetActiveSecondaryBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs randomPrefab);
        
        public abstract void SetAllRuntimeObjectGridGeneratedTextureToDefault();
        public abstract void SetRuntimeObjectGridGeneratedTextureCellToDefault(Vector2Int cellPosition, bool affectAllVerticalGrids, Grid grid);
        public abstract void SetRuntimeObjectGridGeneratedTextureCellColor(Vector2Int cellPosition, Color cellColor, bool affectAllVerticalGrids, Grid grid);
        public abstract void SetRuntimeObjectGridHeatMapTexture(Texture2D generatedTexture, int veritcalGridIndex, Color overrideHDRColor);
        public abstract void SetRuntimeObjectGridHeatMapTextureCellColor(Vector2Int cellPosition, Color color);
        public abstract void SetRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid(bool onlyAffectFirstVerticalGrid);
        public abstract void SetActiveGridCustomModifierValue(Vector2Int cellPosition, GridModifierSO gridModifierSO, float customModifierValue);
    }
}