using System.Collections;
using System.Collections.Generic;
using SoulGames.Utilities;
using UnityEngine;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Object Selector", 1)]
    public class BuildableObjectSelector : MonoBehaviour
    {
        public event OnBuildableObjectHoverEnterDelegate OnBuildableObjectHoverEnter;
        public delegate void OnBuildableObjectHoverEnterDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectHoverExitDelegate OnBuildableObjectHoverExit;
        public delegate void OnBuildableObjectHoverExitDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectSelectedDelegate OnBuildableObjectSelected;
        public delegate void OnBuildableObjectSelectedDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectDeselectedDelegate OnBuildableObjectDeselected;
        public delegate void OnBuildableObjectDeselectedDelegate(BuildableObject buildableObject);

        [SerializeField] private SelectionMode selectionMode = SelectionMode.IndividualAndArea;
        [SerializeField] private float areaSelectionPadding = 10f;
        [SerializeField] private SelectableBuildableObjectType SelectableObjectType = SelectableBuildableObjectType.All;
        [SerializeField] private LayerMask selectableObjectsLayerMask;

        [Space]
        [SerializeField] private Canvas screenSpaceSelector;
        [SerializeField] private Color selectorColor;

        [Space]
        [SerializeField] private bool enableHoldToMultiSelect = true;
        [SerializeField] private bool enableSelectedObjectRotation = true;
        [SerializeField] private bool enableSelectedObjectsDestruction = true;
        [SerializeField] private bool enableSelectedObjectCopy = true;
        [SerializeField] private bool enableSelectedObjectMoving = true;

        [Space]
        [SerializeField] private bool blockSelectionInGridModeDefault = true;
        [SerializeField] private bool blockSelectionInGridModeBuild = true;
        [SerializeField] private bool blockSelectionInGridModeDestroy = true;
        [SerializeField] private bool blockSelectionInGridModeSelect;
        [SerializeField] private bool blockSelectionInGridModeMove = true;

        [Space]
        [SerializeField] private bool enableGridCellIndicator;
        [SerializeField] private GameObject gridCellIndicatorPrefab;
        [SerializeField] private float indicatorMoveSpeed = 25f;
        [SerializeField] private bool lockIndicatorAtFirstGrid;
        [SerializeField] private LayerMask indicatorCustomSurfaceLayerMask;

        [Space]
        [SerializeField] private bool disableInidicatorInGridModeNone;
        [SerializeField] private bool disableInidicatorInGridModeBuild;
        [SerializeField] private bool disableInidicatorInGridModeDestroy;
        [SerializeField] private bool disableInidicatorInGridModeSelect;
        [SerializeField] private bool disableInidicatorInGridModeMove;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;

        private Transform gridCellIndicator;
        private Vector3 previousGridCellIndicatorWorldPosition = new Vector3(-99999, -99999, -99999);

        private bool isSelectionKeyHolding;
        private bool isMultiSelectionKeyHolding;
        private bool isClockwiseRotationKeyHolding;
        private bool isCounterClockwiseRotationKeyHolding;
        private List<BuildableObject> freeRotateBuildableList;

        private Vector3 selectionBoxAreaStartPosition;
        private Vector3 selectionBoxAreaEndPosition;
        private RectTransform screenSpaceSelectorBox;
        private bool isAreaSelectionResetInputTriggered;

        private List<BuildableObject> selectedObjectsList;
        private List<BuildableObject> hoveredObjectsList;
        private BuildableObject previousHoveredObject;
        
        ///-------------------------------------------------------------------------------///
        /// BUILDABLE SELECTOR INITIALIZE FUNCTIONS                                       ///
        ///-------------------------------------------------------------------------------///
        
        private void Start()
        {
            hoveredObjectsList = new List<BuildableObject>();
            selectedObjectsList = new List<BuildableObject>();
            freeRotateBuildableList = new List<BuildableObject>();
            
            StartCoroutine(LateStart());
            if (enableGridCellIndicator) InitializeGridCellIndicator();
            if (selectionMode == SelectionMode.IndividualAndArea) InitializeScreenSpaceSelector();
        }
        
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();
            
            gridManager = GridManager.Instance;
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;

            activeEasyGridBuilderPro = GridManager.Instance.GetActiveEasyGridBuilderPro();
            if (activeEasyGridBuilderPro && enableGridCellIndicator) UpdateGridCellIndicatorActiveSelf(activeEasyGridBuilderPro.GetActiveGridMode());
        }

        private void OnDestroy()
        {
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged -= OnActiveGridModeChanged;
        }

        #region Initialization Functions Start:
        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderProSystem)
        {
            activeEasyGridBuilderPro = activeEasyGridBuilderProSystem;
        }

        private void OnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            ResetAreaSelection();
            ResetIndividualSelection();
            if (enableGridCellIndicator) UpdateGridCellIndicatorActiveSelf(gridMode);
        }

        private void InitializeGridCellIndicator()
        {
            if (gridCellIndicator) return;
            
            gridCellIndicator = Instantiate(gridCellIndicatorPrefab.transform);
            gridCellIndicator.transform.SetParent(this.transform);
            gridCellIndicator.transform.position = new Vector3(0, 0, 0);
        }

        private void InitializeScreenSpaceSelector()
        {
            screenSpaceSelector = Instantiate(screenSpaceSelector);
            screenSpaceSelector.transform.SetParent(transform);
            screenSpaceSelectorBox = screenSpaceSelector.transform.GetChild(0).GetComponent<RectTransform>();
            screenSpaceSelectorBox.GetComponent<Image>().color = selectorColor;
        }
        #endregion Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE SELECTOR UPDATE FUNCTIONS                                           ///
        ///-------------------------------------------------------------------------------///
        
        private void Update()
        {
            if (!activeEasyGridBuilderPro) return;
            if (activeEasyGridBuilderPro.GetUseSelectModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.SelectMode) return;
            if (IsSelectionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            if (isSelectionKeyHolding) UpdateSelectionKeyHodling();
            else UpdateIndividualSelectionHover();
            if (isClockwiseRotationKeyHolding || isCounterClockwiseRotationKeyHolding) UpdateRotationKeyHolding();
        }

        private void LateUpdate()
        {
            if (!activeEasyGridBuilderPro) return;
            if (enableGridCellIndicator) UpdateGridCellIndicatorPosition();
        }

        #region Update Functions Start:
        private void UpdateGridCellIndicatorActiveSelf(GridMode gridMode)
        {
            bool isActive = gridMode switch
            {
                GridMode.None => !disableInidicatorInGridModeNone,
                GridMode.BuildMode => !disableInidicatorInGridModeBuild,
                GridMode.DestroyMode => !disableInidicatorInGridModeDestroy,
                GridMode.SelectMode => !disableInidicatorInGridModeSelect,
                GridMode.MoveMode => !disableInidicatorInGridModeMove,
                _ => true // Default to active if GridMode is unrecognized
            };
            
            gridCellIndicator.gameObject.SetActive(isActive);
        }

        private bool IsSelectionBlockedByActiveGridMode(GridMode gridMode)
        {
            switch (gridMode)
            {
                case GridMode.None : return blockSelectionInGridModeDefault;
                case GridMode.BuildMode : return blockSelectionInGridModeBuild;
                case GridMode.DestroyMode : return blockSelectionInGridModeDestroy;
                case GridMode.SelectMode : return blockSelectionInGridModeSelect;
                case GridMode.MoveMode : return blockSelectionInGridModeMove;
            }
            return true;
        }

        private bool IsSelectionBlockedBySelectableObjectType(BuildableObject buildableObject)
        {
            switch (SelectableObjectType)
            {
                case SelectableBuildableObjectType.All: return false;
                case SelectableBuildableObjectType.BuildableGridObject: return !(buildableObject is BuildableGridObject);
                case SelectableBuildableObjectType.BuildableEdgeObject: return !(buildableObject is BuildableEdgeObject);
                case SelectableBuildableObjectType.BuildableCornerObject: return !(buildableObject is BuildableCornerObject);
                case SelectableBuildableObjectType.BuildableFreeObject: return !(buildableObject is BuildableFreeObject);
            }
            return true;
        }

        private void UpdateGridCellIndicatorPosition()
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();

            Vector3 targetPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? CalculateSnappingPositionXZ() : CalculateSnappingPositionXY();
            if (!lockIndicatorAtFirstGrid) 
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) targetPosition += new Vector3(0, activeVerticalGridIndex * verticalGridHeight, 0);
                else targetPosition += new Vector3(0, 0, activeVerticalGridIndex * verticalGridHeight);
            }
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * indicatorMoveSpeed);
        }

        private Vector3 CalculateSnappingPositionXZ()
        {
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(indicatorCustomSurfaceLayerMask, GridManager.Instance.GetGridSystemLayerMask(), Vector3.down * 9999, 
                out _, out Vector3 firstCollisionWorldPosition);
            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            Vector3 worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);

            if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(cellPosition)) return previousGridCellIndicatorWorldPosition;

            previousGridCellIndicatorWorldPosition = new Vector3(worldPosition.x + (cellSize / 2), firstCollisionWorldPosition.y, worldPosition.z + (cellSize / 2));
            return previousGridCellIndicatorWorldPosition;
        }

        private Vector3 CalculateSnappingPositionXY()
        {
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(indicatorCustomSurfaceLayerMask, GridManager.Instance.GetGridSystemLayerMask(), Vector3.forward * 9999, 
                out _, out Vector3 firstCollisionWorldPosition);
            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            Vector3 worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);

            if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(cellPosition)) return previousGridCellIndicatorWorldPosition;

            previousGridCellIndicatorWorldPosition = new Vector3(worldPosition.x + (cellSize / 2), worldPosition.y + (cellSize / 2), firstCollisionWorldPosition.z);
            return previousGridCellIndicatorWorldPosition;
        }

        private void UpdateSelectionKeyHodling()
        {
            Vector3 selectionBoxAreaCurrentPosition = MouseInteractionUtilities.GetCurrentMousePosition();

            if (selectionMode == SelectionMode.IndividualAndArea && Vector3.Distance(selectionBoxAreaCurrentPosition, selectionBoxAreaStartPosition) > 1)
            {
                UpdateScreenSpaceSelectionVisuals(selectionBoxAreaCurrentPosition);
            }
        }

        private void UpdateScreenSpaceSelectionVisuals(Vector3 selectionBoxAreaCurrentPosition)
        {
            if (isAreaSelectionResetInputTriggered) return;
            
            screenSpaceSelector.gameObject.SetActive(true);
            ResetIndividualSelection(true);

            float selectorWidth = selectionBoxAreaCurrentPosition.x - selectionBoxAreaStartPosition.x;
            float selectorHeight = selectionBoxAreaCurrentPosition.y - selectionBoxAreaStartPosition.y;

            screenSpaceSelectorBox.sizeDelta = new Vector2(Mathf.Abs(selectorWidth), Mathf.Abs(selectorHeight));
            screenSpaceSelectorBox.anchoredPosition = (selectionBoxAreaStartPosition + selectionBoxAreaCurrentPosition) / 2;

            if (Vector3.Distance(selectionBoxAreaEndPosition, selectionBoxAreaStartPosition) < 1) return;

            float minX = screenSpaceSelectorBox.anchoredPosition.x - (screenSpaceSelectorBox.sizeDelta.x / 2);
            float maxX = screenSpaceSelectorBox.anchoredPosition.x + (screenSpaceSelectorBox.sizeDelta.x / 2);
            float minY = screenSpaceSelectorBox.anchoredPosition.y - (screenSpaceSelectorBox.sizeDelta.y / 2);
            float maxY = screenSpaceSelectorBox.anchoredPosition.y + (screenSpaceSelectorBox.sizeDelta.y / 2);

            if (gridManager.TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager))
            {
                foreach (BuildableObject buildableObject in gridBuiltObjectsManager.GetBuiltObjectsList())
                {
                    if (!buildableObject.GetBuildableObjectSO().isObjectSelectable) continue;
                    if (IsSelectionBlockedBySelectableObjectType(buildableObject)) continue;
                    if ((selectableObjectsLayerMask.value & (1 << buildableObject.gameObject.layer)) == 0) continue;
                    if (selectedObjectsList.Contains(buildableObject)) continue;

                    Vector3 screenPosition = MouseInteractionUtilities.GetWorldToScreenPosition(buildableObject.transform.position);
                    if (screenPosition.x > minX - areaSelectionPadding && screenPosition.x < maxX + areaSelectionPadding && screenPosition.y > minY - areaSelectionPadding && screenPosition.y < maxY + areaSelectionPadding)
                    {
                        if (!hoveredObjectsList.Contains(buildableObject)) 
                        {
                            hoveredObjectsList.Add(buildableObject);
                            OnBuildableObjectHoverEnter?.Invoke(buildableObject);
                        }
                    }
                    else 
                    {
                        if (hoveredObjectsList.Contains(buildableObject)) 
                        {
                            hoveredObjectsList.Remove(buildableObject);
                            OnBuildableObjectHoverExit?.Invoke(buildableObject);
                        }
                    }
                }
            }
        }

        private void UpdateIndividualSelectionHover()
        {
            if (MouseInteractionUtilities.TryGetBuildableObject(selectableObjectsLayerMask, out BuildableObject buildableObject))
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();
                if (IsSelectionBlockedBySelectableObjectType(buildableObject)) return;
                    
                // If hovering over a different object than before, call the OnBuildableObjectHoverExit for the previous object
                if (previousHoveredObject != null && previousHoveredObject != buildableObject && !selectedObjectsList.Contains(previousHoveredObject)) OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);
                if (selectedObjectsList.Contains(buildableObject)) return;

                // Call the OnBuildableObjectHoverEnter event for the new object
                OnBuildableObjectHoverEnter?.Invoke(buildableObject);

                // Update the previously hovered object
                previousHoveredObject = buildableObject;
            }
            else 
            {
                // If no object is hovered but a previous object was hovered, call the OnBuildableObjectHoverExit event
                if (previousHoveredObject != null)
                {
                    if (selectedObjectsList.Contains(previousHoveredObject)) return;
                    OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);
                    previousHoveredObject = null; // Reset after exit
                }
            }
        }
        #endregion Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT HANDLING FUNCTIONS                                                      ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle Input Grid Mode Functions Start:
        public void SetInputGridModeReset()
        {
            ResetAreaSelection();
            ResetIndividualSelection();
        }

        public void SetInputAreaSelectionReset()
        {
            ResetAreaSelection();
        }

        private void ResetIndividualSelection(bool invokedByUpdateScreenSpaceSelectionVisuals = false)
        {
            if (previousHoveredObject != null)
            {
                OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);
                previousHoveredObject = null;
            }

            if (invokedByUpdateScreenSpaceSelectionVisuals && isMultiSelectionKeyHolding) return;

            for (int i = selectedObjectsList.Count - 1; i >= 0; i--)
            {
                BuildableObject buildableObject = selectedObjectsList[i];
                OnBuildableObjectDeselected?.Invoke(buildableObject);
                selectedObjectsList.RemoveAt(i);
            }
        }

        private void ResetAreaSelection()
        {
            isAreaSelectionResetInputTriggered = true;
            isSelectionKeyHolding = false;
            if (screenSpaceSelector) screenSpaceSelector.gameObject.SetActive(false);

            for (int i = hoveredObjectsList.Count - 1; i >= 0; i--)
            {
                BuildableObject buildableObject = hoveredObjectsList[i];
                OnBuildableObjectHoverExit?.Invoke(buildableObject);
                hoveredObjectsList.RemoveAt(i);
            }

            for (int i = selectedObjectsList.Count - 1; i >= 0; i--)
            {
                BuildableObject buildableObject = selectedObjectsList[i];
                OnBuildableObjectDeselected?.Invoke(buildableObject);
                selectedObjectsList.RemoveAt(i);
            }
        }
        #endregion Handle Input Grid Mode Functions End:

        #region Handle Input Buildable Object Selection Functions Start:
        public void SetInputSelectBuildableObject()
        {
            if (activeEasyGridBuilderPro.GetUseSelectModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.SelectMode) return;
            if (IsSelectionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            isSelectionKeyHolding = true;
            isAreaSelectionResetInputTriggered = false;
            if (selectionMode == SelectionMode.IndividualAndArea)
            {
                selectionBoxAreaStartPosition = MouseInteractionUtilities.GetCurrentMousePosition();
                return;
            }

            InvokeSelectBuildableObject();
        }

        public void SetInputSelectBuildableObject(BuildableObject buildableObject)
        {
            activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();

            if (activeEasyGridBuilderPro.GetUseSelectModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.SelectMode) return;
            if (IsSelectionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;
            if (IsSelectionBlockedBySelectableObjectType(buildableObject)) return;
                
            SelectBuildableObject(buildableObject);
        }

        public void SetInputSelectBuildableObjectComplete()
        {
            if (activeEasyGridBuilderPro.GetUseSelectModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.SelectMode) return;
            if (IsSelectionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            isSelectionKeyHolding = false;
            if (selectionMode == SelectionMode.Individual) return;

            if (isAreaSelectionResetInputTriggered) 
            {
                isAreaSelectionResetInputTriggered = false;
                return;
            }

            selectionBoxAreaEndPosition = MouseInteractionUtilities.GetCurrentMousePosition();
            if (selectionMode == SelectionMode.IndividualAndArea) screenSpaceSelector.gameObject.SetActive(false);

            if (Vector3.Distance(selectionBoxAreaEndPosition, selectionBoxAreaStartPosition) < 1 || selectionMode == SelectionMode.Individual) 
            {
                InvokeSelectBuildableObject();
                return;
            }

            float minX = screenSpaceSelectorBox.anchoredPosition.x - (screenSpaceSelectorBox.sizeDelta.x / 2);
            float maxX = screenSpaceSelectorBox.anchoredPosition.x + (screenSpaceSelectorBox.sizeDelta.x / 2);
            float minY = screenSpaceSelectorBox.anchoredPosition.y - (screenSpaceSelectorBox.sizeDelta.y / 2);
            float maxY = screenSpaceSelectorBox.anchoredPosition.y + (screenSpaceSelectorBox.sizeDelta.y / 2);

            if (gridManager.TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager))
            {
                foreach (BuildableObject buildableObject in gridBuiltObjectsManager.GetBuiltObjectsList())
                {
                    if (!buildableObject.GetBuildableObjectSO().isObjectDestructable) continue;
                    if (IsSelectionBlockedBySelectableObjectType(buildableObject)) continue;
                    if ((selectableObjectsLayerMask.value & (1 << buildableObject.gameObject.layer)) == 0) continue;

                    Vector3 screenPosition = MouseInteractionUtilities.GetWorldToScreenPosition(buildableObject.transform.position);
                    if (screenPosition.x > minX - areaSelectionPadding && screenPosition.x < maxX + areaSelectionPadding && screenPosition.y > minY - areaSelectionPadding && screenPosition.y < maxY + areaSelectionPadding)
                    {
                        if (!hoveredObjectsList.Contains(buildableObject)) hoveredObjectsList.Add(buildableObject);
                    }
                }

                for (int i = hoveredObjectsList.Count - 1; i >= 0; i--)
                {
                    BuildableObject buildableObject = hoveredObjectsList[i];

                    if (isMultiSelectionKeyHolding && selectedObjectsList.Contains(buildableObject))
                    {
                        OnBuildableObjectDeselected?.Invoke(buildableObject);
                        selectedObjectsList.Remove(buildableObject);
                    }
                    else SetInputSelectBuildableObject(buildableObject);

                    hoveredObjectsList.RemoveAt(i);
                }
            }
        }
        
        public void SetInputMultiSelection()
        {
            if (enableHoldToMultiSelect) isMultiSelectionKeyHolding = true;
        }

        public void SetInputMultiSelectionComplete()
        {
            if (enableHoldToMultiSelect) isMultiSelectionKeyHolding = false;
        }
        #endregion Handle Input Buildable Object Selection Functions End:

        #region Handle Input Destroy Selected Objects Functions Start:
        public void SetInputSelectionDestroy()
        {
            if (!enableSelectedObjectsDestruction) return;
            if (gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer))
            {
                foreach (BuildableObject selectedObject in selectedObjectsList)
                {
                    buildableObjectDestroyer.SetInputDestroyBuildableObject(selectedObject, true);
                }
                ResetAreaSelection();
                ResetIndividualSelection();
            }
        }
        #endregion Handle Input Destroy Selected Objects Functions End:
    
        #region Handle Input Move Selected Object Functions Start:
        public void SetInputSelectionMove()
        {
            if (!enableSelectedObjectMoving) return;
            if (gridManager.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover))
            {
                if (selectedObjectsList.Count != 1) return;
                buildableObjectMover.StartMovingObject(selectedObjectsList[0], true);

                ResetAreaSelection();
                ResetIndividualSelection();
            }
        }
        #endregion Handle Input Move Selected Object Functions End:

        #region Handle Input Rotate Selected Objects Functions Start:
        public void SetInputSelectedBuildableObjectRotationScroll(Vector2 inputDirection)
        {
            if (inputDirection.y > 0) SetInputSelectedBuildableObjectClockwiseRotation();
            else if (inputDirection.y < 0) SetInputSelectedBuildableObjectCounterClockwiseRotation();
        }

        public void SetInputSelectedBuildableObjectClockwiseRotation()
        {
            if (!enableSelectedObjectRotation) return;
            isClockwiseRotationKeyHolding = true;
            InitiateSelectedBuildableObjectRotation(true);
        }

        public void SetInputSelectedBuildableObjectCounterClockwiseRotation()
        {
            if (!enableSelectedObjectRotation) return;
            isCounterClockwiseRotationKeyHolding = true;
            InitiateSelectedBuildableObjectRotation(false);
        }

        private void InitiateSelectedBuildableObjectRotation(bool isDirectionClockWise)
        {
            if (!gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            List<BuildableObject> newSelectedObjectList = new List<BuildableObject>();

            foreach (BuildableObject selectedObject in selectedObjectsList)
            {
                switch (selectedObject)
                {
                    case BuildableGridObject buildableGridObject: RotateSelectedBuildableGridObject(buildableObjectDestroyer, buildableGridObject, isDirectionClockWise, ref newSelectedObjectList); break;
                    case BuildableEdgeObject buildableEdgeObject: RotateSelectedBuildableEdgeObject(buildableObjectDestroyer, buildableEdgeObject, isDirectionClockWise, ref newSelectedObjectList); break;
                    case BuildableCornerObject buildableCornerObject: RotateSelectedBuildableCornerObject(buildableCornerObject, isDirectionClockWise, ref newSelectedObjectList); break;
                    case BuildableFreeObject buildableFreeObject: RotateSelectedBuildableFreeObject(buildableFreeObject, isDirectionClockWise, ref newSelectedObjectList); break;
                }
            }

            RecalculateSelectedObjectsList(newSelectedObjectList);
        }

        private void RecalculateSelectedObjectsList(List<BuildableObject> newSelectedObjectList)
        {
            ResetAreaSelection();
            ResetIndividualSelection();
            
            selectedObjectsList = newSelectedObjectList;
            foreach (BuildableObject selectedObject in selectedObjectsList)
            {
                StartCoroutine(LateSubscribeToOnBuildableObjectSelected(selectedObject));
            }
        }

        private IEnumerator LateSubscribeToOnBuildableObjectSelected(BuildableObject selectedObject)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            OnBuildableObjectSelected?.Invoke(selectedObject);
        }

        private void RotateSelectedBuildableGridObject(BuildableObjectDestroyer buildableObjectDestroyer, BuildableGridObject buildableGridObject, bool rotateClockwise, ref List<BuildableObject> newSelectedObjectList)
        {
            BuildableGridObjectSO buildableGridObjectSO = (BuildableGridObjectSO)buildableGridObject.GetBuildableObjectSO();
            if (!buildableGridObjectSO.isSelectedObjectRotatable) return;

            EasyGridBuilderPro easyGridBuilderPro = buildableGridObject.GetOccupiedGridSystem();
            int verticalGridIndex = buildableGridObject.GetOccupiedVerticalGridIndex();
            Vector2Int objectOriginCellPosition = buildableGridObject.GetObjectOriginCellPosition(out _);
            Vector3 objectOriginWorldPosition = easyGridBuilderPro.GetCellWorldPosition(objectOriginCellPosition, verticalGridIndex);
            FourDirectionalRotation currentFourDirectionalRotation = buildableGridObject.GetObjectFourDirectionalRotation();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = buildableGridObject.GetBuildableObjectSORandomPrefab();

            buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableGridObject, true, true, true);

            bool initializationSuccessful = false;
            int iterationCount = 0;
            BuildableGridObject newBuildableGridObject = null;
            while (initializationSuccessful == false && iterationCount < 4)
            {
                FourDirectionalRotation nextFourDirectionalRotation = rotateClockwise ? buildableGridObjectSO.GetNextDirectionClockwise(currentFourDirectionalRotation) : 
                    buildableGridObjectSO.GetNextDirectionCounterClockwise(currentFourDirectionalRotation);
                if (easyGridBuilderPro.TryInitializeBuildableGridObjectSinglePlacement(objectOriginWorldPosition, buildableGridObjectSO, nextFourDirectionalRotation, true, true, verticalGridIndex, true,
                    out newBuildableGridObject, objectRandomPrefab, buildableGridObject)) initializationSuccessful = true;
                
                currentFourDirectionalRotation = nextFourDirectionalRotation;
                iterationCount ++;
            }

            newSelectedObjectList.Add(newBuildableGridObject);
        }

        private void RotateSelectedBuildableEdgeObject(BuildableObjectDestroyer buildableObjectDestroyer, BuildableEdgeObject buildableEdgeObject, bool rotateClockwise, ref List<BuildableObject> newSelectedObjectList)
        {
            BuildableEdgeObjectSO buildableEdgeObjectSO = (BuildableEdgeObjectSO)buildableEdgeObject.GetBuildableObjectSO();
            if (!buildableEdgeObjectSO.isSelectedObjectRotatable) return;

            EasyGridBuilderPro easyGridBuilderPro = buildableEdgeObject.GetOccupiedGridSystem();
            int verticalGridIndex = buildableEdgeObject.GetOccupiedVerticalGridIndex();
            Vector3 objectOriginWorldPosition = buildableEdgeObject.GetObjectModifiedOriginWorldPosition();
            FourDirectionalRotation currentFourDirectionalRotation = buildableEdgeObject.GetObjectFourDirectionalRotation();
            bool isObjectFlipped = buildableEdgeObject.GetIsObjectFlipped();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = buildableEdgeObject.GetBuildableObjectSORandomPrefab();

            buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableEdgeObject, true, true, true);

            bool initializationSuccessful = false;
            int iterationCount = 0;
            BuildableEdgeObject newBuildableEdgeObject = null;
            while (initializationSuccessful == false && iterationCount < 4)
            {
                FourDirectionalRotation nextFourDirectionalRotation = rotateClockwise ? buildableEdgeObjectSO.GetNextDirectionClockwise(currentFourDirectionalRotation) : 
                    buildableEdgeObjectSO.GetNextDirectionCounterClockwise(currentFourDirectionalRotation);
                if (easyGridBuilderPro.TryInitializeBuildableEdgeObjectSinglePlacement(objectOriginWorldPosition, buildableEdgeObjectSO, nextFourDirectionalRotation, isObjectFlipped, true, true, 
                    verticalGridIndex, true, out newBuildableEdgeObject, objectRandomPrefab, buildableEdgeObject)) initializationSuccessful = true;
                
                currentFourDirectionalRotation = nextFourDirectionalRotation;
                iterationCount ++;
            }

            newSelectedObjectList.Add(newBuildableEdgeObject);
        }

        private void RotateSelectedBuildableCornerObject(BuildableCornerObject buildableCornerObject, bool rotateClockwise, ref List<BuildableObject> newSelectedObjectList)
        {
            BuildableCornerObjectSO buildableCornerObjectSO = (BuildableCornerObjectSO)buildableCornerObject.GetBuildableObjectSO();
            if (!buildableCornerObjectSO.isSelectedObjectRotatable) return;

            EasyGridBuilderPro easyGridBuilderPro = buildableCornerObject.GetOccupiedGridSystem();

            float rotation = 0f;
            switch (buildableCornerObjectSO.rotationType)
            {
                case CornerObjectRotationType.FourDirectionalRotation: 
                    FourDirectionalRotation currentFourDirectionalRotation = buildableCornerObject.GetObjectFourDirectionalRotation();
                    FourDirectionalRotation nextFourDirectionalRotation = rotateClockwise ? buildableCornerObjectSO.GetNextFourDirectionalDirectionClockwise(currentFourDirectionalRotation) : 
                        buildableCornerObjectSO.GetNextFourDirectionalDirectionCounterClockwise(currentFourDirectionalRotation);
                    
                    buildableCornerObject.SetObjectFourDirectionalRotation(nextFourDirectionalRotation);
                    rotation = buildableCornerObjectSO.GetFourDirectionalRotationAngle(nextFourDirectionalRotation);
                    if (easyGridBuilderPro is EasyGridBuilderProXZ) buildableCornerObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
                    else buildableCornerObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                break;
                case CornerObjectRotationType.EightDirectionalRotation:
                    EightDirectionalRotation currentEightDirectionalRotation = buildableCornerObject.GetObjectEightDirectionalRotation();
                    EightDirectionalRotation nextEightDirectionalRotation = rotateClockwise ? buildableCornerObjectSO.GetNextEightDirectionalDirectionClockwise(currentEightDirectionalRotation) : 
                        buildableCornerObjectSO.GetNextEightDirectionalDirectionClockwise(currentEightDirectionalRotation);

                    buildableCornerObject.SetObjectEightDirectionalRotation(nextEightDirectionalRotation);
                    rotation = buildableCornerObjectSO.GetEightDirectionalRotationAngle(nextEightDirectionalRotation); 
                    if (easyGridBuilderPro is EasyGridBuilderProXZ) buildableCornerObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
                    else buildableCornerObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                break;
                case CornerObjectRotationType.FreeRotation: freeRotateBuildableList.Add(buildableCornerObject); break;
            }

            newSelectedObjectList.Add(buildableCornerObject);
        }

        private void RotateSelectedBuildableFreeObject(BuildableFreeObject buildableFreeObject, bool rotateClockwise, ref List<BuildableObject> newSelectedObjectList)
        {
            BuildableFreeObjectSO buildableFreeObjectSO = (BuildableFreeObjectSO)buildableFreeObject.GetBuildableObjectSO();
            if (!buildableFreeObjectSO.isSelectedObjectRotatable) return;

            EasyGridBuilderPro easyGridBuilderPro = buildableFreeObject.GetOccupiedGridSystem();

            float rotation = 0f;
            switch (buildableFreeObjectSO.rotationType)
            {
                case FreeObjectRotationType.FourDirectionalRotation: 
                    FourDirectionalRotation currentFourDirectionalRotation = buildableFreeObject.GetObjectFourDirectionalRotation();
                    FourDirectionalRotation nextFourDirectionalRotation = rotateClockwise ? buildableFreeObjectSO.GetNextFourDirectionalDirectionClockwise(currentFourDirectionalRotation) : 
                        buildableFreeObjectSO.GetNextFourDirectionalDirectionCounterClockwise(currentFourDirectionalRotation);
                    
                    buildableFreeObject.SetObjectFourDirectionalRotation(nextFourDirectionalRotation);
                    rotation = buildableFreeObjectSO.GetFourDirectionalRotationAngle(nextFourDirectionalRotation);
                    if (easyGridBuilderPro is EasyGridBuilderProXZ) buildableFreeObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
                    else buildableFreeObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                break;
                case FreeObjectRotationType.EightDirectionalRotation:
                    EightDirectionalRotation currentEightDirectionalRotation = buildableFreeObject.GetObjectEightDirectionalRotation();
                    EightDirectionalRotation nextEightDirectionalRotation = rotateClockwise ? buildableFreeObjectSO.GetNextEightDirectionalDirectionClockwise(currentEightDirectionalRotation) : 
                        buildableFreeObjectSO.GetNextEightDirectionalDirectionClockwise(currentEightDirectionalRotation);

                    buildableFreeObject.SetObjectEightDirectionalRotation(nextEightDirectionalRotation);
                    rotation = buildableFreeObjectSO.GetEightDirectionalRotationAngle(nextEightDirectionalRotation); 
                    if (easyGridBuilderPro is EasyGridBuilderProXZ) buildableFreeObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
                    else buildableFreeObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                break;
                case FreeObjectRotationType.FreeRotation: freeRotateBuildableList.Add(buildableFreeObject); break;
            }

            newSelectedObjectList.Add(buildableFreeObject);
        }

        public void SetInputSelectedBuildableObjectRotationScrollComplete()
        {
            if (!enableSelectedObjectRotation) return;
            isClockwiseRotationKeyHolding = false;
            isCounterClockwiseRotationKeyHolding = false;
            freeRotateBuildableList.Clear();
        }

        public void SetInputSelectedBuildableObjectClockwiseRotationComplete()
        {
            if (!enableSelectedObjectRotation) return;
            isClockwiseRotationKeyHolding = false;
            freeRotateBuildableList.Clear();
        }

        public void SetInputSelectedBuildableObjectCounterClockwiseRotationComplete()
        {
            if (!enableSelectedObjectRotation) return;
            isCounterClockwiseRotationKeyHolding = false;
            freeRotateBuildableList.Clear();
        }

        private void UpdateRotationKeyHolding()
        {
            foreach (BuildableObject freeRotateBuildableObject in freeRotateBuildableList)
            {
                EasyGridBuilderPro easyGridBuilderPro = freeRotateBuildableObject.GetOccupiedGridSystem();
                float currentFreeRotation = freeRotateBuildableObject.GetObjectFreeRotation();
                BuildableObjectSO buildableObjectSO = freeRotateBuildableObject.GetBuildableObjectSO();

                if (buildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO) 
                {
                    float rotation = default;
                    if (isClockwiseRotationKeyHolding) rotation = FreeRotateClockwise(currentFreeRotation, buildableCornerObjectSO.freeRotationSpeed);
                    else if (isCounterClockwiseRotationKeyHolding) rotation = FreeRotateCounterClockwise(currentFreeRotation, buildableCornerObjectSO.freeRotationSpeed);

                    if (easyGridBuilderPro is EasyGridBuilderProXZ) freeRotateBuildableObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
                    else freeRotateBuildableObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                    freeRotateBuildableObject.SetObjectFreeRotation(rotation);
                }
                else if (buildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
                {
                    float rotation = default;
                    if (isClockwiseRotationKeyHolding) rotation = FreeRotateClockwise(currentFreeRotation, buildableFreeObjectSO.freeRotationSpeed);
                    else if (isCounterClockwiseRotationKeyHolding) rotation = FreeRotateCounterClockwise(currentFreeRotation, buildableFreeObjectSO.freeRotationSpeed);

                    if (easyGridBuilderPro is EasyGridBuilderProXZ) freeRotateBuildableObject.transform.rotation = Quaternion.Euler(0, rotation, 0);
                    else freeRotateBuildableObject.transform.rotation = Quaternion.Euler(0, 0, rotation);
                    freeRotateBuildableObject.SetObjectFreeRotation(rotation);
                }
            }
        }

        private float FreeRotateClockwise(float currentRotation, float rotationSpeed)
        {
            float targetRotation = currentRotation + rotationSpeed * Time.deltaTime;
            if (targetRotation >= 360f) targetRotation -= 360f;
            return targetRotation;
        }

        private float FreeRotateCounterClockwise(float currentRotation, float rotationSpeed)
        {
            float targetRotation = currentRotation - rotationSpeed * Time.deltaTime;
            if (targetRotation < 0f) targetRotation += 360f;
            return targetRotation;
        }

        public void SetInputSelectedBuildableEdgeObjectFlip()
        {
            if (!enableSelectedObjectRotation) return;
            if (!gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            List<BuildableObject> newSelectedObjectList = new List<BuildableObject>();

            foreach (BuildableObject selectedObject in selectedObjectsList)
            {
                if (selectedObject is BuildableEdgeObject buildableEdgeObject) FlipBuildableEdgeObject(buildableEdgeObject, buildableObjectDestroyer, ref newSelectedObjectList);
                else newSelectedObjectList.Add(selectedObject);
            }

            RecalculateSelectedObjectsList(newSelectedObjectList);
        }

        private void FlipBuildableEdgeObject(BuildableEdgeObject buildableEdgeObject, BuildableObjectDestroyer buildableObjectDestroyer, ref List<BuildableObject> newSelectedObjectList)
        {
            BuildableEdgeObjectSO buildableEdgeObjectSO = (BuildableEdgeObjectSO)buildableEdgeObject.GetBuildableObjectSO();
            if (!buildableEdgeObjectSO.isSelectedObjectRotatable) return;

            EasyGridBuilderPro easyGridBuilderPro = buildableEdgeObject.GetOccupiedGridSystem();
            int verticalGridIndex = buildableEdgeObject.GetOccupiedVerticalGridIndex();
            Vector3 objectOriginWorldPosition = buildableEdgeObject.GetObjectModifiedOriginWorldPosition();
            FourDirectionalRotation currentFourDirectionalRotation = buildableEdgeObject.GetObjectFourDirectionalRotation();
            bool isObjectFlipped = buildableEdgeObject.GetIsObjectFlipped();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = buildableEdgeObject.GetBuildableObjectSORandomPrefab();

            buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableEdgeObject, true, true, true);

            easyGridBuilderPro.TryInitializeBuildableEdgeObjectSinglePlacement(objectOriginWorldPosition, buildableEdgeObjectSO, currentFourDirectionalRotation, !isObjectFlipped, true, true, 
                verticalGridIndex, true, out BuildableEdgeObject newBuildableEdgeObject, objectRandomPrefab, buildableEdgeObject);

            newSelectedObjectList.Add(newBuildableEdgeObject);
        }
        #endregion Handle Input Rotate Selected Objects Functions End:

        #region Handle Input Copy Selected Object Functions Start:
        public void SetInputSelectionCopy()
        {
            if (!enableSelectedObjectCopy) return;
            if (selectedObjectsList.Count == 0) return;
            
            BuildableObjectSO buildableObjectSO = selectedObjectsList[0].GetBuildableObjectSO();
            gridManager.SetActiveGridModeInAllGrids(GridMode.BuildMode);
            activeEasyGridBuilderPro.SetInputActiveBuildableObjectSO(buildableObjectSO);
            
            ResetAreaSelection();
            ResetIndividualSelection();
        }
        #endregion Handle Input Copy Selected Object Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT EXECUTION FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///
        
        #region Initiate Select Buildable Objects Functions Start:
        private void InvokeSelectBuildableObject()
        {
            if (MouseInteractionUtilities.TryGetBuildableObject(selectableObjectsLayerMask, out BuildableObject buildableObject))
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();
                if (IsSelectionBlockedBySelectableObjectType(buildableObject)) return;

                if (!isMultiSelectionKeyHolding)
                {
                    for (int i = selectedObjectsList.Count - 1; i >= 0; i--)
                    {
                        BuildableObject _buildableObject = selectedObjectsList[i];
                        OnBuildableObjectDeselected?.Invoke(_buildableObject);
                        selectedObjectsList.RemoveAt(i);
                    }
                }
                
                if (selectedObjectsList.Contains(buildableObject))
                {
                    OnBuildableObjectDeselected?.Invoke(buildableObject);
                    selectedObjectsList.Remove(buildableObject);
                }
                else SelectBuildableObject(buildableObject);
            }
            else if (!isMultiSelectionKeyHolding)
            {
                ResetAreaSelection();
                ResetIndividualSelection();
            }
        }

        private void SelectBuildableObject(BuildableObject buildableObject)
        {
            if (!selectedObjectsList.Contains(buildableObject)) 
            {
                selectedObjectsList.Add(buildableObject);
                OnBuildableObjectSelected?.Invoke(buildableObject);
            }
        }
        #endregion Initiate Select Buildable Objects Functions End:

        public void SetSelectionMode(SelectionMode selectionMode)
        {
            if (this.selectionMode != selectionMode) this.selectionMode = selectionMode;
        }

        public bool GetGridCellIndicatorActiveSelf()
        {
            if (gridCellIndicator) return gridCellIndicator.gameObject.activeSelf;
            return false;
        }

        public bool TryGetGridCellIndicator(out GameObject gridCellIndicator)
        {
            gridCellIndicator = this.gridCellIndicator.gameObject;
            return this.gridCellIndicator != null;
        }

        public List<BuildableObject> GetSelectionHoveredBuildableObjectsList() => hoveredObjectsList;

        public List<BuildableObject> GetSelectedBuildableObjectsList() => selectedObjectsList;

        public void SetGridCellIndicatorActiveSelf(bool activeSelf) 
        {
            if (gridCellIndicator) gridCellIndicator.gameObject.SetActive(activeSelf);
        }
    }
}
