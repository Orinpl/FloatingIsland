using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Interactions;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Input Manager", 2)]
    [RequireComponent(typeof(GridManager))]
    public class GridInputManager : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActionsAsset;
        [SerializeField] private List<string> actionsToBlockWhenPointerOverUI;

        // [Space]
        // [SerializeField] private bool autoPlaceOnBuildableListSelection = true;
        // [SerializeField] private bool autoPlaceUseRandomUnoccupiedCell = false;

        [Space]
        [SerializeField] private bool useBuildModeActivationInput = true;
        [SerializeField] private bool setThisBuildModeInputAsAToggle = true;
        [Space]
        [SerializeField] private bool useDestroyModeActivationInput = true;
        [SerializeField] private bool setThisDestroyModeInputAsAToggle = true;
        [Space]
        [SerializeField] private bool useSelectModeActivationInput = true;
        [SerializeField] private bool setThisSelectModeInputAsAToggle = true;
        [Space]
        [SerializeField] private bool useMoveModeActivationInput = true;
        [SerializeField] private bool setThisMoveModeInputAsAToggle = true;
        [SerializeField] private bool placeMovingObjectOnInputRelease = false;
        [Space]
        [SerializeField] private bool splineCompleteUsePlacementResetAction = true;

        private List<(InputAction.CallbackContext context, Action action)> pendingActions;
        private bool isApplicationFocused;
        private const float FOCUSED_APPLICATION_INPUT_ENABLE_DELAY = 0.1f;

        private float doubleTapTime = 0.3f;                     // Max time between taps for double-tap
        private float buildLastTapTime = -1f;                   // Time of the last tap for building
        private bool isBuildSecondTapInProgress = false;        // Tracks if the second tap is ongoing for building
        private float destroyLastTapTime = -1f;                 // Time of the last tap for destroying
        private bool isDestroySecondTapInProgress = false;      // Tracks if the second tap is ongoing for destroying
        private float selectLastTapTime = -1f;                  // Time of the last tap for selecting
        private bool isSelectSecondTapInProgress = false;       // Tracks if the second tap is ongoing for selecting
        private float moveLastTapTime = -1f;                    // Time of the last tap for moving
        private bool isMoveSecondTapInProgress = false;         // Tracks if the second tap is ongoing for moving
        private float heatMapPaintLastTapTime = -1f;            // Time of the last tap for heat map painting
        private bool isHeatMapPaintSecondTapInProgress = false; // Tracks if the second tap is ongoing for heat map painting

        private InputActionMap coreGridActions;
        private InputActionMap buildActions;
        private InputActionMap destroyActions;
        private InputActionMap selectActions;
        private InputActionMap moveActions;
        private InputActionMap heatMapActions;

        private const string CORE_GRID_ACTIONS = "Core Grid Actions";
        private const string BUILD_ACTIONS = "Build Actions";
        private const string DESTROY_ACTIONS = "Destroy Actions";
        private const string SELECT_ACTIONS = "Select Actions";
        private const string MOVE_ACTIONS = "Move Actions";
        private const string HEAT_MAP_ACTIONS = "Heat Map Actions";

        private const string GRID_MODE_RESET = "Grid Mode Reset";
        private const string PLACEMENT_AND_SELECTION_RESET = "Placement And Selection Reset";
        private const string VERTICAL_GRID_SCROLL = "Vertical Grid Scroll";
        private const string VERTICAL_GRID_MOVE_UP = "Vertical Grid Move Up";
        private const string VERTICAL_GRID_MOVE_DOWN = "Vertical Grid Move Down";
        private const string UNDO = "Undo";
        private const string REDO = "Redo";
        private const string GRID_SAVE = "Grid Save";
        private const string GRID_LOAD = "Grid Load";

        private const string BUILD_MODE_ACTIVATION = "Build Mode Activation";
        private const string BUILD = "Build";
        private const string BUILDABLE_LIST_SCROLL = "Buildable List Scroll";
        private const string BUILDABLE_LIST_MOVE_UP = "Buildable List Move Up";
        private const string BUILDABLE_LIST_MOVE_DOWN = "Buildable List Move Down";
        private const string ACTIVE_BUILDABLE_PLACEMENT_TYPE_SCROLL = "Active Buildable Placement Type Scroll";
        private const string ACTIVE_BUILDABLE_PLACEMENT_TYPE_MOVE_UP = "Active Buildable Placement Type Move Up";
        private const string ACTIVE_BUILDABLE_PLACEMENT_TYPE_MOVE_DOWN = "Active Buildable Placement Type Move Down";
        private const string BUILDABLE_ROTATE_SCROLL = "Buildable Rotate Scroll";
        private const string BUILDABLE_ROTATE_CLOCKWISE = "Buildable Rotate Clockwise";
        private const string BUILDABLE_ROTATE_COUNTER_CLOCKWISE = "Buildable Rotate Counter Clockwise";
        private const string BUILDABLE_EDGE_OBJECT_FLIP = "Buildable Edge Object Flip";
        private const string BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_COMPLETE = "Buildable Free Object Spline Placement Complete";
        private const string BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_SCROLL = "Buildable Free Object Spline Placement Space Scroll";
        private const string BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_MOVE_UP = "Buildable Free Object Spline Placement Space Move Up";
        private const string BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_MOVE_DOWN = "Buildable Free Object Spline Placement Space Move Down";

        private const string DESTROY_MODE_ACTIVATION = "Destroy Mode Activation";
        private const string DESTROY = "Destroy";

        private const string SELECT_MODE_ACTIVATION = "Select Mode Activation";
        private const string SELECT = "Select";
        private const string MULTI_SELECTION_HOLD = "Multi Selection Hold";
        private const string SELECTION_DESTROY = "Selection Destroy";
        private const string SELECTION_COPY = "Selection Copy";
        private const string SELECTION_MOVE = "Selection Move";

        private const string MOVE_MODE_ACTIVATION = "Move Mode Activation";
        private const string MOVE = "Move";

        private const string HEAT_MAP_MODE_TOGGLE = "Heat Map Mode Toggle";
        private const string HEAT_MAP_MODE_ENABLE = "Heat Map Mode Enable";
        private const string HEAT_MAP_MODE_DISABLE = "Heat Map Mode Disable";
        private const string HEAT_MAP_SWITCH_SCROLL = "Heat Map Switch Scroll";
        private const string HEAT_MAP_SWITCH_MOVE_UP = "Heat Map Switch Move Up";
        private const string HEAT_MAP_SWITCH_MOVE_DOWN = "Heat Map Switch Move Down";
        private const string HEAT_MAP_BRUSH_SIZE_SCROLL = "Heat Map Brush Size Scroll";
        private const string HEAT_MAP_BRUSH_SIZE_MOVE_UP = "Heat Map Brush Size Move Up";
        private const string HEAT_MAP_BRUSH_SIZE_MOVE_DOWN = "Heat Map Brush Size Move Down";
        private const string HEAT_MAP_PAINT = "Heat Map Paint";
        private const string HEAT_MAP_READ = "Heat Map Read";

        ///-------------------------------------------------------------------------------///
        /// INPUT INITIALIZE FUNCTIONS                                                    ///
        ///-------------------------------------------------------------------------------///

        private void Awake()
        {
            coreGridActions = inputActionsAsset.FindActionMap(CORE_GRID_ACTIONS);
            buildActions = inputActionsAsset.FindActionMap(BUILD_ACTIONS);
            destroyActions = inputActionsAsset.FindActionMap(DESTROY_ACTIONS);
            selectActions = inputActionsAsset.FindActionMap(SELECT_ACTIONS);
            moveActions = inputActionsAsset.FindActionMap(MOVE_ACTIONS);
            heatMapActions = inputActionsAsset.FindActionMap(HEAT_MAP_ACTIONS);
        }

        private void OnEnable()
        {
            inputActionsAsset.Enable();
            InvokeSubscribeInputActionEvents();
        }

        private void Start()
        {
            pendingActions = new List<(InputAction.CallbackContext, Action)>();
            EasyGridBuilderProXZ.OnGridSystemCreated += OnGridSystemCreated;

            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                SetEasyGridBuilderProList(easyGridBuilderPro);
            }
        }

        private void OnDisable()
        {
            inputActionsAsset.Disable();
            InvokeUnsubscribeInputActionEvents();
        }

        private void OnDestroy()
        {
            EasyGridBuilderProXZ.OnGridSystemCreated -= OnGridSystemCreated;
        }

        #region Input Initialization Functions Start:
        private void OnGridSystemCreated(EasyGridBuilderPro easyGridBuilderPro)
        {
            SetEasyGridBuilderProList(easyGridBuilderPro);
        }

        private void SetEasyGridBuilderProList(EasyGridBuilderPro easyGridBuilderPro)
        {
            easyGridBuilderPro.SetInputGridModeVariables(useBuildModeActivationInput, useDestroyModeActivationInput, useSelectModeActivationInput, useMoveModeActivationInput);
        }

        private void InvokeSubscribeInputActionEvents()
        {
            SubscribeInputActionEvents(
                (coreGridActions.FindAction(GRID_MODE_RESET), null, GridModeResetActionPerformed, null),
                (coreGridActions.FindAction(PLACEMENT_AND_SELECTION_RESET), null, PlacementAndSelectionResetActionPerformed, null),
                (coreGridActions.FindAction(VERTICAL_GRID_SCROLL), null, VerticalGridScrollActionPerformed, null),
                (coreGridActions.FindAction(VERTICAL_GRID_MOVE_UP), null, VerticalGridMoveUpActionPerformed, null),
                (coreGridActions.FindAction(VERTICAL_GRID_MOVE_DOWN), null, VerticalGridMoveDownActionPerformed, null),
                (coreGridActions.FindAction(UNDO), null, UndoActionPerformed, null),
                (coreGridActions.FindAction(REDO), null, RedoActionPerformed, null),
                (coreGridActions.FindAction(GRID_SAVE), null, GridSaveActionPerformed, null),
                (coreGridActions.FindAction(GRID_LOAD), null, GridLoadActionPerformed, null),

                (buildActions.FindAction(BUILD_MODE_ACTIVATION), null, BuildModeActivationActionPerformed, null),
                (buildActions.FindAction(BUILD), BuildActionStarted, BuildActionPerformed, BuildActionCancelled),
                (buildActions.FindAction(BUILDABLE_LIST_SCROLL), null, BuildableListScrollActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_LIST_MOVE_UP), null, BuildableListMoveUpActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_LIST_MOVE_DOWN), null, BuildableListMoveDownActionPerformed, null),
                (buildActions.FindAction(ACTIVE_BUILDABLE_PLACEMENT_TYPE_SCROLL), null, BuildablePlacementTypeScrollActionPerformed, null),
                (buildActions.FindAction(ACTIVE_BUILDABLE_PLACEMENT_TYPE_MOVE_UP), null, BuildablePlacementTypeMoveUpActionPerformed, null),
                (buildActions.FindAction(ACTIVE_BUILDABLE_PLACEMENT_TYPE_MOVE_DOWN), null, BuildablePlacementTypeMoveDownActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_ROTATE_SCROLL), null, BuildableRotateScrollActionPerformed, BuildableRotateScrollActionCancelled),
                (buildActions.FindAction(BUILDABLE_ROTATE_CLOCKWISE), null, BuildableRotateClockwiseActionPerformed, BuildableRotateClockwiseActionCancelled),
                (buildActions.FindAction(BUILDABLE_ROTATE_COUNTER_CLOCKWISE), null, BuildableRotateCounterClockwiseActionPerformed, BuildableRotateCounterClockwiseActionCancelled),
                (buildActions.FindAction(BUILDABLE_EDGE_OBJECT_FLIP), null, BuildableEdgeObjectFlipActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_COMPLETE), null, BuildableFreeObjectSplinePlacementCompleteActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_SCROLL), null, BuildableFreeObjectSplinePlacementSpaceScrollActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_MOVE_UP), null, BuildableFreeObjectSplinePlacementSpaceMoveUpActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_MOVE_DOWN), null, BuildableFreeObjectSplinePlacementSpaceMoveDownActionPerformed, null),

                (destroyActions.FindAction(DESTROY_MODE_ACTIVATION), null, DestroyModeActivationActionPerformed, null),
                (destroyActions.FindAction(DESTROY), DestroyActionStarted, DestroyActionPerformed, DestroyActionCancelled),

                (selectActions.FindAction(SELECT_MODE_ACTIVATION), null, SelectModeActivationActionPerformed, null),
                (selectActions.FindAction(SELECT), SelectActionStarted, SelectActionPerformed, SelectActionCancelled),
                (selectActions.FindAction(MULTI_SELECTION_HOLD), null, MultiSelectionHoldActionPerformed, MultiSelectionHoldActionCancelled),
                (selectActions.FindAction(SELECTION_DESTROY), null, SelectionDestroyActionPerformed, null),
                (selectActions.FindAction(SELECTION_COPY), null, SelectionCopyActionPerformed, null),
                (selectActions.FindAction(SELECTION_MOVE), null, SelectionMoveActionPerformed, null),

                (moveActions.FindAction(MOVE_MODE_ACTIVATION), null, MoveModeActivationActionPerformed, null),
                (moveActions.FindAction(MOVE), MoveActionStarted, MoveActionPerformed, MoveActionCancelled),

                (heatMapActions.FindAction(HEAT_MAP_MODE_TOGGLE), null, HeatMapModeToggleActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_MODE_ENABLE), null, HeatMapModeEnableActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_MODE_DISABLE), null, HeatMapModeDisableActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_SWITCH_SCROLL), null, HeatMapSwitchScrollActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_SWITCH_MOVE_UP), null, HeatMapSwitchMoveUpActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_SWITCH_MOVE_DOWN), null, HeatMapSwitchMoveDownActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_BRUSH_SIZE_SCROLL), null, HeatMapBrushSizeScrollActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_BRUSH_SIZE_MOVE_UP), null, HeatMapBrushSizeMoveUpActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_BRUSH_SIZE_MOVE_DOWN), null, HeatMapBrushSizeMoveDownActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_PAINT), HeatMapPaintActionStarted, HeatMapPaintActionPerformed, HeatMapPaintActionCancelled),
                (heatMapActions.FindAction(HEAT_MAP_READ), null, HeatMapReadActionPerformed, null)
            );
        }

        private void SubscribeInputActionEvents(params (InputAction inputAction, Action<InputAction.CallbackContext> startedAction, Action<InputAction.CallbackContext> performedAction, Action<InputAction.CallbackContext> cancelledAction)[] actions)
        {
            foreach ((InputAction inputAction, Action<InputAction.CallbackContext> startedAction, Action<InputAction.CallbackContext> performedAction, Action<InputAction.CallbackContext> cancelledAction) actionTuple in actions)
            {
                if (actionTuple.inputAction != null)
                {
                    if (actionTuple.startedAction != null) actionTuple.inputAction.started += actionTuple.startedAction;
                    if (actionTuple.performedAction != null) actionTuple.inputAction.performed += actionTuple.performedAction;
                    if (actionTuple.cancelledAction != null) actionTuple.inputAction.canceled += actionTuple.cancelledAction;
                }
            }
        }
        
        private void InvokeUnsubscribeInputActionEvents()
        {
            SubscribeInputActionEvents(
                (coreGridActions.FindAction(GRID_MODE_RESET), null, GridModeResetActionPerformed, null),
                (coreGridActions.FindAction(PLACEMENT_AND_SELECTION_RESET), null, PlacementAndSelectionResetActionPerformed, null),
                (coreGridActions.FindAction(VERTICAL_GRID_SCROLL), null, VerticalGridScrollActionPerformed, null),
                (coreGridActions.FindAction(VERTICAL_GRID_MOVE_UP), null, VerticalGridMoveUpActionPerformed, null),
                (coreGridActions.FindAction(VERTICAL_GRID_MOVE_DOWN), null, VerticalGridMoveDownActionPerformed, null),
                (coreGridActions.FindAction(UNDO), null, UndoActionPerformed, null),
                (coreGridActions.FindAction(REDO), null, RedoActionPerformed, null),
                (coreGridActions.FindAction(GRID_SAVE), null, GridSaveActionPerformed, null),
                (coreGridActions.FindAction(GRID_LOAD), null, GridLoadActionPerformed, null),

                (buildActions.FindAction(BUILD_MODE_ACTIVATION), null, BuildModeActivationActionPerformed, null),
                (buildActions.FindAction(BUILD), BuildActionStarted, BuildActionPerformed, BuildActionCancelled),
                (buildActions.FindAction(BUILDABLE_LIST_SCROLL), null, BuildableListScrollActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_LIST_MOVE_UP), null, BuildableListMoveUpActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_LIST_MOVE_DOWN), null, BuildableListMoveDownActionPerformed, null),
                (buildActions.FindAction(ACTIVE_BUILDABLE_PLACEMENT_TYPE_SCROLL), null, BuildablePlacementTypeScrollActionPerformed, null),
                (buildActions.FindAction(ACTIVE_BUILDABLE_PLACEMENT_TYPE_MOVE_UP), null, BuildablePlacementTypeMoveUpActionPerformed, null),
                (buildActions.FindAction(ACTIVE_BUILDABLE_PLACEMENT_TYPE_MOVE_DOWN), null, BuildablePlacementTypeMoveDownActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_ROTATE_SCROLL), null, BuildableRotateScrollActionPerformed, BuildableRotateScrollActionCancelled),
                (buildActions.FindAction(BUILDABLE_ROTATE_CLOCKWISE), null, BuildableRotateClockwiseActionPerformed, BuildableRotateClockwiseActionCancelled),
                (buildActions.FindAction(BUILDABLE_ROTATE_COUNTER_CLOCKWISE), null, BuildableRotateCounterClockwiseActionPerformed, BuildableRotateCounterClockwiseActionCancelled),
                (buildActions.FindAction(BUILDABLE_EDGE_OBJECT_FLIP), null, BuildableEdgeObjectFlipActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_COMPLETE), null, BuildableFreeObjectSplinePlacementCompleteActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_SCROLL), null, BuildableFreeObjectSplinePlacementSpaceScrollActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_MOVE_UP), null, BuildableFreeObjectSplinePlacementSpaceMoveUpActionPerformed, null),
                (buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_SPACE_MOVE_DOWN), null, BuildableFreeObjectSplinePlacementSpaceMoveDownActionPerformed, null),

                (destroyActions.FindAction(DESTROY_MODE_ACTIVATION), null, DestroyModeActivationActionPerformed, null),
                (destroyActions.FindAction(DESTROY), DestroyActionStarted, DestroyActionPerformed, DestroyActionCancelled),

                (selectActions.FindAction(SELECT_MODE_ACTIVATION), null, SelectModeActivationActionPerformed, null),
                (selectActions.FindAction(SELECT), SelectActionStarted, SelectActionPerformed, SelectActionCancelled),
                (selectActions.FindAction(MULTI_SELECTION_HOLD), null, MultiSelectionHoldActionPerformed, MultiSelectionHoldActionCancelled),
                (selectActions.FindAction(SELECTION_DESTROY), null, SelectionDestroyActionPerformed, null),
                (selectActions.FindAction(SELECTION_COPY), null, SelectionCopyActionPerformed, null),
                (selectActions.FindAction(SELECTION_MOVE), null, SelectionMoveActionPerformed, null),

                (moveActions.FindAction(MOVE_MODE_ACTIVATION), null, MoveModeActivationActionPerformed, null),
                (moveActions.FindAction(MOVE), MoveActionStarted, MoveActionPerformed, MoveActionCancelled),

                (heatMapActions.FindAction(HEAT_MAP_MODE_TOGGLE), null, HeatMapModeToggleActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_MODE_ENABLE), null, HeatMapModeEnableActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_MODE_DISABLE), null, HeatMapModeDisableActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_SWITCH_SCROLL), null, HeatMapSwitchScrollActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_SWITCH_MOVE_UP), null, HeatMapSwitchMoveUpActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_SWITCH_MOVE_DOWN), null, HeatMapSwitchMoveDownActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_BRUSH_SIZE_SCROLL), null, HeatMapBrushSizeScrollActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_BRUSH_SIZE_MOVE_UP), null, HeatMapBrushSizeMoveUpActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_BRUSH_SIZE_MOVE_DOWN), null, HeatMapBrushSizeMoveDownActionPerformed, null),
                (heatMapActions.FindAction(HEAT_MAP_PAINT), HeatMapPaintActionStarted, HeatMapPaintActionPerformed, HeatMapPaintActionCancelled),
                (heatMapActions.FindAction(HEAT_MAP_READ), null, HeatMapReadActionPerformed, null)
            );
        }

        private void UnsubscribeInputActionEvents(params (InputAction inputAction, Action<InputAction.CallbackContext> startedAction, Action<InputAction.CallbackContext> performedAction, Action<InputAction.CallbackContext> cancelledAction)[] actions)
        {
            foreach ((InputAction inputAction, Action<InputAction.CallbackContext> startedAction, Action<InputAction.CallbackContext> performedAction, Action<InputAction.CallbackContext> cancelledAction) actionTuple in actions)
            {
                if (actionTuple.inputAction != null)
                {
                    if (actionTuple.startedAction != null) actionTuple.inputAction.started -= actionTuple.startedAction;
                    if (actionTuple.performedAction != null) actionTuple.inputAction.performed -= actionTuple.performedAction;
                    if (actionTuple.cancelledAction != null) actionTuple.inputAction.canceled -= actionTuple.cancelledAction;
                }
            }
        }
        #endregion Input Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT UPDATE FUNCTIONS                                                        ///
        ///-------------------------------------------------------------------------------///

        private void LateUpdate()
        {
            foreach ((InputAction.CallbackContext context, Action action) in pendingActions)
            {
                if (isApplicationFocused && (!EventSystem.current.IsPointerOverGameObject() || !actionsToBlockWhenPointerOverUI.Contains(context.action.name))) action?.Invoke();
            }
            pendingActions.Clear();
        }

        #region Late Update Functions Start:
        private void EnqueueAction(InputAction.CallbackContext context, System.Action action)
        {
            // Add to pending actions to process in LateUpdate
            pendingActions.Add((context, action));
        }
        #endregion Late Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT VALIDATION FUNCTIONS                                                    ///
        ///-------------------------------------------------------------------------------///

        private void OnApplicationFocus(bool focusStatus)
        {
            if (focusStatus) StartCoroutine(DelayInputProcessing());
            else isApplicationFocused = false;
        }

        #region Application Focus Validation Functions Start:
        private IEnumerator DelayInputProcessing()
        {
            // Wait for 0.1 seconds before enabling input processing
            yield return new WaitForSeconds(FOCUSED_APPLICATION_INPUT_ENABLE_DELAY);
            isApplicationFocused = true;
        }
        #endregion Application Focus Validation Functions End:

        #region Touch Input Validation Functions Start:
        private bool IsTouchInput(InputAction.CallbackContext context)
        {
            return context.control.device is Touchscreen;
        }
        #endregion Touch Input Validation Functions End:

        ///-------------------------------------------------------------------------------///
        /// ACTIONS: CORE GRID INPUT FUNCTIONS                                            ///
        ///-------------------------------------------------------------------------------///
        
        #region Grid Mode Reset Actions Start:
        private void GridModeResetActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputGridModeReset();
                }

                if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.SetInputGridModeReset();
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputGridModeReset();
                if (GridManager.Instance.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover)) buildableObjectMover.SetInputGridModeReset();
            });
        }
        #endregion Grid Mode Reset Actions End:

        #region Placement & Selection Reset Actions Start:
        private void PlacementAndSelectionResetActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputPlacementReset(splineCompleteUsePlacementResetAction && 
                            AreActionsBoundToSameKey(buildActions.FindAction(BUILDABLE_FREE_OBJECT_SPLINE_PLACEMENT_COMPLETE), coreGridActions.FindAction(PLACEMENT_AND_SELECTION_RESET)) ? true : false);
                }

                if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.SetInputAreaDestructionReset();
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputAreaSelectionReset();
                if (GridManager.Instance.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover)) buildableObjectMover.SetInputObjectMovingReset();
            });
        }

        private bool AreActionsBoundToSameKey(InputAction actionA, InputAction actionB)
        {
            foreach (InputControl controlA in actionA.controls)
            {
                foreach (InputControl controlB in actionB.controls)
                {
                    if (controlA.path == controlB.path) return true; // Return true if any binding matches
                }
            }
            return false; // Return false if no bindings match
        }
        #endregion Placement & Selection Reset Actions End:

        #region Vertical Grid Scroll Actions Start:
        private void VerticalGridScrollActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughVerticalGrids(context.ReadValue<Vector2>()));
            }
        }
        #endregion Vertical Grid Scroll Actions End:

        #region Vertical Grid Move Up Actions Start:
        private void VerticalGridMoveUpActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputMoveUpVerticalGrid());
            }
        }
        #endregion Vertical Grid Move Up Actions End:

        #region Vertical Grid Move Down Actions Start:
        private void VerticalGridMoveDownActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputMoveDownVerticalGrid());
            }
        }
        #endregion Vertical Grid Move Down Actions End:

        #region Undo Actions Start:
        private void UndoActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () => GridManager.Instance.GetGridCommandInvoker().UndoCommand());
        }
        #endregion Undo Actions End:
    
        #region Redo Actions Start:
        private void RedoActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () => GridManager.Instance.GetGridCommandInvoker().RedoCommand());
        }
        #endregion Redo Actions End:

        #region Grid Save Actions Start:
        private void GridSaveActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () => 
            {
                if (GridManager.Instance.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) gridSaveAndLoadManager.SetInputSave();
            });
        }
        #endregion Grid Save Actions End:

        #region Grid Load Actions Start:
        private void GridLoadActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () => 
            {
                if (GridManager.Instance.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) gridSaveAndLoadManager.SetInputLoad();
            });
        }
        #endregion Grid Load Actions End:

        ///-------------------------------------------------------------------------------///
        /// ACTIONS: BUILD INPUT FUNCTIONS                                                ///
        ///-------------------------------------------------------------------------------///
        
        #region Build Mode Activation Actions Start:
        private void BuildModeActivationActionPerformed(InputAction.CallbackContext context)
        {
            if (!useBuildModeActivationInput) return;
            EnqueueAction(context, () => GridManager.Instance.SetActiveGridModeInAllGrids(GridMode.BuildMode, setThisBuildModeInputAsAToggle));
        }
        #endregion Build Mode Activation Actions End:

        #region Build Actions Start:
        private void BuildActionStarted(InputAction.CallbackContext context)
        {
            if (!IsTouchInput(context)) return; // Ensure it's mobile touch input

            float currentTime = Time.time;

            // Check for double-tap timing
            if (currentTime - buildLastTapTime <= doubleTapTime) isBuildSecondTapInProgress = true; // The second tap is now in progress
            else isBuildSecondTapInProgress = false; // Reset if it's not within double-tap time

            buildLastTapTime = currentTime;
        }

        private void BuildActionPerformed(InputAction.CallbackContext context)
        {
            // Ensure it's mobile touch input
            if (IsTouchInput(context)) 
            {
                if (isBuildSecondTapInProgress && context.interaction is HoldInteraction)
                {
                    foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                    {
                        EnqueueAction(context, () => easyGridBuilderPro.SetInputBuildableObjectPlacement());
                    }
                }
            }
            else
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    EnqueueAction(context, () => easyGridBuilderPro.SetInputBuildableObjectPlacement());
                }
            }
        }

        private void BuildActionCancelled(InputAction.CallbackContext context)
        {
            // Ensure it's mobile touch input
            if (IsTouchInput(context)) isBuildSecondTapInProgress = false;

            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputBuildableObjectPlacementComplete(placeMovingObjectOnInputRelease));
            }
        }
        #endregion Build Actions End:

        #region Build List Scroll Actions Start:
        private void BuildableListScrollActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughBuildableObjectsSOList(context.ReadValue<Vector2>()));
            }
        }
        #endregion Build List Scroll Actions End:

        #region Build List Move Up Actions Start:
        private void BuildableListMoveUpActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughBuildableObjectsSOListUp());
            }
        }
        #endregion Build List Move Up Actions End:
        
        #region Build List Move Down Actions Start:
        private void BuildableListMoveDownActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughBuildableObjectsSOListDown());
            }
        }
        #endregion Build List Move Down Actions End:

        #region Active Buildable Placement Type Scroll Actions Start:
        private void BuildablePlacementTypeScrollActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughActiveBuildableObjectSOPlacementType(context.ReadValue<Vector2>()));
            }
        }
        #endregion Active Buildable Placement Type Scroll Actions End:

        #region Active Buildable Placement Type Move Up Actions Start:
        private void BuildablePlacementTypeMoveUpActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughActiveBuildableObjectSOPlacementTypeUp());
            }
        }
        #endregion Active Buildable Placement Type Move Up Actions End:

        #region Active Buildable Placement Type Move Down Actions Start:
        private void BuildablePlacementTypeMoveDownActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughActiveBuildableObjectSOPlacementTypeDown());
            }
        }
        #endregion Active Buildable Placement Type Move Down Actions End:

        #region Buildable Rotate Scroll Actions Start:
        private void BuildableRotateScrollActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableObjectRotationScroll(context.ReadValue<Vector2>());
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableObjectRotationScroll(context.ReadValue<Vector2>());
            });
        }

        private void BuildableRotateScrollActionCancelled(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableObjectRotationScrollComplete();
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableObjectRotationScrollComplete();
            });
        }
        #endregion Buildable Rotate Scroll Actions End:

        #region Buildable Rotate Clockwise Actions Start:
        private void BuildableRotateClockwiseActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableObjectClockwiseRotation();
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableObjectClockwiseRotation();
            });
        }
        
        private void BuildableRotateClockwiseActionCancelled(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableObjectClockwiseRotationComplete();
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableObjectClockwiseRotationComplete();
            });
        }
        #endregion Buildable Rotate Clockwise Actions End:

        #region Buildable Rotate Counter Clockwise Actions Start:
        private void BuildableRotateCounterClockwiseActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableObjectCounterClockwiseRotation();
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableObjectCounterClockwiseRotation();
            });
        }
        
        private void BuildableRotateCounterClockwiseActionCancelled(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableObjectCounterClockwiseRotationComplete();
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableObjectCounterClockwiseRotationComplete();
            });
        }
        #endregion Buildable Rotate Counter Clockwise Actions End:

        #region Buildable Edge Object Flip Actions Start:
        private void BuildableEdgeObjectFlipActionPerformed(InputAction.CallbackContext context)
        {
            EnqueueAction(context, () =>
            {
                foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
                {
                    easyGridBuilderPro.SetInputBuildableEdgeObjectFlip();
                }
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectedBuildableEdgeObjectFlip();
            });
        }
        #endregion Buildable Edge Object Flip Actions End:

        #region Buildable Free Object Spline Placement Complete Actions Start:
        private void BuildableFreeObjectSplinePlacementCompleteActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputBuildableFreeObjectSplinePlacementComplete());
            }
        }
        #endregion Buildable Free Object Spline Placement Complete Actions End:

        #region Buildable Free Object Spline Placement Space Scroll Actions Start:
        private void BuildableFreeObjectSplinePlacementSpaceScrollActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputCycleThroughBuildableFreeObjectSplineSpacing(context.ReadValue<Vector2>()));
            }
        }
        #endregion Buildable Free Object Spline Placement Space Scroll Actions End:

        #region Buildable Free Object Spline Placement Space Move Up Actions Start:
        private void BuildableFreeObjectSplinePlacementSpaceMoveUpActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputIncreaseBuildableFreeObjectSplineSpacing());
            }
        }
        #endregion Buildable Free Object Spline Placement Space Move Up Actions End:

        #region Buildable Free Object Spline Placement Space Move Down Actions Start:
        private void BuildableFreeObjectSplinePlacementSpaceMoveDownActionPerformed(InputAction.CallbackContext context)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in GridManager.Instance.GetEasyGridBuilderProSystemsList())
            {
                EnqueueAction(context, () => easyGridBuilderPro.SetInputDecreaseBuildableFreeObjectSplineSpacing());
            }
        }
        #endregion Buildable Free Object Spline Placement Space Move Down Actions End:

        ///-------------------------------------------------------------------------------///
        /// ACTIONS: DESTROY INPUT FUNCTIONS                                              ///
        ///-------------------------------------------------------------------------------///
        
        #region Destroy Mode Activation Actions Start:
        private void DestroyModeActivationActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!useDestroyModeActivationInput) return;
            EnqueueAction(context, () => GridManager.Instance.SetActiveGridModeInAllGrids(GridMode.DestroyMode, setThisDestroyModeInputAsAToggle));
        }
        #endregion Destroy Mode Activation Actions End:

        #region Destroy Actions Start:
        private void DestroyActionStarted(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!IsTouchInput(context)) return; // Ensure it's mobile touch input

            float currentTime = Time.time;

            // Check for double-tap timing
            if (currentTime - destroyLastTapTime <= doubleTapTime) isDestroySecondTapInProgress = true; // The second tap is now in progress
            else isDestroySecondTapInProgress = false; // Reset if it's not within double-tap time

            destroyLastTapTime = currentTime;
        }

        private void DestroyActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) // Ensure it's mobile touch input
            {
                if (isDestroySecondTapInProgress && context.interaction is HoldInteraction)
                {
                    EnqueueAction(context, () => 
                    {
                        if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.SetInputDestroyBuildableObject();
                    });
                }
            }
            else
            {
                EnqueueAction(context, () => 
                {
                    if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.SetInputDestroyBuildableObject();
                });
            }
        }
                
        private void DestroyActionCancelled(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) isDestroySecondTapInProgress = false; // Ensure it's mobile touch input

            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.SetInputDestroyBuildableObjectComplete();
            });
        }
        #endregion Destroy Actions End:

        ///-------------------------------------------------------------------------------///
        /// ACTIONS: SELECT INPUT FUNCTIONS                                               ///
        ///-------------------------------------------------------------------------------///

        #region Select Mode Activation Actions Start:
        private void SelectModeActivationActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!useSelectModeActivationInput) return;
            EnqueueAction(context, () => GridManager.Instance.SetActiveGridModeInAllGrids(GridMode.SelectMode, setThisSelectModeInputAsAToggle));
        }
        #endregion Select Mode Activation Actions End:

        #region Select Actions Start:
        private void SelectActionStarted(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!IsTouchInput(context)) return; // Ensure it's mobile touch input

            float currentTime = Time.time;

            // Check for double-tap timing
            if (currentTime - selectLastTapTime <= doubleTapTime) isSelectSecondTapInProgress = true; // The second tap is now in progress
            else isSelectSecondTapInProgress = false; // Reset if it's not within double-tap time

            selectLastTapTime = currentTime;
        }

        private void SelectActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) // Ensure it's mobile touch input
            {
                if (isSelectSecondTapInProgress && context.interaction is HoldInteraction)
                {
                    EnqueueAction(context, () =>
                    { 
                        if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectBuildableObject();
                    });
                }
            }
            else
            {
                EnqueueAction(context, () =>
                { 
                    if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectBuildableObject();
                });
            }
        }

        private void SelectActionCancelled(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) isSelectSecondTapInProgress = false; // Ensure it's mobile touch input

            EnqueueAction(context, () => 
            { 
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectBuildableObjectComplete();
            });
        }
        #endregion Select Actions End:

        #region Multi Selection Hold Actions Start:
        private void MultiSelectionHoldActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () => 
            {
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputMultiSelection();
            });
        }

        private void MultiSelectionHoldActionCancelled(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () => 
            {
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputMultiSelectionComplete();
            });
        }
        #endregion Multi Selection Hold Actions End:

        #region Selection Destroy Actions Start:
        private void SelectionDestroyActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () => 
            {
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectionDestroy();
            });
        }
        #endregion Selection Destroy Actions End:

        #region Selection Copy Actions Start:
        private void SelectionCopyActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () => 
            {
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectionCopy();
            });
        }
        #endregion Selection Copy Actions End:

        #region Selection Move Actions Start:
        private void SelectionMoveActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            { 
                if (GridManager.Instance.TryGetBuildableObjectSelector(out BuildableObjectSelector buildableObjectSelector)) buildableObjectSelector.SetInputSelectionMove();
            });
        }
        #endregion Selection Move Actions End:

        ///-------------------------------------------------------------------------------///
        /// ACTIONS: SELECT INPUT FUNCTIONS                                               ///
        ///-------------------------------------------------------------------------------///
        
        #region Move Mode Activation Actions Start:
        private void MoveModeActivationActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!useMoveModeActivationInput) return;
            EnqueueAction(context, () => GridManager.Instance.SetActiveGridModeInAllGrids(GridMode.MoveMode, setThisMoveModeInputAsAToggle));
        }
        #endregion Move Mode Activation Actions End:

        #region Move Action Start:
        private void MoveActionStarted(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!IsTouchInput(context)) return; // Ensure it's mobile touch input

            float currentTime = Time.time;

            // Check for double-tap timing
            if (currentTime - moveLastTapTime <= doubleTapTime) isMoveSecondTapInProgress = true; // The second tap is now in progress
            else isMoveSecondTapInProgress = false; // Reset if it's not within double-tap time

            moveLastTapTime = currentTime;
        }

        private void MoveActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) // Ensure it's mobile touch input
            {
                if (isMoveSecondTapInProgress && context.interaction is HoldInteraction)
                {
                    EnqueueAction(context, () =>
                    {
                        if (GridManager.Instance.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover)) buildableObjectMover.SetInputStartMoveBuildableObject();
                    });
                }
            }
            else
            {
                EnqueueAction(context, () =>
                {
                    if (GridManager.Instance.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover)) buildableObjectMover.SetInputStartMoveBuildableObject();
                });
            }
        }

        private void MoveActionCancelled(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) isMoveSecondTapInProgress = false; // Ensure it's mobile touch input
        }
        #endregion Move Action End:

        ///-------------------------------------------------------------------------------///
        /// ACTIONS: HEAT MAP INPUT FUNCTIONS                                             ///
        ///-------------------------------------------------------------------------------///
        
        #region Heat Map Mode Toggle Actions Start:
        private void HeatMapModeToggleActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputToggleHeatMapMode();
            });
        }
        #endregion Heat Map Mode Toggle Actions End:

        #region Heat Map Mode Enable Actions Start:
        private void HeatMapModeEnableActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputToggleHeatMapMode();
            });
        }
        #endregion Heat Map Mode Enable Actions End:

        #region Heat Map Mode Disable Actions Start:
        private void HeatMapModeDisableActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputToggleHeatMapMode();
            });
        }
        #endregion Heat Map Mode Disable Actions End:

        #region Heat Map Switch Scroll Actions Start:
        private void HeatMapSwitchScrollActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapSwitchScroll(context.ReadValue<Vector2>());
            });
        }
        #endregion Heat Map Switch Scroll Actions End:

        #region Heat Map Switch Move Up Actions Start:
        private void HeatMapSwitchMoveUpActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputSwitchNextHeatMap();
            });
        }
        #endregion Heat Map Switch Move Up Actions End:

        #region Heat Map Switch Move Down Actions Start:
        private void HeatMapSwitchMoveDownActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputSwitchPreviousHeatMap();
            });
        }
        #endregion Heat Map Switch Move Down Actions End:

        #region Heat Map Brush Size Scroll Actions Start:
        private void HeatMapBrushSizeScrollActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapBrushSizeScroll(context.ReadValue<Vector2>());
            });
        }
        #endregion Heat Map Brush Size Scroll Actions End:

        #region Heat Map Brush Size Move Up Actions Start:
        private void HeatMapBrushSizeMoveUpActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapBrushSizeIncrease();
            });
        }
        #endregion Heat Map Brush Size Move Up Actions End:

        #region Heat Map Brush Size Move Down Actions Start:
        private void HeatMapBrushSizeMoveDownActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapBrushSizeDecrease();
            });
        }
        #endregion Heat Map Brush Size Move Down Actions End:

        #region Heat Map Paint Actions Start:
        private void HeatMapPaintActionStarted(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (!IsTouchInput(context)) return; // Ensure it's mobile touch input

            float currentTime = Time.time;

            // Check for double-tap timing
            if (currentTime - heatMapPaintLastTapTime <= doubleTapTime) isHeatMapPaintSecondTapInProgress = true; // The second tap is now in progress
            else isHeatMapPaintSecondTapInProgress = false; // Reset if it's not within double-tap time

            heatMapPaintLastTapTime = currentTime;
        }

        private void HeatMapPaintActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) // Ensure it's mobile touch input
            {
                if (isHeatMapPaintSecondTapInProgress && context.interaction is HoldInteraction)
                {
                    EnqueueAction(context, () =>
                    {
                        if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapPainting();
                    });
                }
            }
            else
            {
                EnqueueAction(context, () =>
                {
                    if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapPainting();
                });
            }
        }

        private void HeatMapPaintActionCancelled(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            if (IsTouchInput(context)) isHeatMapPaintSecondTapInProgress = false; // Ensure it's mobile touch input

            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapPaintCancelled();
            });
        }
        #endregion Heat Map Paint Actions End:
        
        #region Heat Map Read Actions Start:
        private void HeatMapReadActionPerformed(InputAction.CallbackContext context)
        {
            if (GridManager.Instance.GetEasyGridBuilderProSystemsList().Count <= 0) return;
            EnqueueAction(context, () =>
            {
                if (GridManager.Instance.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputReadHeatMapValue();
            });
        }
        #endregion Heat Map Read Actions End:

        ///-------------------------------------------------------------------------------///
        /// PULBIC GETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        public InputActionAsset GetInputActionAsset() => inputActionsAsset;
    }
}