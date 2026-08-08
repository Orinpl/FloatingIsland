using System.Collections;
using System.Collections.Generic;
using SoulGames.Utilities;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Edge Object Ghost", 4)]
    public class BuildableEdgeObjectGhost : MonoBehaviour
    {
        [SerializeField] private LayerMask ghostObjectLayer;
        [SerializeField] private float ghostObjectMoveSpeed = 25f;
        [SerializeField] private float ghostObjectRotationSpeed = 25f;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private BuildableObjectSO activeBuildableObjectSO;
        private BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab;
        private EdgeObjectPlacementType activeBuildableEdgeObjectSOPlacementType;
        private Transform ghostTransformVisual;
        private Transform parentTransform;

        private Vector3 boxPlacementStartPosition;
        private Vector3 boxPlacementEndPosition;
        
        private const float ADDITIVE_SCALE = 0.01f;
        private const float BOX_PLACEMENT_OBJECT_GRID_ALPHA_MASK_SCALE_MULTIPLIER = 1.5f;
        private const string GRID_AREA_VISUAL_GENERATOR_QUAD_NAME = "GridAreaVisualGeneratorQuad";

        ///-------------------------------------------------------------------------------///
        /// GHOST OBJECT INITIALIZE FUNCTIONS                                             ///
        ///-------------------------------------------------------------------------------///
        private void Start()
        {
            StartCoroutine(LateStart());
        }
        
        private void OnDestroy()
        {
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
            gridManager.OnIsMousePointerOnGridChanged -= OnIsMousePointerOnGridChanged;

            gridManager.OnActiveBuildableSOChanged -= OnActiveBuildableSOChanged;
            gridManager.OnActiveGridModeChanged -= OnActiveGridModeChanged;
            gridManager.OnBuildableObjectPlaced -= OnBuildableObjectPlaced;
            gridManager.OnEdgeObjectBoxPlacementStarted -= OnBoxPlacementStarted;
            gridManager.OnEdgeObjectBoxPlacementUpdated -= OnBoxPlacementUpdated;
            gridManager.OnEdgeObjectBoxPlacementFinalized -= OnBoxPlacementFinalized;
            gridManager.OnEdgeObjectBoxPlacementCancelled -= OnBoxPlacementCancelled;
        }

        #region Ghost Object Initialization Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();
            
            gridManager = GridManager.Instance;
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnIsMousePointerOnGridChanged += OnIsMousePointerOnGridChanged;
            
            gridManager.OnActiveBuildableSOChanged += OnActiveBuildableSOChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;
            gridManager.OnBuildableObjectPlaced += OnBuildableObjectPlaced;
            gridManager.OnEdgeObjectBoxPlacementStarted += OnBoxPlacementStarted;
            gridManager.OnEdgeObjectBoxPlacementUpdated += OnBoxPlacementUpdated;
            gridManager.OnEdgeObjectBoxPlacementFinalized += OnBoxPlacementFinalized;
            gridManager.OnEdgeObjectBoxPlacementCancelled += OnBoxPlacementCancelled;

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

        private void UpdateActiveBuildableObjects()
        {
            activeBuildableObjectSO = activeEasyGridBuilderPro.GetActiveBuildableObjectSO();
            activeBuildableObjectSORandomPrefab = activeEasyGridBuilderPro.GetActiveBuildableObjectSORandomPrefab();
            RefreshGhostObjectVisual();
        }

        private void OnBoxPlacementStarted(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, EdgeObjectPlacementType placementType)
        {
            this.boxPlacementStartPosition = boxPlacementStartPosition;
            activeBuildableEdgeObjectSOPlacementType = placementType;
            RefreshGhostObjectVisual();
        }

        private void OnBoxPlacementUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            this.boxPlacementEndPosition = boxPlacementEndPosition;
        }
        
        private void OnBoxPlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            activeBuildableEdgeObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableEdgeObjectSOPlacementType();
            boxPlacementStartPosition = default;
            boxPlacementEndPosition = default;
            RefreshGhostObjectVisual();
        }

        private void OnBoxPlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            activeBuildableEdgeObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableEdgeObjectSOPlacementType();
            boxPlacementStartPosition = default;
            boxPlacementEndPosition = default;
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
            UpdateVisualMaterials();
        }

        private void LateUpdate()
        {
            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO && activeEasyGridBuilderPro.GetActiveGridMode() is GridMode.BuildMode)
            {
                if (MouseInteractionUtilities.IsMousePointerOverUI()) return;
                if (!IsBoxPlacementStartAndEndCellsAreTheSame())
                {
                    if (buildableEdgeObjectSO.placementType == EdgeObjectPlacementType.WireBoxPlacement || buildableEdgeObjectSO.placementType == EdgeObjectPlacementType.FourDirectionWirePlacement ||
                        buildableEdgeObjectSO.placementType == EdgeObjectPlacementType.LShapedPlacement) return;
                }
                
                UpdateGhostObjectSinglePlacementPosition(buildableEdgeObjectSO);
                UpdateGhostObjectSinglePlacementRotation(buildableEdgeObjectSO);
            }
        }

        #region Ghost Object Update Functions Start:
        private bool IsBoxPlacementStartAndEndCellsAreTheSame()
        {
            if (activeEasyGridBuilderPro.GetActiveGridCellPosition(boxPlacementStartPosition) == activeEasyGridBuilderPro.GetActiveGridCellPosition(boxPlacementEndPosition)) return true;
            else return false;
        }

        private void UpdateGhostObjectSinglePlacementPosition(BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();

            Vector3 targetPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? CalculateSnappingPositionXZ(buildableEdgeObjectSO) : CalculateSnappingPositionXY(buildableEdgeObjectSO);

            if (buildableEdgeObjectSO.raiseObjectWithVerticalGrids) 
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) targetPosition += new Vector3(0, activeVerticalGridIndex * verticalGridHeight, 0);
                else targetPosition += new Vector3(0, 0, -activeVerticalGridIndex * verticalGridHeight);
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * ghostObjectMoveSpeed);
        }

        private Vector3 CalculateSnappingPositionXZ(BuildableEdgeObjectSO buildableEdgeObjectSO)
        {       
            float customHeight;
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableEdgeObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), Vector3.down * 9999, 
                out bool directHitGridCollisionLayer, out Vector3 firstCollisionWorldPosition);
            if (!directHitGridCollisionLayer) customHeight = firstCollisionWorldPosition.y;
            else customHeight = mouseWorldPosition.y;

            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 cellWorldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
            Vector3 closestCorner = GetClosestCornerXZ(mouseWorldPosition, cellWorldPosition, out CornerObjectCellDirection _);
            Vector3 rayOrigin = closestCorner + new Vector3(0, customHeight, 0) + Vector3.up * 1000; // Start raycast from above

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, buildableEdgeObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
            {
                customHeight = GetClosestVerticalGridSnappingHeight(buildableEdgeObjectSO ,activeEasyGridBuilderPro.transform.position.y, hit.point.y, activeEasyGridBuilderPro.GetVerticalGridHeight());
            }

            Vector3 targetPosition = CalculateFlipPositionXZ(new Vector3(closestCorner.x, customHeight, closestCorner.z), buildableEdgeObjectSO);
            return targetPosition;
        }

        private Vector3 CalculateSnappingPositionXY(BuildableEdgeObjectSO buildableEdgeObjectSO)
        {       
            float customHeight;
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableEdgeObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), Vector3.forward * 9999, 
                out bool directHitGridCollisionLayer, out Vector3 firstCollisionWorldPosition);
            if (!directHitGridCollisionLayer) customHeight = firstCollisionWorldPosition.z;
            else customHeight = mouseWorldPosition.z;

            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 cellWorldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
            Vector3 closestCorner = GetClosestCornerXY(mouseWorldPosition, cellWorldPosition, out CornerObjectCellDirection _);
            Vector3 rayOrigin = closestCorner + new Vector3(0, 0, customHeight) + Vector3.back * 1000; // Start raycast from above

            if (Physics.Raycast(rayOrigin, Vector3.forward, out RaycastHit hit, Mathf.Infinity, buildableEdgeObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
            {
                customHeight = GetClosestVerticalGridSnappingHeight(buildableEdgeObjectSO, activeEasyGridBuilderPro.transform.position.z, hit.point.z, activeEasyGridBuilderPro.GetVerticalGridHeight());
            }

            Vector3 targetPosition = CalculateFlipPositionXY(new Vector3(closestCorner.x, closestCorner.y, customHeight), buildableEdgeObjectSO);
            return targetPosition;
        }

        public Vector3 GetClosestCornerXZ(Vector3 mouseWorldPosition, Vector3 cellWorldPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            float cellSize = activeEasyGridBuilderPro.GetCellSize();

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

            return GetNearestCorner(mouseWorldPosition, corners, out cornerObjectOriginCellDirection);
        }

        public Vector3 GetClosestCornerXY(Vector3 mouseWorldPosition, Vector3 cellWorldPosition, out CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            float cellSize = activeEasyGridBuilderPro.GetCellSize();

            Vector3 lowerLeftCorner = cellWorldPosition;
            Vector3 lowerRightCorner = cellWorldPosition + new Vector3(cellSize, 0, 0);
            Vector3 upperLeftCorner = cellWorldPosition + new Vector3(0, cellSize, 0);
            Vector3 upperRightCorner = cellWorldPosition + new Vector3(cellSize, cellSize, 0);

            // Array of corners and corresponding enum values
            (Vector3 position, CornerObjectCellDirection type)[] corners = 
            {
                (lowerLeftCorner, CornerObjectCellDirection.SouthWest),
                (lowerRightCorner, CornerObjectCellDirection.SouthEast),
                (upperLeftCorner, CornerObjectCellDirection.NorthWest),
                (upperRightCorner, CornerObjectCellDirection.NorthEast)
            };

            return GetNearestCorner(mouseWorldPosition, corners, out cornerObjectOriginCellDirection);
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

        private float GetClosestVerticalGridSnappingHeight(BuildableEdgeObjectSO buildableEdgeObjectSO, float gridHeight, float height, float verticalGridHeight)
        {
            if (buildableEdgeObjectSO.customSurfaceLayerMask == 0) return height;
            if (buildableEdgeObjectSO.snapToTheClosestVerticalGridHeight)
            {
                // Find the difference between the height and the base gridHeight
                float relativeHeight = height - gridHeight;

                // Calculate the closest lower and upper multiples of verticalGridHeight
                float lowerMultiple = Mathf.Floor(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;
                float upperMultiple = Mathf.Ceil(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;

                // Calculate the threshold value (percentage of verticalGridHeight)
                float thresholdValue = (buildableEdgeObjectSO.minimumHeightThresholdPercentage / 100f) * verticalGridHeight;

                if (height - lowerMultiple >= thresholdValue) return upperMultiple;
                else return lowerMultiple;
            }
            return height;
        }

        private Vector3 CalculateFlipPositionXZ(Vector3 currentPosition, BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            if (!activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectFlipped()) return currentPosition;

            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            int objectLengthRelativeToCellSize = buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(cellSize, activeBuildableObjectSORandomPrefab);
            Vector3 targetPosition = default;

            switch (activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectRotation())
            {
                case FourDirectionalRotation.North: targetPosition = new Vector3(0, 0, cellSize * objectLengthRelativeToCellSize); break;
                case FourDirectionalRotation.East: targetPosition = new Vector3(cellSize * objectLengthRelativeToCellSize, 0, 0); break;
                case FourDirectionalRotation.South: targetPosition = new Vector3(0, 0, cellSize * objectLengthRelativeToCellSize * -1); break;
                case FourDirectionalRotation.West: targetPosition = new Vector3(cellSize * objectLengthRelativeToCellSize * -1, 0, 0); break;
            }

            return targetPosition + currentPosition;
        }

        private Vector3 CalculateFlipPositionXY(Vector3 currentPosition, BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            if (!activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectFlipped()) return currentPosition;

            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            int objectLengthRelativeToCellSize = buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(cellSize, activeBuildableObjectSORandomPrefab);
            Vector3 targetPosition = default;

            switch (activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectRotation())
            {
                case FourDirectionalRotation.North: targetPosition = new Vector3(0, cellSize * objectLengthRelativeToCellSize, 0); break;
                case FourDirectionalRotation.East: targetPosition = new Vector3(cellSize * objectLengthRelativeToCellSize, 0, 0); break;
                case FourDirectionalRotation.South: targetPosition = new Vector3(0, cellSize * objectLengthRelativeToCellSize * -1, 0); break;
                case FourDirectionalRotation.West: targetPosition = new Vector3(cellSize * objectLengthRelativeToCellSize * -1, 0, 0); break;
            }

            return targetPosition + currentPosition;
        }

        private void UpdateGhostObjectSinglePlacementRotation(BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            float targetAngle;
            if (activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectFlipped()) targetAngle = buildableEdgeObjectSO.GetRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectRotation()) + 180;
            else targetAngle = buildableEdgeObjectSO.GetRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectRotation());
            
            Quaternion targetRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, targetAngle, 0) : Quaternion.Euler(0, 0, -targetAngle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * ghostObjectRotationSpeed);
        }
        #endregion Ghost Object Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// GHOST VISUAL PLACEMENT HANDLING FUNCTIONS                                     ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle Single Placement Functions Start:
        private void RefreshGhostObjectVisual()
        {
            ClearSpawnedObjects();
            if (IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes() && !IsBoxPlacementStartAndEndCellsAreTheSame()) return;
            if (activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.BuildMode) return;

            if (activeBuildableObjectSO is BuildableEdgeObjectSO buildableEdgeObjectSO)
            {
                if (activeBuildableObjectSORandomPrefab.ghostObjectPrefab == null) activeBuildableObjectSORandomPrefab.ghostObjectPrefab = activeBuildableObjectSORandomPrefab.objectPrefab;

                ghostTransformVisual = Instantiate(activeBuildableObjectSORandomPrefab.ghostObjectPrefab, Vector3.zero, Quaternion.identity);
                BuildableEdgeObject buildableEdgeObject = ghostTransformVisual.GetComponent<BuildableEdgeObject>();
                buildableEdgeObject.SetBasicGridAreaTrigger();
                buildableEdgeObject.SetIsInstantiatedByGhostObject(true);
                parentTransform = new GameObject(buildableEdgeObjectSO.objectName).transform;

                SetUpTransformHierarchy(ghostTransformVisual, parentTransform, transform);
                parentTransform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);

                parentTransform.localPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(0, ghostTransformVisual.localPosition.y, 0) : new Vector3(0, 0, ghostTransformVisual.localPosition.z);
                
                UpdateVisualMaterials();
                SetLayerRecursive(parentTransform.gameObject, GetHighestLayerSet());
            }
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

        private bool IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.FourDirectionWirePlacement || activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.WireBoxPlacement ||
                activeBuildableEdgeObjectSOPlacementType == EdgeObjectPlacementType.LShapedPlacement) return true;
            return false;
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

        ///-------------------------------------------------------------------------------///
        /// GHOST VISUAL SUPPORTER FUNCTIONS                                              ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle Visual Material Functions Start:
        private void UpdateVisualMaterials()
        {
            UpdateSingleVisualMaterial();
        }

        private void UpdateSingleVisualMaterial()
        {
            if (ghostTransformVisual == null) return;

            Material selectedMaterial = DetermineMaterialBasedOnPlacement();
            if (selectedMaterial == null) return;
            ApplyMaterialToGameObjectAndChildren(ghostTransformVisual.gameObject, selectedMaterial);
        }

        private Material DetermineMaterialBasedOnPlacement()
        {
            bool isPlaceable = IsVisualObjectPlaceable();
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

        private bool IsVisualObjectPlaceable()
        {
            BuildableEdgeObjectSO buildableEdgeObjectSO = (BuildableEdgeObjectSO)activeBuildableObjectSO;

            Vector3 secondRayDirection = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;
            Vector3 worldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableEdgeObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), secondRayDirection, out _, out _);

            Vector2Int originCellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(worldPosition);

            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ easyGridBuilderProXZ)
            {   
                return CheckPlaceableXZ(easyGridBuilderProXZ, originCellPosition, worldPosition, buildableEdgeObjectSO);
            }
            else if (activeEasyGridBuilderPro is EasyGridBuilderProXY easyGridBuilderProXY)
            {
                return CheckPlaceableXY(easyGridBuilderProXY, originCellPosition, worldPosition, buildableEdgeObjectSO);
            }
            return true;
        }

        private bool CheckPlaceableXZ(EasyGridBuilderProXZ easyGridBuilderProXZ, Vector2Int originCellPosition, Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            easyGridBuilderProXZ.GetClosestCorner(worldPosition, easyGridBuilderProXZ.GetActiveGridCellWorldPosition(new CellPositionXZ(originCellPosition.x, originCellPosition.y)),
                out CornerObjectCellDirection cornerObjectOriginCellDirection);

            Dictionary<CellPositionXZ, EdgeObjectCellDirection> cellPositionsDictionary = easyGridBuilderProXZ.GetEdgeObjectCellPositionDictionary(new CellPositionXZ(originCellPosition.x, originCellPosition.y), 
                cornerObjectOriginCellDirection, activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectRotation(), buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(activeEasyGridBuilderPro.GetCellSize(), 
                activeBuildableObjectSORandomPrefab));

            Grid activeGrid = easyGridBuilderProXZ.GetActiveGrid();
                
            bool isWithinGridBounds = false;
            foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)))
                {
                    isWithinGridBounds = true;
                    break;
                }
            }
            if (!isWithinGridBounds) return false;

            foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                switch (cellPosition.Value)
                {
                    case EdgeObjectCellDirection.North: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectNorthData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectNorthData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case EdgeObjectCellDirection.East: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectEastData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectEastData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case EdgeObjectCellDirection.South: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectSouthData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectSouthData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case EdgeObjectCellDirection.West: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectWestData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableEdgeObjectWestData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                }
            }

            foreach (KeyValuePair<CellPositionXZ, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeBuildableObjectSO.affectByBasicAreaEnablers && activeEasyGridBuilderPro.IsBuildableObjectEnabledByBasicGridAreaEnablers()) continue;
                if (activeBuildableObjectSO.affectByAreaEnablers && activeEasyGridBuilderPro.IsEdgeObjectEnabledByGridAreaEnablers(activeGrid, buildableEdgeObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value)) continue;
                if (activeBuildableObjectSO.affectByBasicAreaDisablers && activeEasyGridBuilderPro.IsBuildableObjectDisabledByBasicGridAreaDisablers()) return false;
                if (activeBuildableObjectSO.affectByAreaDisablers && activeEasyGridBuilderPro.IsEdgeObjectDisabledByGridAreaDisablers(activeGrid, buildableEdgeObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value)) return false;
            }
            if (gridManager.GetActiveCameraMode() != CameraMode.ThirdPerson) return activeEasyGridBuilderPro.CheckPlacementDistanceForGhostObject(worldPosition);
            return true;
        }

        private bool CheckPlaceableXY(EasyGridBuilderProXY easyGridBuilderProXY, Vector2Int originCellPosition, Vector3 worldPosition, BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            easyGridBuilderProXY.GetClosestCorner(worldPosition, easyGridBuilderProXY.GetActiveGridCellWorldPosition(new CellPositionXY(originCellPosition.x, originCellPosition.y)),
                out CornerObjectCellDirection cornerObjectOriginCellDirection);

            Dictionary<CellPositionXY, EdgeObjectCellDirection> cellPositionsDictionary = easyGridBuilderProXY.GetEdgeObjectCellPositionDictionary(new CellPositionXY(originCellPosition.x, originCellPosition.y), 
                cornerObjectOriginCellDirection, activeEasyGridBuilderPro.GetActiveBuildableEdgeObjectRotation(), buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(activeEasyGridBuilderPro.GetCellSize(), 
                activeBuildableObjectSORandomPrefab));

            Grid activeGrid = easyGridBuilderProXY.GetActiveGrid();
                
            bool isWithinGridBounds = false;
            foreach (KeyValuePair<CellPositionXY, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)))
                {
                    isWithinGridBounds = true;
                    break;
                }
            }
            if (!isWithinGridBounds) return false;

            foreach (KeyValuePair<CellPositionXY, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                switch (cellPosition.Value)
                {
                    case EdgeObjectCellDirection.North: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectNorthData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectNorthData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case EdgeObjectCellDirection.East: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectEastData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectEastData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case EdgeObjectCellDirection.South: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectSouthData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectSouthData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case EdgeObjectCellDirection.West: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectWestData().ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableEdgeObjectWestData()[buildableEdgeObjectSO.buildableEdgeObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                }
            }

            foreach (KeyValuePair<CellPositionXY, EdgeObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeBuildableObjectSO.affectByBasicAreaEnablers && activeEasyGridBuilderPro.IsBuildableObjectEnabledByBasicGridAreaEnablers()) continue;
                if (activeBuildableObjectSO.affectByAreaEnablers && activeEasyGridBuilderPro.IsEdgeObjectEnabledByGridAreaEnablers(activeGrid, buildableEdgeObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.y), cellPosition.Value)) continue;
                if (activeBuildableObjectSO.affectByBasicAreaDisablers && activeEasyGridBuilderPro.IsBuildableObjectDisabledByBasicGridAreaDisablers()) return false;
                if (activeBuildableObjectSO.affectByAreaDisablers && activeEasyGridBuilderPro.IsEdgeObjectDisabledByGridAreaDisablers(activeGrid, buildableEdgeObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.y), cellPosition.Value)) return false;
            }
            if (gridManager.GetActiveCameraMode() != CameraMode.ThirdPerson) return activeEasyGridBuilderPro.CheckPlacementDistanceForGhostObject(worldPosition);
            return true;
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

        #region Handle Public Getter Functions Start:
        public bool TryGetGhostObjectVisual(out Transform ghostObjectVisual)
        {
            ghostObjectVisual = this.ghostTransformVisual;
            return this.ghostTransformVisual != null;
        }

        public Vector3 GetObjectScaleForObjectGridAlphaMask()
        {
            if (IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
            {
                float width = Mathf.Abs(boxPlacementEndPosition.x - boxPlacementStartPosition.x) * BOX_PLACEMENT_OBJECT_GRID_ALPHA_MASK_SCALE_MULTIPLIER;
                float height = Mathf.Abs(boxPlacementEndPosition.y - boxPlacementStartPosition.y) * BOX_PLACEMENT_OBJECT_GRID_ALPHA_MASK_SCALE_MULTIPLIER;
                float length = Mathf.Abs(boxPlacementEndPosition.z - boxPlacementStartPosition.z) * BOX_PLACEMENT_OBJECT_GRID_ALPHA_MASK_SCALE_MULTIPLIER;

                float cellSize = activeEasyGridBuilderPro.GetCellSize();
                width = Mathf.Round(width / cellSize) * cellSize;
                height = Mathf.Round(height / cellSize) * cellSize;
                length = Mathf.Round(length / cellSize) * cellSize;

                return new Vector3(width + cellSize, height + cellSize, length + cellSize);
            }
            return ghostTransformVisual.GetComponent<BuildableObject>().GetObjectScale();
        }

        public Vector3 GetObjectPositionForObjectGridAlphaMask()
        {
            if (IsActiveBuildableEdgeObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
            {
                float centerX = (boxPlacementStartPosition.x + boxPlacementEndPosition.x) / 2;
                float centerY = (boxPlacementStartPosition.y + boxPlacementEndPosition.y) / 2;
                float centerZ = (boxPlacementStartPosition.z + boxPlacementEndPosition.z) / 2;

                float cellSize = activeEasyGridBuilderPro.GetCellSize();
                centerX = Mathf.Round(centerX / cellSize) * cellSize;
                centerY = Mathf.Round(centerY / cellSize) * cellSize;
                centerZ = Mathf.Round(centerZ / cellSize) * cellSize;

                return new Vector3(centerX, centerY, centerZ);
            }
            return ghostTransformVisual.position;
        }
        #endregion Handle Public Getter Functions End:
    }   
}