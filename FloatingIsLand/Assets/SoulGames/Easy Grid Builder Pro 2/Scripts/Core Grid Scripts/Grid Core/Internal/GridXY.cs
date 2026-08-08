using UnityEngine;
using TMPro;
using UnityEngine.UI;
using SoulGames.Utilities;
using System.Collections.Generic;

namespace SoulGames.EasyGridBuilderPro
{
    /// <summary>
    /// A non-MonoBehaviour class that is responsible for creating, managing, and providing functionalities to interact with a grid.
    /// </summary>
    public class GridXY : Grid
    {
        /// <summary>
        /// Event triggered when a grid cell object's value changes.
        /// </summary>
        public event OnGridCellValueChangedDelegate OnGridCellValueChanged;
        public delegate void OnGridCellValueChangedDelegate(int x, int y);

        private int gridWidth; // Width of the grid
        private int gridLength; // Length of the grid
        private float cellSize; // Size of each cell in the grid
        private Vector3 originPosition; // Origin position of the GridXZ
        private Transform gridObjectTransform; // Parent transform of the grid
        private int verticalGridIndex;
        private GridDataHandler gridDataHandler;

        private CellPositionXY[,] gridArray; // Multidimensional array representing the grid
        private Transform runtimeCanvasGrid; // Runtime canvas grid transform
        private Transform runtimeObjectGrid; // Runtime object grid transform
        private Transform runtimeTextGrid; // Runtime text grid transform
        private Transform runtimeProceduralGrid; // Runtime node grid transform

        private Image runtimeCanvasGridSharedCellImage;

        private Material runtimeObjectGridMaterial;
        private Material runtimeObjectGridSharedMaterial;
        private Texture2D runtimeObjectGridGeneratedTexture;
        private Texture2D runtimeObjectGridGeneratedHeatMapTexture;
        private const float PLANE_OBJECT_TRUE_SCALE = 10;
        private int CELL_TEXTURE = Shader.PropertyToID("_Cell_Texture");
        private int CELL_SIZE = Shader.PropertyToID("_Cell_Size");
        private int CELL_COLOR_OVERRIDE = Shader.PropertyToID("_Cell_Color_Override");
        private int HDR_OVERRIDE = Shader.PropertyToID("_HDR_Override");
        private const string USE_ALPHA_MASK = "_USE_ALPHA_MASK";
        private int ALPHA_MASK_TEXTURE = Shader.PropertyToID("_Alpha_Mask_Texture");
        private int ALPHA_MASK_SIZE = Shader.PropertyToID("_Alpha_Mask_Size");
        private int ALPHA_MASK_OFFSET = Shader.PropertyToID("_Alpha_Mask_Offset");
        private const string USE_SCROLLING_NOISE = "_USE_SCROLLING_NOISE";
        private int NOISE_TEXTURE = Shader.PropertyToID("_Noise_Texture");
        private int NOISE_TEXTURE_TILING = Shader.PropertyToID("_Texture_Tiling");
        private int NOISE_TEXTURE_SCROLLING = Shader.PropertyToID("_Texture_Scroll");
        private int GENERATED_TEXTURE = Shader.PropertyToID("_Generated_Texture");
        private const string ENABLE_HEATMAP = "_ENABLE_HEATMAP";
        private int GENERATED_HEATMAP_TEXTURE = Shader.PropertyToID("_Generated_HeatMap_Texture");
        private int HEATMAP_HDR_OVERRIDE = Shader.PropertyToID("_HeatMap_HDR_Override"); 

        private Transform[,] runtimeTextGridTextObjectArray;
        private TextMeshPro[,] runtimeTextGridTextMeshProArray;
        private const int TMP_Text_Multiplier_Size = 4;

        private List<Transform> nodesList = new List<Transform>();

        ///-------------------------------------------------------------------------------///
        /// GRID INITIALIZE FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///

        #region Grid Initialization Functions Start:
        /// <summary>
        /// Constructor for creating a GridXZ object.
        /// </summary>
        /// <param name="width">Width of the grid.</param>
        /// <param name="length">Length of the grid.</param>
        /// <param name="cellSize">Size of each grid cell.</param>
        /// <param name="originPosition">World position for the origin of the grid.</param>
        /// <param name="gridObjectTransform">Transform of the actual grid object.</param>
        public GridXY(int width, int length, float cellSize, Vector3 originPosition, Transform gridObjectTransform, int verticalGridIndex, GridDataHandler gridDataHandler)
        {
            this.gridWidth = width;
            this.gridLength = length;
            this.cellSize = cellSize;
            this.originPosition = originPosition;
            this.gridObjectTransform = gridObjectTransform;
            this.verticalGridIndex = verticalGridIndex;
            this.gridDataHandler = gridDataHandler;

            // Initialize the grid array with the specified dimensions
            gridArray = new CellPositionXY[width, length];

            // Set up the grid
            InitializeGrid();
        }

        /// Initializes the grid by creating and assigning CellObjectXY instances to each cell.
        private void InitializeGrid()
        {
            // Assuming 'width' and 'length' are the dimensions of the grid.
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridLength; y++)
                {
                    gridArray[x, y] = new CellPositionXY(x, y);
                }
            }
        }
        #endregion Grid Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// RUNTIME GRID VISUAL FUNCTIONS                                                 ///
        ///-------------------------------------------------------------------------------///
        
        #region Runtime Canvas Grid Functions Start:
        public void InitializeRuntimeCanvasGrid(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex, float verticalGridHeight, GridMode activeGridMode, GridXY activeGrid, GridOrigin gridOriginType)
        {
            ClearRuntimeCanvasGrid();

            runtimeCanvasGrid = GameObject.Instantiate(canvasGridSettings.canvasGridPrefab.transform, Vector3.zero, Quaternion.identity);
            runtimeCanvasGrid.SetParent(gridObjectTransform, false);

            runtimeCanvasGrid.eulerAngles = new Vector3(0, 0, 0);
            UpdateRuntimeCanvasGridPosition(verticalGridHeight, thisGridVerticalIndex, gridOriginType);
            UpdateRuntimeCanvasGridScale();

            Transform gridTextureTransform = runtimeCanvasGrid.GetChild(0);
            runtimeCanvasGridSharedCellImage = gridTextureTransform.GetComponent<Image>();
            runtimeCanvasGridSharedCellImage.type = Image.Type.Tiled;
            runtimeCanvasGridSharedCellImage.pixelsPerUnitMultiplier = 100 / cellSize;
            
            UpdateRuntimeCanvasGrid(canvasGridSettings, thisGridVerticalIndex, activeGridMode, activeGrid);
        }

        private void UpdateRuntimeCanvasGridScale()
        {
            Transform gridTextureTransform = runtimeCanvasGrid.GetChild(0);
            Vector2 gridDimensions = new Vector2(gridWidth * cellSize, gridLength * cellSize);
            runtimeCanvasGrid.GetComponent<RectTransform>().sizeDelta = gridDimensions;
            gridTextureTransform.GetComponent<RectTransform>().sizeDelta = gridDimensions;
        }

        public void UpdateRuntimeCanvasGridPosition(float verticalGridHeight, int thisGridVerticalIndex, GridOrigin gridOriginType)
        {
            float offsetX = default;
            float offsetY = default;

            if (gridOriginType == GridOrigin.Center)
            {
                offsetX = (cellSize * gridWidth / 2) * -1;
                offsetY = (cellSize * gridLength / 2) * -1;
            }

            runtimeCanvasGrid.localPosition = new Vector3(offsetX, offsetY, -verticalGridHeight * thisGridVerticalIndex);
        }

        public void UpdateRuntimeCanvasGrid(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (runtimeCanvasGrid == null) return;

            runtimeCanvasGridSharedCellImage.sprite = canvasGridSettings.cellImageSprite;

            if (CanDisplayRuntimeCanvasGrid(canvasGridSettings, thisGridVerticalIndex, activeGridMode, activeGrid))
            {
                SetRuntimeCanvasGridTransitionGridColor(canvasGridSettings.gridShowColor, canvasGridSettings.colorTransitionSpeed);
            }
            else
            {
                SetRuntimeCanvasGridTransitionGridColor(canvasGridSettings.gridHideColor, canvasGridSettings.colorTransitionSpeed);
            }
        }

        private bool CanDisplayRuntimeCanvasGrid(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (canvasGridSettings.alwaysLockAtFirstGrid && thisGridVerticalIndex != 0) return false;
            if (canvasGridSettings.onlyShowActiveVerticalGrid && activeGrid != this) return false;
            
            switch (activeGridMode)
            {
                case GridMode.None:         return canvasGridSettings.displayOnDefaultMode;
                case GridMode.BuildMode:    return canvasGridSettings.displayOnBuildMode;
                case GridMode.DestroyMode:  return canvasGridSettings.displayOnDestroyMode;
                case GridMode.SelectMode:   return canvasGridSettings.displayOnSelectMode;
                case GridMode.MoveMode:     return canvasGridSettings.displayOnMoveMode;
                default:                    return false;
            }
        }

        private void SetRuntimeCanvasGridTransitionGridColor(Color targetColor, float colorTransitionSpeed)
        {
            runtimeCanvasGridSharedCellImage.color = Color.Lerp(runtimeCanvasGridSharedCellImage.color, targetColor, colorTransitionSpeed * Time.deltaTime);
        }

        public void ClearRuntimeCanvasGrid() 
        {
            if (runtimeCanvasGrid != null)
            {
                UnityEngine.Object.Destroy(runtimeCanvasGrid.gameObject);
                runtimeCanvasGrid = null;
            }
        }

        public bool TryGetRuntimeCanvasGrid(out Transform runtimeCanvasGrid)
        {
            runtimeCanvasGrid = this.runtimeCanvasGrid;
            return runtimeCanvasGrid != null;
        }
        #endregion Runtime Canvas Grid Functions End:

        #region Runtime Object Grid Functions Start:
        public void InitializeRuntimeObjectGrid(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex, float verticalGridHeight, GridMode activeGridMode, GridXY activeGrid, BuildableObjectSO activeBuildableObjectSO, bool isRuntimeObjectGridHeatMapModeActivated, GridOrigin gridOriginType)
        {
            ClearRuntimeObjectGrid();

            runtimeObjectGrid = GameObject.Instantiate(objectGridSettings.objectGridPrefab, Vector3.zero, Quaternion.identity);
            runtimeObjectGrid.SetParent(gridObjectTransform, false);

            runtimeObjectGridMaterial = runtimeObjectGrid.GetComponent<Renderer>().sharedMaterial;
            runtimeObjectGridSharedMaterial = new Material(runtimeObjectGridMaterial);
            runtimeObjectGrid.GetComponent<Renderer>().sharedMaterial = runtimeObjectGridSharedMaterial;

            runtimeObjectGridSharedMaterial.SetFloat(CELL_SIZE, cellSize);
            runtimeObjectGridSharedMaterial.DisableKeyword(USE_ALPHA_MASK);

            UpdateRuntimeObjectGridScale();
            UpdateRuntimeObjectGridPosition(verticalGridHeight, thisGridVerticalIndex, gridOriginType);
            runtimeObjectGrid.rotation = Quaternion.Euler(new Vector3(-90, 0, 0));

            InitializeRuntimeObjectGridGeneratedTexture();
            UpdateRuntimeObjectGrid(objectGridSettings, thisGridVerticalIndex, activeGridMode, activeGrid, activeBuildableObjectSO, isRuntimeObjectGridHeatMapModeActivated);
        }

        private void UpdateRuntimeObjectGridScale()
        {
            runtimeObjectGrid.localScale = new Vector3((gridWidth / PLANE_OBJECT_TRUE_SCALE) * cellSize, 1, (gridLength / PLANE_OBJECT_TRUE_SCALE) * cellSize);
        }

        public void UpdateRuntimeObjectGridPosition(float verticalGridHeight, int thisGridVerticalIndex, GridOrigin gridOriginType)
        {
            float offsetX = default;
            float offsetY = default;

            if (gridOriginType == GridOrigin.Default)
            {
                offsetX = (cellSize * gridWidth / 2) * 1;
                offsetY = (cellSize * gridLength / 2) * 1;
            }
            
            runtimeObjectGrid.localPosition = new Vector3(offsetX, offsetY, -verticalGridHeight * thisGridVerticalIndex);
        }

        private void InitializeRuntimeObjectGridGeneratedTexture()
        {
            runtimeObjectGridGeneratedTexture = new Texture2D(gridWidth, gridLength, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            runtimeObjectGridSharedMaterial.SetTexture(GENERATED_TEXTURE, runtimeObjectGridGeneratedTexture);
            
            UpdateGeneratedTexture();
        }
        
        public void UpdateGeneratedTexture()
        {
            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridLength; y++)
                {
                    Color color = new Color(255,255,255,255);
                    runtimeObjectGridGeneratedTexture.SetPixel(x, y, color);
                }
            }
            runtimeObjectGridGeneratedTexture.Apply();
        }

        public void UpdateRuntimeObjectGrid(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid, BuildableObjectSO activeBuildableObjectSO, bool isRuntimeObjectGridHeatMapModeActivated)
        {
            if (runtimeObjectGrid == null) return;

            runtimeObjectGridSharedMaterial.SetTexture(CELL_TEXTURE, objectGridSettings.cellImageTexture);

            if (CanDisplayRuntimeObjectGrid(objectGridSettings, thisGridVerticalIndex, activeGridMode, activeGrid))
            {
                SetRuntimeObjectGridTransitionGridColor(objectGridSettings.gridShowColor, objectGridSettings.gridShowColorHDR, objectGridSettings.colorTransitionSpeed);
                if (runtimeObjectGrid.gameObject.activeSelf != true) runtimeObjectGrid.gameObject.SetActive(true);
            }
            else
            {
                SetRuntimeObjectGridTransitionGridColor(objectGridSettings.gridHideColor, objectGridSettings.gridHideColorHDR, objectGridSettings.colorTransitionSpeed);
                if (isRuntimeObjectGridHeatMapModeActivated) runtimeObjectGrid.gameObject.SetActive(false);
            }

            UpdateRuntimeObjectGridScrollingNoise(objectGridSettings);
            UpdateRuntimeObjectGridVisualsAlphaMask(objectGridSettings, activeGridMode, activeBuildableObjectSO);
        }

        private bool CanDisplayRuntimeObjectGrid(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (objectGridSettings.alwaysLockAtFirstGrid && thisGridVerticalIndex != 0) return false;
            if (objectGridSettings.onlyShowActiveVerticalGrid && activeGrid != this) return false;
            
            switch (activeGridMode)
            {
                case GridMode.None:         return objectGridSettings.displayOnDefaultMode;
                case GridMode.BuildMode:    return objectGridSettings.displayOnBuildMode;
                case GridMode.DestroyMode:  return objectGridSettings.displayOnDestroyMode;
                case GridMode.SelectMode:   return objectGridSettings.displayOnSelectMode;
                case GridMode.MoveMode:     return objectGridSettings.displayOnMoveMode;
                default:                    return false;
            }
        }

        private void SetRuntimeObjectGridTransitionGridColor(Color targetColor, Color targetColorHDR, float colorTransitionSpeed)
        {
            runtimeObjectGridSharedMaterial.SetColor(CELL_COLOR_OVERRIDE, Color.Lerp(runtimeObjectGridSharedMaterial.GetColor(CELL_COLOR_OVERRIDE), targetColor, colorTransitionSpeed * Time.deltaTime));
            runtimeObjectGridSharedMaterial.SetColor(HDR_OVERRIDE, Color.Lerp(runtimeObjectGridSharedMaterial.GetColor(HDR_OVERRIDE), targetColorHDR, colorTransitionSpeed * Time.deltaTime));
        }

        public void UpdateRuntimeObjectGridScrollingNoise(EasyGridBuilderPro.ObjectGridSettings objectGridSettings)
        {
            if (objectGridSettings.useScrollingNoise)
            {
                ActivateRuntimeObjectGridScrollingNoise();

                runtimeObjectGridSharedMaterial.SetTexture(NOISE_TEXTURE, objectGridSettings.noiseTexture);
                runtimeObjectGridSharedMaterial.SetVector(NOISE_TEXTURE_TILING, objectGridSettings.textureTiling);
                runtimeObjectGridSharedMaterial.SetVector(NOISE_TEXTURE_SCROLLING, objectGridSettings.textureScrolling);
            }
            else DeactivateRuntimeObjectGridScrollingNoise();
        }

        public void ActivateRuntimeObjectGridScrollingNoise()
        {
            if (!runtimeObjectGridSharedMaterial.IsKeywordEnabled(USE_SCROLLING_NOISE)) runtimeObjectGridSharedMaterial.EnableKeyword(USE_SCROLLING_NOISE);
        }

        public void DeactivateRuntimeObjectGridScrollingNoise()
        {
            if (runtimeObjectGridSharedMaterial.IsKeywordEnabled(USE_SCROLLING_NOISE)) runtimeObjectGridSharedMaterial.DisableKeyword(USE_SCROLLING_NOISE);
        }

        public void UpdateRuntimeObjectGridVisualsAlphaMask(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, GridMode activeGridMode, BuildableObjectSO activeBuildableObjectSO)
        {
            if (objectGridSettings.useAlphaMask)
            {
                if (CanDisplayRuntimeObjectGridAlphaMask(objectGridSettings, activeGridMode))
                {
                    ActivateRuntimeObjectGridAlphaMask();

                    float maskSize = CalculateAdjustedAlphaMaskSize(objectGridSettings, objectGridSettings.alphaMaskSize, activeBuildableObjectSO, out float minScale);
                    Vector4 maskOffset = GetAlphaMaskOffset(objectGridSettings, maskSize, minScale, activeBuildableObjectSO);

                    runtimeObjectGridSharedMaterial.SetTexture(ALPHA_MASK_TEXTURE, objectGridSettings.alphaMaskSprite);
                    runtimeObjectGridSharedMaterial.SetVector(ALPHA_MASK_SIZE, new Vector4(maskSize, maskSize, 0, 0));
                    runtimeObjectGridSharedMaterial.SetVector(ALPHA_MASK_OFFSET, maskOffset);
                }
                else DeactivateRuntimeObjectGridAlphaMask();
            }
            else DeactivateRuntimeObjectGridAlphaMask();
        }

        private bool CanDisplayRuntimeObjectGridAlphaMask(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, GridMode activeGridMode)
        {
            switch (activeGridMode)
            {
                case GridMode.None: return objectGridSettings.activateOnDefaultMode;
                case GridMode.BuildMode: return objectGridSettings.activateOnBuildMode;
                case GridMode.DestroyMode: return objectGridSettings.activateOnDestroyMode;
                case GridMode.SelectMode: return objectGridSettings.activateOnSelectMode;
                case GridMode.MoveMode: return objectGridSettings.activateOnMoveMode;
                default: return false;
            }
        }

        public void ActivateRuntimeObjectGridAlphaMask()
        {
            if (!runtimeObjectGridSharedMaterial.IsKeywordEnabled(USE_ALPHA_MASK)) runtimeObjectGridSharedMaterial.EnableKeyword(USE_ALPHA_MASK);
        }

        public void DeactivateRuntimeObjectGridAlphaMask()
        {
            if (runtimeObjectGridSharedMaterial.IsKeywordEnabled(USE_ALPHA_MASK)) runtimeObjectGridSharedMaterial.DisableKeyword(USE_ALPHA_MASK);
        }

        private float CalculateAdjustedAlphaMaskSize(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, float alphaMaskSize, BuildableObjectSO activeBuildableObjectSO, out float minScale)
        {
            float scaleX = runtimeObjectGrid.localScale.x;
            float scaleZ = runtimeObjectGrid.localScale.z;
            minScale = Mathf.Min(scaleX, scaleZ);
            
            if (objectGridSettings.alphaMaskFollowGhostObject && objectGridSettings.alphaMaskAddGhostObjectScale && activeBuildableObjectSO != null)
            {
                switch (activeBuildableObjectSO)
                {
                    case BuildableGridObjectSO: 
                        if (GridManager.Instance.TryGetBuildableGridObjectGhost(out BuildableGridObjectGhost buildableGridObjectGhost))
                        {
                            if (buildableGridObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableGridObjectGhost.TryGetBoxPlacementHolderObject(out Transform _)) 
                            {
                                Vector3 gridObjectScale;
                                if (activeBuildableObjectSO.useObjectGridCustomAlphaMaskScale) gridObjectScale = activeBuildableObjectSO.objectGridCustomAlphaMaskScale;
                                else gridObjectScale = buildableGridObjectGhost.GetObjectScaleForObjectGridAlphaMask();

                                float highestScale = Mathf.Max(gridObjectScale.x, gridObjectScale.z);
                                return (alphaMaskSize + highestScale) / minScale;
                            }
                        }
                    break;
                    case BuildableEdgeObjectSO: 
                    case BuildableCornerObjectSO:
                        if (GridManager.Instance.TryGetBuildableCornerObjectGhost(out BuildableCornerObjectGhost buildableCornerObjectGhost))
                        {
                            if (buildableCornerObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableCornerObjectGhost.TryGetBoxPlacementHolderObject(out Transform _)) 
                            {
                                Vector3 gridObjectScale;
                                if (activeBuildableObjectSO.useObjectGridCustomAlphaMaskScale) gridObjectScale = activeBuildableObjectSO.objectGridCustomAlphaMaskScale;
                                else gridObjectScale = buildableCornerObjectGhost.GetObjectScaleForObjectGridAlphaMask();

                                float highestScale = Mathf.Max(gridObjectScale.x, gridObjectScale.z);
                                return (alphaMaskSize + highestScale) / minScale;
                            }
                        }
                    break;
                    case BuildableFreeObjectSO:
                        if (GridManager.Instance.TryGetBuildableFreeObjectGhost(out BuildableFreeObjectGhost buildableFreeObjectGhost))
                        {
                            if (buildableFreeObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableFreeObjectGhost.TryGetBoxPlacementHolderObject(out Transform _)) 
                            {
                                Vector3 gridObjectScale;
                                if (activeBuildableObjectSO.useObjectGridCustomAlphaMaskScale) gridObjectScale = activeBuildableObjectSO.objectGridCustomAlphaMaskScale;
                                else gridObjectScale = buildableFreeObjectGhost.GetObjectScaleForObjectGridAlphaMask();

                                float highestScale = Mathf.Max(gridObjectScale.x, gridObjectScale.z);
                                return (alphaMaskSize + highestScale) / minScale;
                            }
                        }
                    break;
                }
            }
            return alphaMaskSize / minScale;
        }

        private Vector4 GetAlphaMaskOffset(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, float maskSize, float minScale, BuildableObjectSO activeBuildableObjectSO)
        {
            if (objectGridSettings.alphaMaskFollowCursor)
            {
                return CalculateAlphaMaskFollowCursorOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO);
            }
            
            if (objectGridSettings.alphaMaskFollowGhostObject)
            {
                switch (activeBuildableObjectSO)
                {
                    case BuildableGridObjectSO: 
                        if (GridManager.Instance.TryGetBuildableGridObjectGhost(out BuildableGridObjectGhost buildableGridObjectGhost))
                        {
                            if (buildableGridObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableGridObjectGhost.TryGetBoxPlacementHolderObject(out Transform _))
                            {
                                return CalculateAlphaMaskFollowGhostObjectOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO, buildableGridObjectGhost, null, null);
                            }
                        }
                    break;
                    case BuildableEdgeObjectSO: 
                    case BuildableCornerObjectSO: 
                        if (GridManager.Instance.TryGetBuildableCornerObjectGhost(out BuildableCornerObjectGhost buildableCornerObjectGhost))
                        {
                            if (buildableCornerObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableCornerObjectGhost.TryGetBoxPlacementHolderObject(out Transform _))
                            {
                                return CalculateAlphaMaskFollowGhostObjectOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO, null, buildableCornerObjectGhost, null);
                            }
                        }
                    break;
                    case BuildableFreeObjectSO: 
                        if (GridManager.Instance.TryGetBuildableFreeObjectGhost(out BuildableFreeObjectGhost buildableFreeObjectGhost))
                        {
                            if (buildableFreeObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableFreeObjectGhost.TryGetBoxPlacementHolderObject(out Transform _))
                            {
                                return CalculateAlphaMaskFollowGhostObjectOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO, null, null, buildableFreeObjectGhost);
                            }
                        }
                    break;
                }
            }
            return new Vector4(objectGridSettings.alphaMaskWorldPosition.x, objectGridSettings.alphaMaskWorldPosition.y, 0, 0) / maskSize / runtimeObjectGrid.localScale.x;
        }

        private Vector4 CalculateAlphaMaskFollowCursorOffset(float maskSize, EasyGridBuilderPro.ObjectGridSettings objectGridSettings, float minScale, BuildableObjectSO activeBuildableObjectSO)
        {
            if (objectGridSettings.alphaMaskFollowGhostObject)
            {
                switch (activeBuildableObjectSO)
                {
                    case BuildableGridObjectSO:
                        if (GridManager.Instance.TryGetBuildableGridObjectGhost(out BuildableGridObjectGhost buildableGridObjectGhost))
                        {
                            if (buildableGridObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableGridObjectGhost.TryGetBoxPlacementHolderObject(out Transform _) && activeBuildableObjectSO != null)
                            {
                                return CalculateAlphaMaskFollowGhostObjectOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO, buildableGridObjectGhost, null, null);
                            }
                        }
                    break;
                    case BuildableEdgeObjectSO:
                    case BuildableCornerObjectSO:
                        if (GridManager.Instance.TryGetBuildableCornerObjectGhost(out BuildableCornerObjectGhost buildableCornerObjectGhost))
                        {
                            if (buildableCornerObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableCornerObjectGhost.TryGetBoxPlacementHolderObject(out Transform _) && activeBuildableObjectSO != null)
                            {
                                return CalculateAlphaMaskFollowGhostObjectOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO, null, buildableCornerObjectGhost, null);
                            }
                        }
                    break;
                    case BuildableFreeObjectSO:
                        if (GridManager.Instance.TryGetBuildableFreeObjectGhost(out BuildableFreeObjectGhost buildableFreeObjectGhost))
                        {
                            if (buildableFreeObjectGhost.TryGetGhostObjectVisual(out Transform _) || buildableFreeObjectGhost.TryGetBoxPlacementHolderObject(out Transform _) && activeBuildableObjectSO != null)
                            {
                                return CalculateAlphaMaskFollowGhostObjectOffset(maskSize, objectGridSettings, minScale, activeBuildableObjectSO, null, null, buildableFreeObjectGhost);
                            }
                        }
                    break;
                } 
                return CalculateRegularAlphaMaskFollowCursorOffset(maskSize, objectGridSettings, minScale);
            }

            return CalculateRegularAlphaMaskFollowCursorOffset(maskSize, objectGridSettings, minScale);
        }

        private Vector4 CalculateRegularAlphaMaskFollowCursorOffset(float maskSize, EasyGridBuilderPro.ObjectGridSettings objectGridSettings, float minScale)
        {
            Vector4 mousePosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(objectGridSettings.customSurfaceLayerMask, GridManager.Instance.GetGridSystemLayerMask(), Vector3.forward * 9999, out _, out _);

            float scaleFactor = minScale / objectGridSettings.alphaMaskSize;

            float offsetX = ((mousePosition.x - gridObjectTransform.position.x) / (runtimeObjectGrid.localScale.x * maskSize)) * scaleFactor;
            float offsetZ = ((mousePosition.y - gridObjectTransform.position.y) / (runtimeObjectGrid.localScale.z * maskSize)) * scaleFactor;
            
            offsetX = offsetX * maskSize;
            offsetZ = offsetZ * maskSize;

            return new Vector4(offsetX, offsetZ, 0, 0);
        }

        private Vector4 CalculateAlphaMaskFollowGhostObjectOffset(float maskSize, EasyGridBuilderPro.ObjectGridSettings objectGridSettings, float minScale, BuildableObjectSO activeBuildableObjectSO, 
            BuildableGridObjectGhost buildableGridObjectGhost, BuildableCornerObjectGhost buildableCornerObjectGhost, BuildableFreeObjectGhost buildableFreeObjectGhost)
        {
            Vector4 mousePosition = default;
            switch (activeBuildableObjectSO)
            {
                case BuildableGridObjectSO: mousePosition = buildableGridObjectGhost.GetObjectPositionForObjectGridAlphaMask(); break;         
                case BuildableEdgeObjectSO: 
                case BuildableCornerObjectSO: 
                    mousePosition = buildableCornerObjectGhost.GetObjectPositionForObjectGridAlphaMask(); break;         
                case BuildableFreeObjectSO: mousePosition = buildableFreeObjectGhost.GetObjectPositionForObjectGridAlphaMask(); break;         
            }
            float scaleFactor;

            if (objectGridSettings.alphaMaskAddGhostObjectScale)
            {
                Vector3 gridObjectScale = default;
                if (activeBuildableObjectSO.useObjectGridCustomAlphaMaskScale) gridObjectScale = activeBuildableObjectSO.objectGridCustomAlphaMaskScale;
                else 
                {
                    switch (activeBuildableObjectSO)
                    {
                        case BuildableGridObjectSO: gridObjectScale = buildableGridObjectGhost.GetObjectScaleForObjectGridAlphaMask(); break;         
                        case BuildableEdgeObjectSO: 
                        case BuildableCornerObjectSO: 
                            gridObjectScale = buildableCornerObjectGhost.GetObjectScaleForObjectGridAlphaMask(); break;         
                        case BuildableFreeObjectSO: gridObjectScale = buildableFreeObjectGhost.GetObjectScaleForObjectGridAlphaMask(); break;         
                    }
                }

                float highestScale = Mathf.Max(gridObjectScale.x, gridObjectScale.z);
                scaleFactor = minScale / (objectGridSettings.alphaMaskSize + highestScale);
            }
            else scaleFactor = minScale / objectGridSettings.alphaMaskSize;
            // float offsetX = ((mousePosition.x - gridObjectTransform.position.x / maskSize) / runtimeObjectGrid.localScale.x) * scaleFactor;
            // float offsetZ = ((mousePosition.z - gridObjectTransform.position.z / maskSize) / runtimeObjectGrid.localScale.z) * scaleFactor;

            float offsetX = ((mousePosition.x - gridObjectTransform.position.x) / (runtimeObjectGrid.localScale.x * maskSize)) * scaleFactor;
            float offsetZ = ((mousePosition.y - gridObjectTransform.position.y) / (runtimeObjectGrid.localScale.z * maskSize)) * scaleFactor;
            
            offsetX = offsetX * maskSize;
            offsetZ = offsetZ * maskSize;

            return new Vector4(offsetX, offsetZ, 0, 0);
        }

        public void ClearRuntimeObjectGrid() 
        {
            if (runtimeObjectGrid != null)
            {
                UnityEngine.Object.Destroy(runtimeObjectGrid.gameObject);
                runtimeObjectGrid = null;
            }
        }

        public bool TryGetRuntimeObjectGrid(out Transform runtimeObjectGrid)
        {
            runtimeObjectGrid = this.runtimeObjectGrid;
            return runtimeObjectGrid != null;
        }
        #endregion Runtime Object Grid Functions End:

        #region Runtime Text Grid Functions Start:
        public void InitializeRuntimeTextGrid(EasyGridBuilderPro.RuntimeTextGridSettings runtimeTextGridSettings, int thisGridVerticalIndex, float verticalGridHeight, GridMode activeGridMode, GridXY activeGrid)
        {
            ClearRuntimeTextGrid();

            if (runtimeTextGridSettings.onlySpawnFirstVerticalGrid && thisGridVerticalIndex != 0) return;

            runtimeTextGrid = new GameObject("Text Grid").transform;
            runtimeTextGrid.parent = gridObjectTransform;
            runtimeTextGrid.localPosition = new Vector3(0, 0, -verticalGridHeight * thisGridVerticalIndex);
            runtimeTextGridTextObjectArray = new Transform[gridWidth, gridLength];
            runtimeTextGridTextMeshProArray = new TextMeshPro[gridWidth, gridLength];

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridLength; y++)
                {
                    runtimeTextGridTextObjectArray[x, y] = InstantiateTextObjectAtPosition(runtimeTextGridSettings, x, y);
                }
            }

            SubscribeToGridCellValueChanged();
            UpdateRuntimeTextGrid(runtimeTextGridSettings, thisGridVerticalIndex, activeGridMode, activeGrid);
        }

        private Transform InstantiateTextObjectAtPosition(EasyGridBuilderPro.RuntimeTextGridSettings runtimeTextGridSettings, int x, int y)
        {
            Transform textObject = GameObject.Instantiate(runtimeTextGridSettings.textPrefab, Vector3.zero, Quaternion.identity);
            float calculatedTextSize = (cellSize / 100) * runtimeTextGridSettings.textSizePercentage;

            if (textObject.GetChild(0).TryGetComponent<TextMeshPro>(out TextMeshPro textMeshPro))
            {
                runtimeTextGridTextMeshProArray[x, y] = textMeshPro;
                textMeshPro.fontSize = calculatedTextSize * TMP_Text_Multiplier_Size;
                textMeshPro.color = runtimeTextGridSettings.textColorOverride;

                if (runtimeTextGridSettings.displayCellPositionText)
                {
                    if (textMeshPro.text != "") textMeshPro.text += "\n" + gridArray[x, y].ToString();
                    else textMeshPro.text = gridArray[x, y].ToString();
                }
            }
            
            textObject.position = new Vector3(cellSize, cellSize, 0) / 2 + runtimeTextGridSettings.textGridCustomOffset + GetCellWorldPosition(x, y);
            textObject.rotation = Quaternion.Euler(new Vector3(-90, 0, 0));
            textObject.parent = runtimeTextGrid;
            return textObject;
        }

        public void UpdateRuntimeTextGrid(EasyGridBuilderPro.RuntimeTextGridSettings runtimeTextGridSettings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (runtimeTextGrid == null) return;

            if (CanDisplayRuntimeTextGrid(runtimeTextGridSettings, thisGridVerticalIndex, activeGridMode, activeGrid))
            {
                SetRuntimeTextGridActiveSelf(true);
            }
            else
            {
                SetRuntimeTextGridActiveSelf(false);
            }
        }

        private bool CanDisplayRuntimeTextGrid(EasyGridBuilderPro.RuntimeTextGridSettings runtimeTextGridSettings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (runtimeTextGridSettings.alwaysLockAtFirstGrid && thisGridVerticalIndex != 0) return false;
            if (runtimeTextGridSettings.onlyShowActiveVerticalGrid && activeGrid != this) return false;
            
            switch (activeGridMode)
            {
                case GridMode.None:         return runtimeTextGridSettings.displayOnDefaultMode;
                case GridMode.BuildMode:    return runtimeTextGridSettings.displayOnBuildMode;
                case GridMode.DestroyMode:  return runtimeTextGridSettings.displayOnDestroyMode;
                case GridMode.SelectMode:   return runtimeTextGridSettings.displayOnSelectMode;
                case GridMode.MoveMode:     return runtimeTextGridSettings.displayOnMoveMode;
                default:                    return false;
            }
        }

        private void SetRuntimeTextGridActiveSelf(bool setActive)
        {
            runtimeTextGrid.gameObject.SetActive(setActive);
        }

        private void SubscribeToGridCellValueChanged()
        {
            OnGridCellValueChanged += (int x, int y) =>
            {
                runtimeTextGridTextMeshProArray[x, y].text = gridArray[x, y].ToString();
            };
        }

        public void ClearRuntimeTextGrid()
        {
            if (runtimeTextGrid != null)
            {
                UnityEngine.Object.Destroy(runtimeTextGrid.gameObject);
                runtimeTextGrid = null;
            }
        }

        public bool TryGetRuntimeTextGrid(out Transform runtimeTextGrid)
        {
            runtimeTextGrid = this.runtimeTextGrid;
            return runtimeTextGrid != null;
        }
        #endregion Runtime Text Grid Functions End:

        #region Runtime Procedural Grid Functions Start:
        public void InitializeRuntimeProceduralGrid(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int thisGridVerticalIndex, float verticalGridHeight, GridMode activeGridMode, GridXY activeGrid)
        {
            ClearRuntimeProceduralGrid();

            if (settings.onlySpawnFirstVerticalGrid && thisGridVerticalIndex != 0) return;

            Random.InitState(settings.seed);
            runtimeProceduralGrid = new GameObject("Procedural Grid").transform;
            runtimeProceduralGrid.parent = gridObjectTransform;
            runtimeProceduralGrid.localPosition = new Vector3(0, 0, -verticalGridHeight * thisGridVerticalIndex);
            Vector2 randomOffset = new Vector2(Random.value, Random.value) * 100f;

            for (int x = 0; x < gridWidth; x++)
            {
                for (int y = 0; y < gridLength; y++)
                {
                    InstantiateNode(settings, x, y, randomOffset);
                }
            }
            if (!settings.preserveSeedPostGeneration) Random.InitState((int)System.DateTime.Now.Ticks);
        }

        public void InstantiateNode(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int x, int y, Vector2 randomOffset)
        {
            int randomNodeIndex = GetRandomNodeIndex(settings, x, y, randomOffset);
            if (randomNodeIndex < 0) return;

            Transform nodePrefab = GetNodePrefab(settings, x, y, randomNodeIndex);
            if (nodePrefab == null) return;

            Transform spawnedNode = GameObject.Instantiate(nodePrefab, Vector3.zero, Quaternion.identity, runtimeProceduralGrid);
            nodesList.Add(spawnedNode);
            SetNodeTransform(spawnedNode, settings, x, y, randomNodeIndex);
        }

        private int GetRandomNodeIndex(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int x, int y, Vector2 randomOffset)
        {
            bool isBorder = IsWithinBorders(settings, x, y);
            List<EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects> nodeObjectsList = isBorder ? settings.borderNodeObjects : settings.nodeObjects;

            if (settings.usePerlinNoise)
            {
                float perlinValue = GetPerlinValue(settings, x, y, randomOffset);
                return Mathf.Clamp((int)Map(perlinValue, 0, 1, 0, nodeObjectsList.Count), 0, nodeObjectsList.Count - 1);
            }

            return Random.Range(0, nodeObjectsList.Count);
        }

        private void SetNodeTransform(Transform node, EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int x, int y, int nodeIndex)
        {
            EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects nodeObject = IsWithinBorders(settings, x, y) ? settings.borderNodeObjects[nodeIndex] : settings.nodeObjects[nodeIndex];
            Vector3 offset = GetNodeOffset(nodeObject);
            float nodeSize = (cellSize / 100f) * nodeObject.nodeSizePercentage;

            node.localPosition = GetCellWorldPosition(x, y) + offset;
            node.localScale = new Vector3(nodeSize / nodeObject.prefabTrueScale.x, nodeSize / nodeObject.prefabTrueScale.y, nodeSize / nodeObject.prefabTrueScale.z);
        }

        private float Map(float value, float originalMin, float originalMax, float targetMin, float targetMax)
        {
            return (value - originalMin) / (originalMax - originalMin) * (targetMax - targetMin) + targetMin;
        }

        private bool IsWithinBorders(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int x, int y)
        {
            int borderTileAmount = settings.borderTilesAmount;
            return x < borderTileAmount || x >= gridWidth - borderTileAmount || y < borderTileAmount || y >= gridLength - borderTileAmount;
        }

        private float GetPerlinValue(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int x, int y, Vector2 randomOffset)
        {
            float ValueX = (x + randomOffset.x + settings.scrollNoiseHorizontally) / gridWidth * settings.noiseScale;
            float ValueY = (y + randomOffset.y + settings.scrollNoiseVertically) / gridLength * settings.noiseScale;
            return Mathf.PerlinNoise(ValueX, ValueY);
        }

        private Transform GetNodePrefab(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int x, int y, int nodeIndex)
        {
            List<EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects> nodeObjectsList = IsWithinBorders(settings, x, y) ? settings.borderNodeObjects : settings.nodeObjects;
            if (nodeObjectsList[nodeIndex].keepEmpty) return null;

            var nodePrefabs = nodeObjectsList[nodeIndex].nodePrefabs;
            float totalProbability = CalculateTotalProbability(nodePrefabs);
            float randomPoint = Random.Range(0f, totalProbability);

            return SelectPrefabByProbability(nodePrefabs, randomPoint);
        }

        private float CalculateTotalProbability(IEnumerable<EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects.NodePrefabs> nodePrefabs)
        {
            float totalProbability = 0f;
            foreach (EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects.NodePrefabs nodePrefab in nodePrefabs)
            {
                totalProbability += nodePrefab.probability;
            }
            return totalProbability;
        }

        private Transform SelectPrefabByProbability(IEnumerable<EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects.NodePrefabs> nodePrefabs, float randomPoint)
        {
            float currentProbability = 0f;
            foreach (EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects.NodePrefabs nodePrefab in nodePrefabs)
            {
                currentProbability += nodePrefab.probability;
                if (randomPoint <= currentProbability)
                {
                    return nodePrefab.nodePrefab;
                }
            }
            return null;
        }

        private Vector3 GetNodeOffset(EasyGridBuilderPro.RuntimeProceduralGridSettings.NodeObjects nodeSettings)
        {
            if (nodeSettings.roundRandomValueTo <= 0) nodeSettings.roundRandomValueTo = 0.01f;
            float offsetX = Mathf.Round(Random.Range(nodeSettings.randomLocalOffsetX.x, nodeSettings.randomLocalOffsetX.y) / nodeSettings.roundRandomValueTo) * nodeSettings.roundRandomValueTo;
            float offsetY = Mathf.Round(Random.Range(nodeSettings.randomLocalOffsetY.x, nodeSettings.randomLocalOffsetY.y) / nodeSettings.roundRandomValueTo) * nodeSettings.roundRandomValueTo;
            float offsetZ = Mathf.Round(Random.Range(nodeSettings.randomLocalOffsetZ.x, nodeSettings.randomLocalOffsetZ.y) / nodeSettings.roundRandomValueTo) * nodeSettings.roundRandomValueTo;
            return new Vector3(offsetX, offsetY, offsetZ) * 1;
        }

        public void UpdateRuntimeProceduralGrid(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (runtimeProceduralGrid == null) return;

            if (CanDisplayRuntimeProceduralGrid(settings, thisGridVerticalIndex, activeGridMode, activeGrid))
            {
                SetRuntimeProceduralGridActiveSelf(true);
            }
            else
            {
                SetRuntimeProceduralGridActiveSelf(false);
            }
        }

        private bool CanDisplayRuntimeProceduralGrid(EasyGridBuilderPro.RuntimeProceduralGridSettings settings, int thisGridVerticalIndex, GridMode activeGridMode, GridXY activeGrid)
        {
            if (settings.alwaysLockAtFirstGrid && thisGridVerticalIndex != 0) return false;
            if (settings.onlyShowActiveVerticalGrid && activeGrid != this) return false;
            
            switch (activeGridMode)
            {
                case GridMode.None:         return settings.displayOnDefaultMode;
                case GridMode.BuildMode:    return settings.displayOnBuildMode;
                case GridMode.DestroyMode:  return settings.displayOnDestroyMode;
                case GridMode.SelectMode:   return settings.displayOnSelectMode;
                case GridMode.MoveMode:     return settings.displayOnMoveMode;
                default:                    return false;
            }
        }

        private void SetRuntimeProceduralGridActiveSelf(bool setActive)
        {
            runtimeProceduralGrid.gameObject.SetActive(setActive);
        }

        public void ClearRuntimeProceduralGrid()
        {
            if (runtimeProceduralGrid != null)
            {
                Object.Destroy(runtimeProceduralGrid.gameObject);
                runtimeProceduralGrid = null;
            }
        }

        public bool TryGetRuntimeProceduralGrid(out Transform runtimeProceduralGrid)
        {
            runtimeProceduralGrid = this.runtimeProceduralGrid;
            return runtimeProceduralGrid != null;;
        }
        #endregion Runtime Procedural Grid Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID VALIDATION FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///

        #region Validation Checker Functions Start:
                
        /// <summary>
        /// Checks if the provided cell position is valid within the grid boundaries.
        /// </summary>
        /// <param name="cellPosition">The cell position to validate.</param>
        /// <returns>
        /// True if the cell position is within the boundaries of the grid; otherwise, false.
        /// </returns>
        public bool IsWithinGridBounds(Vector2Int cellPosition)
        {
            int x = cellPosition.x;
            int y = cellPosition.y;
            return x >= 0 && y >= 0 && x < gridWidth && y < gridLength;
        }

        /// <summary>
        /// Checks if the provided cell position is valid within the grid boundaries.
        /// </summary>
        /// <param name="cellPositionXY">The cell position to validate.</param>
        /// <returns>
        /// True if the cell position is within the boundaries of the grid; otherwise, false.
        /// </returns>
        public bool IsWithinGridBounds(CellPositionXY cellPositionXY)
        {
            int x = cellPositionXY.x;
            int y = cellPositionXY.y;
            return x >= 0 && y >= 0 && x < gridWidth && y < gridLength;
        }

        /// <summary>
        /// Checks if the provided cell position is within the grid's boundaries considering specified outer padding.
        /// </summary>
        /// <param name="cellPosition">The cell position to check.</param>
        /// <param name="outerPadding">The padding to consider outside the grid's boundaries.</param>
        /// <returns>
        /// True if the cell position is within the grid boundaries accounting for the outer padding; otherwise, false.
        /// </returns>
        public bool IsWithinGridBoundsWithOuterPadding(Vector2Int cellPosition, Vector2Int outerPadding)
        {
            int x = cellPosition.x;
            int y = cellPosition.y;
            return x >= 0 && y >= 0 && x < gridWidth - outerPadding.x && y < gridLength - outerPadding.y;
        }

        /// <summary>
        /// Checks if the provided cell position is within the grid's boundaries considering specified outer padding.
        /// </summary>
        /// <param name="cellPositionXY">The cell position to check.</param>
        /// <param name="outerPadding">The padding to consider outside the grid's boundaries.</param>
        /// <returns>
        /// True if the cell position is within the grid boundaries accounting for the outer padding; otherwise, false.
        /// </returns>
        public bool IsWithinGridBoundsWithOuterPadding(CellPositionXY cellPositionXY, Vector2Int outerPadding)
        {
            int x = cellPositionXY.x;
            int y = cellPositionXY.y;
            return x >= 0 && y >= 0 && x < gridWidth - outerPadding.x && y < gridLength - outerPadding.y;
        }
        #endregion Validation Checker Functions End:

        #region Validation Functions Start:
        /// <summary>
        /// Clamps the given cell position to ensure it is within the grid's boundaries.
        /// </summary>
        /// <param name="cellPosition">The cell position to validate.</param>
        /// <returns>
        /// A Vector2Int representing a valid cell position within the grid.
        /// If the input position is outside the grid, it is clamped to the nearest valid position within the grid boundaries.
        /// </returns>
        public Vector2Int ClampToGridBounds(Vector2Int cellPosition)
        {
            return new Vector2Int
            (
                Mathf.Clamp(cellPosition.x, 0, gridWidth - 1),
                Mathf.Clamp(cellPosition.y, 0, gridLength - 1)
            );
        }
        
        /// <summary>
        /// Clamps the given cell position to ensure it is within the grid's boundaries.
        /// </summary>
        /// <param name="cellPositionXY">The cell position to validate.</param>
        /// <returns>
        /// A CellPositionXZ representing a valid cell position within the grid.
        /// If the input position is outside the grid, it is clamped to the nearest valid position within the grid boundaries.
        /// </returns>
        public CellPositionXY ClampToGridBounds(CellPositionXY cellPositionXY)
        {
            return new CellPositionXY
            (
                Mathf.Clamp(cellPositionXY.x, 0, gridWidth - 1),
                Mathf.Clamp(cellPositionXY.y, 0, gridLength - 1)
            );
        }
        #endregion Validation Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID SUPPORTER FUNCTIONS                                                      ///
        ///-------------------------------------------------------------------------------///

        #region Trigger Functions Start:
        /// <summary>
        /// Triggers the OnGridObjectChanged event with the specified grid coordinates.
        /// </summary>
        /// <param name="x">The x-coordinate of the grid cell that changed.</param>
        /// <param name="y">The y-coordinate of the grid cell that changed.</param>
        private void TriggerGridCellValueChanged(int x, int y)
        {
            // Invoke the OnGridObjectChanged event and pass the grid cell coordinates
            OnGridCellValueChanged?.Invoke(x, y);
        }
        #endregion Trigger Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///

        #region Getter Functions Start:
        public Vector3 GetOriginPosition()
        {
            return originPosition;
        }

        /// <summary>
        /// Gets the width of the spawned grid.
        /// </summary>
        public int GetWidth()
        {
            return gridWidth;
        }

        //Return the spawnned grid height
        public int GetLength() 
        {
            return gridLength;
        }

        //Return the spawnned grid cell size
        public float GetCellSize() 
        {
            return cellSize;
        }

        //Return cell world position, by taking manual cell position (x, y)
        public Vector3 GetCellWorldPosition(int x, int y) 
        {
            return new Vector3(x , y, 0 ) * cellSize + originPosition;
        }

        //Return cell world position, by taking cell position (x, y)
        public Vector3 GetCellWorldPosition(CellPositionXY cellPositionXY) 
        {
            return new Vector3(cellPositionXY.x * cellSize, cellPositionXY.y * cellSize, 0) + originPosition;
        }

        //Return cell position (x, y), by taking cell world position
        public CellPositionXY GetCellPosition(Vector3 worldPosition)
        {
            return new CellPositionXY(
                Mathf.FloorToInt((worldPosition - originPosition).x / cellSize),
                Mathf.FloorToInt((worldPosition - originPosition).y / cellSize));
        }
        
        //Return cell object, by taking manual cell position (x, y)
        public GridCellData GetCellData(int x, int y)
        {
            if (IsWithinGridBounds(new CellPositionXY(x, y)))
            {
                return gridDataHandler.GetCellData(verticalGridIndex, new Vector2Int(x, y));
            }
            return default(GridCellData);
        }

        //Return cell object, by taking cell position (x, y)
        public GridCellData GetCellData(CellPositionXY cellPositionXY)
        {
            if (IsWithinGridBounds(cellPositionXY))
            {
                return gridDataHandler.GetCellData(verticalGridIndex, new Vector2Int(cellPositionXY.x, cellPositionXY.y));
            }
            return default(GridCellData);
        }

        //Return cell object, by taking world position (This function converts world position to cell position and pass the values to the function above)
        public GridCellData GetCellData(Vector3 worldPosition)
        {
            CellPositionXY cellPositionXY;
            cellPositionXY.x = GetCellPosition(worldPosition).x;
            cellPositionXY.y = GetCellPosition(worldPosition).y;

            return GetCellData(cellPositionXY);
        }
        #endregion Getter Functions End:
        
        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
     
        #region Setter Functions Start:
        //Set grid origin position
        public void SetOriginPosition(Vector3 newOriginPosition)
        {
            originPosition = newOriginPosition;
        }

        //Set grid width and length
        public void SetGridSize(int newWidth, int newLength)
        {
            if (newWidth == gridWidth && newLength == gridLength) return;

            gridWidth = newWidth;
            gridLength = newLength;

            // Reinitialize the grid with the new dimensions
            ResizeGridArray();
            InitializeGrid();

            // Update any runtime grids
            if (TryGetRuntimeCanvasGrid(out _)) UpdateRuntimeCanvasGridScale();
            if (TryGetRuntimeObjectGrid(out _)) UpdateRuntimeObjectGridScale();
        }

        private void ResizeGridArray()
        {
            // Create a new grid array with the new dimensions
            CellPositionXY[,] newGridArray = new CellPositionXY[gridWidth, gridLength];

            // Copy the existing grid data into the new array, within the bounds of the new size
            for (int x = 0; x < Mathf.Min(gridArray.GetLength(0), gridWidth); x++)
            {
                for (int y = 0; y < Mathf.Min(gridArray.GetLength(1), gridLength); y++)
                {
                    newGridArray[x, y] = gridArray[x, y];
                }
            }

            // Assign the new grid array
            gridArray = newGridArray;
        }

        //Set a specified cell object to a specific cell (occupy cell), by taking manual cell position (x, y), and CellObjectXY
        public void SetCellData(int x, int y, GridCellData gridCellData)
        {
            if (IsWithinGridBounds(new CellPositionXY(x, y)))
            {
                gridDataHandler.SetCellData(verticalGridIndex, new Vector2Int(x, y), gridCellData);
                TriggerGridCellValueChanged(x, y);
            }
        }

        //Set a specified cell object to a specific cell (occupy cell), by taking cell position (x, y), and CellObjectXY
        public void SetCellData(CellPositionXY cellPositionXY, GridCellData gridCellData)
        {
            if (IsWithinGridBounds(cellPositionXY))
            {
                gridDataHandler.SetCellData(verticalGridIndex, new Vector2Int(cellPositionXY.x, cellPositionXY.y), gridCellData);
                TriggerGridCellValueChanged(cellPositionXY.x, cellPositionXY.y);
            }
        }

        //Set a specified cell object to a specified cell (occupy cell), by taking world position, and CellObjectXY (This function converts world position to cell position and pass the values to the function above)
        public void SetCellData(Vector3 worldPosition, GridCellData value)
        {
            CellPositionXY cellPositionXY;
            cellPositionXY.x = GetCellPosition(worldPosition).x;
            cellPositionXY.y = GetCellPosition(worldPosition).y;

            SetCellData(cellPositionXY, value);
        }

        public void SetRuntimeCanvasGrid(Transform runtimeCanvasGrid)
        {
            this.runtimeCanvasGrid = runtimeCanvasGrid;
        }

        public void SetRuntimeObjectGrid(Transform runtimeObjectGrid)
        {
            this.runtimeObjectGrid = runtimeObjectGrid;
        }

        public void SetRuntimeTextGrid(Transform runtimeTextGrid)
        {
            this.runtimeTextGrid = runtimeTextGrid;
        }

        public void SetRuntimeProceduralGrid(Transform runtimeProceduralGrid)
        {
            this.runtimeProceduralGrid = runtimeProceduralGrid;
        }

        public override void SetRuntimeObjectGridGeneratedTextureCellToDefault(Vector2Int cellPosition)
        {
            if (cellPosition.x >= 0 && cellPosition.x < gridWidth && cellPosition.y >= 0 && cellPosition.y < gridLength)
            {
                int adjustedY = gridLength - 1 - cellPosition.y;     // Flip y
                int adjustedX = gridWidth - 1 - cellPosition.x;      // Flip x
                runtimeObjectGridGeneratedTexture.SetPixel(adjustedX, adjustedY, new Color(255,255,255,255));
                runtimeObjectGridGeneratedTexture.Apply();
            }
        }

        public override void SetRuntimeObjectGridGeneratedTextureCellColor(Vector2Int cellPosition, Color color)
        {
            if (cellPosition.x >= 0 && cellPosition.x < gridWidth && cellPosition.y >= 0 && cellPosition.y < gridLength)
            {
                int adjustedY = gridLength - 1 - cellPosition.y;     // Flip y
                int adjustedX = gridWidth - 1 - cellPosition.x;      // Flip x
                runtimeObjectGridGeneratedTexture.SetPixel(adjustedX, adjustedY, color);
                runtimeObjectGridGeneratedTexture.Apply();
            }
        }

        public override void SetRuntimeObjectGridHeatMapActiveSelf(bool activeSelf)
        {
            if (activeSelf) { if (!runtimeObjectGridSharedMaterial.IsKeywordEnabled(ENABLE_HEATMAP)) runtimeObjectGridSharedMaterial.EnableKeyword(ENABLE_HEATMAP); }
            else { if (runtimeObjectGridSharedMaterial.IsKeywordEnabled(ENABLE_HEATMAP)) runtimeObjectGridSharedMaterial.DisableKeyword(ENABLE_HEATMAP); }
        }

        public override void SetRuntimeObjectGridHeatMapActiveSelfToggle(out bool toggleMode)
        {
            if (!runtimeObjectGridSharedMaterial.IsKeywordEnabled(ENABLE_HEATMAP)) 
            {
                runtimeObjectGridSharedMaterial.EnableKeyword(ENABLE_HEATMAP);
                toggleMode = true;
            }
            else
            {
                runtimeObjectGridSharedMaterial.DisableKeyword(ENABLE_HEATMAP);
                toggleMode = false;
            }
        }

        public override void SetRuntimeObjectGridHeatMapTexture(Texture2D generatedTexture, Color overrideHDRColor)
        {
            runtimeObjectGridSharedMaterial.SetTexture(GENERATED_HEATMAP_TEXTURE, generatedTexture);
            runtimeObjectGridGeneratedHeatMapTexture = generatedTexture;
            runtimeObjectGridSharedMaterial.SetColor(HEATMAP_HDR_OVERRIDE, overrideHDRColor);
        }

        public void SetRuntimeObjectGridHeatMapTextureCellColor(Vector2Int cellPosition, Color color)
        {
            runtimeObjectGridGeneratedHeatMapTexture.SetPixel(cellPosition.x, cellPosition.y, color);
        }
        #endregion Setter Functions End:
    }
}