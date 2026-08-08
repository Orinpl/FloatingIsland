using System.Collections;
using SoulGames.Utilities;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Object Mover", 2)]
    public class BuildableObjectMover : MonoBehaviour
    {
        public event OnBuildableObjectHoverEnterDelegate OnBuildableObjectHoverEnter;
        public delegate void OnBuildableObjectHoverEnterDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectHoverExitDelegate OnBuildableObjectHoverExit;
        public delegate void OnBuildableObjectHoverExitDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectStartMovingDelegate OnBuildableObjectStartMoving;
        public delegate void OnBuildableObjectStartMovingDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectEndMovingDelegate OnBuildableObjectEndMoving;
        public delegate void OnBuildableObjectEndMovingDelegate(BuildableObject buildableObject);

        [SerializeField] private MovableBuildableObjectType movableObjectType = MovableBuildableObjectType.All;
        [SerializeField] private LayerMask movableObjectsLayerMask;

        [Space]
        [SerializeField] private bool blockMoveInGridModeDefault = true;
        [SerializeField] private bool blockMoveInGridModeBuild = true;
        [SerializeField] private bool blockMoveInGridModeDestroy = true;
        [SerializeField] private bool blockMoveInSGridModeSelect = true;
        [SerializeField] private bool blockMoveInGridModeMove;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private BuildableObject previousHoveredObject;
        private BuildableObject movingObject;
        private bool isMovingStarted;
        private bool isMovingStartedByBuildableObjectSelector;

        private GridObjectPlacementType originalGridObjectPlacementType;
        private EdgeObjectPlacementType originalEdgeObjectPlacementType;
        private CornerObjectPlacementType originalCornerObjectPlacementType;
        private FreeObjectPlacementType originalFreeObjectPlacementType;
        private bool isOriginalBuildableEdgeObjectMergeWithBuildableCornerObject;

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT MOVER INITIALIZE FUNCTIONS                                   ///
        ///-------------------------------------------------------------------------------///

        private void Start() 
        {
            StartCoroutine(LateStart());
        }

        private void OnDestroy()
        {
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged -= OnActiveGridModeChanged;
            gridManager.OnBuildableObjectPlaced -= OnBuildableObjectPlaced;
        }

        #region Initialization Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            gridManager = GridManager.Instance;
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;
            gridManager.OnBuildableObjectPlaced += OnBuildableObjectPlaced;

            activeEasyGridBuilderPro = GridManager.Instance.GetActiveEasyGridBuilderPro();
        }

        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderProSystem)
        {
            activeEasyGridBuilderPro = activeEasyGridBuilderProSystem;
        }

        private void OnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            ResetIndividualSelection();
            if (gridMode != GridMode.BuildMode && gridMode != GridMode.MoveMode) ResetMovingObject(true);
        }

        private void OnBuildableObjectPlaced(EasyGridBuilderPro _, BuildableObject buildableObject)
        {
            if (!isMovingStarted || buildableObject != movingObject) return;

            gridManager.SetIsObjectMovingInAllGrids(false);
            gridManager.SetMovingBuildableObjectInAllGrids(null);
            gridManager.SetActiveGridModeInAllGrids(GridMode.MoveMode);

            OnBuildableObjectEndMoving?.Invoke(movingObject);
            movingObject = null;

            StartCoroutine(LateSetOriginalObjectValues(buildableObject.GetBuildableObjectSO()));
        }

        private IEnumerator LateSetOriginalObjectValues(BuildableObjectSO buildableObjectSO)
        {
            yield return new WaitForEndOfFrame();
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: buildableGridObjectSO.placementType = originalGridObjectPlacementType; break;
                case BuildableEdgeObjectSO buildableEdgeObjectSO: 
                    buildableEdgeObjectSO.placementType = originalEdgeObjectPlacementType; 
                    buildableEdgeObjectSO.mergeWithBuildableCornerObject =  isOriginalBuildableEdgeObjectMergeWithBuildableCornerObject;
                break;
                case BuildableCornerObjectSO buildableCornerObjectSO: buildableCornerObjectSO.placementType = originalCornerObjectPlacementType; break;
                case BuildableFreeObjectSO buildableFreeObjectSO: buildableFreeObjectSO.placementType = originalFreeObjectPlacementType; break;
            }
            
            isMovingStarted = false;

            if (isMovingStartedByBuildableObjectSelector) gridManager.SetActiveGridModeInAllGrids(GridMode.SelectMode);
            isMovingStartedByBuildableObjectSelector = false;
        }
        #endregion Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT MOVER UPDATE FUNCTIONS                                       ///
        ///-------------------------------------------------------------------------------///
        
        private void Update()
        {
            if (!activeEasyGridBuilderPro) return;
            if (activeEasyGridBuilderPro.GetUseMoveModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.MoveMode) return;
            if (IsMovingBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            UpdateMoveHover();
        }

        #region Update Functions Start:
        private bool IsMovingBlockedByActiveGridMode(GridMode gridMode)
        {
            switch (gridMode)
            {
                case GridMode.None : return blockMoveInGridModeDefault;
                case GridMode.BuildMode : return blockMoveInGridModeBuild;
                case GridMode.DestroyMode : return blockMoveInGridModeDestroy;
                case GridMode.SelectMode : return blockMoveInSGridModeSelect;
                case GridMode.MoveMode : return blockMoveInGridModeMove;
            }
            return true;
        }

        private bool IsMovingBlockedByMovableObjectType(BuildableObject buildableObject)
        {
            switch (movableObjectType)
            {
                case MovableBuildableObjectType.All : return false;
                case MovableBuildableObjectType.BuildableGridObject : 
                    if (buildableObject is BuildableGridObject) return false;
                    else return true;
                case MovableBuildableObjectType.BuildableEdgeObject :
                    if (buildableObject is BuildableEdgeObject) return false;
                    else return true;
                case MovableBuildableObjectType.BuildableCornerObject :
                    if (buildableObject is BuildableCornerObject) return false;
                    else return true;
                case MovableBuildableObjectType.BuildableFreeObject :
                    if (buildableObject is BuildableFreeObject) return false;
                    else return true;
            }
            return true;
        }
        
        private void UpdateMoveHover()
        {
            if (MouseInteractionUtilities.TryGetBuildableObject(movableObjectsLayerMask, out BuildableObject buildableObject))
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();
                if (IsMovingBlockedByMovableObjectType(buildableObject)) return;
                    
                // If hovering over a different object than before, call the OnBuildableObjectHoverExit for the previous object
                if (previousHoveredObject != null && previousHoveredObject != buildableObject) OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);

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
            ResetIndividualSelection();
            ResetMovingObject(true);
        }

        public void SetInputObjectMovingReset()
        {
            ResetIndividualSelection();
            ResetMovingObject();
        }

        private void ResetIndividualSelection()
        {
            if (previousHoveredObject != null)
            {
                OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);
                previousHoveredObject = null;
            }
        }

        private void ResetMovingObject(bool invokedByGridModeReset = false)
        {
            if (!isMovingStarted) return;

            isMovingStarted = false;

            switch (movingObject)
            {
                case BuildableGridObject buildableGridObject: ResetMovingBuildableGridObject(buildableGridObject); break;
                case BuildableEdgeObject buildableEdgeObject: ResetMovingBuildableEdgeObject(buildableEdgeObject); break;
                case BuildableCornerObject buildableCornerObject: ResetMovingBuildableCornerObject(buildableCornerObject); break;
                case BuildableFreeObject buildableFreeObject: ResetMovingBuildableFreeObject(buildableFreeObject); break;
            }

            gridManager.SetIsObjectMovingInAllGrids(false);
            gridManager.SetMovingBuildableObjectInAllGrids(null);
            if (!invokedByGridModeReset) gridManager.SetActiveGridModeInAllGrids(GridMode.MoveMode);
            if (isMovingStartedByBuildableObjectSelector) gridManager.SetActiveGridModeInAllGrids(GridMode.SelectMode);
            isMovingStartedByBuildableObjectSelector = false;
            OnBuildableObjectEndMoving?.Invoke(movingObject);
            movingObject = null;
        }

        private void ResetMovingBuildableGridObject(BuildableGridObject builtBuildableGridObject)
        {
            BuildableObjectSO buildableObjectSO = builtBuildableGridObject.GetBuildableObjectSO();
            EasyGridBuilderPro easyGridBuilderPro = builtBuildableGridObject.GetOccupiedGridSystem();
            int verticalGridIndex = builtBuildableGridObject.GetOccupiedVerticalGridIndex();
            Vector2Int objectOriginCellPosition = builtBuildableGridObject.GetObjectOriginCellPosition(out _);
            Vector3 objectOriginWorldPosition = easyGridBuilderPro.GetCellWorldPosition(objectOriginCellPosition, verticalGridIndex);
            FourDirectionalRotation fourDirectionalRotation = builtBuildableGridObject.GetObjectFourDirectionalRotation();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = builtBuildableGridObject.GetBuildableObjectSORandomPrefab();

            easyGridBuilderPro.TryInitializeBuildableGridObjectSinglePlacement(objectOriginWorldPosition, (BuildableGridObjectSO)buildableObjectSO, fourDirectionalRotation, true, true, 
                verticalGridIndex, true, out _, objectRandomPrefab, builtBuildableGridObject);

            StartCoroutine(LateSetOriginalObjectValues(buildableObjectSO));
        }

        private void ResetMovingBuildableEdgeObject(BuildableEdgeObject builtBuildableEdgeObject)
        {
            BuildableObjectSO buildableObjectSO = builtBuildableEdgeObject.GetBuildableObjectSO();
            EasyGridBuilderPro easyGridBuilderPro = builtBuildableEdgeObject.GetOccupiedGridSystem();
            int verticalGridIndex = builtBuildableEdgeObject.GetOccupiedVerticalGridIndex();
            Vector3 objectOriginWorldPosition = builtBuildableEdgeObject.GetObjectModifiedOriginWorldPosition();
            FourDirectionalRotation fourDirectionalRotation = builtBuildableEdgeObject.GetObjectFourDirectionalRotation();
            bool isObjectFlipped = builtBuildableEdgeObject.GetIsObjectFlipped();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = builtBuildableEdgeObject.GetBuildableObjectSORandomPrefab();

            easyGridBuilderPro.TryInitializeBuildableEdgeObjectSinglePlacement(objectOriginWorldPosition, (BuildableEdgeObjectSO)buildableObjectSO, fourDirectionalRotation, isObjectFlipped, true, true, 
                verticalGridIndex, true, out _, objectRandomPrefab, builtBuildableEdgeObject);
            
            StartCoroutine(LateSetOriginalObjectValues(buildableObjectSO));
        }

        private void ResetMovingBuildableCornerObject(BuildableCornerObject builtBuildableCornerObject)
        {
            BuildableObjectSO buildableObjectSO = builtBuildableCornerObject.GetBuildableObjectSO();
            EasyGridBuilderPro easyGridBuilderPro = builtBuildableCornerObject.GetOccupiedGridSystem();
            int verticalGridIndex = builtBuildableCornerObject.GetOccupiedVerticalGridIndex();
            Vector3 objectOriginWorldPosition = builtBuildableCornerObject.GetObjectOriginWorldPosition();
            FourDirectionalRotation fourDirectionalRotation = builtBuildableCornerObject.GetObjectFourDirectionalRotation();
            EightDirectionalRotation eightDirectionalRotation = builtBuildableCornerObject.GetObjectEightDirectionalRotation();
            float freRotation = builtBuildableCornerObject.GetObjectFreeRotation();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = builtBuildableCornerObject.GetBuildableObjectSORandomPrefab();

            easyGridBuilderPro.TryInitializeBuildableCornerObjectSinglePlacement(objectOriginWorldPosition, (BuildableCornerObjectSO)buildableObjectSO, fourDirectionalRotation, eightDirectionalRotation,
                freRotation, true, true, verticalGridIndex, true, out _, objectRandomPrefab, builtBuildableCornerObject);
            
            StartCoroutine(LateSetOriginalObjectValues(buildableObjectSO));
        }

        private void ResetMovingBuildableFreeObject(BuildableFreeObject builtBuildableFreeObject)
        {
            BuildableObjectSO buildableObjectSO = builtBuildableFreeObject.GetBuildableObjectSO();
            EasyGridBuilderPro easyGridBuilderPro = builtBuildableFreeObject.GetOccupiedGridSystem();
            int verticalGridIndex = builtBuildableFreeObject.GetOccupiedVerticalGridIndex();
            Vector3 objectOriginWorldPosition = builtBuildableFreeObject.GetObjectOriginWorldPosition();
            FourDirectionalRotation fourDirectionalRotation = builtBuildableFreeObject.GetObjectFourDirectionalRotation();
            EightDirectionalRotation eightDirectionalRotation = builtBuildableFreeObject.GetObjectEightDirectionalRotation();
            float freRotation = builtBuildableFreeObject.GetObjectFreeRotation();
            BuildableObjectSO.RandomPrefabs objectRandomPrefab = builtBuildableFreeObject.GetBuildableObjectSORandomPrefab();

            easyGridBuilderPro.TryInitializeBuildableFreeObjectSinglePlacement(objectOriginWorldPosition, (BuildableFreeObjectSO)buildableObjectSO, fourDirectionalRotation, eightDirectionalRotation,
                freRotation, Vector3.zero, true, verticalGridIndex, true, out _, objectRandomPrefab, builtBuildableFreeObject);
            
            StartCoroutine(LateSetOriginalObjectValues(buildableObjectSO));
        }
        #endregion Handle Input Grid Mode Functions End:
    
        #region Handle Input Start Move Functions Start:    
        public void SetInputStartMoveBuildableObject()
        {
            if (activeEasyGridBuilderPro.GetUseMoveModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.MoveMode) return;
            if (IsMovingBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;
            if (isMovingStarted) return;

            ResetIndividualSelection();
            InvokeMoveBuildableObject();
        }

        private void InvokeMoveBuildableObject()
        {
            if (MouseInteractionUtilities.TryGetBuildableObject(movableObjectsLayerMask, out BuildableObject buildableObject))
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();
                if (IsMovingBlockedByMovableObjectType(buildableObject)) return;

                StartMovingObject(buildableObject);
            }
        }

        public void StartMovingObject(BuildableObject buildableObject, bool invokedByBuildableObjectSelector = false)
        {
            BuildableObjectSO buildableObjectSO = buildableObject.GetBuildableObjectSO();
            if (!buildableObjectSO.isObjectMovable) return;
            if (!gridManager.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) return;
            
            isMovingStarted = true;
            if (invokedByBuildableObjectSelector) isMovingStartedByBuildableObjectSelector = true;
            OnBuildableObjectStartMoving?.Invoke(buildableObject);

            movingObject = buildableObject;
            buildableObject.transform.parent = null;
            buildableObject.transform.position = new Vector3(99999, 99999, 99999);
            buildableObjectDestroyer.SetInputDestroyBuildableObject(buildableObject, true, true, true);

            CacheMovingObjectOriginalValues(buildableObjectSO);
            
            gridManager.SetActiveGridModeInAllGrids(GridMode.BuildMode);
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                SetBuildableObject(easyGridBuilderPro, buildableObjectSO, buildableObject);
            }
            gridManager.SetIsObjectMovingInAllGrids(true);
            gridManager.SetMovingBuildableObjectInAllGrids(buildableObject);
        }

        private void CacheMovingObjectOriginalValues(BuildableObjectSO buildableObjectSO)
        {
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: originalGridObjectPlacementType = buildableGridObjectSO.placementType; break;
                case BuildableEdgeObjectSO buildableEdgeObjectSO: 
                    originalEdgeObjectPlacementType = buildableEdgeObjectSO.placementType; 
                    isOriginalBuildableEdgeObjectMergeWithBuildableCornerObject = buildableEdgeObjectSO.mergeWithBuildableCornerObject;
                    buildableEdgeObjectSO.mergeWithBuildableCornerObject = false;
                break;
                case BuildableCornerObjectSO buildableCornerObjectSO: originalCornerObjectPlacementType = buildableCornerObjectSO.placementType; break;
                case BuildableFreeObjectSO buildableFreeObjectSO: originalFreeObjectPlacementType = buildableFreeObjectSO.placementType; break;
            }
        }

        private void SetBuildableObject(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO, BuildableObject buildableObject)
        {
            easyGridBuilderPro.SetInputActiveBuildableObjectSO(buildableObjectSO, buildableObject.GetBuildableObjectSORandomPrefab());
            easyGridBuilderPro.SetActiveBuildableObjectSOPlacementType();

            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO : easyGridBuilderPro.SetActiveBuildableGridObjectRotation(buildableObject.GetObjectFourDirectionalRotation()); break;
                case BuildableEdgeObjectSO : 
                    easyGridBuilderPro.SetActiveBuildableEdgeObjectRotation(buildableObject.GetObjectFourDirectionalRotation());
                    easyGridBuilderPro.SetActiveBuildableEdgeObjectFlipped(buildableObject.GetIsObjectFlipped());
                break;
                case BuildableCornerObjectSO : 
                    easyGridBuilderPro.SetActiveBuildableCornerObjectFourDirectionalRotation(buildableObject.GetObjectFourDirectionalRotation());
                    easyGridBuilderPro.SetActiveBuildableCornerObjectEightDirectionalRotation(buildableObject.GetObjectEightDirectionalRotation());
                    easyGridBuilderPro.SetActiveBuildableCornerObjectFreeRotation(buildableObject.GetObjectFreeRotation());
                break;
                case BuildableFreeObjectSO :
                    easyGridBuilderPro.SetActiveBuildableFreeObjectFourDirectionalRotation(buildableObject.GetObjectFourDirectionalRotation());
                    easyGridBuilderPro.SetActiveBuildableFreeObjectEightDirectionalRotation(buildableObject.GetObjectEightDirectionalRotation());
                    easyGridBuilderPro.SetActiveBuildableFreeObjectFreeRotation(buildableObject.GetObjectFreeRotation());
                break; 
            }
        }
        #endregion Handle Input Start Move Functions End:   

        public BuildableObject GetMovingObject() => movingObject;
        
        public bool GetIsMovingStarted() => isMovingStarted;
    }
}