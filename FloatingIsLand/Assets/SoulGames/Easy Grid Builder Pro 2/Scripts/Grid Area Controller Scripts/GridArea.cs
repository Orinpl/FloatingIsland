using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;
using System.Collections;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Grid Area", 0)]
    public class GridArea : MonoBehaviour
    {
        public static event OnGridAreaInitializedDelegate OnGridAreaInitialized;
        public delegate void OnGridAreaInitializedDelegate(GridArea gridArea, GridAreaData gridAreaData);

        public static event OnGridAreaUpdatedDelegate OnGridAreaUpdated;
        public delegate void OnGridAreaUpdatedDelegate(GridArea gridArea);

        [SerializeField] private bool isStatic = true;
        [SerializeField] private GridAxis gridAxis = GridAxis.XZ;
        [SerializeField] private bool affectAllVerticalGrids;
        [SerializeField] private AreaShape areaShape = AreaShape.Rectangle;
        [SerializeField] private AreaUpdateMode areaUpdateMode = AreaUpdateMode.Initialize;
        [SerializeField] private bool updateOnValueChange;
        [SerializeField] private float cellSize;
        [SerializeField] private int width;
        [SerializeField] private int length;
        [SerializeField] private float radius;

        #if UNITY_EDITOR
        [SerializeField] private bool enableGizmos = true;
        [SerializeField] private bool enableSimplifiedGizmos = false;
        [SerializeField] private Color gizmoColor = Color.red;
        #endif

        private Vector3 previousTransformPosition;
        private float previousCellSize;
        private int previousWidth;
        private int previousLength;
        private float previousRadius;
        
        private EasyGridBuilderPro currentOccupiedEasyGridBuilderPro;
        private EasyGridBuilderPro previousOccupiedEasyGridBuilderPro;
        private Grid currentOccupiedGrid;
        private Grid previousOccupiedGrid;
        private List<Vector2Int> currentOccupiedCellPositionList;
        private List<Vector2Int> previousOccupiedCellPositionList;
        private List<Vector2Int> previousGridAllOccupiedCellPositionList;
        private List<Vector2Int> currentGridAllOccupiedCellPositionList;

        private GridAreaData gridAreaData;

        private void Start()
        {
            InitializeVariables();
            StartCoroutine(LateStart());
        }

        private void InitializeVariables()
        {
            gridAreaData = new GridAreaData();

            currentOccupiedCellPositionList = new List<Vector2Int>();
            previousOccupiedCellPositionList = new List<Vector2Int>();
            previousGridAllOccupiedCellPositionList = new List<Vector2Int>();
            currentGridAllOccupiedCellPositionList = new List<Vector2Int>();

            previousOccupiedEasyGridBuilderPro = currentOccupiedEasyGridBuilderPro;
            previousOccupiedGrid = currentOccupiedGrid;

            previousTransformPosition = transform.position;
            previousCellSize = cellSize;
            previousWidth = width;
            previousLength = length;
            previousRadius = radius;
        }
        
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();
                
            UpdateGridSystemList(true);
            CalculateOccupiedCells(true);
        }

        private void Update()
        {
            if (isStatic) return;

            if (areaUpdateMode == AreaUpdateMode.OnPositionChanged && previousTransformPosition != transform.position)
            {
                UpdateGridListAndAreaShape();
                previousTransformPosition = transform.position;
            }
            else if (areaUpdateMode == AreaUpdateMode.Continuous)
            {
                UpdateGridListAndAreaShape();
            }

            if (updateOnValueChange && HaveValuesChanged())
            {
                CacheCurrentValues();
                CalculateOccupiedCells(false);
            }
        }

        private void UpdateGridListAndAreaShape()
        {
            UpdateGridSystemList(false);
            CalculateOccupiedCells(false);
        }

        private bool HaveValuesChanged() => cellSize != previousCellSize || width != previousWidth || length != previousLength || radius != previousRadius;

        private void CacheCurrentValues()
        {
            previousTransformPosition = transform.position;
            previousCellSize = cellSize;
            previousWidth = width;
            previousLength = length;
            previousRadius = radius;
        }

        private void UpdateGridSystemList(bool isCalledFromInitialize)
        {
            Vector3[] directions = GetRaycastDirections();
            float closestDistance = Mathf.Infinity;

            foreach (Vector3 direction in directions)
            {
                PerformRaycast(transform.position, direction, ref closestDistance, isCalledFromInitialize);
            }
        }
        
        private Vector3[] GetRaycastDirections()
        {
            if (gridAxis == GridAxis.XZ) return new Vector3[] { Vector3.down, Vector3.up };
            else return new Vector3[] { Vector3.forward, Vector3.back };
        }

        private void PerformRaycast(Vector3 origin, Vector3 direction, ref float closestDistance, bool isCalledFromInitialize)
        {
            RaycastHit hit;
            Vector3 RaycastOffset = new Vector3(0.1f, 0.1f, 0.1f);

            if (Physics.Raycast(origin, direction, out hit, Mathf.Infinity, GridManager.Instance.GetGridSystemLayerMask()))
            {
                RaycastResult(hit, origin, ref closestDistance, isCalledFromInitialize);
            }
            else if (Physics.Raycast(origin + RaycastOffset, direction, out hit, Mathf.Infinity, GridManager.Instance.GetGridSystemLayerMask()))
            {
                RaycastResult(hit, origin, ref closestDistance, isCalledFromInitialize);
            }
        }

        private void RaycastResult(RaycastHit hit, Vector3 origin, ref float nearestDistance, bool isCalledFromInitialize)
        {
            if (hit.collider.gameObject.TryGetComponent(out EasyGridBuilderPro easyGridBuilderPro))
            {
                float distance = Vector3.Distance(origin, hit.point);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    
                    if (easyGridBuilderPro != currentOccupiedEasyGridBuilderPro)
                    {
                        previousOccupiedEasyGridBuilderPro = currentOccupiedEasyGridBuilderPro;
                        currentOccupiedEasyGridBuilderPro = easyGridBuilderPro;
                        
                        gridAreaData.currentEasyGridBuilderProChangedDynamicTrigger = true;
                    }
                    if (isCalledFromInitialize) previousOccupiedEasyGridBuilderPro = easyGridBuilderPro;

                    Grid grid = currentOccupiedEasyGridBuilderPro.GetNearestGrid(transform.position);
                    if (grid != currentOccupiedGrid)
                    {
                        previousOccupiedGrid = currentOccupiedGrid;
                        currentOccupiedGrid = grid;

                        SetGridChangeData();
                        gridAreaData.currentGridChangedDynamicTrigger = true;
                    }
                    if (isCalledFromInitialize) previousOccupiedGrid = currentOccupiedGrid;
                }
            }
        }

        private void SetGridChangeData()
        {
            previousGridAllOccupiedCellPositionList.Clear();
            previousGridAllOccupiedCellPositionList.AddRange(currentGridAllOccupiedCellPositionList);
            currentGridAllOccupiedCellPositionList.Clear();
        }

        private void CalculateOccupiedCells(bool isCalledFromInitialize)
        {
            if (areaShape == AreaShape.Rectangle) CalculateRectangleOccupiedCells();
            else CalculateCircleOccupiedCells();

            if (isCalledFromInitialize)
            {
                UpdateGridAreaDisablerData();
                OnGridAreaInitialized?.Invoke(this, gridAreaData);
            }
            else
            {
                UpdateGridAreaDisablerData();
                OnGridAreaUpdated?.Invoke(this);
            }
        }

        private void CalculateRectangleOccupiedCells()
        {
            Vector3 offset = gridAxis is GridAxis.XZ ? new Vector3((width * cellSize) * 0.5f, 0, (length * cellSize) * 0.5f) : new Vector3((width * cellSize) * 0.5f, (length * cellSize) * 0.5f, 0);
            Vector3 basePosition = transform.position - offset;

            previousOccupiedCellPositionList.Clear();
            previousOccupiedCellPositionList.AddRange(currentOccupiedCellPositionList);
            currentOccupiedCellPositionList.Clear();

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < length; z++)
                {
                    Vector3 cellCenter = gridAxis is GridAxis.XZ ? basePosition + new Vector3((x + 0.5f) * cellSize, 0, (z + 0.5f) * cellSize) : 
                        basePosition + new Vector3((x + 0.5f) * cellSize, (z + 0.5f) * cellSize, 0);
                    Vector2Int cellPosition = currentOccupiedEasyGridBuilderPro.GetActiveGridCellPosition(cellCenter);
                    currentOccupiedCellPositionList.Add(cellPosition);
                    if (!currentGridAllOccupiedCellPositionList.Contains(cellPosition))currentGridAllOccupiedCellPositionList.Add(cellPosition);
                }
            }
        }

        private void CalculateCircleOccupiedCells()
        {
            Vector3 offset = gridAxis is GridAxis.XZ ? new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), 0, (length * cellSize) * 0.5f - (0.5f * cellSize)) :
                new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), (length * cellSize) * 0.5f - (0.5f * cellSize), 0);
            Vector3 basePosition = transform.position - offset;
            float radiusSquared = radius * radius;

            previousOccupiedCellPositionList.Clear();
            previousOccupiedCellPositionList.AddRange(currentOccupiedCellPositionList);
            currentOccupiedCellPositionList.Clear();

            Vector3 currentPosition = basePosition;

            if (gridAxis is GridAxis.XZ)
            {
                for (int x = 0; x < width; x++, currentPosition.x += cellSize)
                {
                    currentPosition.z = basePosition.z;
                    for (int z = 0; z < length; z++, currentPosition.z += cellSize)
                    {
                        if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                        {
                            Vector2Int cellPosition = currentOccupiedEasyGridBuilderPro.GetActiveGridCellPosition(currentPosition);
                            currentOccupiedCellPositionList.Add(cellPosition);
                            if (!currentGridAllOccupiedCellPositionList.Contains(cellPosition))currentGridAllOccupiedCellPositionList.Add(cellPosition);
                        }
                    }
                }
            }
            else
            {
                for (int x = 0; x < width; x++, currentPosition.x += cellSize)
                {
                    currentPosition.y = basePosition.y;
                    for (int y = 0; y < length; y++, currentPosition.y += cellSize)
                    {
                        if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                        {
                            Vector2Int cellPosition = currentOccupiedEasyGridBuilderPro.GetActiveGridCellPosition(currentPosition);
                            currentOccupiedCellPositionList.Add(cellPosition);
                            if (!currentGridAllOccupiedCellPositionList.Contains(cellPosition))currentGridAllOccupiedCellPositionList.Add(cellPosition);
                        }
                    }
                }
            }
        }

        private void UpdateGridAreaDisablerData()
        {
            gridAreaData.affectAllVerticalGrids = affectAllVerticalGrids;

            gridAreaData.currentOccupiedEasyGridBuilderPro = currentOccupiedEasyGridBuilderPro;
            gridAreaData.previousOccupiedEasyGridBuilderPro = previousOccupiedEasyGridBuilderPro;

            gridAreaData.currentOccupiedGrid = currentOccupiedGrid;
            gridAreaData.previousOccupiedGrid = previousOccupiedGrid;

            gridAreaData.currentOccupiedCellPositionList = currentOccupiedCellPositionList ?? new List<Vector2Int>();
            gridAreaData.previousOccupiedCellPositionList = previousOccupiedCellPositionList ?? new List<Vector2Int>();
            
            gridAreaData.currentGridAllOccupiedCellPositionList = currentGridAllOccupiedCellPositionList ?? new List<Vector2Int>();
            gridAreaData.previousGridAllOccupiedCellPositionList = previousGridAllOccupiedCellPositionList ?? new List<Vector2Int>();  
        }

        public List<Vector2Int> GetCurrentOccupiedCellPositionList(out EasyGridBuilderPro easyGridBuilderPro)
        {
            easyGridBuilderPro = currentOccupiedEasyGridBuilderPro;
            return currentOccupiedCellPositionList;
        }

        public List<Vector2Int> GetPreviousOccupiedCellPositionList(out EasyGridBuilderPro easyGridBuilderPro)
        {
            easyGridBuilderPro = currentOccupiedEasyGridBuilderPro;
            return previousOccupiedCellPositionList;
        }

        public EasyGridBuilderPro GetOccupiedEasyGridBuilderPro()
        {
            return currentOccupiedEasyGridBuilderPro;
        }

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableGizmos) return;
            
            if (areaShape == AreaShape.Rectangle) DrawRectangleGizmos();
            else DrawCircleGizmos();
        }

        private void DrawRectangleGizmos()
        {
            Vector3 offset = gridAxis is GridAxis.XZ ? new Vector3((width * cellSize) * 0.5f, 0, (length * cellSize) * 0.5f) : new Vector3((width * cellSize) * 0.5f, (length * cellSize) * 0.5f, 0);
            Vector3 basePosition = transform.position - offset;

            if (enableSimplifiedGizmos)
            {
                // Draw simple rectangle outline
                Vector3 bottomLeft;
                Vector3 bottomRight;
                Vector3 topLeft;
                Vector3 topRight;

                if (gridAxis is GridAxis.XZ)
                {
                    bottomLeft = basePosition;
                    bottomRight = basePosition + new Vector3(width * cellSize, 0, 0);
                    topLeft = basePosition + new Vector3(0, 0, length * cellSize);
                    topRight = basePosition + new Vector3(width * cellSize, 0, length * cellSize);
                }
                else
                {
                    bottomLeft = basePosition;
                    bottomRight = basePosition + new Vector3(width * cellSize, 0, 0);
                    topLeft = basePosition + new Vector3(0, length * cellSize, 0);
                    topRight = basePosition + new Vector3(width * cellSize, length * cellSize, 0);
                }
                
                CustomGizmosUtilities.DrawAAPolyLine(bottomLeft, bottomRight, 2, gizmoColor);
                CustomGizmosUtilities.DrawAAPolyLine(bottomLeft, topLeft, 2, gizmoColor);
                CustomGizmosUtilities.DrawAAPolyLine(topRight, bottomRight, 2, gizmoColor);
                CustomGizmosUtilities.DrawAAPolyLine(topRight, topLeft, 2, gizmoColor);
            }
            else
            {
                // Draw detailed rectangle grid
                for (int x = 0; x <= width; x++)
                {
                    Vector3 startPos = basePosition + new Vector3(x * cellSize, 0, 0);
                    Vector3 endPos = gridAxis is GridAxis.XZ ? startPos + new Vector3(0, 0, length * cellSize) : startPos + new Vector3(0, length * cellSize, 0);
                    CustomGizmosUtilities.DrawAAPolyLine(startPos, endPos, 2, gizmoColor);
                }

                for (int z = 0; z <= length; z++)
                {
                    Vector3 startPos = gridAxis is GridAxis.XZ ? basePosition + new Vector3(0, 0, z * cellSize) : basePosition + new Vector3(0, z * cellSize, 0);
                    Vector3 endPos = startPos + new Vector3(width * cellSize, 0, 0);
                    CustomGizmosUtilities.DrawAAPolyLine(startPos, endPos, 2, gizmoColor);
                }
            }
        }

        private void DrawCircleGizmos()
        {
            if (enableSimplifiedGizmos)
            {
                // Draw simple circle
                Vector3 rotation = gridAxis is GridAxis.XZ ? new Vector3(90, 0, 0) : Vector3.zero;
                CustomGizmosUtilities.DrawAAPolyCircle(transform.position, Quaternion.Euler(rotation), radius, 36, false, 2, gizmoColor);
            }
            else
            {
                // Draw detailed circle grid
                Vector3 offset = gridAxis is GridAxis.XZ ? new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), 0, (length * cellSize) * 0.5f - (0.5f * cellSize)) :
                    new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), (length * cellSize) * 0.5f - (0.5f * cellSize), 0);
                Vector3 basePosition = transform.position - offset;
                float radiusSquared = radius * radius;
                HashSet<(Vector3, Vector3)> drawnLines = new HashSet<(Vector3, Vector3)>();

                Vector3 currentPosition = basePosition;

                if (gridAxis is GridAxis.XZ)
                {
                    for (int x = 0; x < width; x++, currentPosition.x += cellSize)
                    {
                        currentPosition.z = basePosition.z;
                        for (int z = 0; z < length; z++, currentPosition.z += cellSize)
                        {
                            if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                            {
                                DrawCellLines(currentPosition, drawnLines);
                            }
                        }
                    }
                }
                else
                {
                    for (int x = 0; x < width; x++, currentPosition.x += cellSize)
                    {
                        currentPosition.y = basePosition.y;
                        for (int y = 0; y < length; y++, currentPosition.y += cellSize)
                        {
                            if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                            {
                                DrawCellLines(currentPosition, drawnLines);
                            }
                        }
                    }
                }
            }
        }

        private void DrawCellLines(Vector3 position, HashSet<(Vector3, Vector3)> drawnLines)
        {
            Vector3 halfCellSize = gridAxis is GridAxis.XZ ? new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f) : new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0);

            Vector3 bottomLeft = position - halfCellSize;
            Vector3 bottomRight = gridAxis is GridAxis.XZ ? position + new Vector3(halfCellSize.x, 0, -halfCellSize.z) : position + new Vector3(halfCellSize.x, -halfCellSize.y, 0);
            Vector3 topLeft = gridAxis is GridAxis.XZ ? position + new Vector3(-halfCellSize.x, 0, halfCellSize.z) : position + new Vector3(-halfCellSize.x, halfCellSize.y, 0);
            Vector3 topRight = position + halfCellSize;

            DrawLineIfNotDrawn(bottomLeft, bottomRight, drawnLines);
            DrawLineIfNotDrawn(topLeft, topRight, drawnLines);
            DrawLineIfNotDrawn(bottomLeft, topLeft, drawnLines);
            DrawLineIfNotDrawn(bottomRight, topRight, drawnLines);
        }

        private void DrawLineIfNotDrawn(Vector3 start, Vector3 end, HashSet<(Vector3, Vector3)> drawnLines)
        {
            var lineKey = (start, end);
            var reverseLineKey = (end, start);

            if (!drawnLines.Contains(lineKey) && !drawnLines.Contains(reverseLineKey))
            {
                CustomGizmosUtilities.DrawAAPolyLine(start, end, 2, gizmoColor);
                drawnLines.Add(lineKey);
                drawnLines.Add(reverseLineKey);
            }
        }
        #endif
    }
}