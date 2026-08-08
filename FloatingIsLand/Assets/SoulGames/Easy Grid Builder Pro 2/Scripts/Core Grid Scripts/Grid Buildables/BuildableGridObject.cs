using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Collections;
using SoulGames.Utilities;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Buildables/Buildable Grid Object", 1)]
    public class BuildableGridObject : BuildableObject
    {   
        [SerializeField] private GridAxis gridAxis = GridAxis.XZ;
        [SerializeField] private bool activateGizmos = true;

        [SerializeField] private bool lockAutoGenerationAndValues;
        [SerializeField] private Vector3 objectScale;
        [SerializeField] private Vector3 objectCenter;
        [SerializeField] private Vector3 objectCustomPivot;
        
        [SerializeField] private float debugCellSize;
        [SerializeField] private Vector2Int objectSizeRelativeToCellSize;

        [SerializeField] private bool activeSceneObject;
        [SerializeField] private BuildableGridObjectSO sceneObjectBuildableGridObjectSO;
        [SerializeField] private FourDirectionalRotation objectRotation;
        [SerializeField] private int verticalGridIndex;

        private bool isInstantiatedByGhostObject;

        //Usable Data
        private EasyGridBuilderPro occupiedGridSystem;
        private int occupiedVerticalGridIndex;
        private float occupiedCellSize;
        private BuildableGridObjectSO buildableGridObjectSO;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private Vector3 objectOriginWorldPosition;
        private Vector3 objectModifiedOriginWorldPosition;
        private Vector2Int objectOriginCellPosition;
        private List<Vector2Int> objectCellPositionList;
        private FourDirectionalRotation objectFourDirectionalRotation;
        
        private void Start()
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
            LayerMask combinedLayerMask = GridManager.Instance.GetGridSystemLayerMask() | sceneObjectBuildableGridObjectSO.customSurfaceLayerMask;

            if (TryGetHit(objectWorldPosition, GridManager.Instance.GetGridSystemLayerMask(), out RaycastHit hit))
            {
                if (hit.collider.TryGetComponent(out EasyGridBuilderPro easyGridBuilderPro)) 
                {
                    if (verticalGridIndex >= easyGridBuilderPro.GetVerticalGridsCount()) 
                    {
                        Debug.Log($"Buildable Grid Object: {this.name}: <color=red><b>Invalid Vertical Grid Index! Scene Object Spawn Terminated!</b></color>"); yield break;
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
            float cellSize = easyGridBuilderPro.GetCellSize();
            Vector2Int objectRelativeScale = new Vector2Int(Mathf.CeilToInt(objectScale.x / cellSize), Mathf.CeilToInt(objectScale.z / cellSize));
            Vector3 adjustedPosition;

            if (gridAxis is GridAxis.XZ)
            {
                adjustedPosition = objectWorldPosition - new Vector3(objectRelativeScale.x * cellSize / 2, 0, objectRelativeScale.y * cellSize / 2);
                adjustedPosition = new Vector3(adjustedPosition.x + cellSize / 2, objectHeight, adjustedPosition.z + cellSize / 2);
            }
            else
            {
                adjustedPosition = objectWorldPosition - new Vector3(objectRelativeScale.x * cellSize / 2, objectRelativeScale.y * cellSize / 2, 0);
                adjustedPosition = new Vector3(adjustedPosition.x + cellSize / 2, adjustedPosition.y + cellSize / 2, objectHeight);
            }
            easyGridBuilderPro.SetInputBuildableObjectPlacement(true, false, sceneObjectBuildableGridObjectSO, adjustedPosition, objectRotation, activeVerticalGridIndex: verticalGridIndex);
        }

        public BuildableGridObject SetupBuildableGridObject(EasyGridBuilderPro gridSystem, int verticalGridIndex, float cellSize, BuildableGridObjectSO buildableGridObjectSO,
            BuildableObjectSO.RandomPrefabs randomPrefab, Vector3 originWorldPosition, Vector2Int originCellPosition, List<Vector2Int> objectCellPositionList, FourDirectionalRotation rotation)
        {
            SetProperties(gridSystem, verticalGridIndex, cellSize, buildableGridObjectSO, randomPrefab, originWorldPosition, originCellPosition, objectCellPositionList, rotation);
            RecalculateWorldPosition(out RaycastHit hit);
            HandleTerrainInteractions(hit);
            ConfigureTransform();
            ApplyLayerIfNeeded();

            if (gridSystem is EasyGridBuilderProXZ) AdjustPositionBasedOnDirectionXZ(rotation, cellSize);
            else AdjustPositionBasedOnDirectionXY(rotation, cellSize);

            return this;
        }

        /// <summary>
        /// Sets the initial properties for the buildable grid object.
        /// </summary>
        private void SetProperties(EasyGridBuilderPro gridSystem, int verticalGridIndex, float cellSize, BuildableGridObjectSO buildableGridObjectSO, BuildableObjectSO.RandomPrefabs randomPrefab,
            Vector3 originWorldPosition, Vector2Int originCellPosition, List<Vector2Int>objectCellPositionList, FourDirectionalRotation rotation)
        {
            this.occupiedGridSystem = gridSystem;
            this.occupiedVerticalGridIndex = verticalGridIndex;
            this.occupiedCellSize = cellSize;
            this.buildableGridObjectSO = buildableGridObjectSO;
            this.buildableObjectSORandomPrefab = randomPrefab;
            this.objectOriginWorldPosition = originWorldPosition;
            this.objectOriginCellPosition = originCellPosition;
            this.objectCellPositionList = objectCellPositionList;
            this.objectFourDirectionalRotation = rotation;
        }

        private void RecalculateWorldPosition(out RaycastHit hit)
        {
            hit = default;
            Vector3 rayOrigin = occupiedGridSystem is EasyGridBuilderProXZ ? (objectOriginWorldPosition + new Vector3(occupiedCellSize / 2, 0, occupiedCellSize / 2)) + Vector3.up * 1000 :
                (objectOriginWorldPosition + new Vector3(occupiedCellSize / 2, occupiedCellSize / 2, 0)) + Vector3.back * 1000;
            Vector3 rayDirection = occupiedGridSystem is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;
            
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, Mathf.Infinity, buildableGridObjectSO.customSurfaceLayerMask | GridManager.Instance.GetGridSystemLayerMask()))
            {
                float height = GetClosestVerticalGridSnappingHeight(buildableGridObjectSO, occupiedGridSystem is EasyGridBuilderProXZ ? occupiedGridSystem.transform.position.y : occupiedGridSystem.transform.position.z, 
                    occupiedGridSystem is EasyGridBuilderProXZ ? hit.point.y : hit.point.z, occupiedGridSystem.GetVerticalGridHeight());
                objectModifiedOriginWorldPosition = occupiedGridSystem is EasyGridBuilderProXZ ? new Vector3(objectOriginWorldPosition.x, height, objectOriginWorldPosition.z) :
                    new Vector3(objectOriginWorldPosition.x, objectOriginWorldPosition.y, height);
            }
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

        private void HandleTerrainInteractions(RaycastHit hit)
        {
            if (occupiedGridSystem is not EasyGridBuilderProXZ) return;
            if (buildableGridObjectSO.enableTerrainInteractions == false || !hit.collider.TryGetComponent<Terrain>(out Terrain terrain)) return;

            Vector3 worldPosition = default;
            switch (objectFourDirectionalRotation)
            {
                case FourDirectionalRotation.North: worldPosition = objectModifiedOriginWorldPosition + new Vector3(occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).x, 0, occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).y); break;
                case FourDirectionalRotation.East: worldPosition = objectModifiedOriginWorldPosition + new Vector3(occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).y, 0, -occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).x); break;
                case FourDirectionalRotation.South: worldPosition = objectModifiedOriginWorldPosition + new Vector3(-occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).x, 0, -occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).y); break;
                case FourDirectionalRotation.West: worldPosition = objectModifiedOriginWorldPosition + new Vector3(-occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).y, 0, occupiedCellSize / 2 * CalculateObjectSizeRelativeToCellSize(occupiedCellSize).x); break;
            }

            if (buildableGridObjectSO.flattenTerrain) TerrainInteractionUtilities.FlattenTerrain(terrain, worldPosition, buildableGridObjectSO.flattenerSize);
            if (buildableGridObjectSO.removeTerrainDetails) TerrainInteractionUtilities.ClearTerrainDetails(terrain, worldPosition, buildableGridObjectSO.detailsRemoverSize);
            if (buildableGridObjectSO.paintTerrainTexture) TerrainInteractionUtilities.PaintTerrainWithTexture(terrain, worldPosition, buildableGridObjectSO.painterBrushSize, 
                buildableGridObjectSO.terrainTextureIndex, buildableGridObjectSO.painterBrushOpacity, buildableGridObjectSO.painterBrushType, buildableGridObjectSO.painterBrushFallOff);
        }

        /// <summary>
        /// Configures the transform of the buildable grid object.
        /// </summary>
        private void ConfigureTransform()
        {
            transform.name = transform.name.Replace("(Clone)", "").Trim();
            transform.localEulerAngles = occupiedGridSystem is EasyGridBuilderProXZ ? new Vector3(0, buildableGridObjectSO.GetRotationAngle(objectFourDirectionalRotation), 0) :
                new Vector3(0, 0, -buildableGridObjectSO.GetRotationAngle(objectFourDirectionalRotation));

            if (buildableGridObjectSO.raiseObjectWithVerticalGrids) 
            {
                if (occupiedGridSystem is EasyGridBuilderProXZ) objectModifiedOriginWorldPosition +=  new Vector3(0, occupiedVerticalGridIndex * occupiedGridSystem.GetVerticalGridHeight(), 0);
                else objectModifiedOriginWorldPosition -=  new Vector3(0, 0, occupiedVerticalGridIndex * occupiedGridSystem.GetVerticalGridHeight());
            }
            transform.localPosition = objectModifiedOriginWorldPosition;

            if (buildableGridObjectSO.setGridSystemAsParent) transform.parent = occupiedGridSystem.transform;
            objectSizeRelativeToCellSize = buildableGridObjectSO.GetObjectSizeRelativeToCellSize(occupiedCellSize, buildableObjectSORandomPrefab);
        }

        /// <summary>
        /// Applies the specified layer to the object and its children if needed.
        /// </summary>
        private void ApplyLayerIfNeeded()
        {
            if (buildableGridObjectSO.setSpawnedObjectLayer) SetLayerRecursive(gameObject, GetHighestLayerSet(buildableGridObjectSO.spawnedObjectLayer));
        }

        /// <summary>
        /// Adjusts the position of the object based on its direction and cell size.
        /// </summary>
        private void AdjustPositionBasedOnDirectionXZ(FourDirectionalRotation direction, float cellSize)
        {
            Vector3 sizeAdjustment = new Vector3(objectSizeRelativeToCellSize.x * cellSize / 2, 0, objectSizeRelativeToCellSize.y * cellSize / 2);

            switch (direction)
            {
                case FourDirectionalRotation.North: transform.localPosition += new Vector3(sizeAdjustment.x, 0, sizeAdjustment.z) - objectCustomPivot; break;
                case FourDirectionalRotation.East: transform.localPosition += new Vector3(sizeAdjustment.z, 0, -sizeAdjustment.x) - new Vector3(objectCustomPivot.z, 0, -objectCustomPivot.x); break;
                case FourDirectionalRotation.South: transform.localPosition += new Vector3(-sizeAdjustment.x, 0, -sizeAdjustment.z) + objectCustomPivot; break;
                case FourDirectionalRotation.West: transform.localPosition += new Vector3(-sizeAdjustment.z, 0, sizeAdjustment.x) + new Vector3(objectCustomPivot.z, 0, -objectCustomPivot.x); break;
            }
        }

        private void AdjustPositionBasedOnDirectionXY(FourDirectionalRotation direction, float cellSize)
        {
            Vector3 sizeAdjustment = new Vector3(objectSizeRelativeToCellSize.x * cellSize / 2, objectSizeRelativeToCellSize.y * cellSize / 2, 0);

            switch (direction)
            {
                case FourDirectionalRotation.North: transform.localPosition += new Vector3(sizeAdjustment.x, sizeAdjustment.y, 0) - objectCustomPivot; break;
                case FourDirectionalRotation.East: transform.localPosition += new Vector3(sizeAdjustment.y, -sizeAdjustment.x, 0) - new Vector3(objectCustomPivot.y, -objectCustomPivot.x, 0); break;
                case FourDirectionalRotation.South: transform.localPosition += new Vector3(-sizeAdjustment.x, -sizeAdjustment.y, 0) + objectCustomPivot; break;
                case FourDirectionalRotation.West: transform.localPosition += new Vector3(-sizeAdjustment.y, sizeAdjustment.x, 0) + new Vector3(objectCustomPivot.y, -objectCustomPivot.x, 0); break;
            }
        }

        public Vector2 GetObjectTrueScale()
        {
            if (occupiedGridSystem)
            {
                if (occupiedGridSystem is EasyGridBuilderProXZ) return new Vector2(objectScale.x, objectScale.z);
                else if (occupiedGridSystem is EasyGridBuilderProXY) return new Vector2(objectScale.x, objectScale.y);
            }
            else
            {
                if (GridManager.Instance.GetActiveEasyGridBuilderPro() is EasyGridBuilderProXZ) return new Vector2(objectScale.x, objectScale.z);
                else if (GridManager.Instance.GetActiveEasyGridBuilderPro() is EasyGridBuilderProXY) return new Vector2(objectScale.x, objectScale.y);
            }
            
            return Vector2.zero;
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
        
        public Vector2Int CalculateObjectSizeRelativeToCellSize(float cellSize)
        {
            switch (gridAxis)
            {
                case GridAxis.XZ: return new Vector2Int(Mathf.CeilToInt(objectScale.x / cellSize), Mathf.CeilToInt(objectScale.z / cellSize));
                case GridAxis.XY: return objectSizeRelativeToCellSize = new Vector2Int(Mathf.CeilToInt(objectScale.x / cellSize), Mathf.CeilToInt(objectScale.y / cellSize));
            }
            return default;
        }

        /// <summary>
        /// Calculates the grid size of the placed object considering the cell size.
        /// </summary>
        #if UNITY_EDITOR
        public void GetObjectSizeRelativeToCellSizeEditor()
        {
            switch (gridAxis)
            {
                case GridAxis.XZ: objectSizeRelativeToCellSize = new Vector2Int(Mathf.CeilToInt(objectScale.x / debugCellSize), Mathf.CeilToInt(objectScale.z / debugCellSize)); break;
                case GridAxis.XY: objectSizeRelativeToCellSize = new Vector2Int(Mathf.CeilToInt(objectScale.x / debugCellSize), Mathf.CeilToInt(objectScale.y / debugCellSize)); break;
            }
        }
        #endif

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
                basicGridAreaTrigger.InvokeBasicGridAreaTrigger(BasicGridAreaTriggerInvokerType.GridObject);
            }
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

        public override float GetDebugCellSize() => debugCellSize;

        public override Vector2Int GetObjectSizeRelativeToCellSize() => objectSizeRelativeToCellSize;

        public override bool GetIsActiveSceneObject(out BuildableObjectSO buildableObjectSO)
        {
            buildableObjectSO = sceneObjectBuildableGridObjectSO;
            return activeSceneObject;
        }

        public override void GetSceneObjectRotation(out FourDirectionalRotation fourDirectionalRotation, out EightDirectionalRotation eightDirectionalRotation, out float freeRotation)
        {
            fourDirectionalRotation = objectRotation;
            eightDirectionalRotation = default;
            freeRotation = default;
        }

        public override int GetSceneObjectVerticalGridIndex() => verticalGridIndex;

        public override bool GetIsInstantiatedByGhostObject() => isInstantiatedByGhostObject;

        public override EasyGridBuilderPro GetOccupiedGridSystem() => occupiedGridSystem;

        public override int GetOccupiedVerticalGridIndex() => occupiedVerticalGridIndex;

        public override float GetOccupiedCellSize() => occupiedCellSize;

        public override BuildableObjectSO GetBuildableObjectSO() => buildableGridObjectSO;

        public override BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;

        public override Vector2Int GetObjectOriginCellPosition(out List<Vector2Int> objectCellPositionList)
        {
            objectCellPositionList = this.objectCellPositionList;
            return objectOriginCellPosition;
        }
        
        public override Vector3 GetObjectOriginWorldPosition() => objectOriginWorldPosition;

        public override Vector3 GetObjectModifiedOriginWorldPosition() => objectModifiedOriginWorldPosition;

        public override Vector3 GetObjectOffset() => default;

        public override List<Vector2Int> GetObjectCellPositionList() => objectCellPositionList;

        public override CornerObjectCellDirection GetCornerObjectOriginCellDirection() => default;

        public override FourDirectionalRotation GetObjectFourDirectionalRotation() => objectFourDirectionalRotation;

        public override EightDirectionalRotation GetObjectEightDirectionalRotation() => default;

        public override float GetObjectFreeRotation() => default;

        public override bool GetIsObjectFlipped() => default;

        public override Vector3 GetObjectHitNormals() => default;

        public override bool GetIsObjectInvokedAsSecondaryPlacement() => default;

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        public override void SetIsInstantiatedByGhostObject(bool isInstantiatedByGhostObject) => this.isInstantiatedByGhostObject = isInstantiatedByGhostObject;

        public override void SetGridAxis(GridAxis gridAxis) => this.gridAxis = gridAxis;

        public override void SetObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation) => objectFourDirectionalRotation = fourDirectionalRotation;

        public override void SetObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation) {}

        public override void SetObjectFreeRotation(float freeRotation) {}

        public override void SetLockAutoGenerationAndValues(bool enable) => lockAutoGenerationAndValues = enable;
        
        public override void SetIsActivateGizmos(bool activateGizmos) => this.activateGizmos = activateGizmos;

        public override void SetObjectScale(Vector3 objectScale) => this.objectScale = objectScale;

        public override void SetObjectCenter(Vector3 objectCenter) => this.objectCenter = objectCenter;

        public override void SetObjectCustomPivot(Vector3 objectCustomPivot) => this.objectCustomPivot = objectCustomPivot;

        public override void SetDebugCellSize(float debugCellSize) => this.debugCellSize = debugCellSize;

        public override void SetObjectSizeRelativeToCellSize(Vector2Int objectSizeRelativeToCellSize) => this.objectSizeRelativeToCellSize = objectSizeRelativeToCellSize;

        public override void SetIsActiveSceneObject(bool activeSceneObject) => this.activeSceneObject = activeSceneObject;

        public override void SetSceneObjectBuildableObjectSO(BuildableObjectSO buildableObjectSO) => this.sceneObjectBuildableGridObjectSO = (BuildableGridObjectSO)buildableObjectSO;

        public override void SetSceneObjectRotation(FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North, 
            EightDirectionalRotation eightDirectionalRotation = EightDirectionalRotation.North, float freeRotation = 0) => this.objectFourDirectionalRotation = fourDirectionalRotation;

        public override void SetSceneObjectVerticalGridIndex(int verticalGridIndex) => this.verticalGridIndex = verticalGridIndex;

        public override void SetOccupiedGridSystem(EasyGridBuilderPro easyGridBuilderPro) => this.occupiedGridSystem = easyGridBuilderPro;

        public override void SetOccupiedVerticalGridIndex(int occupiedVerticalGridIndex) => this.occupiedVerticalGridIndex = occupiedVerticalGridIndex;

        public override void SetOccupiedCellSize(float cellSize) => this.occupiedCellSize = cellSize;

        public override void SetBuildableObjectSO(BuildableObjectSO buildableObjectSO) => this.buildableGridObjectSO = (BuildableGridObjectSO)buildableObjectSO;

        public override void SetBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab) => this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;

        public override void SetObjectOriginWorldPosition(Vector3 objectOriginWorldPosition) => this.objectOriginWorldPosition = objectOriginWorldPosition;

        public override void SetObjectModifiedOriginWorldPosition(Vector3 objectModifiedOriginWorldPosition) => this.objectModifiedOriginWorldPosition = objectModifiedOriginWorldPosition;

        public override void SetObjectOffset(Vector3 objectOffset) {}

        public override void SetObjectOriginCellPosition(Vector2Int objectOriginCellPosition) => this.objectOriginCellPosition = objectOriginCellPosition;

        public override void SetObjectCellPositionList(List<Vector2Int> objectCellPositionList) => this.objectCellPositionList = objectCellPositionList;

        public override void SetCornerObjectOriginCellDirection(CornerObjectCellDirection cornerObjectCellDirection) {}

        public override void SetIsObjectFlipped(bool isFlipped) {}

        public override void SetObjectHitNormals(Vector3 hitNormals) {}

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
                DrawGizmosForDebugGrid();
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

        private void DrawGizmosForDebugGrid()
        {
            if (objectSizeRelativeToCellSize == default || debugCellSize == default) return;

            Vector3 gizmoGridOrigin;
            int width = objectSizeRelativeToCellSize.x + 4;
            int height = objectSizeRelativeToCellSize.y + 4;

            if (gridAxis == GridAxis.XZ)
            {
                if (activeSceneObject) gizmoGridOrigin = new Vector3((objectSizeRelativeToCellSize.x + 4) * (debugCellSize / 2) - objectCustomPivot.x, -objectCustomPivot.y, 
                    (objectSizeRelativeToCellSize.y + 4) * (debugCellSize / 2) - objectCustomPivot.z) - transform.position;
                else gizmoGridOrigin = new Vector3((objectSizeRelativeToCellSize.x + 4) * (debugCellSize / 2), 0, (objectSizeRelativeToCellSize.y + 4) * (debugCellSize / 2)) - objectCustomPivot;
            }
            else
            {
                if (activeSceneObject) gizmoGridOrigin = new Vector3((objectSizeRelativeToCellSize.x + 4) * (debugCellSize / 2) - objectCustomPivot.x, 
                    (objectSizeRelativeToCellSize.y + 4) * (debugCellSize / 2) - objectCustomPivot.y, -objectCustomPivot.z) - transform.position;
                else gizmoGridOrigin = new Vector3((objectSizeRelativeToCellSize.x + 4) * (debugCellSize / 2), (objectSizeRelativeToCellSize.y + 4) * (debugCellSize / 2), 0) - objectCustomPivot;
            }

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < height; z++)
                {
                    if (x > 0) DrawGridLine(x, z, x, z + 1, gizmoGridOrigin);
                    if (z > 0) DrawGridLine(x, z, x + 1, z, gizmoGridOrigin);
                    if (x == 2 && z == 2) DrawAAPolyFilledWireBox(x, z, gizmoGridOrigin);
                }
            }
        }

        private void DrawGridLine(int startX, int startZ, int endX, int endZ, Vector3 origin)
        {
            Vector3 start = gridAxis is GridAxis.XZ ? new Vector3(startX, 0, startZ) * debugCellSize - origin : new Vector3(startX, startZ, 0) * debugCellSize - origin;
            Vector3 end = gridAxis is GridAxis.XZ ? new Vector3(endX, 0, endZ) * debugCellSize - origin : new Vector3(endX, endZ, 0) * debugCellSize - origin;
            CustomGizmosUtilities.DrawAAPolyLine(start, end, 2f, Color.yellow);
        }

        private void DrawAAPolyFilledWireBox(int x, int z, Vector3 origin)
        {
            Vector3 position = gridAxis is GridAxis.XZ ? new Vector3(x + 0.5f, 0, z + 0.5f) * debugCellSize - origin : new Vector3(x + 0.5f, z + 0.5f, 0) * debugCellSize - origin;
            Quaternion rotation = gridAxis is GridAxis.XZ ? Quaternion.Euler(90, 0, 0) : Quaternion.Euler(0, 0, 0);
            CustomGizmosUtilities.DrawAAPolyFilledWireBox(position, debugCellSize, rotation, 10, 2f, Color.yellow, Color.yellow);
        }
        #endif
    }
}