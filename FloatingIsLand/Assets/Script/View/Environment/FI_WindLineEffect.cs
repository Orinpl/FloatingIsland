using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Effects/FI Wind Line Effect")]
public sealed class FI_WindLineEffect : MonoBehaviour
{
    [Header("宽度")]
    [SerializeField] private bool controlLineWidths = true;
    [SerializeField, Range(0.001f, 5f)] private float overallWidth = 0.18f;
    [SerializeField, Range(0.1f, 1f)] private float layerWidthFalloff = 0.72f;

    [Header("动画")]
    [SerializeField, Range(0f, 3f)] private float speedMultiplier = 1f;
    [SerializeField] private float timeOffsetStep = 0.73f;

    private static readonly int TimeOffsetId = Shader.PropertyToID("_TimeOffset");
    private static readonly int SpeedMultiplierId = Shader.PropertyToID("_SpeedMultiplier");
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        ApplyProperties();
    }

    private void OnValidate()
    {
        ApplyProperties();
    }

    [ContextMenu("刷新风线参数")]
    public void ApplyProperties()
    {
        if (propertyBlock == null)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        LineRenderer[] lines = GetComponentsInChildren<LineRenderer>(true);
        for (int i = 0; i < lines.Length; i++)
        {
            LineRenderer line = lines[i];
            if (controlLineWidths)
            {
                line.widthMultiplier = overallWidth * Mathf.Pow(layerWidthFalloff, i);
            }

            // Clear legacy appearance overrides so color/intensity stay editable
            // on the shared material. Only hidden per-line animation data remains.
            propertyBlock.Clear();
            propertyBlock.SetFloat(TimeOffsetId, i * timeOffsetStep);
            propertyBlock.SetFloat(SpeedMultiplierId, speedMultiplier);
            line.SetPropertyBlock(propertyBlock);
        }
    }
}
