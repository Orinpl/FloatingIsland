using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Easy Grid Builder Pro XZ", 0)]
    public class EasyGridBuilderProXZ : EasyGridBuilderPro
    {
        #if UNITY_EDITOR
        private bool isEditorVisualHandlerComponentAdded = false;
        private const string EDITOR_GRID_VISUAL_HANDLER_XZ = nameof(EditorGridVisualHandlerXZ);
        #endif

        private GridManager gridManager;
        private GridDataHandler gridDataHandler;
        
        private GridXZ activeGrid;
        private List<GridXZ> gridList;
        private List<Vector3> gridOriginList;
        private BoxCollider gridCollider;
        private int previousGridWidth;
        private int previousGridLength;
        [SerializeField] private int activeVerticalGridIndex = 0;
        
        [SerializeField] private BuildableObjectSO activeBuildableObjectSO;
        private BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab;
        private BuildableObjectSO.RandomPrefabs activeSecondaryBuildableObjectSORandomPrefab;

        private GridObjectPlacementType activeBuildableGridObjectSOPlacementType;
        private EdgeObjectPlacementType activeBuildableEdgeObjectSOPlacementType;
        private CornerObjectPlacementType activeBuildableCornerObjectSOPlacementType;
        private FreeObjectPlacementType activeBuildableFreeObjectSOPlacementType;
        
        private FourDirectionalRotation activeBuildableGridObjectSORotation;
        
        private FourDirectionalRotation activeBuildableEdgeObjectSORotation;
        private bool activeBuildableEdgeObjectSOFlipped;

        private FourDirectionalRotation activeBuildableCornerObjectSOFourDirectionalRotation;
        private EightDirectionalRotation activeBuildableCornerObjectSOEightDirectionalRotation;
        private float activeBuildableCornerObjectSOFreeRotation;

        private FourDirectionalRotation activeBuildableFreeObjectSOFourDirectionalRotation;
        private EightDirectionalRotation activeBuildableFreeObjectSOEightDirectionalRotation;
        private float activeBuildableFreeObjectSOFreeRotation;

        private SplineContainer buildableFreeObjectSplineContainer;
        private Spline buildableFreeObjectSpline;
        private float buildableFreeObjectSplineObjectSpacing;

        private List<Transform> builtBuildableGridObjectList;
        private List<Transform> builtBuildableEdgeObjectList;
        private List<Transform> builtBuildableCornerObjectList;
        private List<Transform> builtBuildableFreeObjectList;

        private bool isObjectMoving;
        private BuildableObject movingBuildableObject;

        /// Grid Visuals Variables
        private bool isRuntimeCanvasGridInitialized  = false;
        private bool isRuntimeObjectGridInitialized  = false;
        private bool isRuntimeTextGridInitialized  = false;
        private bool isRuntimeProceduralGridInitialized  = false;
        private bool isRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid = true;
        private bool RuntimeObjectGridHeatMapModeEnabled = false;

        /// Input Action Variables
        private bool useBuildModeActivationKey;
        private bool useDestroyModeActivationKey;
        private bool useSelectModeActivationKey;
        private bool useMoveModeActivationKey;
        private bool isPlacementKeyHolding;
        private bool isCounterClockwiseRotationKeyHolding;
        private bool isClockwiseRotationKeyHolding;
        private int selectedBuildableSOIndex = 1;

        private Vector3 boxPlacementStartPosition;
        private Vector3 boxPlacementEndPosition;

        ///-------------------------------------------------------------------------------///
        /// GRID EDITOR FUNCTIONS                                                         ///
        ///-------------------------------------------------------------------------------///
        
        #if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (Application.isPlaying) return;
            base.OnValidate();
        }
        #endif

        #region Grid Editor Functions Start:
        #if UNITY_EDITOR
        protected override void UpdateEditorVisualHandlerComponent() 
        {
            if (updateInEditor)
            {
                if (!isEditorVisualHandlerComponentAdded)
                {
                    EditorApplication.update += DelayedAddEditorComponent;
                    isEditorVisualHandlerComponentAdded = true;
                }
            }
            else 
            {
                if (isEditorVisualHandlerComponentAdded)
                {
                    EditorApplication.update += DelayedRemoveEditorComponent;
                    isEditorVisualHandlerComponentAdded = false;
                }
            }
        }

        private void DelayedAddEditorComponent()
        {
            EditorApplication.update -= DelayedAddEditorComponent;
            if (this == null || transform == null) return;
            
            if (transform.Find(EDITOR_GRID_VISUAL_HANDLER_XZ) == null)
            {
                GameObject editorHandler = new GameObject(EDITOR_GRID_VISUAL_HANDLER_XZ);
                editorHandler.transform.SetParent(transform, false);
                editorHandler.AddComponent<EditorGridVisualHandlerXZ>();
            }
        }

        private void DelayedRemoveEditorComponent()
        {
            EditorApplication.update -= DelayedRemoveEditorComponent;
            
            Transform editorHandler = transform.Find(EDITOR_GRID_VISUAL_HANDLER_XZ);
            if (editorHandler != null)
            {
                DestroyImmediate(editorHandler.gameObject, true);
            }
        }

        protected override void UpdateSafetyChecks()
        {
            if (displayRuntimeTextGrid || displayRuntimeProceduralGrid)
            {   
                if (gridWidth * gridLength > 1000) 
                {
                    Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=orange><b>Warning: Runtime Text Grid or Runtime Procedural Grid Enabled with Exceeded Safety Threshold Cell Count!</b></color> " +
                            $"\nThe total cell count ({gridWidth * gridLength}) exceeds the safety threshold of 1000 cells. " +
                            $"Starting Playmode with a grid of this size may cause a significant lag spike due to the instantiation of many objects. " +
                            $"Consider reducing the grid size to avoid performance issues. " +
                            $"<color=orange><b>Note:</b></color> You can disable safety warnings by toggeling the <color=orange><b>Enable Editor Safety Messages</b></color> option in the Console Messages.");
                }
            }
        }
        #endif
        #endregion Grid Editor Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID INITIALIZE FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///
        
        protected override void Start()
        {
            gridManager = GridManager.Instance;
            gridManager.OnActiveCameraModeChanged += OnActiveCameraModeChanged;
            
            gridDataHandler = GetComponent<GridDataHandler>();
            gridDataHandler.InitializeGridDataHandler(verticalGridsCount);

            activeBuildableObjectSO = null;

            builtBuildableGridObjectList = new List<Transform>();
            builtBuildableEdgeObjectList = new List<Transform>();
            builtBuildableCornerObjectList = new List<Transform>();
            builtBuildableFreeObjectList = new List<Transform>();
            previousGridWidth = gridWidth;
            previousGridLength = gridLength;

            activeCameraMode = gridManager.GetActiveCameraMode();
            UpdateActiveCameraModeProperties();

            base.Start();
        }

        private void OnDestroy()
        {
            gridManager.OnActiveCameraModeChanged -= OnActiveCameraModeChanged;
        }

        private void OnActiveCameraModeChanged(CameraMode activeCameraMode)
        {
            SetActiveCameraMode(activeCameraMode);
        }

        #region Grid Initialization Functions Start:
        protected override void RunVisualGridsSafetyChecks()
        {
            if (!gridManager.GetIsEnableEditorSafetyMessages()) return;

            int safetyThreshold = 10000;
            int totalCells = gridWidth * gridLength;

            if (totalCells <= safetyThreshold) return;
            if (displayRuntimeProceduralGrid && runtimeProceduralGridSettings.delayBetweenSpawns <= 0)
            {
                displayRuntimeProceduralGrid = false;
                Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Warning: Runtime Procedural Grid Exceeded Safety Threshold! Runtime Procedural Grid Disabled!</b></color> " +
                            $"\nThe total cell count ({totalCells}) exceeds the safety threshold of {safetyThreshold} cells and Delay Between Spawns is disabled. " +
                            $"Starting Playmode with a grid of this size may cause a significant lag spike due to the instantiation of many objects at once. " +
                            $"Consider adding a Delay Between Spawns, Disabling Runtime Procedural Grid, or reducing the grid size to avoid performance issues.");
            }
            else if (displayRuntimeTextGrid)
            {
                displayRuntimeTextGrid = false;
                Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Warning: Runtime Text Grid Exceeded Safety Threshold! Runtime Text Grid Disabled!</b></color> " +
                            $"\nThe total cell count ({totalCells}) exceeds the safety threshold of {safetyThreshold} cells. " +
                            $"Starting Playmode with a grid of this size may cause a significant lag spike due to the instantiation of many objects at once. " +
                            $"Consider Disabling Runtime Text Grid, or reducing the grid size to avoid performance issues.");
            }
        }

        protected override void SetupVerticalGrids()
        {
            gridOriginList = new List<Vector3>();
            gridList = new List<GridXZ>();

            for (int i = 0; i < verticalGridsCount; i++)
            {
                Vector3 gridOrigin = CalculateGridOrigin(i);
                gridOriginList.Add(gridOrigin);
                gridList.Add(new GridXZ(gridWidth, gridLength, cellSize, gridOrigin, transform, i, gridDataHandler));
            }
            activeGrid = gridList[0];
            activeGridOrigin = gridOriginList[0];

            RaiseGridSystemCreationEventsAndMessages(this, gridList.Count);
        }

        private Vector3 CalculateGridOrigin(int gridIndex)
        {
            float offsetX = default;
            float offsetZ = default;
                
            if (gridOriginType == GridOrigin.Center)
            {
                offsetX = (cellSize * gridWidth / 2) * -1;
                offsetZ = (cellSize * gridLength / 2) * -1;
            }

            return transform.position + new Vector3(offsetX, verticalGridHeight * gridIndex, offsetZ);
        }
        
        protected override void SetupGridCollider()
        {
            if (TryGetComponent<BoxCollider>(out BoxCollider gridCollider))
            {
                this.gridCollider = gridCollider;
            }
            else
            {
                this.gridCollider = gameObject.AddComponent<BoxCollider>();
            }

            float offsetX = (cellSize * gridWidth / 2) + gridOriginList[0].x;
            float offsetZ = (cellSize * gridLength / 2) + gridOriginList[0].z;  
            
            this.gridCollider.center = new Vector3(offsetX - transform.position.x, gridOriginList[0].y - transform.position.y, offsetZ - transform.position.z);
            this.gridCollider.isTrigger = true;
            this.gridCollider.size = new Vector3((cellSize * gridWidth) * colliderSizeMultiplier, 0, (cellSize * gridLength) * colliderSizeMultiplier);
        }

        protected override void SetupGridVisuals()
        {
            SetupRuntimeCanvasGrid();
            SetupRuntimeObjectGrid();
            SetupRuntimeTextGrid();
            SetupRuntimeProceduralGrid();
        }
        #endregion Grid Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID UPDATE FUNCTIONS                                                         ///
        ///-------------------------------------------------------------------------------///
        
        protected override void Update()
        {
            base.Update();
        }
        
        #region Grid Update Functions Start:
        protected override void UpdateGridOrigin()
        {
            if (updateGridPositionRuntime)
            {
                float offsetX = default;
                float offsetZ = default;
                
                if (gridOriginType == GridOrigin.Center)
                {
                    offsetX = (cellSize * gridWidth / 2) * -1;
                    offsetZ = (cellSize * gridLength / 2) * -1;
                }

                for (int i = 0; i < verticalGridsCount; i++)
                {
                    gridOriginList[i] = new Vector3(transform.position.x + offsetX, transform.position.y + verticalGridHeight * i, transform.position.z + offsetZ);
                    gridList[i].SetOriginPosition(gridOriginList[i]);
                }
            }
            activeGridOrigin = gridOriginList[activeVerticalGridIndex];
        }
        
        protected override void UpdateGriCollider()
        {
            float offsetX;
            float offsetZ;

            if (lockColliderAtBottomGrid)
            {
                offsetX = (cellSize * gridWidth / 2) + gridOriginList[0].x;
                offsetZ = (cellSize * gridLength / 2) + gridOriginList[0].z;  
                gridCollider.center = new Vector3(offsetX - transform.position.x, gridOriginList[0].y - transform.position.y, offsetZ - transform.position.z);
            }
            else
            {
                offsetX = (cellSize * gridWidth / 2) + gridOriginList[activeVerticalGridIndex].x;
                offsetZ = (cellSize * gridLength / 2) + gridOriginList[activeVerticalGridIndex].z;  
                gridCollider.center = new Vector3(offsetX - transform.position.x, gridOriginList[activeVerticalGridIndex].y - transform.position.y, offsetZ - transform.position.z);
            }

            gridCollider.size = new Vector3((cellSize * gridWidth) * colliderSizeMultiplier, 0, (cellSize * gridLength) * colliderSizeMultiplier);
        }

        protected override void UpdateGridOriginType()
        {
            if (updateGridWidthAndLengthRuntime && gridOriginType != GridOrigin.Default) gridOriginType = GridOrigin.Default;
        }
        
        protected override void UpdateGridWidthAndLength()
        {
            if (updateGridWidthAndLengthRuntime && previousGridWidth != gridWidth || previousGridLength != gridLength)
            {
                previousGridWidth = gridWidth;
                previousGridLength = gridLength;

                foreach (GridXZ gridXZ in gridList)
                {
                    gridXZ.SetGridSize(gridWidth, gridLength);
                }
            }
        }

        protected override void UpdateGridVisuals()
        {
            UpdateRuntimeCanvasGrid();
            UpdateRuntimeObjectGrid();
            UpdateRuntimeTextGrid();
            UpdateRuntimeProceduralGrid();
        }
        #endregion Grid Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// RUNTIME GRID VISUAL FUNCTIONS                                                 ///
        ///-------------------------------------------------------------------------------///
        
        #region Runtime Canvas Grid Functions Start:
        private void SetupRuntimeCanvasGrid()
        {
            if (displayCanvasGrid && !isRuntimeCanvasGridInitialized) 
            {
                InitializeRuntimeCanvasGrid();
                isRuntimeCanvasGridInitialized = true;
            }
            else if (displayCanvasGrid)
            {
                ClearRuntimeCanvasGrid();
                SetupRuntimeCanvasGrid();
            }
            else ClearRuntimeCanvasGrid();
        }

        private void InitializeRuntimeCanvasGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (canvasGridSettings.canvasGridPrefab != null && !gridList[i].TryGetRuntimeCanvasGrid(out _))
                {
                    gridList[i].InitializeRuntimeCanvasGrid(canvasGridSettings, i, verticalGridHeight, activeGridMode, activeGrid, gridOriginType);
                }
            }
        }

        private void UpdateRuntimeCanvasGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (!gridList[i].TryGetRuntimeCanvasGrid(out _)) return;
                
                gridList[i].UpdateRuntimeCanvasGridPosition(verticalGridHeight, i, gridOriginType);
                gridList[i].UpdateRuntimeCanvasGrid(canvasGridSettings, i, activeGridMode, activeGrid);
            }
        }

        private void ClearRuntimeCanvasGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (gridList[i].TryGetRuntimeCanvasGrid(out _)) gridList[i].ClearRuntimeCanvasGrid();
            }
            isRuntimeCanvasGridInitialized = false;
        }
        #endregion Runtime Canvas Grid Functions End:

        #region Runtime Object Grid Functions Start:
        private void SetupRuntimeObjectGrid()
        {
            if (displayObjectGrid && !isRuntimeObjectGridInitialized) 
            {
                InitializeRuntimeObjectGrid();
                isRuntimeObjectGridInitialized = true;
            }
            else if (displayObjectGrid)
            {
                ClearRuntimeObjectGrid();
                SetupRuntimeObjectGrid();
            }
            else ClearRuntimeObjectGrid();
        }

        private void InitializeRuntimeObjectGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (objectGridSettings.objectGridPrefab != null && !gridList[i].TryGetRuntimeObjectGrid(out _))
                {
                    gridList[i].InitializeRuntimeObjectGrid(objectGridSettings, i, verticalGridHeight, activeGridMode, activeGrid, activeBuildableObjectSO, RuntimeObjectGridHeatMapModeEnabled, gridOriginType);
                }
            }
        }

        private void UpdateRuntimeObjectGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (!gridList[i].TryGetRuntimeObjectGrid(out _)) return;

                gridList[i].UpdateRuntimeObjectGridPosition(verticalGridHeight, i, gridOriginType);
                gridList[i].UpdateRuntimeObjectGrid(objectGridSettings, i, activeGridMode, activeGrid, activeBuildableObjectSO, RuntimeObjectGridHeatMapModeEnabled);
            }
        }

        private void ClearRuntimeObjectGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (gridList[i].TryGetRuntimeObjectGrid(out _)) gridList[i].ClearRuntimeObjectGrid();
            }
            isRuntimeObjectGridInitialized = false;
        }
        #endregion Runtime Object Grid Functions End:

        #region Runtime Text Grid Functions Start:
        private void SetupRuntimeTextGrid()
        {
            if (displayRuntimeTextGrid && !isRuntimeTextGridInitialized) 
            {
                InitializeRuntimeTextGrid();
                isRuntimeTextGridInitialized = true;
            }
            else if (displayRuntimeTextGrid)
            {
                ClearRuntimeTextGrid();
                SetupRuntimeTextGrid();
            }
            else ClearRuntimeTextGrid();
        }

        private void InitializeRuntimeTextGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (runtimeTextGridSettings.textPrefab != null && !gridList[i].TryGetRuntimeTextGrid(out _))
                {
                    gridList[i].InitializeRuntimeTextGrid(runtimeTextGridSettings, i, verticalGridHeight, activeGridMode, activeGrid);
                }
            }
        }

        private void UpdateRuntimeTextGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (gridList[i].TryGetRuntimeTextGrid(out _)) gridList[i].UpdateRuntimeTextGrid(runtimeTextGridSettings, i, activeGridMode, activeGrid);
            }
        }

        private void ClearRuntimeTextGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (gridList[i].TryGetRuntimeTextGrid(out _)) gridList[i].ClearRuntimeTextGrid();
            }
            isRuntimeObjectGridInitialized = false;
        }
        #endregion Runtime Text Grid Functions End:

        #region Runtime Procedural Grid Functions Start:
        private void SetupRuntimeProceduralGrid()
        {
            if (displayRuntimeProceduralGrid && !isRuntimeProceduralGridInitialized) 
            {
                if (runtimeProceduralGridSettings.delayBetweenSpawns == 0) InitializeRuntimeProceduralGrid();
                else StartCoroutine(InitializeRuntimeProceduralGridWithDelay(runtimeProceduralGridSettings.delayBetweenSpawns));
                isRuntimeProceduralGridInitialized = true;
            }
            else if (displayRuntimeProceduralGrid)
            {
                ClearRuntimeProceduralGrid();
                SetupRuntimeProceduralGrid();
            }
            else ClearRuntimeProceduralGrid();
        }

        private void InitializeRuntimeProceduralGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (runtimeProceduralGridSettings.nodeObjects.Count != 0 && !gridList[i].TryGetRuntimeProceduralGrid(out _))
                {
                    gridList[i].InitializeRuntimeProceduralGrid(runtimeProceduralGridSettings, i, verticalGridHeight, activeGridMode, activeGrid);
                }
            }
        }

        private IEnumerator InitializeRuntimeProceduralGridWithDelay(float delayBetweenSpawns)
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (runtimeProceduralGridSettings.nodeObjects.Count != 0 && !gridList[i].TryGetRuntimeProceduralGrid(out _))
                {
                    gridList[i].ClearRuntimeProceduralGrid();

                    if (runtimeProceduralGridSettings.onlySpawnFirstVerticalGrid && i != 0) break;

                    UnityEngine.Random.InitState(runtimeProceduralGridSettings.seed);
                    gridList[i].SetRuntimeProceduralGrid(new GameObject("Procedural Grid").transform);
                    if (gridList[i].TryGetRuntimeProceduralGrid(out Transform runtimeProceduralGrid)) runtimeProceduralGrid.parent = this.transform;
                    Vector2 randomOffset = new Vector2(UnityEngine.Random.value, UnityEngine.Random.value) * 100f;

                    for (int x = 0; x < gridWidth; x++)
                    {
                        for (int z = 0; z < gridLength; z++)
                        {
                            gridList[i].InstantiateNode(runtimeProceduralGridSettings, x, z, randomOffset);
                            if (!runtimeProceduralGridSettings.spawnRowByRow) yield return new WaitForSeconds(delayBetweenSpawns);
                        }
                        if (runtimeProceduralGridSettings.spawnRowByRow) yield return new WaitForSeconds(delayBetweenSpawns);
                    }

                    UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
                }
            }
        }

        private void UpdateRuntimeProceduralGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (gridList[i].TryGetRuntimeProceduralGrid(out _)) gridList[i].UpdateRuntimeProceduralGrid(runtimeProceduralGridSettings, i, activeGridMode, activeGrid);
            }
        }

        private void ClearRuntimeProceduralGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (gridList[i].TryGetRuntimeProceduralGrid(out _)) gridList[i].ClearRuntimeProceduralGrid();
            }
            isRuntimeProceduralGridInitialized = false;
        }
        #endregion Runtime Procedural Grid Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT HANDLING FUNCTIONS                                                      ///
        ///-------------------------------------------------------------------------------///

        #region Handle Input Grid Mode Functions Start:
        public override void SetInputGridModeVariables(bool useBuildModeActivationKey, bool useDestroyModeActivationKey, bool useSelectModeActivationKey, bool useMoveModeActivationKey)
        {
            this.useBuildModeActivationKey = useBuildModeActivationKey;
            this.useDestroyModeActivationKey = useDestroyModeActivationKey;
            this.useSelectModeActivationKey = useSelectModeActivationKey;
            this.useMoveModeActivationKey = useMoveModeActivationKey;
        }

        public override void SetInputActiveGridMode(GridMode gridMode)
        {
            if (IsActivationKeyUsed(gridMode))
            {
                if (activeGridMode != gridMode)
                {
                    ResetBuildables();
                    activeGridMode = gridMode;
                    RaiseActiveGridModeChangeEventsAndMessages(this, gridMode);
                }
                else ResetBuildablesAndGridMode();
            }
        }

        private bool IsActivationKeyUsed(GridMode gridMode)
        {
            switch (gridMode)
            {
                case GridMode.BuildMode: return useBuildModeActivationKey;
                case GridMode.DestroyMode: return useDestroyModeActivationKey;
                case GridMode.SelectMode: return useSelectModeActivationKey;
                case GridMode.MoveMode: return useMoveModeActivationKey;
                default: return false;
            }
        }

        public override void SetInputCycleThroughGridModes()
        {
            int currentIndex = (int)activeGridMode;
            int nextIndex = (currentIndex + 1) % Enum.GetNames(typeof(GridMode)).Length;
            
            CycleThroughGridModes(nextIndex);
        }

        public override void SetInputCycleThroughGridModes(Vector2 inputDirection)
        {
            int direction = inputDirection.y > 0 ? 1 : -1;
            int currentIndex = (int)activeGridMode;
            int nextIndex = (currentIndex + direction + Enum.GetNames(typeof(GridMode)).Length) % Enum.GetNames(typeof(GridMode)).Length;

            CycleThroughGridModes(nextIndex);
        }

        private void CycleThroughGridModes(int nextIndex)
        {
            if ((GridMode)nextIndex != GridMode.None)
            {
                ResetBuildables();
                activeGridMode = (GridMode)nextIndex;
                RaiseActiveGridModeChangeEventsAndMessages(this, activeGridMode);
            }
            else ResetBuildablesAndGridMode();
        }

        public override void SetInputGridModeReset()
        {
            ResetBuildablesAndGridMode();
        }

        public override void SetInputPlacementReset(bool blockedByFreeObjectSplinePlacementComplete = false)
        {
            if (blockedByFreeObjectSplinePlacementComplete) 
            {
                if (activeBuildableObjectSO is BuildableFreeObjectSO && activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement) return;
                ResetBoxPlacementTypes();
            }
            else ResetBoxPlacementTypes();
        }
        #endregion Handle Input Grid Mode Functions End:

        #region Handle Input Vertical Grid Functions Start:
        public override void SetInputVerticalGrid(int newGridIndex)
        {
            if (!switchVerticalGridWithInput) return;
            if (newGridIndex < 0 || newGridIndex >= gridList.Count || activeGrid == gridList[newGridIndex]) return;

            activeGrid = gridList[newGridIndex];
            activeGridOrigin = gridOriginList[newGridIndex];
            activeVerticalGridIndex = newGridIndex;

            RaiseActiveVerticalGridChangeEventsAndMessages(this, activeVerticalGridIndex);
        }

        public override void SetInputMoveUpVerticalGrid()
        {
            SetInputVerticalGrid((gridList.IndexOf(activeGrid) + 1) % gridList.Count);
        }

        public override void SetInputMoveDownVerticalGrid()
        {
            SetInputVerticalGrid((gridList.IndexOf(activeGrid) - 1) % gridList.Count);
        }

        public override void SetInputCycleThroughVerticalGrids(Vector2 inputDirection)
        {
            int direction = inputDirection.y > 0 ? 1 : -1;
            int newGridIndex = Mathf.Clamp(gridList.IndexOf(activeGrid) + direction, 0, gridList.Count - 1);
            SetInputVerticalGrid(newGridIndex);
        }

        protected override void UpdateAutoDetectAndSetVerticalGrid()
        {
            if (!autoDetectVerticalGrid || autoDetectionLayerMask == 0) return;

            Vector3 surfaceWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(autoDetectionLayerMask, customSurfaceLayerMask | gridManager.GetGridSystemLayerMask(),
                Vector3.down * 9999, out _, out Vector3 firstCollisionWorldPosition);

            float verticalHeightOffset = firstCollisionWorldPosition.y - surfaceWorldPosition.y;
            float rawIndex = verticalHeightOffset / verticalGridHeight;
            int closestGridIndex = Mathf.RoundToInt(rawIndex);

            SetActiveVerticalGrid(closestGridIndex);
        }
        #endregion Handle Input Vertical Grid Functions End:
        
        #region Handle Input HeatMap Functions Start:
        public override void SetInputToggleHeatMapMode()
        {
            if (!TryGetComponent<GridHeatMap>(out _)) return;
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (isRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid && i != 0) return;
                if (gridList[i].TryGetRuntimeObjectGrid(out _)) 
                {
                    gridList[i].SetRuntimeObjectGridHeatMapActiveSelfToggle(out bool toggleMode);
                    RuntimeObjectGridHeatMapModeEnabled = toggleMode;
                }
            }
        }

        public override void SetInputEnableHeatMapMode()
        {
            if (!TryGetComponent<GridHeatMap>(out _)) return;
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (isRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid && i != 0) return;
                if (gridList[i].TryGetRuntimeObjectGrid(out _)) 
                {
                    gridList[i].SetRuntimeObjectGridHeatMapActiveSelf(true);
                    RuntimeObjectGridHeatMapModeEnabled = true;
                }
            }
        }

        public override void SetInputDisableHeatMapMode()
        {
            if (!TryGetComponent<GridHeatMap>(out _)) return;
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (isRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid && i != 0) return;
                if (gridList[i].TryGetRuntimeObjectGrid(out _)) 
                {
                    gridList[i].SetRuntimeObjectGridHeatMapActiveSelf(false);
                    RuntimeObjectGridHeatMapModeEnabled = true;
                }
            }
        }
        #endregion Handle Input HeatMap Functions End:

        #region Handle Input Buildable Object Selection Functions Start:
        public override void SetInputActiveBuildableObjectSO(BuildableObjectSO buildableObjectSO, BuildableObjectSO.RandomPrefabs randomPrefab = null, bool onlySetBuildableExistInBuildablesList = false)
        {
            if (useBuildModeActivationKey && activeGridMode != GridMode.BuildMode) return;
            if (isObjectMoving) return;

            if (onlySetBuildableExistInBuildablesList)
            {
                switch (buildableObjectSO)
                {
                    case BuildableGridObjectSO gridObjectSO: if (!buildableGridObjectSOList.Contains(gridObjectSO)) return; break;
                    case BuildableEdgeObjectSO edgeObjectSO: if (!buildableEdgeObjectSOList.Contains(edgeObjectSO)) return; break;
                    case BuildableCornerObjectSO cornerObjectSO: if (!buildableCornerObjectSOList.Contains(cornerObjectSO)) return; break;
                    case BuildableFreeObjectSO freeObjectSO: if (!buildableFreeObjectSOList.Contains(freeObjectSO)) return; break;
                }
            }

            SetActiveGridMode(GridMode.BuildMode);

            if (activeBuildableObjectSO is BuildableGridObjectSO && IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelGridObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                CancelEdgeObjectBoxPlacement();
                if (buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO) CancelCornerObjectBoxPlacement();
            }
            if (activeBuildableObjectSO is BuildableCornerObjectSO && IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelCornerObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableFreeObjectSO && IsActiveBuildableFreeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelFreeObjectSplinePlacement();

            activeBuildableObjectSO = buildableObjectSO;
            if (randomPrefab == null) UpdateActiveBuildableObjectSORandomPrefab(activeBuildableObjectSO);
            else SetActiveBuildableObjectSORandomPrefab(randomPrefab);
            RaiseActiveBuildableSOChangeEventsAndMessages(this, activeBuildableObjectSO);
        }

        public override void SetInputCycleThroughBuildableObjectsSOList(Vector2 inputDirection)
        {
            if (useBuildModeActivationKey && activeGridMode != GridMode.BuildMode) return;
            if (isObjectMoving) return;

            SetActiveGridMode(GridMode.BuildMode);

            int totalBuildableCount = buildableGridObjectSOList.Count + buildableEdgeObjectSOList.Count + buildableCornerObjectSOList.Count + buildableFreeObjectSOList.Count;
            if (totalBuildableCount == 0) return;

            if (activeBuildableObjectSO is BuildableGridObjectSO && IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelGridObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
            {
                CancelEdgeObjectBoxPlacement();
                if (buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO) CancelCornerObjectBoxPlacement();
            }
            if (activeBuildableObjectSO is BuildableCornerObjectSO && IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelCornerObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableFreeObjectSO && IsActiveBuildableFreeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelFreeObjectSplinePlacement();

            UpdateSelectedBuildableIndex(inputDirection, totalBuildableCount);
            UpdateActiveBuildableObjectSO();
            UpdateActiveBuildableObjectSORandomPrefab(activeBuildableObjectSO);
            RaiseActiveBuildableSOChangeEventsAndMessages(this, activeBuildableObjectSO);
        }

        public override void SetInputCycleThroughBuildableObjectsSOListUp()
        {
            SetInputCycleThroughBuildableObjectsSOList(new Vector2(0, 1));
        }

        public override void SetInputCycleThroughBuildableObjectsSOListDown()
        {
            SetInputCycleThroughBuildableObjectsSOList(new Vector2(0, -1));
        }

        private void UpdateSelectedBuildableIndex(Vector2 inputDirection, int totalBuildableCount)
        {
            if (inputDirection.y != 0f)
            {
                selectedBuildableSOIndex += (int)Mathf.Sign(inputDirection.y);
                selectedBuildableSOIndex = Mathf.Clamp(selectedBuildableSOIndex, 1, totalBuildableCount);
            }
        }

        private void UpdateActiveBuildableObjectSO()
        {
            int buildableGridObjectSOCount = buildableGridObjectSOList.Count;
            int buildableGridAndEdgeObjectSOCount = buildableGridObjectSOCount + buildableEdgeObjectSOList.Count;
            int buildableGridAndEdgeAndCornerObjectSOCount = buildableGridObjectSOCount + buildableEdgeObjectSOList.Count + buildableCornerObjectSOList.Count;

            if (selectedBuildableSOIndex <= buildableGridObjectSOCount)
            {
                activeBuildableObjectSO =  buildableGridObjectSOList[selectedBuildableSOIndex - 1];
            }
            else if (selectedBuildableSOIndex <= buildableGridAndEdgeObjectSOCount)
            {
                activeBuildableObjectSO = buildableEdgeObjectSOList[selectedBuildableSOIndex - buildableGridObjectSOCount - 1];
            }
            else if (selectedBuildableSOIndex <= buildableGridAndEdgeAndCornerObjectSOCount)
            {
                activeBuildableObjectSO = buildableCornerObjectSOList[selectedBuildableSOIndex - buildableGridAndEdgeObjectSOCount - 1];
            }
            else activeBuildableObjectSO = buildableFreeObjectSOList[selectedBuildableSOIndex - buildableGridAndEdgeAndCornerObjectSOCount - 1];
        }

        private void UpdateActiveBuildableObjectSORandomPrefab(BuildableObjectSO buildableObjectSO)
        {
            float totalProbability;
            float randomPoint;

            totalProbability = CalculateTotalProbability(buildableObjectSO);
            randomPoint = UnityEngine.Random.Range(0f, totalProbability);

            activeBuildableObjectSORandomPrefab = SelectPrefabByProbability(buildableObjectSO, randomPoint);

            if (buildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO != null)
            {
                totalProbability = CalculateTotalProbability(buildableEdgeObjectSO.buildableCornerObjectSO);
                randomPoint = UnityEngine.Random.Range(0f, totalProbability);

                activeSecondaryBuildableObjectSORandomPrefab = SelectPrefabByProbability(buildableEdgeObjectSO.buildableCornerObjectSO, randomPoint);
            }
        }

        private BuildableObjectSO.RandomPrefabs GetUpdatedActiveBuildableObjectSORandomPrefab(BuildableObjectSO buildableObjectSO, out BuildableObjectSO.RandomPrefabs secondaryRandomPrefabs)
        {
            BuildableObjectSO.RandomPrefabs randomPrefabs;
            secondaryRandomPrefabs = null;
            float totalProbability;
            float randomPoint;

            totalProbability = CalculateTotalProbability(buildableObjectSO);
            randomPoint = UnityEngine.Random.Range(0f, totalProbability);

            randomPrefabs = SelectPrefabByProbability(buildableObjectSO, randomPoint);

            if (buildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO != null)
            {
                totalProbability = CalculateTotalProbability(buildableEdgeObjectSO.buildableCornerObjectSO);
                randomPoint = UnityEngine.Random.Range(0f, totalProbability);

                secondaryRandomPrefabs = SelectPrefabByProbability(buildableEdgeObjectSO.buildableCornerObjectSO, randomPoint);
            }
            return randomPrefabs;
        }

        private float CalculateTotalProbability(BuildableObjectSO buildableObjectSO)
        {
            float totalProbability = 0f;
            foreach (BuildableObjectSO.RandomPrefabs randomPrefab in buildableObjectSO.randomPrefabs)
            {
                totalProbability += randomPrefab.probability;
            }
            return totalProbability;
        }

        private BuildableObjectSO.RandomPrefabs SelectPrefabByProbability(BuildableObjectSO buildableObjectSO, float randomPoint)
        {
            float currentProbability = 0f;
            foreach (BuildableObjectSO.RandomPrefabs randomPrefab in buildableObjectSO.randomPrefabs)
            {
                currentProbability += randomPrefab.probability;
                if (randomPoint <= currentProbability) return randomPrefab;
            }
            return null;
        }

        public override void SetInputCycleThroughActiveBuildableObjectSOPlacementType(Vector2 inputDirection)
        {
            if (activeBuildableObjectSO == null || activeGridMode != GridMode.BuildMode) return;
            if (isObjectMoving) return;

            int totalPlacementTypesCount = default;
            int selectedPlacementTypeIndex = default;

            switch (activeBuildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO:
                    totalPlacementTypesCount = Enum.GetValues(typeof(GridObjectPlacementType)).Length;
                    selectedPlacementTypeIndex = (int)buildableGridObjectSO.placementType;

                    if (IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) FinalizeGridObjectBoxPlacement();
                    UpdateSelectedPlacementTypeIndex(inputDirection, totalPlacementTypesCount, ref selectedPlacementTypeIndex);
                    SetActiveBuildableObjectSOPlacementType(gridObjectPlacementType: (GridObjectPlacementType)selectedPlacementTypeIndex);
                break;
                case BuildableEdgeObjectSO buildableEdgeObjectSO:
                    totalPlacementTypesCount = Enum.GetValues(typeof(EdgeObjectPlacementType)).Length;
                    selectedPlacementTypeIndex = (int)buildableEdgeObjectSO.placementType;

                    if (IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) FinalizeEdgeObjectBoxPlacement();
                    UpdateSelectedPlacementTypeIndex(inputDirection, totalPlacementTypesCount, ref selectedPlacementTypeIndex);
                    SetActiveBuildableObjectSOPlacementType(edgeObjectPlacementType: (EdgeObjectPlacementType)selectedPlacementTypeIndex);
                break;
                case BuildableCornerObjectSO buildableCornerObjectSO:
                    totalPlacementTypesCount = Enum.GetValues(typeof(CornerObjectPlacementType)).Length;
                    selectedPlacementTypeIndex = (int)buildableCornerObjectSO.placementType;

                    if (IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) FinalizeCornerObjectBoxPlacement();
                    UpdateSelectedPlacementTypeIndex(inputDirection, totalPlacementTypesCount, ref selectedPlacementTypeIndex);
                    SetActiveBuildableObjectSOPlacementType(cornerObjectPlacementType: (CornerObjectPlacementType)selectedPlacementTypeIndex); 
                break;
                case BuildableFreeObjectSO buildableFreeObjectSO:
                    totalPlacementTypesCount = Enum.GetValues(typeof(FreeObjectPlacementType)).Length;
                    selectedPlacementTypeIndex = (int)buildableFreeObjectSO.placementType;

                    if (IsActiveBuildableFreeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelFreeObjectSplinePlacement();
                    UpdateSelectedPlacementTypeIndex(inputDirection, totalPlacementTypesCount, ref selectedPlacementTypeIndex);
                    SetActiveBuildableObjectSOPlacementType(freeObjectPlacementType: (FreeObjectPlacementType)selectedPlacementTypeIndex);
                break;
            }
        }

        public override void SetInputCycleThroughActiveBuildableObjectSOPlacementTypeUp()
        {
            SetInputCycleThroughActiveBuildableObjectSOPlacementType(new Vector2(0, 1));
        }

        public override void SetInputCycleThroughActiveBuildableObjectSOPlacementTypeDown()
        {
            SetInputCycleThroughActiveBuildableObjectSOPlacementType(new Vector2(0, -1));
        }

        private void UpdateSelectedPlacementTypeIndex(Vector2 inputDirection, int totalPlacementTypesCount, ref int selectedPlacementTypeIndex)
        {
            if (inputDirection.y != 0f) selectedPlacementTypeIndex = (selectedPlacementTypeIndex + (int)Mathf.Sign(inputDirection.y) + totalPlacementTypesCount) % totalPlacementTypesCount;
        }

        private void CheckPrefabConditions()
        {
            
        }
        #endregion Handle Input Buildable Object Selection Functions End:

        #region Handle Input Buildable Object Rotation Functions Start:
        public override void SetInputBuildableObjectRotationScroll(Vector2 inputDirection)
        {
            if (inputDirection.y > 0) SetInputBuildableObjectClockwiseRotation();
            else if (inputDirection.y < 0) SetInputBuildableObjectCounterClockwiseRotation();
        }

        public override void SetInputBuildableObjectClockwiseRotation(bool invokeByUI = false)
        {
            if (!invokeByUI) isClockwiseRotationKeyHolding = true;
            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO)
            {
                activeBuildableGridObjectSORotation = buildableGridObjectSO.GetNextDirectionClockwise(activeBuildableGridObjectSORotation);
            }
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO)
            {
                activeBuildableEdgeObjectSORotation = buildableEdgeObjectSO.GetNextDirectionClockwise(activeBuildableEdgeObjectSORotation);
            }
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO)
            {
                if (buildableCornerObjectSO.rotationType == CornerObjectRotationType.FourDirectionalRotation)
                {
                    activeBuildableCornerObjectSOFourDirectionalRotation = buildableCornerObjectSO.GetNextFourDirectionalDirectionClockwise(activeBuildableCornerObjectSOFourDirectionalRotation);
                }
                else if (buildableCornerObjectSO.rotationType == CornerObjectRotationType.EightDirectionalRotation)
                {
                    activeBuildableCornerObjectSOEightDirectionalRotation = buildableCornerObjectSO.GetNextEightDirectionalDirectionClockwise(activeBuildableCornerObjectSOEightDirectionalRotation);
                }
            }
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
            {
                if (buildableFreeObjectSO.rotationType == FreeObjectRotationType.FourDirectionalRotation)
                {
                    activeBuildableFreeObjectSOFourDirectionalRotation = buildableFreeObjectSO.GetNextFourDirectionalDirectionClockwise(activeBuildableFreeObjectSOFourDirectionalRotation);
                }
                else if (buildableFreeObjectSO.rotationType == FreeObjectRotationType.EightDirectionalRotation)
                {
                    activeBuildableFreeObjectSOEightDirectionalRotation = buildableFreeObjectSO.GetNextEightDirectionalDirectionClockwise(activeBuildableFreeObjectSOEightDirectionalRotation);
                }
            }
        }

        public override void SetInputBuildableObjectCounterClockwiseRotation(bool invokeByUI = false)
        {
            if (!invokeByUI) isCounterClockwiseRotationKeyHolding = true;
            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO)
            {
                activeBuildableGridObjectSORotation = buildableGridObjectSO.GetNextDirectionCounterClockwise(activeBuildableGridObjectSORotation);
            }
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO)
            {
                activeBuildableEdgeObjectSORotation = buildableEdgeObjectSO.GetNextDirectionCounterClockwise(activeBuildableEdgeObjectSORotation);
            }
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO)
            {
                if (buildableCornerObjectSO.rotationType == CornerObjectRotationType.FourDirectionalRotation)
                {
                    activeBuildableCornerObjectSOFourDirectionalRotation = buildableCornerObjectSO.GetNextFourDirectionalDirectionCounterClockwise(activeBuildableCornerObjectSOFourDirectionalRotation);
                }
                else if (buildableCornerObjectSO.rotationType == CornerObjectRotationType.EightDirectionalRotation)
                {
                    activeBuildableCornerObjectSOEightDirectionalRotation = buildableCornerObjectSO.GetNextEightDirectionalDirectionCounterClockwise(activeBuildableCornerObjectSOEightDirectionalRotation);
                }
            }
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
            {
                if (buildableFreeObjectSO.rotationType == FreeObjectRotationType.FourDirectionalRotation)
                {
                    activeBuildableFreeObjectSOFourDirectionalRotation = buildableFreeObjectSO.GetNextFourDirectionalDirectionCounterClockwise(activeBuildableFreeObjectSOFourDirectionalRotation);
                }
                else if (buildableFreeObjectSO.rotationType == FreeObjectRotationType.EightDirectionalRotation)
                {
                    activeBuildableFreeObjectSOEightDirectionalRotation = buildableFreeObjectSO.GetNextEightDirectionalDirectionCounterClockwise(activeBuildableFreeObjectSOEightDirectionalRotation);
                }
            }
        }

        public override void SetInputBuildableObjectRotationScrollComplete()
        {
            isCounterClockwiseRotationKeyHolding = false;
            isClockwiseRotationKeyHolding = false;
        }

        public override void SetInputBuildableObjectCounterClockwiseRotationComplete()
        {
            isCounterClockwiseRotationKeyHolding = false;
        }

        public override void SetInputBuildableObjectClockwiseRotationComplete()
        {
            isClockwiseRotationKeyHolding = false;
        }

        public override void SetInputBuildableEdgeObjectFlip()
        {
            activeBuildableEdgeObjectSOFlipped = !activeBuildableEdgeObjectSOFlipped;
        }

        protected override void UpdateRotationKeyHolding()
        {
            if (!isCounterClockwiseRotationKeyHolding && !isClockwiseRotationKeyHolding || activeBuildableObjectSO == null) return;
            if (activeBuildableObjectSO is BuildableGridObjectSO || activeBuildableObjectSO is BuildableEdgeObjectSO) return;

            if (isClockwiseRotationKeyHolding) 
            {
                if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO && buildableCornerObjectSO.rotationType == CornerObjectRotationType.FreeRotation)
                {
                    activeBuildableCornerObjectSOFreeRotation = RotateClockwise(activeBuildableCornerObjectSOFreeRotation, buildableCornerObjectSO.freeRotationSpeed);
                }
                else if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && buildableFreeObjectSO.rotationType == FreeObjectRotationType.FreeRotation)
                {
                    activeBuildableFreeObjectSOFreeRotation = RotateClockwise(activeBuildableFreeObjectSOFreeRotation, buildableFreeObjectSO.freeRotationSpeed);
                }
            }
            else if (isCounterClockwiseRotationKeyHolding)
            {
                if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO && buildableCornerObjectSO.rotationType == CornerObjectRotationType.FreeRotation)
                {
                    activeBuildableCornerObjectSOFreeRotation = RotateCounterClockwise(activeBuildableCornerObjectSOFreeRotation, buildableCornerObjectSO.freeRotationSpeed);
                }
                else if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && buildableFreeObjectSO.rotationType == FreeObjectRotationType.FreeRotation)
                {
                    activeBuildableFreeObjectSOFreeRotation = RotateCounterClockwise(activeBuildableFreeObjectSOFreeRotation, buildableFreeObjectSO.freeRotationSpeed);
                }
            }
        }

        private float RotateClockwise(float currentRotation, float rotationSpeed)
        {
            float targetRotation = currentRotation + rotationSpeed * Time.deltaTime;
            if (targetRotation >= 360f) targetRotation -= 360f;
            return targetRotation;
        }

        public float RotateCounterClockwise(float currentRotation, float rotationSpeed)
        {
            float targetRotation = currentRotation - rotationSpeed * Time.deltaTime;
            if (targetRotation < 0f) targetRotation += 360f;
            return targetRotation;
        }
        #endregion Handle Input Buildable Object Rotation Functions End:

        #region Handle Input Buildable Free Object Spline Placement Functions Start:
        public override void SetInputBuildableFreeObjectSplinePlacementComplete()
        {
            if (buildableFreeObjectSpline == null) return;

            float splineLength = buildableFreeObjectSpline.GetLength();
            float step = buildableFreeObjectSplineObjectSpacing / splineLength;  // Step size based on spacing and spline length
            float interation = 0f;  // Start at the beginning of the spline

            if ((activeBuildableObjectSO as BuildableFreeObjectSO).intervalBetweenEachPlacement > 0) RaiseOnFreeObjectSplinePlacementCancelledEvent(this);

            // Iterate over the spline using normalized t values
            while (interation <= 1f)
            {
                Vector3 position = buildableFreeObjectSpline.EvaluatePosition(interation);  // Get the position at the current t value
                float freeRotation = default;

                if ((activeBuildableObjectSO as BuildableFreeObjectSO).objectRotateToSplineDirection) 
                {
                    Vector3 tangent = buildableFreeObjectSpline.EvaluateTangent(interation);
                    if (tangent != Vector3.zero) freeRotation = Quaternion.LookRotation(tangent).eulerAngles.y;
                }

                SetInputBuildableObjectPlacement(worldPosition: position, freeRotation: freeRotation, invokedByFinalizedBoxPlacement: true, forceFreeObjectRotation: (activeBuildableObjectSO as BuildableFreeObjectSO).objectRotateToSplineDirection ? true : false);
                interation += step;  // Increment "interation" to move to the next point on the spline
            }

            if (buildableFreeObjectSplineContainer != null)
            {
                Destroy(buildableFreeObjectSplineContainer.gameObject);
                buildableFreeObjectSplineContainer = null;
                buildableFreeObjectSpline = null;
            }
            buildableFreeObjectSplineObjectSpacing = (activeBuildableObjectSO as BuildableFreeObjectSO).objectBaseSpacingAlongSpline;
            RaiseOnFreeObjectSplinePlacementCancelledEvent(this);
            
            if (activeBuildableObjectSO.enablePlaceAndDeselect) ResetBuildablesAndGridMode();
            activeBuildableFreeObjectSOPlacementType = FreeObjectPlacementType.SinglePlacement;
        }

        public override void SetInputCycleThroughBuildableFreeObjectSplineSpacing(Vector2 inputDirection)
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement)
            {
                float targetSpacing = buildableFreeObjectSplineObjectSpacing + inputDirection.y;
                buildableFreeObjectSplineObjectSpacing = Mathf.Lerp(buildableFreeObjectSplineObjectSpacing, targetSpacing, Time.deltaTime * buildableFreeObjectSO.spacingChangeSmoothness);
                buildableFreeObjectSplineObjectSpacing = Mathf.Clamp(buildableFreeObjectSplineObjectSpacing, buildableFreeObjectSO.objectMinSpacingAlongSpline, buildableFreeObjectSO.objectMaxSpacingAlongSpline); 
            }
        }

        public override void SetInputIncreaseBuildableFreeObjectSplineSpacing(float amount = 1f)
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement)
            {
                float targetSpacing = buildableFreeObjectSplineObjectSpacing + amount;
                buildableFreeObjectSplineObjectSpacing = Mathf.Lerp(buildableFreeObjectSplineObjectSpacing, targetSpacing, Time.deltaTime * buildableFreeObjectSO.spacingChangeSmoothness);
                buildableFreeObjectSplineObjectSpacing = Mathf.Clamp(buildableFreeObjectSplineObjectSpacing, buildableFreeObjectSO.objectMinSpacingAlongSpline, buildableFreeObjectSO.objectMaxSpacingAlongSpline); 
            }
        }

        public override void SetInputDecreaseBuildableFreeObjectSplineSpacing(float amount = 1f)
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement)
            {
                float targetSpacing = buildableFreeObjectSplineObjectSpacing - amount;
                buildableFreeObjectSplineObjectSpacing = Mathf.Lerp(buildableFreeObjectSplineObjectSpacing, targetSpacing, Time.deltaTime * buildableFreeObjectSO.spacingChangeSmoothness);
                buildableFreeObjectSplineObjectSpacing = Mathf.Clamp(buildableFreeObjectSplineObjectSpacing, buildableFreeObjectSO.objectMinSpacingAlongSpline, buildableFreeObjectSO.objectMaxSpacingAlongSpline); 
            }
        }
        #endregion Handle Input Buildable Free Object Spline Placement Functions End:

        #region Handle Input Buildable Object Placement Functions Start:
        public override void SetInputBuildableObjectPlacement(bool isSceneObject = false, bool invokedInternally = true, BuildableObjectSO buildableObjectSO = null, Vector3 worldPosition = default, 
        FourDirectionalRotation fourDirectionalDirection = FourDirectionalRotation.North, EightDirectionalRotation eightDirectionalDirection = EightDirectionalRotation.North, float freeRotation = 0f, 
        bool isBuildableEdgeObjectFlipped = false, int activeVerticalGridIndex = -1, bool invokedByFinalizedBoxPlacement = false, bool forceFreeObjectRotation = false)
        {
            Vector3 hitNormals = default;

            if (!isSceneObject)
            {
                if (!gridManager.GetIsMousePointerOnGrid() || gridManager.GetActiveEasyGridBuilderPro() != this) return;
                if (useBuildModeActivationKey && activeGridMode != GridMode.BuildMode) return;
            }

            SetActiveGridMode(GridMode.BuildMode);
            isPlacementKeyHolding = true;

            if (buildableObjectSO == null)
            {
                if (activeBuildableObjectSO != null) buildableObjectSO = activeBuildableObjectSO;
                else return;
            }
            else SetInputActiveBuildableObjectSO(buildableObjectSO);

            activeVerticalGridIndex = activeVerticalGridIndex == -1 ? this.activeVerticalGridIndex : activeVerticalGridIndex;
            int previousActiveVerticalGridIndex = this.activeVerticalGridIndex;
            if (activeVerticalGridIndex != this.activeVerticalGridIndex) SetInputVerticalGrid(activeVerticalGridIndex);
            
            if (invokedInternally)
            {
                if (buildableObjectSO is BuildableFreeObjectSO && invokedByFinalizedBoxPlacement == false) worldPosition = AdjustWorldPositionBasedOnCollisionForFreeObject(buildableObjectSO, out hitNormals);
                else if (buildableObjectSO is not BuildableFreeObjectSO) worldPosition = AdjustWorldPositionBasedOnCollision(buildableObjectSO);
                
                if (buildableObjectSO is BuildableGridObjectSO buildableGridObject) 
                {   
                    if (buildableGridObject.freezeRotation) fourDirectionalDirection = buildableGridObject.fourDirectionalRotation;
                    else fourDirectionalDirection = activeBuildableGridObjectSORotation;
                }
                else if (buildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO) 
                {
                    if (buildableEdgeObjectSO.freezeRotation)
                    {
                        isBuildableEdgeObjectFlipped = buildableEdgeObjectSO.isObjectFlipped;
                        fourDirectionalDirection = buildableEdgeObjectSO.fourDirectionalRotation;
                    }
                    else
                    {
                        isBuildableEdgeObjectFlipped = activeBuildableEdgeObjectSOFlipped;
                        fourDirectionalDirection = activeBuildableEdgeObjectSORotation;
                    }
                }
                else if (buildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO) 
                {
                    if (buildableCornerObjectSO.freezeRotation)
                    {
                        fourDirectionalDirection = buildableCornerObjectSO.fourDirectionalRotation;
                        eightDirectionalDirection = buildableCornerObjectSO.eightDirectionalRotation;
                        freeRotation = buildableCornerObjectSO.freeRotation;
                    }
                    else
                    {
                        fourDirectionalDirection = activeBuildableCornerObjectSOFourDirectionalRotation;
                        eightDirectionalDirection = activeBuildableCornerObjectSOEightDirectionalRotation;
                        if (forceFreeObjectRotation == false) freeRotation = activeBuildableCornerObjectSOFreeRotation;
                    }
                }
                else if (buildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO) 
                {
                    if (buildableFreeObjectSO.freezeRotation)
                    {
                        fourDirectionalDirection = buildableFreeObjectSO.fourDirectionalRotation;
                        eightDirectionalDirection = buildableFreeObjectSO.eightDirectionalRotation;
                        freeRotation = buildableFreeObjectSO.freeRotation;
                    }
                    else
                    {
                        fourDirectionalDirection = activeBuildableFreeObjectSOFourDirectionalRotation;
                        eightDirectionalDirection = activeBuildableFreeObjectSOEightDirectionalRotation;
                        if (forceFreeObjectRotation == false) freeRotation = activeBuildableFreeObjectSOFreeRotation;
                    }
                }
            }
            
            if (!isSceneObject)
            {
                if (buildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && buildableFreeObjectSO.placementType != FreeObjectPlacementType.SplinePlacement) if (!CheckPlacementDistance(worldPosition)) return;
                if (MouseInteractionUtilities.IsMousePointerOverUI()) return;
            }
            
            if (buildableObjectSO is BuildableGridObjectSO buildableGridObjectSO)
            {
                if (!isSceneObject)
                {
                    if (buildableGridObjectSO.placementType != GridObjectPlacementType.SinglePlacement && !invokedInternally) return;
                    HandleGridObjectPlacementType(worldPosition, buildableGridObjectSO.placementType);
                    if (invokedByFinalizedBoxPlacement == false && IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) return;
                }
                
                SetupBuildableGridObjectPlacement(buildableGridObjectSO, worldPosition, fourDirectionalDirection, activeVerticalGridIndex, isSceneObject);
                if (isSceneObject) ResetBuildablesAndGridMode();
            }
            else if (buildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO)
            {
                if (!isSceneObject)
                {
                    if (buildableEdgeObjectSO.placementType != EdgeObjectPlacementType.SinglePlacement && !invokedInternally) return;
                    HandleEdgeObjectPlacementType(worldPosition, buildableEdgeObjectSO.placementType);

                    if (buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO != null)
                    {
                        CornerObjectPlacementType cornerObjectPlacementType = default;
                        switch (buildableEdgeObjectSO.placementType)
                        {
                            case EdgeObjectPlacementType.SinglePlacement: cornerObjectPlacementType = CornerObjectPlacementType.SinglePlacement; break;
                            case EdgeObjectPlacementType.PaintPlacement: cornerObjectPlacementType = CornerObjectPlacementType.PaintPlacement; break;
                            case EdgeObjectPlacementType.WireBoxPlacement: cornerObjectPlacementType = CornerObjectPlacementType.WireBoxPlacement; break;
                            case EdgeObjectPlacementType.FourDirectionWirePlacement: cornerObjectPlacementType = CornerObjectPlacementType.FourDirectionWirePlacement; break;
                            case EdgeObjectPlacementType.LShapedPlacement: cornerObjectPlacementType = CornerObjectPlacementType.LShapedPlacement; break;
                        }
                        HandleCornerObjectPlacementType(worldPosition, cornerObjectPlacementType);
                    }

                    if (invokedByFinalizedBoxPlacement == false && IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) return;
                }
                
                SetupBuildableEdgeObjectPlacement(buildableEdgeObjectSO, worldPosition, fourDirectionalDirection, isBuildableEdgeObjectFlipped, activeVerticalGridIndex, isSceneObject);
                
                if (buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO != null)
                {
                    if (!buildableEdgeObjectSO.onlyUsedWithGhostObject) SetupBuildableCornerObjectPlacement(buildableEdgeObjectSO.buildableCornerObjectSO, worldPosition, fourDirectionalDirection, 
                        eightDirectionalDirection, freeRotation, activeVerticalGridIndex, false, true);
                }

                if (isSceneObject) ResetBuildablesAndGridMode();
            }
            else if (buildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO)
            {
                if (!isSceneObject)
                {
                    if (buildableCornerObjectSO.placementType != CornerObjectPlacementType.SinglePlacement && !invokedInternally) return;
                    HandleCornerObjectPlacementType(worldPosition, buildableCornerObjectSO.placementType);
                    if (invokedByFinalizedBoxPlacement == false && IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) return;
                }

                SetupBuildableCornerObjectPlacement(buildableCornerObjectSO, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, activeVerticalGridIndex, isSceneObject);
                if (isSceneObject) ResetBuildablesAndGridMode();
            }
            else if (buildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
            {
                if (!isSceneObject)
                {
                    if (buildableFreeObjectSO.placementType != FreeObjectPlacementType.SinglePlacement && !invokedInternally) return;
                    if (invokedByFinalizedBoxPlacement == false) HandleFreeObjectPlacementType(worldPosition, buildableFreeObjectSO, buildableFreeObjectSO.placementType);
                    if (invokedByFinalizedBoxPlacement == false && IsActiveBuildableFreeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) return;
                }

                BuildableFreeObject originalBuildableFreeObject = null;
                BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab;
                if (isObjectMoving && movingBuildableObject)
                {
                    originalBuildableFreeObject = (BuildableFreeObject)movingBuildableObject;
                    buildableObjectSORandomPrefab = movingBuildableObject.GetBuildableObjectSORandomPrefab();
                }

                ICommand command = new CommandPlaceBuildableFreeObject(this, worldPosition, buildableFreeObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals, 
                    buildableObjectSORandomPrefab, false, activeVerticalGridIndex, isSceneObject, originalBuildableFreeObject);
                
                gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                BuildableFreeObject spawnnedBuildableFreeObject = ((CommandPlaceBuildableFreeObject)command).GetBuildableFreeObject();
                buildableObjectSORandomPrefab = ((CommandPlaceBuildableFreeObject)command).GetBuildableObjectSORandomPrefab();
                if (spawnnedBuildableFreeObject) gridManager.GetGridCommandInvoker().AddCommand(command);

                if (isSceneObject) ResetBuildablesAndGridMode();
            }
            
            if (this.activeVerticalGridIndex != previousActiveVerticalGridIndex) SetInputVerticalGrid(previousActiveVerticalGridIndex);
            else if (isSceneObject && activeVerticalGridIndex != 0) SetInputVerticalGrid(0);
        }

        private Vector3 AdjustWorldPositionBasedOnCollision(BuildableObjectSO activeBuildableObjectSO)
        {
            Vector3 worldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(activeBuildableObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), Vector3.down * 9999,
                out bool _, out Vector3 firstCollisionWorldPosition);
            worldPosition.y = firstCollisionWorldPosition.y;

            return worldPosition;
        }

        private Vector3 AdjustWorldPositionBasedOnCollisionForFreeObject(BuildableObjectSO activeBuildableObjectSO, out Vector3 hitNormals)
        {
            return MouseInteractionUtilities.GetMouseWorldPositionForBuildableFreeObject(activeBuildableObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask(), 
                (BuildableFreeObjectSO)activeBuildableObjectSO, Vector3.down * 99999, out hitNormals);
        }

        private bool CheckPlacementDistance(Vector3 worldPosition)
        {
            if (!gridManager.GetIsEnableDistanceBasedBuilding()) return true;

            if (!gridManager.GetDistanceCheckObject()) gridManager.SetDistanceCheckObject(Camera.main.transform);
            float distance = Vector3.Distance(gridManager.GetDistanceCheckObject().position, worldPosition);
            if (distance > gridManager.GetMinimumDistance() && distance < gridManager.GetMaximumDistance()) return true;

            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectPlacement)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Cannot Build Here!</b></color> Not Within The Provided Range! Distance: {distance}");
            }
            return false;
        }

        private void HandleGridObjectPlacementType(Vector3 worldPosition, GridObjectPlacementType gridObjectPlacementType)
        {
            switch (gridObjectPlacementType)
            {
                case GridObjectPlacementType.SinglePlacement: activeBuildableGridObjectSOPlacementType = GridObjectPlacementType.SinglePlacement; break;
                case GridObjectPlacementType.PaintPlacement: activeBuildableGridObjectSOPlacementType = GridObjectPlacementType.PaintPlacement; break;
                case GridObjectPlacementType.BoxPlacement:
                case GridObjectPlacementType.FourDirectionWirePlacement:
                case GridObjectPlacementType.WireBoxPlacement:
                case GridObjectPlacementType.LShapedPlacement:
                    if (activeBuildableGridObjectSOPlacementType != gridObjectPlacementType)
                    {
                        activeBuildableGridObjectSOPlacementType = gridObjectPlacementType;
                        StartGridObjectBoxPlacement(worldPosition, activeBuildableGridObjectSOPlacementType);
                    }
                break;
            }
        }

        private void HandleEdgeObjectPlacementType(Vector3 worldPosition, EdgeObjectPlacementType edgebjectPlacementType)
        {
            switch (edgebjectPlacementType)
            {
                case EdgeObjectPlacementType.SinglePlacement: activeBuildableEdgeObjectSOPlacementType = EdgeObjectPlacementType.SinglePlacement; break;
                case EdgeObjectPlacementType.PaintPlacement: activeBuildableEdgeObjectSOPlacementType = EdgeObjectPlacementType.PaintPlacement; break;
                case EdgeObjectPlacementType.FourDirectionWirePlacement:
                case EdgeObjectPlacementType.WireBoxPlacement:
                case EdgeObjectPlacementType.LShapedPlacement:
                    if (activeBuildableEdgeObjectSOPlacementType != edgebjectPlacementType)
                    {
                        activeBuildableEdgeObjectSOPlacementType = edgebjectPlacementType;
                        StartEdgeObjectBoxPlacement(worldPosition, activeBuildableEdgeObjectSOPlacementType);
                    }
                break;
            }
        }

        private void HandleCornerObjectPlacementType(Vector3 worldPosition, CornerObjectPlacementType cornerObjectPlacementType)
        {
            switch (cornerObjectPlacementType)
            {
                case CornerObjectPlacementType.SinglePlacement: activeBuildableCornerObjectSOPlacementType = CornerObjectPlacementType.SinglePlacement; break;
                case CornerObjectPlacementType.PaintPlacement: activeBuildableCornerObjectSOPlacementType = CornerObjectPlacementType.PaintPlacement; break;
                case CornerObjectPlacementType.BoxPlacement:
                case CornerObjectPlacementType.FourDirectionWirePlacement:
                case CornerObjectPlacementType.WireBoxPlacement:
                case CornerObjectPlacementType.LShapedPlacement:
                    if (activeBuildableCornerObjectSOPlacementType != cornerObjectPlacementType)
                    {
                        activeBuildableCornerObjectSOPlacementType = cornerObjectPlacementType;
                        StartCornerObjectBoxPlacement(worldPosition, activeBuildableCornerObjectSOPlacementType);
                    }
                break;
            }
        }

        private void HandleFreeObjectPlacementType(Vector3 worldPosition, BuildableFreeObjectSO buildableFreeObjectSO, FreeObjectPlacementType FreeObjectPlacementType)
        {
            switch (FreeObjectPlacementType)
            {
                case FreeObjectPlacementType.SinglePlacement: activeBuildableFreeObjectSOPlacementType = FreeObjectPlacementType.SinglePlacement; break;
                case FreeObjectPlacementType.PaintPlacement: activeBuildableFreeObjectSOPlacementType = FreeObjectPlacementType.PaintPlacement; break;
                case FreeObjectPlacementType.SplinePlacement: activeBuildableFreeObjectSOPlacementType = FreeObjectPlacementType.SplinePlacement; break;
            }

            if (activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement)
            {
                StartAndUpdateFreeObjectSplinePlacement(worldPosition, buildableFreeObjectSO, activeBuildableFreeObjectSOPlacementType);
            }
        }

        public override void SetInputBuildableObjectPlacementComplete(bool placeMovingObjectOnInputRelease = false)
        {
            isPlacementKeyHolding = false;
            if (placeMovingObjectOnInputRelease && isObjectMoving) SetInputBuildableObjectPlacement();
            
            if (!activeBuildableObjectSO) return;

            if (activeBuildableObjectSO is BuildableGridObjectSO && IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                SetInputBuildableObjectPlacement(invokedByFinalizedBoxPlacement: true);
                FinalizeGridObjectBoxPlacement();
            }
            else if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
            {
                SetInputBuildableObjectPlacement(invokedByFinalizedBoxPlacement: true);
                FinalizeEdgeObjectBoxPlacement();
                if (buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO != null) FinalizeCornerObjectBoxPlacement();
            }
            else if (activeBuildableObjectSO is BuildableCornerObjectSO && IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                SetInputBuildableObjectPlacement(invokedByFinalizedBoxPlacement: true);
                FinalizeCornerObjectBoxPlacement();
            }

            if (activeBuildableObjectSO.enablePlaceAndDeselect) ResetBuildablesAndGridMode();

            activeBuildableGridObjectSOPlacementType = GridObjectPlacementType.SinglePlacement;
            activeBuildableEdgeObjectSOPlacementType = EdgeObjectPlacementType.SinglePlacement;
            activeBuildableCornerObjectSOPlacementType = CornerObjectPlacementType.SinglePlacement;
        }

        protected override void UpdatePlacementKeyHodling()
        {
            if (!isPlacementKeyHolding || activeBuildableObjectSO == null) return;
            
            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO)
            {   
                if (buildableGridObjectSO.placementType == GridObjectPlacementType.PaintPlacement && activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.PaintPlacement) SetInputBuildableObjectPlacement();
            }
            else if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO)
            {   
                if (buildableEdgeObjectSO.placementType == EdgeObjectPlacementType.PaintPlacement && activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.PaintPlacement) SetInputBuildableObjectPlacement();
            }
            else if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO)
            {   
                if (buildableCornerObjectSO.placementType == CornerObjectPlacementType.PaintPlacement && activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.PaintPlacement) SetInputBuildableObjectPlacement();
            }
            else if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
            {   
                if (buildableFreeObjectSO.placementType == FreeObjectPlacementType.PaintPlacement && activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.PaintPlacement) SetInputBuildableObjectPlacement();
            }

            if (activeBuildableObjectSO is BuildableGridObjectSO && IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) UpdateGridObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableEdgeObjectSO _buildableEdgeObjectSO && IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                UpdateEdgeObjectBoxPlacement();
                if (_buildableEdgeObjectSO.mergeWithBuildableCornerObject && _buildableEdgeObjectSO.buildableCornerObjectSO != null) UpdateCornerObjectBoxPlacement();
            }
            if (activeBuildableObjectSO is BuildableCornerObjectSO && IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) UpdateCornerObjectBoxPlacement();
        }
        #endregion Handle Input Buildable Object Placement Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT EXECUTION FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///
        
        #region Setup Buildable Grid Object Placement Functions Start:
        public override bool TryInitializeBuildableGridObjectSinglePlacement(Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreCustomConditions, 
            bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, out BuildableGridObject spawnnedBuildableGridObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, 
            BuildableGridObject originalBuildableGridObject = null)
        {
            buildableObjectSORandomPrefab = buildableObjectSORandomPrefab ?? GetUpdatedActiveBuildableObjectSORandomPrefab(buildableGridObjectSO, out _);
            Vector2Int originCellPosition = GetCellPosition(worldPosition, verticalGridIndex);
            
            if (!isObjectMoving)
            {
                ICommand command = new CommandPlaceBuildableGridObject(this, originCellPosition, worldPosition, buildableGridObjectSO, direction, buildableObjectSORandomPrefab, ignoreCustomConditions,
                    ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, originalBuildableGridObject);

                gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                spawnnedBuildableGridObject = ((CommandPlaceBuildableGridObject)command).GetBuildableGridObject();
                buildableObjectSORandomPrefab = ((CommandPlaceBuildableGridObject)command).GetBuildableObjectSORandomPrefab();
                if (spawnnedBuildableGridObject) gridManager.GetGridCommandInvoker().AddCommand(command);
            }
            else
            {
                InvokeTryPlaceBuildableGridObjectSinglePlacement(originCellPosition, worldPosition, buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, 
                    ignoreCustomConditions, false, verticalGridIndex, byPassEventsAndMessages, out spawnnedBuildableGridObject, originalBuildableGridObject);
            }

            return spawnnedBuildableGridObject != null;
        }

        private void SetupBuildableGridObjectPlacement(BuildableGridObjectSO activeBuildableGridObjectSO, Vector3 worldPosition, FourDirectionalRotation direction, int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            bool ignoreBuildConditions = isObjectMoving ? true : false;
            bool ignoreReplacement = isObjectMoving ? true : false;
            byPassEventsAndMessages = isObjectMoving ? true : byPassEventsAndMessages;

            if (!IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) InitializeBuildableGridObjectSinglePlacement(worldPosition, activeBuildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, 
                activeVerticalGridIndex, byPassEventsAndMessages);
            else
            {
                switch (activeBuildableGridObjectSOPlacementType)
                {
                    case GridObjectPlacementType.BoxPlacement: InitializeBuildableGridObjectBoxPlacement(activeBuildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); break;
                    case GridObjectPlacementType.WireBoxPlacement: InitializeBuildableGridObjectWireBoxPlacement(activeBuildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); break;
                    case GridObjectPlacementType.FourDirectionWirePlacement: InitializeBuildableGridObjectFourDirectionWirePlacement(activeBuildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); break;
                    case GridObjectPlacementType.LShapedPlacement: InitializeBuildableGridObjectLShapedPlacement(activeBuildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); break;
                }
            }
        }

        private void InitializeBuildableGridObjectSinglePlacement(Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreCustomConditions, bool ignoreReplacement,
            int verticalGridIndex, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab;
            Vector2Int originCellPosition = GetCellPosition(worldPosition, verticalGridIndex);

            BuildableGridObject originalBuildableGridObject = null;

            if (isObjectMoving && movingBuildableObject)
            {
                originalBuildableGridObject = (BuildableGridObject)movingBuildableObject;
                buildableObjectSORandomPrefab = movingBuildableObject.GetBuildableObjectSORandomPrefab();
            }

            ICommand command = new CommandPlaceBuildableGridObject(this, originCellPosition, worldPosition, buildableGridObjectSO, direction, buildableObjectSORandomPrefab, 
                ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, originalBuildableGridObject);
            
            gridManager.GetGridCommandInvoker().ExecuteCommand(command);
            BuildableGridObject spawnnedBuildableGridObject = ((CommandPlaceBuildableGridObject)command).GetBuildableGridObject();
            buildableObjectSORandomPrefab = ((CommandPlaceBuildableGridObject)command).GetBuildableObjectSORandomPrefab();
            if (spawnnedBuildableGridObject) gridManager.GetGridCommandInvoker().AddCommand(command);
        }

        private void InitializeBuildableGridObjectBoxPlacement(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement,
            int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableGridBoxPlacementObjects(buildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages));
        }

        private void InitializeBuildableGridObjectWireBoxPlacement(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement, 
            int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableGridWireBoxPlacementObjects(buildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages));
        }

        private void InitializeBuildableGridObjectFourDirectionWirePlacement(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement, 
            int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableGridFourDirectionalWirePlacementObjects(buildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages));
        }

        private void InitializeBuildableGridObjectLShapedPlacement(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement, 
            int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableGridLShapedPlacementObjects(buildableGridObjectSO, direction, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages));
        }

        private IEnumerator PlaceBuildableGridBoxPlacementObjects(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreCustomConditions, bool ignoreReplacement,
            int verticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"

            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minY; z <= maxY; z++)
                {
                    Vector2Int originCellPosition = new Vector2Int(x, z);
                    Vector3 worldPosition = GetActiveGridCellWorldPosition(originCellPosition);

                    ICommand command = new CommandPlaceBuildableGridObject(this, originCellPosition, worldPosition, buildableGridObjectSO, direction, buildableObjectSORandomPrefab,
                        ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, null);

                    gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                    BuildableGridObject spawnnedBuildableGridObject = ((CommandPlaceBuildableGridObject)command).GetBuildableGridObject();
                    buildableObjectSORandomPrefab = ((CommandPlaceBuildableGridObject)command).GetBuildableObjectSORandomPrefab();
                    if (spawnnedBuildableGridObject) gridManager.GetGridCommandInvoker().AddCommand(command);

                    if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                }
            }
        }

        private IEnumerator PlaceBuildableGridWireBoxPlacementObjects(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement, 
            int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"

            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            if (buildableGridObjectSO.spawnOnlyAtEndPoints)
            {
                // Place objects only at the four corners
                PlaceIndividualBuildableGridObject(new Vector2Int(minX, minY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                    activeVerticalGridIndex, byPassEventsAndMessages); // Top-left corner
                if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);

                PlaceIndividualBuildableGridObject(new Vector2Int(maxX, minY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                    activeVerticalGridIndex, byPassEventsAndMessages); // Top-right corner
                if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);

                PlaceIndividualBuildableGridObject(new Vector2Int(minX, maxY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                    activeVerticalGridIndex, byPassEventsAndMessages); // Bottom-left corner
                if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);

                PlaceIndividualBuildableGridObject(new Vector2Int(maxX, maxY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                    activeVerticalGridIndex, byPassEventsAndMessages); // Bottom-right corner
            }
            else
            {
                // Original placement logic for full border
                for (int x = minX; x <= maxX; x++)
                {
                    PlaceIndividualBuildableGridObject(new Vector2Int(x, minY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages); // Top border
                    PlaceIndividualBuildableGridObject(new Vector2Int(x, maxY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages); // Bottom border
                    if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                }

                for (int y = minY + 1; y < maxY; y++)
                {
                    PlaceIndividualBuildableGridObject(new Vector2Int(minX, y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages); // Left border
                    PlaceIndividualBuildableGridObject(new Vector2Int(maxX, y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages); // Right border
                    if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                }
            }
        }

        private IEnumerator PlaceBuildableGridFourDirectionalWirePlacementObjects(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement,
            int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"

            int dx = Mathf.Abs(endCell.x - startCell.x);
            int dy = Mathf.Abs(endCell.y - startCell.y);
            bool isHorizontal = dx >= dy;

            if (buildableGridObjectSO.spawnOnlyAtEndPoints)
            {
                if (isHorizontal)
                {
                    // Place only at the start and end points on the horizontal line
                    PlaceIndividualBuildableGridObject(new Vector2Int(startCell.x, startCell.y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    PlaceIndividualBuildableGridObject(new Vector2Int(endCell.x, startCell.y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages);
                }
                else
                {
                    // Place only at the start and end points on the vertical line
                    PlaceIndividualBuildableGridObject(new Vector2Int(startCell.x, startCell.y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    PlaceIndividualBuildableGridObject(new Vector2Int(startCell.x, endCell.y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                        activeVerticalGridIndex, byPassEventsAndMessages);
                }
            }
            else
            {
                // Original placement logic for full line
                if (isHorizontal)
                {
                    int minX = Mathf.Min(startCell.x, endCell.x);
                    int maxX = Mathf.Max(startCell.x, endCell.x);
                    for (int x = minX; x <= maxX; x++)
                    {
                        PlaceIndividualBuildableGridObject(new Vector2Int(x, startCell.y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                            activeVerticalGridIndex, byPassEventsAndMessages);
                        if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    }
                }
                else
                {
                    int minY = Mathf.Min(startCell.y, endCell.y);
                    int maxY = Mathf.Max(startCell.y, endCell.y);
                    for (int y = minY; y <= maxY; y++)
                    {
                        PlaceIndividualBuildableGridObject(new Vector2Int(startCell.x, y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement,
                            activeVerticalGridIndex, byPassEventsAndMessages);
                        if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    }
                }
            }
        }

        private IEnumerator PlaceBuildableGridLShapedPlacementObjects(BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, bool ignoreBuildConditions, bool ignoreReplacement,
            int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"

            int dx = Mathf.Abs(endCell.x - startCell.x);
            int dy = Mathf.Abs(endCell.y - startCell.y);
            bool isHorizontalFirst = dx >= dy;

            if (buildableGridObjectSO.spawnOnlyAtEndPoints)
            {
                // Place at the start point
                PlaceIndividualBuildableGridObject(startCell, buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                
                // Place at the bend point
                Vector2Int bendPoint = isHorizontalFirst ? new Vector2Int(endCell.x, startCell.y) : new Vector2Int(startCell.x, endCell.y);
                PlaceIndividualBuildableGridObject(bendPoint, buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);

                // Place at the end point
                PlaceIndividualBuildableGridObject(endCell, buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
            }
            else
            {
                // Original placement logic for full L-shape
                if (isHorizontalFirst)
                {
                    int minX = Mathf.Min(startCell.x, endCell.x);
                    int maxX = Mathf.Max(startCell.x, endCell.x);
                    for (int x = minX; x <= maxX; x++)
                    {
                        PlaceIndividualBuildableGridObject(new Vector2Int(x, startCell.y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                            activeVerticalGridIndex, byPassEventsAndMessages);
                        if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    }

                    int finalX = isHorizontalFirst ? endCell.x : startCell.x;
                    int minY = Mathf.Min(startCell.y, endCell.y);
                    int maxY = Mathf.Max(startCell.y, endCell.y);
                    for (int y = minY; y <= maxY; y++)
                    {
                        PlaceIndividualBuildableGridObject(new Vector2Int(finalX, y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                            activeVerticalGridIndex, byPassEventsAndMessages);
                        if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    }
                }
                else
                {
                    int minY = Mathf.Min(startCell.y, endCell.y);
                    int maxY = Mathf.Max(startCell.y, endCell.y);
                    for (int y = minY; y <= maxY; y++)
                    {
                        PlaceIndividualBuildableGridObject(new Vector2Int(startCell.x, y), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                            activeVerticalGridIndex, byPassEventsAndMessages);
                        if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    }

                    int finalY = isHorizontalFirst ? startCell.y : endCell.y;
                    int minX = Mathf.Min(startCell.x, endCell.x);
                    int maxX = Mathf.Max(startCell.x, endCell.x);
                    for (int x = minX; x <= maxX; x++)
                    {
                        PlaceIndividualBuildableGridObject(new Vector2Int(x, finalY), buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, 
                            activeVerticalGridIndex, byPassEventsAndMessages);
                        if (buildableGridObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableGridObjectSO.intervalBetweenEachPlacement);
                    }
                }
            }
        }

        private void PlaceIndividualBuildableGridObject(Vector2Int cellPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab,
            bool ignoreCustomConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector3 worldPosition = GetActiveGridCellWorldPosition(cellPosition);
            
            ICommand command = new CommandPlaceBuildableGridObject(this, cellPosition, worldPosition, buildableGridObjectSO, direction, buildableObjectSORandomPrefab, 
                ignoreCustomConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, null);
                
            gridManager.GetGridCommandInvoker().ExecuteCommand(command);
            BuildableGridObject spawnnedBuildableGridObject = ((CommandPlaceBuildableGridObject)command).GetBuildableGridObject();
            buildableObjectSORandomPrefab = ((CommandPlaceBuildableGridObject)command).GetBuildableObjectSORandomPrefab();
            if (spawnnedBuildableGridObject) gridManager.GetGridCommandInvoker().AddCommand(command);
        }

        public override void InvokeTryPlaceBuildableGridObjectSinglePlacement(Vector2Int originCellPosition, Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, 
            ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, 
            out BuildableGridObject buildableGridObject, BuildableGridObject originalBuildableGridObject = null)
        {
            List<Vector2Int> cellPositionList = buildableGridObjectSO.GetObjectCellPositionsList(new Vector2Int(originCellPosition.x, originCellPosition.y), direction, cellSize, buildableObjectSORandomPrefab);

            if (TryPlaceBuildableGridObjectSinglePlacement(cellPositionList, new CellPositionXZ(originCellPosition.x, originCellPosition.y), worldPosition, buildableGridObjectSO, direction, 
                ref buildableObjectSORandomPrefab, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, out buildableGridObject, originalBuildableGridObject))
            {
                RaiseBuildableObjectPlacementEventsAndMessages(this, buildableGridObject, byPassEventsAndMessages);
            }
            else
            {
                if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectPlacement)
                {
                    string message = default;
                    bool isWithinGridBounds = true;
                    foreach (Vector2Int cellPosition in cellPositionList)
                    {
                        if (!IsWithinGridBounds(cellPosition, verticalGridIndex)) isWithinGridBounds = false;
                    }

                    message = isWithinGridBounds ? $"Grid Position Is Occupied!" : "Out of Grid Bounds!";
                    Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Cannot Build Here!</b></color> {message}");
                }
            }
        }

        private bool TryPlaceBuildableGridObjectSinglePlacement(List<Vector2Int> cellPositionList, CellPositionXZ buildableObjectOriginCellPosition, Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, 
            FourDirectionalRotation direction, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, 
            bool byPassEventsAndMessages, out BuildableGridObject buildableGridObject, BuildableGridObject originalBuildableGridObject = null)
        {
            bool isBuildable = true;
            bool isObjectReplacing = false;

            foreach (Vector2Int cellPosition in cellPositionList)
            {
                if (!gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition))
                {
                    isBuildable = false;
                    break;
                }
                if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.x, cellPosition.y).GetBuildableGridObjectData().ContainsKey(buildableGridObjectSO.buildableGridObjectCategorySO))
                {
                    BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.x, cellPosition.y).GetBuildableGridObjectData()[buildableGridObjectSO.buildableGridObjectCategorySO];
                    if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                    {
                        isObjectReplacing = true;
                        buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, false);
                    }
                    else
                    {
                        isBuildable = false;
                        break;
                    }
                }

                if (buildableGridObjectSO.affectByBasicAreaEnablers && IsBuildableObjectEnabledByBasicGridAreaEnablers())
                {
                    isBuildable = true;
                    continue;
                }
                if (buildableGridObjectSO.affectByAreaEnablers && IsGridObjectEnabledByGridAreaEnablers(gridList[activeVerticalGridIndex], buildableGridObjectSO, cellPosition))
                {
                    isBuildable = true;
                    continue;
                }

                if (buildableGridObjectSO.affectByBasicAreaDisablers && IsBuildableObjectDisabledByBasicGridAreaDisablers())
                {
                    isBuildable = false;
                    break;
                }
                if (buildableGridObjectSO.affectByAreaDisablers && IsGridObjectDisabledByGridAreaDisablers(gridList[activeVerticalGridIndex], buildableGridObjectSO, cellPosition))
                {
                    isBuildable = false;
                    break;
                }

                // Add Later
                if (isObjectReplacing){}

                // if (!ignoreBuildConditions)
                // {
                //     if (isObjectReplacing && buildableGridObjectSO.replacingObjectIgnoreCustomConditions) continue;
                //     if (buildableGridObjectSO.enableBuildConditions)
                //     {
                //         foreach (BuildConditionsSO buildConditionsSO in buildableGridObjectSO.buildConditionsSOList)
                //         {
                //             if (!GetBuildConditionBuildableGridObject(buildableGridObjectSO))
                //             {
                //                 //Debug.Log("Conditions not met");
                //                 isBuildable = false;
                //                 break;
                //             }
                //         }
                //     }

                //     if (buildableAreaBlockerHit)
                //     {
                //         if (buildableAreaBlockerHitBlockGridObject)
                //         {
                //             //Debug.Log("Conditions not met");
                //             isBuildable = false;
                //             break;
                //         }
                //     }
                // }
            }

            if (isBuildable)
            {
                InitializeBuildableGridObjectPlacement(buildableObjectOriginCellPosition, worldPosition, buildableGridObjectSO, direction, ref buildableObjectSORandomPrefab, false, activeVerticalGridIndex, byPassEventsAndMessages,
                cellPositionList, out buildableGridObject, originalBuildableGridObject);
                return true;
            }
            else
            {
                buildableGridObject = null;
                return false;
            }
        }

        private void InitializeBuildableGridObjectPlacement(CellPositionXZ buildableObjectOriginCellPosition, Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, FourDirectionalRotation direction, 
            ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreBuildConditions, int activeVerticalGridIndex, bool byPassEventsAndMessages, List<Vector2Int> cellPositionList, 
            out BuildableGridObject buildableGridObject, BuildableGridObject originalBuildableGridObject = null)
        {
            Vector2Int rotationOffset = buildableGridObjectSO.GetObjectRotationOffset(direction, cellSize, buildableObjectSORandomPrefab);
            Vector3 placedObjectWorldPosition = gridList[activeVerticalGridIndex].GetCellWorldPosition(buildableObjectOriginCellPosition) + new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
            placedObjectWorldPosition = new Vector3(placedObjectWorldPosition.x, worldPosition.y, placedObjectWorldPosition.z);

            if (!originalBuildableGridObject) buildableGridObject = Instantiate(buildableObjectSORandomPrefab.objectPrefab, Vector3.zero, Quaternion.identity).GetComponent<BuildableGridObject>();
            else buildableGridObject = originalBuildableGridObject;

            buildableGridObject.SetupBuildableGridObject(this, activeVerticalGridIndex, cellSize, buildableGridObjectSO, buildableObjectSORandomPrefab, placedObjectWorldPosition, 
                new Vector2Int(buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z), cellPositionList, direction);

            if (buildableGridObjectSO.eachPlacementUseRandomPrefab) 
            {
                buildableObjectSORandomPrefab = GetUpdatedActiveBuildableObjectSORandomPrefab(buildableGridObjectSO, out _);
                if (buildableGridObjectSO.intervalBetweenEachPlacement <= 0) UpdateActiveBuildableObjectSORandomPrefab(buildableGridObjectSO);
            }

            if (!ignoreBuildConditions)
            {
                // if (buildableGridObjectSO.enableBuildConditions)
                // {
                //     CompleteBuildConditionBuildableGridObject(buildableGridObjectSO);
                // }
            }

            foreach (Vector2Int cellPosition in cellPositionList)
            {
                GridCellData gridCellData = gridList[activeVerticalGridIndex].GetCellData(cellPosition.x, cellPosition.y);                          // Get the GridCellData Copy
                Dictionary<BuildableGridObjectCategorySO, BuildableGridObject> buildableGridObjects = gridCellData.GetBuildableGridObjectData();    // Initialize the dictionary
                buildableGridObjects[buildableGridObjectSO.buildableGridObjectCategorySO] = buildableGridObject;                                    // Modify the data
                gridList[activeVerticalGridIndex].SetCellData(cellPosition.x, cellPosition.y, gridCellData);                                        // Write the modified GridCellData back to the original
                
                if (!byPassEventsAndMessages) RaiseActiveBuildableSOChangeEventsAndMessages(this, cellPosition);
            }
        }

        private void StartGridObjectBoxPlacement(Vector3 worldPosition, GridObjectPlacementType placementType)
        {
            boxPlacementStartPosition = worldPosition;
            RaiseOnGridObjectBoxPlacementStartedEvent(this, boxPlacementStartPosition, placementType);
        }

        private void UpdateGridObjectBoxPlacement()
        {
            LayerMask layerMask = gridManager.GetGridSystemLayerMask();

            boxPlacementEndPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(activeBuildableObjectSO.customSurfaceLayerMask, layerMask, Vector3.down * 9999,
                out _, out Vector3 firstCollisionWorldPosition);
            boxPlacementEndPosition = new Vector3(boxPlacementEndPosition.x, firstCollisionWorldPosition.y, boxPlacementEndPosition.z);
            
            RaiseOnGridObjectBoxPlacementUpdatedEvent(this, boxPlacementEndPosition);
        }

        private void FinalizeGridObjectBoxPlacement()
        {
            activeBuildableGridObjectSOPlacementType = GridObjectPlacementType.SinglePlacement;
            RaiseOnGridObjectBoxPlacementFinalizedEvent(this);
        }

        private void CancelGridObjectBoxPlacement()
        {
            activeBuildableGridObjectSOPlacementType = GridObjectPlacementType.SinglePlacement;
            RaiseOnGridObjectBoxPlacementCancelledEvent(this);
        }
        #endregion Setup Buildable Grid Object Placement Functions End:

        #region Setup Buildable Edge Object Placement Functions Start:
        public override bool TryInitializeBuildableEdgeObjectSinglePlacement(Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            bool isBuildableEdgeObjectFlipped, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, out BuildableEdgeObject spawnnedBuildableEdgeObject, 
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableEdgeObject originalBuildableEdgeObject = null)
        {
            buildableObjectSORandomPrefab = buildableObjectSORandomPrefab ?? GetUpdatedActiveBuildableObjectSORandomPrefab(buildableEdgeObjectSO, out _);
            Vector2Int buildableObjectOriginCellPosition = GetCellPosition(worldPosition, verticalGridIndex);
            Vector3 closestCornerWorldPosition = GetClosestCorner(worldPosition, GetActiveGridCellWorldPosition(buildableObjectOriginCellPosition), out CornerObjectCellDirection cornerObjectOriginCellDirection);

            if (!isObjectMoving)
            {
                ICommand command = new CommandPlaceBuildableEdgeObject(this, buildableObjectOriginCellPosition, closestCornerWorldPosition, default, buildableEdgeObjectSO, fourDirectionalDirection, 
                    isBuildableEdgeObjectFlipped, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions,
                    ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, originalBuildableEdgeObject);

                gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                spawnnedBuildableEdgeObject = ((CommandPlaceBuildableEdgeObject)command).GetBuildableEdgeObject();
                buildableObjectSORandomPrefab = ((CommandPlaceBuildableEdgeObject)command).GetBuildableObjectSORandomPrefab();
                if (spawnnedBuildableEdgeObject) gridManager.GetGridCommandInvoker().AddCommand(command);
            }
            else
            {
                InvokeTryPlaceBuildableEdgeObjectSinglePlacement(buildableObjectOriginCellPosition, buildableEdgeObjectSO, fourDirectionalDirection, 
                    isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, closestCornerWorldPosition, cornerObjectOriginCellDirection, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, 
                    byPassEventsAndMessages, out spawnnedBuildableEdgeObject, originalBuildableEdgeObject: originalBuildableEdgeObject);
            }

            return spawnnedBuildableEdgeObject != null;
        }

        private void SetupBuildableEdgeObjectPlacement(BuildableEdgeObjectSO buildableEdgeObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection,
        bool isBuildableEdgeObjectFlipped, int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            bool ignoreBuildConditions = isObjectMoving ? true : false;
            bool ignoreReplacement = isObjectMoving ? true : false;
            byPassEventsAndMessages = isObjectMoving ? true : byPassEventsAndMessages;

            if (!IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                InitializeBuildableEdgeObjectSinglePlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
            }
            else
            {
                switch (activeBuildableEdgeObjectSOPlacementType)
                {
                    case EdgeObjectPlacementType.WireBoxPlacement:
                        InitializeBuildableEdgeObjectWireBoxPlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    break;
                    case EdgeObjectPlacementType.FourDirectionWirePlacement:
                        InitializeBuildableEdgeObjectFourDirectionWirePlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    break;
                    case EdgeObjectPlacementType.LShapedPlacement:
                        InitializeBuildableEdgeObjectLShapedPlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    break;
                }
            }
        }

        private void InitializeBuildableEdgeObjectSinglePlacement(Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            bool isBuildableEdgeObjectFlipped, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector2Int buildableObjectOriginCellPosition = GetCellPosition(worldPosition, verticalGridIndex);
            Vector3 closestCornerWorldPosition = GetClosestCorner(worldPosition, GetActiveGridCellWorldPosition(buildableObjectOriginCellPosition), out CornerObjectCellDirection cornerObjectOriginCellDirection);

            BuildableEdgeObject originalBuildableEdgeObject = null;

            if (isObjectMoving && movingBuildableObject)
            {
                originalBuildableEdgeObject = (BuildableEdgeObject)movingBuildableObject;
                buildableObjectSORandomPrefab = movingBuildableObject.GetBuildableObjectSORandomPrefab();
            }

            ICommand command = new CommandPlaceBuildableEdgeObject(this, buildableObjectOriginCellPosition, closestCornerWorldPosition, default, buildableEdgeObjectSO, fourDirectionalDirection, 
                isBuildableEdgeObjectFlipped, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, 
                originalBuildableEdgeObject);

            gridManager.GetGridCommandInvoker().ExecuteCommand(command);
            BuildableEdgeObject spawnnedBuildableEdgeObject = ((CommandPlaceBuildableEdgeObject)command).GetBuildableEdgeObject();
            buildableObjectSORandomPrefab = ((CommandPlaceBuildableEdgeObject)command).GetBuildableObjectSORandomPrefab();
            if (spawnnedBuildableEdgeObject) gridManager.GetGridCommandInvoker().AddCommand(command);
        }

        private void InitializeBuildableEdgeObjectWireBoxPlacement(Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);

            if (startCell == endCell) InitializeBuildableEdgeObjectSinglePlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement,
                activeVerticalGridIndex, byPassEventsAndMessages);
            else StartCoroutine(PlaceBuildableEdgeWireBoxPlacementObjects(buildableEdgeObjectSO, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, 
                startCell, endCell, byPassEventsAndMessages));
        }

        private void InitializeBuildableEdgeObjectFourDirectionWirePlacement(Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);

            if (startCell == endCell) InitializeBuildableEdgeObjectSinglePlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement,
                activeVerticalGridIndex, byPassEventsAndMessages);
            else StartCoroutine(PlaceBuildableEdgeFourDirectionalWirePlacementObjects(buildableEdgeObjectSO, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, 
                startCell, endCell, byPassEventsAndMessages));
        }

        private void InitializeBuildableEdgeObjectLShapedPlacement(Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection,
            bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            
            if (startCell == endCell) InitializeBuildableEdgeObjectSinglePlacement(worldPosition, buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ignoreBuildConditions, 
                ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
            else StartCoroutine(PlaceBuildableEdgeLShapedPlacementObjects(buildableEdgeObjectSO, isBuildableEdgeObjectFlipped, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, 
                startCell, endCell, byPassEventsAndMessages));
        }

        private IEnumerator PlaceBuildableEdgeWireBoxPlacementObjects(BuildableEdgeObjectSO buildableEdgeObjectSO, bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement,
            int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection);
            FourDirectionalRotation fourDirectionalDirection;

            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            for (int x = minX; x < maxX; x++)
            {
                fourDirectionalDirection = FourDirectionalRotation.East;

                PlaceIndividualBuildableEdgeObject(new Vector2Int(x, minY), buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                    ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); // Top border
                PlaceIndividualBuildableEdgeObject(new Vector2Int(x, maxY), buildableEdgeObjectSO, fourDirectionalDirection, !isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                    ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); // Bottom border
                if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
            }

            for (int y = minY; y < maxY; y++)
            {
                fourDirectionalDirection = FourDirectionalRotation.North;

                PlaceIndividualBuildableEdgeObject(new Vector2Int(minX, y), buildableEdgeObjectSO, fourDirectionalDirection, !isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                    ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); // Left border
                PlaceIndividualBuildableEdgeObject(new Vector2Int(maxX, y), buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                    ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages); // Right border
                if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
            }
        }

        private IEnumerator PlaceBuildableEdgeFourDirectionalWirePlacementObjects(BuildableEdgeObjectSO buildableEdgeObjectSO, bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement,
            int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection);
            FourDirectionalRotation fourDirectionalDirection;

            int dx = Mathf.Abs(endCell.x - startCell.x);
            int dy = Mathf.Abs(endCell.y - startCell.y);
            bool isHorizontal = dx >= dy;

            if (isHorizontal)
            {
                fourDirectionalDirection = FourDirectionalRotation.East;

                int minX = Mathf.Min(startCell.x, endCell.x);
                int maxX = Mathf.Max(startCell.x, endCell.x);
                for (int x = minX; x < maxX; x++)
                {
                    PlaceIndividualBuildableEdgeObject(new Vector2Int(x, startCell.y), buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                        ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
                }
            }
            else
            {
                fourDirectionalDirection = FourDirectionalRotation.North;

                int minY = Mathf.Min(startCell.y, endCell.y);
                int maxY = Mathf.Max(startCell.y, endCell.y);
                for (int y = minY; y < maxY; y++)
                {
                    PlaceIndividualBuildableEdgeObject(new Vector2Int(startCell.x, y), buildableEdgeObjectSO, fourDirectionalDirection, isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                        ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
                }
            }
        }

        private IEnumerator PlaceBuildableEdgeLShapedPlacementObjects(BuildableEdgeObjectSO buildableEdgeObjectSO, bool isBuildableEdgeObjectFlipped, bool ignoreBuildConditions, bool ignoreReplacement,
            int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, bool byPassEventsAndMessages)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection);
            FourDirectionalRotation fourDirectionalDirection;

            int dx = Mathf.Abs(endCell.x - startCell.x);
            int dy = Mathf.Abs(endCell.y - startCell.y);
            bool isHorizontalFirst = dx >= dy;

            // Determine the direction of movement for both segments
            bool isHorizontalLeftToRight = startCell.x < endCell.x;
            bool isVerticalBottomToTop = startCell.y < endCell.y;

            if (isHorizontalFirst)
            {
                fourDirectionalDirection = FourDirectionalRotation.East;

                int minX = Mathf.Min(startCell.x, endCell.x);
                int maxX = Mathf.Max(startCell.x, endCell.x);
                for (int x = minX; x < maxX; x++)
                {
                    bool tempIsBuildableEdgeObjectFlipped;
                    if (isHorizontalLeftToRight && !isVerticalBottomToTop) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else if (!isHorizontalLeftToRight && !isVerticalBottomToTop) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else tempIsBuildableEdgeObjectFlipped = isBuildableEdgeObjectFlipped;

                    PlaceIndividualBuildableEdgeObject(new Vector2Int(x, startCell.y), buildableEdgeObjectSO, fourDirectionalDirection, tempIsBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                        ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
                }

                fourDirectionalDirection = FourDirectionalRotation.North;

                int finalX = endCell.x; // The point where the horizontal and vertical parts meet
                int minY = Mathf.Min(startCell.y, endCell.y);
                int maxY = Mathf.Max(startCell.y, endCell.y);

                for (int y = minY; y < maxY; y++)
                {
                    bool tempIsBuildableEdgeObjectFlipped;
                    if (!isHorizontalLeftToRight && isVerticalBottomToTop) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else if (!isHorizontalLeftToRight && !isVerticalBottomToTop) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else tempIsBuildableEdgeObjectFlipped = isBuildableEdgeObjectFlipped;

                    PlaceIndividualBuildableEdgeObject(new Vector2Int(finalX, y), buildableEdgeObjectSO, fourDirectionalDirection, tempIsBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                        ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
                }
            }
            else
            {
                fourDirectionalDirection = FourDirectionalRotation.North;

                int minY = Mathf.Min(startCell.y, endCell.y);
                int maxY = Mathf.Max(startCell.y, endCell.y);
                for (int y = minY; y < maxY; y++)
                {
                    bool tempIsBuildableEdgeObjectFlipped;
                    if (!isVerticalBottomToTop && isHorizontalLeftToRight) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else if (isVerticalBottomToTop && isHorizontalLeftToRight) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else tempIsBuildableEdgeObjectFlipped = isBuildableEdgeObjectFlipped;

                    PlaceIndividualBuildableEdgeObject(new Vector2Int(startCell.x, y), buildableEdgeObjectSO, fourDirectionalDirection, tempIsBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                        ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
                }

                fourDirectionalDirection = FourDirectionalRotation.East;

                int finalY = endCell.y; // The point where the horizontal and vertical parts meet
                int minX = Mathf.Min(startCell.x, endCell.x);
                int maxX = Mathf.Max(startCell.x, endCell.x);
                for (int x = minX; x < maxX; x++)
                {
                    bool tempIsBuildableEdgeObjectFlipped;
                    if (isVerticalBottomToTop && !isHorizontalLeftToRight) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else if (isVerticalBottomToTop && isHorizontalLeftToRight) tempIsBuildableEdgeObjectFlipped = !isBuildableEdgeObjectFlipped;
                    else tempIsBuildableEdgeObjectFlipped = isBuildableEdgeObjectFlipped;

                    PlaceIndividualBuildableEdgeObject(new Vector2Int(x, finalY), buildableEdgeObjectSO, fourDirectionalDirection, tempIsBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, offset, cornerObjectOriginCellDirection,
                        ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages);
                    if (buildableEdgeObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableEdgeObjectSO.intervalBetweenEachPlacement);
                }
            }
        }

        private void PlaceIndividualBuildableEdgeObject(Vector2Int cellPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, FourDirectionalRotation fourDirectionalDirection, bool isBuildableEdgeObjectFlipped, 
            ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, Vector3 offset, CornerObjectCellDirection cornerObjectOriginCellDirection, bool ignoreCustomConditions, bool ignoreReplacement, 
            int verticalGridIndex, bool byPassEventsAndMessages)
        {
            Vector3 closestCornerWorldPosition = GetActiveGridCellWorldPosition(cellPosition);

            ICommand command = new CommandPlaceBuildableEdgeObject(this, cellPosition, closestCornerWorldPosition, offset, buildableEdgeObjectSO, fourDirectionalDirection, 
                isBuildableEdgeObjectFlipped, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions, ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, null);

            gridManager.GetGridCommandInvoker().ExecuteCommand(command);
            BuildableEdgeObject spawnnedBuildableEdgeObject = ((CommandPlaceBuildableEdgeObject)command).GetBuildableEdgeObject();
            buildableObjectSORandomPrefab = ((CommandPlaceBuildableEdgeObject)command).GetBuildableObjectSORandomPrefab();
            if (spawnnedBuildableEdgeObject) gridManager.GetGridCommandInvoker().AddCommand(command);
        }

        public override void InvokeTryPlaceBuildableEdgeObjectSinglePlacement(Vector2Int buildableObjectOriginCellPosition, BuildableEdgeObjectSO buildableEdgeObjectSO, 
            FourDirectionalRotation fourDirectionalDirection, bool isBuildableEdgeObjectFlipped, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, Vector3 cellWorldPosition, 
            CornerObjectCellDirection cornerObjectOriginCellDirection, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
            out BuildableEdgeObject buildableEdgeObject, Vector3 offset = default, BuildableEdgeObject originalBuildableEdgeObject = null)
        {
            int objectLengthRelativeToCellSize = buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(cellSize, buildableObjectSORandomPrefab);
            Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary = GetEdgeObjectCellPositionDictionary(new CellPositionXZ(buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.y), 
                cornerObjectOriginCellDirection, fourDirectionalDirection, objectLengthRelativeToCellSize);

            if (TryPlaceBuildableEdgeObjectSinglePlacement(cellPositionsDictionary, cellWorldPosition, offset, buildableEdgeObjectSO, cornerObjectOriginCellDirection, fourDirectionalDirection, 
                isBuildableEdgeObjectFlipped,  ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, 
                out buildableEdgeObject, originalBuildableEdgeObject))
            {
                RaiseBuildableObjectPlacementEventsAndMessages(this, buildableEdgeObject, byPassEventsAndMessages);
            }
            else
            {
                if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectPlacement)
                {
                    string message = default;
                    bool isWithinGridBounds = true;
                    foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
                    {
                        if (!IsWithinGridBounds(cellPosition.Key, activeVerticalGridIndex)) isWithinGridBounds = false;
                    }
                    
                    message = isWithinGridBounds ? $"Grid Position Is Occupied!" : "Out of Grid Bounds!";
                    Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Cannot Build Here!</b></color> {message}");
                }
            }
        }

        public Dictionary<CellPositionXZ, EdgeObjectCellDirection> GetEdgeObjectCellPositionDictionary(CellPositionXZ buildableObjectOriginCellPosition, CornerObjectCellDirection originCornerObjectCellDirection,
        FourDirectionalRotation fourDirectionalDirection, int objectLengthRelativeToCellSize)
        {
            Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary = new Dictionary<CellPositionXZ, EdgeObjectCellDirection>();

            for (int i = 0; i < objectLengthRelativeToCellSize; i++)
            {
                if (originCornerObjectCellDirection == CornerObjectCellDirection.NorthEast) HandleEdgeObjectNorthEast(cellPositionsDictionary, buildableObjectOriginCellPosition, fourDirectionalDirection, i);
                else if (originCornerObjectCellDirection == CornerObjectCellDirection.SouthEast) HandleEdgeObjectSouthEast(cellPositionsDictionary, buildableObjectOriginCellPosition, fourDirectionalDirection, i);
                else if (originCornerObjectCellDirection == CornerObjectCellDirection.SouthWest) HandleEdgeObjectSouthWest(cellPositionsDictionary, buildableObjectOriginCellPosition, fourDirectionalDirection, i);
                else if (originCornerObjectCellDirection == CornerObjectCellDirection.NorthWest) HandleEdgeObjectNorthWest(cellPositionsDictionary, buildableObjectOriginCellPosition, fourDirectionalDirection, i);
            }
            return cellPositionsDictionary;
        }

        private void HandleEdgeObjectNorthEast(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, CellPositionXZ buildableObjectOriginCellPosition, FourDirectionalRotation fourDirectionalDirection, int i)
        {
            switch (fourDirectionalDirection)
            {
                case FourDirectionalRotation.North:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z + 1 + i, EdgeObjectCellDirection.East);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1, buildableObjectOriginCellPosition.z + 1 + i, EdgeObjectCellDirection.West);
                    break;
                case FourDirectionalRotation.East:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1 + i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.North);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1 + i, buildableObjectOriginCellPosition.z + 1, EdgeObjectCellDirection.South);
                    break;
                case FourDirectionalRotation.South:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z - i, EdgeObjectCellDirection.East);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1, buildableObjectOriginCellPosition.z - i, EdgeObjectCellDirection.West);
                    break;
                case FourDirectionalRotation.West:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.North);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - i, buildableObjectOriginCellPosition.z + 1, EdgeObjectCellDirection.South);
                    break;
            }
        }

        private void HandleEdgeObjectSouthEast(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, CellPositionXZ buildableObjectOriginCellPosition, FourDirectionalRotation fourDirectionalDirection, int i)
        {
            switch (fourDirectionalDirection)
            {
                case FourDirectionalRotation.North:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z + i, EdgeObjectCellDirection.East);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1, buildableObjectOriginCellPosition.z + i, EdgeObjectCellDirection.West);
                    break;
                case FourDirectionalRotation.East:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1 + i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.South);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1 + i, buildableObjectOriginCellPosition.z - 1, EdgeObjectCellDirection.North);
                    break;
                case FourDirectionalRotation.South:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z - 1 - i, EdgeObjectCellDirection.East);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + 1, buildableObjectOriginCellPosition.z - 1 - i, EdgeObjectCellDirection.West);
                    break;
                case FourDirectionalRotation.West:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.South);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - i, buildableObjectOriginCellPosition.z - 1, EdgeObjectCellDirection.North);
                    break;
            }
        }

        private void HandleEdgeObjectSouthWest(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, CellPositionXZ buildableObjectOriginCellPosition, FourDirectionalRotation fourDirectionalDirection, int i)
        {
            switch (fourDirectionalDirection)
            {
                case FourDirectionalRotation.North:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z + i, EdgeObjectCellDirection.West);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1, buildableObjectOriginCellPosition.z + i, EdgeObjectCellDirection.East);
                    break;
                case FourDirectionalRotation.East:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.South);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + i, buildableObjectOriginCellPosition.z - 1, EdgeObjectCellDirection.North);
                    break;
                case FourDirectionalRotation.South:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z - 1 - i, EdgeObjectCellDirection.West);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1, buildableObjectOriginCellPosition.z - 1 - i, EdgeObjectCellDirection.East);
                    break;
                case FourDirectionalRotation.West:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1 - i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.South);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1 - i, buildableObjectOriginCellPosition.z - 1, EdgeObjectCellDirection.North);
                    break;
            }
        }

        private void HandleEdgeObjectNorthWest(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, CellPositionXZ buildableObjectOriginCellPosition, FourDirectionalRotation fourDirectionalDirection, int i)
        {
            switch (fourDirectionalDirection)
            {
                case FourDirectionalRotation.North:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z + 1 + i, EdgeObjectCellDirection.West);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1, buildableObjectOriginCellPosition.z + 1 + i, EdgeObjectCellDirection.East);
                    break;
                case FourDirectionalRotation.East:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.North);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x + i, buildableObjectOriginCellPosition.z + 1, EdgeObjectCellDirection.South);
                    break;
                case FourDirectionalRotation.South:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.z - i, EdgeObjectCellDirection.West);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1, buildableObjectOriginCellPosition.z - i, EdgeObjectCellDirection.East);
                    break;
                case FourDirectionalRotation.West:
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1 - i, buildableObjectOriginCellPosition.z, EdgeObjectCellDirection.North);
                    AddEdgeObjectCellPosition(cellPositionsDictionary, buildableObjectOriginCellPosition.x - 1 - i, buildableObjectOriginCellPosition.z + 1, EdgeObjectCellDirection.South);
                    break;
            }
        }

        private void AddEdgeObjectCellPosition(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, int x, int z, EdgeObjectCellDirection direction)
        {
            cellPositionsDictionary[new CellPositionXZ(x, z)] = direction;
        }

        private bool TryPlaceBuildableEdgeObjectSinglePlacement(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, Vector3 modifiedCellWorldPosition, Vector3 offset,
            BuildableEdgeObjectSO buildableEdgeObjectSO, CornerObjectCellDirection cornerObjectOriginCellDirection, FourDirectionalRotation fourDirectionalDirection, bool isBuildableEdgeObjectFlipped, 
            ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
            out BuildableEdgeObject buildableEdgeObject, BuildableEdgeObject originalBuildableEdgeObject = null)
        {
            bool isWithinGridBounds = false;
            bool isBuildable = true;
            bool isObjectReplacing = false;

            foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                List<CellPositionXZ> adjacentCellPositions =  new List<CellPositionXZ>();

                if (gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition.Key))
                {
                    isWithinGridBounds = true;
                    foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> edgeCellDirection in cellPositionsDictionary)
                    {
                        if (edgeCellDirection.Value == cellPosition.Value) adjacentCellPositions.Add(edgeCellDirection.Key);
                    }
                }

                foreach (CellPositionXZ adjacentCellPosition in adjacentCellPositions)
                {
                    if (gridList[activeVerticalGridIndex].IsWithinGridBounds(adjacentCellPosition) == false)
                    {
                        isWithinGridBounds = false;
                        goto BreakNestedLoops;
                    }
                }
            }
            BreakNestedLoops:

            if (!isWithinGridBounds) isBuildable = false;

            if (isWithinGridBounds)
            {
                foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
                {
                    if (!gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition.Key)) continue;

                    switch (cellPosition.Value)
                    {
                        case EdgeObjectCellDirection.North: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectNorthData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectNorthData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                        case EdgeObjectCellDirection.East: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectEastData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectEastData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                            }
                        break;
                        case EdgeObjectCellDirection.South: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectSouthData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectSouthData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                        case EdgeObjectCellDirection.West: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectWestData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableEdgeObjectWestData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                    }
                }
            }

            // Add Later
            if (isObjectReplacing){}

            if (isWithinGridBounds && isBuildable)
            {
                foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
                {
                    if (buildableEdgeObjectSO.affectByBasicAreaEnablers && IsBuildableObjectEnabledByBasicGridAreaEnablers())
                    {
                        isBuildable = true;
                        continue;
                    }
                    if (buildableEdgeObjectSO.affectByAreaEnablers && IsEdgeObjectEnabledByGridAreaEnablers(gridList[activeVerticalGridIndex], buildableEdgeObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value))
                    {
                        isBuildable = true;
                        continue;
                    }

                    if (buildableEdgeObjectSO.affectByBasicAreaDisablers && IsBuildableObjectDisabledByBasicGridAreaDisablers())
                    {
                        isBuildable = false;
                        break;
                    }
                    if (buildableEdgeObjectSO.affectByAreaDisablers && IsEdgeObjectDisabledByGridAreaDisablers(gridList[activeVerticalGridIndex], buildableEdgeObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value))
                    {
                        isBuildable = false;
                        break;
                    }
                }
            }

            if (isBuildable)
            {
                InitializeBuildableEdgeObjectPlacement(cellPositionsDictionary, modifiedCellWorldPosition, offset, buildableEdgeObjectSO, cornerObjectOriginCellDirection, fourDirectionalDirection, 
                    isBuildableEdgeObjectFlipped, ref buildableObjectSORandomPrefab, false, activeVerticalGridIndex, byPassEventsAndMessages, out buildableEdgeObject, 
                    originalBuildableEdgeObject);
                return true;
            }
            else
            {
                buildableEdgeObject = null;
                return false;
            }
        }

        private void InitializeBuildableEdgeObjectPlacement(Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary, Vector3 worldPosition, Vector3 offset, BuildableEdgeObjectSO buildableEdgeObjectSO, 
            CornerObjectCellDirection cornerObjectOriginCellDirection, FourDirectionalRotation fourDirectionalDirection, bool isBuildableEdgeObjectFlipped, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, 
            bool ignoreBuildConditions, int activeVerticalGridIndex, bool byPassEventsAndMessages, out BuildableEdgeObject buildableEdgeObject, BuildableEdgeObject originalBuildableEdgeObject = null)
        {
            if (!originalBuildableEdgeObject) buildableEdgeObject = Instantiate(buildableObjectSORandomPrefab.objectPrefab, Vector3.zero, Quaternion.identity).GetComponent<BuildableEdgeObject>();
            else buildableEdgeObject = originalBuildableEdgeObject;
            
            List<Vector2Int> cellPositionList = new List<Vector2Int>();
            Dictionary<Vector2Int, EdgeObjectCellDirection> newCellPositionsDictionary = new Dictionary<Vector2Int, EdgeObjectCellDirection>();
            foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                cellPositionList.Add(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z));
                newCellPositionsDictionary[new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)] = cellPosition.Value;
            }
            
            buildableEdgeObject.SetupBuildableEdgeObject(this, activeVerticalGridIndex, cellSize, buildableEdgeObjectSO, buildableObjectSORandomPrefab, worldPosition, offset,
                cellPositionList, newCellPositionsDictionary, cornerObjectOriginCellDirection, fourDirectionalDirection, isBuildableEdgeObjectFlipped);

            if (buildableEdgeObjectSO.eachPlacementUseRandomPrefab) 
            {
                buildableObjectSORandomPrefab = GetUpdatedActiveBuildableObjectSORandomPrefab(buildableEdgeObjectSO, out _);
                if (buildableEdgeObjectSO.intervalBetweenEachPlacement <= 0) UpdateActiveBuildableObjectSORandomPrefab(buildableEdgeObjectSO);
            }

            if (!ignoreBuildConditions)
            {
                // if (buildableGridObjectSO.enableBuildConditions)
                // {
                //     CompleteBuildConditionBuildableGridObject(buildableGridObjectSO);
                // }
            }

            foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (!gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition.Key)) continue;
                GridCellData gridCellData = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z);      // Get the GridCellData Copy
                Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjects = default;

                switch (cellPosition.Value)
                {
                    case EdgeObjectCellDirection.North: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectNorthData(); break;   // Initialize the dictionary
                    case EdgeObjectCellDirection.East: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectEastData(); break;     // Initialize the dictionary
                    case EdgeObjectCellDirection.South: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectSouthData(); break;   // Initialize the dictionary
                    case EdgeObjectCellDirection.West: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectWestData(); break;     // Initialize the dictionary
                }

                buildableEdgeObjects[buildableEdgeObjectSO.buildableEdgeObjectCategorySO] = buildableEdgeObject;                        // Modify the data
                gridList[activeVerticalGridIndex].SetCellData(cellPosition.Key.x, cellPosition.Key.z, gridCellData);                    // Write the modified GridCellData back to the original
                if (!byPassEventsAndMessages) RaiseActiveBuildableSOChangeEventsAndMessages(this, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z));
            }
        }

        private void StartEdgeObjectBoxPlacement(Vector3 worldPosition, EdgeObjectPlacementType placementType)
        {
            boxPlacementStartPosition = worldPosition;
            RaiseOnEdgeObjectBoxPlacementStartedEvent(this, boxPlacementStartPosition, placementType);
        }

        private void UpdateEdgeObjectBoxPlacement()
        {
            LayerMask layerMask = gridManager.GetGridSystemLayerMask();

            boxPlacementEndPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(activeBuildableObjectSO.customSurfaceLayerMask, layerMask, Vector3.down * 9999,
                out _, out Vector3 firstCollisionWorldPosition);
            boxPlacementEndPosition = new Vector3(boxPlacementEndPosition.x, firstCollisionWorldPosition.y, boxPlacementEndPosition.z);
            
            RaiseOnEdgeObjectBoxPlacementUpdatedEvent(this, boxPlacementEndPosition);
        }

        private void FinalizeEdgeObjectBoxPlacement()
        {
            activeBuildableEdgeObjectSOPlacementType = EdgeObjectPlacementType.SinglePlacement;
            RaiseOnEdgeObjectBoxPlacementFinalizedEvent(this);
        }

        private void CancelEdgeObjectBoxPlacement()
        {
            activeBuildableEdgeObjectSOPlacementType = EdgeObjectPlacementType.SinglePlacement;
            RaiseOnEdgeObjectBoxPlacementCancelledEvent(this);
        }
        #endregion Setup Buildable Edge Object Placement Functions End:

        #region Setup Buildable Corner Object Placement Functions Start:
        public override bool TryInitializeBuildableCornerObjectSinglePlacement(Vector3 worldPosition, BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, 
            out BuildableCornerObject spawnnedBuildableCornerObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableCornerObject originalBuildableCornerObject = null)
        {
            buildableObjectSORandomPrefab = buildableObjectSORandomPrefab ?? GetUpdatedActiveBuildableObjectSORandomPrefab(buildableCornerObjectSO, out _);
            Vector2Int buildableObjectOriginCellPosition = GetCellPosition(worldPosition, verticalGridIndex);
            Vector3 closestCornerWorldPosition = GetClosestCorner(worldPosition, GetActiveGridCellWorldPosition(buildableObjectOriginCellPosition), out CornerObjectCellDirection cornerObjectOriginCellDirection);
            
            if (!isObjectMoving)
            {
                ICommand command = new CommandPlaceBuildableCornerObject(this, buildableObjectOriginCellPosition, closestCornerWorldPosition, default, buildableCornerObjectSO, fourDirectionalDirection, 
                    eightDirectionalDirection, freeRotation, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions,
                    ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, true, originalBuildableCornerObject);

                gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                spawnnedBuildableCornerObject = ((CommandPlaceBuildableCornerObject)command).GetBuildableCornerObject();
                buildableObjectSORandomPrefab = ((CommandPlaceBuildableCornerObject)command).GetBuildableObjectSORandomPrefab();
                if (spawnnedBuildableCornerObject) gridManager.GetGridCommandInvoker().AddCommand(command);
            }
            else
            {
                InvokeTryPlaceBuildableCornerObjectSinglePlacement(buildableObjectOriginCellPosition, buildableCornerObjectSO, fourDirectionalDirection, 
                    eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, closestCornerWorldPosition, cornerObjectOriginCellDirection, ignoreCustomConditions, ignoreReplacement, 
                    verticalGridIndex, byPassEventsAndMessages, false, out spawnnedBuildableCornerObject, originalBuildableCornerObject: originalBuildableCornerObject);
            }

            return spawnnedBuildableCornerObject != null;
        }

        private void SetupBuildableCornerObjectPlacement(BuildableCornerObjectSO activeBuildableCornerObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, int activeVerticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement = false)
        {
            bool ignoreBuildConditions = isObjectMoving ? true : false;
            bool ignoreReplacement = isObjectMoving ? true : false;
            byPassEventsAndMessages = isObjectMoving ? true : byPassEventsAndMessages;

            if (!IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                InitializeBuildableCornerObjectSinglePlacement(worldPosition, activeBuildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, 
                    byPassEventsAndMessages, invokedAsSecondaryPlacement);
            }
            else
            {
                switch (activeBuildableCornerObjectSOPlacementType)
                {
                    case CornerObjectPlacementType.BoxPlacement:
                        InitializeBuildableCornerObjectBoxPlacement(activeBuildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, 
                            byPassEventsAndMessages, invokedAsSecondaryPlacement);
                    break;
                    case CornerObjectPlacementType.WireBoxPlacement:
                        InitializeBuildableCornerObjectWireBoxPlacement(activeBuildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex,
                            byPassEventsAndMessages, invokedAsSecondaryPlacement);
                    break;
                    case CornerObjectPlacementType.FourDirectionWirePlacement:
                        InitializeBuildableCornerObjectFourDirectionWirePlacement(activeBuildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex,
                            byPassEventsAndMessages, invokedAsSecondaryPlacement);
                    break;
                    case CornerObjectPlacementType.LShapedPlacement:
                        InitializeBuildableCornerObjectLShapedPlacement(activeBuildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex,
                            byPassEventsAndMessages, invokedAsSecondaryPlacement);
                    break;
                }
            }
        }

        private void InitializeBuildableCornerObjectSinglePlacement(Vector3 worldPosition, BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = invokedAsSecondaryPlacement ? activeSecondaryBuildableObjectSORandomPrefab : activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector2Int buildableObjectOriginCellPosition = GetCellPosition(worldPosition, verticalGridIndex);
            Vector3 closestCornerWorldPosition = GetClosestCorner(worldPosition, GetActiveGridCellWorldPosition(buildableObjectOriginCellPosition), out CornerObjectCellDirection cornerObjectOriginCellDirection);
            
            BuildableCornerObject originalBuildableCornerObject = null;

            if (isObjectMoving && movingBuildableObject)
            {
                originalBuildableCornerObject = (BuildableCornerObject)movingBuildableObject;
                buildableObjectSORandomPrefab = movingBuildableObject.GetBuildableObjectSORandomPrefab();
            }

            ICommand command = new CommandPlaceBuildableCornerObject(this, buildableObjectOriginCellPosition, closestCornerWorldPosition, default, buildableCornerObjectSO, fourDirectionalDirection, 
                eightDirectionalDirection, freeRotation, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions,
                ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement, originalBuildableCornerObject);

            gridManager.GetGridCommandInvoker().ExecuteCommand(command);
            BuildableCornerObject spawnnedBuildableEdgeObject = ((CommandPlaceBuildableCornerObject)command).GetBuildableCornerObject();
            buildableObjectSORandomPrefab = ((CommandPlaceBuildableCornerObject)command).GetBuildableObjectSORandomPrefab();
            if (spawnnedBuildableEdgeObject) gridManager.GetGridCommandInvoker().AddCommand(command);
        }

        private void InitializeBuildableCornerObjectBoxPlacement(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableCornerBoxPlacementObjects(buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement, 
                activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages, invokedAsSecondaryPlacement));
        }

        private void InitializeBuildableCornerObjectWireBoxPlacement(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableCornerWireBoxPlacementObjects(buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement,
                activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages, invokedAsSecondaryPlacement));
        }

        private void InitializeBuildableCornerObjectFourDirectionWirePlacement(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableCornerWirePlacementObjects(buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement,
                activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages, invokedAsSecondaryPlacement));
        }

        private void InitializeBuildableCornerObjectLShapedPlacement(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            Vector2Int startCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementStartPosition).x, GetActiveGridCellPositionXZ(boxPlacementStartPosition).z);
            Vector2Int endCell = new Vector2Int(GetActiveGridCellPositionXZ(boxPlacementEndPosition).x, GetActiveGridCellPositionXZ(boxPlacementEndPosition).z);
            StartCoroutine(PlaceBuildableCornerLShapedPlacementObjects(buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ignoreBuildConditions, ignoreReplacement,
                activeVerticalGridIndex, startCell, endCell, byPassEventsAndMessages, invokedAsSecondaryPlacement));
        }

        private IEnumerator PlaceBuildableCornerBoxPlacementObjects(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, Vector2Int startCell, Vector2Int endCell, 
            bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = invokedAsSecondaryPlacement ? activeSecondaryBuildableObjectSORandomPrefab : activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection);

            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minY; z <= maxY; z++)
                {
                    Vector2Int buildableObjectOriginCellPosition = new Vector2Int(x, z);
                    Vector3 cellWorldPosition = GetActiveGridCellWorldPosition(buildableObjectOriginCellPosition);

                    ICommand command = new CommandPlaceBuildableCornerObject(this, buildableObjectOriginCellPosition, cellWorldPosition, offset, buildableCornerObjectSO, fourDirectionalDirection, 
                        eightDirectionalDirection, freeRotation, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions,
                        ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement, null);

                    gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                    BuildableCornerObject spawnnedBuildableCornerObject = ((CommandPlaceBuildableCornerObject)command).GetBuildableCornerObject();
                    buildableObjectSORandomPrefab = ((CommandPlaceBuildableCornerObject)command).GetBuildableObjectSORandomPrefab();
                    if (spawnnedBuildableCornerObject) gridManager.GetGridCommandInvoker().AddCommand(command);

                    if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                }
            }
        }

        private IEnumerator PlaceBuildableCornerWireBoxPlacementObjects(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, 
            bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = invokedAsSecondaryPlacement ? activeSecondaryBuildableObjectSORandomPrefab : activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectStartOriginCellDirection);

            int minX = Mathf.Min(startCell.x, endCell.x);
            int maxX = Mathf.Max(startCell.x, endCell.x);
            int minY = Mathf.Min(startCell.y, endCell.y);
            int maxY = Mathf.Max(startCell.y, endCell.y);

            if (buildableCornerObjectSO.spawnOnlyAtEndPoints)
            {
                // Place objects only at the four corners
                PlaceIndividualBuildableCornerObject(new Vector2Int(minX, minY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Top-left corner
                if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);

                PlaceIndividualBuildableCornerObject(new Vector2Int(maxX, minY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Top-right corner
                if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                
                PlaceIndividualBuildableCornerObject(new Vector2Int(minX, maxY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Bottom-left corner
                if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);

                PlaceIndividualBuildableCornerObject(new Vector2Int(maxX, maxY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Bottom-right corner
            }
            else
            {
                // Original placement logic for full border
                for (int x = minX; x <= maxX; x++)
                {
                    PlaceIndividualBuildableCornerObject(new Vector2Int(x, minY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Top border
                    PlaceIndividualBuildableCornerObject(new Vector2Int(x, maxY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Bottom border
                    if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                }

                for (int y = minY + 1; y < maxY; y++)
                {
                    PlaceIndividualBuildableCornerObject(new Vector2Int(minX, y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Left border
                    PlaceIndividualBuildableCornerObject(new Vector2Int(maxX, y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectStartOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement); // Right border
                    if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                }
            }
        }

        private IEnumerator PlaceBuildableCornerWirePlacementObjects(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, 
            bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = invokedAsSecondaryPlacement ? activeSecondaryBuildableObjectSORandomPrefab : activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection);

            int dx = Mathf.Abs(endCell.x - startCell.x);
            int dy = Mathf.Abs(endCell.y - startCell.y);
            bool isHorizontal = dx >= dy;

            if (buildableCornerObjectSO.spawnOnlyAtEndPoints)
            {
                if (isHorizontal)
                {
                    // Place only at the start and end points on the horizontal line
                    PlaceIndividualBuildableCornerObject(new Vector2Int(startCell.x, startCell.y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                    if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);

                    PlaceIndividualBuildableCornerObject(new Vector2Int(endCell.x, startCell.y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                }
                else
                {
                    // Place only at the start and end points on the vertical line
                    PlaceIndividualBuildableCornerObject(new Vector2Int(startCell.x, startCell.y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                    if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);

                    PlaceIndividualBuildableCornerObject(new Vector2Int(startCell.x, endCell.y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                        offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                }
            }
            else
            {
                // Original placement logic for full line
                if (isHorizontal)
                {
                    int minX = Mathf.Min(startCell.x, endCell.x);
                    int maxX = Mathf.Max(startCell.x, endCell.x);
                    for (int x = minX; x <= maxX; x++)
                    {
                        PlaceIndividualBuildableCornerObject(new Vector2Int(x, startCell.y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                            offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                        if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                    }
                }
                else
                {
                    int minY = Mathf.Min(startCell.y, endCell.y);
                    int maxY = Mathf.Max(startCell.y, endCell.y);
                    for (int y = minY; y <= maxY; y++)
                    {
                        PlaceIndividualBuildableCornerObject(new Vector2Int(startCell.x, y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                            offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                        if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                    }
                }
            }
        }

        private IEnumerator PlaceBuildableCornerLShapedPlacementObjects(BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, Vector2Int startCell, Vector2Int endCell, 
            bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = invokedAsSecondaryPlacement ? activeSecondaryBuildableObjectSORandomPrefab : activeBuildableObjectSORandomPrefab; //Caching the "activeBuildableObjectSORandomPrefab"
            Vector3 offset = GetBuildableCornerObjectOffsetForBoxPlacementTypes(startCell, boxPlacementStartPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection);

            int dx = Mathf.Abs(endCell.x - startCell.x);
            int dy = Mathf.Abs(endCell.y - startCell.y);
            bool isHorizontalFirst = dx >= dy;

            if (buildableCornerObjectSO.spawnOnlyAtEndPoints)
            {
                // Place at the start point
                PlaceIndividualBuildableCornerObject(startCell, buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);

                // Place at the bend point
                Vector2Int bendPoint = isHorizontalFirst ? new Vector2Int(endCell.x, startCell.y) : new Vector2Int(startCell.x, endCell.y);
                PlaceIndividualBuildableCornerObject(bendPoint, buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);

                // Place at the end point
                PlaceIndividualBuildableCornerObject(endCell, buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                    offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
            }
            else
            {
                // Original placement logic for full L-shape
                if (isHorizontalFirst)
                {
                    int minX = Mathf.Min(startCell.x, endCell.x);
                    int maxX = Mathf.Max(startCell.x, endCell.x);
                    for (int x = minX; x <= maxX; x++)
                    {
                        PlaceIndividualBuildableCornerObject(new Vector2Int(x, startCell.y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                            offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                        if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                    }

                    int finalX = endCell.x; // The point where the horizontal and vertical parts meet
                    int minY = Mathf.Min(startCell.y, endCell.y);
                    int maxY = Mathf.Max(startCell.y, endCell.y);
                    for (int y = minY; y <= maxY; y++)
                    {
                        PlaceIndividualBuildableCornerObject(new Vector2Int(finalX, y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                            offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                        if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                    }
                }
                else
                {
                    int minY = Mathf.Min(startCell.y, endCell.y);
                    int maxY = Mathf.Max(startCell.y, endCell.y);
                    for (int y = minY; y <= maxY; y++)
                    {
                        PlaceIndividualBuildableCornerObject(new Vector2Int(startCell.x, y), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, 
                            offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                        if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                    }

                    int finalY = endCell.y; // The point where the horizontal and vertical parts meet
                    int minX = Mathf.Min(startCell.x, endCell.x);
                    int maxX = Mathf.Max(startCell.x, endCell.x);
                    for (int x = minX; x <= maxX; x++)
                    {
                        PlaceIndividualBuildableCornerObject(new Vector2Int(x, finalY), buildableCornerObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab,
                            offset, cornerObjectOriginCellDirection, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement);
                        if (buildableCornerObjectSO.intervalBetweenEachPlacement > 0) yield return new WaitForSeconds(buildableCornerObjectSO.intervalBetweenEachPlacement);
                    }
                }
            }
        }

        private void PlaceIndividualBuildableCornerObject(Vector2Int cellPosition, BuildableCornerObjectSO buildableCornerObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, Vector3 offset, CornerObjectCellDirection cornerObjectOriginCellDirection, 
            bool ignoreCustomConditions, bool ignoreReplacement, int verticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement)
        {
            Vector3 cellWorldPosition = GetActiveGridCellWorldPosition(cellPosition);

            ICommand command = new CommandPlaceBuildableCornerObject(this, cellPosition, cellWorldPosition, offset, buildableCornerObjectSO, fourDirectionalDirection, 
                eightDirectionalDirection, freeRotation, cornerObjectOriginCellDirection, buildableObjectSORandomPrefab, ignoreCustomConditions,
                ignoreReplacement, verticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement, null);

            gridManager.GetGridCommandInvoker().ExecuteCommand(command);
            BuildableCornerObject spawnnedBuildableCornerObject = ((CommandPlaceBuildableCornerObject)command).GetBuildableCornerObject();
            buildableObjectSORandomPrefab = ((CommandPlaceBuildableCornerObject)command).GetBuildableObjectSORandomPrefab();
            if (spawnnedBuildableCornerObject) gridManager.GetGridCommandInvoker().AddCommand(command);
        }

        public override void InvokeTryPlaceBuildableCornerObjectSinglePlacement(Vector2Int buildableObjectOriginCellPosition, BuildableCornerObjectSO buildableCornerObjectSO, 
            FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, 
            Vector3 cellWorldPosition, CornerObjectCellDirection cornerObjectOriginCellDirection, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages,
            bool invokedAsSecondaryPlacement, out BuildableCornerObject buildableCornerObject, Vector3 offset = default, BuildableCornerObject originalBuildableCornerObject = null)
        {
            Dictionary<CellPositionXZ, CornerObjectCellDirection> cellPositionsDictionary = GetCornerObjectCellPositionDictionary(new CellPositionXZ(buildableObjectOriginCellPosition.x, buildableObjectOriginCellPosition.y), 
                cornerObjectOriginCellDirection);
            
            if (TryPlaceBuildableCornerObjectSinglePlacement(cellPositionsDictionary, cellWorldPosition, offset, buildableCornerObjectSO, cornerObjectOriginCellDirection, fourDirectionalDirection, 
                eightDirectionalDirection, freeRotation, ref buildableObjectSORandomPrefab, ignoreBuildConditions, ignoreReplacement, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement, 
                out buildableCornerObject, originalBuildableCornerObject))
            {
                RaiseBuildableObjectPlacementEventsAndMessages(this, buildableCornerObject, byPassEventsAndMessages);
            }
            else
            {
                if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectPlacement)
                {
                    string message = default;
                    bool isWithinGridBounds = true;
                    foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
                    {
                        if (!IsWithinGridBounds(cellPosition.Key, activeVerticalGridIndex)) isWithinGridBounds = false;
                    }
                    
                    message = isWithinGridBounds ? $"Grid Position Is Occupied!" : "Out of Grid Bounds!";
                    Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Cannot Build Here!</b></color> {message}");
                }
            }
        }

        private Vector3 GetBuildableCornerObjectOffsetForBoxPlacementTypes(Vector2Int cellPosition, Vector3 boxPlacementWorldPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            Vector3 cellWorldPosition = GetActiveGridCellWorldPosition(cellPosition);
            GetClosestCorner(boxPlacementWorldPosition, cellWorldPosition, out cornerObjectOriginCellDirection);

            Vector3 offset = default;
            switch (cornerObjectOriginCellDirection)
            {
                case CornerObjectCellDirection.NorthEast: offset = new Vector3(cellSize, 0, cellSize); break;
                case CornerObjectCellDirection.SouthEast: offset = new Vector3(cellSize, 0, 0); break;
                case CornerObjectCellDirection.SouthWest: offset = Vector3.zero; break;
                case CornerObjectCellDirection.NorthWest: offset = new Vector3(0, 0, cellSize); break;
            }
            return offset;
        }

        public Vector3 GetClosestCorner(Vector3 worldPosition, Vector3 cellWorldPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            Vector3 lowerLeftCorner = cellWorldPosition;
            Vector3 lowerRightCorner = cellWorldPosition + new Vector3(cellSize, 0, 0);
            Vector3 upperLeftCorner = cellWorldPosition + new Vector3(0, 0, cellSize);
            Vector3 upperRightCorner = cellWorldPosition + new Vector3(cellSize, 0, cellSize);

            // Array of corners and corresponding enum values
            (Vector3 position, CornerObjectCellDirection type)[] corners = 
            {
                (lowerLeftCorner, CornerObjectCellDirection.SouthWest),
                (lowerRightCorner, CornerObjectCellDirection.SouthEast),
                (upperLeftCorner, CornerObjectCellDirection.NorthWest),
                (upperRightCorner, CornerObjectCellDirection.NorthEast)
            };

            return GetNearestCorner(worldPosition, corners, out cornerObjectOriginCellDirection);
        }

        public Vector3 GetClosestCorner(Vector3 cellWorldPosition, CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            // Define the positions of the four corners of the cell
            Vector3 lowerLeftCorner = cellWorldPosition;
            Vector3 lowerRightCorner = cellWorldPosition + new Vector3(cellSize, 0, 0);
            Vector3 upperLeftCorner = cellWorldPosition + new Vector3(0, 0, cellSize);
            Vector3 upperRightCorner = cellWorldPosition + new Vector3(cellSize, 0, cellSize);

            // Return the corner based on the direction passed
            switch (cornerObjectOriginCellDirection)
            {
                case CornerObjectCellDirection.SouthWest: return lowerLeftCorner;
                case CornerObjectCellDirection.SouthEast: return lowerRightCorner;
                case CornerObjectCellDirection.NorthWest: return upperLeftCorner;
                case CornerObjectCellDirection.NorthEast: return upperRightCorner;
                default: throw new System.ArgumentException("Invalid CornerObjectCellDirection provided");
            }
        }

        private Vector3 GetNearestCorner(Vector3 mousePosition, (Vector3 position, CornerObjectCellDirection type)[] corners, out CornerObjectCellDirection cornerObjectPosition)
        {
            (Vector3 position, CornerObjectCellDirection type) nearestCorner = corners[0];
            float minDistance = Vector3.Distance(mousePosition, nearestCorner.position);

            for (int i = 1; i < corners.Length; i++)
            {
                float distance = Vector3.Distance(mousePosition, corners[i].position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestCorner = corners[i];
                }
            }

            cornerObjectPosition = nearestCorner.type;
            return nearestCorner.position;
        }

        public Dictionary<CellPositionXZ, CornerObjectCellDirection> GetCornerObjectCellPositionDictionary(CellPositionXZ originCellPosition, CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            Dictionary<CellPositionXZ, CornerObjectCellDirection> cellPositionsDictionary = new Dictionary<CellPositionXZ, CornerObjectCellDirection>();

            // Add the origin position and its corner
            cellPositionsDictionary.Add(originCellPosition, cornerObjectOriginCellDirection);

            // Calculate and add the other three corners
            switch (cornerObjectOriginCellDirection)
            {
                case CornerObjectCellDirection.NorthEast:
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(0, 1), CornerObjectCellDirection.SouthEast);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(1, 1), CornerObjectCellDirection.SouthWest);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(1, 0), CornerObjectCellDirection.NorthWest);
                    break;

                case CornerObjectCellDirection.SouthEast:
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(0, -1), CornerObjectCellDirection.NorthEast);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(1, -1), CornerObjectCellDirection.NorthWest);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(1, 0), CornerObjectCellDirection.SouthWest);
                    break;

                case CornerObjectCellDirection.SouthWest:
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(0, -1), CornerObjectCellDirection.NorthWest);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(-1, -1), CornerObjectCellDirection.NorthEast);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(-1, 0), CornerObjectCellDirection.SouthEast);
                    break;

                case CornerObjectCellDirection.NorthWest:
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(0, 1), CornerObjectCellDirection.SouthWest);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(-1, 1), CornerObjectCellDirection.SouthEast);
                    cellPositionsDictionary.Add(originCellPosition + new CellPositionXZ(-1, 0), CornerObjectCellDirection.NorthEast);
                    break;
            }

            return cellPositionsDictionary;
        }

        private bool TryPlaceBuildableCornerObjectSinglePlacement(Dictionary<CellPositionXZ, CornerObjectCellDirection> cellPositionsDictionary, Vector3 modifiedCellWorldPosition, Vector3 offset,
            BuildableCornerObjectSO buildableCornerObjectSO, CornerObjectCellDirection cornerObjectOriginCellDirection, FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, 
            float freeRotation, ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreBuildConditions, bool ignoreReplacement, int activeVerticalGridIndex, bool byPassEventsAndMessages, 
            bool invokedAsSecondaryPlacement, out BuildableCornerObject buildableCornerObject, BuildableCornerObject originalBuildableCornerObject = null)
        {
            bool isWithinGridBounds = false;
            bool isBuildable = true;
            bool isObjectReplacing = false;

            foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition.Key))
                {
                    isWithinGridBounds = true;
                    break;
                }
            }
            if (!isWithinGridBounds) isBuildable = false;

            if (isWithinGridBounds)
            {
                foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
                {
                    if (!gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition.Key)) continue;

                    switch (cellPosition.Value)
                    {
                        case CornerObjectCellDirection.NorthEast: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectNorthEastData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectNorthEastData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                        case CornerObjectCellDirection.SouthEast: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectSouthEastData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectSouthEastData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                        case CornerObjectCellDirection.SouthWest: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectSouthWestData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectSouthWestData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                        case CornerObjectCellDirection.NorthWest: 
                            if (gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectNorthWestData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                            {
                                BuildableObject buildableObject = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z).GetBuildableCornerObjectNorthWestData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                                if (!ignoreReplacement && buildableObject.GetBuildableObjectSO().isObjectReplacable && gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
                                {
                                    isObjectReplacing = true;
                                    buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true);
                                }
                                else isBuildable = false;
                            }
                        break;
                    }
                }
            }

            // Add Later
            if (isObjectReplacing){}

            if (isWithinGridBounds && isBuildable)
            {
                foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
                {
                    if (buildableCornerObjectSO.affectByBasicAreaEnablers && IsBuildableObjectEnabledByBasicGridAreaEnablers())
                    {
                        isBuildable = true;
                        continue;
                    }
                    if (buildableCornerObjectSO.affectByAreaEnablers && IsCornerObjectEnabledByGridAreaEnablers(gridList[activeVerticalGridIndex], buildableCornerObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value))
                    {
                        isBuildable = true;
                        continue;
                    }

                    if (buildableCornerObjectSO.affectByBasicAreaDisablers && IsBuildableObjectDisabledByBasicGridAreaDisablers())
                    {
                        isBuildable = false;
                        break;
                    }
                    if (buildableCornerObjectSO.affectByAreaDisablers && IsCornerObjectDisabledByGridAreaDisablers(gridList[activeVerticalGridIndex], buildableCornerObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value))
                    {
                        isBuildable = false;
                        break;
                    }
                }
            }

            if (isBuildable)
            {
                InitializeBuildableCornerObjectPlacement(cellPositionsDictionary, modifiedCellWorldPosition, offset, buildableCornerObjectSO, cornerObjectOriginCellDirection, fourDirectionalDirection, eightDirectionalDirection,
                    freeRotation, ref buildableObjectSORandomPrefab, false, activeVerticalGridIndex, byPassEventsAndMessages, invokedAsSecondaryPlacement, out buildableCornerObject, originalBuildableCornerObject);
                return true;
            }
            else
            {
                buildableCornerObject = null;
                return false;
            }
        }

        private void InitializeBuildableCornerObjectPlacement(Dictionary<CellPositionXZ, CornerObjectCellDirection> cellPositionsDictionary, Vector3 modifiedCellWorldPosition, Vector3 offset, BuildableCornerObjectSO buildableCornerObjectSO, 
            CornerObjectCellDirection cornerObjectOriginCellDirection, FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation,  
            ref BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab, bool ignoreBuildConditions, int activeVerticalGridIndex, bool byPassEventsAndMessages, bool invokedAsSecondaryPlacement, 
            out BuildableCornerObject buildableCornerObject, BuildableCornerObject originalBuildableCornerObject = null)
        {
            if (!originalBuildableCornerObject) buildableCornerObject = Instantiate(buildableObjectSORandomPrefab.objectPrefab, Vector3.zero, Quaternion.identity).GetComponent<BuildableCornerObject>();
            else buildableCornerObject = originalBuildableCornerObject;
            
            List<Vector2Int> cellPositionList = new List<Vector2Int>();
            Dictionary<Vector2Int, CornerObjectCellDirection> newCellPositionsDictionary = new Dictionary<Vector2Int, CornerObjectCellDirection>();
            foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                cellPositionList.Add(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z));
                newCellPositionsDictionary[new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)] = cellPosition.Value;
            }
            
            buildableCornerObject.SetupBuildableCornerObject(this, activeVerticalGridIndex, cellSize, buildableCornerObjectSO, buildableObjectSORandomPrefab, modifiedCellWorldPosition, offset, cellPositionList,
                newCellPositionsDictionary, cornerObjectOriginCellDirection, fourDirectionalDirection, eightDirectionalDirection, freeRotation, invokedAsSecondaryPlacement);

            if (buildableCornerObjectSO.eachPlacementUseRandomPrefab) buildableObjectSORandomPrefab = GetUpdatedActiveBuildableObjectSORandomPrefab(buildableCornerObjectSO, out _);

            if (!ignoreBuildConditions)
            {
                // if (buildableGridObjectSO.enableBuildConditions)
                // {
                //     CompleteBuildConditionBuildableGridObject(buildableGridObjectSO);
                // }
            }

            foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (!gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition.Key)) continue;
                GridCellData gridCellData = gridList[activeVerticalGridIndex].GetCellData(cellPosition.Key.x, cellPosition.Key.z);                      // Get the GridCellData Copy
                Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjects = default;

                switch (cellPosition.Value)
                {
                    case CornerObjectCellDirection.NorthEast: buildableCornerObjects = gridCellData.GetBuildableCornerObjectNorthEastData(); break;     // Initialize the dictionary
                    case CornerObjectCellDirection.SouthEast: buildableCornerObjects = gridCellData.GetBuildableCornerObjectSouthEastData(); break;     // Initialize the dictionary
                    case CornerObjectCellDirection.SouthWest: buildableCornerObjects = gridCellData.GetBuildableCornerObjectSouthWestData(); break;     // Initialize the dictionary
                    case CornerObjectCellDirection.NorthWest: buildableCornerObjects = gridCellData.GetBuildableCornerObjectNorthWestData(); break;     // Initialize the dictionary
                }

                buildableCornerObjects[buildableCornerObjectSO.buildableCornerObjectCategorySO] = buildableCornerObject;                                // Modify the data
                gridList[activeVerticalGridIndex].SetCellData(cellPosition.Key.x, cellPosition.Key.z, gridCellData);                                    // Write the modified GridCellData back to the original
                if (!byPassEventsAndMessages) RaiseActiveBuildableSOChangeEventsAndMessages(this, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z));
            }
        }

        private void StartCornerObjectBoxPlacement(Vector3 worldPosition, CornerObjectPlacementType placementType)
        {
            boxPlacementStartPosition = worldPosition;
            RaiseOnCornerObjectBoxPlacementStartedEvent(this, boxPlacementStartPosition, placementType);
        }

        private void UpdateCornerObjectBoxPlacement()
        {
            LayerMask layerMask = gridManager.GetGridSystemLayerMask();

            boxPlacementEndPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(activeBuildableObjectSO.customSurfaceLayerMask, layerMask, Vector3.down * 9999,
                out _, out Vector3 firstCollisionWorldPosition);
            boxPlacementEndPosition = new Vector3(boxPlacementEndPosition.x, firstCollisionWorldPosition.y, boxPlacementEndPosition.z);
            
            RaiseOnCornerObjectBoxPlacementUpdatedEvent(this, boxPlacementEndPosition);
        }

        private void FinalizeCornerObjectBoxPlacement()
        {
            activeBuildableCornerObjectSOPlacementType = CornerObjectPlacementType.SinglePlacement;
            RaiseOnCornerObjectBoxPlacementFinalizedEvent(this);
        }

        private void CancelCornerObjectBoxPlacement()
        {
            activeBuildableCornerObjectSOPlacementType = CornerObjectPlacementType.SinglePlacement;
            RaiseOnCornerObjectBoxPlacementCancelledEvent(this);
        }
        #endregion Setup Buildable Corner Object Placement Functions End:

        #region Setup Buildable Free Object Placement Functions Start:
        public override bool TryInitializeBuildableFreeObjectSinglePlacement(Vector3 worldPosition, BuildableFreeObjectSO buildableFreeObjectSO, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, bool ignoreCustomConditions, int verticalGridIndex, bool byPassEventsAndMessages, 
            out BuildableFreeObject spawnnedBuildableFreeObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, BuildableFreeObject originalBuildableFreeObject = null)
        {
            ignoreCustomConditions = isObjectMoving ? true : ignoreCustomConditions;
            byPassEventsAndMessages = isObjectMoving ? true : byPassEventsAndMessages;

            buildableObjectSORandomPrefab = buildableObjectSORandomPrefab ?? GetUpdatedActiveBuildableObjectSORandomPrefab(buildableFreeObjectSO, out _);
            UpdateActiveBuildableObjectSORandomPrefab(buildableFreeObjectSO);

            if (!isObjectMoving)
            {
                ICommand command = new CommandPlaceBuildableFreeObject(this, worldPosition, buildableFreeObjectSO, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals,
                    buildableObjectSORandomPrefab, ignoreCustomConditions, verticalGridIndex, byPassEventsAndMessages, originalBuildableFreeObject);

                gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                spawnnedBuildableFreeObject = ((CommandPlaceBuildableFreeObject)command).GetBuildableFreeObject();
                buildableObjectSORandomPrefab = ((CommandPlaceBuildableFreeObject)command).GetBuildableObjectSORandomPrefab();
                if (spawnnedBuildableFreeObject) gridManager.GetGridCommandInvoker().AddCommand(command);
            }
            else
            {
                InvokeTryPlaceBuildableFreeObjectSinglePlacement(buildableFreeObjectSO, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals, ignoreCustomConditions, verticalGridIndex, 
                    byPassEventsAndMessages, out spawnnedBuildableFreeObject, buildableObjectSORandomPrefab, originalBuildableFreeObject);
            }

            return spawnnedBuildableFreeObject != null;
        }

        public override void InvokeTryPlaceBuildableFreeObjectSinglePlacement(BuildableFreeObjectSO buildableFreeObjectSO, Vector3 worldPosition, FourDirectionalRotation fourDirectionalDirection, 
            EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, bool ignoreBuildConditions, 
            int activeVerticalGridIndex, bool byPassEventsAndMessages, out BuildableFreeObject buildableFreeObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null, 
            BuildableFreeObject originalBuildableFreeObject = null)
        {
            CellPositionXZ cellPosition = GetActiveGridCellPositionXZ(worldPosition);
            if (TryPlaceBuildableFreeObjectSinglePlacement(buildableFreeObjectSO, cellPosition, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals, 
                ignoreBuildConditions, activeVerticalGridIndex, byPassEventsAndMessages, out buildableFreeObject, buildableObjectSORandomPrefab, originalBuildableFreeObject))
            {
                RaiseBuildableObjectPlacementEventsAndMessages(this, buildableFreeObject, byPassEventsAndMessages);
            }
            else
            {
                if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectPlacement)
                {
                    string message = default;
                    bool isWithinGridBounds = true;
                    if (!IsWithinGridBounds(cellPosition, activeVerticalGridIndex)) isWithinGridBounds = false;
                    
                    message = isWithinGridBounds ? $"Grid Position Is Occupied!" : "Out of Grid Bounds!";
                    Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Cannot Build Here!</b></color> {message}");
                }
            }
        }

        private bool TryPlaceBuildableFreeObjectSinglePlacement(BuildableFreeObjectSO buildableFreeObjectSO, CellPositionXZ cellPosition, Vector3 worldPosition, 
            FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, bool ignoreBuildConditions,
            int activeVerticalGridIndex, bool byPassEventsAndMessages, out BuildableFreeObject buildableFreeObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null,
            BuildableFreeObject originalBuildableFreeObject = null)
        {
            bool isBuildable = true;
            CheckConditions();

            void CheckConditions()
            {
                if (!gridList[activeVerticalGridIndex].IsWithinGridBounds(cellPosition))
                {
                    isBuildable = false;
                    return;
                }

                if (buildableFreeObjectSO.affectByBasicAreaEnablers && IsBuildableObjectEnabledByBasicGridAreaEnablers())
                {
                    isBuildable = true;
                    return;
                }
                if (buildableFreeObjectSO.affectByAreaEnablers && IsFreeObjectEnabledByGridAreaEnablers(gridList[activeVerticalGridIndex], buildableFreeObjectSO, new Vector2Int(cellPosition.x, cellPosition.z)))
                {
                    isBuildable = true;
                    return;
                }

                if (buildableFreeObjectSO.affectByBasicAreaDisablers && IsBuildableObjectDisabledByBasicGridAreaDisablers())
                {
                    isBuildable = false;
                    return;
                }
                if (buildableFreeObjectSO.affectByAreaDisablers && IsFreeObjectDisabledByGridAreaDisablers(gridList[activeVerticalGridIndex], buildableFreeObjectSO, new Vector2Int(cellPosition.x, cellPosition.z)))
                {
                    isBuildable = false;
                    return;
                }

                // if (!ignoreBuildConditions)
                // {
                //     if (buildableGridObjectSO.enableBuildConditions)
                //     {
                //         foreach (BuildConditionsSO buildConditionsSO in buildableGridObjectSO.buildConditionsSOList)
                //         {
                //             if (!GetBuildConditionBuildableGridObject(buildableGridObjectSO))
                //             {
                //                 //Debug.Log("Conditions not met");
                //                 isBuildable = false;
                //                 break;
                //             }
                //         }
                //     }

                //     if (buildableAreaBlockerHit)
                //     {
                //         if (buildableAreaBlockerHitBlockGridObject)
                //         {
                //             //Debug.Log("Conditions not met");
                //             isBuildable = false;
                //             break;
                //         }
                //     }
                // }
            }

            if (isBuildable)
            {
                InitializeBuildableFreeObjectPlacement(buildableFreeObjectSO, cellPosition, worldPosition, fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals,
                    ignoreBuildConditions, activeVerticalGridIndex, byPassEventsAndMessages, out buildableFreeObject, buildableObjectSORandomPrefab, originalBuildableFreeObject);
                return true;
            }
            else
            {
                buildableFreeObject = null;
                return false;
            }
        }

        private void InitializeBuildableFreeObjectPlacement(BuildableFreeObjectSO buildableFreeObjectSO, CellPositionXZ cellPosition, Vector3 worldPosition, 
            FourDirectionalRotation fourDirectionalDirection, EightDirectionalRotation eightDirectionalDirection, float freeRotation, Vector3 hitNormals, bool ignoreBuildConditions,
            int activeVerticalGridIndex, bool byPassEventsAndMessages, out BuildableFreeObject buildableFreeObject, BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab = null,
            BuildableFreeObject originalBuildableFreeObject = null)
        {
            BuildableObjectSO.RandomPrefabs selectedRandomPrefab = buildableObjectSORandomPrefab ?? activeBuildableObjectSORandomPrefab;
            if (!originalBuildableFreeObject) buildableFreeObject = Instantiate(selectedRandomPrefab.objectPrefab, Vector3.zero, Quaternion.identity).GetComponent<BuildableFreeObject>();
            else buildableFreeObject = originalBuildableFreeObject;

            buildableFreeObject.SetupBuildableFreeObject(this, activeVerticalGridIndex, cellSize, buildableFreeObjectSO, activeBuildableObjectSORandomPrefab, worldPosition, 
                new Vector2Int(cellPosition.x, cellPosition.z), fourDirectionalDirection, eightDirectionalDirection, freeRotation, hitNormals);

            if (buildableFreeObjectSO.eachPlacementUseRandomPrefab && buildableFreeObjectSO.intervalBetweenEachPlacement <= 0) UpdateActiveBuildableObjectSORandomPrefab(buildableFreeObjectSO);

            if (!ignoreBuildConditions)
            {
                // if (buildableGridObjectSO.enableBuildConditions)
                // {
                //     CompleteBuildConditionBuildableGridObject(buildableGridObjectSO);
                // }
            }

            GridCellData gridCellData = gridList[activeVerticalGridIndex].GetCellData(cellPosition.x, cellPosition.z);                          // Get the GridCellData Copy
            List<BuildableFreeObject> buildableFreeObjects = gridCellData.GetBuildableFreeObjectData();                                         // Initialize the dictionary
            buildableFreeObjects.Add(buildableFreeObject);                                                                                      // Modify the data
            gridList[activeVerticalGridIndex].SetCellData(cellPosition.x, cellPosition.z, gridCellData);                                        // Write the modified GridCellData back to the original
                
            if (!byPassEventsAndMessages) RaiseActiveBuildableSOChangeEventsAndMessages(this, new Vector2Int(cellPosition.x, cellPosition.z));
        }

        private void StartAndUpdateFreeObjectSplinePlacement(Vector3 worldPosition, BuildableFreeObjectSO buildableFreeObjectSO, FreeObjectPlacementType freeObjectPlacementType)
        {
            if (buildableFreeObjectSplineContainer == null)
            {
                buildableFreeObjectSplineContainer = new GameObject("RuntimeSplineContainer").AddComponent<SplineContainer>();
                buildableFreeObjectSplineContainer.transform.parent = transform;
                buildableFreeObjectSpline = buildableFreeObjectSplineContainer.Spline;
            }
            if (buildableFreeObjectSpline.Count <= 1) buildableFreeObjectSplineObjectSpacing = buildableFreeObjectSO.objectBaseSpacingAlongSpline;

            BezierKnot knot = new BezierKnot(worldPosition);
            TangentMode tangentMode = buildableFreeObjectSO.splineTangetMode == SplineTangetMode.AutoSmooth ? TangentMode.AutoSmooth : TangentMode.Linear;
            buildableFreeObjectSpline.Add(knot, tangentMode);
            if (buildableFreeObjectSO.closedSpline) buildableFreeObjectSpline.Closed = true;

            RaiseOnFreeObjectSplinePlacementStartedAndUpdatedEvent(this, worldPosition, freeObjectPlacementType);
        }

        private void CancelFreeObjectSplinePlacement()
        {
            if (buildableFreeObjectSplineContainer != null)
            {
                Destroy(buildableFreeObjectSplineContainer.gameObject);
                buildableFreeObjectSplineContainer = null;
                buildableFreeObjectSpline = null;
            }
            buildableFreeObjectSplineObjectSpacing = (activeBuildableObjectSO as BuildableFreeObjectSO).objectBaseSpacingAlongSpline;

            activeBuildableFreeObjectSOPlacementType = FreeObjectPlacementType.SinglePlacement;
            RaiseOnFreeObjectSplinePlacementCancelledEvent(this);
        }
        #endregion Setup Buildable Free Object Placement Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID VALIDATION FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///
        
        #region Grid Validation Functions Start:
        public override bool IsWithinActiveGridBounds(Vector2Int cellPosition)
        {
            return activeGrid.IsWithinGridBounds(cellPosition);
        }

        public override bool IsWithinGridBounds(Vector2Int cellPosition, int verticalGridIndex)
        {
            return gridList[verticalGridIndex].IsWithinGridBounds(cellPosition);
        }

        public bool IsWithinActiveGridBounds(CellPositionXZ cellPositionXZ)
        {
            return activeGrid.IsWithinGridBounds(cellPositionXZ);
        }

        public bool IsWithinGridBounds(CellPositionXZ cellPositionXZ, int verticalGridIndex)
        {
            return gridList[verticalGridIndex].IsWithinGridBounds(cellPositionXZ);
        }

        public override bool IsGridObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableGridObjectSO buildingGridObjectSO, Vector2Int cellPosition)
        {
            if (gridManager.TryGetGridAreaDisablerManager(out GridAreaDisablerManager gridAreaDisablerManager))
            {
                return gridAreaDisablerManager.IsBuildableGridObjectBlockedByGridAreaDisablers(this, activeGrid, buildingGridObjectSO, cellPosition);
            }   
            else return false;
        }

        public override bool IsEdgeObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableEdgeObjectSO buildingEdgeObjectSO, Vector2Int cellPosition, EdgeObjectCellDirection edgeObjectCellDirection)
        {
            if (gridManager.TryGetGridAreaDisablerManager(out GridAreaDisablerManager gridAreaDisablerManager))
            {
                return gridAreaDisablerManager.IsBuildableEdgeObjectBlockedByGridAreaDisablers(this, activeGrid, buildingEdgeObjectSO, cellPosition, edgeObjectCellDirection);
            }   
            else return false;
        }

        public override bool IsCornerObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableCornerObjectSO buildingCornerObjectSO, Vector2Int cellPosition, CornerObjectCellDirection cornerObjectCellDirection)
        {
            if (gridManager.TryGetGridAreaDisablerManager(out GridAreaDisablerManager gridAreaDisablerManager))
            {
                return gridAreaDisablerManager.IsBuildableCornerObjectBlockedByGridAreaDisablers(this, activeGrid, buildingCornerObjectSO, cellPosition, cornerObjectCellDirection);
            }   
            else return false;
        }

        public override bool IsFreeObjectDisabledByGridAreaDisablers(Grid activeGrid, BuildableFreeObjectSO buildingFreeObjectSO, Vector2Int cellPosition)
        {
            if (gridManager.TryGetGridAreaDisablerManager(out GridAreaDisablerManager gridAreaDisablerManager))
            {
                return gridAreaDisablerManager.IsBuildableFreeObjectBlockedByGridAreaDisablers(this, activeGrid, buildingFreeObjectSO, cellPosition);
            }   
            else return false;
        }

        public override bool IsBuildableObjectDisabledByBasicGridAreaDisablers()
        {   
            if (gridManager.TryGetGridAreaDisablerManager(out GridAreaDisablerManager gridAreaDisablerManager))
            {
                return gridAreaDisablerManager.IsBuildableObjectBlockedByBasicGridAreaDisablers();
            }   
            else return false;
        }

        public override bool IsGridObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableGridObjectSO buildingGridObjectSO, Vector2Int cellPosition)
        {
            if (gridManager.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
            {
                return gridAreaEnablerManager.IsBuildableGridObjectEnabledByGridAreaEnablers(this, activeGrid, buildingGridObjectSO, cellPosition);
            }   
            else return false;
        }

        public override bool IsEdgeObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableEdgeObjectSO buildingEdgeObjectSO, Vector2Int cellPosition, EdgeObjectCellDirection edgeObjectCellDirection)
        {
            if (gridManager.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
            {
                return gridAreaEnablerManager.IsBuildableEdgeObjectEnabledByGridAreaEnablers(this, activeGrid, buildingEdgeObjectSO, cellPosition, edgeObjectCellDirection);
            }   
            else return false;
        }

        public override bool IsCornerObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableCornerObjectSO buildingCornerObjectSO, Vector2Int cellPosition, CornerObjectCellDirection cornerObjectCellDirection)
        {
            if (gridManager.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
            {
                return gridAreaEnablerManager.IsBuildableCornerObjectEnabledByGridAreaEnablers(this, activeGrid, buildingCornerObjectSO, cellPosition, cornerObjectCellDirection);
            }   
            else return false;
        }

        public override bool IsFreeObjectEnabledByGridAreaEnablers(Grid activeGrid, BuildableFreeObjectSO buildingFreeObjectSO, Vector2Int cellPosition)
        {
            if (gridManager.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
            {
                return gridAreaEnablerManager.IsBuildableFreeObjectEnabledByGridAreaEnablers(this, activeGrid, buildingFreeObjectSO, cellPosition);
            }   
            else return false;
        }

        public override bool IsBuildableObjectEnabledByBasicGridAreaEnablers()
        {   
            if (gridManager.TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager))
            {
                return gridAreaEnablerManager.IsBuildableObjectEnabledByBasicGridAreaEnablers();
            }   
            else return false;
        }
        #endregion Grid Validation Functions End:
        
        ///-------------------------------------------------------------------------------///
        /// MODULE SUPPORTER FUNCTIONS                                                    ///
        ///-------------------------------------------------------------------------------///

        #region Ghost Object Module Supporter Functions Start:
        public override bool CheckPlacementDistanceForGhostObject(Vector3 worldPosition)
        {
            if (!gridManager.GetIsEnableDistanceBasedBuilding()) return true;

            if (!gridManager.GetDistanceCheckObject()) gridManager.SetDistanceCheckObject(Camera.main.transform);
            float distance = Vector3.Distance(gridManager.GetDistanceCheckObject().position, worldPosition);
            
            if (distance > gridManager.GetMinimumDistance() && distance < gridManager.GetMaximumDistance()) return true;
            return false;
        }
        #endregion Ghost Object Module Supporter Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID SUPPORTER FUNCTIONS                                                      ///
        ///-------------------------------------------------------------------------------///

        #region Grid Supporter Functions Start:
        private void ResetBuildablesAndGridMode()
        {
            if (activeGridMode != GridMode.None)
            {
                activeGridMode = GridMode.None;
                RaiseActiveGridModeChangeEventsAndMessages(this, GridMode.None);
            }
            ResetBuildables();
        }

        private void ResetBuildables()
        {
            ResetBoxPlacementTypes();
            selectedBuildableSOIndex = 1;
            activeBuildableObjectSO = null;
            RaiseActiveBuildableSOChangeEventsAndMessages(this, activeBuildableObjectSO);
        }

        private void ResetBoxPlacementTypes()
        {
            if (activeBuildableObjectSO is BuildableGridObjectSO && IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelGridObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) 
            {
                CancelEdgeObjectBoxPlacement();
                if (buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO) CancelCornerObjectBoxPlacement();
            }
            if (activeBuildableObjectSO is BuildableCornerObjectSO && IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelCornerObjectBoxPlacement();
            if (activeBuildableObjectSO is BuildableFreeObjectSO && IsActiveBuildableFreeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()) CancelFreeObjectSplinePlacement();
        }

        private bool IsActiveBuildableGridObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.BoxPlacement || activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.FourDirectionWirePlacement || 
                activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.WireBoxPlacement || activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.LShapedPlacement) return true;
            return false;
        }

        private bool IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.FourDirectionWirePlacement || 
                activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.WireBoxPlacement || activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.LShapedPlacement) return true;
            return false;
        }

        private bool IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.BoxPlacement || activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.FourDirectionWirePlacement || 
                activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.WireBoxPlacement || activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.LShapedPlacement) return true;
            return false;
        }

        private bool IsActiveBuildableFreeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement) return true;
            return false;
        }

        protected void UpdateActiveCameraModeProperties()
        {
            switch (activeCameraMode)
            {
                case CameraMode.ThirdPerson: if (!gridManager.GetIsEnableDistanceBasedBuilding()) gridManager.SetIsEnableDistanceBasedBuilding(true); break;
                case CameraMode.TopDown: if (gridManager.GetIsEnableDistanceBasedBuilding() && gridManager.GetIsAutoDisableInTopDownMode()) gridManager.SetIsEnableDistanceBasedBuilding(false); break;
            }
        }
        #endregion Grid Supporter Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID EVENT AND MESSAGE RAISER FUNCTIONS                                       ///
        ///-------------------------------------------------------------------------------///

        #region Grid Event And Message Raiser Functions Start:
        private void RaiseGridSystemCreationEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, int verticalGridCount)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onGridSystemCreation)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {easyGridBuilderPro.name}: <color=green><b>Grid System Created!</b></color> Vertical Grid Count: {verticalGridCount}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnGridSystemCreatedUnityEvent?.Invoke(easyGridBuilderPro);
            RaiseOnGridSystemCreated(easyGridBuilderPro);
        }

        private void RaiseActiveGridModeChangeEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onActiveGridModeChange)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=green><b>Active Grid Mode Changed!</b></color> {gridMode}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnActiveGridModeChangedUnityEvent?.Invoke(easyGridBuilderPro, gridMode);
            gridManager.RaiseOnActiveGridModeChanged(easyGridBuilderPro, gridMode);
        }

        private void RaiseActiveBuildableSOChangeEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onActiveBuildableSOChange)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=green><b>Active Buildable Object SO Changed!</b></color> {buildableObjectSO.name}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnActiveBuildableSOChangedUnityEvent?.Invoke(easyGridBuilderPro, buildableObjectSO);
            gridManager.RaiseOnActiveBuildableSOChanged(easyGridBuilderPro, buildableObjectSO);
        }

        private void RaiseActiveVerticalGridChangeEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, int activeVerticalGridIndex)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onActiveVerticalGridChange)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=green><b>Active Vertical Grid Changed!</b></color> Vertical Grid Index: {activeVerticalGridIndex}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnActiveVerticalGridChangedUnityEvent?.Invoke(easyGridBuilderPro, activeVerticalGridIndex);
            gridManager.RaiseOnActiveVerticalGridChanged(easyGridBuilderPro, activeVerticalGridIndex);
        }

        private void RaiseActiveBuildableSOChangeEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, Vector2Int cellPosition)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onCellObjectValueChange)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {easyGridBuilderPro.name}: <color=green><b>Cell Object Value Changed!</b></color> Cell Position: {cellPosition}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnCellObjectValueChangedUnityEvent?.Invoke(easyGridBuilderPro, cellPosition);
            gridManager.RaiseOnCellObjectValueChanged(easyGridBuilderPro, cellPosition);
        }

        private void RaiseBuildableObjectPlacementEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject, bool byPassEventsAndMessages = false)
        {
            if (!byPassEventsAndMessages && activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && buildableEdgeObjectSO.placementType != EdgeObjectPlacementType.SinglePlacement && 
                buildableEdgeObjectSO.placementType != EdgeObjectPlacementType.PaintPlacement)
            {
                if (buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(cellSize, activeBuildableObjectSORandomPrefab) != 1)
                {
                    Debug.Log($"EasyGridBuilderPro XZ: {this.name}: Placement Type: {buildableEdgeObjectSO.placementType}: " +
                    $"<color=orange><b>Warning: The length of the Active Buildable Edge Object</b></color> {buildableEdgeObjectSO.name} <color=orange><b>exceeds the size of the grid cell!</b></color> " +
                    "Using objects larger than the cell size with one of the Box Placement type may result in unexpected behavior. " +
                    "To resolve this issue, consider one of the following:\n" +
                    "<color=orange><b>- Increase the grid cell size.</b></color>\n" +
                    "<color=orange><b>- Use a 3D model with a length that is equal to or smaller than the cell size.</b></color>\n" +
                    "<color=orange><b>- Adjust the Object True Scale properties in the Buildable Edge Object.</b></color>\n" +
                    "<color=orange><b>- Limit this object placement type to Single or Paint placement types.</b></color>");
                }
            }

            if (!byPassEventsAndMessages && gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectPlacement)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {easyGridBuilderPro.name}: <color=green><b>Buildable Object Placed!</b></color> {buildableObject.name}");
            }

            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnBuildableObjectPlacedUnityEvent?.Invoke(easyGridBuilderPro, buildableObject);
            gridManager.RaiseOnBuildableObjectPlaced(easyGridBuilderPro, buildableObject);
        }

        private void RaiseOnBuildableObjectSOAddedEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectSOAdded)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {easyGridBuilderPro.name}: <color=green><b>New Buildable Object SO Added! </b></color> {buildableObjectSO}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnBuildableObjectSOAddedUnityEvent?.Invoke(easyGridBuilderPro, buildableObjectSO);
            gridManager.RaiseOnBuildableObjectSOAdded(easyGridBuilderPro, buildableObjectSO);
        }

        private void RaiseOnBuildableObjectSORemovedEventsAndMessages(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            if (gridManager.GetIsEnableConsoleMessages() && gridManager.GetConsoleMessagesSettings().onBuildableObjectSORemoved)
            {
                Debug.Log($"EasyGridBuilderPro XZ: {easyGridBuilderPro.name}: <color=green><b>New Buildable Object SO Removed! </b></color> {buildableObjectSO}");
            }
            if (gridManager.GetIsEnableUnityEvents()) gridManager.OnBuildableObjectSORemovedUnityEvent?.Invoke(easyGridBuilderPro, buildableObjectSO);
            gridManager.RaiseOnBuildableObjectSORemoved(easyGridBuilderPro, buildableObjectSO);
        }
        #endregion Grid Event And Message Raiser Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID INTERNAL SUPPORTER EVENT RAISER FUNCTIONS                                ///
        ///-------------------------------------------------------------------------------///

        #region Grid Internal Supporter Event Raiser Functions Start:
        private void RaiseOnGridObjectBoxPlacementStartedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, GridObjectPlacementType placementType)
        {
            gridManager.RaiseOnGridObjectBoxPlacementStarted(easyGridBuilderPro, boxPlacementStartPosition, placementType);
        }

        private void RaiseOnGridObjectBoxPlacementUpdatedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            gridManager.RaiseOnGridObjectBoxPlacementUpdated(easyGridBuilderPro, boxPlacementEndPosition);
        }

        private void RaiseOnGridObjectBoxPlacementFinalizedEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnGridObjectBoxPlacementFinalized(easyGridBuilderPro);
        }

        private void RaiseOnGridObjectBoxPlacementCancelledEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnGridObjectBoxPlacementCancelled(easyGridBuilderPro);
        }

        private void RaiseOnEdgeObjectBoxPlacementStartedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, EdgeObjectPlacementType placementType)
        {
            gridManager.RaiseOnEdgeObjectBoxPlacementStarted(easyGridBuilderPro, boxPlacementStartPosition, placementType);
        }

        private void RaiseOnEdgeObjectBoxPlacementUpdatedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            gridManager.RaiseOnEdgeObjectBoxPlacementUpdated(easyGridBuilderPro, boxPlacementEndPosition);
        }

        private void RaiseOnEdgeObjectBoxPlacementFinalizedEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnEdgeObjectBoxPlacementFinalized(easyGridBuilderPro);
        }

        private void RaiseOnEdgeObjectBoxPlacementCancelledEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnEdgeObjectBoxPlacementCancelled(easyGridBuilderPro);
        }

        private void RaiseOnCornerObjectBoxPlacementStartedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, CornerObjectPlacementType placementType)
        {
            gridManager.RaiseOnCornerObjectBoxPlacementStarted(easyGridBuilderPro, boxPlacementStartPosition, placementType);
        }

        private void RaiseOnCornerObjectBoxPlacementUpdatedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            gridManager.RaiseOnCornerObjectBoxPlacementUpdated(easyGridBuilderPro, boxPlacementEndPosition);
        }

        private void RaiseOnCornerObjectBoxPlacementFinalizedEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnCornerObjectBoxPlacementFinalized(easyGridBuilderPro);
        }

        private void RaiseOnCornerObjectBoxPlacementCancelledEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnCornerObjectBoxPlacementCancelled(easyGridBuilderPro);
        }

        private void RaiseOnFreeObjectSplinePlacementStartedAndUpdatedEvent(EasyGridBuilderPro easyGridBuilderPro, Vector3 worldPosition, FreeObjectPlacementType freeObjectPlacementType)
        {
            gridManager.RaiseOnFreeObjectSplinePlacementStartedAndUpdated(easyGridBuilderPro, worldPosition, freeObjectPlacementType);
        }

        private void RaiseOnFreeObjectSplinePlacementFinalizedEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnFreeObjectSplinePlacementFinalized(easyGridBuilderPro);
        }

        private void RaiseOnFreeObjectSplinePlacementCancelledEvent(EasyGridBuilderPro easyGridBuilderPro)
        {
            gridManager.RaiseOnFreeObjectSplinePlacementCancelled(easyGridBuilderPro);
        }
        #endregion Grid Internal Supporter Event Raiser Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS - EXTERNAL                                            ///
        ///-------------------------------------------------------------------------------///
        
        #region Public External Getter Functions Start:
        public override List<BuildableGridObjectSO> GetBuildableGridObjectSOList() => buildableGridObjectSOList;

        public override List<BuildableEdgeObjectSO> GetBuildableEdgeObjectSOList() => buildableEdgeObjectSOList;

        public override List<BuildableCornerObjectSO> GetBuildableCornerObjectSOList() => buildableCornerObjectSOList;
        
        public override List<BuildableFreeObjectSO> GetBuildableFreeObjectSOList() => buildableFreeObjectSOList;

        /// <summary>
        /// Determines if the grid should update in the editor.
        /// </summary>
        /// <returns>True if the grid updates in the editor, false otherwise.</returns>
        public override bool GetIsUpdateInEditor() => updateInEditor;

        /// <summary>
        /// Gets the width of the grid.
        /// </summary>
        /// <returns>The width of the grid.</returns>
        public override int GetGridWidth() => gridWidth;

        /// <summary>
        /// Gets the length of the grid.
        /// </summary>
        /// <returns>The length of the grid.</returns>
        public override int GetGridLength() => gridLength;

        /// <summary>
        /// Gets the size of each cell in the grid.
        /// </summary>
        /// <returns>The size of each cell.</returns>
        public override float GetCellSize() => cellSize;

        /// <summary>
        /// Determines if the grid position should update during runtime.
        /// </summary>
        /// <returns>True if the grid position updates during runtime, false otherwise.</returns>
        public override bool GetIsUpdateGridPositionRuntime() => updateGridPositionRuntime;

        public override GridOrigin GetGridOriginType() => gridOriginType;

        public override bool GetIsUpdateGridWidthAndLengthRuntime() => updateGridWidthAndLengthRuntime;

        /// <summary>
        /// Gets the current active grid mode.
        /// </summary>
        /// <returns>The current active grid mode.</returns>
        public override GridMode GetActiveGridMode() => activeGridMode;

        /// <summary>
        /// Gets the current active camera mode.
        /// </summary>
        /// <returns>The current active camera mode.</returns>
        public override CameraMode GetActiveCameraMode() => activeCameraMode;

        /// <summary>
        /// Gets the number of vertical grids.
        /// </summary>
        /// <returns>The number of vertical grids.</returns>
        public override int GetVerticalGridsCount() => verticalGridsCount;

        /// <summary>
        /// Gets the height of each vertical grid.
        /// </summary>
        /// <returns>The height of each vertical grid.</returns>
        public override float GetVerticalGridHeight() => verticalGridHeight;

        /// <summary>
        /// Determines if switching vertical grids with input is enabled.
        /// </summary>
        /// <returns>True if switching vertical grids with input is enabled, false otherwise.</returns>
        public override bool GetIsSwitchVerticalGridWithInput() => switchVerticalGridWithInput;

        /// <summary>
        /// Determines if auto-detection of vertical grids is enabled.
        /// </summary>
        /// <returns>True if auto-detection of vertical grids is enabled, false otherwise.</returns>
        public override bool GetIsAutoDetectVerticalGrid() => autoDetectVerticalGrid;

        /// <summary>
        /// Gets the layer mask for vertical grid auto-detection.
        /// </summary>
        /// <returns>The layer mask for vertical grid auto-detection.</returns>
        public override LayerMask GetVerticalGridAutoDetectionLayer() => autoDetectionLayerMask;

        /// <summary>
        /// Gets the multiplier for the size of the collider.
        /// </summary>
        /// <returns>The multiplier for the collider size.</returns>
        public override float GetColliderSizeMultiplier() => colliderSizeMultiplier;

        /// <summary>
        /// Determines if the collider is locked at the bottom grid.
        /// </summary>
        /// <returns>True if the collider is locked at the bottom grid, false otherwise.</returns>
        public override bool GetIsLockColliderAtBottomGrid() => lockColliderAtBottomGrid;

        /// <summary>
        /// Determines if the canvas grid display is enabled.
        /// </summary>
        /// <returns>True if the canvas grid display is enabled, false otherwise.</returns>
        public override bool GetIsDisplayCanvasGrid() => displayCanvasGrid;

        /// <summary>
        /// Gets the settings for the canvas grid.
        /// </summary>
        /// <returns>The settings for the canvas grid.</returns>
        public override CanvasGridSettings GetCanvasGridSettings() => canvasGridSettings;

        /// <summary>
        /// Determines if the object grid display is enabled.
        /// </summary>
        /// <returns>True if the object grid display is enabled, false otherwise.</returns>
        public override bool GetIsDisplayObjectGrid() => displayObjectGrid;

        /// <summary>
        /// Gets the settings for the object grid.
        /// </summary>
        /// <returns>The settings for the object grid.</returns>
        public override ObjectGridSettings GetObjectGridSettings() => objectGridSettings;

        /// <summary>
        /// Determines if the runtime text grid display is enabled.
        /// </summary>
        /// <returns>True if the runtime text grid display is enabled, false otherwise.</returns>
        public override bool GetIsDisplayRuntimeTextGrid() => displayRuntimeTextGrid;

        /// <summary>
        /// Gets the settings for the runtime text grid.
        /// </summary>
        /// <returns>The settings for the runtime text grid.</returns>
        public override RuntimeTextGridSettings GetRuntimeTextGridSettings() => runtimeTextGridSettings;

        /// <summary>
        /// Determines if the runtime procedural grid display is enabled.
        /// </summary>
        /// <returns>True if the runtime procedural grid display is enabled, false otherwise.</returns>
        public override bool GetIsDisplayRuntimeProceduralGrid() => displayRuntimeProceduralGrid;

        /// <summary>
        /// Gets the settings for the runtime procedural grid.
        /// </summary>
        /// <returns>The settings for the runtime procedural grid.</returns>
        public override RuntimeProceduralGridSettings GetRuntimeProceduralGridSettings() => runtimeProceduralGridSettings;
        #endregion Public External Getter Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS - INTERNAL                                            ///
        ///-------------------------------------------------------------------------------///

        #region Public Internal Getter Functions Start:
        public override Vector3 GetActiveGridOrigin()
        {
            return gridOriginList[activeVerticalGridIndex];
        }

        public override Vector3 GetActiveGridOrigin(int activeVerticalGridIndex)
        {
            return gridOriginList[activeVerticalGridIndex];
        }

        public override BuildableObjectSO GetActiveBuildableObjectSO()
        {
            return activeBuildableObjectSO;
        }

        public override BuildableObjectSO.RandomPrefabs GetActiveBuildableObjectSORandomPrefab()
        {
            return activeBuildableObjectSORandomPrefab;
        }

        public override BuildableObjectSO.RandomPrefabs GetActiveSecondaryBuildableObjectSORandomPrefab()
        {
            return activeSecondaryBuildableObjectSORandomPrefab;
        }

        public override GridObjectPlacementType GetActiveBuildableGridObjectSOPlacementType()
        {
            return activeBuildableGridObjectSOPlacementType;
        }

        public override EdgeObjectPlacementType GetActiveBuildableEdgeObjectSOPlacementType()
        {
            return activeBuildableEdgeObjectSOPlacementType;
        }

        public override CornerObjectPlacementType GetActiveBuildableCornerObjectSOPlacementType()
        {
            return activeBuildableCornerObjectSOPlacementType;
        }

        public override FreeObjectPlacementType GetActiveBuildableFreeObjectSOPlacementType()
        {
            return activeBuildableFreeObjectSOPlacementType;
        }

        public override Grid GetActiveGrid()
        {
            return activeGrid;
        }

        public override Grid GetNearestGrid(Vector3 worldPosition)
        {
            Vector3 closestPosition = Vector3.zero;
            float closestDistanceSqr = Mathf.Infinity;

            foreach (Vector3 origin in gridOriginList)
            {
                float distanceSqr = (origin - worldPosition).sqrMagnitude;
                if (distanceSqr < closestDistanceSqr)
                {
                    closestDistanceSqr = distanceSqr;
                    closestPosition = origin;
                }
            }
            return gridList[gridOriginList.IndexOf(closestPosition)];
        }

        public override FourDirectionalRotation GetActiveBuildableGridObjectRotation()
        {
            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO && buildableGridObjectSO.freezeRotation)
            {
                return buildableGridObjectSO.fourDirectionalRotation;
            }
            return activeBuildableGridObjectSORotation;
        }

        public override FourDirectionalRotation GetActiveBuildableEdgeObjectRotation()
        {
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && buildableEdgeObjectSO.freezeRotation)
            {
                return buildableEdgeObjectSO.fourDirectionalRotation;
            }
            return activeBuildableEdgeObjectSORotation;
        }

        public override bool GetActiveBuildableEdgeObjectFlipped()
        {
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && buildableEdgeObjectSO.freezeRotation)
            {
                return buildableEdgeObjectSO.isObjectFlipped;
            }
            return activeBuildableEdgeObjectSOFlipped;
        }

        public override FourDirectionalRotation GetActiveBuildableCornerObjectFourDirectionalRotation()
        {
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO && buildableCornerObjectSO.freezeRotation)
            {
                return buildableCornerObjectSO.fourDirectionalRotation;
            }
            return activeBuildableCornerObjectSOFourDirectionalRotation;
        }

        public override EightDirectionalRotation GetActiveBuildableCornerObjectEightDirectionalRotation()
        {
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO && buildableCornerObjectSO.freezeRotation)
            {
                return buildableCornerObjectSO.eightDirectionalRotation;
            }
            return activeBuildableCornerObjectSOEightDirectionalRotation;
        }

        public override float GetActiveBuildableCornerObjectFreeRotation()
        {
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO && buildableCornerObjectSO.freezeRotation)
            {
                return buildableCornerObjectSO.freeRotation;
            }
            return activeBuildableCornerObjectSOFreeRotation;
        }

        public override FourDirectionalRotation GetActiveBuildableFreeObjectFourDirectionalRotation()
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && buildableFreeObjectSO.freezeRotation)
            {
                return buildableFreeObjectSO.fourDirectionalRotation;
            }
            return activeBuildableFreeObjectSOFourDirectionalRotation;
        }

        public override EightDirectionalRotation GetActiveBuildableFreeObjectEightDirectionalRotation()
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && buildableFreeObjectSO.freezeRotation)
            {
                return buildableFreeObjectSO.eightDirectionalRotation;
            }
            return activeBuildableFreeObjectSOEightDirectionalRotation;
        }

        public override float GetActiveBuildableFreeObjectFreeRotation()
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && buildableFreeObjectSO.freezeRotation)
            {
                return buildableFreeObjectSO.freeRotation;
            }
            return activeBuildableFreeObjectSOFreeRotation;
        }

        public override float GetActiveBuildableFreeObjectSplineSpacing()
        {
            return buildableFreeObjectSplineObjectSpacing;
        }

        public override bool GetUseBuildModeActivationInput() => useBuildModeActivationKey;

        public override bool GetUseDestroyModeActivationInput() => useDestroyModeActivationKey;

        public override bool GetUseSelectModeActivationInput() => useSelectModeActivationKey;

        public override bool GetUseMoveModeActivationInput() => useMoveModeActivationKey;

        public override int GetActiveVerticalGridIndex()
        {
            return activeVerticalGridIndex;
        }

        public override int GetVerticalGridIndexOf(Grid grid)
        {
            return gridList.IndexOf(grid as GridXZ);
        }

        public override Grid GetVerticalGridIndexOf(int verticalGridIndex)
        {
            return gridList[verticalGridIndex];
        }

        public override Vector3 GetActiveGridCellWorldPosition(Vector2Int cellPosition)
        {
            return activeGrid.GetCellWorldPosition(cellPosition.x, cellPosition.y);
        }

        public Vector3 GetActiveGridCellWorldPosition(CellPositionXZ cellPositionXZ)
        {
            return activeGrid.GetCellWorldPosition(cellPositionXZ);
        }

        public override Vector3 GetCellWorldPosition(Vector2Int cellPosition, int verticalGridIndex)
        {
            return gridList[verticalGridIndex].GetCellWorldPosition(cellPosition.x, cellPosition.y);
        }

        public Vector3 GetCellWorldPosition(CellPositionXZ cellPositionXZ, int verticalGridIndex)
        {
            return gridList[verticalGridIndex].GetCellWorldPosition(cellPositionXZ);
        }

        public override Vector2Int GetActiveGridCellPosition(Vector3 worldPosition)
        {
            return new Vector2Int(activeGrid.GetCellPosition(worldPosition).x, activeGrid.GetCellPosition(worldPosition).z);
        }

        public override Vector2Int GetCellPosition(Vector3 worldPosition, int verticalGridIndex)
        {
            return new Vector2Int(gridList[verticalGridIndex].GetCellPosition(worldPosition).x, gridList[verticalGridIndex].GetCellPosition(worldPosition).z);
        }
        
        public override GridCellData GetActiveGridCellData(Vector2Int cellPosition)
        {
            return activeGrid.GetCellData(cellPosition.x, cellPosition.y);
        }

        public override GridCellData GetCellData(Vector2Int cellPosition, int verticalGridIndex)
        {
            return gridList[verticalGridIndex].GetCellData(cellPosition.x, cellPosition.y);
        }

        public CellPositionXZ GetActiveGridCellPositionXZ(Vector3 worldPosition)
        {
            return activeGrid.GetCellPosition(worldPosition);
        }

        public CellPositionXZ GetCellPositionXZ(Vector3 worldPosition, int verticalGridIndex)
        {
            return gridList[verticalGridIndex].GetCellPosition(worldPosition);
        }

        public override bool GetIsObjectMoving() => isObjectMoving;

        public override BuildableObject GetMovingBuildableObject() => movingBuildableObject;

        public override bool GetRuntimeObjectGridHeatMapModeActiveSelf() => RuntimeObjectGridHeatMapModeEnabled;

        public override float GetActiveGridCustomModifierValue(Vector2Int cellPosition, GridModifierSO gridModifierSO)
        {
            return activeGrid.GetCellData(cellPosition.x, cellPosition.y).GetCustomModifierValue(gridModifierSO);
        }

        public override Dictionary<GridModifierSO, float> GetActiveGridAllCustomModifierValue(Vector2Int cellPosition)
        {
            return activeGrid.GetCellData(cellPosition.x, cellPosition.y).GetCustomModifierData();
        }
        #endregion Public Internal Getter Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS - EXTERNAL                                            ///
        ///-------------------------------------------------------------------------------///

        #region Public External Setter Functions Start:
        /// <summary>
        /// Sets whether the grid should update in the editor.
        /// </summary>
        /// <param name="isUpdateInEditor">True to enable updates in the editor, false otherwise.</param>
        public override void SetUpdateInEditor(bool isUpdateInEditor) => this.updateInEditor = isUpdateInEditor;

        /// <summary>
        /// Sets the width of the grid.
        /// </summary>
        /// <param name="gridWidth">The width of the grid.</param>
        public override void SetGridWidth(int gridWidth) => this.gridWidth = gridWidth;

        /// <summary>
        /// Sets the length of the grid.
        /// </summary>
        /// <param name="gridLength">The length of the grid.</param>
        public override void SetGridLength(int gridLength) => this.gridLength = gridLength;

        /// <summary>
        /// Sets the size of each cell in the grid.
        /// </summary>
        /// <param name="cellSize">The size of each cell.</param>
        public override void SetCellSize(float cellSize) => this.cellSize = cellSize;

        /// <summary>
        /// Sets whether the grid position should update during runtime.
        /// </summary>
        /// <param name="isUpdateGridPositionRuntime">True to enable runtime position updates, false otherwise.</param>
        public override void SetUpdateGridPositionRuntime(bool isUpdateGridPositionRuntime) => this.updateGridPositionRuntime = isUpdateGridPositionRuntime;

        public override void SetUpdateGridWidthAndLengthRuntime(bool isUpdateGridWidthAndLengthRuntime) => this.updateGridWidthAndLengthRuntime = isUpdateGridWidthAndLengthRuntime;

        public override void SetGridOriginType(GridOrigin gridOriginType) => this.gridOriginType = gridOriginType;

        /// <summary>
        /// Sets whether switching vertical grids with input is enabled.
        /// </summary>
        /// <param name="isSwitchVerticalGridWithInput">True to enable input-based vertical grid switching, false otherwise.</param>
        public override void SetSwitchVerticalGridWithInput(bool isSwitchVerticalGridWithInput) => this.switchVerticalGridWithInput = isSwitchVerticalGridWithInput;

        /// <summary>
        /// Sets whether auto-detection of vertical grids is enabled.
        /// </summary>
        /// <param name="isAutoDetectVerticalGrid">True to enable auto-detection of vertical grids, false otherwise.</param>
        public override void SetAutoDetectVerticalGrid(bool isAutoDetectVerticalGrid) => this.autoDetectVerticalGrid = isAutoDetectVerticalGrid;

        /// <summary>
        /// Sets the layer mask for vertical grid auto-detection.
        /// </summary>
        /// <param name="verticalGridAutoDetectionLayer">The layer mask for vertical grid auto-detection.</param>
        public override void SetVerticalGridAutoDetectionLayer(LayerMask verticalGridAutoDetectionLayer) => this.autoDetectionLayerMask = verticalGridAutoDetectionLayer;

        /// <summary>
        /// Sets the multiplier for the size of the collider.
        /// </summary>
        /// <param name="colliderSizeMultiplier">The multiplier for the collider size.</param>
        public override void SetColliderSizeMultiplier(float colliderSizeMultiplier) => this.colliderSizeMultiplier = colliderSizeMultiplier;

        /// <summary>
        /// Sets whether the collider is locked at the bottom grid.
        /// </summary>
        /// <param name="isLockColliderAtBottomGrid">True to lock the collider at the bottom grid, false otherwise.</param>
        public override void SetLockColliderAtBottomGrid(bool isLockColliderAtBottomGrid) => this.lockColliderAtBottomGrid = isLockColliderAtBottomGrid;

        /// <summary>
        /// Sets whether the canvas grid display is enabled.
        /// </summary>
        /// <param name="isDisplayCanvasGrid">True to enable the canvas grid display, false otherwise.</param>
        public override void SetDisplayCanvasGrid(bool isDisplayCanvasGrid) => this.displayCanvasGrid = isDisplayCanvasGrid;

        /// <summary>
        /// Sets the settings for the canvas grid.
        /// </summary>
        /// <param name="canvasGridSettings">The settings for the canvas grid.</param>
        public override void SetCanvasGridSettings(CanvasGridSettings canvasGridSettings) => this.canvasGridSettings = canvasGridSettings;

        /// <summary>
        /// Sets whether the object grid display is enabled.
        /// </summary>
        /// <param name="isDisplayObjectGrid">True to enable the object grid display, false otherwise.</param>
        public override void SetDisplayObjectGrid(bool isDisplayObjectGrid) => this.displayObjectGrid = isDisplayObjectGrid;

        /// <summary>
        /// Sets the settings for the object grid.
        /// </summary>
        /// <param name="objectGridSettings">The settings for the object grid.</param>
        public override void SetObjectGridSettings(ObjectGridSettings objectGridSettings) => this.objectGridSettings = objectGridSettings;

        /// <summary>
        /// Sets whether the runtime text grid display is enabled.
        /// </summary>
        /// <param name="isDisplayRuntimeTextGrid">True to enable the runtime text grid display, false otherwise.</param>
        public override void SetDisplayRuntimeTextGrid(bool isDisplayRuntimeTextGrid) => this.displayRuntimeTextGrid = isDisplayRuntimeTextGrid;

        /// <summary>
        /// Sets the settings for the runtime text grid.
        /// </summary>
        /// <param name="runtimeTextGridSettings">The settings for the runtime text grid.</param>
        public override void SetRuntimeTextGridSettings(RuntimeTextGridSettings runtimeTextGridSettings) => this.runtimeTextGridSettings = runtimeTextGridSettings;

        /// <summary>
        /// Sets whether the runtime procedural grid display is enabled.
        /// </summary>
        /// <param name="isDisplayRuntimeProceduralGrid">True to enable the runtime procedural grid display, false otherwise.</param>
        public override void SetDisplayRuntimeProceduralGrid(bool isDisplayRuntimeProceduralGrid) => this.displayRuntimeProceduralGrid = isDisplayRuntimeProceduralGrid;

        /// <summary>
        /// Sets the settings for the runtime procedural grid.
        /// </summary>
        /// <param name="runtimeProceduralGridSettings">The settings for the runtime procedural grid.</param>
        public override void SetRuntimeProceduralGridSettings(RuntimeProceduralGridSettings runtimeProceduralGridSettings) => this.runtimeProceduralGridSettings = runtimeProceduralGridSettings;
        #endregion Public External Setter Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS - INTERNAL                                            ///
        ///-------------------------------------------------------------------------------///

        #region Public Internal Setter Functions Start:
        public override void SetActiveGridMode(GridMode gridMode, bool isAToggle = false)
        {
            if (activeGridMode == gridMode && isAToggle) gridMode = GridMode.None;
            else if (activeGridMode == gridMode) return;

            if (gridMode == GridMode.None) 
            {
                ResetBuildablesAndGridMode();
                return;
            }
            ResetBuildables();

            switch (gridMode)
            {
                case GridMode.BuildMode: activeGridMode = GridMode.BuildMode; break;
                case GridMode.DestroyMode: activeGridMode = GridMode.DestroyMode; break;
                case GridMode.SelectMode: activeGridMode = GridMode.SelectMode; break;
                case GridMode.MoveMode: activeGridMode = GridMode.MoveMode; break;
            }
            RaiseActiveGridModeChangeEventsAndMessages(this, gridMode);
        }

        public override void SetActiveCameraMode(CameraMode cameraMode)
        {
            if (activeCameraMode == cameraMode) return;

            switch (cameraMode)
            {
                case CameraMode.TopDown: activeCameraMode = CameraMode.TopDown; break;
                case CameraMode.ThirdPerson: activeCameraMode = CameraMode.ThirdPerson; break;
            }
            UpdateActiveCameraModeProperties();
        }

        public override void SetGridWidthAndLength(int gridWidth, int gridLength, bool byPassUpdateGridWidthAndLengthRuntime = false)
        {
            if (!byPassUpdateGridWidthAndLengthRuntime && !updateGridWidthAndLengthRuntime) return;
            this.gridWidth = gridWidth;
            this.gridLength = gridLength;

            foreach (GridXZ gridXZ in gridList)
            {
                gridXZ.SetGridSize(gridWidth, gridLength);
            }
        }

        public override void SetActiveVerticalGrid(int verticalGridIndex)
        {
            if (verticalGridIndex < 0 || verticalGridIndex >= gridList.Count || activeGrid == gridList[verticalGridIndex]) return;

            activeGrid = gridList[verticalGridIndex];
            activeGridOrigin = gridOriginList[verticalGridIndex];
            activeVerticalGridIndex = verticalGridIndex;

            RaiseActiveVerticalGridChangeEventsAndMessages(this, activeVerticalGridIndex);
        }

        public override void AddVerticalGrid()
        {

        }
        public override void RemoveVerticalGrid()
        {

        }

        public override void SetActiveGridCellData(Vector2Int cellPosition, GridCellData gridCellData)
        {
            activeGrid.SetCellData(cellPosition.x, cellPosition.y, gridCellData);
        }

        public override void SetCellData(Vector2Int cellPosition, int verticalGridIndex, GridCellData gridCellData)
        {
            gridList[verticalGridIndex].SetCellData(cellPosition.x, cellPosition.y, gridCellData);
        }

        public override void SetIsObjectMoving(bool isObjectMoving)
        {
            this.isObjectMoving = isObjectMoving;
        }

        public override void SetMovingBuildableObject(BuildableObject buildableObject)
        {
            movingBuildableObject = buildableObject;
        }

        public override void SetActiveBuildableGridObjectRotation(FourDirectionalRotation fourDirectionalRotation)
        {
            activeBuildableGridObjectSORotation = fourDirectionalRotation;
        }

        public override void SetActiveBuildableEdgeObjectRotation(FourDirectionalRotation fourDirectionalRotation)
        {
            activeBuildableEdgeObjectSORotation = fourDirectionalRotation;
        }

        public override void SetActiveBuildableEdgeObjectFlipped(bool isFlipped)
        {
            activeBuildableEdgeObjectSOFlipped = isFlipped;
        }

        public override void SetActiveBuildableCornerObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation)
        {
            activeBuildableCornerObjectSOFourDirectionalRotation = fourDirectionalRotation;
        }

        public override void SetActiveBuildableCornerObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation)
        {
            activeBuildableCornerObjectSOEightDirectionalRotation = eightDirectionalRotation;
        }

        public override void SetActiveBuildableCornerObjectFreeRotation(float freeRotation)
        {
            activeBuildableCornerObjectSOFreeRotation = freeRotation;
        }

        public override void SetActiveBuildableFreeObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation)
        {
            activeBuildableFreeObjectSOFourDirectionalRotation = fourDirectionalRotation;
        }

        public override void SetActiveBuildableFreeObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation)
        {
            activeBuildableFreeObjectSOEightDirectionalRotation = eightDirectionalRotation;
        }

        public override void SetActiveBuildableFreeObjectFreeRotation(float freeRotation)
        {
            activeBuildableFreeObjectSOFreeRotation = freeRotation;
        }

        public override void AddBuildableObjectSOToTheList(BuildableObjectSO buildableObjectSO)
        {
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO:
                    if (buildableGridObjectSOList.Contains(buildableGridObjectSO)) break;
                    buildableGridObjectSOList.Add(buildableGridObjectSO);
                    RaiseOnBuildableObjectSOAddedEventsAndMessages(this, buildableGridObjectSO);
                break;
                case BuildableEdgeObjectSO buildableEdgeObject:
                    if (buildableEdgeObjectSOList.Contains(buildableEdgeObject)) break;
                    buildableEdgeObjectSOList.Add(buildableEdgeObject);
                    RaiseOnBuildableObjectSOAddedEventsAndMessages(this, buildableEdgeObject);
                break;
                case BuildableCornerObjectSO buildableCornerObject:
                    if (buildableCornerObjectSOList.Contains(buildableCornerObject)) break;
                    buildableCornerObjectSOList.Add(buildableCornerObject);
                    RaiseOnBuildableObjectSOAddedEventsAndMessages(this, buildableCornerObject);
                break;
                case BuildableFreeObjectSO buildableFreeObject:
                    if (buildableFreeObjectSOList.Contains(buildableFreeObject)) break;
                    buildableFreeObjectSOList.Add(buildableFreeObject);
                    RaiseOnBuildableObjectSOAddedEventsAndMessages(this, buildableFreeObject);
                break;
            }
        }

        public override void RemoveBuildableObjectSOFromTheList(BuildableObjectSO buildableObjectSO)
        {
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO:
                    if (!buildableGridObjectSOList.Contains(buildableGridObjectSO)) break;
                    buildableGridObjectSOList.Remove(buildableGridObjectSO);
                    RaiseOnBuildableObjectSORemovedEventsAndMessages(this, buildableGridObjectSO);
                break;
                case BuildableEdgeObjectSO buildableEdgeObject:
                    if (!buildableEdgeObjectSOList.Contains(buildableEdgeObject)) break;
                    buildableEdgeObjectSOList.Remove(buildableEdgeObject);
                    RaiseOnBuildableObjectSORemovedEventsAndMessages(this, buildableEdgeObject);
                break;
                case BuildableCornerObjectSO buildableCornerObject:
                    if (!buildableCornerObjectSOList.Contains(buildableCornerObject)) break;
                    buildableCornerObjectSOList.Remove(buildableCornerObject);
                    RaiseOnBuildableObjectSORemovedEventsAndMessages(this, buildableCornerObject);
                break;
                case BuildableFreeObjectSO buildableFreeObject:
                    if (!buildableFreeObjectSOList.Contains(buildableFreeObject)) break;
                    buildableFreeObjectSOList.Remove(buildableFreeObject);
                    RaiseOnBuildableObjectSORemovedEventsAndMessages(this, buildableFreeObject);
                break;
            }
        }

        public override void SetBuildableObjectSOLists(List<BuildableGridObjectSO> buildableGridObjectSOList, List<BuildableEdgeObjectSO> buildableEdgeObjectSOList, 
            List<BuildableCornerObjectSO> buildableCornerObjectSOList, List<BuildableFreeObjectSO> buildableFreeObjectSOList)
        {
            if (buildableGridObjectSOList != null) this.buildableGridObjectSOList = buildableGridObjectSOList;
            if (buildableEdgeObjectSOList != null) this.buildableEdgeObjectSOList = buildableEdgeObjectSOList;
            if (buildableCornerObjectSOList != null) this.buildableCornerObjectSOList = buildableCornerObjectSOList;
            if (buildableFreeObjectSOList != null) this.buildableFreeObjectSOList = buildableFreeObjectSOList;
        }

        public override void ClearBuildableObjectSOLists()
        {
            buildableGridObjectSOList.Clear();
            buildableEdgeObjectSOList.Clear();
            buildableCornerObjectSOList.Clear();
            buildableFreeObjectSOList.Clear();
        }

        public override void SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType gridObjectPlacementType = GridObjectPlacementType.SinglePlacement, 
            EdgeObjectPlacementType edgeObjectPlacementType = EdgeObjectPlacementType.SinglePlacement, CornerObjectPlacementType cornerObjectPlacementType = CornerObjectPlacementType.SinglePlacement, 
            FreeObjectPlacementType freeObjectPlacementType = FreeObjectPlacementType.SinglePlacement)
        {
            if (activeBuildableObjectSO == null) return;
            switch (activeBuildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: buildableGridObjectSO.placementType = gridObjectPlacementType; break;
                case BuildableEdgeObjectSO buildableEdgeObject: buildableEdgeObject.placementType = edgeObjectPlacementType; break;
                case BuildableCornerObjectSO buildableCornerObject: buildableCornerObject.placementType = cornerObjectPlacementType; break;
                case BuildableFreeObjectSO buildableFreeObject: buildableFreeObject.placementType = freeObjectPlacementType; break;
            }
        }

        public override void SetActiveBuildableFreeObjectSplineConnectionMode(bool isClosed)
        {
            if (activeBuildableObjectSO == null || activeBuildableObjectSO is not BuildableFreeObjectSO buildableFreeObjectSO) return;
            if (buildableFreeObjectSO.placementType != FreeObjectPlacementType.SplinePlacement) return;

            buildableFreeObjectSO.closedSpline = isClosed;
        }

        public override void SetActiveBuildableFreeObjectSplineConnectionModeSwap()
        {
            if (activeBuildableObjectSO == null || activeBuildableObjectSO is not BuildableFreeObjectSO buildableFreeObjectSO) return;
            if (buildableFreeObjectSO.placementType != FreeObjectPlacementType.SplinePlacement) return;

            buildableFreeObjectSO.closedSpline = !buildableFreeObjectSO.closedSpline;
        }

        public override void SetActiveBuildableObjectOnlySpawnAtEndPoints(bool onlySpawnAtEndPoints)
        {
            if (activeBuildableObjectSO == null) return;
            switch (activeBuildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: buildableGridObjectSO.spawnOnlyAtEndPoints = onlySpawnAtEndPoints; break;
                case BuildableCornerObjectSO buildableCornerObjectSO: buildableCornerObjectSO.spawnOnlyAtEndPoints = onlySpawnAtEndPoints; break;
                case BuildableEdgeObjectSO: return;
                case BuildableFreeObjectSO: return;
            }
        }

        public override void SetActiveBuildableObjectOnlySpawnAtEndPointsModeSwap()
        {
            if (activeBuildableObjectSO == null) return;
            switch (activeBuildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: buildableGridObjectSO.spawnOnlyAtEndPoints = !buildableGridObjectSO.spawnOnlyAtEndPoints; break;
                case BuildableCornerObjectSO buildableCornerObjectSO: buildableCornerObjectSO.spawnOnlyAtEndPoints = !buildableCornerObjectSO.spawnOnlyAtEndPoints; break;
                case BuildableEdgeObjectSO: return;
                case BuildableFreeObjectSO: return;
            }
        }

        public override void SetActiveBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs randomPrefab)
        {
            activeBuildableObjectSORandomPrefab = randomPrefab;
        }

        public override void SetActiveSecondaryBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs randomPrefab)
        {
            activeSecondaryBuildableObjectSORandomPrefab = randomPrefab;
        }

        public override void SetAllRuntimeObjectGridGeneratedTextureToDefault()
        {
            foreach (GridXZ grid in gridList)
            {
                grid.UpdateGeneratedTexture();
            }
        }
        
        public override void SetRuntimeObjectGridGeneratedTextureCellToDefault(Vector2Int cellPosition, bool blockAllVerticalGrids, Grid grid)
        {
            if (blockAllVerticalGrids)
            {
                foreach (GridXZ gridSystem in gridList)
                {
                    gridSystem.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition);
                }
            }
            else grid.SetRuntimeObjectGridGeneratedTextureCellToDefault(cellPosition);
        }

        public override void SetRuntimeObjectGridGeneratedTextureCellColor(Vector2Int cellPosition, Color cellColor, bool blockAllVerticalGrids, Grid grid)
        {
            if (blockAllVerticalGrids)
            {
                foreach (GridXZ gridSystem in gridList)
                {
                    gridSystem.SetRuntimeObjectGridGeneratedTextureCellColor(cellPosition, cellColor);
                }
            }
            else grid.SetRuntimeObjectGridGeneratedTextureCellColor(cellPosition, cellColor);
        }

        public override void SetRuntimeObjectGridHeatMapTexture(Texture2D generatedTexture, int veritcalGridIndex, Color overrideHDRColor)
        {
            gridList[veritcalGridIndex].SetRuntimeObjectGridHeatMapTexture(generatedTexture, overrideHDRColor);
        }

        public override void SetRuntimeObjectGridHeatMapTextureCellColor(Vector2Int cellPosition, Color color)
        {
            activeGrid.SetRuntimeObjectGridHeatMapTextureCellColor(cellPosition, color);
        }

        public override void SetRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid(bool onlyAffectFirstVerticalGrid)
        {
            isRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid = onlyAffectFirstVerticalGrid;
        }

        public override void SetActiveGridCustomModifierValue(Vector2Int cellPosition, GridModifierSO gridModifierSO, float customModifierValue)
        {
            GridCellData gridCellData = activeGrid.GetCellData(cellPosition.x, cellPosition.y);         // Get the GridCellData Copy
            Dictionary<GridModifierSO, float> customModifiers = gridCellData.GetCustomModifierData();   // Initialize the dictionary
            customModifiers[gridModifierSO] = customModifierValue;                                      // Modify the data
            activeGrid.SetCellData(cellPosition.x, cellPosition.y, gridCellData);                     // Write the modified GridCellData back to the original
        }
        #endregion Public Internal Setter Functions End:
    }
}