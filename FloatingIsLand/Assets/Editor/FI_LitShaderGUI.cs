using UnityEditor;
using UnityEngine;

public sealed class FI_LitShaderGUI : ShaderGUI
{
    private static class Styles
    {
        public static readonly GUIContent Surface = new GUIContent("Surface");
        public static readonly GUIContent Lighting = new GUIContent("Lighting");
        public static readonly GUIContent Highlights = new GUIContent("Highlights");
        public static readonly GUIContent Advanced = new GUIContent("Advanced");
        public static readonly GUIContent Texture = new GUIContent("Texture");
        public static readonly GUIContent BaseColor = new GUIContent("Base Color");
        public static readonly GUIContent TopColor = new GUIContent("Top Light Tint");
        public static readonly GUIContent ShadowColor = new GUIContent("Cool Shadow Tint");
        public static readonly GUIContent LightDirection = new GUIContent("Light Direction");
        public static readonly GUIContent Ambient = new GUIContent("Ambient");
        public static readonly GUIContent LightSteps = new GUIContent("Lowpoly Light Steps");
        public static readonly GUIContent TopTint = new GUIContent("Upward Tint");
        public static readonly GUIContent HighlightColor = new GUIContent("Highlight Color");
        public static readonly GUIContent FaceHighlight = new GUIContent("Face Highlight");
        public static readonly GUIContent FaceHighlightSize = new GUIContent("Face Highlight Size");
        public static readonly GUIContent BevelHighlight = new GUIContent("Bevel Highlight");
        public static readonly GUIContent BevelSharpness = new GUIContent("Bevel Sharpness");
        public static readonly GUIContent FakeBevelWidth = new GUIContent("Fake Bevel Width");
        public static readonly GUIContent RimHighlight = new GUIContent("Rim Highlight");
        public static readonly GUIContent RimPower = new GUIContent("Rim Power");
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
            MaterialProperty baseColor = Find("_BaseColor");
            editor.TexturePropertySingleLine(Styles.Texture, mainTex, baseColor);
            editor.TextureScaleOffsetProperty(mainTex);
        });
    }

    private void DrawLighting()
    {
        DrawSection(Styles.Lighting, () =>
        {
            editor.ColorProperty(Find("_TopColor"), Styles.TopColor.text);
            editor.ColorProperty(Find("_ShadowColor"), Styles.ShadowColor.text);
            editor.VectorProperty(Find("_LightDirection"), Styles.LightDirection.text);
            editor.RangeProperty(Find("_Ambient"), Styles.Ambient.text);
            editor.RangeProperty(Find("_LightSteps"), Styles.LightSteps.text);
            editor.RangeProperty(Find("_TopTint"), Styles.TopTint.text);
        });
    }

    private void DrawHighlights()
    {
        DrawSection(Styles.Highlights, () =>
        {
            editor.ColorProperty(Find("_HighlightColor"), Styles.HighlightColor.text);
            editor.RangeProperty(Find("_HighlightStrength"), Styles.FaceHighlight.text);
            editor.RangeProperty(Find("_HighlightSize"), Styles.FaceHighlightSize.text);
            editor.RangeProperty(Find("_BevelHighlight"), Styles.BevelHighlight.text);
            editor.RangeProperty(Find("_BevelSharpness"), Styles.BevelSharpness.text);
            editor.RangeProperty(Find("_BevelWidth"), Styles.FakeBevelWidth.text);
            editor.RangeProperty(Find("_RimStrength"), Styles.RimHighlight.text);
            editor.RangeProperty(Find("_RimPower"), Styles.RimPower.text);
        });
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

    private void DrawSection(GUIContent title, System.Action drawContent)
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
