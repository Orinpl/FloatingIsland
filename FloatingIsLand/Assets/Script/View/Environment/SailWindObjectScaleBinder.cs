using UnityEngine;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Passes the rendered object's scale to FI/Sail Wind through a MaterialPropertyBlock.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SailWindObjectScaleBinder : MonoBehaviour
    {
        private static readonly int SailObjectScale = Shader.PropertyToID("_SailObjectScale");

        [SerializeField] private bool includeChildRenderers = true;
        [SerializeField] private Vector3 referenceScale = new Vector3(100f, 100f, 100f);
        [SerializeField] private Vector3 sentScaleMultiplier = Vector3.one;

        private readonly MaterialPropertyBlock _propertyBlock = new MaterialPropertyBlock();
        private Renderer[] _renderers;
        private Vector3 _lastScale = new Vector3(float.NaN, float.NaN, float.NaN);
        private Vector3 _lastReferenceScale = new Vector3(float.NaN, float.NaN, float.NaN);

        private void OnEnable()
        {
            RefreshRenderers();
            ApplyScale(true);
        }

        private void OnValidate()
        {
            RefreshRenderers();
            ApplyScale(true);
        }

        private void LateUpdate()
        {
            ApplyScale(false);
        }

        private void OnTransformChildrenChanged()
        {
            RefreshRenderers();
            ApplyScale(true);
        }

        private void RefreshRenderers()
        {
            _renderers = includeChildRenderers
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();
        }

        private void ApplyScale(bool force)
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                RefreshRenderers();
            }

            Vector3 scale = transform.lossyScale;
            if (!force && scale == _lastScale && referenceScale == _lastReferenceScale)
            {
                return;
            }

            _lastScale = scale;
            _lastReferenceScale = referenceScale;
            sentScaleMultiplier = new Vector3(
                Mathf.Abs(scale.x) / Mathf.Max(Mathf.Abs(referenceScale.x), 0.0001f),
                Mathf.Abs(scale.y) / Mathf.Max(Mathf.Abs(referenceScale.y), 0.0001f),
                Mathf.Abs(scale.z) / Mathf.Max(Mathf.Abs(referenceScale.z), 0.0001f));
            Vector4 shaderScale = new Vector4(sentScaleMultiplier.x, sentScaleMultiplier.y, sentScaleMultiplier.z, 1f);

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer target = _renderers[i];
                if (target == null)
                {
                    continue;
                }

                target.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetVector(SailObjectScale, shaderScale);
                target.SetPropertyBlock(_propertyBlock);
            }
        }

        [ContextMenu("Capture Current Scale As Reference")]
        private void CaptureCurrentScaleAsReference()
        {
            referenceScale = transform.lossyScale;
            ApplyScale(true);
        }
    }
}
