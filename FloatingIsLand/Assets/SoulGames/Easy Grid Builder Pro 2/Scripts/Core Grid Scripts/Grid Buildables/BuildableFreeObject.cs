using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;
using Unity.Mathematics;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Buildables/Buildable Free Object", 4)]
    public class BuildableFreeObject : BuildableObject
    {
        [SerializeField] private GridAxis gridAxis = GridAxis.XZ;
        [SerializeField] private bool activateGizmos = true;

        [SerializeField] private bool lockAutoGenerationAndValues;
        [SerializeField] private Vector3 objectScale;
        [SerializeField] private Vector3 objectCenter;
        [SerializeField] private Vector3 objectCustomPivot;
        
        [SerializeField] private bool activeSceneObject;
        [SerializeField] private BuildableFreeObjectSO sceneObjectBuildableFreeObjectSO;
        [SerializeField] private FourDirectionalRotation sceneObjectFourDirectionalRotation;
        [SerializeField] private EightDirectionalRotation sceneObjectEightDirectionalRotation;
        [SerializeField] private float sceneObjectFreeRotation;
        [SerializeField] private int verticalGridIndex;

        private bool isInstantiatedByGhostObject;

        //Usable Data
        private EasyGridBuilderPro occupiedGridSystem;
        private int occupiedVerticalGridIndex;
        private float occupiedCellSize;
        private BuildableFreeObjectSO buildableFreeObjectSO;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private Vector3 objectOriginWorldPosition;
        private Vector2Int occupiedCellPosition;
        private FourDirectionalRotation objectFourDirectionalRotation;
        private EightDirectionalRotation objectEightDirectionalRotation;
        private float objectFreeRotation;
        private Vector3 objectHitNormals;

        private  void Start()
        {
            if (activeSceneObject && !occupiedGridSystem) StartCoroutine(LateStart());
        }

        protected override void OnEnable()
        {
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }
        
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            Vector3 objectWorldPosition = transform.position;
            LayerMask combinedLayerMask = GridManager.Instance.GetGridSystemLayerMask() | sceneObjectBuildableFreeObjectSO.customSurfaceLayerMask;

            if (TryGetHit(objectWorldPosition, GridManager.Instance.GetGridSystemLayerMask(), out RaycastHit hit))
            {
                if (hit.collider.TryGetComponent(out EasyGridBuilderPro easyGridBuilderPro)) 
                {
                    if (verticalGridIndex >= easyGridBuilderPro.GetVerticalGridsCount()) 
                    {
                        Debug.Log($"Buildable Free Object: {this.name}: <color=red><b>Invalid Vertical Grid Index! Scene Object Spawn Terminated!</b></color>"); yield break;
                    }
                    easyGridBuilderPro.SetActiveVerticalGrid(verticalGridIndex);
                }
            }
            
            yield return new WaitForEndOfFrame();

            if (TryGetHit(objectWorldPosition, combinedLayerMask, out hit))
            {
                float objectHeight = gridAxis is GridAxis.XZ ? hit.point.y : hit.point.z;

                if (hit.collider.TryGetComponent(out EasyGridBuilderPro easyGridBuilderPro)) InvokeBuildableObjectPlacement(easyGridBuilderPro, objectWorldPosition, objectHeight);
                else if (TryGetHit(hit.point, GridManager.Instance.GetGridSystemLayerMask(), out hit) && hit.collider.TryGetComponent(out easyGridBuilderPro))
                {
                    InvokeBuildableObjectPlacement(easyGridBuilderPro, objectWorldPosition, objectHeight);
                }
            }
            Destroy(gameObject);
        }

        private bool TryGetHit(Vector3 origin, LayerMask layerMask, out RaycastHit hit)
        {
            Vector3 rayOrigin = gridAxis is GridAxis.XZ ? origin + new Vector3(0, objectCenter.y, 0) : origin + new Vector3(0, objectCenter.z, 0);
            Vector3 rayDirection = gridAxis is GridAxis.XZ ? Vector3.down : Vector3.forward;

            return Physics.Raycast(rayOrigin, rayDirection, out hit, Mathf.Infinity, layerMask);
        }

        private void InvokeBuildableObjectPlacement(EasyGridBuilderPro easyGridBuilderPro, Vector3 objectWorldPosition, float objectHeight)
        {
            objectWorldPosition = gridAxis is GridAxis.XZ ? new Vector3(objectWorldPosition.x, objectWorldPosition.y + objectHeight, objectWorldPosition.z) :
                new Vector3(objectWorldPosition.x, objectWorldPosition.y, objectWorldPosition.z + objectHeight);
            easyGridBuilderPro.SetInputBuildableObjectPlacement(true, false, sceneObjectBuildableFreeObjectSO, objectWorldPosition, sceneObjectFourDirectionalRotation, sceneObjectEightDirectionalRotation,
            sceneObjectFreeRotation, activeVerticalGridIndex: verticalGridIndex);
        }

        public BuildableFreeObject SetupBuildableFreeObject(EasyGridBuilderPro gridSystem, int verticalGridIndex, float cellSize, BuildableFreeObjectSO buildableFreeObjectSO,
            BuildableObjectSO.RandomPrefabs randomPrefab, Vector3 originWorldPosition, Vector2Int occupiedCellPosition, FourDirectionalRotation fourDirectionalRotation,
            EightDirectionalRotation eightDirectionalRotation, float freeRotation, Vector3 hitNormals)
        {
            SetProperties(gridSystem, verticalGridIndex, cellSize, buildableFreeObjectSO, randomPrefab, originWorldPosition, occupiedCellPosition, fourDirectionalRotation, eightDirectionalRotation, freeRotation, hitNormals);
            HandleTerrainInteractions();
            ConfigureTransform(buildableFreeObjectSO);
            ApplyLayerIfNeeded();

            return this;
        }

        /// <summary>
        /// Sets the initial properties for the buildable grid object.
        /// </summary>
        private void SetProperties(EasyGridBuilderPro gridSystem, int verticalGridIndex, float cellSize, BuildableFreeObjectSO buildableFreeObjectSO, BuildableObjectSO.RandomPrefabs randomPrefab,
            Vector3 originWorldPosition, Vector2Int occupiedCellPosition, FourDirectionalRotation fourDirectionalRotation, EightDirectionalRotation eightDirectionalRotation, float freeRotation, Vector3 hitNormals)
        {
            this.occupiedGridSystem = gridSystem;
            this.occupiedVerticalGridIndex = verticalGridIndex;
            this.occupiedCellSize = cellSize;
            this.buildableFreeObjectSO = buildableFreeObjectSO;
            this.buildableObjectSORandomPrefab = randomPrefab;
            this.objectOriginWorldPosition = originWorldPosition;
            this.occupiedCellPosition = occupiedCellPosition;
            this.objectFourDirectionalRotation = fourDirectionalRotation;
            this.objectEightDirectionalRotation = eightDirectionalRotation;
            this.objectFreeRotation = freeRotation;
            this.objectHitNormals = hitNormals;
        }

        private void HandleTerrainInteractions()
        {
            if (occupiedGridSystem is not EasyGridBuilderProXZ) return;
            
            Vector3 rayOrigin = objectOriginWorldPosition + Vector3.up * 1000;
            Vector3 rayDirection = Vector3.down * 9999;

            if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, Mathf.Infinity, buildableFreeObjectSO.customSurfaceLayerMask | GridManager.Instance.GetGridSystemLayerMask()))
            {
                if (buildableFreeObjectSO.enableTerrainInteractions == false || !hit.collider.TryGetComponent<Terrain>(out Terrain terrain)) return;
                if (buildableFreeObjectSO.flattenTerrain) TerrainInteractionUtilities.FlattenTerrain(terrain, objectOriginWorldPosition, buildableFreeObjectSO.flattenerSize);
                if (buildableFreeObjectSO.removeTerrainDetails) TerrainInteractionUtilities.ClearTerrainDetails(terrain, objectOriginWorldPosition, buildableFreeObjectSO.detailsRemoverSize);
                if (buildableFreeObjectSO.paintTerrainTexture) TerrainInteractionUtilities.PaintTerrainWithTexture(terrain, objectOriginWorldPosition, buildableFreeObjectSO.painterBrushSize, 
                        buildableFreeObjectSO.terrainTextureIndex, buildableFreeObjectSO.painterBrushOpacity, buildableFreeObjectSO.painterBrushType, buildableFreeObjectSO.painterBrushFallOff);
            }
        }

        /// <summary>
        /// Configures the transform of the buildable grid object.
        /// </summary>
        private void ConfigureTransform(BuildableFreeObjectSO buildableFreeObjectSO)
        {
            transform.name = transform.name.Replace("(Clone)", "").Trim();

            float rotation = 0f;
            switch (buildableFreeObjectSO.rotationType)
            {
                case FreeObjectRotationType.FourDirectionalRotation: rotation = this.buildableFreeObjectSO.GetFourDirectionalRotationAngle(objectFourDirectionalRotation); break;
                case FreeObjectRotationType.EightDirectionalRotation: rotation = this.buildableFreeObjectSO.GetEightDirectionalRotationAngle(objectEightDirectionalRotation); break;
                case FreeObjectRotationType.FreeRotation: rotation = objectFreeRotation; break;
            }

            if (buildableFreeObjectSO.placementType == FreeObjectPlacementType.SplinePlacement && buildableFreeObjectSO.objectRotateToSplineDirection) rotation = objectFreeRotation;

            if (buildableFreeObjectSO.faceCollidingSurfaceNormalDirection && objectHitNormals != Vector3.zero) transform.rotation = Quaternion.LookRotation(objectHitNormals);
            else transform.localEulerAngles = occupiedGridSystem is EasyGridBuilderProXZ ? new Vector3(0, rotation, 0) : new Vector3(0, 0, rotation);

            if (buildableFreeObjectSO.raiseObjectWithVerticalGrids) 
            {
                if (occupiedGridSystem is EasyGridBuilderProXZ) objectOriginWorldPosition +=  new Vector3(0, occupiedVerticalGridIndex * occupiedGridSystem.GetVerticalGridHeight(), 0);
                else objectOriginWorldPosition -=  new Vector3(0, 0, occupiedVerticalGridIndex * occupiedGridSystem.GetVerticalGridHeight());
            }
            transform.localPosition = objectOriginWorldPosition;
            
            if (buildableFreeObjectSO.setGridSystemAsParent) transform.parent = occupiedGridSystem.transform;
        }

        /// <summary>
        /// Applies the specified layer to the object and its children if needed.
        /// </summary>
        private void ApplyLayerIfNeeded()
        {
            if (buildableFreeObjectSO.setSpawnedObjectLayer) SetLayerRecursive(gameObject, GetHighestLayerSet(buildableFreeObjectSO.spawnedObjectLayer));
        }

        /// <summary>
        /// Calculates the highest layer bit set in the ghostObjectLayer mask.
        /// </summary>
        /// <returns>The highest layer index set, or 0 if no layers are set.</returns>
        private int GetHighestLayerSet(LayerMask spawnedObjectLayer)
        {
            int highestLayer = 0;
            int layerMask = spawnedObjectLayer.value;

            while (layerMask > 0)
            {
                layerMask >>= 1; // Shift the bits to the right by 1
                highestLayer++;
            }

            return highestLayer > 1 ? highestLayer - 1 : 0;
        }

        /// <summary>
        /// Recursively sets the given layer to the target GameObject and all its children.
        /// </summary>
        /// <param name="targetGameObject">The GameObject whose layer will be set.</param>
        /// <param name="layer">The layer to apply to the GameObject and its children.</param>
        private void SetLayerRecursive(GameObject targetGameObject, int layer)
        {
            targetGameObject.layer = layer;
            foreach (Transform child in targetGameObject.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        public void SetBasicGridAreaTrigger()
        {
            if (!TryGetComponent<BasicGridAreaTrigger>(out _)) 
            {
                BasicGridAreaTrigger basicGridAreaTrigger = gameObject.AddComponent<BasicGridAreaTrigger>();
                basicGridAreaTrigger.InvokeBasicGridAreaTrigger(BasicGridAreaTriggerInvokerType.FreeObject);
            }
        }

        #if UNITY_EDITOR
        public override void AutoCalculatePivotAndSize()
        {
            Bounds bounds = CalculateBounds();
            AddBoxCollider(bounds);

            objectScale = bounds.size;
            objectCenter = bounds.center;

            switch (gridAxis)
            {
                case GridAxis.XZ: objectCustomPivot = new Vector3(objectCenter.x, objectCenter.y - objectScale.y / 2, objectCenter.z); break;
                case GridAxis.XY: objectCustomPivot = new Vector3(objectCenter.x, objectCenter.y, objectCenter.z + objectScale.z / 2); break;
            }
        }

        public override void AutoCalculateSize()
        {
            if (lockAutoGenerationAndValues) return;

            Bounds bounds = CalculateBounds();
            AddBoxCollider(bounds);

            objectScale = bounds.size;
            objectCenter = bounds.center;
        }
        #endif

        private Bounds CalculateBounds()
        {
            Bounds bounds = new Bounds();
            Renderer[] childRenderers = gameObject.GetComponentsInChildren<Renderer>();

            foreach (Renderer childRenderer in childRenderers)
            {
                if (bounds.size == Vector3.zero) bounds = childRenderer.bounds;
                else bounds.Encapsulate(childRenderer.bounds);
            }

            return bounds;
        }

        private void AddBoxCollider(Bounds bounds)
        {
            if (!TryGetComponent<BoxCollider>(out BoxCollider boxCollider)) boxCollider = gameObject.AddComponent<BoxCollider>();

            boxCollider.isTrigger = true;
            boxCollider.center = bounds.center;
            boxCollider.size = bounds.size;
        }

        ///-------------------------------------------------------------------------------///
        /// PUBLIC GETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        public override bool GetIsObjectBuilt() => occupiedGridSystem ? true : false;
        
        public override GridAxis GetGridAxis() => gridAxis;

        public override bool GetIsActivateGizmos() => activateGizmos;

        public override bool GetLockAutoGenerationAndValues() => lockAutoGenerationAndValues;

        public override Vector3 GetObjectScale() => objectScale;

        public override Vector3 GetObjectCenter() => objectScale;

        public override Vector3 GetObjectCustomPivot() => objectCustomPivot;

        public override float GetDebugCellSize() => default;

        public override Vector2Int GetObjectSizeRelativeToCellSize() => default;

        public override bool GetIsActiveSceneObject(out BuildableObjectSO buildableObjectSO)
        {
            buildableObjectSO = sceneObjectBuildableFreeObjectSO;
            return activeSceneObject;
        }

        public BuildableFreeObjectSO GetSceneObjectBuildableFreeObjectSO() => sceneObjectBuildableFreeObjectSO;

        public override void GetSceneObjectRotation(out FourDirectionalRotation fourDirectionalRotation, out EightDirectionalRotation eightDirectionalRotation, out float freeRotation)
        {
            fourDirectionalRotation = sceneObjectFourDirectionalRotation;
            eightDirectionalRotation = sceneObjectEightDirectionalRotation;
            freeRotation = sceneObjectFreeRotation;
        }

        public override int GetSceneObjectVerticalGridIndex() => verticalGridIndex;

        public override bool GetIsInstantiatedByGhostObject() => isInstantiatedByGhostObject;

        public override EasyGridBuilderPro GetOccupiedGridSystem() => occupiedGridSystem;

        public override int GetOccupiedVerticalGridIndex() => occupiedVerticalGridIndex;

        public override float GetOccupiedCellSize() => occupiedCellSize;

        public override BuildableObjectSO GetBuildableObjectSO() => buildableFreeObjectSO;

        public override BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;

        public override Vector2Int GetObjectOriginCellPosition(out List<Vector2Int> objectCellPositionList)
        {
            objectCellPositionList = null;
            return occupiedCellPosition;
        }

        public override List<Vector2Int> GetObjectCellPositionList() => new List<Vector2Int> { occupiedCellPosition };

        public override Vector3 GetObjectOriginWorldPosition() => objectOriginWorldPosition;

        public override Vector3 GetObjectModifiedOriginWorldPosition() => objectOriginWorldPosition;

        public override Vector3 GetObjectOffset() => default;

        public override CornerObjectCellDirection GetCornerObjectOriginCellDirection() => default;

        public override FourDirectionalRotation GetObjectFourDirectionalRotation() => objectFourDirectionalRotation;

        public override EightDirectionalRotation GetObjectEightDirectionalRotation() => objectEightDirectionalRotation;

        public override float GetObjectFreeRotation() => objectFreeRotation;

        public override bool GetIsObjectFlipped() => default;

        public override Vector3 GetObjectHitNormals() => objectHitNormals;

        public override bool GetIsObjectInvokedAsSecondaryPlacement() => default;

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        public override void SetIsInstantiatedByGhostObject(bool isInstantiatedByGhostObject) => this.isInstantiatedByGhostObject = isInstantiatedByGhostObject;

        public override void SetLockAutoGenerationAndValues(bool enable) => lockAutoGenerationAndValues = enable;

        public override void SetGridAxis(GridAxis gridAxis) => this.gridAxis = gridAxis;

        public override void SetObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation) => objectFourDirectionalRotation = fourDirectionalRotation;

        public override void SetObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation) => objectEightDirectionalRotation = eightDirectionalRotation;

        public override void SetObjectFreeRotation(float freeRotation) => objectFreeRotation = freeRotation;

        public override void SetIsActivateGizmos(bool activateGizmos) => this.activateGizmos = activateGizmos;

        public override void SetObjectScale(Vector3 objectScale) => this.objectScale = objectScale;

        public override void SetObjectCenter(Vector3 objectCenter) => this.objectCenter = objectCenter;

        public override void SetObjectCustomPivot(Vector3 objectCustomPivot) => this.objectCustomPivot = objectCustomPivot;

        public override void SetDebugCellSize(float debugCellSize) {}

        public override void SetObjectSizeRelativeToCellSize(Vector2Int objectSizeRelativeToCellSize) {}

        public override void SetIsActiveSceneObject(bool activeSceneObject) => this.activeSceneObject = activeSceneObject;

        public override void SetSceneObjectBuildableObjectSO(BuildableObjectSO buildableObjectSO) => this.sceneObjectBuildableFreeObjectSO = (BuildableFreeObjectSO)buildableObjectSO;

        public override void SetSceneObjectRotation(FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North, 
            EightDirectionalRotation eightDirectionalRotation = EightDirectionalRotation.North, float freeRotation = 0) 
        {
            this.objectFourDirectionalRotation = fourDirectionalRotation;
            this.objectEightDirectionalRotation = eightDirectionalRotation;
            this.objectFreeRotation = freeRotation;
        } 

        public override void SetSceneObjectVerticalGridIndex(int verticalGridIndex) => this.verticalGridIndex = verticalGridIndex;

        public override void SetOccupiedGridSystem(EasyGridBuilderPro easyGridBuilderPro) => this.occupiedGridSystem = easyGridBuilderPro;

        public override void SetOccupiedVerticalGridIndex(int occupiedVerticalGridIndex) => this.occupiedVerticalGridIndex = occupiedVerticalGridIndex;

        public override void SetOccupiedCellSize(float cellSize) => this.occupiedCellSize = cellSize;

        public override void SetBuildableObjectSO(BuildableObjectSO buildableObjectSO) => this.buildableFreeObjectSO = (BuildableFreeObjectSO)buildableObjectSO;

        public override void SetBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab) => this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;

        public override void SetObjectOriginWorldPosition(Vector3 objectOriginWorldPosition) => this.objectOriginWorldPosition = objectOriginWorldPosition;

        public override void SetObjectModifiedOriginWorldPosition(Vector3 objectModifiedOriginWorldPosition) {}

        public override void SetObjectOffset(Vector3 objectOffset) {}

        public override void SetObjectOriginCellPosition(Vector2Int objectOriginCellPosition) => this.occupiedCellPosition = objectOriginCellPosition;

        public override void SetObjectCellPositionList(List<Vector2Int> objectCellPositionList) {}

        public override void SetCornerObjectOriginCellDirection(CornerObjectCellDirection cornerObjectCellDirection) {}

        public override void SetIsObjectFlipped(bool isFlipped) {}

        public override void SetObjectHitNormals(Vector3 hitNormals) => this.objectHitNormals = hitNormals;

        public override void SetIsObjectInvokedAsSecondaryPlacement(bool isInvokedAsSecondaryPlacement) {}

        ///-------------------------------------------------------------------------------///
        /// EDITOR GIZMOS FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!activateGizmos) return;
            if (!Application.isPlaying)
            {
                DrawGizmosForBounds();
                DrawGizmosForCustomPivot();
                DrawGizmosForForwardAxis();
            }
        }

        private void DrawGizmosForBounds()
        {
            Vector3 center;
            if (activeSceneObject) center = transform.position + objectCenter;
            else center = objectCenter;

            CustomGizmosUtilities.DrawAAPolyWireCube(center, objectScale, quaternion.identity, 2f, Color.cyan);
        }

        private void DrawGizmosForCustomPivot()
        {
            Vector3 startPos;
            Vector3 endPos = default;

            if (activeSceneObject) startPos = transform.position + objectCustomPivot;
            else startPos = objectCustomPivot;

            switch (gridAxis)
            {
                case GridAxis.XZ: endPos = new Vector3(startPos.x, startPos.y + objectScale.y, startPos.z); break;
                case GridAxis.XY: endPos = new Vector3(startPos.x, startPos.y, startPos.z - objectScale.z); break;
            }

            float minScale = Mathf.Min(objectScale.x, objectScale.y, objectScale.z);
            CustomGizmosUtilities.DrawAAPolyArrow(endPos, startPos, minScale / 5, 25, 5, 2, Color.red);
            CustomGizmosUtilities.DrawAAPolyWireSphere(endPos, minScale / 20, 4, 2, Color.red);
        }

        private void DrawGizmosForForwardAxis()
        {
            Vector3 startPos;

            if (activeSceneObject) startPos = transform.position + objectCustomPivot;
            else startPos = objectCustomPivot;

            float maxScale = Mathf.Max(objectScale.x, objectScale.y, objectScale.z);
            float minScale = Mathf.Min(objectScale.x, objectScale.y, objectScale.z);
            
            Vector3 direction = gridAxis is GridAxis.XZ ? Vector3.forward : Vector3.up;
            CustomGizmosUtilities.DrawAAPolyArrow(startPos, startPos + direction * maxScale * 2, minScale, 25, 5, 2, Color.red);
        }
        #endif
    }
}