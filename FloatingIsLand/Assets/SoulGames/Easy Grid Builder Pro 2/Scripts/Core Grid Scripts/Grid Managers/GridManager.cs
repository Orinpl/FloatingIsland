using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;
using System;
using UnityEngine.Events;

namespace SoulGames.EasyGridBuilderPro
{
    /// <summary>
    /// Manages multiple grid systems in a scene and controls the active grid system based on user interaction.
    /// </summary>
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Manager", 0)]
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        ///-------------------------------------------------------------------------------///
        /// GRID MANAGER RELATED EVENTS                                                   ///
        ///-------------------------------------------------------------------------------///

        public event OnActiveEasyGridBuilderProChangedDelegate OnActiveEasyGridBuilderProChanged;
        public delegate void OnActiveEasyGridBuilderProChangedDelegate(EasyGridBuilderPro activeEasyGridBuilderProSystem);

        public event OnIsMousePointerOnGridChangedDelegate OnIsMousePointerOnGridChanged;
        public delegate void OnIsMousePointerOnGridChangedDelegate(bool isMousePointerOnGrid);

        public event OnActiveCameraModeChangedDelegate OnActiveCameraModeChanged;
        public delegate void OnActiveCameraModeChangedDelegate(CameraMode activeCameraMode);

        ///-------------------------------------------------------------------------------///
        /// EASY GRID BUILDER PRO SYSTEMS RELATED EVENTS                                  ///
        ///-------------------------------------------------------------------------------///
        
        public event OnActiveGridModeChangedDelegate OnActiveGridModeChanged;
        public delegate void OnActiveGridModeChangedDelegate(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode);

        public event OnActiveBuildableSOChangedDelegate OnActiveBuildableSOChanged;
        public delegate void OnActiveBuildableSOChangedDelegate(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO);

        public event OnActiveVerticalGridChangedDelegate OnActiveVerticalGridChanged;
        public delegate void OnActiveVerticalGridChangedDelegate(EasyGridBuilderPro easyGridBuilderPro, int activeVerticalGridIndex);
        
        public event OnCellObjectValueChangedDelegate OnCellObjectValueChanged;
        public delegate void OnCellObjectValueChangedDelegate(EasyGridBuilderPro easyGridBuilderPro, Vector2Int cellPosition);

        public event OnBuildableObjectPlacedDelegate OnBuildableObjectPlaced;
        public delegate void OnBuildableObjectPlacedDelegate(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject);

        public event OnBuildableObjectSOAddedDelegate OnBuildableObjectSOAdded;
        public delegate void OnBuildableObjectSOAddedDelegate(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO);

        public event OnBuildableObjectSORemovedDelegate OnBuildableObjectSORemoved;
        public delegate void OnBuildableObjectSORemovedDelegate(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO);

        ///-------------------------------------------------------------------------------///
        /// EASY GRID BUILDER PRO SYSTEMS RELATED INTERNAL SUPPORTER EVENTS               ///
        ///-------------------------------------------------------------------------------///
        
        public event OnGridObjectBoxPlacementStartedDelegate OnGridObjectBoxPlacementStarted;
        public delegate void OnGridObjectBoxPlacementStartedDelegate(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, GridObjectPlacementType placementType);
        public event OnGridObjectBoxPlacementUpdatedDelegate OnGridObjectBoxPlacementUpdated;
        public delegate void OnGridObjectBoxPlacementUpdatedDelegate(EasyGridBuilderPro easyGridBuilderPro,  Vector3 boxPlacementEndPosition);
        public event OnGridObjectBoxPlacementFinalizedDelegate OnGridObjectBoxPlacementFinalized;
        public delegate void OnGridObjectBoxPlacementFinalizedDelegate(EasyGridBuilderPro easyGridBuilderPro);
        public event OnGridObjectBoxPlacementCancelledDelegate OnGridObjectBoxPlacementCancelled;
        public delegate void OnGridObjectBoxPlacementCancelledDelegate(EasyGridBuilderPro easyGridBuilderPro);

        public event OnEdgeObjectBoxPlacementStartedDelegate OnEdgeObjectBoxPlacementStarted;
        public delegate void OnEdgeObjectBoxPlacementStartedDelegate(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, EdgeObjectPlacementType placementType);
        public event OnEdgeObjectBoxPlacementUpdatedDelegate OnEdgeObjectBoxPlacementUpdated;
        public delegate void OnEdgeObjectBoxPlacementUpdatedDelegate(EasyGridBuilderPro easyGridBuilderPro,  Vector3 boxPlacementEndPosition);
        public event OnEdgeObjectBoxPlacementFinalizedDelegate OnEdgeObjectBoxPlacementFinalized;
        public delegate void OnEdgeObjectBoxPlacementFinalizedDelegate(EasyGridBuilderPro easyGridBuilderPro);
        public event OnEdgeObjectBoxPlacementCancelledDelegate OnEdgeObjectBoxPlacementCancelled;
        public delegate void OnEdgeObjectBoxPlacementCancelledDelegate(EasyGridBuilderPro easyGridBuilderPro);

        public event OnCornerObjectBoxPlacementStartedDelegate OnCornerObjectBoxPlacementStarted;
        public delegate void OnCornerObjectBoxPlacementStartedDelegate(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, CornerObjectPlacementType placementType);
        public event OnCornerObjectBoxPlacementUpdatedDelegate OnCornerObjectBoxPlacementUpdated;
        public delegate void OnCornerObjectBoxPlacementUpdatedDelegate(EasyGridBuilderPro easyGridBuilderPro,  Vector3 boxPlacementEndPosition);
        public event OnCornerObjectBoxPlacementFinalizedDelegate OnCornerObjectBoxPlacementFinalized;
        public delegate void OnCornerObjectBoxPlacementFinalizedDelegate(EasyGridBuilderPro easyGridBuilderPro);
        public event OnCornerObjectBoxPlacementCancelledDelegate OnCornerObjectBoxPlacementCancelled;
        public delegate void OnCornerObjectBoxPlacementCancelledDelegate(EasyGridBuilderPro easyGridBuilderPro);

        public event OnFreeObjectSplinePlacementStartedAndUpdatedDelegate OnFreeObjectSplinePlacementStartedAndUpdated;
        public delegate void OnFreeObjectSplinePlacementStartedAndUpdatedDelegate(EasyGridBuilderPro easyGridBuilderPro, Vector3 worldPosition, FreeObjectPlacementType placementType);
        public event OnFreeObjectSplinePlacementFinalizedDelegate OnFreeObjectSplinePlacementFinalized;
        public delegate void OnFreeObjectSplinePlacementFinalizedDelegate(EasyGridBuilderPro easyGridBuilderPro);
        public event OnFreeObjectSplinePlacementCancelledDelegate OnFreeObjectSplinePlacementCancelled;
        public delegate void OnFreeObjectSplinePlacementCancelledDelegate(EasyGridBuilderPro easyGridBuilderPro);
        
        [SerializeField] private LayerMask gridSystemLayerMask;
        [SerializeField] private LayerMask customSurfaceLayerMask;
        [SerializeField] private CameraMode activeCameraMode;

        [Space]
        [SerializeField] protected private bool enableDistanceBasedBuilding = false;
        [SerializeField] protected private bool autoDisableInTopDownMode = false;
        [SerializeField] protected private Transform distanceCheckObject;
        [SerializeField] protected private float minimumDistance;
        [SerializeField] protected private float maximumDistance;

        [Space]
        [SerializeField] private BuildableGridObjectGhost buildableGridObjectGhost;
        [SerializeField] private BuildableEdgeObjectGhost buildableEdgeObjectGhost;
        [SerializeField] private BuildableCornerObjectGhost buildableCornerObjectGhost;
        [SerializeField] private BuildableFreeObjectGhost buildableFreeObjectGhost;
        [SerializeField] private BuildableObjectSelector buildableObjectSelector;
        [SerializeField] private BuildableObjectDestroyer buildableObjectDestroyer;
        [SerializeField] private BuildableObjectMover buildableObjectMover;

        [SerializeField] private int maxUndoRedoCount = 100;
        [SerializeField] private int maxBoxPlacementObjects = 500;

        [Space]
        [SerializeField] private bool enableEditorSafetyMessages = true;
        [SerializeField] private bool enableConsoleMessages;
        [Serializable] public class ConsoleMessagesSettings
        {
            [SerializeField] public bool onGridSystemCreation;
            [SerializeField] public bool onActiveCameraModeChange;
            [SerializeField] public bool onActiveGridModeChange;
            [SerializeField] public bool onActiveBuildableSOChange;
            [SerializeField] public bool onActiveVerticalGridChange;
            [SerializeField] public bool onCellObjectValueChange;
            [SerializeField] public bool onBuildableObjectPlacement;
            [SerializeField] public bool onBuildableObjectSOAdded;
            [SerializeField] public bool onBuildableObjectSORemoved;
        }
        [SerializeField] private ConsoleMessagesSettings consoleMessagesSettings;

        ///-------------------------------------------------------------------------------///
        /// GRID MANAGER RELATED UNITY EVENTS                                             ///
        ///-------------------------------------------------------------------------------///
        
        [Space]
        [SerializeField] private bool enableUnityEvents;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, CameraMode> OnActiveCameraModeChangedUnityEvent;

        ///-------------------------------------------------------------------------------///
        /// EASY GRID BUILDER PRO SYSTEMS RELATED UNITY EVENTS                            ///
        ///-------------------------------------------------------------------------------///

        [SerializeField] public UnityEvent<EasyGridBuilderPro> OnGridSystemCreatedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, GridMode> OnActiveGridModeChangedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, BuildableObjectSO> OnActiveBuildableSOChangedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, int> OnActiveVerticalGridChangedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, Vector2Int> OnCellObjectValueChangedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, BuildableObject> OnBuildableObjectPlacedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, BuildableObjectSO> OnBuildableObjectSOAddedUnityEvent;
        [SerializeField] public UnityEvent<EasyGridBuilderPro, BuildableObjectSO> OnBuildableObjectSORemovedUnityEvent;

        private List<EasyGridBuilderPro> easyGridBuilderProSystemsList = new List<EasyGridBuilderPro>();
        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private bool isMousePointerOnGrid;

        private GridCommandInvoker gridCommandInvoker;
        private GridBuiltObjectsManager gridBuiltObjectsManager;
        private GridSaveAndLoadManager gridSaveAndLoadManager;
        private GridHeatMapManager gridHeatMapManager;
        private GridAreaDisablerManager gridAreaDisablerManager;
        private GridAreaEnablerManager gridAreaEnablerManager;

        private void Awake()
        {
            Instance = this;
            gridCommandInvoker = new GridCommandInvoker();
            EasyGridBuilderPro.OnGridSystemCreated += OnGridSystemCreated;
        }
        
        private void Start()
        {
            if (TryGetComponent<GridBuiltObjectsManager>(out GridBuiltObjectsManager gridBuiltObjectsManager)) this.gridBuiltObjectsManager = gridBuiltObjectsManager;
            if (TryGetComponent<GridSaveAndLoadManager>(out GridSaveAndLoadManager gridSaveAndLoadManager)) this.gridSaveAndLoadManager = gridSaveAndLoadManager;
            if (TryGetComponent<GridHeatMapManager>(out GridHeatMapManager gridHeatMapManager)) this.gridHeatMapManager = gridHeatMapManager;
            if (TryGetComponent<GridAreaDisablerManager>(out GridAreaDisablerManager gridAreaDisablerManager)) this.gridAreaDisablerManager = gridAreaDisablerManager;
            if (TryGetComponent<GridAreaEnablerManager>(out GridAreaEnablerManager gridAreaEnablerManager)) this.gridAreaEnablerManager = gridAreaEnablerManager;
        }

        private void OnDestroy()
        {
            EasyGridBuilderPro.OnGridSystemCreated -= OnGridSystemCreated;
        }

        /// Adds a new grid system to the manager when created.
        private void OnGridSystemCreated(EasyGridBuilderPro easyGridBuilderPro)
        {
            if (!easyGridBuilderProSystemsList.Contains(easyGridBuilderPro)) easyGridBuilderProSystemsList.Add(easyGridBuilderPro);
            if (activeEasyGridBuilderPro == null) SetActiveGridSystem(easyGridBuilderProSystemsList[0]);
        }

        private void Update()
        {
            DetectActiveGridSystem();
        }

        /// Detects and sets the active grid system based on the mouse position.
        private void DetectActiveGridSystem()
        {
            EasyGridBuilderPro currentGridSystem = GetCurrentGridSystemBelowMousePoint();
            if (currentGridSystem != null && activeEasyGridBuilderPro != currentGridSystem)
            {
                SetActiveGridSystem(currentGridSystem);
            }
        }

        /// Retrieves the grid system currently under the mouse cursor.
        private EasyGridBuilderPro GetCurrentGridSystemBelowMousePoint()
        {
            Ray ray = Camera.main.ScreenPointToRay(MouseInteractionUtilities.GetCurrentMousePosition());
            bool previousIsMousePointerOnGrid = isMousePointerOnGrid;

            EasyGridBuilderPro easyGridBuilderPro = MouseInteractionUtilities.GetEasyGridBuilderProWithCustomSurface(false, customSurfaceLayerMask, gridSystemLayerMask, Vector3.down * 9999);
            easyGridBuilderPro ??= MouseInteractionUtilities.GetEasyGridBuilderProWithCustomSurface(true, customSurfaceLayerMask, gridSystemLayerMask, Vector3.down * 9999);
            
            if (easyGridBuilderPro)
            {
                isMousePointerOnGrid = easyGridBuilderPro != null;
                if (isMousePointerOnGrid != previousIsMousePointerOnGrid)
                {
                    OnIsMousePointerOnGridChanged?.Invoke(isMousePointerOnGrid);
                }
                return easyGridBuilderPro;
            }

            isMousePointerOnGrid = false;
            if (isMousePointerOnGrid != previousIsMousePointerOnGrid)
            {
                OnIsMousePointerOnGridChanged?.Invoke(isMousePointerOnGrid);
            }

            return activeEasyGridBuilderPro; // Return current active system if no new system is under mouse point
        }
        ///-------------------------------------------------------------------------------///
        /// RUNTIME GRID INSTANTIATION FUNCTIONS                                          ///
        ///-------------------------------------------------------------------------------///
        
        #region Grid System Instantiate Functions Start:
        public void InstantiateGridSystem(EasyGridBuilderPro gridSystemPrefab, Vector3 worldPosition, out EasyGridBuilderPro easyGridBuilderPro)
        {
            easyGridBuilderPro = Instantiate(gridSystemPrefab, worldPosition, Quaternion.identity);
            
            #if UNITY_EDITOR
            easyGridBuilderPro.GenerateGridUniqueID(true);
            #endif
        }

        public void InstantiateGridSystem(GameObject gridSystemPrefab, Vector3 worldPosition, out EasyGridBuilderPro easyGridBuilderPro)
        {
            easyGridBuilderPro = default;
            if (gridSystemPrefab.TryGetComponent<EasyGridBuilderPro>(out EasyGridBuilderPro gridSystem)) InstantiateGridSystem(gridSystem, worldPosition, out easyGridBuilderPro);
        }
        #endregion Grid System Instantiate Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID PRIMARY EVENT RAISER FUNCTIONS                                           ///
        ///-------------------------------------------------------------------------------///

        #region Grid Primary Event Raiser Functions Start:
        public void RaiseOnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode activeGridMode)
        {
            OnActiveGridModeChanged?.Invoke(easyGridBuilderPro, activeGridMode);
        }

        public void RaiseOnActiveBuildableSOChanged(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            OnActiveBuildableSOChanged?.Invoke(easyGridBuilderPro, buildableObjectSO);
        }

        public void RaiseOnActiveVerticalGridChanged(EasyGridBuilderPro easyGridBuilderPro, int activeVerticalGridIndex)
        {
            OnActiveVerticalGridChanged?.Invoke(easyGridBuilderPro, activeVerticalGridIndex);
        }

        public void RaiseOnCellObjectValueChanged(EasyGridBuilderPro easyGridBuilderPro, Vector2Int cellPosition)
        {
            OnCellObjectValueChanged?.Invoke(easyGridBuilderPro, cellPosition);
        }

        public void RaiseOnBuildableObjectPlaced(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject)
        {
            OnBuildableObjectPlaced?.Invoke(easyGridBuilderPro, buildableObject);
        }

        public void RaiseOnBuildableObjectSOAdded(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            OnBuildableObjectSOAdded?.Invoke(easyGridBuilderPro, buildableObjectSO);
        }

        public void RaiseOnBuildableObjectSORemoved(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            OnBuildableObjectSORemoved?.Invoke(easyGridBuilderPro, buildableObjectSO);
        }
        #endregion Grid Primary Event Raiser Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID INTERNAL SUPPORTER EVENT RAISER FUNCTIONS                                ///
        ///-------------------------------------------------------------------------------///

        #region Grid Internal Supporter Event Raiser Functions Start:
        public void RaiseOnGridObjectBoxPlacementStarted(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, GridObjectPlacementType placementType)
        {
            OnGridObjectBoxPlacementStarted?.Invoke(easyGridBuilderPro, boxPlacementStartPosition, placementType);
        }

        public void RaiseOnGridObjectBoxPlacementUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            OnGridObjectBoxPlacementUpdated?.Invoke(easyGridBuilderPro, boxPlacementEndPosition);
        }

        public void RaiseOnGridObjectBoxPlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnGridObjectBoxPlacementFinalized?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnGridObjectBoxPlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnGridObjectBoxPlacementCancelled?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnEdgeObjectBoxPlacementStarted(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, EdgeObjectPlacementType placementType)
        {
            OnEdgeObjectBoxPlacementStarted?.Invoke(easyGridBuilderPro, boxPlacementStartPosition, placementType);
        }

        public void RaiseOnEdgeObjectBoxPlacementUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            OnEdgeObjectBoxPlacementUpdated?.Invoke(easyGridBuilderPro, boxPlacementEndPosition);
        }

        public void RaiseOnEdgeObjectBoxPlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnEdgeObjectBoxPlacementFinalized?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnEdgeObjectBoxPlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnEdgeObjectBoxPlacementCancelled?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnCornerObjectBoxPlacementStarted(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, CornerObjectPlacementType placementType)
        {
            OnCornerObjectBoxPlacementStarted?.Invoke(easyGridBuilderPro, boxPlacementStartPosition, placementType);
        }

        public void RaiseOnCornerObjectBoxPlacementUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            OnCornerObjectBoxPlacementUpdated?.Invoke(easyGridBuilderPro, boxPlacementEndPosition);
        }

        public void RaiseOnCornerObjectBoxPlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnCornerObjectBoxPlacementFinalized?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnCornerObjectBoxPlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnCornerObjectBoxPlacementCancelled?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnFreeObjectSplinePlacementStartedAndUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition, FreeObjectPlacementType freeObjectPlacementType)
        {
            OnFreeObjectSplinePlacementStartedAndUpdated?.Invoke(easyGridBuilderPro, boxPlacementEndPosition, freeObjectPlacementType);
        }

        public void RaiseOnFreeObjectSplinePlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnFreeObjectSplinePlacementFinalized?.Invoke(easyGridBuilderPro);
        }

        public void RaiseOnFreeObjectSplinePlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            OnFreeObjectSplinePlacementCancelled?.Invoke(easyGridBuilderPro);
        }
        #endregion Grid Internal Supporter Event Raiser Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///

        /// <summary>
        /// Sets and notifies changes to the active grid system.
        /// </summary>
        /// <param name="gridSystem">The new active grid system.</param>
        public void SetActiveGridSystem(EasyGridBuilderPro gridSystem)
        {
            activeEasyGridBuilderPro = gridSystem;
            OnActiveEasyGridBuilderProChanged?.Invoke(activeEasyGridBuilderPro);
        }

        public void SetActiveCameraMode(CameraMode cameraMode)
        {
            if (activeCameraMode == cameraMode) return;

            switch (cameraMode)
            {
                case CameraMode.TopDown: 
                    activeCameraMode = CameraMode.TopDown;
                    if (buildableObjectDestroyer) buildableObjectDestroyer.SetDestroyMode(DestroyMode.IndividualAndArea);
                    if (buildableObjectSelector) buildableObjectSelector.SetSelectionMode(SelectionMode.IndividualAndArea);
                break;
                case CameraMode.ThirdPerson: 
                    activeCameraMode = CameraMode.ThirdPerson; 
                    if (buildableObjectDestroyer) buildableObjectDestroyer.SetDestroyMode(DestroyMode.Individual);
                    if (buildableObjectSelector) buildableObjectSelector.SetSelectionMode(SelectionMode.Individual);
                break;
            }
            
            if (enableConsoleMessages && consoleMessagesSettings.onActiveCameraModeChange) Debug.Log($"Grid Manager: <color=green><b>Active Camera Mode Changed!</b></color> {activeCameraMode}");
            OnActiveCameraModeChanged?.Invoke(activeCameraMode);
        }

        public void SetIsEnableDistanceBasedBuilding(bool enableDistanceBasedBuilding) => this.enableDistanceBasedBuilding = enableDistanceBasedBuilding;

        public void SetIsAutoDisableInTopDownMode(bool autoDisableInTopDownMode) => this.autoDisableInTopDownMode = autoDisableInTopDownMode;

        public void SetDistanceCheckObject(Transform distanceCheckObject) => this.distanceCheckObject = distanceCheckObject;

        public void SetMinimumDistance(float minimumDistance) => this.minimumDistance = minimumDistance;

        public void SetMaximumDistance(float maximumDistance) => this.maximumDistance = maximumDistance;


        public void SetActiveGridModeInAllGrids(GridMode gridMode, bool isAToggle = false)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in easyGridBuilderProSystemsList)
            {
                easyGridBuilderPro.SetActiveGridMode(gridMode, isAToggle);
            }
        }

        public void SetIsObjectMovingInAllGrids(bool isObjectMoving)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in easyGridBuilderProSystemsList)
            {
                easyGridBuilderPro.SetIsObjectMoving(isObjectMoving);
            }
        }

        public void SetMovingBuildableObjectInAllGrids(BuildableObject buildableObject)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in easyGridBuilderProSystemsList)
            {
                easyGridBuilderPro.SetMovingBuildableObject(buildableObject);
            }
        }

        /// <summary>
        /// Sets whether console messages are enabled.
        /// </summary>
        /// <param name="enableConsoleMessages">True to enable console messages, false otherwise.</param>
        public void SetEnableConsoleMessages(bool enableConsoleMessages) => this.enableConsoleMessages = enableConsoleMessages;

        /// <summary>
        /// Sets the settings for console messages.
        /// </summary>
        /// <param name="consoleMessagesSettings">The settings for console messages.</param>
        public void SetConsoleMessagesSettings(ConsoleMessagesSettings consoleMessagesSettings) => this.consoleMessagesSettings = consoleMessagesSettings;

        public void SetUndoCommandLinkedList(LinkedList<ICommand> undoCommandList)
        {
            if (gridCommandInvoker != null) gridCommandInvoker.SetUndoCommandLinkedList(undoCommandList);
        }

        public void ClearUndoCommandLinkedList()
        {
            if (gridCommandInvoker != null) gridCommandInvoker.ClearUndoCommandLinkedList();
        }

        public void SetRedoCommandStack(Stack<ICommand> redoCommandStack)
        {
            if (gridCommandInvoker != null) gridCommandInvoker.SetRedoCommandStack(redoCommandStack);
        }

        public void ClearRedoCommandStack()
        {
            if (gridCommandInvoker != null) gridCommandInvoker.ClearRedoCommandStack();
        }

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        public GridCommandInvoker GetGridCommandInvoker() => gridCommandInvoker;

        public bool TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager)
        {
            gridBuiltObjectsManager = this.gridBuiltObjectsManager;
            return gridBuiltObjectsManager ? true : false;
        }

        public bool TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)
        {
            gridSaveAndLoadManager = this.gridSaveAndLoadManager;
            return gridSaveAndLoadManager ? true : false;
        }

        public bool TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)
        {
            gridHeatMapManager = this.gridHeatMapManager;
            return gridHeatMapManager ? true : false;
        }

        public bool TryGetGridAreaDisablerManager(out GridAreaDisablerManager gridAreaDisablerManager)
        {
            gridAreaDisablerManager = this.gridAreaDisablerManager;
            return gridAreaDisablerManager ? true : false;
        }

        public bool TryGetGridAreaEnablerManager(out GridAreaEnablerManager gridAreaEnablerManager)
        {
            gridAreaEnablerManager = this.gridAreaEnablerManager;
            return gridAreaEnablerManager ? true : false;
        }

        public bool TryGetBuildableGridObjectGhost(out BuildableGridObjectGhost buildableGridObjectGhost)
        {
            buildableGridObjectGhost = this.buildableGridObjectGhost;
            return buildableGridObjectGhost ? true : false;
        }

        public bool TryGetBuildableEdgeObjectGhost(out BuildableEdgeObjectGhost buildableEdgeObjectGhost)
        {
            buildableEdgeObjectGhost = this.buildableEdgeObjectGhost;
            return buildableEdgeObjectGhost ? true : false;
        }

        public bool TryGetBuildableCornerObjectGhost(out BuildableCornerObjectGhost buildableCornerObjectGhost)
        {
            buildableCornerObjectGhost = this.buildableCornerObjectGhost;
            return buildableCornerObjectGhost ? true : false;
        }

        public bool TryGetBuildableFreeObjectGhost(out BuildableFreeObjectGhost buildableFreeObjectGhost)
        {
            buildableFreeObjectGhost = this.buildableFreeObjectGhost;
            return buildableFreeObjectGhost ? true : false;
        }

        public bool TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)
        {
            buildableObjectSelector = this.buildableObjectSelector;
            return buildableObjectSelector ? true : false;
        }

        public bool TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)
        {
            buildableObjectDestroyer = this.buildableObjectDestroyer;
            return buildableObjectDestroyer ? true : false;
        }

        public bool TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover)
        {
            buildableObjectMover = this.buildableObjectMover;
            return buildableObjectMover ? true : false;
        }
        
        public EasyGridBuilderPro GetActiveEasyGridBuilderPro() => activeEasyGridBuilderPro;

        public List<EasyGridBuilderPro> GetEasyGridBuilderProSystemsList() => easyGridBuilderProSystemsList;

        public LayerMask GetGridSystemLayerMask() => gridSystemLayerMask;

        public CameraMode GetActiveCameraMode() => activeCameraMode;


        public bool GetIsEnableDistanceBasedBuilding() => enableDistanceBasedBuilding;

        public bool GetIsAutoDisableInTopDownMode() => autoDisableInTopDownMode;

        public Transform GetDistanceCheckObject() => distanceCheckObject;

        public float GetMinimumDistance() => minimumDistance;

        public float GetMaximumDistance() => maximumDistance;

        
        public bool GetIsMousePointerOnGrid() => isMousePointerOnGrid;

        public int GetMaxBoxPlacementObjects() => maxBoxPlacementObjects;

        public int GetMaxUndoRedoCount() => maxUndoRedoCount;

        public bool GetIsEnableEditorSafetyMessages() => enableEditorSafetyMessages;

        public bool GetIsEnableConsoleMessages() => enableConsoleMessages;

        public ConsoleMessagesSettings GetConsoleMessagesSettings() => consoleMessagesSettings;

        public bool GetIsEnableUnityEvents() => enableUnityEvents;


        public LinkedList<ICommand> GetUndoCommandLinkedList()
        {
            if (gridCommandInvoker != null) return gridCommandInvoker.GetUndoCommandLinkedList();
            else return null;
        }

        public Stack<ICommand> GetRedoCommandStack()
        {
            if (gridCommandInvoker != null) return gridCommandInvoker.GetRedoCommandStack();
            else return null;
        }
    }
}