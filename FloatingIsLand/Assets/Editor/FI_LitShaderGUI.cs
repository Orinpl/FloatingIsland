using UnityEditor;
using UnityEngine;

public class FI_LitShaderGUI : ShaderGUI
{
    protected MaterialEditor editor;
    protected MaterialProperty[] properties;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        editor = materialEditor;
        properties = props;

        EditorGUI.BeginChangeCheck();
        DrawBase();
        DrawExtraAfterBase();
        DrawNormalMap();
        DrawShadow();
        DrawSpecular();
        DrawRimLight();
        DrawMatCap();
        DrawIBL();
        DrawEmission();
        DrawOutline();
        DrawRendering();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Object target in editor.targets)
            {
                if (target is Material material)
                {
                    SetupMaterialKeywords(material);
                    EditorUtility.SetDirty(material);
                }
            }
        }

        foreach (Object target in editor.targets)
        {
            if (target is Material material)
            {
                SetupMaterialKeywords(material);
            }
        }
    }

    protected virtual void DrawExtraAfterBase()
    {
    }

    protected virtual void SetupMaterialKeywords(Material material)
    {
        SetKeyword(material, "_NORMALMAP", material.GetFloat("_NormalMapGroup") > 0.5f);
        SetKeyword(material, "_SPECULAR", material.GetFloat("_SpecularGroup") > 0.5f);
        SetKeyword(material, "_RIMLIGHT", material.GetFloat("_RimLightGroup") > 0.5f);
        SetKeyword(material, "_MATCAP", material.GetFloat("_MatCapGroup") > 0.5f);
        SetKeyword(material, "_IBL", material.GetFloat("_IBLGroup") > 0.5f);
        SetKeyword(material, "_IBL_USE_BUILTIN", material.GetFloat("_IBLUseBuiltin") > 0.5f);
        SetKeyword(material, "_EMISSION", material.GetFloat("_EmissionGroup") > 0.5f);
        SetKeyword(material, "_USE_SHADOW_RAMP", material.GetFloat("_UseShadowRamp") > 0.5f);
        SetKeyword(material, "_OUTLINE", material.GetFloat("_OutlineGroup") > 0.5f);

        int smoothNormalMode = Mathf.RoundToInt(material.GetFloat("_SmoothNormalMode"));
        SetKeyword(material, "_SMOOTH_NORMAL_OFF", smoothNormalMode == 0);
        SetKeyword(material, "_SMOOTH_NORMAL", smoothNormalMode == 1);
        SetKeyword(material, "_SMOOTH_NORMAL_VERTEXCOLOR", smoothNormalMode == 2);
    }

    protected void DrawBase()
    {
        DrawSection("基础设置", () =>
        {
            editor.TexturePropertySingleLine(Label("主贴图"), Find("_MainTex"), Find("_Color"));
            editor.TextureScaleOffsetProperty(Find("_MainTex"));
        });
    }

    private void DrawNormalMap()
    {
        bool enabled = DrawFeatureToggle("_NormalMapGroup", "法线贴图");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            editor.TexturePropertySingleLine(Label("法线贴图"), Find("_BumpMap"), Find("_BumpScale"));
        }
    }

    private void DrawShadow()
    {
        DrawSection("阴影设置", () =>
        {
            editor.ColorProperty(Find("_ShadowColor"), "1st 阴影颜色");
            editor.ColorProperty(Find("_Shadow2ndColor"), "2nd 阴影颜色");
            editor.RangeProperty(Find("_ShadowBorderRange"), "阴影边缘柔和度");
            editor.RangeProperty(Find("_ShadowBorderPosition"), "阴影位置偏移");
            editor.RangeProperty(Find("_Shadow2ndBorderRange"), "2nd阴影柔和度");
            editor.RangeProperty(Find("_Shadow2ndBorderPosition"), "2nd阴影位置");

            DrawToggle("_UseShadowRamp", "使用渐变贴图");
            if (Find("_UseShadowRamp").floatValue > 0.5f)
            {
                editor.TexturePropertySingleLine(Label("阴影渐变贴图"), Find("_ShadowRampTex"));
            }

            editor.RangeProperty(Find("_ShadowReceive"), "接收阴影强度");
            editor.RangeProperty(Find("_ShadowEnvStrength"), "环境光影响阴影");
        });
    }

    private void DrawSpecular()
    {
        bool enabled = DrawFeatureToggle("_SpecularGroup", "高光");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            editor.ColorProperty(Find("_SpecularColor"), "高光颜色");
            editor.RangeProperty(Find("_SpecularPower"), "高光锐度");
            editor.RangeProperty(Find("_SpecularIntensity"), "高光强度");
            editor.RangeProperty(Find("_SpecularSmoothness"), "高光边缘柔和");
            editor.TexturePropertySingleLine(Label("高光遮罩"), Find("_SpecularMask"));
        }
    }

    private void DrawRimLight()
    {
        bool enabled = DrawFeatureToggle("_RimLightGroup", "边缘光");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            editor.ColorProperty(Find("_RimColor"), "边缘光颜色");
            editor.RangeProperty(Find("_RimPower"), "边缘光范围");
            editor.RangeProperty(Find("_RimIntensity"), "边缘光强度");
            editor.RangeProperty(Find("_RimSmoothness"), "边缘光软硬度");
            editor.RangeProperty(Find("_RimLightDirOffset"), "光方向偏移");
            editor.VectorProperty(Find("_RimCustomOffset"), "自定义偏移");
            editor.RangeProperty(Find("_RimShadowMask"), "阴影区域遮蔽");
        }
    }

    private void DrawMatCap()
    {
        bool enabled = DrawFeatureToggle("_MatCapGroup", "MatCap");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            editor.TexturePropertySingleLine(Label("MatCap贴图"), Find("_MatCapTex"));
            editor.ColorProperty(Find("_MatCapColor"), "MatCap颜色");
            editor.RangeProperty(Find("_MatCapIntensity"), "MatCap强度");
            DrawPopup("_MatCapBlendMode", "MatCap混合模式", new[] { "Add", "Multiply", "Screen" });
        }
    }

    private void DrawIBL()
    {
        bool enabled = DrawFeatureToggle("_IBLGroup", "IBL 环境反射");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            DrawToggle("_IBLUseBuiltin", "使用内置IBL");
            if (Find("_IBLUseBuiltin").floatValue < 0.5f)
            {
                editor.TexturePropertySingleLine(Label("自定义环境Cubemap"), Find("_IBLCubemap"));
            }

            editor.ColorProperty(Find("_IBLColor"), "IBL 颜色");
            editor.RangeProperty(Find("_IBLIntensity"), "IBL 强度");
            editor.RangeProperty(Find("_IBLRoughness"), "IBL 粗糙度");
            editor.RangeProperty(Find("_IBLMetallic"), "金属度");
            DrawPopup("_IBLBlendMode", "IBL 混合模式", new[] { "Add", "Multiply", "Screen", "Lerp" });
        }
    }

    private void DrawEmission()
    {
        bool enabled = DrawFeatureToggle("_EmissionGroup", "自发光");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            editor.TexturePropertySingleLine(Label("自发光贴图"), Find("_EmissionMap"), Find("_EmissionColor"));
        }
    }

    private void DrawOutline()
    {
        bool enabled = DrawFeatureToggle("_OutlineGroup", "描边");
        if (!enabled)
        {
            return;
        }

        using (new EditorGUI.IndentLevelScope())
        {
            editor.ColorProperty(Find("_OutlineColor"), "描边颜色");
            editor.RangeProperty(Find("_OutlineWidth"), "描边宽度");
            editor.TexturePropertySingleLine(Label("描边宽度遮罩"), Find("_OutlineWidthMask"));
            editor.RangeProperty(Find("_OutlineColorMix"), "描边混合主色比例");
            editor.RangeProperty(Find("_OutlineZOffset"), "描边深度偏移");
            DrawPopup("_SmoothNormalMode", "软法线模式", new[] { "Off", "Tangent", "VertexColor" });
        }
    }

    private void DrawRendering()
    {
        DrawSection("渲染设置", () =>
        {
            DrawPopup("_Cull", "剔除模式", new[] { "Off", "Front", "Back" });
            DrawPopup("_ZWrite", "深度写入", new[] { "Off", "On" });
            editor.ShaderProperty(Find("_SrcBlend"), "源混合");
            editor.ShaderProperty(Find("_DstBlend"), "目标混合");
            editor.RenderQueueField();
            editor.EnableInstancingField();
            editor.DoubleSidedGIField();
        });
    }

    protected bool DrawFeatureToggle(string propertyName, string title)
    {
        MaterialProperty property = Find(propertyName);
        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft(title, property.floatValue > 0.5f, EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                editor.RegisterPropertyChangeUndo(title);
                property.floatValue = enabled ? 1f : 0f;
            }
            return enabled;
        }
    }

    protected void DrawSection(string title, System.Action drawContent)
    {
        EditorGUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                drawContent.Invoke();
            }
        }
    }

    protected void DrawToggle(string propertyName, string label)
    {
        MaterialProperty property = Find(propertyName);
        EditorGUI.BeginChangeCheck();
        bool value = EditorGUILayout.Toggle(label, property.floatValue > 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(label);
            property.floatValue = value ? 1f : 0f;
        }
    }

    protected void DrawPopup(string propertyName, string label, string[] labels)
    {
        MaterialProperty property = Find(propertyName);
        EditorGUI.BeginChangeCheck();
        int value = EditorGUILayout.Popup(label, Mathf.Clamp(Mathf.RoundToInt(property.floatValue), 0, labels.Length - 1), labels);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(label);
            property.floatValue = value;
        }
    }

    protected MaterialProperty Find(string propertyName)
    {
        return FindProperty(propertyName, properties);
    }

    protected static GUIContent Label(string text)
    {
        return new GUIContent(text);
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }
}
