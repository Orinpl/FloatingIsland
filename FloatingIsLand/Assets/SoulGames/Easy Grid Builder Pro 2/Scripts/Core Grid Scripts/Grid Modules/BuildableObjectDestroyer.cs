using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;
using UnityEngine.UI;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Modules/Buildable Object Destroyer", 0)]
    public class BuildableObjectDestroyer : MonoBehaviour
    {
        public event OnBuildableObjectHoverEnterDelegate OnBuildableObjectHoverEnter;
        public delegate void OnBuildableObjectHoverEnterDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectHoverExitDelegate OnBuildableObjectHoverExit;
        public delegate void OnBuildableObjectHoverExitDelegate(BuildableObject buildableObject);

        public event OnBuildableObjectDestroyedDelegate OnBuildableObjectDestroyed;
        public delegate void OnBuildableObjectDestroyedDelegate(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject);
        
        public event OnBuildableObjectDestroyedInternalDelegate OnBuildableObjectDestroyedInternal;
        public delegate void OnBuildableObjectDestroyedInternalDelegate(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject);

        [SerializeField] private DestroyMode destroyMode = DestroyMode.IndividualAndArea;
        [SerializeField] private float areaDestructionPadding = 10f;
        [SerializeField] private DestructableBuildableObjectType destructableObjectType = DestructableBuildableObjectType.All;
        [SerializeField] private LayerMask destructableObjectsLayerMask;

        [Space]
        [SerializeField] private Canvas screenSpaceSelector;
        [SerializeField] private Color selectorColor;

        [Space]
        [SerializeField] private bool blockDestructionInGridModeDefault = true;
        [SerializeField] private bool blockDestructionInGridModeBuild = true;
        [SerializeField] private bool blockDestructionInGridModeDestroy;
        [SerializeField] private bool blockDestructionInSGridModeSelect = true;
        [SerializeField] private bool blockDestructionInGridModeMove = true;

        private EasyGridBuilderPro activeEasyGridBuilderPro;
        private GridManager gridManager;
        private bool isDestructionKeyHolding;
        private Vector3 destructionBoxAreaStartPosition;
        private Vector3 destructionBoxAreaEndPosition;
        private RectTransform screenSpaceSelectorBox;
        private bool isAreaDestructionResetInputTriggered;

        private List<BuildableObject> destructableObjectsList;
        private BuildableObject previousHoveredObject;

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE DESTROYER INITIALIZE FUNCTIONS                                      ///
        ///-------------------------------------------------------------------------------///

        private void Start() 
        {
            destructableObjectsList = new List<BuildableObject>();
            StartCoroutine(LateStart());
            if (destroyMode == DestroyMode.IndividualAndArea) InitializeScreenSpaceSelector();
        }

        private void OnDestroy()
        {
            gridManager.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged -= OnActiveGridModeChanged;
        }

        #region Initialization Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            gridManager = GridManager.Instance;
            gridManager.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;
            gridManager.OnActiveGridModeChanged += OnActiveGridModeChanged;

            activeEasyGridBuilderPro = GridManager.Instance.GetActiveEasyGridBuilderPro();
        }

        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderProSystem)
        {
            activeEasyGridBuilderPro = activeEasyGridBuilderProSystem;
        }

        private void OnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            ResetAreaDestruction();
            ResetIndividualDestruction();
        }

        private void InitializeScreenSpaceSelector()
        {
            screenSpaceSelector = Instantiate(screenSpaceSelector);
            screenSpaceSelector.transform.SetParent(transform);
            screenSpaceSelectorBox = screenSpaceSelector.transform.GetChild(0).GetComponent<RectTransform>();
            screenSpaceSelectorBox.GetComponent<Image>().color = selectorColor;
        }
        #endregion Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE DESTROYER UPDATE FUNCTIONS                                          ///
        ///-------------------------------------------------------------------------------///
        
        private void Update()
        {
            if (!activeEasyGridBuilderPro) return;
            if (activeEasyGridBuilderPro.GetUseDestroyModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.DestroyMode) return;
            if (IsDestructionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            if (isDestructionKeyHolding) UpdateDestructionKeyHodling();
            else UpdateIndividualDestructionHover();
        }

        #region Update Functions Start:
        private bool IsDestructionBlockedByActiveGridMode(GridMode gridMode)
        {
            switch (gridMode)
            {
                case GridMode.None : return blockDestructionInGridModeDefault;
                case GridMode.BuildMode : return blockDestructionInGridModeBuild;
                case GridMode.DestroyMode : return blockDestructionInGridModeDestroy;
                case GridMode.SelectMode : return blockDestructionInSGridModeSelect;
                case GridMode.MoveMode : return blockDestructionInGridModeMove;
            }
            return true;
        }

        private bool IsDestructionBlockedByDestructableObjectType(BuildableObject buildableObject)
        {
            switch (destructableObjectType)
            {
                case DestructableBuildableObjectType.All : return false;
                case DestructableBuildableObjectType.BuildableGridObject : 
                    if (buildableObject is BuildableGridObject) return false;
                    else return true;
                case DestructableBuildableObjectType.BuildableEdgeObject :
                    if (buildableObject is BuildableEdgeObject) return false;
                    else return true;
                case DestructableBuildableObjectType.BuildableCornerObject :
                    if (buildableObject is BuildableCornerObject) return false;
                    else return true;
                case DestructableBuildableObjectType.BuildableFreeObject :
                    if (buildableObject is BuildableFreeObject) return false;
                    else return true;
            }
            return true;
        }

        private void UpdateDestructionKeyHodling()
        {
            Vector3 destructionBoxAreaCurrentPosition = MouseInteractionUtilities.GetCurrentMousePosition();

            if (destroyMode == DestroyMode.IndividualAndArea && Vector3.Distance(destructionBoxAreaCurrentPosition, destructionBoxAreaStartPosition) > 1)
            {
                UpdateScreenSpaceSelectionVisuals(destructionBoxAreaCurrentPosition);
            }
        }

        private void UpdateScreenSpaceSelectionVisuals(Vector3 destructionBoxAreaCurrentPosition)
        {
            if (isAreaDestructionResetInputTriggered) return;
            
            screenSpaceSelector.gameObject.SetActive(true);

            ResetIndividualDestruction();

            float selectorWidth = destructionBoxAreaCurrentPosition.x - destructionBoxAreaStartPosition.x;
            float selectorHeight = destructionBoxAreaCurrentPosition.y - destructionBoxAreaStartPosition.y;

            screenSpaceSelectorBox.sizeDelta = new Vector2(Mathf.Abs(selectorWidth), Mathf.Abs(selectorHeight));
            screenSpaceSelectorBox.anchoredPosition = (destructionBoxAreaStartPosition + destructionBoxAreaCurrentPosition) / 2;

            if (Vector3.Distance(destructionBoxAreaEndPosition, destructionBoxAreaStartPosition) < 1) return;

            float minX = screenSpaceSelectorBox.anchoredPosition.x - (screenSpaceSelectorBox.sizeDelta.x / 2);
            float maxX = screenSpaceSelectorBox.anchoredPosition.x + (screenSpaceSelectorBox.sizeDelta.x / 2);
            float minY = screenSpaceSelectorBox.anchoredPosition.y - (screenSpaceSelectorBox.sizeDelta.y / 2);
            float maxY = screenSpaceSelectorBox.anchoredPosition.y + (screenSpaceSelectorBox.sizeDelta.y / 2);

            if (gridManager.TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager))
            {
                foreach (BuildableObject buildableObject in gridBuiltObjectsManager.GetBuiltObjectsList())
                {
                    if (!buildableObject.GetBuildableObjectSO().isObjectDestructable) continue;
                    if (IsDestructionBlockedByDestructableObjectType(buildableObject)) continue;
                    if ((destructableObjectsLayerMask.value & (1 << buildableObject.gameObject.layer)) == 0) continue;

                    Vector3 screenPosition = MouseInteractionUtilities.GetWorldToScreenPosition(buildableObject.transform.position);
                    if (screenPosition.x > minX - areaDestructionPadding && screenPosition.x < maxX + areaDestructionPadding && screenPosition.y > minY - areaDestructionPadding && screenPosition.y < maxY + areaDestructionPadding)
                    {
                        if (!destructableObjectsList.Contains(buildableObject)) 
                        {
                            destructableObjectsList.Add(buildableObject);
                            OnBuildableObjectHoverEnter?.Invoke(buildableObject);
                        }
                    }
                    else 
                    {
                        if (destructableObjectsList.Contains(buildableObject)) 
                        {
                            destructableObjectsList.Remove(buildableObject);
                            OnBuildableObjectHoverExit?.Invoke(buildableObject);
                        }
                    }
                }
            }
        }
        
        private void UpdateIndividualDestructionHover()
        {
            if (MouseInteractionUtilities.TryGetBuildableObject(destructableObjectsLayerMask, out BuildableObject buildableObject))
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();
                if (IsDestructionBlockedByDestructableObjectType(buildableObject)) return;
                    
                // If hovering over a different object than before, call the OnBuildableObjectHoverExit for the previous object
                if (previousHoveredObject != null && previousHoveredObject != buildableObject) OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);

                // Call the OnBuildableObjectHoverEnter event for the new object
                OnBuildableObjectHoverEnter?.Invoke(buildableObject);

                // Update the previously hovered object
                previousHoveredObject = buildableObject;
            }
            else 
            {
                // If no object is hovered but a previous object was hovered, call the OnBuildableObjectHoverExit event
                if (previousHoveredObject != null)
                {
                    OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);
                    previousHoveredObject = null; // Reset after exit
                }
            }
        }
        #endregion Update Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT HANDLING FUNCTIONS                                                      ///
        ///-------------------------------------------------------------------------------///
        
        #region Handle Input Grid Mode Functions Start:
        public void SetInputGridModeReset()
        {
            ResetAreaDestruction();
            ResetIndividualDestruction();
        }

        public void SetInputAreaDestructionReset()
        {
            ResetAreaDestruction();
        }

        private void ResetIndividualDestruction()
        {
            if (previousHoveredObject != null)
            {
                OnBuildableObjectHoverExit?.Invoke(previousHoveredObject);
                previousHoveredObject = null;
            }
        }

        private void ResetAreaDestruction()
        {
            isAreaDestructionResetInputTriggered = true;
            isDestructionKeyHolding = false;
            if (screenSpaceSelector) screenSpaceSelector.gameObject.SetActive(false);

            for (int i = destructableObjectsList.Count - 1; i >= 0; i--)
            {
                BuildableObject buildableObject = destructableObjectsList[i];
                OnBuildableObjectHoverExit?.Invoke(buildableObject);
                destructableObjectsList.RemoveAt(i);
            }
        }
        #endregion Handle Input Grid Mode Functions End:

        #region Handle Input Destructable Buildable Object Type Functions Start:
        public void SetInputDestructableBuildableObjectType(DestructableBuildableObjectType destructableObjectType)
        {
            ResetAreaDestruction();
            ResetIndividualDestruction();

            if (this.destructableObjectType != destructableObjectType) this.destructableObjectType = destructableObjectType;  
        }
        #endregion Handle Input Destructable Buildable Object Type Functions End:

        #region Handle Input Buildable Object Destruction Functions Start:
        public void SetInputDestroyBuildableObject()
        {
            if (activeEasyGridBuilderPro.GetUseDestroyModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.DestroyMode) return;
            if (IsDestructionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            isDestructionKeyHolding = true;
            isAreaDestructionResetInputTriggered = false;
            if (destroyMode == DestroyMode.IndividualAndArea)
            {
                destructionBoxAreaStartPosition = MouseInteractionUtilities.GetCurrentMousePosition();
                return;
            }

            InvokeDestoryBuildableObject();
        }

        public void SetInputDestroyBuildableObjectComplete()
        {
            if (activeEasyGridBuilderPro.GetUseDestroyModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.DestroyMode) return;
            if (IsDestructionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;

            isDestructionKeyHolding = false;
            if (destroyMode == DestroyMode.Individual) return;

            if (isAreaDestructionResetInputTriggered) 
            {
                isAreaDestructionResetInputTriggered = false;
                return;
            }

            destructionBoxAreaEndPosition = MouseInteractionUtilities.GetCurrentMousePosition();
            if (destroyMode == DestroyMode.IndividualAndArea) screenSpaceSelector.gameObject.SetActive(false);

            if (Vector3.Distance(destructionBoxAreaEndPosition, destructionBoxAreaStartPosition) < 1 || destroyMode == DestroyMode.Individual) 
            {
                InvokeDestoryBuildableObject();
                return;
            }

            float minX = screenSpaceSelectorBox.anchoredPosition.x - (screenSpaceSelectorBox.sizeDelta.x / 2);
            float maxX = screenSpaceSelectorBox.anchoredPosition.x + (screenSpaceSelectorBox.sizeDelta.x / 2);
            float minY = screenSpaceSelectorBox.anchoredPosition.y - (screenSpaceSelectorBox.sizeDelta.y / 2);
            float maxY = screenSpaceSelectorBox.anchoredPosition.y + (screenSpaceSelectorBox.sizeDelta.y / 2);

            if (gridManager.TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager))
            {
                foreach (BuildableObject buildableObject in gridBuiltObjectsManager.GetBuiltObjectsList())
                {
                    if (!buildableObject.GetBuildableObjectSO().isObjectDestructable) continue;
                    if (IsDestructionBlockedByDestructableObjectType(buildableObject)) continue;
                    if ((destructableObjectsLayerMask.value & (1 << buildableObject.gameObject.layer)) == 0) continue;

                    Vector3 screenPosition = MouseInteractionUtilities.GetWorldToScreenPosition(buildableObject.transform.position);
                    if (screenPosition.x > minX - areaDestructionPadding && screenPosition.x < maxX + areaDestructionPadding && screenPosition.y > minY - areaDestructionPadding && screenPosition.y < maxY + areaDestructionPadding)
                    {
                        if (!destructableObjectsList.Contains(buildableObject)) destructableObjectsList.Add(buildableObject);
                    }
                }

                for (int i = destructableObjectsList.Count - 1; i >= 0; i--)
                {
                    BuildableObject buildableObject = destructableObjectsList[i];
                    SetInputDestroyBuildableObject(buildableObject);
                    destructableObjectsList.RemoveAt(i);
                }
            }
        }

        private void InvokeDestoryBuildableObject()
        {
            if (MouseInteractionUtilities.TryGetBuildableObject(destructableObjectsLayerMask, out BuildableObject buildableObject))
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();

                if (activeEasyGridBuilderPro.GetUseDestroyModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.DestroyMode) return;
                if (IsDestructionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;
                if (IsDestructionBlockedByDestructableObjectType(buildableObject)) return;
                
                DestroyBuildableObject(buildableObject);
            }
        }

        public void SetInputDestroyBuildableObject(BuildableObject buildableObject, bool byPassBuildableDestroyerConditions = false, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            if (!byPassBuildableDestroyerConditions)
            {
                activeEasyGridBuilderPro = buildableObject.GetOccupiedGridSystem();

                if (activeEasyGridBuilderPro.GetUseDestroyModeActivationInput() && activeEasyGridBuilderPro.GetActiveGridMode() != GridMode.DestroyMode) return;
                if (IsDestructionBlockedByActiveGridMode(activeEasyGridBuilderPro.GetActiveGridMode())) return;
                if (IsDestructionBlockedByDestructableObjectType(buildableObject)) return;
            }
                
            DestroyBuildableObject(buildableObject, byPassEventsAndMessages, detachInstead);
        }

        private void DestroyBuildableObject(BuildableObject buildableObject, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            ICommand command;
            switch (buildableObject)
            {
                case BuildableGridObject buildableGridObject: 
                    command = new CommandDestroyBuildableGridObject(buildableGridObject, byPassEventsAndMessages, detachInstead);
                    gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                    if (!detachInstead && ((CommandDestroyBuildableGridObject)command).GetIsDestructionSuccessful()) gridManager.GetGridCommandInvoker().AddCommand(command);
                break;
                case BuildableEdgeObject buildableEdgeObject: 
                    command = new CommandDestroyBuildableEdgeObject(buildableEdgeObject, byPassEventsAndMessages, detachInstead);
                    gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                    if (!detachInstead && ((CommandDestroyBuildableEdgeObject)command).GetIsDestructionSuccessful()) gridManager.GetGridCommandInvoker().AddCommand(command);
                break;
                case BuildableCornerObject buildableCornerObject: 
                    command = new CommandDestroyBuildableCornerObject(buildableCornerObject, byPassEventsAndMessages, detachInstead);
                    gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                    if (!detachInstead && ((CommandDestroyBuildableCornerObject)command).GetIsDestructionSuccessful()) gridManager.GetGridCommandInvoker().AddCommand(command);
                break;
                case BuildableFreeObject buildableFreeObject: 
                    command = new CommandDestroyBuildableFreeObject(buildableFreeObject, byPassEventsAndMessages, detachInstead);
                    gridManager.GetGridCommandInvoker().ExecuteCommand(command);
                    if (!detachInstead && ((CommandDestroyBuildableFreeObject)command).GetIsDestructionSuccessful()) gridManager.GetGridCommandInvoker().AddCommand(command);
                break;
            }
        }

        public void SetInputDestroyAllBuildableObjectInScene(bool byPassBuildableDestroyerConditions = false, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            if (!gridManager.TryGetGridBuiltObjectsManager(out GridBuiltObjectsManager gridBuiltObjectsManager)) return;
            List<BuildableObject> builtObjectsList = gridBuiltObjectsManager.GetBuiltObjectsList();
            for (int i = builtObjectsList.Count - 1; i >= 0; i--)
            {
                BuildableObject buildableObject = builtObjectsList[i];
                SetInputDestroyBuildableObject(buildableObject, byPassBuildableDestroyerConditions, byPassEventsAndMessages, detachInstead);
            }
        }
        #endregion Handle Input Buildable Object Destruction Functions End:

        ///-------------------------------------------------------------------------------///
        /// INPUT EXECUTION FUNCTIONS                                                     ///
        ///-------------------------------------------------------------------------------///
        
        #region Initiate Destroy Buildable Objects Functions Start:
        public bool TryDestroyBuildableGridObject(BuildableGridObject buildableGridObject, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            bool destructionSuccessful = false;

            BuildableGridObjectSO buildableGridObjectSO = (BuildableGridObjectSO)buildableGridObject.GetBuildableObjectSO();
            if (!buildableGridObjectSO.isObjectDestructable) return destructionSuccessful;

            List<Vector2Int> cellPositionList = buildableGridObject.GetObjectCellPositionList();
            int verticalGridIndex = buildableGridObject.GetOccupiedVerticalGridIndex();

            foreach (Vector2Int cellPosition in cellPositionList)
            {
                GridCellData gridCellData =  activeEasyGridBuilderPro.GetCellData(cellPosition, verticalGridIndex);                                                                             // Get the GridCellData Copy
                Dictionary<BuildableGridObjectCategorySO, BuildableGridObject> buildableGridObjects = gridCellData.GetBuildableGridObjectData();                                                // Initialize the dictionary
                if (buildableGridObjects.ContainsKey(buildableGridObjectSO.buildableGridObjectCategorySO)) buildableGridObjects.Remove(buildableGridObjectSO.buildableGridObjectCategorySO);    // Modify the data
                activeEasyGridBuilderPro.SetCellData(cellPosition, verticalGridIndex, gridCellData);                                                                                            // Write the modified GridCellData back to the original
            }

            if (!detachInstead) 
            {
                Destroy(buildableGridObject.gameObject);
                destructionSuccessful = true;
            }

            if (!byPassEventsAndMessages) OnBuildableObjectDestroyed?.Invoke(activeEasyGridBuilderPro, buildableGridObject);
            OnBuildableObjectDestroyedInternal?.Invoke(activeEasyGridBuilderPro, buildableGridObject);

            return destructionSuccessful;
        }

        public bool TryDestroyBuildableEdgeObject(BuildableEdgeObject buildableEdgeObject, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            bool destructionSuccessful = false;
            
            BuildableEdgeObjectSO buildableEdgeObjectSO = (BuildableEdgeObjectSO)buildableEdgeObject.GetBuildableObjectSO();
            if (!buildableEdgeObjectSO.isObjectDestructable) return destructionSuccessful;;

            Dictionary<Vector2Int, EdgeObjectCellDirection> cellPositionDictionary = buildableEdgeObject.GetCellPositionDictionary();
            int verticalGridIndex = buildableEdgeObject.GetOccupiedVerticalGridIndex();

            foreach (KeyValuePair<Vector2Int, EdgeObjectCellDirection> cellPosition in cellPositionDictionary)
            {
                GridCellData gridCellData =  activeEasyGridBuilderPro.GetCellData(cellPosition.Key, verticalGridIndex);                                                                         // Get the GridCellData Copy
                Dictionary<BuildableEdgeObjectCategorySO, BuildableEdgeObject> buildableEdgeObjects = default;

                switch (cellPosition.Value)
                {
                    case EdgeObjectCellDirection.North: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectNorthData(); break;                                                           // Initialize the dictionary
                    case EdgeObjectCellDirection.East: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectEastData(); break;                                                             // Initialize the dictionary
                    case EdgeObjectCellDirection.South: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectSouthData(); break;                                                           // Initialize the dictionary    
                    case EdgeObjectCellDirection.West: buildableEdgeObjects = gridCellData.GetBuildableEdgeObjectWestData(); break;                                                             // Initialize the dictionary
                }

                if (buildableEdgeObjects.ContainsKey(buildableEdgeObjectSO.buildableEdgeObjectCategorySO)) buildableEdgeObjects.Remove(buildableEdgeObjectSO.buildableEdgeObjectCategorySO);    // Modify the data
                activeEasyGridBuilderPro.SetCellData(cellPosition.Key, verticalGridIndex, gridCellData);                                                                                        // Write the modified GridCellData back to the original
            }

            if (!detachInstead) 
            {
                Destroy(buildableEdgeObject.gameObject);
                destructionSuccessful = true;
            }

            if (!byPassEventsAndMessages) OnBuildableObjectDestroyed?.Invoke(activeEasyGridBuilderPro, buildableEdgeObject);
            OnBuildableObjectDestroyedInternal?.Invoke(activeEasyGridBuilderPro, buildableEdgeObject);

            return destructionSuccessful;
        }

        public bool TryDestroyBuildableCornerObject(BuildableCornerObject buildableCornerObject, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            bool destructionSuccessful = false;

            BuildableCornerObjectSO buildableCornerObjectSO = (BuildableCornerObjectSO)buildableCornerObject.GetBuildableObjectSO();
            if (!buildableCornerObjectSO.isObjectDestructable) return destructionSuccessful;

            Dictionary<Vector2Int, CornerObjectCellDirection> cellPositionDictionary = buildableCornerObject.GetCellPositionDictionary();
            int verticalGridIndex = buildableCornerObject.GetOccupiedVerticalGridIndex();

            foreach (KeyValuePair<Vector2Int, CornerObjectCellDirection> cellPosition in cellPositionDictionary)
            {
                GridCellData gridCellData =  activeEasyGridBuilderPro.GetCellData(cellPosition.Key, verticalGridIndex);                                                                                     // Get the GridCellData Copy
                Dictionary<BuildableCornerObjectCategorySO, BuildableCornerObject> buildableCornerObjects = default;

                switch (cellPosition.Value)
                {
                    case CornerObjectCellDirection.NorthEast: buildableCornerObjects = gridCellData.GetBuildableCornerObjectNorthEastData(); break;                                                         // Initialize the dictionary
                    case CornerObjectCellDirection.SouthEast: buildableCornerObjects = gridCellData.GetBuildableCornerObjectSouthEastData(); break;                                                         // Initialize the dictionary
                    case CornerObjectCellDirection.SouthWest: buildableCornerObjects = gridCellData.GetBuildableCornerObjectSouthWestData(); break;                                                         // Initialize the dictionary
                    case CornerObjectCellDirection.NorthWest: buildableCornerObjects = gridCellData.GetBuildableCornerObjectNorthWestData(); break;                                                         // Initialize the dictionary
                }

                if (buildableCornerObjects.ContainsKey(buildableCornerObjectSO.buildableCornerObjectCategorySO)) buildableCornerObjects.Remove(buildableCornerObjectSO.buildableCornerObjectCategorySO);    // Modify the data
                activeEasyGridBuilderPro.SetCellData(cellPosition.Key, verticalGridIndex, gridCellData);                                                                                                    // Write the modified GridCellData back to the original
            }

            if (!detachInstead) 
            {
                Destroy(buildableCornerObject.gameObject);
                destructionSuccessful = true;
            }

            if (!byPassEventsAndMessages) OnBuildableObjectDestroyed?.Invoke(activeEasyGridBuilderPro, buildableCornerObject);
            OnBuildableObjectDestroyedInternal?.Invoke(activeEasyGridBuilderPro, buildableCornerObject);

            return destructionSuccessful;
        }

        public bool TryDestroyBuildableFreeObject(BuildableFreeObject buildableFreeObject, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            bool destructionSuccessful = false;
            
            BuildableFreeObjectSO buildableFreeObjectSO = (BuildableFreeObjectSO)buildableFreeObject.GetBuildableObjectSO();
            if (!buildableFreeObjectSO.isObjectDestructable) return destructionSuccessful;

            Vector2Int cellPosition = buildableFreeObject.GetObjectCellPositionList()[0];
            int verticalGridIndex = buildableFreeObject.GetOccupiedVerticalGridIndex();

            GridCellData gridCellData =  activeEasyGridBuilderPro.GetCellData(cellPosition, verticalGridIndex);                                             // Get the GridCellData Copy
            List<BuildableFreeObject> buildableFreeObjects = gridCellData.GetBuildableFreeObjectData();                                                     // Initialize the dictionary
            if (buildableFreeObjects.Contains(buildableFreeObject)) buildableFreeObjects.Remove(buildableFreeObject);                                       // Modify the data
            activeEasyGridBuilderPro.SetCellData(cellPosition, verticalGridIndex, gridCellData);                                                            // Write the modified GridCellData back to the original

            if (!detachInstead) 
            {
                Destroy(buildableFreeObject.gameObject);
                destructionSuccessful = true;
            }

            if (!byPassEventsAndMessages) OnBuildableObjectDestroyed?.Invoke(activeEasyGridBuilderPro, buildableFreeObject);
            OnBuildableObjectDestroyedInternal?.Invoke(activeEasyGridBuilderPro, buildableFreeObject);

            return destructionSuccessful;
        }

        public bool TryDestroyBuildableGridObjectByUniqueID(string uniqueID, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            BuildableGridObject buildableGridObject = FindBuildableGridObjectByID(uniqueID);
            if (buildableGridObject != null) return TryDestroyBuildableGridObject(buildableGridObject, byPassEventsAndMessages, detachInstead);
            return false;
        }

        public bool TryDestroyBuildableEdgeObjectByUniqueID(string uniqueID, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            BuildableEdgeObject buildableEdgeObject = FindBuildableEdgeObjectByID(uniqueID);
            if (buildableEdgeObject != null) return TryDestroyBuildableEdgeObject(buildableEdgeObject, byPassEventsAndMessages, detachInstead);
            return false;
        }

        public bool TryDestroyBuildableCornerObjectByUniqueID(string uniqueID, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            BuildableCornerObject buildableCornerObject = FindBuildableCornerObjectByID(uniqueID);
            if (buildableCornerObject != null) return TryDestroyBuildableCornerObject(buildableCornerObject, byPassEventsAndMessages, detachInstead);
            return false;
        }

        public bool TryDestroyBuildableFreeObjectByUniqueID(string uniqueID, bool byPassEventsAndMessages = false, bool detachInstead = false)
        {
            BuildableFreeObject buildableFreeObject = FindBuildableFreeObjectByID(uniqueID); 
            if (buildableFreeObject != null) return TryDestroyBuildableFreeObject(buildableFreeObject, byPassEventsAndMessages, detachInstead);
            return false;
        }

        private BuildableGridObject FindBuildableGridObjectByID(string uniqueID)
        {
            foreach (BuildableGridObject buildableGridObject in FindObjectsByType<BuildableGridObject>(FindObjectsSortMode.None))
            {
                if (buildableGridObject.GetUniqueID() == uniqueID) return buildableGridObject;
            }
            return null;
        }

        private BuildableEdgeObject FindBuildableEdgeObjectByID(string uniqueID)
        {
            foreach (BuildableEdgeObject buildableEdgeObject in FindObjectsByType<BuildableEdgeObject>(FindObjectsSortMode.None))
            {
                if (buildableEdgeObject.GetUniqueID() == uniqueID) return buildableEdgeObject;
            }
            return null;
        }

        private BuildableCornerObject FindBuildableCornerObjectByID(string uniqueID)
        {
            foreach (BuildableCornerObject buildableCornerObject in FindObjectsByType<BuildableCornerObject>(FindObjectsSortMode.None))
            {
                if (buildableCornerObject.GetUniqueID() == uniqueID) return buildableCornerObject;
            }
            return null;
        }

        private BuildableFreeObject FindBuildableFreeObjectByID(string uniqueID)
        {
            foreach (BuildableFreeObject buildableFreeObject in FindObjectsByType<BuildableFreeObject>(FindObjectsSortMode.None))
            {
                if (buildableFreeObject.GetUniqueID() == uniqueID) return buildableFreeObject;
            }
            return null;
        }
        #endregion Initiate Destroy Buildable Objects Functions End:
    
        public void SetDestroyMode(DestroyMode destroyMode)
        {
            if (this.destroyMode != destroyMode) this.destroyMode = destroyMode;
        }
    }
}