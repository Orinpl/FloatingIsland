using UnityEngine;
using SoulGames.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Grid Object Ghost", 3)]
    public class BuildableGridObjectGhost : MonoBehaviour
    {
        [SerializeField] private LayerMask ghostObjectLayer;
        [SerializeField] private float ghostObjectMoveSpeed = 25f;
        [SerializeField] private float ghostObjectRotationSpeed = 25f;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private BuildableObjectSO activeBuildableObjectSO;
        private BuildableObjectSO.RandomPrefabs activeBuildableObjectSORandomPrefab;
        private GridObjectPlacementType activeBuildableGridObjectSOPlacementType;
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
            gridManager.OnGridObjectBoxPlacementStarted -= OnBoxPlacementStarted;
            gridManager.OnGridObjectBoxPlacementUpdated -= OnBoxPlacementUpdated;
            gridManager.OnGridObjectBoxPlacementFinalized -= OnBoxPlacementFinalized;
            gridManager.OnGridObjectBoxPlacementCancelled -= OnBoxPlacementCancelled;
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
            gridManager.OnGridObjectBoxPlacementStarted += OnBoxPlacementStarted;
            gridManager.OnGridObjectBoxPlacementUpdated += OnBoxPlacementUpdated;
            gridManager.OnGridObjectBoxPlacementFinalized += OnBoxPlacementFinalized;
            gridManager.OnGridObjectBoxPlacementCancelled += OnBoxPlacementCancelled;

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

        private void OnBoxPlacementStarted(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementStartPosition, GridObjectPlacementType placementType)
        {
            this.boxPlacementStartPosition = boxPlacementStartPosition;
            activeBuildableGridObjectSOPlacementType = placementType;
            StartBoxPlacement();
        }

        private void OnBoxPlacementUpdated(EasyGridBuilderPro easyGridBuilderPro, Vector3 boxPlacementEndPosition)
        {
            this.boxPlacementEndPosition = boxPlacementEndPosition;
            UpdateBoxPlacement();
        }
        
        private void OnBoxPlacementFinalized(EasyGridBuilderPro easyGridBuilderPro)
        {
            activeBuildableGridObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableGridObjectSOPlacementType();
            FinalizeBoxPlacement();
        }

        private void OnBoxPlacementCancelled(EasyGridBuilderPro easyGridBuilderPro)
        {
            activeBuildableGridObjectSOPlacementType = easyGridBuilderPro.GetActiveBuildableGridObjectSOPlacementType();
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
            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO && activeEasyGridBuilderPro.GetActiveGridMode() is GridMode.BuildMode)
            {
                if (MouseInteractionUtilities.IsMousePointerOverUI()) return;
                UpdateGhostObjectPosition(buildableGridObjectSO);
                UpdateGhostObjectRotation(buildableGridObjectSO);
            }
        }

        #region Ghost Object Update Functions Start:
        private void UpdateGhostObjectPosition(BuildableGridObjectSO buildableGridObjectSO)
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();

            Vector3 targetPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? CalculateSnappingPositionXZ(buildableGridObjectSO) : CalculateSnappingPositionXY(buildableGridObjectSO);

            if (buildableGridObjectSO.raiseObjectWithVerticalGrids) 
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) targetPosition += new Vector3(0, activeVerticalGridIndex * verticalGridHeight, 0);
                else targetPosition += new Vector3(0, 0, -activeVerticalGridIndex * verticalGridHeight);
            }

            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * ghostObjectMoveSpeed);
        }

        private Vector3 CalculateSnappingPositionXZ(BuildableGridObjectSO buildableGridObjectSO)
        {
            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            Vector2Int rotationOffset = buildableGridObjectSO.GetObjectRotationOffset(activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation(), cellSize, activeEasyGridBuilderPro.GetActiveBuildableObjectSORandomPrefab());
            
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableGridObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), Vector3.down * 9999, 
                out _, out Vector3 firstCollisionWorldPosition);

            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
            Vector3 rayOrigin =  new Vector3(worldPosition.x + (cellSize / 2), 0, worldPosition.z + (cellSize / 2)) + new Vector3(0, firstCollisionWorldPosition.y, 0) + Vector3.up * 1000; // Start raycast from above

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, Mathf.Infinity, buildableGridObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
            {
                firstCollisionWorldPosition.y = GetClosestVerticalGridSnappingHeight(buildableGridObjectSO, activeEasyGridBuilderPro.transform.position.y, hit.point.y, activeEasyGridBuilderPro.GetVerticalGridHeight());
            }

            return new Vector3(worldPosition.x, firstCollisionWorldPosition.y, worldPosition.z) + new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        }

        private Vector3 CalculateSnappingPositionXY(BuildableGridObjectSO buildableGridObjectSO)
        {
            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            Vector2Int rotationOffset = buildableGridObjectSO.GetObjectRotationOffset(activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation(), cellSize, activeEasyGridBuilderPro.GetActiveBuildableObjectSORandomPrefab());
            
            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableGridObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), Vector3.forward * 9999, 
                out _, out Vector3 firstCollisionWorldPosition);

            Vector2Int cellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
            Vector3 rayOrigin =  new Vector3(worldPosition.x + (cellSize / 2), worldPosition.z + (cellSize / 2), 0) + new Vector3(0, 0, firstCollisionWorldPosition.z) + Vector3.back * 1000; // Start raycast from above

            if (Physics.Raycast(rayOrigin, Vector3.forward, out RaycastHit hit, Mathf.Infinity, buildableGridObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
            {
                firstCollisionWorldPosition.z = GetClosestVerticalGridSnappingHeight(buildableGridObjectSO, activeEasyGridBuilderPro.transform.position.z, hit.point.z, activeEasyGridBuilderPro.GetVerticalGridHeight());
            }

            return new Vector3(worldPosition.x, worldPosition.y, firstCollisionWorldPosition.z) + new Vector3(rotationOffset.x, rotationOffset.y, 0) * cellSize;
        }

        private float GetClosestVerticalGridSnappingHeight(BuildableGridObjectSO buildableGridObjectSO, float gridHeight, float height, float verticalGridHeight)
        {
            if (buildableGridObjectSO.customSurfaceLayerMask == 0) return height;
            if (buildableGridObjectSO.snapToTheClosestVerticalGridHeight)
            {
                // Find the difference between the height and the base gridHeight
                float relativeHeight = height - gridHeight;

                // Calculate the closest lower and upper multiples of verticalGridHeight
                float lowerMultiple = Mathf.Floor(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;
                float upperMultiple = Mathf.Ceil(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;

                // Calculate the threshold value (percentage of verticalGridHeight)
                float thresholdValue = (buildableGridObjectSO.minimumHeightThresholdPercentage / 100f) * verticalGridHeight;

                if (height - lowerMultiple >= thresholdValue) return upperMultiple;
                else return lowerMultiple;
            }
            return height;
        }

        private void UpdateGhostObjectRotation(BuildableGridObjectSO buildableGridObjectSO)
        {
            float targetAngle = buildableGridObjectSO.GetRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation());

            Quaternion targetRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, targetAngle, 0) : Quaternion.Euler(0, 0, -targetAngle);
            transform.rotation  = Quaternion.Lerp(transform.localRotation, targetRotation, Time.deltaTime * ghostObjectRotationSpeed);
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
            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO)
            {
                Vector2Int startCell = activeEasyGridBuilderPro.GetActiveGridCellPosition(boxPlacementStartPosition);
                Vector2Int endCell = activeEasyGridBuilderPro.GetActiveGridCellPosition(boxPlacementEndPosition);

                boxPlacementCellPositionSet.Clear();

                int minX = Mathf.Min(startCell.x, endCell.x);
                int maxX = Mathf.Max(startCell.x, endCell.x);
                int minY = Mathf.Min(startCell.y, endCell.y);
                int maxY = Mathf.Max(startCell.y, endCell.y);

                switch (activeBuildableGridObjectSOPlacementType)
                {
                    case GridObjectPlacementType.BoxPlacement: UpdateBoxPlacementCellPositions(startCell, endCell, minX, maxX, minY, maxY); break;   
                    case GridObjectPlacementType.WireBoxPlacement: UpdateWireBoxPlacementCellPositions(minX, maxX, minY, maxY, buildableGridObjectSO); break;
                    case GridObjectPlacementType.FourDirectionWirePlacement: UpdateFourDirectionWirePlacementCellPositions(startCell, endCell, minX, maxX, minY, maxY, buildableGridObjectSO); break;
                    case GridObjectPlacementType.LShapedPlacement: UpdateLShapedPlacementCellPositions(startCell, endCell, buildableGridObjectSO); break;
                }
                UpdateBoxPlacementVisuals(buildableGridObjectSO);
            }
        }

        private void UpdateBoxPlacementCellPositions(Vector2Int startCell, Vector2Int endCell, int minX, int maxX, int minY, int maxY)
        {
            FourDirectionalRotation rotation = activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation();

            if (rotation == FourDirectionalRotation.North || rotation == FourDirectionalRotation.South) HandleNorthSouthDirections(minX, maxX, minY, maxY, startCell, endCell);
            else HandleEastWestDirections(minX, maxX, minY, maxY, startCell, endCell);
        }

        private void HandleNorthSouthDirections(int minX, int maxX, int minY, int maxY, Vector2Int startCell, Vector2Int endCell)
        {
            if (endCell.y < startCell.y) NorthSouthAddCellsYFirst(minX, maxX, maxY, minY);      // Moving north
            else if (endCell.y > startCell.y) NorthSouthAddCellsYFirst(minX, maxX, minY, maxY); // Moving south
            else if (endCell.x < startCell.x) NorthSouthAddCellsXFirst(maxX, minX, minY, maxY); // Moving west
            else if (endCell.x > startCell.x) NorthSouthAddCellsXFirst(minX, maxX, maxY, minY); // Moving east
        }

        private void HandleEastWestDirections(int minX, int maxX, int minY, int maxY, Vector2Int startCell, Vector2Int endCell)
        {
            if (endCell.x < startCell.x) EastWestAddCellsXFirst(maxX, minX, minY, maxY);        // Moving west
            else if (endCell.x > startCell.x) EastWestAddCellsXFirst(minX, maxX, maxY, minY);   // Moving east
            else if (endCell.y < startCell.y) EastWestAddCellsYFirst(maxY, minY, minX, maxX);   // Moving north
            else if (endCell.y > startCell.y)  EastWestAddCellsYFirst(minY, maxY, maxX, minX);  // Moving south
        }

        private void NorthSouthAddCellsYFirst(int startX, int endX, int startY, int endY)
        {
            int xStep = startX <= endX ? 1 : -1;
            int yStep = startY <= endY ? 1 : -1;

            for (int y = startY; y != endY + yStep; y += yStep)
            {
                for (int x = startX; x != endX + xStep; x += xStep)
                {
                    if (boxPlacementCellPositionSet.Count >= gridManager.GetMaxBoxPlacementObjects()) return;
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, y));
                }
            }
        }

        private void NorthSouthAddCellsXFirst(int startX, int endX, int startY, int endY)
        {
            int xStep = startX <= endX ? 1 : -1;
            int yStep = startY <= endY ? 1 : -1;

            for (int x = startX; x != endX + xStep; x += xStep)
            {
                for (int y = startY; y != endY + yStep; y += yStep)
                {
                    if (boxPlacementCellPositionSet.Count >= gridManager.GetMaxBoxPlacementObjects()) return;
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, y));
                }
            }
        }

        private void EastWestAddCellsXFirst(int startX, int endX, int startY, int endY)
        {
            int xStep = startX <= endX ? 1 : -1;
            int yStep = startY <= endY ? 1 : -1;

            for (int x = startX; x != endX + xStep; x += xStep)
            {
                for (int y = startY; y != endY + yStep; y += yStep)
                {
                    if (boxPlacementCellPositionSet.Count >= gridManager.GetMaxBoxPlacementObjects()) return;
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, y));
                }
            }
        }

        private void EastWestAddCellsYFirst(int startY, int endY, int startX, int endX)
        {
            int xStep = startX <= endX ? 1 : -1;
            int yStep = startY <= endY ? 1 : -1;

            for (int y = startY; y != endY + yStep; y += yStep)
            {
                for (int x = startX; x != endX + xStep; x += xStep)
                {
                    if (boxPlacementCellPositionSet.Count >= gridManager.GetMaxBoxPlacementObjects()) return;
                    boxPlacementCellPositionSet.Add(new Vector2Int(x, y));
                }
            }
        }

        private void UpdateWireBoxPlacementCellPositions(int minX, int maxX, int minY, int maxY, BuildableGridObjectSO buildableGridObjectSO)
        {
            if (buildableGridObjectSO.spawnOnlyAtEndPoints)
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

        private void UpdateFourDirectionWirePlacementCellPositions(Vector2Int startCell, Vector2Int endCell, int minX, int maxX, int minY, int maxY, BuildableGridObjectSO buildableGridObjectSO)
        {
            if (buildableGridObjectSO.spawnOnlyAtEndPoints)
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
        
        private void UpdateLShapedPlacementCellPositions(Vector2Int startCell, Vector2Int endCell, BuildableGridObjectSO buildableGridObjectSO)
        {
            bool isHorizontalFirst = Mathf.Abs(endCell.x - startCell.x) >= Mathf.Abs(endCell.y - startCell.y);
            bool isPositiveDirection = isHorizontalFirst ? (endCell.x >= startCell.x) : (endCell.y >= startCell.y);

            if (buildableGridObjectSO.spawnOnlyAtEndPoints)
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

        private void UpdateBoxPlacementVisuals(BuildableGridObjectSO buildableGridObjectSO)
        {
            RemoveInvalidGhostObjects(buildableGridObjectSO);
            AddValidGhostObjects(buildableGridObjectSO);
        }

        private void RemoveInvalidGhostObjects(BuildableGridObjectSO buildableGridObjectSO)
        {
            foreach (Vector2Int cellPosition in ghostTransformVisualDictionary.Keys.ToList())
            {
                if (!boxPlacementCellPositionSet.Contains(cellPosition))
                {
                    Destroy(parentObjectDictionary[cellPosition].gameObject);
                    ghostTransformVisualDictionary.Remove(cellPosition);
                    parentObjectDictionary.Remove(cellPosition);
                    MarkCellsAsUnoccupied(cellPosition, buildableGridObjectSO);
                }
            }
        }

        private void MarkCellsAsUnoccupied(Vector2Int originCellPosition, BuildableGridObjectSO buildableGridObjectSO)
        {
            List<Vector2Int> cellPositionList = GetObjectCellPositionsList(originCellPosition, buildableGridObjectSO);
            foreach (var cellPosition in cellPositionList)
            {
                occupiedCellsSet.Remove(cellPosition);
            }
        }

        private void AddValidGhostObjects(BuildableGridObjectSO buildableGridObjectSO)
        {
            int activeVerticalGridIndex = activeEasyGridBuilderPro.GetActiveVerticalGridIndex();
            float verticalGridHeight = activeEasyGridBuilderPro.GetVerticalGridHeight();
            float cellSize = activeEasyGridBuilderPro.GetCellSize();

            foreach (Vector2Int cellPosition in boxPlacementCellPositionSet)
            {
                if (!ghostTransformVisualDictionary.ContainsKey(cellPosition) && !occupiedCellsSet.Contains(cellPosition) && !isObjectCellPositionListOccupied(cellPosition, buildableGridObjectSO))
                {
                    Vector3 worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);

                    // Adjust height based on collision for each cell position
                    float customHeight = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? GetAdjustedHeightXZ(worldPosition, cellSize, buildableGridObjectSO, activeVerticalGridIndex, verticalGridHeight) : 
                        GetAdjustedHeightXY(worldPosition, cellSize, buildableGridObjectSO, activeVerticalGridIndex, verticalGridHeight);

                    Transform visual = InstantiateVisualAtCell(worldPosition, buildableGridObjectSO, out Transform grandParentTransform, customHeight).transform;
                    ghostTransformVisualDictionary[cellPosition] = visual;
                    parentObjectDictionary[cellPosition] = grandParentTransform;
                    MarkCellsAsOccupied(cellPosition, buildableGridObjectSO);
                }
            }
        }

        private float GetAdjustedHeightXZ(Vector3 worldPosition, float cellSize, BuildableGridObjectSO buildableGridObjectSO, int activeVerticalGridIndex, float verticalGridHeight)
        {
            float customHeight = worldPosition.y;
            if (buildableGridObjectSO.customSurfaceLayerMask != 0)
            {
                Vector3 raycastOffset = new Vector3(cellSize / 2, 0, cellSize / 2);
                Vector3 rayOrigin = (worldPosition + raycastOffset) + Vector3.up * 1000; // Start raycast from above

                if (Physics.Raycast(rayOrigin, Vector3.down * 9999, out RaycastHit hit, Mathf.Infinity, buildableGridObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
                {
                    if (buildableGridObjectSO.raiseObjectWithVerticalGrids) customHeight = (activeVerticalGridIndex * verticalGridHeight) + hit.point.y;
                    else customHeight = hit.point.y;

                    customHeight = GetClosestVerticalGridSnappingHeight(buildableGridObjectSO, activeEasyGridBuilderPro.transform.position.y, customHeight, activeEasyGridBuilderPro.GetVerticalGridHeight());
                }
            }
            return customHeight;
        }

        private float GetAdjustedHeightXY(Vector3 worldPosition, float cellSize, BuildableGridObjectSO buildableGridObjectSO, int activeVerticalGridIndex, float verticalGridHeight)
        {
            float customHeight = worldPosition.z;
            if (buildableGridObjectSO.customSurfaceLayerMask != 0)
            {
                Vector3 raycastOffset = new Vector3(cellSize / 2, cellSize / 2, 0);
                Vector3 rayOrigin = (worldPosition + raycastOffset) + Vector3.back * 1000; // Start raycast from above

                if (Physics.Raycast(rayOrigin, Vector3.forward * 9999, out RaycastHit hit, Mathf.Infinity, buildableGridObjectSO.customSurfaceLayerMask | gridManager.GetGridSystemLayerMask()))
                {
                    if (buildableGridObjectSO.raiseObjectWithVerticalGrids) customHeight = -(activeVerticalGridIndex * verticalGridHeight) + hit.point.z;
                    else customHeight = hit.point.z;

                    customHeight = GetClosestVerticalGridSnappingHeight(buildableGridObjectSO, activeEasyGridBuilderPro.transform.position.z, customHeight, activeEasyGridBuilderPro.GetVerticalGridHeight());
                }
            }
            return customHeight;
        }

        private bool isObjectCellPositionListOccupied(Vector2Int originCellPosition, BuildableGridObjectSO buildableGridObjectSO)
        {
            foreach (Vector2Int cellPosition in occupiedCellsSet)
            {
                if (GetObjectCellPositionsList(originCellPosition, buildableGridObjectSO).Contains(cellPosition)) return true; 
            }
            return false;
        }

        private void MarkCellsAsOccupied(Vector2Int originCellPosition, BuildableGridObjectSO buildableGridObjectSO)
        {
            List<Vector2Int> cellPositionList = GetObjectCellPositionsList(originCellPosition, buildableGridObjectSO);
            foreach (var cellPosition in cellPositionList)
            {
                occupiedCellsSet.Add(cellPosition);
            }
        }

        private List<Vector2Int> GetObjectCellPositionsList(Vector2Int originCellPosition, BuildableGridObjectSO buildableGridObjectSO)
        {
            FourDirectionalRotation visualRotation = activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation();
            float cellSize = activeEasyGridBuilderPro.GetCellSize();
            return buildableGridObjectSO.GetObjectCellPositionsList(originCellPosition, visualRotation, cellSize, activeBuildableObjectSORandomPrefab);
        }

        private GameObject InstantiateVisualAtCell(Vector3 worldPosition, BuildableGridObjectSO buildableGridObjectSO, out Transform grandParentTransform, float customYPosition = 0)
        {
            Transform firstPrefab = buildableGridObjectSO.randomPrefabs[0].ghostObjectPrefab;
            if (firstPrefab == null) firstPrefab = buildableGridObjectSO.randomPrefabs[0].objectPrefab;

            Transform ghostObjectVisual = Instantiate(firstPrefab, Vector3.zero, Quaternion.identity);
            BuildableGridObject buildableGridObject = ghostObjectVisual.GetComponent<BuildableGridObject>();
            buildableGridObject.SetBasicGridAreaTrigger();
            buildableGridObject.SetIsInstantiatedByGhostObject(true);

            Transform parentTransform = new GameObject(buildableGridObjectSO.objectName).transform;
            grandParentTransform = new GameObject(buildableGridObjectSO.objectName).transform;

            SetUpTransformHierarchy(ghostObjectVisual, parentTransform, grandParentTransform);
            parentTransform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);
            grandParentTransform.parent = boxPlacementHolderObject;

            Vector2Int objectSize = GetObjectSize(buildableGridObjectSO, out float cellSize);
            PositionParentTransform(parentTransform, ghostObjectVisual, objectSize, cellSize, buildableGridObject);
            RotateAndPositionGrandParentTransform(buildableGridObjectSO, grandParentTransform, worldPosition, cellSize);

            // Apply custom Height
            grandParentTransform.localPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(grandParentTransform.localPosition.x, customYPosition, grandParentTransform.localPosition.z) :
                new Vector3(grandParentTransform.localPosition.x, grandParentTransform.localPosition.y, customYPosition);
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

        private Vector2Int GetObjectSize(BuildableGridObjectSO buildableGridObjectSO, out float cellSize)
        {
            cellSize = activeEasyGridBuilderPro.GetCellSize();
            return buildableGridObjectSO.GetObjectSizeRelativeToCellSize(cellSize, activeBuildableObjectSORandomPrefab);
        }

        private void PositionParentTransform(Transform parentTransform, Transform ghostObjectVisual, Vector2Int objectSize, float cellSize, BuildableGridObject buildableGridObject)
        {
            parentTransform.localPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(objectSize.x * cellSize / 2, ghostObjectVisual.localPosition.y, objectSize.y * cellSize / 2) : 
                new Vector3(objectSize.x * cellSize / 2, objectSize.y * cellSize / 2, ghostObjectVisual.localPosition.z);
        }

        private void RotateAndPositionGrandParentTransform(BuildableGridObjectSO buildableGridObjectSO, Transform grandParentTransform, Vector3 worldPosition, float cellSize)
        {
            float targetAngle = buildableGridObjectSO.GetRotationAngle(activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation());
            grandParentTransform.localRotation = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Quaternion.Euler(0, targetAngle, 0) : Quaternion.Euler(0, 0, -targetAngle);

            Vector2Int rotationOffset = buildableGridObjectSO.GetObjectRotationOffset(activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation(), cellSize, activeBuildableObjectSORandomPrefab);
            Vector3 offsetPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize : new Vector3(rotationOffset.x, rotationOffset.y, 0) * cellSize;

            grandParentTransform.localPosition = worldPosition + offsetPosition;
        }
        
        private bool IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes()
        {
            if (activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.BoxPlacement || activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.FourDirectionWirePlacement || 
                activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.WireBoxPlacement || activeBuildableGridObjectSOPlacementType == GridObjectPlacementType.LShapedPlacement) return true;
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
            if (IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes() || activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.BuildMode) return;

            if (activeBuildableObjectSO is BuildableGridObjectSO buildableGridObjectSO)
            {
                if (activeBuildableObjectSORandomPrefab.ghostObjectPrefab == null) activeBuildableObjectSORandomPrefab.ghostObjectPrefab = activeBuildableObjectSORandomPrefab.objectPrefab;
                
                ghostTransformVisual = Instantiate(activeBuildableObjectSORandomPrefab.ghostObjectPrefab, Vector3.zero, Quaternion.identity);
                BuildableGridObject buildableGridObject = ghostTransformVisual.GetComponent<BuildableGridObject>();
                buildableGridObject.SetBasicGridAreaTrigger();
                buildableGridObject.SetIsInstantiatedByGhostObject(true);
                parentTransform = new GameObject(buildableGridObjectSO.objectName).transform;

                SetUpTransformHierarchy(ghostTransformVisual, parentTransform, transform);
                parentTransform.localScale += new Vector3(ADDITIVE_SCALE, ADDITIVE_SCALE, ADDITIVE_SCALE);
                float cellSize = activeEasyGridBuilderPro.GetCellSize();

                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ)
                {
                    parentTransform.localPosition = new Vector3(GetObjectSize(buildableGridObjectSO, out float _).x * cellSize / 2, ghostTransformVisual.localPosition.y, GetObjectSize(buildableGridObjectSO, out float _).y * cellSize / 2);
                }
                else
                {
                    parentTransform.localPosition = new Vector3(GetObjectSize(buildableGridObjectSO, out float _).x * cellSize / 2, GetObjectSize(buildableGridObjectSO, out float _).y * cellSize / 2, ghostTransformVisual.localPosition.z);
                }
                
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
            if (ghostTransformVisual != null)
            {
                Material selectedMaterial = DetermineMaterialBasedOnPlacement();
                if (selectedMaterial == null) return;
                ApplyMaterialToGameObjectAndChildren(ghostTransformVisual.gameObject, selectedMaterial);
            }
        }

        private void UpdateMultipleVisualMaterials()
        {
            if (ghostTransformVisualDictionary.Count == 0) return;

            foreach (KeyValuePair<Vector2Int, Transform> entry in ghostTransformVisualDictionary)
            {
                Material selectedMaterial = DetermineMaterialBasedOnBoxPlacement(entry.Key);
                if (selectedMaterial == null) return;
                ApplyMaterialToGameObjectAndChildren(entry.Value.gameObject, selectedMaterial);
            }
        }

        private Material DetermineMaterialBasedOnPlacement()
        {
            bool isPlaceable = IsVisualObjectPlaceable();
            return GetPlacementMaterial(isPlaceable);
        }

        private Material DetermineMaterialBasedOnBoxPlacement(Vector2Int objectCellPosition)
        {
            bool isPlaceable = IsVisualObjectPlaceable(true, objectCellPosition);
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

        private bool IsVisualObjectPlaceable(bool invokedFromBoxPlacement = false, Vector2Int objectCellPosition = default)
        {
            BuildableGridObjectSO buildableGridObjectSO = (BuildableGridObjectSO)activeBuildableObjectSO;

            Vector3 worldPosition;
            Vector3 secondRayDirection = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;

            if (invokedFromBoxPlacement) worldPosition = activeEasyGridBuilderPro.GetActiveGridCellWorldPosition(objectCellPosition);
            else worldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(buildableGridObjectSO.customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), secondRayDirection, out _, out _);
            
            Vector2Int originCellPosition = invokedFromBoxPlacement ? objectCellPosition : activeEasyGridBuilderPro.GetActiveGridCellPosition(worldPosition);

            FourDirectionalRotation direction = activeEasyGridBuilderPro.GetActiveBuildableGridObjectRotation();
            float cellSize = activeEasyGridBuilderPro.GetCellSize();

            List<Vector2Int> cellPositionList = buildableGridObjectSO.GetObjectCellPositionsList(originCellPosition, direction, cellSize, activeBuildableObjectSORandomPrefab);
            Grid activeGrid = activeEasyGridBuilderPro.GetActiveGrid();

            foreach (Vector2Int cellPosition in cellPositionList)
            {
                if (!activeEasyGridBuilderPro.IsWithinActiveGridBounds(cellPosition)) return false;
                if (activeEasyGridBuilderPro.GetActiveGridCellData(cellPosition).GetBuildableGridObjectData().ContainsKey(buildableGridObjectSO.buildableGridObjectCategorySO))
                {
                    if (activeEasyGridBuilderPro.GetIsObjectMoving()) return false;
                    BuildableObject buildableObject = activeEasyGridBuilderPro.GetActiveGridCellData(cellPosition).GetBuildableGridObjectData()[buildableGridObjectSO.buildableGridObjectCategorySO];
                    if (!buildableObject.GetBuildableObjectSO().isObjectReplacable || !gridManager.TryGetBuildableObjectDestroyer(out _)) return false;
                }
                if (activeBuildableObjectSO.affectByBasicAreaEnablers && activeEasyGridBuilderPro.IsBuildableObjectEnabledByBasicGridAreaEnablers()) continue;
                if (activeBuildableObjectSO.affectByAreaEnablers && activeEasyGridBuilderPro.IsGridObjectEnabledByGridAreaEnablers(activeGrid, buildableGridObjectSO, cellPosition)) continue;
                if (activeBuildableObjectSO.affectByBasicAreaDisablers && activeEasyGridBuilderPro.IsBuildableObjectDisabledByBasicGridAreaDisablers()) return false;
                if (activeBuildableObjectSO.affectByAreaDisablers && activeEasyGridBuilderPro.IsGridObjectDisabledByGridAreaDisablers(activeGrid, buildableGridObjectSO, cellPosition)) return false;
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
            if (IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
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
            if (IsActiveBuildableObjectSOPlacementTypeOneOfTheBoxPlacementTypes())
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
    }
}