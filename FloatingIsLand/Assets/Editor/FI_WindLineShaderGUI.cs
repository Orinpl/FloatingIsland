using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class FI_WindLineShaderGUI : ShaderGUI
{
    private MaterialEditor editor;
    private MaterialProperty[] properties;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        editor = materialEditor;
        properties = props;

        EditorGUI.BeginChangeCheck();

        DrawSection("外观", () =>
        {
            editor.TexturePropertySingleLine(Label("风丝贴图"), Find("_MainTex"), Find("_Color"));
            editor.TextureScaleOffsetProperty(Find("_MainTex"));
            editor.ShaderProperty(Find("_Intensity"), "亮度");
            editor.ShaderProperty(Find("_Alpha"), "透明度");
            editor.ShaderProperty(Find("_Feather"), "风丝羽化（0 硬 / 1 软）");
        });

        DrawSection("流动", () =>
        {
            editor.ShaderProperty(Find("_ScrollSpeed"), "流动速度 XY");
            editor.ShaderProperty(Find("_DistortionStrength"), "摆动强度");
            editor.ShaderProperty(Find("_DistortionScale"), "摆动密度");
            editor.ShaderProperty(Find("_DistortionSpeed"), "摆动速度");
        });

        DrawSection("形状", () =>
        {
            editor.ShaderProperty(Find("_VisibleWidth"), "可见宽度");
            editor.ShaderProperty(Find("_EdgePower"), "宽度边缘柔和");
            editor.ShaderProperty(Find("_TipFade"), "首尾淡出");
        });

        DrawSection("笔锋", () =>
        {
            editor.ShaderProperty(Find("_BrushStrength"), "笔锋强度");
            editor.ShaderProperty(Find("_BrushHeadLength"), "左端收尖");
            editor.ShaderProperty(Find("_BrushTailLength"), "右端收尖");
            editor.ShaderProperty(Find("_BrushSharpness"), "笔锋锐度");
            editor.ShaderProperty(Find("_BrushBias"), "笔锋偏移");
        });

        DrawSection("脉冲", () =>
        {
            editor.ShaderProperty(Find("_PulseStrength"), "亮度脉冲");
            editor.ShaderProperty(Find("_PulseScale"), "脉冲密度");
            editor.ShaderProperty(Find("_PulseSpeed"), "脉冲速度");
            editor.ShaderProperty(Find("_PulseSharpness"), "脉冲锐度");
        });

        DrawSection("渲染", () =>
        {
            editor.ShaderProperty(Find("_BlendMode"), "混合模式");
            editor.ShaderProperty(Find("_ZTestAlways"), "始终显示（忽略遮挡）");
            editor.RenderQueueField();
            editor.EnableInstancingField();
        });

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(
            "真实几何宽度在预制体根节点的 FI Wind Line Effect 上统一控制；材质的“可见宽度”用于快速收窄。“笔锋偏移”可让尖端向一侧偏斜。",
            MessageType.Info);

        if (EditorGUI.EndChangeCheck())
        {
            foreach (Object target in editor.targets)
            {
                if (target is Material material)
                {
                    SetupMaterial(material);
                    EditorUtility.SetDirty(material);
                }
            }
        }

        foreach (Object target in editor.targets)
        {
            if (target is Material material)
            {
                SetupMaterial(material);
            }
        }
    }

    private static void SetupMaterial(Material material)
    {
        bool additive = material.GetFloat("_BlendMode") > 0.5f;
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZTest", material.GetFloat("_ZTestAlways") > 0.5f
            ? (int)CompareFunction.Always
            : (int)CompareFunction.LessEqual);
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.renderQueue < (int)RenderQueue.Transparent)
        {
            material.renderQueue = (int)RenderQueue.Transparent;
        }
    }

    private void DrawSection(string title, System.Action draw)
    {
        EditorGUILayout.Space(5f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                draw();
            }
        }
    }

    private MaterialProperty Find(string name)
    {
        return FindProperty(name, properties);
    }

    private static GUIContent Label(string text)
    {
        return new GUIContent(text);
    }
}
