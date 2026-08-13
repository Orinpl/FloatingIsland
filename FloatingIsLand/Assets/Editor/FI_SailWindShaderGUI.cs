using UnityEditor;

public sealed class FI_SailWindShaderGUI : FI_LitShaderGUI
{
    private static readonly string[] MaskModes = { "UV V", "Vertex Color R", "Mask Texture", "UV U Edges" };

    protected override void DrawExtraAfterBase()
    {
        DrawSection("Wind", () =>
        {
            Find("_WindGroup").floatValue = 1f;
            DrawRange("_SailMaxDisplacement", "Max Offset");
            DrawRange("_SailSwayAmplitude", "Sway");
            DrawRange("_SailWaveSpeed", "Wave Speed");
            DrawRange("_SailFlutterAmplitude", "Flutter");
            DrawRange("_SailWindPush", "Wind Push");

            EditorGUILayout.Space(4f);
            DrawPopup("_SailMaskMode", "Pinned Mode", MaskModes);
            float maskMode = Find("_SailMaskMode").floatValue;
            if (maskMode > 1.5f && maskMode < 2.5f)
            {
                ShaderProperty("_SailMaskTex", "Mask Texture");
            }

            DrawToggle("_SailMaskInvert", "Invert Pin");
            DrawRange("_SailMaskStart", "Fixed");
            DrawRange("_SailMaskEnd", "Full Move");
        });
    }

    private void DrawRange(string propertyName, string label)
    {
        ShaderProperty(propertyName, label);
    }

    private void ShaderProperty(string propertyName, string label)
    {
        editor.ShaderProperty(Find(propertyName), label);
    }
}
