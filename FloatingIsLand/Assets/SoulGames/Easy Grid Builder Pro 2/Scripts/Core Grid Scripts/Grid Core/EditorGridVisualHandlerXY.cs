using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    [ExecuteInEditMode]
    public class EditorGridVisualHandlerXY : MonoBehaviour
    {
        #if UNITY_EDITOR
        private EasyGridBuilderPro easyGridBuilderPro;

        private int gridWidth;
        private int gridLength;
        private float cellSize;

        private int verticalGridsCount = 1;
        private float verticalGridHeight = 2.5f;

        private float colliderSizeMultiplier = 5;
        private bool lockColliderAtBottomGrid;

        private bool displayCanvasGrid;
        private EasyGridBuilderPro.CanvasGridSettings canvasGridSettings;

        private bool displayObjectGrid;
        private EasyGridBuilderPro.ObjectGridSettings objectGridSettings;
        
        private List<Vector3> gridOriginList;
        private BoxCollider gridCollider;
        [SerializeField] private int activeVerticalGridIndex = 0;
        [SerializeField] private GridMode activeGridMode = GridMode.None;

        /// Grid Visuals Variables
        private bool isEditorCanvasGridInitialized  = false;
        private bool isEditorObjectGridInitialized  = false;
        private List<Transform> editorCanvasGridsList;
        private List<Transform> editorObjectGridsList;
        private List<Image> editorCanvasGridCellImagesList;
        private List<Material> editorObjectGridMaterialsList;
        private Material editorObjectGridBaseMaterial;

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

        ///-------------------------------------------------------------------------------///
        /// EDITOR GRID INITIALIZE FUNCTIONS                                              ///
        ///-------------------------------------------------------------------------------///

        private void Awake()
        {
            if (Application.isPlaying) 
            {
                Destroy(this.gameObject);
                return;
            }

            gridOriginList = new List<Vector3>();
            editorCanvasGridsList = new List<Transform>();
            editorObjectGridsList = new List<Transform>();
            editorCanvasGridCellImagesList = new List<Image>();
            editorObjectGridMaterialsList = new List<Material>();

            ClearEditorCanvasGrid();
            ClearEditorObjectGrid();
        }
        
        private void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        #region Editor Grid Event Functions Start:
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                DestroyImmediate(gameObject, true);
            }
        }
        #endregion Editor Grid Event Functions End:

        private void Start()
        {
            if (Application.isPlaying) return;
            if (!easyGridBuilderPro) easyGridBuilderPro = transform.parent.GetComponent<EasyGridBuilderPro>();
            if (!easyGridBuilderPro.GetIsUpdateInEditor()) return;

            SetupVariables();
            SetupVerticalGrids();
            UpdateActiveVerticalGrid();
            SetupGridCollider();
            SetupGridVisuals();
        }

        #region Editor Grid Initialization Functions Start:
        private void SetupVariables()
        {
            gridWidth = easyGridBuilderPro.GetGridWidth();
            gridLength = easyGridBuilderPro.GetGridLength();
            cellSize = easyGridBuilderPro.GetCellSize();
            
            verticalGridsCount = easyGridBuilderPro.GetVerticalGridsCount();
            verticalGridHeight = easyGridBuilderPro.GetVerticalGridHeight();

            colliderSizeMultiplier = easyGridBuilderPro.GetColliderSizeMultiplier();
            lockColliderAtBottomGrid = easyGridBuilderPro.GetIsLockColliderAtBottomGrid();

            displayCanvasGrid = easyGridBuilderPro.GetIsDisplayCanvasGrid();
            canvasGridSettings = easyGridBuilderPro.GetCanvasGridSettings();

            displayObjectGrid = easyGridBuilderPro.GetIsDisplayObjectGrid();
            objectGridSettings = easyGridBuilderPro.GetObjectGridSettings();
        }

        private void SetupVerticalGrids()
        {
            gridOriginList.Clear();
            for (int i = 0; i < verticalGridsCount; i++)
            {
                gridOriginList.Add(CalculateGridOrigin(i));
            }
        }

        private Vector3 CalculateGridOrigin(int gridIndex)
        {
            float offsetX = default;
            float offsetY = default;
                
            if (easyGridBuilderPro.GetGridOriginType() == GridOrigin.Center)
            {
                offsetX = (cellSize * gridWidth / 2) * -1;
                offsetY = (cellSize * gridLength / 2) * -1;
            }

            return transform.position + new Vector3(offsetX, offsetY, -verticalGridHeight * gridIndex);
        }
        
        private void SetupGridCollider()
        {
            if (TryGetComponent<BoxCollider>(out BoxCollider gridCollider)) this.gridCollider = gridCollider;
            else this.gridCollider = gameObject.AddComponent<BoxCollider>();

            UpdateGridCollider();
        }

        private void SetupGridVisuals()
        {
            SetupEditorCanvasGrid();
            SetupEditorObjectGrid();
        }
        #endregion Editor Grid Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// EDITOR GRID UPDATE FUNCTIONS                                                  ///
        ///-------------------------------------------------------------------------------///

        private void Update()
        {
            if (Application.isPlaying) return;
            if (!easyGridBuilderPro) easyGridBuilderPro = transform.parent.GetComponent<EasyGridBuilderPro>();
            if (!easyGridBuilderPro.GetIsUpdateInEditor()) return;

            UpdateVariables();
            UpdateActiveVerticalGrid();
            UpdateGridOrigin();
            UpdateGridCollider();
            UpdateGridVisuals();
        }

        #region Editor Grid Update Functions Start:
        private void UpdateVariables()
        {
            if (gridWidth != easyGridBuilderPro.GetGridWidth() || gridLength != easyGridBuilderPro.GetGridLength() || cellSize != easyGridBuilderPro.GetCellSize())
            {
                gridWidth = easyGridBuilderPro.GetGridWidth();
                gridLength = easyGridBuilderPro.GetGridLength();
                cellSize = easyGridBuilderPro.GetCellSize();
                UpdateGridOrigin();
                UpdateEditorCanvasGrid(true, true);
                UpdateEditorObjectGrid(true, true);
            }
            
            if (verticalGridsCount != easyGridBuilderPro.GetVerticalGridsCount())
            {
                ClearEditorCanvasGrid();
                ClearEditorObjectGrid();
                verticalGridsCount = easyGridBuilderPro.GetVerticalGridsCount();
                SetupVerticalGrids();
                SetupGridVisuals();
            }
            
            if (verticalGridHeight != easyGridBuilderPro.GetVerticalGridHeight())
            {
                verticalGridHeight = easyGridBuilderPro.GetVerticalGridHeight();
                UpdateGridOrigin();
                UpdateEditorCanvasGrid(true, true);
                UpdateEditorObjectGrid(true, true);
            }

            colliderSizeMultiplier = easyGridBuilderPro.GetColliderSizeMultiplier();
            lockColliderAtBottomGrid = easyGridBuilderPro.GetIsLockColliderAtBottomGrid();

            if (displayCanvasGrid != easyGridBuilderPro.GetIsDisplayCanvasGrid())
            {
                displayCanvasGrid = easyGridBuilderPro.GetIsDisplayCanvasGrid();
                SetupGridVisuals();
            }
            canvasGridSettings = easyGridBuilderPro.GetCanvasGridSettings();

            if (displayObjectGrid != easyGridBuilderPro.GetIsDisplayObjectGrid())
            {
                displayObjectGrid = easyGridBuilderPro.GetIsDisplayObjectGrid();
                SetupGridVisuals();
            }
            objectGridSettings = easyGridBuilderPro.GetObjectGridSettings();
        }

        private void UpdateActiveVerticalGrid()
        {
            activeVerticalGridIndex = Mathf.Clamp(activeVerticalGridIndex, 0, easyGridBuilderPro.GetVerticalGridsCount() - 1);
        }

        private void UpdateGridOrigin()
        {
            float offsetX = default;
            float offsetY = default;
                    
            if (easyGridBuilderPro.GetGridOriginType() == GridOrigin.Center)
            {
                offsetX = (cellSize * gridWidth / 2) * -1;
                offsetY = (cellSize * gridLength / 2) * -1;
            }

            for (int i = 0; i < verticalGridsCount; i++)
            {
                gridOriginList[i] = new Vector3(transform.position.x + offsetX, transform.position.y + offsetY, transform.position.z - verticalGridHeight * i);
            }
        }
        
        private void UpdateGridCollider()
        {
            if (!gridCollider) return;

            if (lockColliderAtBottomGrid) UpdateColliderProperties(0);
            else UpdateColliderProperties(activeVerticalGridIndex);
        }

        private void UpdateColliderProperties(int gridIndex)
        {
            float offsetX = (cellSize * gridWidth / 2) + gridOriginList[gridIndex].x;
            float offsetY = (cellSize * gridLength / 2) + gridOriginList[gridIndex].y;  

            gridCollider.center = new Vector3(offsetX - transform.position.x, offsetY - transform.position.y, gridOriginList[gridIndex].z - transform.position.z);
            gridCollider.size = new Vector3((cellSize * gridWidth) * colliderSizeMultiplier, (cellSize * gridLength) * colliderSizeMultiplier, 0);
        }
            
        private void UpdateGridVisuals()
        {
            UpdateEditorCanvasGrid(false, true);
            UpdateEditorObjectGrid(false, true);
        }
        #endregion Editor Grid Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// EDITOR GRID VISUAL FUNCTIONS PRIMARY                                          ///
        ///-------------------------------------------------------------------------------///
        
        #region Editor Canvas Grid Functions Start:
        private void SetupEditorCanvasGrid()
        {
            if (displayCanvasGrid && !isEditorCanvasGridInitialized) 
            {
                InitializeEditorCanvasGrid();
                isEditorCanvasGridInitialized = true;
            }
            else if (displayCanvasGrid)
            {
                ClearEditorCanvasGrid();
                SetupEditorCanvasGrid();
            }
            else ClearEditorCanvasGrid();
        }

        private void InitializeEditorCanvasGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (canvasGridSettings.canvasGridPrefab != null && !TryGetEditorCanvasGrid(out _, i))
                {
                    InitializeEditorCanvasGrid(canvasGridSettings, i, activeGridMode);
                }
            }
        }

        private void UpdateEditorCanvasGrid(bool updateProperties, bool updateVisuals)
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (!TryGetEditorCanvasGrid(out _, i)) return;

                if (updateProperties) UpdateEditorCanvasGridProperties(i);
                if (updateVisuals) UpdateEditorCanvasGridVisuals(canvasGridSettings, i, activeGridMode);
            }
        }

        private void ClearEditorCanvasGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (TryGetEditorCanvasGrid(out _, i)) ClearEditorCanvasGrid(i);
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            editorCanvasGridsList.Clear();
            editorCanvasGridCellImagesList.Clear();
            isEditorCanvasGridInitialized = false;
        }
        #endregion Editor Canvas Grid Functions End:

        #region Editor Object Grid Functions Start:
        private void SetupEditorObjectGrid()
        {
            if (displayObjectGrid && !isEditorObjectGridInitialized) 
            {
                InitializeEditorObjectGrid();
                isEditorObjectGridInitialized = true;
            }
            else if (displayObjectGrid)
            {
                ClearEditorObjectGrid();
                SetupEditorObjectGrid();
            }
            else ClearEditorObjectGrid();
        }

        private void InitializeEditorObjectGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (objectGridSettings.objectGridPrefab != null && !TryGetEditorObjectGrid(out _, i))
                {
                    InitializeEditorObjectGrid(objectGridSettings, i, activeGridMode);
                }
            }
        }

        private void UpdateEditorObjectGrid(bool updateProperties, bool updateVisuals)
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (!TryGetEditorObjectGrid(out _, i)) return;

                if (updateProperties) UpdateEditorObjectGridProperties(i);
                if (updateVisuals) UpdateEditorObjectGridVisuals(objectGridSettings, i, activeGridMode);
            }
        }

        private void ClearEditorObjectGrid()
        {
            for (int i = 0; i < verticalGridsCount; i++)
            {
                if (TryGetEditorObjectGrid(out _, i)) ClearEditorObjectGrid(i);
            }
            for (int i = 0; i < transform.childCount; i++)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }

            editorObjectGridsList.Clear();
            editorObjectGridMaterialsList.Clear();
            isEditorObjectGridInitialized = false;
        }
        #endregion Editor Object Grid Functions End:
        
        ///-------------------------------------------------------------------------------///
        /// EDITOR GRID VISUAL FUNCTIONS SECONDARY                                        ///
        ///-------------------------------------------------------------------------------///
        
        #region Editor Canvas Grid Functions Start:
        private void InitializeEditorCanvasGrid(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex, GridMode activeGridMode)
        {
            InstantiateEditorCanvasGrid(canvasGridSettings, thisGridVerticalIndex);
            UpdateEditorCanvasGridProperties(thisGridVerticalIndex);
            UpdateEditorCanvasGridVisuals(canvasGridSettings, thisGridVerticalIndex, activeGridMode);
        }

        private void InstantiateEditorCanvasGrid(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex)
        {
            EnsureListSize(editorCanvasGridsList, thisGridVerticalIndex);
            EnsureListSize(editorCanvasGridCellImagesList, thisGridVerticalIndex);

            editorCanvasGridsList[thisGridVerticalIndex] = GameObject.Instantiate(canvasGridSettings.canvasGridPrefab.transform, Vector3.zero, Quaternion.identity);
            SetEditorCanvasGridsParent(thisGridVerticalIndex);
        }

        private void SetEditorCanvasGridsParent(int thisGridVerticalIndex)
        {
            editorCanvasGridsList[thisGridVerticalIndex].SetParent(gameObject.transform);
        }

        private void EnsureListSize<T>(List<T> list, int index)
        {
            while (list.Count <= index)
            {
                list.Add(default(T));
            }
        }

        private void UpdateEditorCanvasGridProperties(int thisGridVerticalIndex)
        {
            Vector2 gridDimensions = new Vector2(gridWidth * cellSize, gridLength * cellSize);
            Transform gridTextureTransform = editorCanvasGridsList[thisGridVerticalIndex].GetChild(0);
            editorCanvasGridCellImagesList[thisGridVerticalIndex] = gridTextureTransform.GetComponent<Image>();

            editorCanvasGridsList[thisGridVerticalIndex].eulerAngles = new Vector3(0, 0, 0);
            editorCanvasGridsList[thisGridVerticalIndex].GetComponent<RectTransform>().sizeDelta = gridDimensions;
            gridTextureTransform.GetComponent<RectTransform>().sizeDelta = gridDimensions;

            editorCanvasGridCellImagesList[thisGridVerticalIndex].type = Image.Type.Tiled;
            editorCanvasGridCellImagesList[thisGridVerticalIndex].pixelsPerUnitMultiplier = 100 / cellSize;
        }

        private void UpdateEditorCanvasGridVisuals(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex, GridMode activeGridMode)
        {
            if (editorCanvasGridsList[thisGridVerticalIndex] == null) return;

            editorCanvasGridsList[thisGridVerticalIndex].position = gridOriginList[thisGridVerticalIndex];
            editorCanvasGridCellImagesList[thisGridVerticalIndex].sprite = canvasGridSettings.cellImageSprite;

            if (CanDisplayEditorCanvasGrid(canvasGridSettings, thisGridVerticalIndex, activeGridMode))
            {
                SetEditorCanvasGridTransitionGridColor(canvasGridSettings.gridShowColor, canvasGridSettings.colorTransitionSpeed, thisGridVerticalIndex);
            }
            else
            {
                SetEditorCanvasGridTransitionGridColor(canvasGridSettings.gridHideColor, canvasGridSettings.colorTransitionSpeed, thisGridVerticalIndex);
            }
        }

        private bool CanDisplayEditorCanvasGrid(EasyGridBuilderPro.CanvasGridSettings canvasGridSettings, int thisGridVerticalIndex, GridMode activeGridMode)
        {
            if (canvasGridSettings.alwaysLockAtFirstGrid && thisGridVerticalIndex != 0) return false;
            if (canvasGridSettings.onlyShowActiveVerticalGrid && activeVerticalGridIndex != thisGridVerticalIndex) return false;
            
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

        private void SetEditorCanvasGridTransitionGridColor(Color targetColor, float colorTransitionSpeed, int thisGridVerticalIndex)
        {
            editorCanvasGridCellImagesList[thisGridVerticalIndex].color = Color.Lerp(editorCanvasGridCellImagesList[thisGridVerticalIndex].color, targetColor, colorTransitionSpeed * Time.deltaTime);
        }

        private void ClearEditorCanvasGrid(int thisGridVerticalIndex)
        {
            if (editorCanvasGridsList != null && thisGridVerticalIndex >= 0 && thisGridVerticalIndex < editorCanvasGridsList.Count)
            {
                if (editorCanvasGridsList[thisGridVerticalIndex] != null)
                {
                    DestroyImmediate(editorCanvasGridsList[thisGridVerticalIndex].gameObject);
                    editorCanvasGridsList[thisGridVerticalIndex] = null;
                }
            }
        }
        
        private bool TryGetEditorCanvasGrid(out Transform editorCanvasGrid, int thisGridVerticalIndex)
        {
            editorCanvasGrid = null;
            if (editorCanvasGridsList != null && thisGridVerticalIndex >= 0 && thisGridVerticalIndex < editorCanvasGridsList.Count)
            {
                editorCanvasGrid = editorCanvasGridsList[thisGridVerticalIndex];
                return editorCanvasGrid != null;
            }
            return false;
        }
        #endregion Editor Canvas Grid Functions End:

        #region Editor Object Grid Functions Start:
        private void InitializeEditorObjectGrid(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex, GridMode activeGridMode)
        {
            InstantiateEditorObjectGrid(objectGridSettings, thisGridVerticalIndex);
            
            UpdateEditorObjectGridProperties(thisGridVerticalIndex);
            UpdateEditorObjectGridVisuals(objectGridSettings, thisGridVerticalIndex, activeGridMode);
        }

        private void InstantiateEditorObjectGrid(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex)
        {
            EnsureListSize(editorObjectGridsList, thisGridVerticalIndex);
            EnsureListSize(editorObjectGridMaterialsList, thisGridVerticalIndex);

            editorObjectGridsList[thisGridVerticalIndex] = GameObject.Instantiate(objectGridSettings.objectGridPrefab.transform, Vector3.zero, Quaternion.identity);
            editorObjectGridsList[thisGridVerticalIndex].SetParent(transform, false);
            
            if (thisGridVerticalIndex == 0) editorObjectGridBaseMaterial = editorObjectGridsList[thisGridVerticalIndex].GetComponent<Renderer>().sharedMaterial;
            editorObjectGridMaterialsList[thisGridVerticalIndex] = new Material(editorObjectGridBaseMaterial);
            editorObjectGridsList[thisGridVerticalIndex].GetComponent<Renderer>().sharedMaterial = editorObjectGridMaterialsList[thisGridVerticalIndex];
        }

        private void UpdateEditorObjectGridProperties(int thisGridVerticalIndex)
        {
            editorObjectGridMaterialsList[thisGridVerticalIndex].SetFloat(CELL_SIZE, cellSize);
            editorObjectGridMaterialsList[thisGridVerticalIndex].DisableKeyword(USE_ALPHA_MASK);

            editorObjectGridsList[thisGridVerticalIndex].localScale = new Vector3((gridWidth / PLANE_OBJECT_TRUE_SCALE) * cellSize, 1, (gridLength / PLANE_OBJECT_TRUE_SCALE) * cellSize);
        }

        private void UpdateEditorObjectGridVisuals(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex, GridMode activeGridMode)
        {
            if (editorObjectGridsList[thisGridVerticalIndex] == null) return;

            editorObjectGridsList[thisGridVerticalIndex].position = gridOriginList[thisGridVerticalIndex] + (new Vector3(gridWidth * cellSize, gridLength * cellSize, 0) / 2);
            editorObjectGridsList[thisGridVerticalIndex].rotation = Quaternion.Euler(new Vector3(-90, 0, 0));
            editorObjectGridMaterialsList[thisGridVerticalIndex].SetTexture(CELL_TEXTURE, objectGridSettings.cellImageTexture);

            if (CanDisplayEditorObjectGrid(objectGridSettings, thisGridVerticalIndex, activeGridMode))
            {
                SetEditorObjectGridTransitionGridColor(objectGridSettings.gridShowColor, objectGridSettings.gridShowColorHDR, objectGridSettings.colorTransitionSpeed, thisGridVerticalIndex);
            }
            else
            {
                SetEditorObjectGridTransitionGridColor(objectGridSettings.gridHideColor, objectGridSettings.gridHideColorHDR, objectGridSettings.colorTransitionSpeed, thisGridVerticalIndex);
            }

            UpdateEditorObjectGridScrollingNoise(objectGridSettings, thisGridVerticalIndex);
            UpdateEditorObjectGridVisualsAlphaMask(objectGridSettings, activeGridMode, thisGridVerticalIndex);
        }

        private bool CanDisplayEditorObjectGrid(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex, GridMode activeGridMode)
        {
            if (objectGridSettings.alwaysLockAtFirstGrid && thisGridVerticalIndex != 0) return false;
            if (objectGridSettings.onlyShowActiveVerticalGrid && activeVerticalGridIndex != thisGridVerticalIndex) return false;
            
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

        private void SetEditorObjectGridTransitionGridColor(Color targetColor, Color targetColorHDR, float colorTransitionSpeed, int thisGridVerticalIndex)
        {
            editorObjectGridMaterialsList[thisGridVerticalIndex].SetColor(CELL_COLOR_OVERRIDE, Color.Lerp(editorObjectGridMaterialsList[thisGridVerticalIndex].GetColor(CELL_COLOR_OVERRIDE), targetColor, colorTransitionSpeed * Time.deltaTime));
            editorObjectGridMaterialsList[thisGridVerticalIndex].SetColor(HDR_OVERRIDE, Color.Lerp(editorObjectGridMaterialsList[thisGridVerticalIndex].GetColor(HDR_OVERRIDE), targetColorHDR, colorTransitionSpeed * Time.deltaTime));
        }

        private void UpdateEditorObjectGridScrollingNoise(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, int thisGridVerticalIndex)
        {
            if (objectGridSettings.useScrollingNoise)
            {
                ActivateEditorObjectGridScrollingNoise(thisGridVerticalIndex);

                editorObjectGridMaterialsList[thisGridVerticalIndex].SetTexture(NOISE_TEXTURE, objectGridSettings.noiseTexture);
                editorObjectGridMaterialsList[thisGridVerticalIndex].SetVector(NOISE_TEXTURE_TILING, objectGridSettings.textureTiling);
                editorObjectGridMaterialsList[thisGridVerticalIndex].SetVector(NOISE_TEXTURE_SCROLLING, objectGridSettings.textureScrolling);
            }
            else DeactivateEditorObjectGridScrollingNoise(thisGridVerticalIndex);
        }

        private void ActivateEditorObjectGridScrollingNoise(int thisGridVerticalIndex)
        {
            if (!editorObjectGridMaterialsList[thisGridVerticalIndex].IsKeywordEnabled(USE_SCROLLING_NOISE)) editorObjectGridMaterialsList[thisGridVerticalIndex].EnableKeyword(USE_SCROLLING_NOISE);
        }

        private void DeactivateEditorObjectGridScrollingNoise(int thisGridVerticalIndex)
        {
            if (editorObjectGridMaterialsList[thisGridVerticalIndex].IsKeywordEnabled(USE_SCROLLING_NOISE)) editorObjectGridMaterialsList[thisGridVerticalIndex].DisableKeyword(USE_SCROLLING_NOISE);
        }

        private void UpdateEditorObjectGridVisualsAlphaMask(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, GridMode activeGridMode, int thisGridVerticalIndex)
        {
            if (objectGridSettings.useAlphaMask)
            {
                if (CanDisplayEditorObjectGridAlphaMask(objectGridSettings, activeGridMode))
                {
                    ActivateEditorObjectGridAlphaMask(thisGridVerticalIndex);

                    float maskSize = CalculateAdjustedAlphaMaskSize(objectGridSettings.alphaMaskSize, thisGridVerticalIndex);
                    Vector4 maskOffset = GetAlphaMaskOffset(objectGridSettings, maskSize, thisGridVerticalIndex);

                    editorObjectGridMaterialsList[thisGridVerticalIndex].SetTexture(ALPHA_MASK_TEXTURE, objectGridSettings.alphaMaskSprite);
                    editorObjectGridMaterialsList[thisGridVerticalIndex].SetVector(ALPHA_MASK_SIZE, new Vector4(maskSize, maskSize, 0, 0));
                    editorObjectGridMaterialsList[thisGridVerticalIndex].SetVector(ALPHA_MASK_OFFSET, maskOffset);
                }
                else DeactivateEditorObjectGridAlphaMask(thisGridVerticalIndex);
            }
            else DeactivateEditorObjectGridAlphaMask(thisGridVerticalIndex);
        }

        private bool CanDisplayEditorObjectGridAlphaMask(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, GridMode activeGridMode)
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

        private void ActivateEditorObjectGridAlphaMask(int thisGridVerticalIndex)
        {
            if (!editorObjectGridMaterialsList[thisGridVerticalIndex].IsKeywordEnabled(USE_ALPHA_MASK)) editorObjectGridMaterialsList[thisGridVerticalIndex].EnableKeyword(USE_ALPHA_MASK);
        }

        private void DeactivateEditorObjectGridAlphaMask(int thisGridVerticalIndex)
        {
            if (editorObjectGridMaterialsList[thisGridVerticalIndex].IsKeywordEnabled(USE_ALPHA_MASK)) editorObjectGridMaterialsList[thisGridVerticalIndex].DisableKeyword(USE_ALPHA_MASK);
        }

        private float CalculateAdjustedAlphaMaskSize(float alphaMaskSize, int thisGridVerticalIndex)
        {
            float scaleX = editorObjectGridsList[thisGridVerticalIndex].localScale.x;
            float scaleZ = editorObjectGridsList[thisGridVerticalIndex].localScale.z;
            return alphaMaskSize / Mathf.Min(scaleX, scaleZ);
        }

        private Vector4 GetAlphaMaskOffset(EasyGridBuilderPro.ObjectGridSettings objectGridSettings, float maskSize, int thisGridVerticalIndex)
        {
            return new Vector4(objectGridSettings.alphaMaskWorldPosition.x, objectGridSettings.alphaMaskWorldPosition.y, 0, 0) / maskSize / editorObjectGridsList[thisGridVerticalIndex].localScale.x;
        }

        private void ClearEditorObjectGrid(int thisGridVerticalIndex) 
        {
            if (editorObjectGridsList != null && thisGridVerticalIndex >= 0 && thisGridVerticalIndex < editorObjectGridsList.Count)
            {
                if (editorObjectGridsList[thisGridVerticalIndex] != null)
                {
                    DestroyImmediate(editorObjectGridsList[thisGridVerticalIndex].gameObject);
                    DestroyImmediate(editorObjectGridMaterialsList[thisGridVerticalIndex]);
                    editorObjectGridsList[thisGridVerticalIndex] = null;
                }
            }
        }

        private bool TryGetEditorObjectGrid(out Transform editorObjectGrid, int thisGridVerticalIndex)
        {
            editorObjectGrid = null;
            if (editorObjectGridsList != null && thisGridVerticalIndex >= 0 && thisGridVerticalIndex < editorObjectGridsList.Count)
            {
                editorObjectGrid = editorObjectGridsList[thisGridVerticalIndex];
                return editorObjectGrid != null;
            }
            return false;
        }
        #endregion Editor Object Grid Functions End:
        #endif
    }
}