using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class FI_WindLineAssetBuilder
{
    private const string ShaderPath = "Assets/Resources/Shaders/FI_WindLine.shader";
    private const string TexturePath = "Assets/Res/Common/WindEffect/T_WindStreaks.png";
    private const string MaterialPath = "Assets/Res/Common/WindEffect/M_WindLine.mat";
    private const string PrefabPath = "Assets/Resources/Prefab/Element/VFX_WindLine.prefab";

    static FI_WindLineAssetBuilder()
    {
        EditorApplication.delayCall += EnsureAssets;
    }

    [MenuItem("Tools/Floating Island/重建 LineRenderer 风特效")]
    public static void RebuildAssets()
    {
        BuildAssets(true);
    }

    private static void EnsureAssets()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureAssets;
            return;
        }

        BuildAssets(false);
    }

    private static void BuildAssets(bool rebuildPrefab)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null)
        {
            return;
        }

        ConfigureTextureImporter();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        if (texture == null)
        {
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        bool newMaterial = material == null;
        if (newMaterial)
        {
            material = new Material(shader) { name = "M_WindLine" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.shader = shader;
        bool initializeMaterial = newMaterial
            || rebuildPrefab
            || !material.HasProperty("_MainTex")
            || material.GetTexture("_MainTex") == null;
        if (!material.HasProperty("_MainTex"))
        {
            // Shader imports and editor compilation may finish in different frames.
            // Retry after Unity has registered the shader property table.
            EditorApplication.delayCall += EnsureAssets;
            return;
        }

        material.SetTexture("_MainTex", texture);
        if (initializeMaterial)
        {
            material.SetTextureScale("_MainTex", new Vector2(1.35f, 0.14f));
            material.SetTextureOffset("_MainTex", new Vector2(0f, 0.285f));
            material.SetColor("_Color", new Color(0.55f, 0.9f, 1f, 1f));
            material.SetFloat("_Intensity", 2f);
            material.SetFloat("_Alpha", 0.8f);
            material.SetFloat("_Feather", 0.35f);
            material.SetVector("_ScrollSpeed", new Vector4(-0.45f, 0f, 0f, 0f));
            material.SetFloat("_DistortionStrength", 0.035f);
            material.SetFloat("_DistortionScale", 2.4f);
            material.SetFloat("_DistortionSpeed", 1.1f);
            material.SetFloat("_EdgePower", 2f);
            material.SetFloat("_VisibleWidth", 1f);
            material.SetFloat("_TipFade", 0.13f);
            material.SetFloat("_PulseStrength", 0.35f);
            material.SetFloat("_PulseScale", 1.6f);
            material.SetFloat("_PulseSpeed", 2f);
            material.SetFloat("_PulseSharpness", 1.5f);
            material.SetFloat("_BrushStrength", 1f);
            material.SetFloat("_BrushHeadLength", 0.22f);
            material.SetFloat("_BrushTailLength", 0.22f);
            material.SetFloat("_BrushSharpness", 1.6f);
            material.SetFloat("_BrushBias", 0f);
            material.SetFloat("_BlendMode", 1f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.One);
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);

        if (rebuildPrefab || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            CreatePrefab(material);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (rebuildPrefab)
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Debug.Log("[FI Wind Line] 已重建材质与预制体：" + PrefabPath);
        }
    }

    private static void ConfigureTextureImporter()
    {
        if (AssetImporter.GetAtPath(TexturePath) is not TextureImporter importer)
        {
            return;
        }

        bool dirty = importer.textureType != TextureImporterType.Default
            || importer.sRGBTexture
            || importer.wrapMode != TextureWrapMode.Repeat
            || importer.mipmapEnabled
            || importer.maxTextureSize != 1024
            || importer.textureCompression != TextureImporterCompression.CompressedHQ;

        if (!dirty)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }

    private static void CreatePrefab(Material material)
    {
        GameObject root = new GameObject("VFX_WindLine");
        root.AddComponent<FI_WindLineEffect>();

        try
        {
            CreateLine(root.transform, material, 0, 0.18f, 0.32f, 0f);
            CreateLine(root.transform, material, 1, 0.12f, 0.23f, 1.9f);
            CreateLine(root.transform, material, 2, 0.09f, 0.17f, 4.1f);
            root.GetComponent<FI_WindLineEffect>().ApplyProperties();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateLine(
        Transform parent,
        Material material,
        int index,
        float width,
        float amplitude,
        float phase)
    {
        GameObject child = new GameObject("Wind_Streak_" + (index + 1));
        child.transform.SetParent(parent, false);

        LineRenderer line = child.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 4;
        line.numCapVertices = 3;
        line.widthMultiplier = width;
        line.widthCurve = new AnimationCurve(
            new Keyframe(0f, 0.15f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.78f, 0.82f),
            new Keyframe(1f, 0.05f));
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.lightProbeUsage = LightProbeUsage.Off;
        line.reflectionProbeUsage = ReflectionProbeUsage.Off;
        line.allowOcclusionWhenDynamic = false;
        line.sortingOrder = 10 + index;

        const int pointCount = 18;
        Vector3[] positions = new Vector3[pointCount];
        float length = 7.6f - index * 0.65f;
        float verticalOffset = (index - 1) * 0.46f;
        for (int i = 0; i < pointCount; i++)
        {
            float t = i / (pointCount - 1f);
            float x = Mathf.Lerp(-length * 0.5f, length * 0.5f, t) + index * 0.16f;
            float y = verticalOffset
                + Mathf.Sin(t * Mathf.PI * 2f + phase) * amplitude
                + Mathf.Sin(t * Mathf.PI * 5f + phase * 0.7f) * amplitude * 0.22f;
            float z = index * 0.035f + Mathf.Sin(t * Mathf.PI * 3f + phase) * 0.025f;
            positions[i] = new Vector3(x, y, z);
        }

        line.positionCount = positions.Length;
        line.SetPositions(positions);

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(index == 0 ? 1f : 0.72f, 0.14f),
                new GradientAlphaKey(index == 0 ? 0.75f : 0.5f, 0.78f),
                new GradientAlphaKey(0f, 1f)
            });
        line.colorGradient = gradient;
    }
}
