using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid UI/Grid UI Manager", 0)]
    public class GridUIManager : MonoBehaviour
    {
        [SerializeField] private float uiFadeDuration = 0.05f;

        [SerializeField] private bool displayMainUIPanelOnGridModeNone = true;
        [SerializeField] private bool displayMainUIPanelOnGridModeBuild = true;
        [SerializeField] private bool displayMainUIPanelOnGridModeDestroy = true;
        [SerializeField] private bool displayMainUIPanelOnGridModeSelect = true;
        [SerializeField] private bool displayMainUIPanelOnGridModeMove = true;

        [Serializable]
        public class MainUIPanelData
        {
            public RectTransform mainUIPanel;
            public Button buildButton;
            public Button destroyButton;
            public Button selectButton;
            public Button moveButton;
            public Button resetButton;
            public Button switchAboveVerticalGridButton;
            public Button switchBelowVerticalGridButton;
            public Button undoButton;
            public Button redoButton;
            public Button saveButton;
            public Button loadButton;
            public Button enableHeatMapModeButton;
            public Button disableHeatMapModeButton;
            public Button heatMapBrushSizeIncreaseButton;
            public Button heatMapBrushSizeDecreaseButton;
            public float heatMapBrushSizeChangeAmount = 25f;
            public Button switchNextHeatMapButton;
            public Button switchPreviousHeatMapButton;
        }
        [SerializeField] private MainUIPanelData mainUIPanelData;

        [SerializeField] private bool displayBuildableObjectsPanelOnBuildMode = true;
        [Serializable]
        public class BuildableObjectsUIPanelData
        {
            public RectTransform buildableObjectsUIPanel;
            public RectTransform buildablesContentUIPanel;
            public RectTransform buildablesTemplateButton;
            public RectTransform categoriesContentUIPanel;
            public RectTransform categoriesTemplateButton;
        }
        [SerializeField] private BuildableObjectsUIPanelData buildableObjectsUIPanelData;

        [SerializeField] private bool displayBuildingOptionsPanelOnBuildMode = true;
        [Serializable]
        public class BuildingOptionsUIPanelData
        {
            public RectTransform buildingOptionsUIPanel;
            public Button rotateLeftButton;
            public Button rotateRightButton;
            public Button flipButton;
            public Button placementTypeButton;
            public Button swapSpawnOnlyAtEndPointsButton;
            public Button increaseSplineSpaceButton;
            public Button decreaseSplineSpaceButton;
            public float splineSpaceChangeAmount = 1f;
            public Button swapSplineConnectionButton;
            public Button resetSplinePlacementButton;
            public RectTransform placementTypeUIPanel;
            public Button singlePlacementButton;
            public Button boxPlacementButton;
            public Button wireBoxPlacementButton;
            public Button lShapedPlacementButton;
            public Button wirePlacementButton;
            public Button paintPlacementButton;
            public Button splinePlacementButton;
            public Sprite singlePlacementTypeImage;
            public Sprite boxPlacementTypeImage;
            public Sprite wireBoxPlacementTypeImage;
            public Sprite lShapedPlacementTypeImage;
            public Sprite wirePlacementTypeImage;
            public Sprite paintPlacementTypeImage;
            public Sprite splinePlacementTypeImage;
            public Sprite swapSpawnOnlyAtEndPointsTrueImage;
            public Sprite swapSpawnOnlyAtEndPointsFalseImage;
            public Sprite swapSplineConnectionTrueImage;
            public Sprite swapSplineConnectionFalseImage;
        }
        [SerializeField] private BuildingOptionsUIPanelData buildingOptionsUIPanelData;

        [SerializeField] private bool displaySelectorPanelOnObjectSelection = true;
        [Serializable]
        public class SelectorUIPanelData
        {
            public RectTransform selectorUIPanel;
            public Image selectorPortraitImage;
            public Button selectorCloseButton;
            public Button selectorDestroyButton;
            public Button selectorMoveButton;
            public Button selectorCopyBuildableButton;
            public Button selectorFlipButton;
            public Button selectorRotateLeftButton;
            public Button selectorRotateRightButton;
        }
        [SerializeField] private SelectorUIPanelData selectorUIPanelData;

        [SerializeField] private bool displayGridCellManipulatorPanel = true;
        [Serializable]
        public class GridCellManipulatorPanelData
        {
            public RectTransform gridCellManipulatorPanel;
            public Button increaseGridWidthButton;
            public Button decreaseGridWidthButton;
            public Button increaseGridLengthButton;
            public Button decreaseGridLengthButton;
        }
        [SerializeField] private GridCellManipulatorPanelData gridCellManipulatorPanelData;

        private GridManager gridManager;
        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridMode activeGridMode;
        private Dictionary<CanvasGroup, Coroutine> fadeCoroutines;

        private bool isMainUIPanelEnabled;
        private bool isHeatMapModeEnabled;

        private event OnCategoryButtonPressedDelegate OnCategoryButtonPressed;
        private delegate void OnCategoryButtonPressedDelegate(BuildableObjectUICategorySO buildableObjectUICategorySO);
        private event OnBuildableButtonPressedDelegate OnBuildableButtonPressed;
        private delegate void OnBuildableButtonPressedDelegate(BuildableObjectSO buildableObjectSO);

        private bool isBuildableObjectsUIPanelEnabled;
        private List<BuildableObjectUICategorySO> buildableObjectUICategorySOList;
        private List<BuildableObjectSO> buildableObjectSOList;
        private Dictionary<BuildableObjectUICategorySO, RectTransform> instantiatedUICategoryObjectsDictionary;
        private Dictionary<BuildableObjectSO, RectTransform> instantiatedUIBuildableObjectsDictionary;
        private BuildableObjectUICategorySO activeBuildableObjectUICategorySO;

        private bool isBuildingOptionsUIPanelEnabled;
        private bool isPlacementTypeUIPanelEnabled;

        private BuildableObjectSelector buildableObjectSelector;
        private bool isSelectorUIPanelEnabled;

        ///-------------------------------------------------------------------------------///
        /// UI INITIALIZE FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        private void Start()
        {
            fadeCoroutines = new Dictionary<CanvasGroup, Coroutine>();
            buildableObjectUICategorySOList = new List<BuildableObjectUICategorySO>();
            buildableObjectSOList = new List<BuildableObjectSO>();
            instantiatedUICategoryObjectsDictionary = new Dictionary<BuildableObjectUICategorySO, RectTransform>();
            instantiatedUIBuildableObjectsDictionary = new Dictionary<BuildableObjectSO, RectTransform>();

            StartCoroutine(LateStart());
        }

        private void OnDestroy()
        {
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged -= OnActiveGridModeChanged;
            gridManager.OnActiveBuildableSOChanged -= OnActiveBuildableSOChanged;
            gridManager.OnBuildableObjectSOAdded -= OnBuildableObjectSOAdded;
            gridManager.OnBuildableObjectSORemoved -= OnBuildableObjectSORemoved;

            OnCategoryButtonPressed -= OnCategoryButtonPressedMethod;
            OnBuildableButtonPressed -= OnBuildableButtonPressedMethod;

            if (buildableObjectSelector)
            {
                buildableObjectSelector.OnBuildableObjectSelected -= OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected -= OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (mainUIPanelData.mainUIPanel) UnsubscribeFromMainPanelUIButtonEvents();
            if (buildingOptionsUIPanelData.buildingOptionsUIPanel) UnsubscribeFromBuildingOptionPanelUIButtonEvents();
            if (selectorUIPanelData.selectorUIPanel && displaySelectorPanelOnObjectSelection) UnsubscribeFromSelectorPanelUIButtonEvents();
            if (gridCellManipulatorPanelData.gridCellManipulatorPanel && displayGridCellManipulatorPanel) UnsubscribeFromGridCellManipulatorPanelUIButtonEvents();
        }

        #region Initialization Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            gridManager = GridManager.Instance;
            SubscribeToEvents();

            activeEasyGridBuilderPro = GridManager.Instance.GetActiveEasyGridBuilderPro();
            if (activeEasyGridBuilderPro) activeGridMode = activeEasyGridBuilderPro.GetActiveGridMode();
            
            if (mainUIPanelData.mainUIPanel) 
            {
                HandleMainUIPanelActiveSelf(activeGridMode);
                SetUIBGColorForGridModeNone();
            }

            if (gridCellManipulatorPanelData.gridCellManipulatorPanel) HandleGridCellManipulatorUIPanelActiveSelf();
        }

        private void SubscribeToEvents()
        {
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;
            gridManager.OnActiveBuildableSOChanged += OnActiveBuildableSOChanged;
            gridManager.OnBuildableObjectSOAdded += OnBuildableObjectSOAdded;
            gridManager.OnBuildableObjectSORemoved += OnBuildableObjectSORemoved;

            OnCategoryButtonPressed += OnCategoryButtonPressedMethod;
            OnBuildableButtonPressed += OnBuildableButtonPressedMethod;

            if (GridManager.Instance.TryGetBuildableObjectSelector(out buildableObjectSelector))
            {
                buildableObjectSelector.OnBuildableObjectSelected += OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected += OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (mainUIPanelData.mainUIPanel) SubscribeToMainPanelUIButtonEvents();
            if (buildingOptionsUIPanelData.buildingOptionsUIPanel) SubscribeToBuildingOptionPanelUIButtonEvents();
            if (selectorUIPanelData.selectorUIPanel && displaySelectorPanelOnObjectSelection) SubscribeToSelectorPanelUIButtonEvents();
            if (gridCellManipulatorPanelData.gridCellManipulatorPanel && displayGridCellManipulatorPanel) SubscribeToGridCellManipulatorPanelUIButtonEvents();
        }

        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderProSystem)
        {
            activeEasyGridBuilderPro = activeEasyGridBuilderProSystem;
            activeGridMode = activeEasyGridBuilderPro.GetActiveGridMode();

            if (mainUIPanelData.mainUIPanel)
            {
                HandleMainUIPanelActiveSelf(activeGridMode);
                switch (activeGridMode)
                {
                    case GridMode.None: SetUIBGColorForGridModeNone(); break;
                    case GridMode.BuildMode: SetUIBGColorForGridModeBuild(); break;
                    case GridMode.DestroyMode: SetUIBGColorForGridModeDestroy(); break;
                    case GridMode.SelectMode: SetUIBGColorForGridModeSelect(); break;
                    case GridMode.MoveMode: SetUIBGColorForGridModeMove(); break;
                }
            }

            if (buildableObjectsUIPanelData.buildableObjectsUIPanel) 
            {
                HandleBuildableObjectsUIPanelActiveSelf(activeGridMode);
                if (isBuildableObjectsUIPanelEnabled) HandleBuildableObjectsUICategories();
            }
            if (buildingOptionsUIPanelData.buildingOptionsUIPanel) HandleBuildingOptionsUIPanelActiveSelf(activeEasyGridBuilderPro.GetActiveBuildableObjectSO());
        }

        private void OnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            if (activeEasyGridBuilderPro != easyGridBuilderPro) return;
            activeGridMode = gridMode;

            if (mainUIPanelData.mainUIPanel)
            {
                HandleMainUIPanelActiveSelf(activeGridMode);
                switch (activeGridMode)
                {
                    case GridMode.None: SetUIBGColorForGridModeNone(); break;
                    case GridMode.BuildMode: SetUIBGColorForGridModeBuild(); break;
                    case GridMode.DestroyMode: SetUIBGColorForGridModeDestroy(); break;
                    case GridMode.SelectMode: SetUIBGColorForGridModeSelect(); break;
                    case GridMode.MoveMode: SetUIBGColorForGridModeMove(); break;
                }
            }

            if (buildableObjectsUIPanelData.buildableObjectsUIPanel) 
            {
                HandleBuildableObjectsUIPanelActiveSelf(activeGridMode);
                if (isBuildableObjectsUIPanelEnabled) HandleBuildableObjectsUICategories();
            }
            if (buildingOptionsUIPanelData.buildingOptionsUIPanel) HandleBuildingOptionsUIPanelActiveSelf(activeEasyGridBuilderPro.GetActiveBuildableObjectSO());
        }

        private void OnActiveBuildableSOChanged(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            if (activeEasyGridBuilderPro != easyGridBuilderPro) return;
            if (buildingOptionsUIPanelData.buildingOptionsUIPanel) HandleBuildingOptionsUIPanelActiveSelf(buildableObjectSO);
        }

        private void OnBuildableObjectSOAdded(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            if (activeEasyGridBuilderPro != easyGridBuilderPro) return;
            if (isBuildingOptionsUIPanelEnabled) HandleBuildableObjectsUICategories();
        }
        
        private void OnBuildableObjectSORemoved(EasyGridBuilderPro easyGridBuilderPro, BuildableObjectSO buildableObjectSO)
        {
            if (activeEasyGridBuilderPro != easyGridBuilderPro) return;
            if (isBuildingOptionsUIPanelEnabled) HandleBuildableObjectsUICategories();
        }

        private void OnCategoryButtonPressedMethod(BuildableObjectUICategorySO buildableObjectUICategorySO)
        {
            HandleOnCategoryButtonPressed(buildableObjectUICategorySO);
        }

        private void OnBuildableButtonPressedMethod(BuildableObjectSO buildableObjectSO)
        {
            HandleOnBuildableButtonPressed(buildableObjectSO);
        }

        private void OnSelectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            StartCoroutine(OnSelectedByBuildableObjectSelectorDelegate());
        }

        private void OnDeselectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            StartCoroutine(InvokeOnDeselectedByBuildableObjectSelectorDelegate());
        }
        #endregion Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// UI ACTIVE SELF HANDLE FUNCTIONS                                               ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle Main UI Panel ActiveSelf Functions Start:
        private void HandleMainUIPanelActiveSelf(GridMode gridMode)
        {
            if (mainUIPanelData.mainUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                bool shouldDisplayPanel = gridMode switch
                {
                    GridMode.None => displayMainUIPanelOnGridModeNone,
                    GridMode.BuildMode => displayMainUIPanelOnGridModeBuild,
                    GridMode.DestroyMode => displayMainUIPanelOnGridModeDestroy,
                    GridMode.SelectMode => displayMainUIPanelOnGridModeSelect,
                    GridMode.MoveMode => displayMainUIPanelOnGridModeMove,
                    _ => false
                };

                if (shouldDisplayPanel && !isMainUIPanelEnabled)
                {
                    isMainUIPanelEnabled = true;
                    FadeIn(canvasGroup);
                } 
                else if (!shouldDisplayPanel && isMainUIPanelEnabled)
                {
                    isMainUIPanelEnabled = false;
                    FadeOut(canvasGroup);
                }
            }
        }
        #endregion Handle Main UI Panel ActiveSelf Functions End:

        #region Handle Buildable Objects UI Panel ActiveSelf Functions Start:
        private void HandleBuildableObjectsUIPanelActiveSelf(GridMode gridMode)
        {
            if (!displayBuildableObjectsPanelOnBuildMode) return;
            if (buildableObjectsUIPanelData.buildableObjectsUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                gridManager.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover);

                if (gridMode == GridMode.BuildMode && !isBuildableObjectsUIPanelEnabled)
                {
                    if (buildableObjectMover && buildableObjectMover.GetIsMovingStarted()) return;
                    isBuildableObjectsUIPanelEnabled = true;
                    FadeIn(canvasGroup);
                } 
                else if (gridMode != GridMode.BuildMode && isBuildableObjectsUIPanelEnabled)
                {
                    isBuildableObjectsUIPanelEnabled = false;
                    FadeOut(canvasGroup);
                }
            }
        }
        #endregion Handle Buildable Objects UI Panel ActiveSelf Functions End:

        #region Handle Building Options UI Panel ActiveSelf Functions Start:
        private void HandleBuildingOptionsUIPanelActiveSelf(BuildableObjectSO buildableObjectSO)
        {
            if (!displayBuildingOptionsPanelOnBuildMode) return;
            if (buildingOptionsUIPanelData.buildingOptionsUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                if (buildableObjectSO && !isBuildingOptionsUIPanelEnabled)
                {
                    isBuildingOptionsUIPanelEnabled = true;
                    FadeIn(canvasGroup);
                } 
                else if (!buildableObjectSO && isBuildingOptionsUIPanelEnabled)
                {
                    isBuildingOptionsUIPanelEnabled = false;
                    FadeOut(canvasGroup);
                }
            }

            if (buildableObjectSO) 
            {
                HandleBuildingOptionsUIButtonsActiveSelf(buildableObjectSO);
                HandleBuildingOptionsUIPlacementTypeButtonImage(buildableObjectSO);
                HandleSwapOnlySpawnEndPointsButtonImage(buildableObjectSO);
                HandleswapSplineConnectionButtonImage(buildableObjectSO);
            }

            if (gridManager.TryGetBuildableObjectMover(out BuildableObjectMover buildableObjectMover) && buildableObjectMover.GetIsMovingStarted())
            {
                StartCoroutine(HandleBuildingOptionsUIButtonsActiveSelfForMoving(buildableObjectSO));
            }
        }

        private void HandleBuildingOptionsUIButtonsActiveSelf(BuildableObjectSO buildableObjectSO)
        {
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(buildableGridObjectSO.placementType is not GridObjectPlacementType.SinglePlacement && 
                        buildableGridObjectSO.placementType is not GridObjectPlacementType.PaintPlacement && buildableGridObjectSO.placementType is not GridObjectPlacementType.BoxPlacement);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
                case BuildableEdgeObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
                case BuildableCornerObjectSO buildableCornerObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(buildableCornerObjectSO.placementType is not CornerObjectPlacementType.SinglePlacement && 
                        buildableCornerObjectSO.placementType is not CornerObjectPlacementType.PaintPlacement && buildableCornerObjectSO.placementType is not CornerObjectPlacementType.BoxPlacement);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
                case BuildableFreeObjectSO buildableFreeObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(buildableFreeObjectSO.placementType is FreeObjectPlacementType.SplinePlacement);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(buildableFreeObjectSO.placementType is FreeObjectPlacementType.SplinePlacement);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(buildableFreeObjectSO.placementType is FreeObjectPlacementType.SplinePlacement);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(buildableFreeObjectSO.placementType is FreeObjectPlacementType.SplinePlacement);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(true);
                break;
            }
        }

        private void HandleBuildingOptionsUIPlacementTypeButtonImage(BuildableObjectSO buildableObjectSO)
        {
            if (!buildingOptionsUIPanelData.splinePlacementTypeImage || !buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) return;
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: 
                    switch (buildableGridObjectSO.placementType)
                    {
                        case GridObjectPlacementType.SinglePlacement: imageComponent.sprite = buildingOptionsUIPanelData.singlePlacementTypeImage; break;
                        case GridObjectPlacementType.BoxPlacement: imageComponent.sprite = buildingOptionsUIPanelData.boxPlacementTypeImage; break;
                        case GridObjectPlacementType.WireBoxPlacement: imageComponent.sprite = buildingOptionsUIPanelData.wireBoxPlacementTypeImage; break;
                        case GridObjectPlacementType.LShapedPlacement: imageComponent.sprite = buildingOptionsUIPanelData.lShapedPlacementTypeImage; break;
                        case GridObjectPlacementType.FourDirectionWirePlacement: imageComponent.sprite = buildingOptionsUIPanelData.wirePlacementTypeImage; break;
                        case GridObjectPlacementType.PaintPlacement: imageComponent.sprite = buildingOptionsUIPanelData.paintPlacementTypeImage; break;
                    }
                break;
                case BuildableEdgeObjectSO buildableEdgeObjectSO: 
                    switch (buildableEdgeObjectSO.placementType)
                    {
                        case EdgeObjectPlacementType.SinglePlacement: imageComponent.sprite = buildingOptionsUIPanelData.singlePlacementTypeImage; break;
                        case EdgeObjectPlacementType.WireBoxPlacement: imageComponent.sprite = buildingOptionsUIPanelData.wireBoxPlacementTypeImage; break;
                        case EdgeObjectPlacementType.LShapedPlacement: imageComponent.sprite = buildingOptionsUIPanelData.lShapedPlacementTypeImage; break;
                        case EdgeObjectPlacementType.FourDirectionWirePlacement: imageComponent.sprite = buildingOptionsUIPanelData.wirePlacementTypeImage; break;
                        case EdgeObjectPlacementType.PaintPlacement: imageComponent.sprite = buildingOptionsUIPanelData.paintPlacementTypeImage; break;
                    }
                break;
                case BuildableCornerObjectSO buildableCornerObjectSO: 
                    switch (buildableCornerObjectSO.placementType)
                    {
                        case CornerObjectPlacementType.SinglePlacement: imageComponent.sprite = buildingOptionsUIPanelData.singlePlacementTypeImage; break;
                        case CornerObjectPlacementType.BoxPlacement: imageComponent.sprite = buildingOptionsUIPanelData.boxPlacementTypeImage; break;
                        case CornerObjectPlacementType.WireBoxPlacement: imageComponent.sprite = buildingOptionsUIPanelData.wireBoxPlacementTypeImage; break;
                        case CornerObjectPlacementType.LShapedPlacement: imageComponent.sprite = buildingOptionsUIPanelData.lShapedPlacementTypeImage; break;
                        case CornerObjectPlacementType.FourDirectionWirePlacement: imageComponent.sprite = buildingOptionsUIPanelData.wirePlacementTypeImage; break;
                        case CornerObjectPlacementType.PaintPlacement: imageComponent.sprite = buildingOptionsUIPanelData.paintPlacementTypeImage; break;
                    }
                break;
                case BuildableFreeObjectSO buildableFreeObjectSO: 
                    switch (buildableFreeObjectSO.placementType)
                    {
                        case FreeObjectPlacementType.SinglePlacement: imageComponent.sprite = buildingOptionsUIPanelData.singlePlacementTypeImage; break;
                        case FreeObjectPlacementType.PaintPlacement: imageComponent.sprite = buildingOptionsUIPanelData.paintPlacementTypeImage; break;
                        case FreeObjectPlacementType.SplinePlacement: imageComponent.sprite = buildingOptionsUIPanelData.splinePlacementTypeImage; break;
                    }
                break;
            }
        }

        private void HandleSwapOnlySpawnEndPointsButtonImage(BuildableObjectSO buildableObjectSO)
        {
            if (!buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsTrueImage || !buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsFalseImage || 
                !buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.TryGetComponent<Image>(out Image imageComponent)) return;
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO buildableGridObjectSO: 
                    if (buildableGridObjectSO.spawnOnlyAtEndPoints) imageComponent.sprite = buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsTrueImage;
                    else imageComponent.sprite = buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsFalseImage;
                break;
                case BuildableEdgeObjectSO: break;
                case BuildableCornerObjectSO buildableCornerObjectSO: 
                    if (buildableCornerObjectSO.spawnOnlyAtEndPoints) imageComponent.sprite = buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsTrueImage;
                    else imageComponent.sprite = buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsFalseImage;
                break;
                case BuildableFreeObjectSO: break;
            }
        }

        private void HandleswapSplineConnectionButtonImage(BuildableObjectSO buildableObjectSO)
        {
            if (!buildingOptionsUIPanelData.swapSplineConnectionTrueImage || !buildingOptionsUIPanelData.swapSplineConnectionFalseImage || 
                !buildingOptionsUIPanelData.swapSplineConnectionButton.TryGetComponent<Image>(out Image imageComponent)) return;

            if (buildableObjectSO is BuildableFreeObjectSO buildableFreeObjectSO)
            {
                if (buildableFreeObjectSO.closedSpline) imageComponent.sprite = buildingOptionsUIPanelData.swapSplineConnectionTrueImage;
                else imageComponent.sprite = buildingOptionsUIPanelData.swapSplineConnectionFalseImage;
            }
        }

        private IEnumerator HandleBuildingOptionsUIButtonsActiveSelfForMoving(BuildableObjectSO buildableObjectSO)
        {
            switch (buildableObjectSO)
            {
                case BuildableGridObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
                case BuildableEdgeObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(true);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
                case BuildableCornerObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
                case BuildableFreeObjectSO: 
                    buildingOptionsUIPanelData.flipButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.increaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.decreaseSplineSpaceButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.swapSplineConnectionButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.resetSplinePlacementButton.transform.parent.gameObject.SetActive(false);

                    buildingOptionsUIPanelData.singlePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.paintPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.boxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wireBoxPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.lShapedPlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.wirePlacementButton.transform.parent.gameObject.SetActive(false);
                    buildingOptionsUIPanelData.splinePlacementButton.transform.parent.gameObject.SetActive(false);
                break;
            }
            
            yield return new WaitForEndOfFrame();
            HandleBuildingOptionsUIPlacementTypeButtonImage(buildableObjectSO);
        }
        #endregion Handle Building Options UI Panel ActiveSelf Functions End:

        #region Handle Selector UI Panel ActiveSelf Functions Start:
        private IEnumerator OnSelectedByBuildableObjectSelectorDelegate()
        {
            yield return new WaitForEndOfFrame();
            if (!selectorUIPanelData.selectorUIPanel || !displaySelectorPanelOnObjectSelection || isSelectorUIPanelEnabled) yield break;
            if (selectorUIPanelData.selectorUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup)) 
            {
                isSelectorUIPanelEnabled = true;
                FadeIn(canvasGroup);
            }

            if (selectorUIPanelData.selectorPortraitImage) UpdateSelectorUIPortraitImage();
        }

        private IEnumerator InvokeOnDeselectedByBuildableObjectSelectorDelegate()
        {
            yield return new WaitForEndOfFrame();

            if (buildableObjectSelector.GetSelectedBuildableObjectsList().Count > 0)
            {
                if (selectorUIPanelData.selectorPortraitImage) UpdateSelectorUIPortraitImage();
                yield break;
            } 

            if (!selectorUIPanelData.selectorUIPanel || !displaySelectorPanelOnObjectSelection || !isSelectorUIPanelEnabled) yield break;
            if (selectorUIPanelData.selectorUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                isSelectorUIPanelEnabled = false;
                FadeOut(canvasGroup);
            }
        }

        private void UpdateSelectorUIPortraitImage()
        {
            foreach (BuildableObject buildableObject in buildableObjectSelector.GetSelectedBuildableObjectsList())
            {
                if (!buildableObject.GetBuildableObjectSO().objectIcon) continue;
                else
                {
                    selectorUIPanelData.selectorPortraitImage.sprite = buildableObject.GetBuildableObjectSO().objectIcon;
                    return;
                }   
            }
        }
        #endregion Handle Selector UI Panel ActiveSelf Functions End:

        #region Handle Grid Cell Manipulator UI Panel ActiveSelf Functions Start:
        private void HandleGridCellManipulatorUIPanelActiveSelf()
        {
            if (!displayGridCellManipulatorPanel) return;
            if (gridCellManipulatorPanelData.gridCellManipulatorPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup)) FadeIn(canvasGroup);
        }
        #endregion Handle Grid Cell Manipulator UI Panel ActiveSelf Functions End:

        #region Handle UI Panel ActiveSelf Supporter Functions Start:
        private void FadeIn(CanvasGroup canvasGroup)
        {
            StartFade(canvasGroup, 0f, 1f);
        }

        private void FadeOut(CanvasGroup canvasGroup)
        {
            StartFade(canvasGroup, 1f, 0f);
        }

        private void StartFade(CanvasGroup canvasGroup, float startAlpha, float endAlpha)
        {
            // Stop any existing coroutine for this CanvasGroup before starting a new one
            if (fadeCoroutines.TryGetValue(canvasGroup, out Coroutine currentCoroutine) && currentCoroutine != null) StopCoroutine(currentCoroutine);
            Coroutine newCoroutine = StartCoroutine(FadeUIPanel(canvasGroup, startAlpha, endAlpha));
            fadeCoroutines[canvasGroup] = newCoroutine; // Store the new coroutine reference
        }

        private IEnumerator FadeUIPanel(CanvasGroup canvasGroup, float startAlpha, float endAlpha)
        {
            bool canvasGroupActiveSelfHandled = false;
            if (!canvasGroup.gameObject.activeSelf && !canvasGroupActiveSelfHandled)
            {
                canvasGroupActiveSelfHandled = true;
                canvasGroup.gameObject.SetActive(true);
            } 

            float elapsed = 0f;
            canvasGroup.alpha = startAlpha;

            while (elapsed < uiFadeDuration)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / uiFadeDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            canvasGroup.alpha = endAlpha; // Ensure it reaches the exact endAlpha
            // Remove the completed coroutine from the dictionary
            fadeCoroutines[canvasGroup] = null;

            if (canvasGroup.gameObject.activeSelf && !canvasGroupActiveSelfHandled) canvasGroup.gameObject.SetActive(false);
        }
        #endregion Handle UI Panel ActiveSelf Supporter Functions End:

        ///-------------------------------------------------------------------------------///
        /// UI BUTTON EVENTS HANDLE FUNCTIONS                                             ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle All UI Button Events Start:
        private void SubscribeToMainPanelUIButtonEvents()
        {
            if (mainUIPanelData.buildButton) mainUIPanelData.buildButton.onClick.AddListener(OnBuildButtonClicked);
            if (mainUIPanelData.destroyButton) mainUIPanelData.destroyButton.onClick.AddListener(OnDestroyButtonClicked);
            if (mainUIPanelData.selectButton) mainUIPanelData.selectButton.onClick.AddListener(OnSelectButtonClicked);
            if (mainUIPanelData.moveButton) mainUIPanelData.moveButton.onClick.AddListener(OnMoveButtonClicked);
            if (mainUIPanelData.resetButton) mainUIPanelData.resetButton.onClick.AddListener(OnResetButtonClicked);
            if (mainUIPanelData.switchAboveVerticalGridButton) mainUIPanelData.switchAboveVerticalGridButton.onClick.AddListener(OnSwitchAboveVerticalGridButtonClicked);
            if (mainUIPanelData.switchBelowVerticalGridButton) mainUIPanelData.switchBelowVerticalGridButton.onClick.AddListener(OnSwitchBelowVerticalGridButtonClicked);
            if (mainUIPanelData.undoButton) mainUIPanelData.undoButton.onClick.AddListener(OnUndoButtonClicked);
            if (mainUIPanelData.redoButton) mainUIPanelData.redoButton.onClick.AddListener(OnRedoButtonClicked);
            if (mainUIPanelData.saveButton) mainUIPanelData.saveButton.onClick.AddListener(OnSaveButtonClicked);
            if (mainUIPanelData.loadButton) mainUIPanelData.loadButton.onClick.AddListener(OnLoadButtonClicked);
            if (mainUIPanelData.enableHeatMapModeButton) mainUIPanelData.enableHeatMapModeButton.onClick.AddListener(OnEnableHeatMapModeButtonClicked);
            if (mainUIPanelData.disableHeatMapModeButton) mainUIPanelData.disableHeatMapModeButton.onClick.AddListener(OnDisableHeatMapModeButtonClicked);
            if (mainUIPanelData.heatMapBrushSizeIncreaseButton) mainUIPanelData.heatMapBrushSizeIncreaseButton.onClick.AddListener(OnHeatMapBrushSizeIncreaseButtonClicked);
            if (mainUIPanelData.heatMapBrushSizeDecreaseButton) mainUIPanelData.heatMapBrushSizeDecreaseButton.onClick.AddListener(OnHeatMapBrushSizeDecreaseButtonClicked);
            if (mainUIPanelData.switchNextHeatMapButton) mainUIPanelData.switchNextHeatMapButton.onClick.AddListener(OnSwitchNextHeatMapButtonClicked);
            if (mainUIPanelData.switchPreviousHeatMapButton) mainUIPanelData.switchPreviousHeatMapButton.onClick.AddListener(OnSwitchPreviousHeatMapButtonClicked);
        }

        private void UnsubscribeFromMainPanelUIButtonEvents()
        {
            if (mainUIPanelData.buildButton) mainUIPanelData.buildButton.onClick.RemoveListener(OnBuildButtonClicked);
            if (mainUIPanelData.destroyButton) mainUIPanelData.destroyButton.onClick.RemoveListener(OnDestroyButtonClicked);
            if (mainUIPanelData.selectButton) mainUIPanelData.selectButton.onClick.RemoveListener(OnSelectButtonClicked);
            if (mainUIPanelData.moveButton) mainUIPanelData.moveButton.onClick.RemoveListener(OnMoveButtonClicked);
            if (mainUIPanelData.resetButton) mainUIPanelData.resetButton.onClick.RemoveListener(OnResetButtonClicked);
            if (mainUIPanelData.switchAboveVerticalGridButton) mainUIPanelData.switchAboveVerticalGridButton.onClick.RemoveListener(OnSwitchAboveVerticalGridButtonClicked);
            if (mainUIPanelData.switchBelowVerticalGridButton) mainUIPanelData.switchBelowVerticalGridButton.onClick.RemoveListener(OnSwitchBelowVerticalGridButtonClicked);
            if (mainUIPanelData.undoButton) mainUIPanelData.undoButton.onClick.RemoveListener(OnUndoButtonClicked);
            if (mainUIPanelData.redoButton) mainUIPanelData.redoButton.onClick.RemoveListener(OnRedoButtonClicked);
            if (mainUIPanelData.saveButton) mainUIPanelData.saveButton.onClick.RemoveListener(OnSaveButtonClicked);
            if (mainUIPanelData.loadButton) mainUIPanelData.loadButton.onClick.RemoveListener(OnLoadButtonClicked);
            if (mainUIPanelData.enableHeatMapModeButton) mainUIPanelData.enableHeatMapModeButton.onClick.RemoveListener(OnEnableHeatMapModeButtonClicked);
            if (mainUIPanelData.disableHeatMapModeButton) mainUIPanelData.disableHeatMapModeButton.onClick.RemoveListener(OnDisableHeatMapModeButtonClicked);
            if (mainUIPanelData.heatMapBrushSizeIncreaseButton) mainUIPanelData.heatMapBrushSizeIncreaseButton.onClick.RemoveListener(OnHeatMapBrushSizeIncreaseButtonClicked);
            if (mainUIPanelData.heatMapBrushSizeDecreaseButton) mainUIPanelData.heatMapBrushSizeDecreaseButton.onClick.RemoveListener(OnHeatMapBrushSizeDecreaseButtonClicked);
            if (mainUIPanelData.switchNextHeatMapButton) mainUIPanelData.switchNextHeatMapButton.onClick.RemoveListener(OnSwitchNextHeatMapButtonClicked);
            if (mainUIPanelData.switchPreviousHeatMapButton) mainUIPanelData.switchPreviousHeatMapButton.onClick.RemoveListener(OnSwitchPreviousHeatMapButtonClicked);
        }

        private void SubscribeToBuildingOptionPanelUIButtonEvents()
        {
            if (buildingOptionsUIPanelData.rotateLeftButton) buildingOptionsUIPanelData.rotateLeftButton.onClick.AddListener(OnRotateLeftButtonClicked);
            if (buildingOptionsUIPanelData.rotateRightButton) buildingOptionsUIPanelData.rotateRightButton.onClick.AddListener(OnRotateRightButtonClicked);
            if (buildingOptionsUIPanelData.flipButton) buildingOptionsUIPanelData.flipButton.onClick.AddListener(OnFlipRightButtonClicked);
            if (buildingOptionsUIPanelData.placementTypeButton) buildingOptionsUIPanelData.placementTypeButton.onClick.AddListener(OnPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton) buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.onClick.AddListener(OnSwapSpawnOnlyAtEndPointsButtonClicked);
            if (buildingOptionsUIPanelData.increaseSplineSpaceButton) buildingOptionsUIPanelData.increaseSplineSpaceButton.onClick.AddListener(OnIncreaseSplineSpaceButtonClicked);
            if (buildingOptionsUIPanelData.decreaseSplineSpaceButton) buildingOptionsUIPanelData.decreaseSplineSpaceButton.onClick.AddListener(OnDecreaseSplineSpaceButtonClicked);
            if (buildingOptionsUIPanelData.swapSplineConnectionButton) buildingOptionsUIPanelData.swapSplineConnectionButton.onClick.AddListener(OnSwapSplineConnectionButtonClicked);
            if (buildingOptionsUIPanelData.resetSplinePlacementButton) buildingOptionsUIPanelData.resetSplinePlacementButton.onClick.AddListener(OnResetSplinePlacementButtonClicked);

            if (buildingOptionsUIPanelData.singlePlacementButton) buildingOptionsUIPanelData.singlePlacementButton.onClick.AddListener(OnSetSinglePlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.boxPlacementButton) buildingOptionsUIPanelData.boxPlacementButton.onClick.AddListener(OnSetBoxPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.wireBoxPlacementButton) buildingOptionsUIPanelData.wireBoxPlacementButton.onClick.AddListener(OnSetWireBoxPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.lShapedPlacementButton) buildingOptionsUIPanelData.lShapedPlacementButton.onClick.AddListener(OnSetLShapedPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.wirePlacementButton) buildingOptionsUIPanelData.wirePlacementButton.onClick.AddListener(OnSetWirePlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.paintPlacementButton) buildingOptionsUIPanelData.paintPlacementButton.onClick.AddListener(OnSetPaintPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.splinePlacementButton) buildingOptionsUIPanelData.splinePlacementButton.onClick.AddListener(OnSetSplinePlacementTypeButtonClicked);
        }

        private void UnsubscribeFromBuildingOptionPanelUIButtonEvents()
        {
            if (buildingOptionsUIPanelData.rotateLeftButton) buildingOptionsUIPanelData.rotateLeftButton.onClick.RemoveListener(OnRotateLeftButtonClicked);
            if (buildingOptionsUIPanelData.rotateRightButton) buildingOptionsUIPanelData.rotateRightButton.onClick.RemoveListener(OnRotateRightButtonClicked);
            if (buildingOptionsUIPanelData.flipButton) buildingOptionsUIPanelData.flipButton.onClick.RemoveListener(OnFlipRightButtonClicked);
            if (buildingOptionsUIPanelData.placementTypeButton) buildingOptionsUIPanelData.placementTypeButton.onClick.RemoveListener(OnPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton) buildingOptionsUIPanelData.swapSpawnOnlyAtEndPointsButton.onClick.RemoveListener(OnSwapSpawnOnlyAtEndPointsButtonClicked);
            if (buildingOptionsUIPanelData.increaseSplineSpaceButton) buildingOptionsUIPanelData.increaseSplineSpaceButton.onClick.RemoveListener(OnIncreaseSplineSpaceButtonClicked);
            if (buildingOptionsUIPanelData.decreaseSplineSpaceButton) buildingOptionsUIPanelData.decreaseSplineSpaceButton.onClick.RemoveListener(OnDecreaseSplineSpaceButtonClicked);
            if (buildingOptionsUIPanelData.swapSplineConnectionButton) buildingOptionsUIPanelData.swapSplineConnectionButton.onClick.RemoveListener(OnSwapSplineConnectionButtonClicked);
            if (buildingOptionsUIPanelData.resetSplinePlacementButton) buildingOptionsUIPanelData.resetSplinePlacementButton.onClick.RemoveListener(OnResetSplinePlacementButtonClicked);

            if (buildingOptionsUIPanelData.singlePlacementButton) buildingOptionsUIPanelData.singlePlacementButton.onClick.RemoveListener(OnSetSinglePlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.boxPlacementButton) buildingOptionsUIPanelData.boxPlacementButton.onClick.RemoveListener(OnSetBoxPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.wireBoxPlacementButton) buildingOptionsUIPanelData.wireBoxPlacementButton.onClick.RemoveListener(OnSetWireBoxPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.lShapedPlacementButton) buildingOptionsUIPanelData.lShapedPlacementButton.onClick.RemoveListener(OnSetLShapedPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.wirePlacementButton) buildingOptionsUIPanelData.wirePlacementButton.onClick.RemoveListener(OnSetWirePlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.paintPlacementButton) buildingOptionsUIPanelData.paintPlacementButton.onClick.RemoveListener(OnSetPaintPlacementTypeButtonClicked);
            if (buildingOptionsUIPanelData.splinePlacementButton) buildingOptionsUIPanelData.splinePlacementButton.onClick.RemoveListener(OnSetSplinePlacementTypeButtonClicked);
        }

        private void SubscribeToSelectorPanelUIButtonEvents()
        {
            if (selectorUIPanelData.selectorCloseButton) selectorUIPanelData.selectorCloseButton.onClick.AddListener(OnSelectorCloseButtonClicked);
            if (selectorUIPanelData.selectorDestroyButton) selectorUIPanelData.selectorDestroyButton.onClick.AddListener(OnSelectorDestroyButtonClicked);
            if (selectorUIPanelData.selectorMoveButton) selectorUIPanelData.selectorMoveButton.onClick.AddListener(OnSelectorMoveButtonClicked);
            if (selectorUIPanelData.selectorCopyBuildableButton) selectorUIPanelData.selectorCopyBuildableButton.onClick.AddListener(OnSelectorCopyBuildableButtonClicked);
            if (selectorUIPanelData.selectorFlipButton) selectorUIPanelData.selectorFlipButton.onClick.AddListener(OnSelectorFlipButtonClicked);
            if (selectorUIPanelData.selectorRotateLeftButton) selectorUIPanelData.selectorRotateLeftButton.onClick.AddListener(OnSelectorRotateLeftButtonClicked);
            if (selectorUIPanelData.selectorRotateRightButton) selectorUIPanelData.selectorRotateRightButton.onClick.AddListener(OnSelectorRotateRightButtonClicked);
        }

        private void UnsubscribeFromSelectorPanelUIButtonEvents()
        {
            if (selectorUIPanelData.selectorCloseButton) selectorUIPanelData.selectorCloseButton.onClick.RemoveListener(OnSelectorCloseButtonClicked);
            if (selectorUIPanelData.selectorDestroyButton) selectorUIPanelData.selectorDestroyButton.onClick.RemoveListener(OnSelectorDestroyButtonClicked);
            if (selectorUIPanelData.selectorMoveButton) selectorUIPanelData.selectorMoveButton.onClick.RemoveListener(OnSelectorMoveButtonClicked);
            if (selectorUIPanelData.selectorCopyBuildableButton) selectorUIPanelData.selectorCopyBuildableButton.onClick.RemoveListener(OnSelectorCopyBuildableButtonClicked);
            if (selectorUIPanelData.selectorFlipButton) selectorUIPanelData.selectorFlipButton.onClick.RemoveListener(OnSelectorFlipButtonClicked);
            if (selectorUIPanelData.selectorRotateLeftButton) selectorUIPanelData.selectorRotateLeftButton.onClick.RemoveListener(OnSelectorRotateLeftButtonClicked);
            if (selectorUIPanelData.selectorRotateRightButton) selectorUIPanelData.selectorRotateRightButton.onClick.RemoveListener(OnSelectorRotateRightButtonClicked);
        }

        private void SubscribeToGridCellManipulatorPanelUIButtonEvents()
        {
            if (gridCellManipulatorPanelData.increaseGridWidthButton) gridCellManipulatorPanelData.increaseGridWidthButton.onClick.AddListener(OnIncreaseGridWidthButtonClicked);
            if (gridCellManipulatorPanelData.decreaseGridWidthButton) gridCellManipulatorPanelData.decreaseGridWidthButton.onClick.AddListener(OnDecreaseGridWidthButtonClicked);
            if (gridCellManipulatorPanelData.increaseGridLengthButton) gridCellManipulatorPanelData.increaseGridLengthButton.onClick.AddListener(OnIncreaseGridLengthButtonClicked);
            if (gridCellManipulatorPanelData.decreaseGridLengthButton) gridCellManipulatorPanelData.decreaseGridLengthButton.onClick.AddListener(OnDecreaseGridLengthButtonClicked);
        }

        private void UnsubscribeFromGridCellManipulatorPanelUIButtonEvents()
        {
            if (gridCellManipulatorPanelData.increaseGridWidthButton) gridCellManipulatorPanelData.increaseGridWidthButton.onClick.RemoveListener(OnIncreaseGridWidthButtonClicked);
            if (gridCellManipulatorPanelData.decreaseGridWidthButton) gridCellManipulatorPanelData.decreaseGridWidthButton.onClick.RemoveListener(OnDecreaseGridWidthButtonClicked);
            if (gridCellManipulatorPanelData.increaseGridLengthButton) gridCellManipulatorPanelData.increaseGridLengthButton.onClick.RemoveListener(OnIncreaseGridLengthButtonClicked);
            if (gridCellManipulatorPanelData.decreaseGridLengthButton) gridCellManipulatorPanelData.decreaseGridLengthButton.onClick.RemoveListener(OnDecreaseGridLengthButtonClicked);
        }
        #endregion Handle All UI Button Events End:

        #region Handle Main UI Functions Start:
        private void OnBuildButtonClicked()
        {
            gridManager.SetActiveGridModeInAllGrids(GridMode.BuildMode);
            SetUIBGColorForGridModeBuild();
        }

        private void OnDestroyButtonClicked()
        {
            gridManager.SetActiveGridModeInAllGrids(GridMode.DestroyMode);
            SetUIBGColorForGridModeDestroy();
        }

        private void OnSelectButtonClicked()
        {
            gridManager.SetActiveGridModeInAllGrids(GridMode.SelectMode);
            SetUIBGColorForGridModeSelect();
        }

        private void OnMoveButtonClicked()
        {
            gridManager.SetActiveGridModeInAllGrids(GridMode.MoveMode);
            SetUIBGColorForGridModeMove();
        }

        private void OnResetButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputGridModeReset();
            }
            SetUIBGColorForGridModeNone();
        }

        private void SetUIBGColorForGridModeBuild()
        {
            Image imageComponent;
            if (mainUIPanelData.buildButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.647f, 0.906f, 0.2f);
            if (mainUIPanelData.destroyButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.selectButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.moveButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.resetButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
        }

        private void SetUIBGColorForGridModeDestroy()
        {
            Image imageComponent;
            if (mainUIPanelData.buildButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.destroyButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.647f, 0.906f, 0.2f);
            if (mainUIPanelData.selectButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.moveButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.resetButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
        }

        private void SetUIBGColorForGridModeSelect()
        {
            Image imageComponent;
            if (mainUIPanelData.buildButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.destroyButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.selectButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.647f, 0.906f, 0.2f);
            if (mainUIPanelData.moveButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.resetButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
        }

        private void SetUIBGColorForGridModeMove()
        {
            Image imageComponent;
            if (mainUIPanelData.buildButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.destroyButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.selectButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.moveButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.647f, 0.906f, 0.2f);
            if (mainUIPanelData.resetButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
        }

        private void SetUIBGColorForGridModeNone()
        {
            Image imageComponent;
            if (mainUIPanelData.buildButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.destroyButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.selectButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.moveButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            if (mainUIPanelData.resetButton.transform.parent.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.647f, 0.906f, 0.2f);
        }

        private void OnSwitchAboveVerticalGridButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputMoveUpVerticalGrid();
            }
        }

        private void OnSwitchBelowVerticalGridButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputMoveDownVerticalGrid();
            }
        }

        private void OnUndoButtonClicked()
        {
            gridManager.GetGridCommandInvoker().UndoCommand();
        }

        private void OnRedoButtonClicked()
        {
            gridManager.GetGridCommandInvoker().RedoCommand();
        }

        private void OnSaveButtonClicked()
        {
            if (gridManager.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) gridSaveAndLoadManager.SetInputSave();
        }

        private void OnLoadButtonClicked()
        {
            if (gridManager.TryGetGridSaveAndLoadManager(out GridSaveAndLoadManager gridSaveAndLoadManager)) gridSaveAndLoadManager.SetInputLoad();
        }

        private void OnEnableHeatMapModeButtonClicked()
        {
            if (gridManager.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputEnableHeatMapMode();
            if (!isHeatMapModeEnabled)
            {
                isHeatMapModeEnabled = true;
                mainUIPanelData.enableHeatMapModeButton.gameObject.SetActive(false);
                mainUIPanelData.disableHeatMapModeButton.gameObject.SetActive(true);

                mainUIPanelData.buildButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.destroyButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.selectButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.moveButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.undoButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.redoButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.saveButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.loadButton.transform.parent.gameObject.SetActive(false);

                mainUIPanelData.heatMapBrushSizeIncreaseButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.heatMapBrushSizeDecreaseButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.switchNextHeatMapButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.switchPreviousHeatMapButton.transform.parent.gameObject.SetActive(true);
            }
        }

        private void OnDisableHeatMapModeButtonClicked()
        {
            if (gridManager.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputDisableHeatMapMode();
            if (isHeatMapModeEnabled)
            {
                isHeatMapModeEnabled = false;
                mainUIPanelData.enableHeatMapModeButton.gameObject.SetActive(true);
                mainUIPanelData.disableHeatMapModeButton.gameObject.SetActive(false);

                mainUIPanelData.buildButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.destroyButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.selectButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.moveButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.undoButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.redoButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.saveButton.transform.parent.gameObject.SetActive(true);
                mainUIPanelData.loadButton.transform.parent.gameObject.SetActive(true);

                mainUIPanelData.heatMapBrushSizeIncreaseButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.heatMapBrushSizeDecreaseButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.switchNextHeatMapButton.transform.parent.gameObject.SetActive(false);
                mainUIPanelData.switchPreviousHeatMapButton.transform.parent.gameObject.SetActive(false);
            }
        }

        private void OnHeatMapBrushSizeIncreaseButtonClicked()
        {
            if (gridManager.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapBrushSizeIncrease(mainUIPanelData.heatMapBrushSizeChangeAmount);
        }

        private void OnHeatMapBrushSizeDecreaseButtonClicked()
        {
            if (gridManager.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputHeatMapBrushSizeDecrease(mainUIPanelData.heatMapBrushSizeChangeAmount);
        }

        private void OnSwitchNextHeatMapButtonClicked()
        {
            if (gridManager.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputSwitchNextHeatMap();
        }

        private void OnSwitchPreviousHeatMapButtonClicked()
        {
            if (gridManager.TryGetGridHeatMapManager(out GridHeatMapManager gridHeatMapManager)) gridHeatMapManager.SetInputSwitchPreviousHeatMap();
        }
        #endregion Handle Main UI Functions End:
        
        #region Handle Buildable Objects UI Functions Start:
        private void HandleBuildableObjectsUICategories()
        {
            if (!buildableObjectsUIPanelData.categoriesContentUIPanel || !buildableObjectsUIPanelData.categoriesTemplateButton) return;

            buildableObjectUICategorySOList.Clear();
            ClearInstantiatedUICategoryObjects();
            HashSet<BuildableObjectUICategorySO> uniqueCategorieshashSet = new HashSet<BuildableObjectUICategorySO>();

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableGridObjectSOList())
            {
                if (buildableObjectSO.buildableObjectUICategorySO != null) uniqueCategorieshashSet.Add(buildableObjectSO.buildableObjectUICategorySO);
            }

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableEdgeObjectSOList())
            {
                if (buildableObjectSO.buildableObjectUICategorySO != null) uniqueCategorieshashSet.Add(buildableObjectSO.buildableObjectUICategorySO);
            }

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableCornerObjectSOList())
            {
                if (buildableObjectSO.buildableObjectUICategorySO != null) uniqueCategorieshashSet.Add(buildableObjectSO.buildableObjectUICategorySO);
            }

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableFreeObjectSOList())
            {
                if (buildableObjectSO.buildableObjectUICategorySO != null) uniqueCategorieshashSet.Add(buildableObjectSO.buildableObjectUICategorySO);
            }

            buildableObjectUICategorySOList = new List<BuildableObjectUICategorySO>(uniqueCategorieshashSet);

            InstantiateUICategoryObjects();
            HandleBuildableObjectsUIBuildables();

            if (buildableObjectUICategorySOList.Count > 0) activeBuildableObjectUICategorySO = buildableObjectUICategorySOList[0];
            HandleActiveBuildableObjectUICategorySOInteraction();
        }

        private void HandleBuildableObjectsUIBuildables()
        {
            if (!buildableObjectsUIPanelData.buildablesContentUIPanel || !buildableObjectsUIPanelData.buildablesTemplateButton) return;

            buildableObjectSOList.Clear();
            ClearInstantiatedUIBuildableObjects();

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableGridObjectSOList())
            {
                buildableObjectSOList.Add(buildableObjectSO);
            }

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableEdgeObjectSOList())
            {
                buildableObjectSOList.Add(buildableObjectSO);
            }

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableCornerObjectSOList())
            {
                buildableObjectSOList.Add(buildableObjectSO);
            }

            foreach (BuildableObjectSO buildableObjectSO in activeEasyGridBuilderPro.GetBuildableFreeObjectSOList())
            {
                buildableObjectSOList.Add(buildableObjectSO);
            }

            InstantiateUIBuildableObjects();
        }

        private void InstantiateUICategoryObjects()
        {
            foreach (BuildableObjectUICategorySO buildableObjectUICategorySO in buildableObjectUICategorySOList)
            {
                RectTransform categoryUIObject = Instantiate(buildableObjectsUIPanelData.categoriesTemplateButton);
                categoryUIObject.transform.SetParent(buildableObjectsUIPanelData.categoriesContentUIPanel);
                categoryUIObject.localScale = buildableObjectsUIPanelData.categoriesTemplateButton.localScale;
                categoryUIObject.gameObject.SetActive(true);

                if (buildableObjectUICategorySO.categoryIcon && categoryUIObject.transform.GetChild(0).TryGetComponent<Image>(out Image imageComponent)) 
                {
                    imageComponent.sprite = buildableObjectUICategorySO.categoryIcon;
                }
                instantiatedUICategoryObjectsDictionary.Add(buildableObjectUICategorySO, categoryUIObject);

                if (categoryUIObject.transform.GetChild(0).TryGetComponent<Button>(out Button button)) 
                {
                    button.onClick.AddListener(delegate { OnCategoryButtonPressed(buildableObjectUICategorySO); });
                }
            }
        }

        private void InstantiateUIBuildableObjects()
        {
            foreach (BuildableObjectSO buildableObjectSO in buildableObjectSOList)
            {
                RectTransform buildableUIObject = Instantiate(buildableObjectsUIPanelData.buildablesTemplateButton);
                buildableUIObject.transform.SetParent(buildableObjectsUIPanelData.buildablesContentUIPanel);
                buildableUIObject.localScale = buildableObjectsUIPanelData.buildablesTemplateButton.localScale;
                buildableUIObject.gameObject.SetActive(true);

                if (buildableObjectSO.objectIcon && buildableUIObject.transform.GetChild(0).TryGetComponent<Image>(out Image imageComponent)) 
                {
                    imageComponent.sprite = buildableObjectSO.objectIcon;
                }
                instantiatedUIBuildableObjectsDictionary.Add(buildableObjectSO, buildableUIObject);

                if (buildableUIObject.transform.GetChild(0).TryGetComponent<Button>(out Button button)) 
                {
                    button.onClick.AddListener(delegate { OnBuildableButtonPressed(buildableObjectSO); });
                }
            }
        }

        private void HandleActiveBuildableObjectUICategorySOInteraction()
        {
            Image imageComponent;
            foreach (KeyValuePair<BuildableObjectUICategorySO, RectTransform> instantiatedUICategoryObject in instantiatedUICategoryObjectsDictionary)
            {
                if (instantiatedUICategoryObject.Key == activeBuildableObjectUICategorySO)
                {
                    if (instantiatedUICategoryObject.Value.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.647f, 0.906f, 0.2f);
                }
                else if (instantiatedUICategoryObject.Value.TryGetComponent<Image>(out imageComponent)) imageComponent.color = new Color(0.125f, 0.482f, 1.0f);
            }

            foreach (KeyValuePair<BuildableObjectSO, RectTransform> instantiatedUIBuildableObject in instantiatedUIBuildableObjectsDictionary)
            {
                if (instantiatedUIBuildableObject.Key.buildableObjectUICategorySO == activeBuildableObjectUICategorySO)
                {
                    instantiatedUIBuildableObject.Value.gameObject.SetActive(true);
                }
                else instantiatedUIBuildableObject.Value.gameObject.SetActive(false);
            }
        }

        private void ClearInstantiatedUICategoryObjects()
        {
            foreach (KeyValuePair<BuildableObjectUICategorySO, RectTransform> instantiatedUICategoryObject in instantiatedUICategoryObjectsDictionary)
            {
                if (instantiatedUICategoryObject.Value.transform.GetChild(0).TryGetComponent<Button>(out Button button)) 
                {
                    button.onClick.RemoveAllListeners();
                }
                Destroy(instantiatedUICategoryObject.Value.gameObject);
            }
            instantiatedUICategoryObjectsDictionary.Clear();
        }

        private void ClearInstantiatedUIBuildableObjects()
        {
            foreach (KeyValuePair<BuildableObjectSO, RectTransform> instantiatedUIBuildableObject in instantiatedUIBuildableObjectsDictionary)
            {
                if (instantiatedUIBuildableObject.Value.transform.GetChild(0).TryGetComponent<Button>(out Button button)) 
                {
                    button.onClick.RemoveAllListeners();
                }
                Destroy(instantiatedUIBuildableObject.Value.gameObject);
            }
            instantiatedUIBuildableObjectsDictionary.Clear();
        }

        private void HandleOnCategoryButtonPressed(BuildableObjectUICategorySO buildableObjectUICategorySO)
        {
            activeBuildableObjectUICategorySO = buildableObjectUICategorySO;
            HandleActiveBuildableObjectUICategorySOInteraction();
        }

        private void HandleOnBuildableButtonPressed(BuildableObjectSO buildableObjectSO)
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputActiveBuildableObjectSO(buildableObjectSO, onlySetBuildableExistInBuildablesList: true);
            }
        }
        #endregion Handle Buildable Objects UI Functions End:
        
        #region Handle Building Options UI Functions Start:
        private void OnRotateLeftButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputBuildableObjectClockwiseRotation(true);
            }
        }

        private void OnRotateRightButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputBuildableObjectCounterClockwiseRotation(true);
            }
        }

        private void OnFlipRightButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputBuildableEdgeObjectFlip();
            }
        }

        private void OnPlacementTypeButtonClicked()
        {
            if (buildingOptionsUIPanelData.placementTypeUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                if (!isPlacementTypeUIPanelEnabled)
                {
                    isPlacementTypeUIPanelEnabled = true;
                    FadeIn(canvasGroup);
                } 
                else
                {
                    isPlacementTypeUIPanelEnabled = false;
                    FadeOut(canvasGroup);
                }
            }
            HandleBuildingOptionsUIButtonsActiveSelf(null);
        }

        private void OnSwapSpawnOnlyAtEndPointsButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectOnlySpawnAtEndPointsModeSwap();
            HandleSwapOnlySpawnEndPointsButtonImage(activeEasyGridBuilderPro.GetActiveBuildableObjectSO());
        }

        private void OnIncreaseSplineSpaceButtonClicked()
        {
            activeEasyGridBuilderPro.SetInputIncreaseBuildableFreeObjectSplineSpacing(buildingOptionsUIPanelData.splineSpaceChangeAmount);
        }

        private void OnDecreaseSplineSpaceButtonClicked()
        {
            activeEasyGridBuilderPro.SetInputDecreaseBuildableFreeObjectSplineSpacing(buildingOptionsUIPanelData.splineSpaceChangeAmount);
        }

        private void OnSwapSplineConnectionButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableFreeObjectSplineConnectionModeSwap();
            HandleswapSplineConnectionButtonImage(activeEasyGridBuilderPro.GetActiveBuildableObjectSO());
        }

        private void OnResetSplinePlacementButtonClicked()
        {
            foreach (EasyGridBuilderPro easyGridBuilderPro in gridManager.GetEasyGridBuilderProSystemsList())
            {
                easyGridBuilderPro.SetInputPlacementReset();
            }
        }

        private void OnSetSinglePlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType.SinglePlacement, EdgeObjectPlacementType.SinglePlacement, CornerObjectPlacementType.SinglePlacement, 
                FreeObjectPlacementType.SinglePlacement);

            if (buildingOptionsUIPanelData.singlePlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.singlePlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }

        private void OnSetBoxPlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType.BoxPlacement, cornerObjectPlacementType: CornerObjectPlacementType.BoxPlacement);

            if (buildingOptionsUIPanelData.boxPlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.boxPlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }

        private void OnSetWireBoxPlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType.WireBoxPlacement, EdgeObjectPlacementType.WireBoxPlacement, CornerObjectPlacementType.WireBoxPlacement);

            if (buildingOptionsUIPanelData.wireBoxPlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.wireBoxPlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }

        private void OnSetLShapedPlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType.LShapedPlacement, EdgeObjectPlacementType.LShapedPlacement, CornerObjectPlacementType.LShapedPlacement);

            if (buildingOptionsUIPanelData.lShapedPlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.lShapedPlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }

        private void OnSetWirePlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType.FourDirectionWirePlacement, EdgeObjectPlacementType.FourDirectionWirePlacement, 
                    CornerObjectPlacementType.FourDirectionWirePlacement);

            if (buildingOptionsUIPanelData.wirePlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.wirePlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }

        private void OnSetPaintPlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(GridObjectPlacementType.PaintPlacement, EdgeObjectPlacementType.PaintPlacement, CornerObjectPlacementType.PaintPlacement, 
                    FreeObjectPlacementType.PaintPlacement);

            if (buildingOptionsUIPanelData.paintPlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.paintPlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }

        private void OnSetSplinePlacementTypeButtonClicked()
        {
            activeEasyGridBuilderPro.SetActiveBuildableObjectSOPlacementType(freeObjectPlacementType: FreeObjectPlacementType.SplinePlacement);

            if (buildingOptionsUIPanelData.splinePlacementTypeImage && buildingOptionsUIPanelData.placementTypeButton.TryGetComponent<Image>(out Image imageComponent)) 
            {
                imageComponent.sprite = buildingOptionsUIPanelData.splinePlacementTypeImage;
            }
            CompleteSetPlacementTypeButtonClick();
        }   

        private void CompleteSetPlacementTypeButtonClick()
        {
            HandleBuildingOptionsUIButtonsActiveSelf(activeEasyGridBuilderPro.GetActiveBuildableObjectSO());

            if (buildingOptionsUIPanelData.placementTypeUIPanel.TryGetComponent<CanvasGroup>(out CanvasGroup canvasGroup))
            {
                if (isPlacementTypeUIPanelEnabled)
                {
                    isPlacementTypeUIPanelEnabled = false;
                    FadeOut(canvasGroup);
                }
            }
        }
        #endregion Handle Building Options UI Functions End:

        #region Handle Selector UI Functions Start:
        private void OnSelectorCloseButtonClicked()
        {
            if (buildableObjectSelector) buildableObjectSelector.SetInputAreaSelectionReset();
        }

        private void OnSelectorDestroyButtonClicked()
        {
            if (buildableObjectSelector) buildableObjectSelector.SetInputSelectionDestroy();
        }

        private void OnSelectorMoveButtonClicked()
        {
            if (buildableObjectSelector) buildableObjectSelector.SetInputSelectionMove();
        }

        private void OnSelectorCopyBuildableButtonClicked()
        {
            if (buildableObjectSelector) buildableObjectSelector.SetInputSelectionCopy();
        }

        private void OnSelectorFlipButtonClicked()
        {
            if (buildableObjectSelector) buildableObjectSelector.SetInputSelectedBuildableEdgeObjectFlip();
        }

        private void OnSelectorRotateLeftButtonClicked()
        {
            if (buildableObjectSelector) 
            {
                buildableObjectSelector.SetInputSelectedBuildableObjectCounterClockwiseRotation();
                buildableObjectSelector.SetInputSelectedBuildableObjectCounterClockwiseRotationComplete();
            }
        }

        private void OnSelectorRotateRightButtonClicked()
        {
            if (buildableObjectSelector) 
            {
                buildableObjectSelector.SetInputSelectedBuildableObjectClockwiseRotation();
                buildableObjectSelector.SetInputSelectedBuildableObjectClockwiseRotationComplete();
            }
        }
        #endregion Handle Selector UI Functions End:

        #region Handle Grid Cell Manipulator UI Functions Start:
        private void OnIncreaseGridWidthButtonClicked()
        {
            if (!activeEasyGridBuilderPro) return;
            activeEasyGridBuilderPro.SetGridWidthAndLength(activeEasyGridBuilderPro.GetGridWidth() + 1, activeEasyGridBuilderPro.GetGridLength(), true);
        }

        private void OnDecreaseGridWidthButtonClicked()
        {
            if (!activeEasyGridBuilderPro) return;
            if (activeEasyGridBuilderPro.GetGridWidth() <= 1) return;
            activeEasyGridBuilderPro.SetGridWidthAndLength(activeEasyGridBuilderPro.GetGridWidth() - 1, activeEasyGridBuilderPro.GetGridLength(), true);
        }

        private void OnIncreaseGridLengthButtonClicked()
        {
            if (!activeEasyGridBuilderPro) return;
            activeEasyGridBuilderPro.SetGridWidthAndLength(activeEasyGridBuilderPro.GetGridWidth(), activeEasyGridBuilderPro.GetGridLength() + 1, true);
        }

        private void OnDecreaseGridLengthButtonClicked()
        {
            if (!activeEasyGridBuilderPro) return;
            if (activeEasyGridBuilderPro.GetGridLength() <= 1) return;
            activeEasyGridBuilderPro.SetGridWidthAndLength(activeEasyGridBuilderPro.GetGridWidth(), activeEasyGridBuilderPro.GetGridLength() - 1, true);
        }
        #endregion Handle Grid Cell Manipulator UI Functions End:
    }
}