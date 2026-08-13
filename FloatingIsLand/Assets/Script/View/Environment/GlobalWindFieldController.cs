using UnityEngine;

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
        [SerializeField] private Vector3 fallbackDirection = Vector3.right;
        [SerializeField] private Vector3 fieldScrollSpeed = new Vector3(0.015f, 0f, 0f);
        [SerializeField, Min(0f)] private float globalStrength = 1f;
        [SerializeField] private Texture3D windFieldTexture;
        [SerializeField] private bool generateProceduralField = true;
        [SerializeField, Range(4, 64)] private int proceduralResolution = 16;
        [SerializeField, Range(0f, 1f)] private float proceduralDirectionNoise = 0.18f;
        [SerializeField, Range(0f, 1f)] private float proceduralStrengthNoise = 0.2f;

        private static readonly int GlobalWindField3D = Shader.PropertyToID("_GlobalWindField3D");
        private static readonly int GlobalWindFieldOrigin = Shader.PropertyToID("_GlobalWindFieldOrigin");
        private static readonly int GlobalWindFieldSize = Shader.PropertyToID("_GlobalWindFieldSize");
        private static readonly int GlobalWindDirection = Shader.PropertyToID("_GlobalWindDirection");
        private static readonly int GlobalWindFieldScrollSpeed = Shader.PropertyToID("_GlobalWindFieldScrollSpeed");
        private static readonly int GlobalWindStrength = Shader.PropertyToID("_GlobalWindStrength");
        private static readonly int GlobalWindFieldEnabled = Shader.PropertyToID("_GlobalWindFieldEnabled");

        private Texture3D _proceduralTexture;
        private Texture3D _defaultWindFieldTexture;
        private int _lastProceduralResolution;
        private Vector3 _lastFallbackDirection;
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

        public Vector3 FallbackDirection
        {
            get => fallbackDirection;
            set => fallbackDirection = value;
        }

        public float GlobalStrength
        {
            get => globalStrength;
            set => globalStrength = Mathf.Max(0f, value);
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
            proceduralResolution = Mathf.Clamp(proceduralResolution, 4, 64);
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

            Vector3 direction = SafeDirection(fallbackDirection);
            Shader.SetGlobalVector(GlobalWindFieldOrigin, fieldOrigin);
            Shader.SetGlobalVector(GlobalWindFieldSize, fieldSize);
            Shader.SetGlobalVector(GlobalWindDirection, direction);
            Shader.SetGlobalVector(GlobalWindFieldScrollSpeed, fieldScrollSpeed);
            Shader.SetGlobalFloat(GlobalWindStrength, globalStrength);
            Shader.SetGlobalFloat(GlobalWindFieldEnabled, activeTexture != null ? 1f : 0f);

            if (activeTexture != null)
            {
                Shader.SetGlobalTexture(GlobalWindField3D, activeTexture);
            }
        }

        private Texture3D GetOrCreateProceduralTexture()
        {
            if (_proceduralTexture == null ||
                _lastProceduralResolution != proceduralResolution ||
                _lastFallbackDirection != fallbackDirection ||
                !Mathf.Approximately(_lastDirectionNoise, proceduralDirectionNoise) ||
                !Mathf.Approximately(_lastStrengthNoise, proceduralStrengthNoise))
            {
                RebuildProceduralTexture();
            }

            return _proceduralTexture;
        }

        private void RebuildProceduralTexture()
        {
            int resolution = Mathf.Clamp(proceduralResolution, 4, 64);
            var texture = new Texture3D(resolution, resolution, resolution, TextureFormat.RGBA32, false)
            {
                name = "Generated Global Wind Field",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
            };

            var colors = new Color[resolution * resolution * resolution];
            Vector3 baseDirection = SafeDirection(fallbackDirection);
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
            _lastFallbackDirection = fallbackDirection;
            _lastDirectionNoise = proceduralDirectionNoise;
            _lastStrengthNoise = proceduralStrengthNoise;
        }

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
