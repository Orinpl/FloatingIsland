using System.Collections.Generic;
using UnityEngine;
using SoulGames.Utilities;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Effects/Grid Cell Indicator Visual Generator", 2)]
    public class GridCellIndicatorVisualGenerator : MonoBehaviour
    {
        [SerializeField] private bool warpAroundSurfaceBelow;
        [SerializeField] private LayerMask customWarpSurfaceLayerMask;
        [SerializeField] private float raycastAboveOffset = 10f;
        [SerializeField] private float spaceBetweenVisualAndSurface = 0.25f;

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
        [SerializeField] public Color cellShowColor = new Color32(255, 255, 255, 255);
        [SerializeField] public Color cellHideColor = new Color32(255, 255, 255, 0);
        [SerializeField] [ColorUsage(true, true)] public Color cellShowColorHDR = new Color32(0, 0, 0, 255);
        [SerializeField] [ColorUsage(true, true)] public Color cellHideColorHDR = new Color32(0, 0, 0, 0);
        [SerializeField] public float colorTransitionSpeed = 20f;

        [SerializeField] public bool useScrollingNoise;
        [SerializeField] public Texture noiseTexture;
        [SerializeField] public Vector2 textureTiling = new Vector2(1, 1);
        [SerializeField] public Vector2 textureScrolling = new Vector2(0, 0.1f);

        private List<GameObject> quads;
        private Vector3 previousTransformPosition;
        private EasyGridBuilderPro activeEasyGridBuilderPro;
    
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
        /// GRID CELL INDICATOR INITIALIZE FUNCTIONS                                      ///
        ///-------------------------------------------------------------------------------///

        private void Start()
        {
            previousTransformPosition = transform.position;
            activeEasyGridBuilderPro = GridManager.Instance.GetActiveEasyGridBuilderPro();
            GridManager.Instance.OnActiveEasyGridBuilderProChanged += OnActiveEasyGridBuilderProChanged;

            GenerateQuads();
            SetupMaterials();
        }

        private void OnDestroy()
        {
            GridManager.Instance.OnActiveEasyGridBuilderProChanged -= OnActiveEasyGridBuilderProChanged;
        }

        #region Grid Cell Indicator Events Functions Start:
        private void OnActiveEasyGridBuilderProChanged(EasyGridBuilderPro activeEasyGridBuilderProSystem)
        {
            activeEasyGridBuilderPro = activeEasyGridBuilderProSystem;

            ClearQuads();
            GenerateQuads();
            SetupMaterials();
        }
        #endregion Grid Cell Indicator Events Functions End:

        #region Grid Cell Indicator Initialization Functions Start:
        private void GenerateQuads()
        {
            quads = new List<GameObject>();
            
            if (areaShape == AreaShape.Rectangle) GenerateRectangleQuads();
            else GenerateCircleQuads();
        }

        private void GenerateRectangleQuads()
        {
            Vector3 offset = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? 
                new Vector3((width * cellSize) * 0.5f, 0, (length * cellSize) * 0.5f) :
                new Vector3((width * cellSize) * 0.5f, (length * cellSize) * 0.5f, 0);
            Vector3 basePosition = transform.position - offset;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < length; y++)
                {
                    Vector3 quadPosition = activeEasyGridBuilderPro is EasyGridBuilderProXZ ?  
                        basePosition + new Vector3(x * cellSize + cellSize * 0.5f, 0, y * cellSize + cellSize * 0.5f) :
                        basePosition + new Vector3(x * cellSize + cellSize * 0.5f, y * cellSize + cellSize * 0.5f, 0);
                    CreateQuad(quadPosition);
                }
            }
        }

        private void GenerateCircleQuads()
        {
            Vector3 offset = activeEasyGridBuilderPro is EasyGridBuilderProXZ ? 
                new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), 0, (length * cellSize) * 0.5f - (0.5f * cellSize)) :
                new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), (length * cellSize) * 0.5f - (0.5f * cellSize), 0);
            Vector3 basePosition = transform.position - offset;
            float radiusSquared = radius * radius;

            Vector3 currentPosition = basePosition;
            for (int x = 0; x < width; x++, currentPosition.x += cellSize)
            {
                if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) 
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

            if (activeEasyGridBuilderPro is EasyGridBuilderProXZ) quad.transform.rotation = Quaternion.Euler(90, 0, 0);
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

                    if (activeEasyGridBuilderPro is EasyGridBuilderProXZ)
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

        private void ClearQuads()
        {
            foreach (GameObject quad in quads)
            {
                Destroy(quad.gameObject);
            }
            quads. Clear();
        }
        #endregion Grid Cell Indicator Initialization Functions End:

        ///-------------------------------------------------------------------------------///
        /// GRID CELL INDICATOR UPDATE FUNCTIONS                                          ///
        ///-------------------------------------------------------------------------------///

        private void Update()
        {
            if (previousTransformPosition != transform.position && warpAroundSurfaceBelow) 
            {
                foreach (GameObject quad in quads)
                {
                    UpdateQuadVertices(quad);
                }
            }
        }

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

        #region Grid Cell Indicator Gizmos Functions Start:
        private void DrawRectangleGizmos()
        {
            Vector3 offset = new Vector3((width * cellSize) * 0.5f, 0, (length * cellSize) * 0.5f);
            Vector3 basePosition = transform.position - offset;

            if (enableSimplifiedGizmos)
            {
                // Draw simple rectangle outline
                Vector3 bottomLeft = basePosition;
                Vector3 bottomRight = basePosition + new Vector3(width * cellSize, 0, 0);
                Vector3 topLeft = basePosition + new Vector3(0, 0, length * cellSize);
                Vector3 topRight = basePosition + new Vector3(width * cellSize, 0, length * cellSize);

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
                    Vector3 endPos = startPos + new Vector3(0, 0, length * cellSize);
                    CustomGizmosUtilities.DrawAAPolyLine(startPos, endPos, 2, gizmoColor);
                }

                for (int z = 0; z <= length; z++)
                {
                    Vector3 startPos = basePosition + new Vector3(0, 0, z * cellSize);
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
                CustomGizmosUtilities.DrawAAPolyCircle(transform.position, Quaternion.Euler(new Vector3(90, 0, 0)), radius, 36, false, 2, gizmoColor);
            }
            else
            {
                // Draw detailed circle grid
                Vector3 offset = new Vector3((width * cellSize) * 0.5f - (0.5f * cellSize), 0, (length * cellSize) * 0.5f - (0.5f * cellSize));
                Vector3 basePosition = transform.position - offset;
                float radiusSquared = radius * radius;
                HashSet<(Vector3, Vector3)> drawnLines = new HashSet<(Vector3, Vector3)>();

                Vector3 currentPosition = basePosition;
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
        }

        private void DrawCellLines(Vector3 position, HashSet<(Vector3, Vector3)> drawnLines)
        {
            Vector3 halfCellSize = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);

            Vector3 bottomLeft = position - halfCellSize;
            Vector3 bottomRight = position + new Vector3(halfCellSize.x, 0, -halfCellSize.z);
            Vector3 topLeft = position + new Vector3(-halfCellSize.x, 0, halfCellSize.z);
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
        #endregion Grid Cell Indicator Gizmos Functions End:
        #endif
    }
}