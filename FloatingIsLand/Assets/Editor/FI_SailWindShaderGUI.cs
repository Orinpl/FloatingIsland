using UnityEditor;
using UnityEngine;

public sealed class FI_SailWindShaderGUI : ShaderGUI
{
    private static class Styles
    {
        public static readonly GUIContent Surface = new GUIContent("Surface");
        public static readonly GUIContent Lighting = new GUIContent("Lighting");
        public static readonly GUIContent Highlights = new GUIContent("Highlights");
        public static readonly GUIContent SailMotion = new GUIContent("Sail Motion");
        public static readonly GUIContent AttachmentMask = new GUIContent("Attachment Mask");
        public static readonly GUIContent Advanced = new GUIContent("Advanced");
        public static readonly GUIContent Texture = new GUIContent("Texture");
        public static readonly GUIContent MaskTexture = new GUIContent(
            "Displacement Mask",
            "Mask Texture mode reads its red channel through UV0. White moves and black stays fixed.");
        public static readonly GUIContent MaskMode = new GUIContent(
            "Displacement Control Mode",
            "Vertex Color reads vertex color R. Mask Texture reads a grayscale texture through UV0.");
        public static readonly GUIContent MaskInvert = new GUIContent(
            "Invert Vertex Mask",
            "Invert when the connected edge is stored as 1 instead of 0.");
        public static readonly GUIContent MaskRemap = new GUIContent(
            "Fixed / Full Movement",
            "Values at or below Fixed are pinned. Values at or above Full Movement receive the full displacement.");
        public static readonly GUIContent MaskStrength = new GUIContent(
            "Mask Strength",
            "Multiplier for all vertex displacement after channel and texture masking.");

        public static readonly string[] MaskModeNames = { "Vertex Color", "Mask Texture" };
    }

    private MaterialEditor editor;
    private MaterialProperty[] properties;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        editor = materialEditor;
        properties = props;

        EditorGUI.BeginChangeCheck();
        DrawSurface();
        DrawLighting();
        DrawHighlights();
        DrawSailMotion();
        DrawAttachmentMask();
        DrawAdvanced();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Object target in editor.targets)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }

    private void DrawSurface()
    {
        DrawSection(Styles.Surface, () =>
        {
            MaterialProperty mainTex = Find("_MainTex");
            editor.TexturePropertySingleLine(Styles.Texture, mainTex, Find("_BaseColor"));
            editor.TextureScaleOffsetProperty(mainTex);
        });
    }

    private void DrawLighting()
    {
        DrawSection(Styles.Lighting, () =>
        {
            editor.ColorProperty(Find("_TopColor"), "Top Light Tint");
            editor.ColorProperty(Find("_ShadowColor"), "Cool Shadow Tint");
            editor.VectorProperty(Find("_LightDirection"), "Light Direction");
            editor.RangeProperty(Find("_Ambient"), "Ambient");
            editor.RangeProperty(Find("_LightSteps"), "Lowpoly Light Steps");
            editor.RangeProperty(Find("_TopTint"), "Upward Tint");
        });
    }

    private void DrawHighlights()
    {
        DrawSection(Styles.Highlights, () =>
        {
            editor.ColorProperty(Find("_HighlightColor"), "Highlight Color");
            editor.RangeProperty(Find("_HighlightStrength"), "Face Highlight");
            editor.RangeProperty(Find("_HighlightSize"), "Face Highlight Size");
            editor.RangeProperty(Find("_BevelHighlight"), "Bevel Highlight");
            editor.RangeProperty(Find("_BevelSharpness"), "Bevel Sharpness");
            editor.RangeProperty(Find("_BevelWidth"), "Fake Bevel Width");
            editor.RangeProperty(Find("_RimStrength"), "Rim Highlight");
            editor.RangeProperty(Find("_RimPower"), "Rim Power");
        });
    }

    private void DrawSailMotion()
    {
        DrawSection(Styles.SailMotion, () =>
        {
            editor.RangeProperty(Find("_SailSwayAmplitude"), "Sway Amplitude");
            editor.RangeProperty(Find("_SailWaveSpeed"), "Wave Speed");
            editor.RangeProperty(Find("_SailWaveScale"), "Wave Scale");
            editor.RangeProperty(Find("_SailClothAmplitude"), "Cloth Amplitude");
            editor.RangeProperty(Find("_SailClothFrequency"), "Cloth Frequency");
            editor.RangeProperty(Find("_SailFlutterAmplitude"), "Flutter Amplitude");
            editor.RangeProperty(Find("_SailFlutterSpeed"), "Flutter Speed");
            editor.RangeProperty(Find("_SailFlutterScale"), "Flutter Scale");
            editor.RangeProperty(Find("_SailWindPush"), "Wind Push");
            editor.RangeProperty(Find("_SailNormalPush"), "Normal Push");
        });
    }

    private void DrawAttachmentMask()
    {
        DrawSection(Styles.AttachmentMask, () =>
        {
            MaterialProperty mode = Find("_SailMaskMode");
            DrawMaskMode(mode);
            if (mode.hasMixedValue || mode.floatValue > 0.5f)
            {
                editor.TexturePropertySingleLine(Styles.MaskTexture, Find("_SailMaskTex"));
            }

            DrawToggle(Find("_SailMaskInvert"), Styles.MaskInvert);
            DrawMaskRemap();
            editor.RangeProperty(Find("_SailMaskStrength"), Styles.MaskStrength.text);

            string sourceDescription = mode.floatValue < 0.5f
                ? "Vertex Color mode: R = 0 is fixed, R = 1 receives full displacement."
                : "Mask Texture mode: black is fixed, white receives full displacement.";
            EditorGUILayout.HelpBox(
                sourceDescription + " Increase Fixed to keep a wider attachment strip completely pinned.",
                MessageType.Info);
        });
    }

    private void DrawMaskMode(MaterialProperty mode)
    {
        EditorGUI.showMixedValue = mode.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        int value = EditorGUILayout.Popup(
            Styles.MaskMode,
            Mathf.Clamp(Mathf.RoundToInt(mode.floatValue), 0, 1),
            Styles.MaskModeNames);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(Styles.MaskMode.text);
            mode.floatValue = value;
        }

        EditorGUI.showMixedValue = false;
    }

    private void DrawToggle(MaterialProperty property, GUIContent label)
    {
        EditorGUI.showMixedValue = property.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        bool value = EditorGUILayout.Toggle(label, property.floatValue > 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(label.text);
            property.floatValue = value ? 1f : 0f;
        }

        EditorGUI.showMixedValue = false;
    }

    private void DrawMaskRemap()
    {
        MaterialProperty startProperty = Find("_SailMaskStart");
        MaterialProperty endProperty = Find("_SailMaskEnd");
        float start = startProperty.floatValue;
        float end = endProperty.floatValue;

        EditorGUI.showMixedValue = startProperty.hasMixedValue || endProperty.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.MinMaxSlider(Styles.MaskRemap, ref start, ref end, 0f, 1f);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(EditorGUIUtility.labelWidth);
            start = EditorGUILayout.FloatField(start, GUILayout.MinWidth(35f));
            end = EditorGUILayout.FloatField(end, GUILayout.MinWidth(35f));
        }

        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(Styles.MaskRemap.text);
            start = Mathf.Clamp(start, 0f, 0.9999f);
            end = Mathf.Clamp(end, start + 0.0001f, 1f);
            startProperty.floatValue = start;
            endProperty.floatValue = end;
        }

        EditorGUI.showMixedValue = false;
    }

    private void DrawAdvanced()
    {
        DrawSection(Styles.Advanced, () =>
        {
            editor.RenderQueueField();
            editor.EnableInstancingField();
            editor.DoubleSidedGIField();
        });
    }

    private static void DrawSection(GUIContent title, System.Action drawContent)
    {
        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            drawContent.Invoke();
            EditorGUI.indentLevel--;
        }
    }

    private MaterialProperty Find(string propertyName)
    {
        return FindProperty(propertyName, properties);
    }
}
