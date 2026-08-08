using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Effects/Grid Area Visual Generator", 1)]
    public class GridAreaVisualGenerator : MonoBehaviour
    {
        [SerializeField] private GridAxis gridAxis = GridAxis.XZ;
        [SerializeField] private bool warpAroundSurfaceBelow;
        [SerializeField] private LayerMask customWarpSurfaceLayerMask;
        [SerializeField] private float raycastAboveOffset = 10f;
        [SerializeField] private float spaceBetweenVisualAndSurface = 0.25f;
        [SerializeField] private bool updateOnPositionChange;

        [SerializeField] private AreaShape areaShape = AreaShape.Rectangle;
        [SerializeField] private float cellSize;
        [SerializeField] private int width;
        [SerializeField] private int length;
        [SerializeField] private float radius;

        #if UNITY_EDITOR
        [SerializeField] private bool enableGizmos = true;
        [SerializeField] private bool enableSimplifiedGizmos = false;
        [SerializeField] private Color gizmoColor = Color.red;
        #endif

        [SerializeField] private Material visualMaterial;
        [SerializeField] private Texture cellImageTexture;
        [SerializeField] private Color cellShowColor = new Color32(255, 255, 255, 255);
        [SerializeField] private Color cellHideColor = new Color32(255, 255, 255, 0);
        [SerializeField] [ColorUsage(true, true)] private Color cellShowColorHDR = new Color32(0, 0, 0, 255);
        [SerializeField] [ColorUsage(true, true)] private Color cellHideColorHDR = new Color32(0, 0, 0, 0);
        [SerializeField] private float colorTransitionSpeed = 20f;

        [SerializeField] private bool useScrollingNoise;
        [SerializeField] private Texture noiseTexture;
        [SerializeField] private Vector2 textureTiling = new Vector2(1, 1);
        [SerializeField] private Vector2 textureScrolling = new Vector2(0, 0.1f);
        
        [SerializeField] private IsAttachedToABuildableObject isAttachedToABuildableObject = IsAttachedToABuildableObject.Yes;
        [SerializeField] private BuildableObject buildableObject;
        
        [SerializeField] private bool deactivateOnGhostMode = true;
        [SerializeField] private bool deactivateAfterBuild = true;
        [SerializeField] private bool toggleOnSelect = false;
        [SerializeField] private bool toggleOnMove = false;

        [SerializeField] private bool activateOnDefaultMode = true;
        [SerializeField] private bool activateOnBuildMode = true;
        [SerializeField] private bool activateOnDestroyMode = true;
        [SerializeField] private bool activateOnSelectMode = true;
        [SerializeField] private bool activateOnMoveMode = true;

        private List<GameObject> quads;
        private Coroutine lerpCoroutine;
        private bool objectsActiveSelf = true;
        private Vector3 previousTransformPosition;
        private bool isInstantiatedByGhostObject;

        private BuildableObjectSelector buildableObjectSelector;
        private BuildableObjectMover buildableObjectMover;
    
        private Material visualSharedMaterial;
        private const string QUAD_NAME = "GridAreaVisualGeneratorQuad";
        private int CELL_TEXTURE = Shader.PropertyToID("_Cell_Texture");
        private int CELL_COLOR_OVERRIDE = Shader.PropertyToID("_Cell_Color_Override");
        private int HDR_OVERRIDE = Shader.PropertyToID("_HDR_Override");
        private const string USE_SCROLLING_NOISE = "_USE_SCROLLING_NOISE";
        private int NOISE_TEXTURE = Shader.PropertyToID("_Noise_Texture");
        private int NOISE_TEXTURE_TILING = Shader.PropertyToID("_Texture_Tiling");
        private int NOISE_TEXTURE_SCROLLING = Shader.PropertyToID("_Texture_Scroll");

        ///-------------------------------------------------------------------------------///
        /// GRID AREA VISUAL INITIALIZE FUNCTIONS                                         ///
        ///-------------------------------------------------------------------------------///
        
        private void Start()
        {
            previousTransformPosition = transform.position;

            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.Yes)
            {
                if (buildableObject) isInstantiatedByGhostObject = buildableObject.GetIsInstantiatedByGhostObject();
                if (isInstantiatedByGhostObject) 
                {
                    if (!deactivateOnGhostMode) updateOnPositionChange = true;
                    else 
                    {
                        objectsActiveSelf = false;
                        return;
                    }
                }
                else if (deactivateAfterBuild) SetInputDisableGridAreaVisual();
            }

            GenerateQuads();
            SetupMaterials();
            StartCoroutine(LateStart());

            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.No)
            {
                if (CanDisplayOnGridMode(GridManager.Instance.GetActiveEasyGridBuilderPro().GetActiveGridMode())) SetInputEnableGridAreaVisual();
                else SetInputDisableGridAreaVisual();
            }
        }

        private void OnDestroy()
        {
            GridManager.Instance.OnActiveGridModeChanged -= OnActiveGridModeChanged;

            if (buildableObjectSelector)
            {
                buildableObjectSelector.OnBuildableObjectSelected -= OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected -= OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (buildableObjectMover)
            {
                buildableObjectMover.OnBuildableObjectStartMoving -= OnBuildableObjectStartMovingByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectEndMoving -= OnBuildableObjectEndMovingByBuildableObjectMoverDelegate;
            }
        }

        #region Grid Area Visual Initialize Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();

            GridManager.Instance.OnActiveGridModeChanged += OnActiveGridModeChanged;

            if (GridManager.Instance.TryGetBuildableObjectSelector(out buildableObjectSelector))
            {
                buildableObjectSelector.OnBuildableObjectSelected += OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected += OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (GridManager.Instance.TryGetBuildableObjectMover(out buildableObjectMover))
            {
                buildableObjectMover.OnBuildableObjectStartMoving += OnBuildableObjectStartMovingByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectEndMoving += OnBuildableObjectEndMovingByBuildableObjectMoverDelegate;
            }
        }

        private void OnActiveGridModeChanged(EasyGridBuilderPro easyGridBuilderPro, GridMode gridMode)
        {
            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.Yes) return;
            if (CanDisplayOnGridMode(gridMode)) SetInputEnableGridAreaVisual();
            else SetInputDisableGridAreaVisual();
        }

        private bool CanDisplayOnGridMode(GridMode activeGridMode)
        {
            switch (activeGridMode)
            {
                case GridMode.None: return activateOnDefaultMode;
                case GridMode.BuildMode: return activateOnBuildMode;
                case GridMode.DestroyMode: return activateOnDestroyMode;
                case GridMode.SelectMode: return activateOnSelectMode;
                case GridMode.MoveMode: return activateOnMoveMode;
                default: return false;
            }
        }

        private void OnSelectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.No) return;
            if (this.buildableObject.GetUniqueID() == buildableObject.GetUniqueID() && toggleOnSelect) SetInputEnableGridAreaVisual();
        }

        private void OnDeselectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.No) return;
            if (this.buildableObject.GetUniqueID() == buildableObject.GetUniqueID() && toggleOnSelect) SetInputDisableGridAreaVisual();
        }

        private void OnBuildableObjectStartMovingByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.No) return;
            if (this.buildableObject.GetUniqueID() == buildableObject.GetUniqueID() && toggleOnMove) SetInputEnableGridAreaVisual();
        }

        private void OnBuildableObjectEndMovingByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (isAttachedToABuildableObject == IsAttachedToABuildableObject.No) return;
            if (this.buildableObject.GetUniqueID() == buildableObject.GetUniqueID() && toggleOnMove) SetInputDisableGridAreaVisual();
        }

        private void GenerateQuads()
        {
            quads = new List<GameObject>();
            
            if (areaShape == AreaShape.Rectangle) GenerateRectangleQuads();
            else GenerateCircleQuads();
        }

        private void GenerateRectangleQuads()
        {
            Vector3 offset = gridAxis is GridAxis.XZ ? 
                new Vector3((width * cellSize) * 0.5f, 0, (length * cellSize) * 0.5f) :
                new Vector3((width * cellSize) * 0.5f, (length * cellSize) * 0.5f, 0);
            Vector3 basePosition = transform.position - offset;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < length; y++)
                {
                    Vector3 quadPosition = gridAxis is GridAxis.XZ ?  
                        basePosition + new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f) :
                        basePosition + new Vector3(x * cellSize + cellSize * 0.5f, y * cellSize + cellSize * 0.5f, 0);
                    CreateQuad(quadPosition);
                }
            }
        }

        private void GenerateCircleQuads()
        {
            Vector3 offset = gridAxis is GridAxis.XZ ? 
                new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), 0, (length * cellSize) * 0.5f - (0.5f * cellSize)) :
                new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), (length * cellSize) * 0.5f - (0.5f * cellSize), 0);
            Vector3 basePosition = transform.position - offset;
            float radiusSquared = radius * radius;

            Vector3 currentPosition = basePosition;
            for (int x = 0; x < width; x++, currentPosition.x += cellSize)
            {
                if (gridAxis is GridAxis.XZ) 
                {
                    currentPosition.z = basePosition.z;
                    for (int z = 0; z < length; z++, currentPosition.z += cellSize)
                    {
                        if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                        {
                            CreateQuad(currentPosition);
                        }
                    }
                }
                else 
                {
                    currentPosition.y = basePosition.y;
                    for (int y = 0; y < length; y++, currentPosition.y += cellSize)
                    {
                        if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                        {
                            CreateQuad(currentPosition);
                        }
                    }
                }
            }
        }

        private void CreateQuad(Vector3 position)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = QUAD_NAME;
            quad.transform.position = position;

            if (gridAxis is GridAxis.XZ) quad.transform.rotation = Quaternion.Euler(90, 0, 0);
            else quad.transform.rotation = Quaternion.Euler(0, 0, 0);

            quad.transform.localScale = new Vector3(cellSize, cellSize, 1);
            quad.transform.parent = transform;
            quads.Add(quad);

            UpdateQuadVertices(quad);
        }

        private void UpdateQuadVertices(GameObject quad)
        {
            if (warpAroundSurfaceBelow)
            {
                MeshFilter meshFilter = quad.GetComponent<MeshFilter>();
                Mesh mesh = meshFilter.mesh;
                Vector3[] vertices = mesh.vertices;
                float offset = spaceBetweenVisualAndSurface; // Add a slight offset

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 localPos = vertices[i];
                    Vector3 worldPos = quad.transform.TransformPoint(localPos);

                    if (gridAxis is GridAxis.XZ)
                    {
                        if (Physics.Raycast(worldPos + Vector3.up * raycastAboveOffset, Vector3.down, out RaycastHit hit, Mathf.Infinity, customWarpSurfaceLayerMask | GridManager.Instance.GetGridSystemLayerMask()))
                        {
                            worldPos.y = hit.point.y + offset; // Add the offset here
                            vertices[i] = quad.transform.InverseTransformPoint(worldPos);
                        }
                    }
                    else
                    {
                        if (Physics.Raycast(worldPos + Vector3.back * raycastAboveOffset, Vector3.forward, out RaycastHit hit, Mathf.Infinity, customWarpSurfaceLayerMask | GridManager.Instance.GetGridSystemLayerMask()))
                        {
                            worldPos.z = hit.point.z + offset; // Add the offset here
                            vertices[i] = quad.transform.InverseTransformPoint(worldPos);
                        }
                    }
                }

                mesh.vertices = vertices;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
            }
            previousTransformPosition = transform.position;
        }

        private void SetupMaterials()
        {
            visualSharedMaterial = new Material(visualMaterial);
            foreach (GameObject quad in quads)
            {
                Renderer renderer = quad.GetComponent<Renderer>();
                renderer.sharedMaterial = visualSharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            
            visualSharedMaterial.SetTexture(CELL_TEXTURE, cellImageTexture);
            visualSharedMaterial.SetColor(CELL_COLOR_OVERRIDE, cellShowColor);
            visualSharedMaterial.SetColor(HDR_OVERRIDE, cellShowColorHDR);

            if (useScrollingNoise)
            {
                if (!visualSharedMaterial.IsKeywordEnabled(USE_SCROLLING_NOISE)) visualSharedMaterial.EnableKeyword(USE_SCROLLING_NOISE);
                visualSharedMaterial.SetTexture(NOISE_TEXTURE, noiseTexture);
                visualSharedMaterial.SetVector(NOISE_TEXTURE_TILING, textureTiling);
                visualSharedMaterial.SetVector(NOISE_TEXTURE_SCROLLING, textureScrolling);
            }
            else
            {
                if (visualSharedMaterial.IsKeywordEnabled(USE_SCROLLING_NOISE)) visualSharedMaterial.DisableKeyword(USE_SCROLLING_NOISE);
            }
        }
        #endregion Grid Area Visual Initialize Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID AREA VISUAL UPDATE FUNCTIONS                                             ///
        ///-------------------------------------------------------------------------------///
        
        private void Update()
        {
            if (!objectsActiveSelf) return;
            if (updateOnPositionChange && previousTransformPosition != transform.position && warpAroundSurfaceBelow) 
            {
                foreach (GameObject quad in quads)
                {
                    UpdateQuadVertices(quad);
                }
            }
        }
        
        ///-------------------------------------------------------------------------------///
        /// GRID AREA VISUAL INPUT HANDLE FUNCTIONS                                       ///
        ///-------------------------------------------------------------------------------///
        
        public void SetInputEnableGridAreaVisual()
        {
            if (lerpCoroutine != null) StopCoroutine(lerpCoroutine);
            lerpCoroutine = StartCoroutine(LerpAndEnable());
        }

        public void SetInputDisableGridAreaVisual()
        {
            if (lerpCoroutine != null) StopCoroutine(lerpCoroutine);
            lerpCoroutine = StartCoroutine(LerpAndDisable());
        }

        #region Grid Area Visual Input Supporter Functions Start:
        private IEnumerator LerpAndEnable()
        {
            foreach (GameObject quad in quads)
            {
                quad.SetActive(true);
            }
            objectsActiveSelf = true;

            while (true)
            {
                if (visualSharedMaterial)
                {
                    // Get the current colors
                    Color currentColor = visualSharedMaterial.GetColor(CELL_COLOR_OVERRIDE);
                    Color currentHDRColor = visualSharedMaterial.GetColor(HDR_OVERRIDE);

                    // Calculate the new colors
                    Color newColor = Color.Lerp(currentColor, cellShowColor, (colorTransitionSpeed / 4) * Time.deltaTime);
                    Color newHDRColor = Color.Lerp(currentHDRColor, cellShowColorHDR, (colorTransitionSpeed / 4) * Time.deltaTime);

                    // Apply the new colors
                    visualSharedMaterial.SetColor(CELL_COLOR_OVERRIDE, newColor);
                    visualSharedMaterial.SetColor(HDR_OVERRIDE, newHDRColor);

                    // Check if the lerp is complete
                    if (Approximately(newColor, cellShowColor) && Approximately(newHDRColor, cellShowColorHDR)) break;
                }
                yield return null;
            }
        }

        private IEnumerator LerpAndDisable()
        {
            while (true)
            {
                if (visualSharedMaterial)
                {
                    // Get the current colors
                    Color currentColor = visualSharedMaterial.GetColor(CELL_COLOR_OVERRIDE);
                    Color currentHDRColor = visualSharedMaterial.GetColor(HDR_OVERRIDE);

                    // Calculate the new colors
                    Color newColor = Color.Lerp(currentColor, cellHideColor, colorTransitionSpeed * Time.deltaTime);
                    Color newHDRColor = Color.Lerp(currentHDRColor, cellHideColorHDR, colorTransitionSpeed * Time.deltaTime);

                    // Apply the new colors
                    visualSharedMaterial.SetColor(CELL_COLOR_OVERRIDE, newColor);
                    visualSharedMaterial.SetColor(HDR_OVERRIDE, newHDRColor);

                    // Check if the lerp is complete
                    if (Approximately(newColor, cellHideColor) && Approximately(newHDRColor, cellHideColorHDR)) break;
                }
                yield return null;
            }

            foreach (GameObject quad in quads)
            {
                quad.SetActive(false);
            }
            objectsActiveSelf = false;
        }

        private bool Approximately(Color a, Color b, float threshold = 0.01f)
        {
            return Mathf.Abs(a.r - b.r) < threshold && Mathf.Abs(a.g - b.g) < threshold && Mathf.Abs(a.b - b.b) < threshold && Mathf.Abs(a.a - b.a) < threshold;
        }
        #endregion Grid Area Visual Input Supporter Functions End:


        ///-------------------------------------------------------------------------------///
        /// EDITOR GIZMOS FUNCTIONS                                                       ///
        ///-------------------------------------------------------------------------------///
        
        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableGizmos) return;
            
            if (areaShape == AreaShape.Rectangle) DrawRectangleGizmos();
            else DrawCircleGizmos();
        }

        #region Grid Area Visual Gizmos Functions Start:
        private void DrawRectangleGizmos()
        {
            Vector3 offset = gridAxis is GridAxis.XZ ? new Vector3((width * cellSize) * 0.5f, 0, (length * cellSize) * 0.5f) : new Vector3((width * cellSize) * 0.5f, (length * cellSize) * 0.5f, 0);
            Vector3 basePosition = transform.position - offset;

            if (enableSimplifiedGizmos)
            {
                // Draw simple rectangle outline
                Vector3 bottomLeft;
                Vector3 bottomRight;
                Vector3 topLeft;
                Vector3 topRight;

                if (gridAxis is GridAxis.XZ)
                {
                    bottomLeft = basePosition;
                    bottomRight = basePosition + new Vector3(width * cellSize, 0, 0);
                    topLeft = basePosition + new Vector3(0, 0, length * cellSize);
                    topRight = basePosition + new Vector3(width * cellSize, 0, length * cellSize);
                }
                else
                {
                    bottomLeft = basePosition;
                    bottomRight = basePosition + new Vector3(width * cellSize, 0, 0);
                    topLeft = basePosition + new Vector3(0, length * cellSize, 0);
                    topRight = basePosition + new Vector3(width * cellSize, length * cellSize, 0);
                }
                
                CustomGizmosUtilities.DrawAAPolyLine(bottomLeft, bottomRight, 2, gizmoColor);
                CustomGizmosUtilities.DrawAAPolyLine(bottomLeft, topLeft, 2, gizmoColor);
                CustomGizmosUtilities.DrawAAPolyLine(topRight, bottomRight, 2, gizmoColor);
                CustomGizmosUtilities.DrawAAPolyLine(topRight, topLeft, 2, gizmoColor);
            }
            else
            {
                // Draw detailed rectangle grid
                for (int x = 0; x <= width; x++)
                {
                    Vector3 startPos = basePosition + new Vector3(x * cellSize, 0, 0);
                    Vector3 endPos = gridAxis is GridAxis.XZ ? startPos + new Vector3(0, 0, length * cellSize) : startPos + new Vector3(0, length * cellSize, 0);
                    CustomGizmosUtilities.DrawAAPolyLine(startPos, endPos, 2, gizmoColor);
                }

                for (int z = 0; z <= length; z++)
                {
                    Vector3 startPos = gridAxis is GridAxis.XZ ? basePosition + new Vector3(0, 0, z * cellSize) : basePosition + new Vector3(0, z * cellSize, 0);
                    Vector3 endPos = startPos + new Vector3(width * cellSize, 0, 0);
                    CustomGizmosUtilities.DrawAAPolyLine(startPos, endPos, 2, gizmoColor);
                }
            }
        }

        private void DrawCircleGizmos()
        {
            if (enableSimplifiedGizmos)
            {
                // Draw simple circle
                Vector3 rotation = gridAxis is GridAxis.XZ ? new Vector3(90, 0, 0) : Vector3.zero;
                CustomGizmosUtilities.DrawAAPolyCircle(transform.position, Quaternion.Euler(rotation), radius, 36, false, 2, gizmoColor);
            }
            else
            {
                // Draw detailed circle grid
                Vector3 offset = gridAxis is GridAxis.XZ ? new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), 0, (length * cellSize) * 0.5f - (0.5f * cellSize)) :
                    new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), (length * cellSize) * 0.5f - (0.5f * cellSize), 0);
                Vector3 basePosition = transform.position - offset;
                float radiusSquared = radius * radius;
                HashSet<(Vector3, Vector3)> drawnLines = new HashSet<(Vector3, Vector3)>();

                Vector3 currentPosition = basePosition;

                if (gridAxis is GridAxis.XZ)
                {
                    for (int x = 0; x < width; x++, currentPosition.x += cellSize)
                    {
                        currentPosition.z = basePosition.z;
                        for (int z = 0; z < length; z++, currentPosition.z += cellSize)
                        {
                            if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                            {
                                DrawCellLines(currentPosition, drawnLines);
                            }
                        }
                    }
                }
                else
                {
                    for (int x = 0; x < width; x++, currentPosition.x += cellSize)
                    {
                        currentPosition.y = basePosition.y;
                        for (int y = 0; y < length; y++, currentPosition.y += cellSize)
                        {
                            if ((currentPosition - transform.position).sqrMagnitude <= radiusSquared)
                            {
                                DrawCellLines(currentPosition, drawnLines);
                            }
                        }
                    }
                }
            }
        }

        private void DrawCellLines(Vector3 position, HashSet<(Vector3, Vector3)> drawnLines)
        {
            Vector3 halfCellSize = gridAxis is GridAxis.XZ ? new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f) : new Vector3(cellSize * 0.5f, cellSize * 0.5f, 0);

            Vector3 bottomLeft = position - halfCellSize;
            Vector3 bottomRight = gridAxis is GridAxis.XZ ? position + new Vector3(halfCellSize.x, 0, -halfCellSize.z) : position + new Vector3(halfCellSize.x, -halfCellSize.y, 0);
            Vector3 topLeft = gridAxis is GridAxis.XZ ? position + new Vector3(-halfCellSize.x, 0, halfCellSize.z) : position + new Vector3(-halfCellSize.x, halfCellSize.y, 0);
            Vector3 topRight = position + halfCellSize;

            DrawLineIfNotDrawn(bottomLeft, bottomRight, drawnLines);
            DrawLineIfNotDrawn(topLeft, topRight, drawnLines);
            DrawLineIfNotDrawn(bottomLeft, topLeft, drawnLines);
            DrawLineIfNotDrawn(bottomRight, topRight, drawnLines);
        }

        private void DrawLineIfNotDrawn(Vector3 start, Vector3 end, HashSet<(Vector3, Vector3)> drawnLines)
        {
            var lineKey = (start, end);
            var reverseLineKey = (end, start);

            if (!drawnLines.Contains(lineKey) && !drawnLines.Contains(reverseLineKey))
            {
                CustomGizmosUtilities.DrawAAPolyLine(start, end, 2, gizmoColor);
                drawnLines.Add(lineKey);
                drawnLines.Add(reverseLineKey);
            }
        }
        #endregion Grid Area Visual Gizmos Functions End:
        #endif
    }
}