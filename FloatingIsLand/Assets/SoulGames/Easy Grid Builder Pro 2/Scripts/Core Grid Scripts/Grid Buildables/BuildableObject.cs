using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace SoulGames.EasyGridBuilderPro
{
    public abstract class BuildableObject : MonoBehaviour
    {
        [SerializeField] protected bool enableUnityEvents;
        [SerializeField] public UnityEvent OnHoverEnterByBuildableObjectDestroyerUnityEvent;
        [SerializeField] public UnityEvent OnHoverExitByBuildableObjectDestroyerUnityEvent;
        [SerializeField] public UnityEvent OnHoverEnterByBuildableObjectSelectorUnityEvent;
        [SerializeField] public UnityEvent OnHoverExitByBuildableObjectSelectorUnityEvent;
        [SerializeField] public UnityEvent OnSelectedByBuildableObjectSelectorUnityEvent;
        [SerializeField] public UnityEvent OnDeselectedByBuildableObjectSelectorUnityEvent;
        [SerializeField] public UnityEvent OnHoverEnterByBuildableObjectMoverUnityEvent;
        [SerializeField] public UnityEvent OnHoverExitByBuildableObjectMoverUnityEvent;
        [SerializeField] public UnityEvent OnStartMovingByBuildableObjectMoverUnityEvent;
        [SerializeField] public UnityEvent OnEndMovingByBuildableObjectMoverUnityEvent;

        private string uniqueID;

        private BuildableObjectDestroyer buildableObjectDestroyer;
        private BuildableObjectSelector buildableObjectSelector;
        private BuildableObjectMover buildableObjectMover;

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT EDITOR FUNCTIONS                                             ///
        ///-------------------------------------------------------------------------------///
        
        #if UNITY_EDITOR
        public abstract void AutoCalculatePivotAndSize();
        public abstract void AutoCalculateSize();
        #endif

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT INITIALIZE FUNCTIONS                                         ///
        ///-------------------------------------------------------------------------------///
        
        protected virtual void Awake()
        {
            uniqueID = Guid.NewGuid().ToString();
        }

        protected virtual void OnEnable()
        {
            StartCoroutine(LateStart());
        }

        protected virtual void OnDisable()
        {
            UnsubscribeFromEvents();
        }
        
        #region Buildable Object Initialization Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            if (GridManager.Instance.TryGetBuildableObjectDestroyer(out buildableObjectDestroyer))
            {
                buildableObjectDestroyer.OnBuildableObjectHoverEnter += OnHoverEnterByBuildableObjectDestroyerDelegate;
                buildableObjectDestroyer.OnBuildableObjectHoverExit += OnHoverExitByBuildableObjectDestroyerDelegate;
            }

            if (GridManager.Instance.TryGetBuildableObjectSelector(out buildableObjectSelector))
            {
                buildableObjectSelector.OnBuildableObjectHoverEnter += OnHoverEnterByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectHoverExit += OnHoverExitByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectSelected += OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected += OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (GridManager.Instance.TryGetBuildableObjectMover(out buildableObjectMover))
            {
                buildableObjectMover.OnBuildableObjectHoverEnter += OnHoverEnterByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectHoverExit += OnHoverExitByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectStartMoving += OnBuildableObjectStartMovingByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectEndMoving += OnBuildableObjectEndMovingByBuildableObjectMoverDelegate;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (buildableObjectDestroyer)
            {
                buildableObjectDestroyer.OnBuildableObjectHoverEnter -= OnHoverEnterByBuildableObjectDestroyerDelegate;
                buildableObjectDestroyer.OnBuildableObjectHoverExit -= OnHoverExitByBuildableObjectDestroyerDelegate;
            }

            if (buildableObjectSelector)
            {
                buildableObjectSelector.OnBuildableObjectHoverEnter -= OnHoverEnterByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectHoverExit -= OnHoverExitByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectSelected -= OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected -= OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (buildableObjectMover)
            {
                buildableObjectMover.OnBuildableObjectHoverEnter -= OnHoverEnterByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectHoverExit -= OnHoverExitByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectStartMoving -= OnBuildableObjectStartMovingByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectEndMoving -= OnBuildableObjectEndMovingByBuildableObjectMoverDelegate;
            }
        }
        #endregion Buildable Object Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT EVENT HANDLER FUNCTIONS                                      ///
        ///-------------------------------------------------------------------------------///
        
        #region Buildable Object Destroyer Events Start:
        private void OnHoverEnterByBuildableObjectDestroyerDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnHoverEnterByBuildableObjectDestroyerUnityEvent?.Invoke();
        }

        private void OnHoverExitByBuildableObjectDestroyerDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnHoverExitByBuildableObjectDestroyerUnityEvent?.Invoke();
        }
        #endregion Buildable Object Destroyer Events End:

        #region Buildable Object Selector Events Start:
        private void OnHoverEnterByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnHoverEnterByBuildableObjectSelectorUnityEvent?.Invoke();
        }

        private void OnHoverExitByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnHoverExitByBuildableObjectSelectorUnityEvent?.Invoke();
        }

        private void OnSelectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnSelectedByBuildableObjectSelectorUnityEvent?.Invoke();
        }

        private void OnDeselectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnDeselectedByBuildableObjectSelectorUnityEvent?.Invoke();
        }
        #endregion Buildable Object Selector Events End:

        #region Buildable Object Mover Events Start:
        private void OnHoverEnterByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnHoverEnterByBuildableObjectMoverUnityEvent?.Invoke();
        }

        private void OnHoverExitByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnHoverExitByBuildableObjectMoverUnityEvent?.Invoke();
        }

        private void OnBuildableObjectStartMovingByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnStartMovingByBuildableObjectMoverUnityEvent?.Invoke();
        }

        private void OnBuildableObjectEndMovingByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (enableUnityEvents && buildableObject == this) OnEndMovingByBuildableObjectMoverUnityEvent?.Invoke();
        }
        #endregion Buildable Object Mover Events End:
    
        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        #region Public Getter Functions Start:
        public string GetUniqueID() =>  uniqueID;
        public abstract bool GetIsObjectBuilt();
        
        public abstract GridAxis GetGridAxis();
        public abstract bool GetIsActivateGizmos();
        public abstract bool GetLockAutoGenerationAndValues();
        public abstract Vector3 GetObjectScale();
        public abstract Vector3 GetObjectCenter();
        public abstract Vector3 GetObjectCustomPivot();
        public abstract float GetDebugCellSize();
        public abstract Vector2Int GetObjectSizeRelativeToCellSize();
        public abstract bool GetIsActiveSceneObject(out BuildableObjectSO buildableObjectSO);
        public abstract void GetSceneObjectRotation(out FourDirectionalRotation fourDirectionalRotation, out EightDirectionalRotation eightDirectionalRotation, out float freeRotation);
        public abstract int GetSceneObjectVerticalGridIndex();

        public abstract bool GetIsInstantiatedByGhostObject();

        public abstract EasyGridBuilderPro GetOccupiedGridSystem();
        public abstract int GetOccupiedVerticalGridIndex();
        public abstract float GetOccupiedCellSize();
        public abstract BuildableObjectSO GetBuildableObjectSO();
        public abstract BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab();
        public abstract Vector3 GetObjectOriginWorldPosition();
        public abstract Vector3 GetObjectModifiedOriginWorldPosition();
        public abstract Vector3 GetObjectOffset();
        public abstract Vector2Int GetObjectOriginCellPosition(out List<Vector2Int> objectCellPositionList);
        public abstract List<Vector2Int> GetObjectCellPositionList();
        public abstract CornerObjectCellDirection GetCornerObjectOriginCellDirection();
        public abstract FourDirectionalRotation GetObjectFourDirectionalRotation();
        public abstract EightDirectionalRotation GetObjectEightDirectionalRotation();
        public abstract float GetObjectFreeRotation();
        public abstract bool GetIsObjectFlipped();
        public abstract Vector3 GetObjectHitNormals();
        public abstract bool GetIsObjectInvokedAsSecondaryPlacement();
        #endregion Public Getter Functions End:

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        #region Public Setter Functions Start:
        public void SetUniqueID(string uniqueID) => this.uniqueID = uniqueID;

        public abstract void SetGridAxis(GridAxis gridAxis);
        public abstract void SetIsActivateGizmos(bool activateGizmos);
        public abstract void SetLockAutoGenerationAndValues(bool lockAutoGenerationAndValues);
        public abstract void SetObjectScale(Vector3 objectScale);
        public abstract void SetObjectCenter(Vector3 objectCenter);
        public abstract void SetObjectCustomPivot(Vector3 objectCustomPivot);
        public abstract void SetDebugCellSize(float debugCellSize);
        public abstract void SetObjectSizeRelativeToCellSize(Vector2Int objectSizeRelativeToCellSize);
        public abstract void SetIsActiveSceneObject(bool activeSceneObject);
        public abstract void SetSceneObjectBuildableObjectSO(BuildableObjectSO buildableObjectSO);
        public abstract void SetSceneObjectRotation(FourDirectionalRotation fourDirectionalRotation = default, EightDirectionalRotation eightDirectionalRotation = default, float freeRotation = default);
        public abstract void SetSceneObjectVerticalGridIndex(int verticalGridIndex);

        public abstract void SetIsInstantiatedByGhostObject(bool isInstantiatedByGhostObject);

        public abstract void SetOccupiedGridSystem(EasyGridBuilderPro easyGridBuilderPro);
        public abstract void SetOccupiedVerticalGridIndex(int occupiedVerticalGridIndex);
        public abstract void SetOccupiedCellSize(float cellSize);
        public abstract void SetBuildableObjectSO(BuildableObjectSO buildableObjectSO);
        public abstract void SetBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab);
        public abstract void SetObjectOriginWorldPosition(Vector3 objectOriginWorldPosition);
        public abstract void SetObjectModifiedOriginWorldPosition(Vector3 objectModifiedOriginWorldPosition);
        public abstract void SetObjectOffset(Vector3 objectOffset);
        public abstract void SetObjectOriginCellPosition(Vector2Int objectOriginCellPosition);
        public abstract void SetObjectCellPositionList(List<Vector2Int> objectCellPositionList);
        public abstract void SetCornerObjectOriginCellDirection(CornerObjectCellDirection cornerObjectCellDirection);
        public abstract void SetObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation);
        public abstract void SetObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation);
        public abstract void SetObjectFreeRotation(float freeRotation);
        public abstract void SetIsObjectFlipped(bool isFlipped);
        public abstract void SetObjectHitNormals(Vector3 hitNormals);
        public abstract void SetIsObjectInvokedAsSecondaryPlacement(bool isInvokedAsSecondaryPlacement);
        #endregion Public Setter Functions End:
    }
}