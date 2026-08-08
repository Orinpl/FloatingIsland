using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;
using System;
using System.Collections;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Easy Grid Builder Pro Heat Map", 2)]
    [RequireComponent(typeof(EasyGridBuilderPro))]
    public class GridHeatMap : MonoBehaviour
    {
        [SerializeField] private bool onlyGenerateFirstVerticalGrid = true;
        [SerializeField] private bool enableBrushPainting;
        [SerializeField] private LayerMask customSurfaceLayerMask;

        [Serializable] public class HeatMap
        {
            public string heatMapID;
            public float pixelMultiplier = 1;
            public Vector2Int textureSize;
            public int maxTextureSize = 2048;
            public FilterMode textureFilterMode = FilterMode.Bilinear;
            public Color backgroundColor;
            [ColorUsage(true, true)] public Color heatMapHDROverrideColor;
            
            [Space]
            public GridModifierSO gridModifierSO;
            public bool affectByGridAreaModifiers = true;

            [Space]
            public float modifierValueChangeAmount;
            public PaintingMethod paintingMethod = PaintingMethod.OnClickHold;
            public float holdInterval = 0.1f;
            public ValueChangeType valueChangeType = ValueChangeType.Addition;
            public Color modifierValueMaxColor;
            public Color modifierValueMinColor;
        }
        [Space]
        public List<HeatMap> heatMapList;

        private EasyGridBuilderPro easyGridBuilderPro;
        private int gridWidth;
        private int gridLength;
        private float cellSize;

        private HeatMap activeHeatMap;
        private int activeHeatMapIndex;
        private Dictionary<HeatMap, Texture2D[]> generatedHeatMapTextureDictionary;

        private int activeVerticalGridIndex;
        private Vector3 mouseWorldPosition;
        private bool isHeatMapPaintKeyHolding;
        private float paintBrushInnerCircleRadius = 1f;
        private float paintBrushOuterCircleRadius = 1.5f;

        private float timer = 0f;

        #if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateAndInitialize();
        }
        #endif

        #region HeatMap Editor Functions Start:
        #if UNITY_EDITOR
        private void ValidateAndInitialize()
        {
            if (!easyGridBuilderPro) easyGridBuilderPro = GetComponent<EasyGridBuilderPro>();

            gridWidth = easyGridBuilderPro.GetGridWidth();
            gridLength = easyGridBuilderPro.GetGridLength();
            cellSize = easyGridBuilderPro.GetCellSize();

            if (heatMapList == null) return;
            foreach (HeatMap heatmap in heatMapList)
            {
                if (gridWidth > 16384 || gridLength > 16384) heatmap.maxTextureSize = 16;
                else if (gridWidth > 8192 || gridLength > 8192 && gridWidth <= 16384 && gridLength <= 16384) heatmap.maxTextureSize = 16384;
                else if (gridWidth > 4096 || gridLength > 4096 && gridWidth <= 8192 && gridLength <= 8192) heatmap.maxTextureSize = 8192;
                else if (gridWidth > 2048 || gridLength > 2048 && gridWidth <= 4096 && gridLength <= 4096) heatmap.maxTextureSize = 4096;
                else if (gridWidth <= 2048 && gridLength <= 2048) heatmap.maxTextureSize = 2048;

                heatmap.pixelMultiplier = Mathf.Clamp(heatmap.pixelMultiplier, 1, CalculateMaxPixelMultiplier(Mathf.Max(gridWidth, gridLength), heatmap.maxTextureSize));
                heatmap.textureSize = new Vector2Int(Mathf.Min(Mathf.RoundToInt(gridWidth * heatmap.pixelMultiplier), heatmap.maxTextureSize), Mathf.Min(Mathf.RoundToInt(gridLength * heatmap.pixelMultiplier), heatmap.maxTextureSize));
            }
        }

        private float CalculateMaxPixelMultiplier(int maxValue, int maxTextureSize)
        {
            return (float)maxTextureSize / maxValue;
        }
        #endif
        #endregion HeatMap Editor Functions End:

        private void Awake()
        {
            generatedHeatMapTextureDictionary = new Dictionary<HeatMap, Texture2D[]>();
            mouseWorldPosition = Vector3.zero;
            if (!easyGridBuilderPro) easyGridBuilderPro = GetComponent<EasyGridBuilderPro>();
        }

        private void Start()
        {
            StartCoroutine(LateStart());
        }

        private void OnDestroy()
        {
            GridManager.Instance.OnActiveVerticalGridChanged += OnActiveVerticalGridChanged;
            GridAreaModifierManager.OnGridAreaModifierManagerUpdated -= OnGridAreaModifierManagerUpdated;
        }

        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            GridManager.Instance.OnActiveVerticalGridChanged += OnActiveVerticalGridChanged;
            GridAreaModifierManager.OnGridAreaModifierManagerUpdated += OnGridAreaModifierManagerUpdated;
            
            if (!easyGridBuilderPro.GetIsDisplayObjectGrid())
            {
                if (easyGridBuilderPro is EasyGridBuilderProXZ) Debug.Log($"EasyGridBuilderPro XZ: {this.name}: <color=red><b>Object Grid Visual is Not Enabled! Deactivating HeatMap</b></color>");
                else Debug.Log($"EasyGridBuilderPro XY: {this.name}: <color=red><b>Object Grid Visual is Not Enabled! Deactivating HeatMap</b></color>");
                yield break;
            }

            activeHeatMapIndex = 0;
            activeHeatMap = heatMapList[activeHeatMapIndex];

            InitializeHeatMap();
        }

        private void OnGridAreaModifierManagerUpdated(GridModifierSO gridModifierSO, Dictionary<Vector2Int, float> modifiedCellValuesDictionary, Grid occupiedGrid, bool affectAllVerticalGrids)
        {
            StartCoroutine(LateOnGridAreaModifierManagerUpdated(gridModifierSO, modifiedCellValuesDictionary, occupiedGrid, affectAllVerticalGrids));
        }

        private IEnumerator LateOnGridAreaModifierManagerUpdated(GridModifierSO gridModifierSO, Dictionary<Vector2Int, float> modifiedCellValuesDictionary, Grid occupiedGrid, bool affectAllVerticalGrids)
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            foreach (HeatMap heatMap in heatMapList)
            {
                if (!heatMap.affectByGridAreaModifiers) yield break;

                if (gridModifierSO == heatMap.gridModifierSO)
                {
                    foreach (KeyValuePair<Vector2Int, float> modifiedCellValue in modifiedCellValuesDictionary)
                    {
                        if (affectAllVerticalGrids)
                        {
                            for (int i = 0; i < generatedHeatMapTextureDictionary[heatMap].Length; i++)
                            {
                                PaintTexture(modifiedCellValue.Value, modifiedCellValue.Key, heatMap, i);
                            }
                        }
                        else PaintTexture(modifiedCellValue.Value, modifiedCellValue.Key, heatMap, easyGridBuilderPro.GetVerticalGridIndexOf(occupiedGrid));
                    }
                }
            }
        }

        #region HeatMap Initialization Functions Start:
        private void OnActiveVerticalGridChanged(EasyGridBuilderPro easyGridBuilderPro, int activeVerticalGridIndex)
        {
            if (this.easyGridBuilderPro != easyGridBuilderPro) return;
            this.activeVerticalGridIndex = activeVerticalGridIndex;
        }

        private void InitializeHeatMap()
        {
            easyGridBuilderPro.SetRuntimeObjectGridHeatMapOnlyAffectFirstVerticalGrid(onlyGenerateFirstVerticalGrid);

            foreach (HeatMap heatMap in heatMapList)
            {
                if (onlyGenerateFirstVerticalGrid) generatedHeatMapTextureDictionary[heatMap] = new Texture2D[1];
                else generatedHeatMapTextureDictionary[heatMap] = new Texture2D[easyGridBuilderPro.GetVerticalGridsCount()];

                for (int i = 0; i < generatedHeatMapTextureDictionary[heatMap].Length; i++)
                {
                    generatedHeatMapTextureDictionary[heatMap][i] = new Texture2D(heatMap.textureSize.x, heatMap.textureSize.y, TextureFormat.RGBA32, false)
                    {
                        filterMode = heatMap.textureFilterMode,
                        //alphaIsTransparency = true
                    };
                    easyGridBuilderPro.SetRuntimeObjectGridHeatMapTexture(generatedHeatMapTextureDictionary[heatMap][i], i, heatMap.heatMapHDROverrideColor);
                    InitializeGeneratedTexture(i, heatMap);
                }
            }
            SetHeatMapTexture(activeHeatMap);
        }

        public void InitializeGeneratedTexture(int verticalGridIndex, HeatMap heatMap)
        {
            for (int x = 0; x < heatMap.textureSize.x; x++)
            {
                for (int y = 0; y < heatMap.textureSize.y; y++)
                {
                    generatedHeatMapTextureDictionary[heatMap][verticalGridIndex].SetPixel(x, y, heatMap.backgroundColor);
                }
            }
            generatedHeatMapTextureDictionary[heatMap][verticalGridIndex].Apply();
        }
        #endregion HeatMap Initialization Functions End:
        
        #region Handle Input Functions Start:
        public void SetInputHeatMapPainting()
        {
            isHeatMapPaintKeyHolding = true;
            HandleHeatMapBrushPainting();
        }

        public void SetInputHeatMapPaintCancelled()
        {
            isHeatMapPaintKeyHolding = false;
        }

        public void SetInputHeatMapSwitchScroll(Vector2 inputDirection)
        {
            activeHeatMapIndex = (activeHeatMapIndex + (int)inputDirection.y) % heatMapList.Count;
            activeHeatMap = heatMapList[activeHeatMapIndex];
            SetHeatMapTexture(activeHeatMap);
        }

        public void SetInputHeatMapSwitch(string heatMapID)
        {
            foreach (HeatMap heatMap in heatMapList)
            {
                if (heatMap.heatMapID == heatMapID) SetHeatMapTexture(heatMap);
                break;
            }
        }

        public void SetInputSwitchNextHeatMap()
        {
            SetInputHeatMapSwitchScroll(new Vector2(0, 1));
        }

        public void SetInputSwitchPreviousHeatMap()
        {
            SetInputHeatMapSwitchScroll(new Vector2(0, -1));
        }

        private void SetHeatMapTexture(HeatMap heatMap)
        {
            for (int i = 0; i < generatedHeatMapTextureDictionary[heatMap].Length; i++)
            {
                easyGridBuilderPro.SetRuntimeObjectGridHeatMapTexture(generatedHeatMapTextureDictionary[heatMap][i], i, heatMap.heatMapHDROverrideColor);
            }
        }
        #endregion Handle Input Functions End:

        private void Update()
        {
            Vector3 secondRayDirection; 
            if (easyGridBuilderPro is EasyGridBuilderProXZ) secondRayDirection = Vector3.down * 99999;
            else secondRayDirection = Vector3.forward * 99999;

            mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(customSurfaceLayerMask, GridManager.Instance.GetGridSystemLayerMask(), secondRayDirection, out _, out _);

            if (activeHeatMap == null) return;
            if (activeHeatMap.paintingMethod == PaintingMethod.OnClickHold) HandleHeatMapBrushPainting();
        }

        public void HandleHeatMapBrushPainting()
        {
            if (!enableBrushPainting || !isHeatMapPaintKeyHolding) return;
            if (GridManager.Instance.GetActiveEasyGridBuilderPro() != easyGridBuilderPro) return;
            if (onlyGenerateFirstVerticalGrid && activeVerticalGridIndex != 0) return;

            timer += Time.deltaTime;
            if (timer < activeHeatMap.holdInterval) return;

            Vector2Int centerCellPosition = easyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            Vector3 centerWorldPosition = easyGridBuilderPro.GetActiveGridCellWorldPosition(centerCellPosition);
            int cellRadius = Mathf.CeilToInt(paintBrushOuterCircleRadius / cellSize);

            for (int x = -cellRadius; x <= cellRadius; x++)
            {
                for (int y = -cellRadius; y <= cellRadius; y++)
                {
                    Vector2Int cellOffset = new Vector2Int(x, y);
                    Vector2Int cellPosition = centerCellPosition + cellOffset;

                    if (!easyGridBuilderPro.IsWithinActiveGridBounds(cellPosition)) continue;

                    Vector3 cellWorldPosition = easyGridBuilderPro.GetActiveGridCellWorldPosition(cellPosition);
                    float distance = Vector3.Distance(centerWorldPosition, cellWorldPosition);

                    if (distance <= paintBrushOuterCircleRadius)
                    {
                        float modifierValue;
                        if (distance <= paintBrushInnerCircleRadius) modifierValue = activeHeatMap.modifierValueChangeAmount;
                        else
                        {
                            float fallOffFactor = (distance - paintBrushInnerCircleRadius) / (paintBrushOuterCircleRadius - paintBrushInnerCircleRadius);
                            modifierValue = Mathf.Lerp(activeHeatMap.modifierValueChangeAmount, activeHeatMap.gridModifierSO.minimumValue, fallOffFactor);
                        }

                        float currentValue = easyGridBuilderPro.GetActiveGridCustomModifierValue(cellPosition, activeHeatMap.gridModifierSO);

                        switch (activeHeatMap.valueChangeType)
                        {
                            case ValueChangeType.Fixed: currentValue = modifierValue; break;
                            case ValueChangeType.Addition: currentValue += modifierValue; break;
                            case ValueChangeType.Multiplication: currentValue *= activeHeatMap.modifierValueChangeAmount; break;
                        }
                        currentValue = Mathf.Clamp(currentValue, activeHeatMap.gridModifierSO.minimumValue, activeHeatMap.gridModifierSO.maximumValue);

                        PaintTexture(currentValue, cellPosition, activeHeatMap, activeVerticalGridIndex);
                        easyGridBuilderPro.SetActiveGridCustomModifierValue(cellPosition, activeHeatMap.gridModifierSO, currentValue);
                    }
                }
                timer = 0f;
            }
        }

        private void PaintTexture(float currentValue, Vector2Int cellPosition, HeatMap activeHeatMap, int activeVerticalGridIndex)
        {
            Color modifierColor = Color.Lerp(activeHeatMap.modifierValueMinColor, activeHeatMap.modifierValueMaxColor, Mathf.InverseLerp(activeHeatMap.gridModifierSO.minimumValue, activeHeatMap.gridModifierSO.maximumValue, currentValue));

            Vector2Int textureCoord = CellToTextureCoord(cellPosition, activeHeatMap);
            int cellPixelSize = Mathf.RoundToInt(activeHeatMap.pixelMultiplier);
            for (int x = 0; x < cellPixelSize; x++)
            {
                for (int y = 0; y < cellPixelSize; y++)
                {
                    int xCoord = textureCoord.x + x;
                    int zCoord = textureCoord.y + y;

                    if (xCoord >= 0 && xCoord < activeHeatMap.textureSize.x && zCoord >= 0 && zCoord < activeHeatMap.textureSize.y)
                    {
                        int adjustedX = activeHeatMap.textureSize.x - 1 - xCoord;     // Flip x
                        int adjustedZ = activeHeatMap.textureSize.y - 1 - zCoord;     // Flip z
                        generatedHeatMapTextureDictionary[activeHeatMap][activeVerticalGridIndex].SetPixel(adjustedX, adjustedZ, modifierColor);
                    }
                }
            }
            generatedHeatMapTextureDictionary[activeHeatMap][activeVerticalGridIndex].Apply();
        }

        private Vector2Int CellToTextureCoord(Vector2Int cellPosition, HeatMap activeHeatMap)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt(cellPosition.x * activeHeatMap.pixelMultiplier), 0, activeHeatMap.textureSize.x - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt(cellPosition.y * activeHeatMap.pixelMultiplier), 0, activeHeatMap.textureSize.y - 1);
            return new Vector2Int(x, z);
        }

        public void SetPaintBrushInnerCircleRadius(float radius)
        {
            paintBrushInnerCircleRadius = radius;
        }

        public void SetPaintBrushOuterCircleRadius(float radius)
        {
            paintBrushOuterCircleRadius = radius;
        }
    }
}