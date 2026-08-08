using UnityEngine;
using SoulGames.Utilities;
using System.Collections;
using UnityEngine.Splines;
using System.Collections.Generic;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Free Object Ghost", 6)]
    public class BuildableFreeObjectGhost : MonoBehaviour
    {
        [SerializeField] private LayerMask ghostObjectLayer;
        [SerializeField] private float ghostObjectMoveSpeed = 25f;
        [SerializeField] private float ghostObjectRotationSpeed = 25f;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private BuildableObjectSO activeBuildableObjectSO;
        private BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab;
        private FreeObjectPlacementType activeBuildableFreeObjectSOPlacementType;
        private Transform ghostTransformVisual;
        private Transform parentTransform;

        private SplineContainer buildableFreeObjectSplineContainer;
        private Spline buildableFreeObjectSpline;
        private float buildableFreeObjectSplineObjectSpacing;
        private bool buildableFreeObjectSplineEditing = false;
        private Transform splinePlacementHolderObject;
        private List<GameObject> ghostTransformVisualList;
        
        private const float ADDITIVE_SCALE = 0.01f;
        private const float BOX_PLACEMENT_OBJECT_GRID_ALPHA_MASK_SCALE_MULTIPLIER = 1.5f;
        private const string GRID_AREA_VISUAL_GENERATOR_QUAD_NAME = "GridAreaVisualGeneratorQuad";

        ///-------------------------------------------------------------------------------///
        /// GHOST OBJECT INITIALIZE FUNCTIONS                                             ///
        ///-------------------------------------------------------------------------------///
        private void Start()
        {
            InitializeDataStructures();
            StartCoroutine(LateStart());
        }
        
        private void OnDestroy()
        {
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
            gridManager.OnIsMousePointerOnGridChanged -= OnIsMousePointerOnGridChanged;

            gridManager.OnActiveBuildableSOChanged -= OnActiveBuildableSOChanged;
            gridManager.OnActiveGridModeChanged -= OnActiveGridModeChanged;
            gridManager.OnBuildableObjectPlaced -= OnBuildableObjectPlaced;
            gridManager.OnFreeObjectSplinePlacementStartedAndUpdated -= OnFreeObjectSplinePlacementStartedAndUpdated;
            gridManager.OnFreeObjectSplinePlacementFinalized -= OnFreeObjectSplinePlacementFinalized;
            gridManager.OnFreeObjectSplinePlacementCancelled -= OnFreeObjectSplinePlacementCancelled;
        }

        #region Ghost Object Initialization Functions Start:
        private void InitializeDataStructures()
        {
            ghostTransformVisualList = new List<GameObject>();
        }

        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            gridManager = GridManager.Instance;
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnIsMousePointerOnGridChanged += OnIsMousePointerOnGridChanged;

            gridManager.OnActiveBuildableSOChanged += OnActiveBuildableSOChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;
            gridManager.OnFreeObjectSplinePlacementStartedAndUpdated += OnFreeObjectSplinePlacementStartedAndUpdated;
            gridManager.OnFreeObjectSplinePlacementFinalized += OnFreeObjectSplinePlacementFinalized;
            gridManager.OnFreeObjectSplinePlacementCancelled += OnFreeObjectSplinePlacementCancelled;

            activeEasyGridBuilderPro = gridManager.GetActiveEasyGridBuilderPro();
        }
        #endregion Ghost Object Initialization Functions End:
        
        #region Ghost Object Event Functions Start:
        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderPro)
        {
            this.activeEasyGridBuilderPro = activeEasyGridBuilderPro;
            UpdateActiveBuildableObjects();
        }

        private void OnIsMousePointerOnGridChanged(bool isMousePointerOnGrid)
        {
            UpdateActiveBuildableObjects();
        }
        
        private void OnActiveBuildableSOChanged(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            UpdateActiveBuildableObjects();
        }

        private void OnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            UpdateActiveBuildableObjects();
        }

        private void OnBuildableObjectPlaced(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject)
        {
            UpdateActiveBuildableObjects();
        }

        private void OnFreeObjectSplinePlacementStartedAndUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 worldPosition, FreeObjectPlacementType freeObjectPlacementType)
        {
            buildableFreeObjectSplineEditing = true;
            if (!splinePlacementHolderObject) splinePlacementHolderObject = new GameObject("Box Placement Holder").transform;
            StartAndUpdateFreeObjectSplinePlacementGhost(worldPosition);
            activeBuildableFreeObjectSOPlacementType = freeObjectPlacementType;
        }

        private void OnFreeObjectSplinePlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            buildableFreeObjectSplineEditing = false;
            if (splinePlacementHolderObject) Destroy(splinePlacementHolderObject.gameObject);
            activeBuildableFreeObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableFreeObjectSOPlacementType();
            CancelFreeObjectSplinePlacementGhost();
            
            if (activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement) RefreshGhostObjectVisual(true);
        }

        private void OnFreeObjectSplinePlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            buildableFreeObjectSplineEditing = false;
            if (splinePlacementHolderObject) Destroy(splinePlacementHolderObject.gameObject);
            activeBuildableFreeObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableFreeObjectSOPlacementType();
            CancelFreeObjectSplinePlacementGhost();

            if (activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement) RefreshGhostObjectVisual(true);
        }

        private void UpdateActiveBuildableObjects()
        {
            activeBuildableObjectSO = activeEasyGridBuilderPro.GetActiveBuildableObjectSO();
            activeBuildableObjectSORandomPrefab = activeEasyGridBuilderPro.GetActiveBuildableObjectSORandomPrefab();
            activeBuildableFreeObjectSOPlacementType = activeEasyGridBuilderPro.GetActiveBuildableFreeObjectSOPlacementType();
            RefreshGhostObjectVisual();
        }
        #endregion Ghost Object Event Functions End:

        ///-------------------------------------------------------------------------------///
        /// GHOST OBJECT UPDATE FUNCTIONS                                                 ///
        ///-------------------------------------------------------------------------------///

        private void Update()
        {
            if (activeEasyGridBuilderPro == null) return;
            if (activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.BuildMode || activeBuildableObjectSO == null) ClearSpawnedObjects();

            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes() && buildableFreeObjectSplineEditing) 
            {
                buildableFreeObjectSplineObjectSpacing = activeEasyGridBuilderPro.GetActiveBuildableFreeObjectSplineSpacing();
                UpdateFreeObjectSplinePlacementGhostObjects(buildableFreeObjectSO);
            }

            UpdateVisualMaterials();
        }

        private void LateUpdate()
        {
            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO && activeEasyGridBuilderPro.GetActiveGridMode() is GridMode.BuildMode)
            {
                if (MouseInteractionUtilities.IsMousePointerOverUI()) return;
                UpdateGhostObjectPosition(buildableFreeObjectSO, out Vector3 hitNormals);
                UpdateGhostObjectRotation(buildableFreeObjectSO, hitNormals);
            }
        }

        #region Ghost Object Update Functions Start:
        private void UpdateGhostObjectPosition(BuildableFreeObjectSO buildableFreeObjectSO, out Vector3 hitNormals)
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();
            Vector3 secondRayDirection = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;

            Vector3 targetPosition = MouseInteractionUtilities.GetMouseWorldPositionForBuildableFreeObject(buildableFreeObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask(), 
                buildableFreeObjectSO, secondRayDirection, out hitNormals);
            if (buildableFreeObjectSO.raiseObjectWithVerticalGrids) 
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) targetPosition += new Vector3(0, activeVerticalGridIndex * verticalGridHeight, 0);
                else targetPosition += new Vector3(0, 0, -activeVerticalGridIndex * verticalGridHeight);
            }
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * ghostObjectMoveSpeed);
        }

        private void UpdateGhostObjectRotation(BuildableFreeObjectSO buildableFreeObjectSO, Vector3 hitNormals)
        {
            float targetAngle = 0f;
            switch (buildableFreeObjectSO.rotationType)
            {
                case FreeObjectRotationType.FourDirectionalRotation: targetAngle = buildableFreeObjectSO.GetFourDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableFreeObjectFourDirectionalRotation()); break;
                case FreeObjectRotationType.EightDirectionalRotation: targetAngle = buildableFreeObjectSO.GetEightDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableFreeObjectEightDirectionalRotation()); break;
                case FreeObjectRotationType.FreeRotation: targetAngle = activeEasyGridBuilderPro.GetActiveBuildableFreeObjectFreeRotation(); break;
            }
            Quaternion targetRotation;

            if (buildableFreeObjectSO.faceCollidingSurfaceNormalDirection && hitNormals != Vector3.zero) targetRotation = Quaternion.LookRotation(hitNormals);
            else targetRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, targetAngle, 0) : Quaternion.Euler(0, 0, -targetAngle);
            
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * ghostObjectRotationSpeed);
        }
        #endregion Ghost Object Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// GHOST VISUAL PLACEMENT HANDLING FUNCTIONS                                     ///
        ///-------------------------------------------------------------------------------///

        #region Handle Single Placement Functions Start:
        private void RefreshGhostObjectVisual(bool invokedBySplinePlacementFunctions = false)
        {
            ClearSpawnedObjects();
            if (!invokedBySplinePlacementFunctions) 
            {
                if (IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes() || activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.BuildMode) return;
            }

            if (activeBuildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
            {
                if (activeBuildableObjectSORandomPrefab.ghostObjectPrefab == null) activeBuildableObjectSORandomPrefab.ghostObjectPrefab = activeBuildableObjectSORandomPrefab.objectPrefab;

                ghostTransformVisual = Instantiate(activeBuildableObjectSORandomPrefab.ghostObjectPrefab, Vector3.zero, Quaternion.identity);
                BuildableFreeObject buildableFreeObject = ghostTransformVisual.GetComponent<BuildableFreeObject>();
                buildableFreeObject.SetBasicGridAreaTrigger();
                buildableFreeObject.SetIsInstantiatedByGhostObject(true);
                parentTransform = new GameObject(buildableFreeObjectSO.objectName).transform;

                SetUpTransformHierarchy(ghostTransformVisual, parentTransform, transform);
                parentTransform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);

                UpdateVisualMaterials();
                SetLayerRecursive(parentTransform.gameObject, GetHighestLayerSet());
            }
        }

        private bool IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableFreeObjectSOPlacementType == FreeObjectPlacementType.SplinePlacement) return true;
            return false;
        }

        private void SetUpTransformHierarchy(Transform ghostObjectVisual, Transform parentTransform, Transform grandParentTransform)
        {
            ghostObjectVisual.parent = parentTransform;
            ghostObjectVisual.localPosition = Vector3.zero;
            ghostObjectVisual.localEulerAngles = Vector3.zero;

            parentTransform.parent = grandParentTransform;
            parentTransform.localPosition = Vector3.zero;
            parentTransform.localEulerAngles = Vector3.zero;
        }

        private void ClearSpawnedObjects()
        {
            if (parentTransform)
            {
                Destroy(parentTransform.gameObject);
                parentTransform = null;
                ghostTransformVisual = null;
            }
        }
        #endregion Handle Single Placement Functions End:

        #region Handle Spline Placement Functions Start:
        private void StartAndUpdateFreeObjectSplinePlacementGhost(Vector3 worldPosition)
        {
            BuildableFreeObjectSO buildableFreeObjectSO = activeBuildableObjectSO as BuildableFreeObjectSO;
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();

            if (buildableFreeObjectSplineContainer == null)
            {
                buildableFreeObjectSplineContainer = new GameObject("RuntimeSplineContainerGhost").AddComponent<SplineContainer>();
                buildableFreeObjectSplineContainer.transform.parent = activeEasyGridBuilderPro.transform;
                buildableFreeObjectSpline = buildableFreeObjectSplineContainer.Spline;
                if (buildableFreeObjectSO.closedSpline) buildableFreeObjectSpline.Closed = true;
            }

            float customHeight = 0f;
            if (buildableFreeObjectSO.raiseObjectWithVerticalGrids) customHeight = -activeVerticalGridIndex * verticalGridHeight;

            BezierKnot knot = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new BezierKnot(worldPosition + new Vector3(0, customHeight, 0)) : new BezierKnot(worldPosition + new Vector3(0, 0, customHeight));
            TangentMode tangentMode = buildableFreeObjectSO.splineTangetMode == SplineTangetMode.AutoSmooth ? TangentMode.AutoSmooth : TangentMode.Linear;
            buildableFreeObjectSpline.Add(knot, tangentMode);

            UpdateFreeObjectSplinePlacementGhostObjects(buildableFreeObjectSO);
        }

        private void UpdateFreeObjectSplinePlacementGhostObjects(BuildableFreeObjectSO buildableFreeObjectSO)
        {
            // Remove all current temporary objects
            foreach (GameObject transformVisual in ghostTransformVisualList)
            {
                Destroy(transformVisual);
            }
            ghostTransformVisualList.Clear();

            // Get the total length of the spline and start placing temporary objects
            SpawnTemporaryGhostVisualsAlongSpline(buildableFreeObjectSO);
        }

        private void SpawnTemporaryGhostVisualsAlongSpline(BuildableFreeObjectSO buildableFreeObjectSO)
        {
            if (buildableFreeObjectSpline == null) return;

            Transform firstPrefab = buildableFreeObjectSO.randomPrefabs[0].ghostObjectPrefab;
            if (firstPrefab == null) firstPrefab = buildableFreeObjectSO.randomPrefabs[0].objectPrefab;

            float splineLength = buildableFreeObjectSpline.GetLength();
            float step = buildableFreeObjectSplineObjectSpacing / splineLength;  // Step size based on spacing and spline length
            float iteration = 0f;  // Start at the beginning of the spline

            // Avoid infinite loops or issues with extremely small steps
            if (step <= 0f) return;

            // Iterate over the spline using normalized t values to place temporary objects
            while (iteration <= 1f)
            {
                Vector3 position = buildableFreeObjectSpline.EvaluatePosition(iteration);  // Get the position at the current interation value
                GameObject ghostVisual = Instantiate(firstPrefab.gameObject, position, Quaternion.identity);  // Spawn the temp object
                SetUpTransformHierarchyForSplineObjects(buildableFreeObjectSO, ghostVisual);
                
                if (buildableFreeObjectSO.objectRotateToSplineDirection) 
                {
                    Vector3 tangent = buildableFreeObjectSpline.EvaluateTangent(iteration);
                    if (tangent != Vector3.zero)
                    {
                        if (activeEasyGridBuilderPro is EasyGridBuilderProXZ)
                        {
                            // XZ Grid: Forward is tangent, up is Y
                            ghostVisual.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
                        }
                        else
                        {
                            // XY Grid: Forward is tangent, adjust up to ensure proper alignment
                            Quaternion baseRotation = Quaternion.LookRotation(tangent, Vector3.forward);
                            // Correct orientation to stand "up" in the XY grid
                            ghostVisual.transform.rotation = baseRotation * Quaternion.Euler(90, 0, 90);
                        }
                    }
                }
                else 
                {
                    float targetAngle = 0f;
                    switch (buildableFreeObjectSO.rotationType)
                    {
                        case FreeObjectRotationType.FourDirectionalRotation: targetAngle = buildableFreeObjectSO.GetFourDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableFreeObjectFourDirectionalRotation()); break;
                        case FreeObjectRotationType.EightDirectionalRotation: targetAngle = buildableFreeObjectSO.GetEightDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableFreeObjectEightDirectionalRotation()); break;
                        case FreeObjectRotationType.FreeRotation: targetAngle = activeEasyGridBuilderPro.GetActiveBuildableFreeObjectFreeRotation(); break;
                    }
                    ghostVisual.transform.rotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(ghostVisual.transform.rotation.x, targetAngle, ghostVisual.transform.rotation.z) :
                        Quaternion.Euler(ghostVisual.transform.rotation.x, ghostVisual.transform.rotation.y, targetAngle);
                }

                ghostTransformVisualList.Add(ghostVisual);  // Track the temp object

                iteration += step;  // Increment t to move to the next point on the spline
                if (iteration > 1f && buildableFreeObjectSpline.Closed) break;
            }
            UpdateVisualMaterials();
        }

        private void SetUpTransformHierarchyForSplineObjects(BuildableFreeObjectSO buildableFreeObjectSO, GameObject ghostVisual)
        {
            BuildableFreeObject buildableFreeObject = ghostVisual.GetComponent<BuildableFreeObject>();
            buildableFreeObject.SetBasicGridAreaTrigger();
            buildableFreeObject.SetIsInstantiatedByGhostObject(true);
            buildableFreeObject.gameObject.name = buildableFreeObjectSO.objectName;
            buildableFreeObject.transform.parent = splinePlacementHolderObject;

            buildableFreeObject.transform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);
            SetLayerRecursive(buildableFreeObject.gameObject, GetHighestLayerSet());
        }

        private void CancelFreeObjectSplinePlacementGhost()
        {
            if (buildableFreeObjectSplineContainer != null)
            {
                Destroy(buildableFreeObjectSplineContainer.gameObject);
                buildableFreeObjectSplineContainer = null;
                buildableFreeObjectSpline = null;
            }
            ghostTransformVisualList.Clear();
        }
        #endregion Handle Spline Placement Functions End:

        ///-------------------------------------------------------------------------------///
        /// GHOST VISUAL SUPPORTER FUNCTIONS                                              ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle Visual Material Functions Start:
        private void UpdateVisualMaterials()
        {
            UpdateSingleVisualMaterial();
            UpdateMultipleVisualMaterial();
        }

        private void UpdateSingleVisualMaterial()
        {
            if (ghostTransformVisual != null)
            {
                Material selectedMaterial = DetermineMaterialBasedOnPlacement();
                if (selectedMaterial == null) return;
                ApplyMaterialToGameObjectAndChildren(ghostTransformVisual.gameObject, selectedMaterial);
            }
        }

        private void UpdateMultipleVisualMaterial()
        {
            if (ghostTransformVisualList.Count == 0) return;

            foreach (GameObject ghostVisual in ghostTransformVisualList)
            {
                Material selectedMaterial = DetermineMaterialBasedOnSplinePlacement(ghostVisual.transform.position);
                if (selectedMaterial == null) return;
                ApplyMaterialToGameObjectAndChildren(ghostVisual, selectedMaterial);
            }
        }

        private Material DetermineMaterialBasedOnPlacement()
        {
            bool isPlaceable = IsVisualObjectPlaceable();
            return GetPlacementMaterial(isPlaceable);
        }

        private Material DetermineMaterialBasedOnSplinePlacement(Vector3 worldPosition)
        {
            bool isPlaceable = IsVisualObjectPlaceable(true, worldPosition);
            return GetPlacementMaterial(isPlaceable);
        }

        private Material GetPlacementMaterial(bool isPlaceable)
        {
            if (!isPlaceable && activeBuildableObjectSO.invalidPlacementMaterial != null)
            {
                return activeBuildableObjectSO.invalidPlacementMaterial;
            }
            else if (activeBuildableObjectSO.validPlacementMaterial != null)
            {
                return activeBuildableObjectSO.validPlacementMaterial;
            }
            return null;
        }

        private bool IsVisualObjectPlaceable(bool invokedFromSplinePlacement = false, Vector3 worldPosition = default)
        {
            BuildableFreeObjectSO buildableFreeObjectSO = (BuildableFreeObjectSO)activeBuildableObjectSO;
            
            Vector3 secondRayDirection = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;
            if (!invokedFromSplinePlacement) worldPosition = MouseInteractionUtilities.GetMouseWorldPositionForBuildableFreeObject(buildableFreeObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask(), 
                    buildableFreeObjectSO, secondRayDirection, out _);
                
            Vector3 gridLevelWorldPosition = worldPosition;
            gridLevelWorldPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? GetGridLevelWorldPositionXZ(gridLevelWorldPosition, worldPosition) : GetGridLevelWorldPositionXY(gridLevelWorldPosition, worldPosition);

            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(gridLevelWorldPosition);
            Grid activeGrid = activeEasyGridBuilderPro.GetActiveGrid();

            if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(cellPosition)) return false;
            if (activeBuildableObjectSO.affectByBasicAreaEnablers && activeEasyGridBuilderPro.IsBuildableObjectEnabledByBasicGridAreaEnablers()) return true;
            if (activeBuildableObjectSO.affectByAreaEnablers && activeEasyGridBuilderPro.IsFreeObjectEnabledByGridAreaEnablers(activeGrid, buildableFreeObjectSO, cellPosition)) return true;
            if (activeBuildableObjectSO.affectByBasicAreaDisablers && activeEasyGridBuilderPro.IsBuildableObjectDisabledByBasicGridAreaDisablers()) return false;
            if (activeBuildableObjectSO.affectByAreaDisablers && activeEasyGridBuilderPro.IsFreeObjectDisabledByGridAreaDisablers(activeGrid, buildableFreeObjectSO, cellPosition)) return false;
                
            if (buildableFreeObjectSO.placementType != FreeObjectPlacementType.SplinePlacement && gridManager.GetActiveCameraMode() != CameraMode.ThirdPerson) 
            {
                return activeEasyGridBuilderPro.CheckPlacementDistanceForGhostObject(worldPosition);
            }
            return true;
        }

        private Vector3 GetGridLevelWorldPositionXZ(Vector3 gridLevelWorldPosition, Vector3 worldPosition)
        {
            Vector3 rayOrigin = worldPosition + Vector3.up * 1000; // Start raycast from above
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 9999, gridManager.GetGridSystemLayerMask()))
            {
                if (hit.collider.TryGetComponent<EasyGridBuilderProXZ>(out _)) 
                {
                    gridLevelWorldPosition = hit.point;
                }
            }
            return gridLevelWorldPosition;
        }

        private Vector3 GetGridLevelWorldPositionXY(Vector3 gridLevelWorldPosition, Vector3 worldPosition)
        {
            Vector3 rayOrigin = worldPosition + Vector3.back * 1000; // Start raycast from above
            if (Physics.Raycast(rayOrigin, Vector3.forward, out RaycastHit hit, 9999, gridManager.GetGridSystemLayerMask()))
            {
                if (hit.collider.TryGetComponent<EasyGridBuilderProXZ>(out _)) 
                {
                    gridLevelWorldPosition = hit.point;
                }
            }
            return gridLevelWorldPosition;
        }

        private void ApplyMaterialToGameObjectAndChildren(GameObject ghostObjectVisual, Material material)
        {
            MeshRenderer renderer = ghostObjectVisual.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            foreach (Transform child in ghostObjectVisual.transform)
            {
                if (child.gameObject.name != GRID_AREA_VISUAL_GENERATOR_QUAD_NAME) ApplyMaterialToGameObjectAndChildren(child.gameObject, material);
            }
        }
        #endregion Handle Visual Material Functions End:

        #region Handle Visual Layer Functions Start:
        private int GetHighestLayerSet()
        {
            int highestLayer = 0;
            int layerMask = ghostObjectLayer.value;

            while (layerMask > 0)
            {
                layerMask >>= 1;
                highestLayer++;
            }

            return highestLayer > 1 ? highestLayer - 1 : 0;
        }

        private void SetLayerRecursive(GameObject targetGameObject, int layer)
        {
            targetGameObject.layer = layer;
            foreach (Transform child in targetGameObject.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }
        #endregion Handle Visual Layer Functions End:

        ///-------------------------------------------------------------------------------///
        /// GHOST VISUAL PULBIC GETTER FUNCTIONS                                          ///
        ///-------------------------------------------------------------------------------///
        
        public bool TryGetGhostObjectVisual(out Transform ghostObjectVisual)
        {
            ghostObjectVisual = this.ghostTransformVisual;
            return this.ghostTransformVisual != null;
        }

        public bool TryGetBoxPlacementHolderObject(out Transform splinePlacementHolderObject)
        {
            splinePlacementHolderObject = this.splinePlacementHolderObject;
            return this.splinePlacementHolderObject != null;
        }

        public Vector3 GetObjectScaleForObjectGridAlphaMask()
        {
            return ghostTransformVisual.GetComponent<BuildableObject>().GetObjectScale();
        }

        public Vector3 GetObjectPositionForObjectGridAlphaMask()
        {
            return ghostTransformVisual.position;
        }
    }
}