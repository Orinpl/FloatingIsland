using UnityEngine;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Collections;
using SoulGames.Utilities;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Buildables/Buildable Edge Object", 2)]
    public class BuildableEdgeObject : BuildableObject
    {
        [SerializeField] private GridAxis gridAxis = GridAxis.XZ;
        [SerializeField] private bool activateGizmos = true;

        [SerializeField] private bool lockAutoGenerationAndValues;
        [SerializeField] private Vector3 objectScale;
        [SerializeField] private Vector3 objectCenter;
        [SerializeField] private Vector3 objectCustomPivot;
        
        [SerializeField] private float debugCellSize;
        [SerializeField] private Vector2Int objectSizeRelativeToCellSize;
        [SerializeField] private int objectLengthRelativeToCellSize;

        [SerializeField] private bool activeSceneObject;
        [SerializeField] private BuildableEdgeObjectSO sceneObjectBuildableEdgeObjectSO;
        [SerializeField] private FourDirectionalRotation objectRotation;
        [SerializeField] private bool flipEdgeObject;
        [SerializeField] private int verticalGridIndex;

        private bool isInstantiatedByGhostObject;

        //Usable Data
        private EasyGridBuilderPro occupiedGridSystem;
        private int occupiedVerticalGridIndex;
        private float occupiedCellSize;
        private BuildableEdgeObjectSO buildableEdgeObjectSO;
        private BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab;
        private Vector3 objectOriginWorldPosition;
        private Vector3 objectModifiedOriginWorldPosition;
        private Vector3 objectOffset;
        private List<Vector2Int> objectCellPositionList;
        private Dictionary<Vector2Int, EdgeObjectCellDirection> objectCellPositionDictionary;
        private CornerObjectCellDirection cornerObjectOriginCellDirection;
        private FourDirectionalRotation objectFourDirectionalRotation;
        private bool isObjectFlipped;

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
            LayerMask combinedLayerMask = GridManager.Instance.GetGridSystemLayerMask() | sceneObjectBuildableEdgeObjectSO.customSurfaceLayerMask;

            if (TryGetHit(objectWorldPosition, GridManager.Instance.GetGridSystemLayerMask(), out RaycastHit hit))
            {
                if (hit.collider.TryGetComponent(out EasyGridBuilderPro easyGridBuilderPro)) 
                {
                    if (verticalGridIndex >= easyGridBuilderPro.GetVerticalGridsCount()) 
                    {
                        Debug.Log($"Buildable Edge Object: {this.name}: <color=red><b>Invalid Vertical Grid Index! Scene Object Spawn Terminated!</b></color>"); yield break;
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
            easyGridBuilderPro.SetInputBuildableObjectPlacement(true, false, sceneObjectBuildableEdgeObjectSO, objectWorldPosition, objectRotation, isBuildableEdgeObjectFlipped: flipEdgeObject, 
                activeVerticalGridIndex: verticalGridIndex);
        }

        public BuildableEdgeObject SetupBuildableEdgeObject(EasyGridBuilderPro gridSystem, int verticalGridIndex, float cellSize, BuildableEdgeObjectSO buildableEdgeObjectSO,
            BuildableObjectSO.RandomPrefabs randomPrefab, Vector3 originWorldPosition, Vector3 offset, List<Vector2Int> cellPositionList, Dictionary<Vector2Int, EdgeObjectCellDirection> cellPositionDictionary,
            CornerObjectCellDirection cornerObjectOriginCellDirection, FourDirectionalRotation rotation, bool isBuildableEdgeObjectFlipped)
        {
            SetProperties(gridSystem, verticalGridIndex, cellSize, buildableEdgeObjectSO, randomPrefab, originWorldPosition, offset, cellPositionList, cellPositionDictionary, cornerObjectOriginCellDirection,
                rotation, isBuildableEdgeObjectFlipped);
            RecalculateWorldPosition(out RaycastHit hit);
            HandleTerrainInteractions(hit);
            ConfigureTransform(buildableEdgeObjectSO);

            if (gridSystem is EasyGridBuilderProXZ) ConfigureSinglePlacementEdgeObjectFlipXZ(rotation, cellSize);
            else ConfigureSinglePlacementEdgeObjectFlipXY(rotation, cellSize);

            ApplyLayerIfNeeded();
            return this;
        }

        /// <summary>
        /// Sets the initial properties for the buildable grid object.
        /// </summary>
        private void SetProperties(EasyGridBuilderPro gridSystem, int verticalGridIndex, float cellSize, BuildableEdgeObjectSO buildableEdgeObjectSO, BuildableObjectSO.RandomPrefabs randomPrefab,
            Vector3 originWorldPosition, Vector3 offset, List<Vector2Int> cellPositionList, Dictionary<Vector2Int, EdgeObjectCellDirection> cellPositionDictionary, CornerObjectCellDirection cornerObjectOriginCellDirection,
            FourDirectionalRotation rotation, bool isBuildableEdgeObjectFlipped)
        {
            this.occupiedGridSystem = gridSystem;
            this.occupiedVerticalGridIndex = verticalGridIndex;
            this.occupiedCellSize = cellSize;
            this.buildableEdgeObjectSO = buildableEdgeObjectSO;
            this.buildableObjectSORandomPrefab = randomPrefab;
            this.objectOriginWorldPosition = originWorldPosition;
            this.objectOffset = offset;
            this.objectCellPositionList = cellPositionList;
            this.objectCellPositionDictionary = cellPositionDictionary;
            this.objectFourDirectionalRotation = rotation;
            this.cornerObjectOriginCellDirection = cornerObjectOriginCellDirection;
            this.isObjectFlipped = isBuildableEdgeObjectFlipped;
        }

        private void RecalculateWorldPosition(out RaycastHit hit)
        {
            hit = default;
            Vector3 rayOrigin =  occupiedGridSystem is EasyGridBuilderProXZ ? (objectOriginWorldPosition + objectOffset) + Vector3.up * 1000 : (objectOriginWorldPosition + objectOffset) + Vector3.back * 1000;
            Vector3 rayDirection = occupiedGridSystem is EasyGridBuilderProXZ ? Vector3.down * 9999 : Vector3.forward * 9999;

            if (Physics.Raycast(rayOrigin, rayDirection, out hit, Mathf.Infinity, buildableEdgeObjectSO.customSurfaceLayerMask | GridManager.Instance.GetGridSystemLayerMask()))
            {
                float height = GetClosestVerticalGridSnappingHeight(buildableEdgeObjectSO, occupiedGridSystem is EasyGridBuilderProXZ ? occupiedGridSystem.transform.position.y : occupiedGridSystem.transform.position.z,
                    occupiedGridSystem is EasyGridBuilderProXZ ? hit.point.y : hit.point.z, occupiedGridSystem.GetVerticalGridHeight());
                objectModifiedOriginWorldPosition =  occupiedGridSystem is EasyGridBuilderProXZ ? new Vector3(objectOriginWorldPosition.x + objectOffset.x, height, objectOriginWorldPosition.z + objectOffset.z) :
                    new Vector3(objectOriginWorldPosition.x + objectOffset.x, objectOriginWorldPosition.y + objectOffset.y, height);
            }
        }

        private float GetClosestVerticalGridSnappingHeight(BuildableEdgeObjectSO buildableEdgeObjectSO, float gridHeight, float height, float verticalGridHeight)
        {
            if (buildableEdgeObjectSO.customSurfaceLayerMask == 0) return height;
            if (buildableEdgeObjectSO.snapToTheClosestVerticalGridHeight)
            {
                // Find the difference between the height and the base gridHeight
                float relativeHeight = height - gridHeight;

                // Calculate the closest lower and upper multiples of verticalGridHeight
                float lowerMultiple = Mathf.Floor(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;
                float upperMultiple = Mathf.Ceil(relativeHeight / verticalGridHeight) * verticalGridHeight + gridHeight;

                // Calculate the threshold value (percentage of verticalGridHeight)
                float thresholdValue = (buildableEdgeObjectSO.minimumHeightThresholdPercentage / 100f) * verticalGridHeight;

                if (height - lowerMultiple >= thresholdValue) return upperMultiple;
                else return lowerMultiple;
            }
            return height;
        }
        private void HandleTerrainInteractions(RaycastHit hit)
        {
            if (occupiedGridSystem is not EasyGridBuilderProXZ) return;
            if (buildableEdgeObjectSO.enableTerrainInteractions == false || !hit.collider.TryGetComponent<Terrain>(out Terrain terrain)) return;
            if (buildableEdgeObjectSO.flattenTerrain) TerrainInteractionUtilities.FlattenTerrain(terrain, objectModifiedOriginWorldPosition, buildableEdgeObjectSO.flattenerSize);
            if (buildableEdgeObjectSO.removeTerrainDetails) TerrainInteractionUtilities.ClearTerrainDetails(terrain, objectModifiedOriginWorldPosition, buildableEdgeObjectSO.detailsRemoverSize);
            if (buildableEdgeObjectSO.paintTerrainTexture) TerrainInteractionUtilities.PaintTerrainWithTexture(terrain, objectModifiedOriginWorldPosition, buildableEdgeObjectSO.painterBrushSize, 
                buildableEdgeObjectSO.terrainTextureIndex, buildableEdgeObjectSO.painterBrushOpacity, buildableEdgeObjectSO.painterBrushType, buildableEdgeObjectSO.painterBrushFallOff);
        }

        /// <summary>
        /// Configures the transform of the buildable grid object.
        /// </summary>
        private void ConfigureTransform(BuildableEdgeObjectSO buildableEdgeObjectSO)
        {
            transform.name = transform.name.Replace("(Clone)", "").Trim();

            transform.localEulerAngles = occupiedGridSystem is EasyGridBuilderProXZ ? new Vector3(0, buildableEdgeObjectSO.GetRotationAngle(objectFourDirectionalRotation), 0) :
                new Vector3(0, 0, -buildableEdgeObjectSO.GetRotationAngle(objectFourDirectionalRotation));
            
            if (buildableEdgeObjectSO.raiseObjectWithVerticalGrids) 
            {
                if (occupiedGridSystem is EasyGridBuilderProXZ) objectModifiedOriginWorldPosition +=  new Vector3(0, occupiedVerticalGridIndex * occupiedGridSystem.GetVerticalGridHeight(), 0);
                else objectModifiedOriginWorldPosition -=  new Vector3(0, 0, occupiedVerticalGridIndex * occupiedGridSystem.GetVerticalGridHeight());
            }
            transform.localPosition = objectModifiedOriginWorldPosition;

            if (buildableEdgeObjectSO.setGridSystemAsParent) transform.parent = occupiedGridSystem.transform;
            objectLengthRelativeToCellSize = buildableEdgeObjectSO.GetObjectLengthRelativeToCellSize(occupiedCellSize, buildableObjectSORandomPrefab);
        }

        private void ConfigureSinglePlacementEdgeObjectFlipXZ(FourDirectionalRotation rotation, float cellSize)
        {
            if (isObjectFlipped == false) return;

            transform.localEulerAngles += new Vector3(0, 180, 0);
            switch (rotation)
            {
                case FourDirectionalRotation.North: transform.position += new Vector3(0, 0, cellSize * objectLengthRelativeToCellSize); break;
                case FourDirectionalRotation.East: transform.position += new Vector3(cellSize * objectLengthRelativeToCellSize, 0, 0); break;
                case FourDirectionalRotation.South: transform.position += new Vector3(0, 0, cellSize * objectLengthRelativeToCellSize * -1); break;
                case FourDirectionalRotation.West: transform.position += new Vector3(cellSize * objectLengthRelativeToCellSize * -1, 0, 0); break;
            }
        }

        private void ConfigureSinglePlacementEdgeObjectFlipXY(FourDirectionalRotation rotation, float cellSize)
        {
            if (isObjectFlipped == false) return;

            transform.localEulerAngles += new Vector3(0, 0, 180);
            switch (rotation)
            {
                case FourDirectionalRotation.North: transform.position += new Vector3(0, cellSize * objectLengthRelativeToCellSize, 0); break;
                case FourDirectionalRotation.East: transform.position += new Vector3(cellSize * objectLengthRelativeToCellSize, 0, 0); break;
                case FourDirectionalRotation.South: transform.position += new Vector3(0, cellSize * objectLengthRelativeToCellSize * -1, 0); break;
                case FourDirectionalRotation.West: transform.position += new Vector3(cellSize * objectLengthRelativeToCellSize * -1, 0, 0); break;
            }
        }

        /// <summary>
        /// Applies the specified layer to the object and its children if needed.
        /// </summary>
        private void ApplyLayerIfNeeded()
        {
            if (buildableEdgeObjectSO.setSpawnedObjectLayer) SetLayerRecursive(gameObject, GetHighestLayerSet(buildableEdgeObjectSO.spawnedObjectLayer));
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
                case GridAxis.XZ: objectCustomPivot = new Vector3(objectCenter.x - objectScale.x / 2, objectCenter.y - objectScale.y / 2, objectCenter.z); break;
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

        /// <summary>
        /// Calculates the grid size of the placed object considering the cell size.
        /// </summary>
        #if UNITY_EDITOR
        public void GetObjectSizeRelativeToCellSizeEditor()
        {
            objectLengthRelativeToCellSize = Mathf.CeilToInt(objectScale.x / debugCellSize);
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
                basicGridAreaTrigger.InvokeBasicGridAreaTrigger(BasicGridAreaTriggerInvokerType.EdgeObject);
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
            buildableObjectSO = sceneObjectBuildableEdgeObjectSO;
            return activeSceneObject;
        }

        public BuildableEdgeObjectSO GetSceneObjectBuildableEdgeObjectSO() => sceneObjectBuildableEdgeObjectSO;

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

        public override BuildableObjectSO GetBuildableObjectSO() => buildableEdgeObjectSO;

        public override BuildableObjectSO.RandomPrefabs GetBuildableObjectSORandomPrefab() => buildableObjectSORandomPrefab;

        public override Vector2Int GetObjectOriginCellPosition(out List<Vector2Int> objectCellPositionList)
        {
            objectCellPositionList = this.objectCellPositionList;
            return objectCellPositionList[0];
        }

        public override List<Vector2Int> GetObjectCellPositionList() => objectCellPositionList;
        
        public override Vector3 GetObjectOriginWorldPosition() => objectOriginWorldPosition;

        public override Vector3 GetObjectModifiedOriginWorldPosition() => objectModifiedOriginWorldPosition;
        
        public override Vector3 GetObjectOffset() => objectOffset;

        public override CornerObjectCellDirection GetCornerObjectOriginCellDirection() => cornerObjectOriginCellDirection;

        public override FourDirectionalRotation GetObjectFourDirectionalRotation() => objectFourDirectionalRotation;

        public override EightDirectionalRotation GetObjectEightDirectionalRotation() => default;

        public override float GetObjectFreeRotation() => default;

        public override bool GetIsObjectFlipped() => isObjectFlipped;

        public override Vector3 GetObjectHitNormals() => default;

        public override bool GetIsObjectInvokedAsSecondaryPlacement() => default;

        public Dictionary<Vector2Int, EdgeObjectCellDirection> GetCellPositionDictionary() => objectCellPositionDictionary;

        ///-------------------------------------------------------------------------------///
        /// PUBLIC SETTER FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///

        public override void SetIsInstantiatedByGhostObject(bool isInstantiatedByGhostObject) => this.isInstantiatedByGhostObject = isInstantiatedByGhostObject;

        public override void SetLockAutoGenerationAndValues(bool enable) => lockAutoGenerationAndValues = enable;

        public override void SetGridAxis(GridAxis gridAxis) => this.gridAxis = gridAxis;

        public override void SetObjectFourDirectionalRotation(FourDirectionalRotation fourDirectionalRotation) => objectFourDirectionalRotation = fourDirectionalRotation;

        public override void SetObjectEightDirectionalRotation(EightDirectionalRotation eightDirectionalRotation) {}

        public override void SetObjectFreeRotation(float freeRotation) {}

        public override void SetIsActivateGizmos(bool activateGizmos) => this.activateGizmos = activateGizmos;

        public override void SetObjectScale(Vector3 objectScale) => this.objectScale = objectScale;

        public override void SetObjectCenter(Vector3 objectCenter) => this.objectCenter = objectCenter;

        public override void SetObjectCustomPivot(Vector3 objectCustomPivot) => this.objectCustomPivot = objectCustomPivot;

        public override void SetDebugCellSize(float debugCellSize) => this.debugCellSize = debugCellSize;

        public override void SetObjectSizeRelativeToCellSize(Vector2Int objectSizeRelativeToCellSize) => this.objectSizeRelativeToCellSize = objectSizeRelativeToCellSize;

        public override void SetIsActiveSceneObject(bool activeSceneObject) => this.activeSceneObject = activeSceneObject;

        public override void SetSceneObjectBuildableObjectSO(BuildableObjectSO buildableObjectSO) => this.sceneObjectBuildableEdgeObjectSO = (BuildableEdgeObjectSO)buildableObjectSO;

        public override void SetSceneObjectRotation(FourDirectionalRotation fourDirectionalRotation = FourDirectionalRotation.North, 
            EightDirectionalRotation eightDirectionalRotation = EightDirectionalRotation.North, float freeRotation = 0) => this.objectFourDirectionalRotation = fourDirectionalRotation;

        public override void SetSceneObjectVerticalGridIndex(int verticalGridIndex) => this.verticalGridIndex = verticalGridIndex;

        public override void SetOccupiedGridSystem(EasyGridBuilderPro easyGridBuilderPro) => this.occupiedGridSystem = easyGridBuilderPro;

        public override void SetOccupiedVerticalGridIndex(int occupiedVerticalGridIndex) => this.occupiedVerticalGridIndex = occupiedVerticalGridIndex;

        public override void SetOccupiedCellSize(float cellSize) => this.occupiedCellSize = cellSize;

        public override void SetBuildableObjectSO(BuildableObjectSO buildableObjectSO) => this.buildableEdgeObjectSO = (BuildableEdgeObjectSO)buildableObjectSO;

        public override void SetBuildableObjectSORandomPrefab(BuildableObjectSO.RandomPrefabs buildableObjectSORandomPrefab) => this.buildableObjectSORandomPrefab = buildableObjectSORandomPrefab;

        public override void SetObjectOriginWorldPosition(Vector3 objectOriginWorldPosition) => this.objectOriginWorldPosition = objectOriginWorldPosition;

        public override void SetObjectModifiedOriginWorldPosition(Vector3 objectModifiedOriginWorldPosition) => this.objectModifiedOriginWorldPosition = objectModifiedOriginWorldPosition;

        public override void SetObjectOffset(Vector3 objectOffset) => this.objectOffset = objectOffset;

        public override void SetObjectOriginCellPosition(Vector2Int objectOriginCellPosition) {}

        public override void SetObjectCellPositionList(List<Vector2Int> objectCellPositionList) => this.objectCellPositionList = objectCellPositionList;

        public override void SetCornerObjectOriginCellDirection(CornerObjectCellDirection cornerObjectCellDirection) => this.cornerObjectOriginCellDirection = cornerObjectCellDirection;

        public override void SetIsObjectFlipped(bool isFlipped) => this.isObjectFlipped = isFlipped;

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
                DrawGizmosForRightAxis();
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

        private void DrawGizmosForRightAxis()
        {
            Vector3 startPos;

            if (activeSceneObject) startPos = transform.position + objectCustomPivot;
            else startPos = objectCustomPivot;

            float maxScale = Mathf.Max(objectScale.x, objectScale.y, objectScale.z);
            float minScale = Mathf.Min(objectScale.x, objectScale.y, objectScale.z);

            CustomGizmosUtilities.DrawAAPolyArrow(startPos, startPos + Vector3.right * maxScale * 2, minScale, 25, 5, 2, Color.red);
        }

        private void DrawGizmosForDebugGrid()
        {
            if (objectLengthRelativeToCellSize == default || debugCellSize == default) return;

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
                }
            }
        }

        private void DrawGridLine(int startX, int startZ, int endX, int endZ, Vector3 origin)
        {
            Vector3 start = gridAxis is GridAxis.XZ ? new Vector3(startX, 0, startZ) * debugCellSize - origin : new Vector3(startX, startZ, 0) * debugCellSize - origin;
            Vector3 end = gridAxis is GridAxis.XZ ? new Vector3(endX, 0, endZ) * debugCellSize - origin : new Vector3(endX, endZ, 0) * debugCellSize - origin;
            CustomGizmosUtilities.DrawAAPolyLine(start, end, 2f, Color.yellow);
        }
        #endif
    }
}
