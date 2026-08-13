using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Publishes a global 3D wind field for wind-driven shaders.
    /// This is a presentation-layer controller; it does not mutate domain wind data.
    /// </summary>
    [ExecuteAlways]
    public sealed class GlobalWindFieldController : MonoBehaviour
    {
        [SerializeField] private Vector3 fieldOrigin;
        [SerializeField] private Vector3 fieldSize = new Vector3(64f, 16f, 64f);
        [SerializeField] private Vector3 fieldScrollSpeed = new Vector3(0.015f, 0f, 0f);
        [SerializeField, Min(0f)] private float globalStrength = 1f;
        [SerializeField, Range(0f, 1f)] private float mainDirectionWeight = 0.75f;
        [SerializeField] private Texture3D windFieldTexture;
        [SerializeField] private bool generateProceduralField = true;
        [SerializeField, Range(4, 64)] private int proceduralResolution = 16;
        [SerializeField, Range(0f, 1f)] private float proceduralDirectionNoise = 0.18f;
        [SerializeField, Range(0f, 1f)] private float proceduralStrengthNoise = 0.2f;
        [SerializeField] private bool drawWindGizmos = true;
        [SerializeField] private bool drawGizmosWhenUnselected = true;
        [SerializeField, Min(0.1f)] private float gizmoArrowLength = 6f;
        [SerializeField, Range(1, 9)] private int gizmoStreamLineCount = 5;

        private static readonly int GlobalWindField3D = Shader.PropertyToID("_GlobalWindField3D");
        private static readonly int GlobalWindFieldOrigin = Shader.PropertyToID("_GlobalWindFieldOrigin");
        private static readonly int GlobalWindFieldSize = Shader.PropertyToID("_GlobalWindFieldSize");
        private static readonly int GlobalWindDirection = Shader.PropertyToID("_GlobalWindDirection");
        private static readonly int GlobalWindFieldScrollSpeed = Shader.PropertyToID("_GlobalWindFieldScrollSpeed");
        private static readonly int GlobalWindStrength = Shader.PropertyToID("_GlobalWindStrength");
        private static readonly int GlobalWindMainDirectionWeight = Shader.PropertyToID("_GlobalWindMainDirectionWeight");
        private static readonly int GlobalWindFieldEnabled = Shader.PropertyToID("_GlobalWindFieldEnabled");

        private Texture3D _proceduralTexture;
        private Texture3D _defaultWindFieldTexture;
        private int _lastProceduralResolution;
        private Vector3 _lastBaseDirection;
        private float _lastDirectionNoise;
        private float _lastStrengthNoise;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureRuntimeInstance()
        {
            if (FindObjectOfType<GlobalWindFieldController>() != null)
            {
                return;
            }

            var instance = new GameObject("Global Wind Field");
            DontDestroyOnLoad(instance);
            instance.AddComponent<GlobalWindFieldController>();
        }

        public Vector3 FieldOrigin
        {
            get => fieldOrigin;
            set => fieldOrigin = value;
        }

        public Vector3 FieldSize
        {
            get => fieldSize;
            set => fieldSize = value;
        }

        public float GlobalStrength
        {
            get => globalStrength;
            set => globalStrength = Mathf.Max(0f, value);
        }

        public float MainDirectionWeight
        {
            get => mainDirectionWeight;
            set => mainDirectionWeight = Mathf.Clamp01(value);
        }

        public Vector3 FieldScrollSpeed
        {
            get => fieldScrollSpeed;
            set => fieldScrollSpeed = value;
        }

        public Texture3D WindFieldTexture
        {
            get => windFieldTexture;
            set => windFieldTexture = value;
        }

        private void OnEnable()
        {
            Publish();
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(GlobalWindFieldEnabled, 0f);
        }

        private void LateUpdate()
        {
            Publish();
        }

        private void OnValidate()
        {
            fieldSize.x = Mathf.Max(0.001f, fieldSize.x);
            fieldSize.y = Mathf.Max(0.001f, fieldSize.y);
            fieldSize.z = Mathf.Max(0.001f, fieldSize.z);
            globalStrength = Mathf.Max(0f, globalStrength);
            mainDirectionWeight = Mathf.Clamp01(mainDirectionWeight);
            proceduralResolution = Mathf.Clamp(proceduralResolution, 4, 64);
            gizmoArrowLength = Mathf.Max(0.1f, gizmoArrowLength);
            gizmoStreamLineCount = Mathf.Clamp(gizmoStreamLineCount, 1, 9);
            Publish();
        }

        public void Publish()
        {
            Texture3D activeTexture = windFieldTexture;
            if (activeTexture == null)
            {
                if (_defaultWindFieldTexture == null)
                {
                    _defaultWindFieldTexture = Resources.Load<Texture3D>("Wind/GlobalWindField_Seamless");
                }

                activeTexture = _defaultWindFieldTexture;
            }

            if (activeTexture == null && generateProceduralField)
            {
                activeTexture = GetOrCreateProceduralTexture();
            }

            Vector3 direction = GetWindDirection();
            Shader.SetGlobalVector(GlobalWindFieldOrigin, fieldOrigin);
            Shader.SetGlobalVector(GlobalWindFieldSize, fieldSize);
            Shader.SetGlobalVector(GlobalWindDirection, direction);
            Shader.SetGlobalVector(GlobalWindFieldScrollSpeed, fieldScrollSpeed);
            Shader.SetGlobalFloat(GlobalWindStrength, globalStrength);
            Shader.SetGlobalFloat(GlobalWindMainDirectionWeight, mainDirectionWeight);
            Shader.SetGlobalFloat(GlobalWindFieldEnabled, activeTexture != null ? 1f : 0f);

            if (activeTexture != null)
            {
                Shader.SetGlobalTexture(GlobalWindField3D, activeTexture);
            }
        }

        private Texture3D GetOrCreateProceduralTexture()
        {
            Vector3 baseDirection = GetWindDirection();
            if (_proceduralTexture == null ||
                _lastProceduralResolution != proceduralResolution ||
                _lastBaseDirection != baseDirection ||
                !Mathf.Approximately(_lastDirectionNoise, proceduralDirectionNoise) ||
                !Mathf.Approximately(_lastStrengthNoise, proceduralStrengthNoise))
            {
                RebuildProceduralTexture(baseDirection);
            }

            return _proceduralTexture;
        }

        private void RebuildProceduralTexture(Vector3 baseDirection)
        {
            int resolution = Mathf.Clamp(proceduralResolution, 4, 64);
            var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Generated Global Wind Field",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };

            var colors = new Color[resolution * resolution * resolution];
            baseDirection = SafeDirection(baseDirection);
            int index = 0;

            for (int z = 0; z < resolution; z++)
            {
                for (int y = 0; y < resolution; y++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        float px = 2f * Mathf.PI * x / resolution;
                        float py = 2f * Mathf.PI * y / resolution;
                        float pz = 2f * Mathf.PI * z / resolution;
                        Vector3 periodicNoise = new Vector3(
                            Mathf.Sin(py) * Mathf.Cos(pz) + 0.45f * Mathf.Sin(2f * pz + px),
                            0.55f * Mathf.Sin(pz + px) + 0.35f * Mathf.Cos(2f * px - py),
                            Mathf.Cos(px) * Mathf.Sin(py) + 0.45f * Mathf.Sin(2f * py - pz));
                        Vector3 noisyDirection = SafeDirection(baseDirection + periodicNoise * proceduralDirectionNoise);
                        float periodicStrength =
                            0.55f * Mathf.Sin(px + py) +
                            0.3f * Mathf.Cos(py + pz) +
                            0.15f * Mathf.Sin(pz + px);
                        float strength = Mathf.Clamp01(0.8f + periodicStrength * proceduralStrengthNoise);

                        colors[index++] = new Color(
                            noisyDirection.x * 0.5f + 0.5f,
                            noisyDirection.y * 0.5f + 0.5f,
                            noisyDirection.z * 0.5f + 0.5f,
                            strength);
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply(false, true);

            if (_proceduralTexture != null)
            {
                DestroyGeneratedTexture(_proceduralTexture);
            }

            _proceduralTexture = texture;
            _lastProceduralResolution = resolution;
            _lastBaseDirection = baseDirection;
            _lastDirectionNoise = proceduralDirectionNoise;
            _lastStrengthNoise = proceduralStrengthNoise;
        }

        private Vector3 GetWindDirection()
        {
            return SafeDirection(transform.right);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!drawWindGizmos || !drawGizmosWhenUnselected)
            {
                return;
            }

            DrawWindGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawWindGizmos)
            {
                return;
            }

            DrawWindGizmos(true);
        }

        private void DrawWindGizmos(bool selected)
        {
            Vector3 size = new Vector3(
                Mathf.Max(0.001f, fieldSize.x),
                Mathf.Max(0.001f, fieldSize.y),
                Mathf.Max(0.001f, fieldSize.z));
            Vector3 center = fieldOrigin + size * 0.5f;
            Vector3 direction = GetWindDirection();
            float strengthScale = Mathf.Clamp01(globalStrength / 3f);
            Color fieldColor = selected
                ? new Color(0.25f, 0.65f, 1f, 0.55f)
                : new Color(0.25f, 0.65f, 1f, 0.25f);
            Color arrowColor = Color.Lerp(new Color(0.55f, 0.8f, 1f, 0.75f), new Color(0.1f, 0.9f, 1f, 1f), strengthScale);

            Gizmos.color = fieldColor;
            Gizmos.DrawWireCube(center, size);
            Gizmos.DrawWireSphere(fieldOrigin, HandleUtility.GetHandleSize(fieldOrigin) * 0.08f);

            Handles.color = arrowColor;
            DrawArrow(center - direction * gizmoArrowLength * 0.5f, direction, gizmoArrowLength);

            int lineCount = Mathf.Max(1, gizmoStreamLineCount);
            Vector3 side = Vector3.Cross(direction, Vector3.up);
            if (side.sqrMagnitude < 0.0001f)
            {
                side = Vector3.Cross(direction, Vector3.forward);
            }

            side.Normalize();
            Vector3 up = Vector3.Cross(side, direction).normalized;
            float span = Mathf.Min(size.x, size.z) * 0.35f;
            float lineLength = Mathf.Max(1f, gizmoArrowLength * 0.65f);
            for (int i = 0; i < lineCount; i++)
            {
                float t = lineCount == 1 ? 0f : i / (lineCount - 1f) - 0.5f;
                Vector3 offset = side * t * span + up * Mathf.Sin(i * 1.618f) * span * 0.12f;
                DrawArrow(center + offset - direction * lineLength * 0.5f, direction, lineLength);
            }
        }

        private static void DrawArrow(Vector3 start, Vector3 direction, float length)
        {
            Vector3 end = start + direction * length;
            Handles.DrawLine(start, end);
            Handles.ConeHandleCap(
                0,
                end,
                Quaternion.LookRotation(direction),
                HandleUtility.GetHandleSize(end) * 0.22f,
                EventType.Repaint);
        }
#endif

        private static Vector3 SafeDirection(Vector3 value)
        {
            return value.sqrMagnitude > 0.0001f ? value.normalized : Vector3.right;
        }

        private static void DestroyGeneratedTexture(Texture3D texture)
        {
            if (Application.isPlaying)
            {
                Destroy(texture);
            }
            else
            {
                DestroyImmediate(texture);
            }
        }
    }
}
