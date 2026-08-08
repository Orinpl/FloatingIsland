using UnityEngine;
using SoulGames.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Corner Object Ghost", 5)]
    public class BuildableCornerObjectGhost : MonoBehaviour
    {
        [SerializeField] private LayerMask ghostObjectLayer;
        [SerializeField] private float ghostObjectMoveSpeed = 25f;
        [SerializeField] private float ghostObjectRotationSpeed = 25f;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private BuildableObjectSO activeBuildableObjectSO;
        private BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab;
        private CornerObjectPlacementType activeBuildableCornerObjectSOPlacementType;
        private Transform ghostTransformVisual;
        private Transform parentTransform;

        private Vector3 boxPlacementStartPosition;
        private Vector3 boxPlacementEndPosition;
        private HashSet<Vector2Int> occupiedCellsSet;
        private Dictionary<Vector2Int, Transform> ghostTransformVisualDictionary;
        private Dictionary<Vector2Int, Transform> parentObjectDictionary;
        private Transform boxPlacementHolderObject;
        private HashSet<Vector2Int> boxPlacementCellPositionSet;
        
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
            gridManager.OnCornerObjectBoxPlacementStarted -= OnBoxPlacementStarted;
            gridManager.OnCornerObjectBoxPlacementUpdated -= OnBoxPlacementUpdated;
            gridManager.OnCornerObjectBoxPlacementFinalized -= OnBoxPlacementFinalized;
            gridManager.OnCornerObjectBoxPlacementCancelled -= OnBoxPlacementCancelled;
        }

        #region Ghost Object Initialization Functions Start:
        private void InitializeDataStructures()
        {
            occupiedCellsSet = new HashSet<Vector2Int>();
            ghostTransformVisualDictionary = new Dictionary<Vector2Int, Transform>();
            parentObjectDictionary = new Dictionary<Vector2Int, Transform>();
            boxPlacementCellPositionSet = new HashSet<Vector2Int>();
        }

        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            gridManager = GridManager.Instance;
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnIsMousePointerOnGridChanged += OnIsMousePointerOnGridChanged;

            gridManager.OnActiveBuildableSOChanged += OnActiveBuildableSOChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;
            gridManager.OnBuildableObjectPlaced += OnBuildableObjectPlaced;
            gridManager.OnCornerObjectBoxPlacementStarted += OnBoxPlacementStarted;
            gridManager.OnCornerObjectBoxPlacementUpdated += OnBoxPlacementUpdated;
            gridManager.OnCornerObjectBoxPlacementFinalized += OnBoxPlacementFinalized;
            gridManager.OnCornerObjectBoxPlacementCancelled += OnBoxPlacementCancelled;

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
            if (activeEasyGridBuilderPro.GetActiveBuildableObjectSO() is BuildableEdgeObjectSO buildableEdgeObjectSO && buildableEdgeObjectSO.mergeWithBuildableCornerObject && buildableEdgeObjectSO.buildableCornerObjectSO != null)
            {
                activeBuildableObjectSO = buildableEdgeObjectSO.buildableCornerObjectSO;
                activeBuildableObjectSORandomPrefab = activeEasyGridBuilderPro.GetActiveSecondaryBuildableObjectSORandomPrefab();
            }
            else
            {
                activeBuildableObjectSO = activeEasyGridBuilderPro.GetActiveBuildableObjectSO();
                activeBuildableObjectSORandomPrefab = activeEasyGridBuilderPro.GetActiveBuildableObjectSORandomPrefab();
            }

            RefreshGhostObjectVisual();
        }

        private void OnBoxPlacementStarted(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, CornerObjectPlacementType placementType)
        {
            this.boxPlacementStartPosition = boxPlacementStartPosition;
            activeBuildableCornerObjectSOPlacementType = placementType;
            StartBoxPlacement();
        }

        private void OnBoxPlacementUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            this.boxPlacementEndPosition = boxPlacementEndPosition;
            UpdateBoxPlacement();
        }
        
        private void OnBoxPlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            activeBuildableCornerObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableCornerObjectSOPlacementType();
            FinalizeBoxPlacement();
        }

        private void OnBoxPlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            activeBuildableCornerObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableCornerObjectSOPlacementType();
            FinalizeBoxPlacement();
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
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO && activeEasyGridBuilderPro.GetActiveGridMode() is GridMode.BuildMode)
            {
                if (MouseInteractionUtilities.IsMousePointerOverUI()) return;
                UpdateGhostObjectPosition(buildableCornerObjectSO);
                UpdateGhostObjectRotation(buildableCornerObjectSO);
            }
        }

        #region Ghost Object Update Functions Start:
        private void UpdateGhostObjectPosition(BuildableCornerObjectSO buildableCornerObjectSO)
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();

            Vector3 targetPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? CalculateSnappingPositionXZ(buildableCornerObjectSO) : CalculateSnappingPositionXY(buildableCornerObjectSO);

            if (buildableCornerObjectSO.raiseObjectWithVerticalGrids)
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) targetPosition += new Vector3(0, activeVerticalGridIndex * verticalGridHeight, 0);
                else targetPosition += new Vector3(0, 0, -activeVerticalGridIndex * verticalGridHeight);
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * ghostObjectMoveSpeed);
        }

        private Vector3 CalculateSnappingPositionXZ(BuildableCornerObjectSO buildableCornerObjectSO)
        {       
            float customYPosition;
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableCornerObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(),  Vector3.down * 9999, 
                out bool directHitGridCollisionLayer, out Vector3 firstCollisionWorldPosition);
            if (!directHitGridCollisionLayer) customYPosition = firstCollisionWorldPosition.y - mouseWorldPosition.y;
            else customYPosition = mouseWorldPosition.y;
            
            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 cellWorldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
            Vector3 closestCorner = GetClosestCornerXZ(mouseWorldPosition, cellWorldPosition, out CornerObjectCellDirection _);
            Vector3 rayOrigin = closestCorner + new Vector3(0, customYPosition, 0) + Vector3.up * 1000; // Start raycast from above

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, buildableCornerObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
            {
                customYPosition = GetClosestVerticalGridSnappingHeight(buildableCornerObjectSO, activeEasyGridBuilderPro.transform.position.y, hit.point.y, activeEasyGridBuilderPro.GetVerticalGridHeight());
            }
            return new Vector3(closestCorner.x, customYPosition, closestCorner.z);
        }

        private Vector3 CalculateSnappingPositionXY(BuildableCornerObjectSO buildableCornerObjectSO)
        {       
            float customZPosition;
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableCornerObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(),  Vector3.forward * 9999, 
                out bool directHitGridCollisionLayer, out Vector3 firstCollisionWorldPosition);
            if (!directHitGridCollisionLayer) customZPosition = firstCollisionWorldPosition.z - mouseWorldPosition.z;
            else customZPosition = mouseWorldPosition.z;
            
            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 cellWorldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
            Vector3 closestCorner = GetClosestCornerXY(mouseWorldPosition, cellWorldPosition, out CornerObjectCellDirection _);
            Vector3 rayOrigin = closestCorner + new Vector3(0, 0, customZPosition) + Vector3.back * 1000; // Start raycast from above

            if (Physics.Raycast(rayOrigin, Vector3.forward, out RaycastHit hit, Mathf.Infinity, buildableCornerObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
            {
                customZPosition = GetClosestVerticalGridSnappingHeight(buildableCornerObjectSO, activeEasyGridBuilderPro.transform.position.z, hit.point.z, activeEasyGridBuilderPro.GetVerticalGridHeight());
            }
            return new Vector3(closestCorner.x, closestCorner.y, customZPosition);
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

        private float GetClosestVerticalGridSnappingHeight(BuildableCornerObjectSO buildableCornerObjectSO, float gridHeight, float height, float verticalGridHeight)
        {
            if (buildableCornerObjectSO.customSurfaceLayerMask == 0) return height;
            if (buildableCornerObjectSO.snapToTheClosestVerticalGridHeight)
            {
                // Find the difference between the height and the base gridHeight
                float relativeHeight = height - gridHeight;

                // Calculate the closest lower and upper multiples of verticalGridHeight
                float lowerMultiple = Mathf.Floor(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;
                float upperMultiple = Mathf.Ceil(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;

                // Calculate the threshold value (percentage of verticalGridHeight)
                float thresholdValue = (buildableCornerObjectSO.minimumHeightThresholdPercentage / 100f) * verticalGridHeight;

                if (height - lowerMultiple >= thresholdValue) return upperMultiple;
                else return lowerMultiple;
            }
            return height;
        }

        private void UpdateGhostObjectRotation(BuildableCornerObjectSO buildableCornerObjectSO)
        {
            float targetAngle = 0f;
            switch (buildableCornerObjectSO.rotationType)
            {
                case CornerObjectRotationType.FourDirectionalRotation: targetAngle = buildableCornerObjectSO.GetFourDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableCornerObjectFourDirectionalRotation()); break;
                case CornerObjectRotationType.EightDirectionalRotation: targetAngle = buildableCornerObjectSO.GetEightDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableCornerObjectEightDirectionalRotation()); break;
                case CornerObjectRotationType.FreeRotation: targetAngle = activeEasyGridBuilderPro.GetActiveBuildableCornerObjectFreeRotation(); break;
            }

            Quaternion targetRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, targetAngle, 0) : Quaternion.Euler(0, 0, -targetAngle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * ghostObjectRotationSpeed);
        }
        #endregion Ghost Object Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// GHOST VISUAL PLACEMENT HANDLING FUNCTIONS                                     ///
        ///-------------------------------------------------------------------------------///

        #region Handle Box Placement Functions Start:
        private void StartBoxPlacement()
        {
            boxPlacementHolderObject = new GameObject("Box Placement Holder").transform;
            ClearSpawnedObjects();
        }

        private void UpdateBoxPlacement()
        {
            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO)
            {
                Vector2Int startCell = activeEasyGridBuilderPro.GetActiveGridCellPosition(boxPlacementStartPosition);
                Vector2Int endCell = activeEasyGridBuilderPro.GetActiveGridCellPosition(boxPlacementEndPosition);

                boxPlacementCellPositionSet.Clear();

                int minX = Mathf.Min(startCell.x, endCell.x);
                int maxX = Mathf.Max(startCell.x, endCell.x);
                int minY = Mathf.Min(startCell.y, endCell.y);
                int maxY = Mathf.Max(startCell.y, endCell.y);

                switch (activeBuildableCornerObjectSOPlacementType)
                {
                    case CornerObjectPlacementType.BoxPlacement: UpdateBoxPlacementCellPositions(minX, maxX, minY, maxY); break;   
                    case CornerObjectPlacementType.WireBoxPlacement: UpdateWireBoxPlacementCellPositions(minX, maxX, minY, maxY, buildableCornerObjectSO); break;
                    case CornerObjectPlacementType.FourDirectionWirePlacement: UpdateFourDirectionWirePlacementCellPositions(startCell, endCell, minX, maxX, minY, maxY, buildableCornerObjectSO); break;
                    case CornerObjectPlacementType.LShapedPlacement: UpdateLShapedPlacementCellPositions(startCell, endCell, buildableCornerObjectSO); break;
                }
                UpdateBoxPlacementVisuals(startCell, buildableCornerObjectSO);
            }
        }

        private void UpdateBoxPlacementCellPositions(int minX, int maxX, int minY, int maxY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (boxPlacementCellPositionSet.Count >= gridManager.GetMaxBoxPlacementObjects()) return;
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, y));
                }
            }
        }

        private void UpdateWireBoxPlacementCellPositions(int minX, int maxX, int minY, int maxY, BuildableCornerObjectSO buildableCornerObjectSO)
        {
            if (buildableCornerObjectSO.spawnOnlyAtEndPoints)
            {
                // Only add the four corner positions
                boxPlacementCellPositionSet.Add(new Vector2Int(minX, minY)); // Top-left
                boxPlacementCellPositionSet.Add(new Vector2Int(maxX, minY)); // Top-right
                boxPlacementCellPositionSet.Add(new Vector2Int(minX, maxY)); // Bottom-left
                boxPlacementCellPositionSet.Add(new Vector2Int(maxX, maxY)); // Bottom-right
            }
            else
            {
                // Original logic to add all borders
                for (int x = minX; x <= maxX; x++)                              // Add top and bottom borders
                {
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, minY));   // Top border
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, maxY));   // Bottom border
                }
                for (int y = minY + 1; y < maxY; y++)                           // Add left and right borders
                {
                    boxPlacementCellPositionSet.Add(new Vector2Int(minX, y));   // Left border
                    boxPlacementCellPositionSet.Add(new Vector2Int(maxX, y));   // Right border
                }
            }
        }

        private void UpdateFourDirectionWirePlacementCellPositions(Vector2Int startCell, Vector2Int endCell, int minX, int maxX, int minY, int maxY, BuildableCornerObjectSO buildableCornerObjectSO)
        {
            if (buildableCornerObjectSO.spawnOnlyAtEndPoints)
            {
                // Ensure the start and end points are along the straight line
                if (Mathf.Abs(endCell.x - startCell.x) > Mathf.Abs(endCell.y - startCell.y))
                {
                    // Horizontal line placement
                    boxPlacementCellPositionSet.Add(new Vector2Int(minX, startCell.y));  // Start point
                    boxPlacementCellPositionSet.Add(new Vector2Int(maxX, startCell.y));  // End point
                }
                else
                {
                    // Vertical line placement
                    boxPlacementCellPositionSet.Add(new Vector2Int(startCell.x, minY));  // Start point
                    boxPlacementCellPositionSet.Add(new Vector2Int(startCell.x, maxY));  // End point
                }
            }
            else
            {
                // Original logic to add all points along the line
                if (Mathf.Abs(endCell.x - startCell.x) > Mathf.Abs(endCell.y - startCell.y))
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        boxPlacementCellPositionSet.Add(new Vector2Int(x, startCell.y));
                    }
                }
                else
                {
                    for (int y = minY; y <= maxY; y++)
                    {
                        boxPlacementCellPositionSet.Add(new Vector2Int(startCell.x, y));
                    }
                }
            }
        }

        private void UpdateLShapedPlacementCellPositions(Vector2Int startCell, Vector2Int endCell, BuildableCornerObjectSO buildableCornerObjectSO)
        {
            bool isHorizontalFirst = Mathf.Abs(endCell.x - startCell.x) >= Mathf.Abs(endCell.y - startCell.y);
            bool isPositiveDirection = isHorizontalFirst ? (endCell.x >= startCell.x) : (endCell.y >= startCell.y);

            if (buildableCornerObjectSO.spawnOnlyAtEndPoints)
            {
                // Only add the start, bend, and end points
                boxPlacementCellPositionSet.Add(startCell);
                Vector2Int bendPoint = isHorizontalFirst ? new Vector2Int(endCell.x, startCell.y) : new Vector2Int(startCell.x, endCell.y);
                boxPlacementCellPositionSet.Add(bendPoint);
                boxPlacementCellPositionSet.Add(endCell);
            }
            else
            {
                // Original logic to add all points in the L-shape
                AddLShape(startCell, endCell, isHorizontalFirst, isPositiveDirection);
            }
        }

        private void AddLShape(Vector2Int startCell, Vector2Int endCell, bool isHorizontalFirst, bool isPositiveDirection)
        {
            int primaryStart = isHorizontalFirst ? startCell.x : startCell.y;
            int primaryEnd = isHorizontalFirst ? endCell.x : endCell.y;
            int secondaryStart = isHorizontalFirst ? startCell.y : startCell.x;
            int secondaryEnd = isHorizontalFirst ? endCell.y : endCell.x;

            int primaryStep = isPositiveDirection ? 1 : -1;
            int secondaryStep = secondaryEnd > secondaryStart ? 1 : -1;
            
            for (int primary = primaryStart; primary != primaryEnd + primaryStep; primary += primaryStep)                   // Add primary part
            {
                if (isHorizontalFirst) boxPlacementCellPositionSet.Add(new Vector2Int(primary, secondaryStart));
                else boxPlacementCellPositionSet.Add(new Vector2Int(secondaryStart, primary));
            }
            for (int secondary = secondaryStart; secondary != secondaryEnd + secondaryStep; secondary += secondaryStep)     // Add secondary part
            {
                if (isHorizontalFirst) boxPlacementCellPositionSet.Add(new Vector2Int(primaryEnd, secondary));
                else boxPlacementCellPositionSet.Add(new Vector2Int(secondary, primaryEnd));
            }
        }

        private void UpdateBoxPlacementVisuals(Vector2Int startCell, BuildableCornerObjectSO buildableCornerObjectSO)
        {
            RemoveInvalidGhostObjects();

            Vector3 startingCellWorldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(startCell);
            CornerObjectCellDirection cornerObjectOriginCellDirection;
            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) GetClosestCornerXZ(boxPlacementStartPosition, startingCellWorldPosition, out cornerObjectOriginCellDirection);
            else GetClosestCornerXY(boxPlacementStartPosition, startingCellWorldPosition, out cornerObjectOriginCellDirection);
            
            Vector3 offset = default;
            float cellSize = activeEasyGridBuilderPro.GetCellSize();

            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ)
            {
                switch (cornerObjectOriginCellDirection)
                {
                    case CornerObjectCellDirection.NorthEast: offset = new Vector3(cellSize, 0, cellSize); break;
                    case CornerObjectCellDirection.SouthEast: offset = new Vector3(cellSize, 0, 0); break;
                    case CornerObjectCellDirection.SouthWest: offset = Vector3.zero; break;
                    case CornerObjectCellDirection.NorthWest: offset = new Vector3(0, 0, cellSize); break;
                }
            }
            else
            {
                switch (cornerObjectOriginCellDirection)
                {
                    case CornerObjectCellDirection.NorthEast: offset = new Vector3(cellSize, cellSize, 0); break;
                    case CornerObjectCellDirection.SouthEast: offset = new Vector3(cellSize, 0, 0); break;
                    case CornerObjectCellDirection.SouthWest: offset = Vector3.zero; break;
                    case CornerObjectCellDirection.NorthWest: offset = new Vector3(0, cellSize, 0); break;
                }
            }

            AddValidGhostObjects(offset, buildableCornerObjectSO);
        }

        private void RemoveInvalidGhostObjects()
        {
            foreach (Vector2Int cellPosition in ghostTransformVisualDictionary.Keys.ToList())
            {
                if (!boxPlacementCellPositionSet.Contains(cellPosition))
                {
                    Destroy(parentObjectDictionary[cellPosition].gameObject);
                    ghostTransformVisualDictionary.Remove(cellPosition);
                    parentObjectDictionary.Remove(cellPosition);
                    occupiedCellsSet.Remove(cellPosition);
                }
            }
        }

        private void AddValidGhostObjects(Vector3 offset, BuildableCornerObjectSO buildableCornerObjectSO)
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();

            foreach (Vector2Int cellPosition in boxPlacementCellPositionSet)
            {
                if (!ghostTransformVisualDictionary.ContainsKey(cellPosition) && !occupiedCellsSet.Contains(cellPosition))
                {
                    Vector3 worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);

                    // Adjust height based on collision for each cell position
                    float customHeight = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? GetAdjustedHeightXZ(worldPosition, offset, buildableCornerObjectSO, activeVerticalGridIndex, verticalGridHeight) :
                        GetAdjustedHeightXY(worldPosition, offset, buildableCornerObjectSO, activeVerticalGridIndex, verticalGridHeight);

                    Transform visual = InstantiateVisualAtCell(worldPosition, offset, buildableCornerObjectSO, out Transform grandParentTransform, customHeight).transform;
                    ghostTransformVisualDictionary[cellPosition] = visual;
                    parentObjectDictionary[cellPosition] = grandParentTransform;
                    occupiedCellsSet.Add(cellPosition);
                }
            }

            UpdateGhostObjectsRotation(buildableCornerObjectSO);
        }

        private float GetAdjustedHeightXZ(Vector3 worldPosition, Vector3 offset, BuildableCornerObjectSO buildableCornerObjectSO, int activeVerticalGridIndex, float verticalGridHeight)
        {
            float customHeight = worldPosition.y;
            if (buildableCornerObjectSO.customSurfaceLayerMask != 0)
            {
                Vector3 rayOrigin = (worldPosition + offset) + Vector3.up * 1000; // Start raycast from above
                if (Physics.Raycast(rayOrigin, Vector3.down * 9999, out RaycastHit hit, Mathf.Infinity, buildableCornerObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
                {
                    if (buildableCornerObjectSO.raiseObjectWithVerticalGrids) customHeight = (activeVerticalGridIndex * verticalGridHeight) + hit.point.y;
                    else customHeight = hit.point.y;

                    customHeight = GetClosestVerticalGridSnappingHeight(buildableCornerObjectSO, activeEasyGridBuilderPro.transform.position.y, customHeight, activeEasyGridBuilderPro.GetVerticalGridHeight());
                }
            }
            return customHeight;
        }

        private float GetAdjustedHeightXY(Vector3 worldPosition, Vector3 offset, BuildableCornerObjectSO buildableCornerObjectSO, int activeVerticalGridIndex, float verticalGridHeight)
        {
            float customHeight = worldPosition.z;
            if (buildableCornerObjectSO.customSurfaceLayerMask != 0)
            {
                Vector3 rayOrigin = (worldPosition + offset) + Vector3.back * 1000; // Start raycast from above
                if (Physics.Raycast(rayOrigin, Vector3.forward * 9999, out RaycastHit hit, Mathf.Infinity, buildableCornerObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
                {
                    if (buildableCornerObjectSO.raiseObjectWithVerticalGrids) customHeight = -(activeVerticalGridIndex * verticalGridHeight) + hit.point.z;
                    else customHeight = hit.point.z;

                    customHeight = GetClosestVerticalGridSnappingHeight(buildableCornerObjectSO, activeEasyGridBuilderPro.transform.position.z, customHeight, activeEasyGridBuilderPro.GetVerticalGridHeight());
                }
            }
            return customHeight;
        }

        private void UpdateGhostObjectsRotation(BuildableCornerObjectSO buildableCornerObjectSO)
        {
            foreach (KeyValuePair<Vector2Int, Transform> parentTransform in ghostTransformVisualDictionary)
            {
                parentTransform.Value.parent.localRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, GetParentRotation(buildableCornerObjectSO), 0) : 
                    Quaternion.Euler(0, 0, -GetParentRotation(buildableCornerObjectSO));
            }
        }

        private GameObject InstantiateVisualAtCell(Vector3 worldPosition, Vector3 offset, BuildableCornerObjectSO buildableCornerObjectSO, out Transform grandParentTransform, float customHeight = 0)
        {
            Transform firstPrefab = buildableCornerObjectSO.randomPrefabs[0].ghostObjectPrefab;
            if (firstPrefab == null) firstPrefab = buildableCornerObjectSO.randomPrefabs[0].objectPrefab;

            Transform ghostObjectVisual = Instantiate(firstPrefab, Vector3.zero, Quaternion.identity);
            BuildableCornerObject buildableCornerObject = ghostObjectVisual.GetComponent<BuildableCornerObject>();
            buildableCornerObject.SetBasicGridAreaTrigger();
            buildableCornerObject.SetIsInstantiatedByGhostObject(true);

            Transform parentTransform = new GameObject(buildableCornerObjectSO.objectName).transform;
            grandParentTransform = new GameObject(buildableCornerObjectSO.objectName).transform;

            SetUpTransformHierarchy(ghostObjectVisual, parentTransform, grandParentTransform);
            parentTransform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);
            grandParentTransform.parent = boxPlacementHolderObject;

            PositionParentTransform(parentTransform, ghostObjectVisual, offset, buildableCornerObject);
            parentTransform.localRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, GetParentRotation(buildableCornerObjectSO), 0) : Quaternion.Euler(0, 0, -GetParentRotation(buildableCornerObjectSO));
            grandParentTransform.localPosition = worldPosition;
            
            // Apply custom Height
            grandParentTransform.localPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(grandParentTransform.localPosition.x, customHeight, grandParentTransform.localPosition.z) :
                new Vector3(grandParentTransform.localPosition.x, grandParentTransform.localPosition.y, customHeight);
            SetLayerRecursive(grandParentTransform.gameObject, GetHighestLayerSet());

            return ghostObjectVisual.gameObject;
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

        private void PositionParentTransform(Transform parentTransform, Transform ghostObjectVisual, Vector3 offset, BuildableCornerObject buildableCornerObject)
        {
            parentTransform.localPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(offset.x, ghostObjectVisual.localPosition.y, offset.z) : new Vector3(offset.x, offset.y, ghostObjectVisual.localPosition.z);
        }

        private float GetParentRotation(BuildableCornerObjectSO buildableCornerObjectSO)
        {
            float targetAngle = 0f;
            switch (buildableCornerObjectSO.rotationType)
            {
                case CornerObjectRotationType.FourDirectionalRotation: targetAngle = buildableCornerObjectSO.GetFourDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableCornerObjectFourDirectionalRotation()); break;
                case CornerObjectRotationType.EightDirectionalRotation: targetAngle = buildableCornerObjectSO.GetEightDirectionalRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableCornerObjectEightDirectionalRotation()); break;
                case CornerObjectRotationType.FreeRotation: targetAngle = activeEasyGridBuilderPro.GetActiveBuildableCornerObjectFreeRotation(); break;
            }
            return targetAngle;
        }
        
        private bool IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.BoxPlacement || activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.FourDirectionWirePlacement || 
                activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.WireBoxPlacement || activeBuildableCornerObjectSOPlacementType == CornerObjectPlacementType.LShapedPlacement) return true;
            return false;
        }

        private void FinalizeBoxPlacement()
        {
            if (boxPlacementHolderObject) Destroy(boxPlacementHolderObject.gameObject);
            ClearPlacementData();
            RefreshGhostObjectVisual();
        }

        private void ClearPlacementData()
        {
            ghostTransformVisualDictionary.Clear();
            parentObjectDictionary.Clear();
            boxPlacementCellPositionSet.Clear();
            occupiedCellsSet.Clear();
        }
        #endregion Handle Box Placement Functions End:

        #region Handle Single Placement Functions Start:
        private void RefreshGhostObjectVisual()
        {
            ClearSpawnedObjects();
            if (IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes() || activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.BuildMode) return;

            if (activeBuildableObjectSO is BuildableCornerObjectSO buildableCornerObjectSO)
            {
                if (activeBuildableObjectSORandomPrefab.ghostObjectPrefab == null) activeBuildableObjectSORandomPrefab.ghostObjectPrefab = activeBuildableObjectSORandomPrefab.objectPrefab;

                ghostTransformVisual = Instantiate(activeBuildableObjectSORandomPrefab.ghostObjectPrefab, Vector3.zero, Quaternion.identity);
                BuildableCornerObject buildableCornerObject = ghostTransformVisual.GetComponent<BuildableCornerObject>();
                buildableCornerObject.SetBasicGridAreaTrigger();
                buildableCornerObject.SetIsInstantiatedByGhostObject(true);
                parentTransform = new GameObject(buildableCornerObjectSO.objectName).transform;

                SetUpTransformHierarchy(ghostTransformVisual, parentTransform, transform);
                parentTransform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);

                parentTransform.localPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(0, ghostTransformVisual.localPosition.y, 0) : new Vector3(0, 0, ghostTransformVisual.localPosition.z);
                
                UpdateVisualMaterials();
                SetLayerRecursive(parentTransform.gameObject, GetHighestLayerSet());
            }
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
            UpdateMultipleVisualMaterials();
        }

        private void UpdateSingleVisualMaterial()
        {
            if (ghostTransformVisual == null) return;

            Material selectedMaterial = DetermineMaterialBasedOnPlacement();
            if (selectedMaterial == null) return;
            ApplyMaterialToGameObjectAndChildren(ghostTransformVisual.gameObject, selectedMaterial);
        }

        private void UpdateMultipleVisualMaterials()
        {
            if (ghostTransformVisualDictionary.Count == 0) return;
            
            CornerObjectCellDirection cornerObjectOriginCellDirection = default;
            foreach (KeyValuePair<Vector2Int, Transform> entry in ghostTransformVisualDictionary)
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ easyGridBuilderProXZ) easyGridBuilderProXZ.GetClosestCorner(boxPlacementStartPosition, 
                    easyGridBuilderProXZ.GetActiveGridCellWorldPosition(entry.Key), out cornerObjectOriginCellDirection);
                else if (activeEasyGridBuilderPro is EasyGridBuilderProXY easyGridBuilderProXY) easyGridBuilderProXY.GetClosestCorner(boxPlacementStartPosition, 
                    easyGridBuilderProXY.GetActiveGridCellWorldPosition(entry.Key), out cornerObjectOriginCellDirection);
                break;
            }

            foreach (KeyValuePair<Vector2Int, Transform> entry in ghostTransformVisualDictionary)
            {
                Material selectedMaterial = DetermineMaterialBasedOnBoxPlacement(entry.Key, cornerObjectOriginCellDirection);
                if (selectedMaterial == null) return;
                ApplyMaterialToGameObjectAndChildren(entry.Value.gameObject, selectedMaterial);
            }
        }

        private Material DetermineMaterialBasedOnPlacement()
        {
            bool isPlaceable = IsVisualObjectPlaceable();
            return GetPlacementMaterial(isPlaceable);
        }

        private Material DetermineMaterialBasedOnBoxPlacement(Vector2Int objectCellPosition, CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            bool isPlaceable = IsVisualObjectPlaceable(true, objectCellPosition, cornerObjectOriginCellDirection);
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

        private bool IsVisualObjectPlaceable(bool invokedFromBoxPlacement = false, Vector2Int objectCellPosition = default, CornerObjectCellDirection cornerObjectOriginCellDirection = default)
        {
            BuildableCornerObjectSO buildableCornerObjectSO = (BuildableCornerObjectSO)activeBuildableObjectSO;

            Vector3 worldPosition;
            Vector3 secondRayDirection = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;

            if (invokedFromBoxPlacement) worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(objectCellPosition);
            else worldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableCornerObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), secondRayDirection, out _, out _);
            
            Vector2Int originCellPosition = invokedFromBoxPlacement ? objectCellPosition : activeEasyGridBuilderPro.GetActiveGridCellPosition(worldPosition);

            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ easyGridBuilderProXZ)
            {   
                if (!invokedFromBoxPlacement) easyGridBuilderProXZ.GetClosestCorner(worldPosition, easyGridBuilderProXZ.GetActiveGridCellWorldPosition(originCellPosition), out cornerObjectOriginCellDirection);
                return CheckPlaceableXZ(easyGridBuilderProXZ, originCellPosition, worldPosition, buildableCornerObjectSO, cornerObjectOriginCellDirection);
            }
            else if (activeEasyGridBuilderPro is EasyGridBuilderProXY easyGridBuilderProXY)
            {
                if (!invokedFromBoxPlacement) easyGridBuilderProXY.GetClosestCorner(worldPosition, easyGridBuilderProXY.GetActiveGridCellWorldPosition(originCellPosition), out cornerObjectOriginCellDirection);
                return CheckPlaceableXY(easyGridBuilderProXY, originCellPosition, worldPosition, buildableCornerObjectSO, cornerObjectOriginCellDirection);
            }
            return true;
        }

        private bool CheckPlaceableXZ(EasyGridBuilderProXZ easyGridBuilderProXZ, Vector2Int originCellPosition, Vector3 worldPosition, BuildableCornerObjectSO buildableCornerObjectSO, CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            Dictionary<CellPositionXZ, CornerObjectCellDirection> cellPositionsDictionary = easyGridBuilderProXZ.GetCornerObjectCellPositionDictionary(new CellPositionXZ(originCellPosition.x, originCellPosition.y), cornerObjectOriginCellDirection);
            Grid activeGrid = easyGridBuilderProXZ.GetActiveGrid();
                
            bool isWithinGridBounds = false;
            foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)))
                {
                    isWithinGridBounds = true;
                    break;
                }
            }
            if (!isWithinGridBounds) return false;

            foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                switch (cellPosition.Value)
                {
                    case CornerObjectCellDirection.NorthEast: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectNorthEastData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectNorthEastData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case CornerObjectCellDirection.SouthEast: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectSouthEastData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectSouthEastData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case CornerObjectCellDirection.SouthWest: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectSouthWestData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectSouthWestData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case CornerObjectCellDirection.NorthWest: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z))) continue;
                        if (easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectNorthWestData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXZ.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXZ.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.z)).GetBuildableCornerObjectNorthWestData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                }      
            }

            foreach (KeyValuePair<CellPositionXZ, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeBuildableObjectSO.affectByBasicAreaEnablers && activeEasyGridBuilderPro.IsBuildableObjectEnabledByBasicGridAreaEnablers()) continue;
                if (activeBuildableObjectSO.affectByAreaEnablers && activeEasyGridBuilderPro.IsCornerObjectEnabledByGridAreaEnablers(activeGrid, buildableCornerObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value)) continue;
                if (activeBuildableObjectSO.affectByBasicAreaDisablers && activeEasyGridBuilderPro.IsBuildableObjectDisabledByBasicGridAreaDisablers()) return false;
                if (activeBuildableObjectSO.affectByAreaDisablers && activeEasyGridBuilderPro.IsCornerObjectDisabledByGridAreaDisablers(activeGrid, buildableCornerObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.z), cellPosition.Value)) return false;
            }
            if (gridManager.GetActiveCameraMode() != CameraMode.ThirdPerson) return activeEasyGridBuilderPro.CheckPlacementDistanceForGhostObject(worldPosition);

            return true;
        }

        private bool CheckPlaceableXY(EasyGridBuilderProXY easyGridBuilderProXY, Vector2Int originCellPosition, Vector3 worldPosition, BuildableCornerObjectSO buildableCornerObjectSO, CornerObjectCellDirection cornerObjectOriginCellDirection)
        {
            Dictionary<CellPositionXY, CornerObjectCellDirection> cellPositionsDictionary = easyGridBuilderProXY.GetCornerObjectCellPositionDictionary(new CellPositionXY(originCellPosition.x, originCellPosition.y), cornerObjectOriginCellDirection);
            Grid activeGrid = easyGridBuilderProXY.GetActiveGrid();
                
            bool isWithinGridBounds = false;
            foreach (KeyValuePair<CellPositionXY, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)))
                {
                    isWithinGridBounds = true;
                    break;
                }
            }
            if (!isWithinGridBounds) return false;

            foreach (KeyValuePair<CellPositionXY, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                switch (cellPosition.Value)
                {
                    case CornerObjectCellDirection.NorthEast: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectNorthEastData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectNorthEastData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case CornerObjectCellDirection.SouthEast: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectSouthEastData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectSouthEastData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case CornerObjectCellDirection.SouthWest: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectSouthWestData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectSouthWestData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                    case CornerObjectCellDirection.NorthWest: 
                        if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y))) continue;
                        if (easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectNorthWestData().ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO))
                        {
                            if (easyGridBuilderProXY.GetIsObjectMoving()) return false;
                            BuildableObject buildableObject = easyGridBuilderProXY.GetActiveGridCellData(new Vector2Int(cellPosition.Key.x, cellPosition.Key.y)).GetBuildableCornerObjectNorthWestData()[buildableCornerObjectSO.buildableCornerObjectCategorySO];
                            if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                        }
                    break;
                }      
            }

            foreach (KeyValuePair<CellPositionXY, CornerObjectCellDirection> cellPosition in cellPositionsDictionary)
            {
                if (activeBuildableObjectSO.affectByBasicAreaEnablers && activeEasyGridBuilderPro.IsBuildableObjectEnabledByBasicGridAreaEnablers()) continue;
                if (activeBuildableObjectSO.affectByAreaEnablers && activeEasyGridBuilderPro.IsCornerObjectEnabledByGridAreaEnablers(activeGrid, buildableCornerObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.y), cellPosition.Value)) continue;
                if (activeBuildableObjectSO.affectByBasicAreaDisablers && activeEasyGridBuilderPro.IsBuildableObjectDisabledByBasicGridAreaDisablers()) return false;
                if (activeBuildableObjectSO.affectByAreaDisablers && activeEasyGridBuilderPro.IsCornerObjectDisabledByGridAreaDisablers(activeGrid, buildableCornerObjectSO, new Vector2Int(cellPosition.Key.x, cellPosition.Key.y), cellPosition.Value)) return false;
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

        public bool TryGetGhostObjectVisualDictionary(out Dictionary<Vector2Int, Transform> ghostObjectVisualDictionary)
        {
            ghostObjectVisualDictionary = this.ghostTransformVisualDictionary;
            if (ghostTransformVisualDictionary == null) return false;

            return this.ghostTransformVisualDictionary.Count != 0;
        }

        public bool TryGetBoxPlacementHolderObject(out Transform boxPlacementHolderObject)
        {
            boxPlacementHolderObject = this.boxPlacementHolderObject;
            return this.boxPlacementHolderObject != null;
        }

        public Vector3 GetObjectScaleForObjectGridAlphaMask()
        {
            if (IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
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
            if (IsActiveBuildableCornerObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
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