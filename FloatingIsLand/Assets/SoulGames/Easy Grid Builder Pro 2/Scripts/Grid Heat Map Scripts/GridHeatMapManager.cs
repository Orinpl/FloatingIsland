using System.Collections;
using System.Collections.Generic;
using SoulGames.Utilities;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Heat Map Manager", 4)]
    [RequireComponent(typeof(GridManager))]
    public class GridHeatMapManager : MonoBehaviour
    {
        public static event OnGridHeatMapReadAllValuesDelegate OnGridHeatMapReadAllValues;
        public delegate void OnGridHeatMapReadAllValuesDelegate(Dictionary<GridModifierSO, float> customModifierValues);

        public static event OnGridHeatMapReadValueDelegate OnGridHeatMapReadValue;
        public delegate void OnGridHeatMapReadValueDelegate(float customModifierValue);

        [SerializeField] private bool enableBrushPainting;
        [SerializeField] private GameObject brushPrefab;
        [SerializeField] private LayerMask customSurfaceLayerMask;
        [SerializeField] private bool brushFaceSurfaceNormals;
        [SerializeField] private float brushSmoothMoveSpeed = 35;
        [SerializeField] private float brushSmoothRotationSpeed = 25;
        [SerializeField] private float brushSmoothScaleSpeed = 10;
        [SerializeField] private float brushInnerCircleRadius = 1f;
        [SerializeField] private float brushOuterCircleRadius = 1.5f;
        [SerializeField] private bool changeBrushRadiusWithInput = true;
        [SerializeField] private float brushChangeSmoothness = 1f; 
        [SerializeField] private float brushMinimumChangeRadius;
        [SerializeField] private float brushMaximumChangeRadius;

        [SerializeField] private bool enableHeatMapValueReading;
        [SerializeField] private HeatMapValueReadMethod heatMapValueReadMethod = HeatMapValueReadMethod.ReadAllValues;
        [SerializeField] private GridModifierSO specificGridModifierSO;

        private Transform brushPrefabTransform;
        private float brushInnerAndOuterRadiusDifference;

        private List<EasyGridBuilderPro> easyGridBuilderProList;
        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private List<GridHeatMap> gridHeatMapList;

        private bool isHeatMapModeEnabled = false;
        private Coroutine scaleCoroutine;

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (brushMinimumChangeRadius < 0.01f) brushMinimumChangeRadius = 0.01f;
            if (brushMaximumChangeRadius < brushMinimumChangeRadius) brushMaximumChangeRadius = brushMinimumChangeRadius;
            brushOuterCircleRadius = Mathf.Clamp(brushOuterCircleRadius, brushInnerCircleRadius, brushOuterCircleRadius);
            brushInnerAndOuterRadiusDifference = brushOuterCircleRadius / brushInnerCircleRadius;
        }
        #endif

        private void Awake()
        {
            easyGridBuilderProList = new List<EasyGridBuilderPro>();
            gridHeatMapList = new List<GridHeatMap>();

            EasyGridBuilderPro.OnGridSystemCreated += OnGridSystemCreated;
        }

        private void Start()
        {
            StartCoroutine(LateStart());
        }

        private void OnDestroy()
        {
            EasyGridBuilderPro.OnGridSystemCreated -= OnGridSystemCreated;
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
        }

        private void OnGridSystemCreated(EasyGridBuilderPro easyGridBuilderPro)
        { 
            if (!easyGridBuilderProList.Contains(easyGridBuilderPro)) easyGridBuilderProList.Add(easyGridBuilderPro);
            if (easyGridBuilderPro.TryGetComponent<GridHeatMap>(out GridHeatMap gridHeatMap))
            {
                if (!gridHeatMapList.Contains(gridHeatMap)) 
                {
                    gridHeatMapList.Add(gridHeatMap);
                    gridHeatMap.SetPaintBrushInnerCircleRadius(brushInnerCircleRadius);
                    gridHeatMap.SetPaintBrushOuterCircleRadius(brushOuterCircleRadius);
                }
            }
        }

        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderProSystem)
        {
            activeEasyGridBuilderPro = activeEasyGridBuilderProSystem;
        }

        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            gridManager = GridManager.Instance;
            
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            activeEasyGridBuilderPro = gridManager.GetActiveEasyGridBuilderPro();

            if (!enableBrushPainting || !brushPrefab) yield break;

            brushPrefabTransform = Instantiate(brushPrefab, Vector3.zero, Quaternion.identity).transform;
            brushPrefabTransform.parent = this.transform;

            brushPrefabTransform.localScale = new Vector3(brushOuterCircleRadius * 2, brushOuterCircleRadius * 2, brushOuterCircleRadius * 2);
            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) brushPrefabTransform.eulerAngles = new Vector3(90, 0, 0);
            brushPrefabTransform.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            UpdateHeatMapBrush();
        }

        private void UpdateHeatMapBrush()
        {
            if (!brushPrefabTransform) return;

            if (!isHeatMapModeEnabled && brushPrefabTransform.gameObject.activeSelf == true) brushPrefabTransform.gameObject.SetActive(false);
            else if (isHeatMapModeEnabled && brushPrefabTransform.gameObject.activeSelf == false) brushPrefabTransform.gameObject.SetActive(true);
            
            Vector3 mouseWorldPosition;
            Quaternion targetRotation;
            Vector3 secondRayDirection; 
            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) secondRayDirection = Vector3.down * 99999;
            else secondRayDirection = Vector3.forward * 99999;

            if (customSurfaceLayerMask != 0) 
            {
                mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPosition(customSurfaceLayerMask, secondRayDirection, out Quaternion hitRotation);
                targetRotation = hitRotation;
            }
            else 
            {
                mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPosition(gridManager.GetGridSystemLayerMask(), secondRayDirection, out Quaternion hitRotation);
                targetRotation = hitRotation;
            }

            brushPrefabTransform.position = Vector3.Lerp(brushPrefabTransform.position, mouseWorldPosition, Time.deltaTime * brushSmoothMoveSpeed);

            if (brushFaceSurfaceNormals) 
            {
                Quaternion rotationOffset = Quaternion.Euler(90, 0, 0);
                targetRotation = targetRotation * rotationOffset;
                brushPrefabTransform.rotation = Quaternion.Lerp(brushPrefabTransform.rotation, targetRotation, Time.deltaTime * brushSmoothRotationSpeed);
            }
            else
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) brushPrefabTransform.eulerAngles = new Vector3(90, 0, 0);
                else brushPrefabTransform.eulerAngles = new Vector3(0, 0, 0);
            }
        }

        public void SetInputToggleHeatMapMode()
        {
            if (!isHeatMapModeEnabled) isHeatMapModeEnabled = true;
            else isHeatMapModeEnabled = false;
            foreach (EasyGridBuilderPro easyGridBuilderPro in easyGridBuilderProList)
            {
                easyGridBuilderPro.SetInputToggleHeatMapMode();
            }
        }

        public void SetInputEnableHeatMapMode()
        {
            isHeatMapModeEnabled = true;
            foreach (EasyGridBuilderPro easyGridBuilderPro in easyGridBuilderProList)
            {
                easyGridBuilderPro.SetInputEnableHeatMapMode();
            }
        }

        public void SetInputDisableHeatMapMode()
        {
            isHeatMapModeEnabled = false;
            foreach (EasyGridBuilderPro easyGridBuilderPro in easyGridBuilderProList)
            {
                easyGridBuilderPro.SetInputDisableHeatMapMode();
            }
        }

        public void SetInputHeatMapSwitchScroll(Vector2 inputDirection)
        {
            if (!isHeatMapModeEnabled) return;
            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetInputHeatMapSwitchScroll(inputDirection);
            }
        }

        public void SetInputHeatMapSwitch(string heatMapID)
        {
            if (!isHeatMapModeEnabled) return;
            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetInputHeatMapSwitch(heatMapID);
            }
        }

        public void SetInputSwitchNextHeatMap()
        {
            if (!isHeatMapModeEnabled) return;
            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetInputSwitchNextHeatMap();
            }
        }

        public void SetInputSwitchPreviousHeatMap()
        {
            if (!isHeatMapModeEnabled) return;
            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetInputSwitchPreviousHeatMap();
            }
        }

        public void SetInputHeatMapPainting()
        {
            if (!isHeatMapModeEnabled || !enableBrushPainting) return;
            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetInputHeatMapPainting();
            }
        }

        public void SetInputHeatMapPaintCancelled()
        {
            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetInputHeatMapPaintCancelled();
            }
        }

        public void SetInputReadHeatMapValue()
        {
            if (!isHeatMapModeEnabled || !enableHeatMapValueReading) return;

            if (heatMapValueReadMethod == HeatMapValueReadMethod.ReadAllValues || heatMapValueReadMethod == HeatMapValueReadMethod.Both) ReadAllHeatMapValues();
            if (heatMapValueReadMethod == HeatMapValueReadMethod.ReadSpecificValue || heatMapValueReadMethod == HeatMapValueReadMethod.Both && specificGridModifierSO != null) ReadSpecificHeatMapValue(specificGridModifierSO);
        }

        private void ReadAllHeatMapValues()
        {
            Vector3 secondRayDirection; 
            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) secondRayDirection = Vector3.down * 99999;
            else secondRayDirection = Vector3.forward * 99999;

            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), secondRayDirection, out _, out _);

            Vector2Int centerCellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            if (activeEasyGridBuilderPro.IsWithinActiveGridBounds(centerCellPosition))
            {
                Dictionary<GridModifierSO, float> customModifierValues = activeEasyGridBuilderPro.GetActiveGridAllCustomModifierValue(centerCellPosition);
                OnGridHeatMapReadAllValues?.Invoke(customModifierValues);
                    
                foreach (KeyValuePair<GridModifierSO, float> customModifierValue in customModifierValues)
                {
                    Debug.Log($"<color=green><b>Custom Modifier:</b></color> {customModifierValue.Key}: <color=green><b>Value:</b></color> {customModifierValue.Value}");
                }
            }
        }

        private void ReadSpecificHeatMapValue(GridModifierSO specificGridModifierSO)
        {
            Vector3 secondRayDirection; 
            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) secondRayDirection = Vector3.down * 99999;
            else secondRayDirection = Vector3.forward * 99999;

            Vector3 mouseWorldPosition = MouseInteractionUtilities.GetMouseWorldPositionWithCustomSurface(customSurfaceLayerMask, gridManager.GetGridSystemLayerMask(), secondRayDirection, out _, out _);

            Vector2Int centerCellPosition = activeEasyGridBuilderPro.GetActiveGridCellPosition(mouseWorldPosition);
            if (activeEasyGridBuilderPro.IsWithinActiveGridBounds(centerCellPosition))
            {
                float value = activeEasyGridBuilderPro.GetActiveGridCustomModifierValue(centerCellPosition, specificGridModifierSO);
                OnGridHeatMapReadValue?.Invoke(value);
                Debug.Log($"<color=green><b>Custom Modifier:</b></color> {specificGridModifierSO}: <color=green><b>Value:</b></color> {value}");
            }
        }
        
        public void SetInputHeatMapBrushSizeScroll(Vector2 inputDirection)
        {
            if (!isHeatMapModeEnabled || !brushPrefabTransform || !changeBrushRadiusWithInput) return;

            brushInnerCircleRadius += inputDirection.y * brushChangeSmoothness * Time.deltaTime;
            brushOuterCircleRadius += inputDirection.y * brushChangeSmoothness * Time.deltaTime;

            SetBrushSize();
        }
        
        public void SetInputHeatMapBrushSizeIncrease(float amount = 10f)
        {
            if (!isHeatMapModeEnabled || !brushPrefabTransform || !changeBrushRadiusWithInput) return;

            brushInnerCircleRadius += amount * brushChangeSmoothness * Time.deltaTime;
            brushOuterCircleRadius += amount * brushChangeSmoothness * Time.deltaTime;

            SetBrushSize();
        }

        public void SetInputHeatMapBrushSizeDecrease(float amount = 10f)
        {
            if (!isHeatMapModeEnabled || !brushPrefabTransform || !changeBrushRadiusWithInput) return;

            brushInnerCircleRadius -= amount * brushChangeSmoothness * Time.deltaTime;
            brushOuterCircleRadius -= amount * brushChangeSmoothness * Time.deltaTime;

            SetBrushSize();
        }
        
        private void SetBrushSize()
        {
            brushInnerCircleRadius = brushOuterCircleRadius / brushInnerAndOuterRadiusDifference;
            brushInnerCircleRadius = Mathf.Clamp(brushInnerCircleRadius, brushMinimumChangeRadius, brushMaximumChangeRadius);
            brushOuterCircleRadius = Mathf.Clamp(brushOuterCircleRadius, brushMinimumChangeRadius, brushMaximumChangeRadius);

            Vector3 targetScale = new Vector3(brushOuterCircleRadius * 2, brushOuterCircleRadius * 2, brushOuterCircleRadius * 2);
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);

            scaleCoroutine = StartCoroutine(SmoothScaleBrush(targetScale));

            foreach (GridHeatMap gridHeatMap in gridHeatMapList)
            {
                gridHeatMap.SetPaintBrushInnerCircleRadius(brushInnerCircleRadius);
                gridHeatMap.SetPaintBrushOuterCircleRadius(brushOuterCircleRadius);
            }
        }

        private IEnumerator SmoothScaleBrush(Vector3 targetScale)
        {
            float lerpSpeed = brushSmoothScaleSpeed;
            while (Vector3.Distance(brushPrefabTransform.localScale, targetScale) > 0.01f)
            {
                brushPrefabTransform.localScale = Vector3.Lerp(brushPrefabTransform.localScale, targetScale, Time.deltaTime * lerpSpeed);
                yield return null;
            }

            brushPrefabTransform.localScale = targetScale;
        }

        public List<GridHeatMap> GetGridHeatMapList() => gridHeatMapList;
    }
}
