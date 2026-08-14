using UnityEditor;
using UnityEngine;

public sealed class FI_SkyboxShaderGUI : FI_LitShaderGUI
{
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        editor = materialEditor;
        properties = props;

        EditorGUI.BeginChangeCheck();

        DrawSection("天空渐变", () =>
        {
            editor.ColorProperty(Find("_SkyTopColor"), "顶部颜色");
            editor.ColorProperty(Find("_SkyHorizonColor"), "近地平线颜色");
            editor.RangeProperty(Find("_SkyGradientPower"), "渐变曲线");
            editor.RangeProperty(Find("_Exposure"), "曝光");
            editor.RangeProperty(Find("_Saturation"), "饱和度");
            editor.RangeProperty(Find("_Contrast"), "对比度");
        });

        DrawSection("地平线与海洋", () =>
        {
            editor.RangeProperty(Find("_HorizonLevel"), "地平线高度");
            editor.RangeProperty(Find("_HorizonWidth"), "地平线雾宽度");
            editor.ColorProperty(Find("_HorizonGlowColor"), "地平线光颜色");
            editor.RangeProperty(Find("_HorizonGlowStrength"), "地平线光强度");
            editor.ColorProperty(Find("_OceanHorizonColor"), "远处海水颜色");
            editor.ColorProperty(Find("_OceanDeepColor"), "下方海水颜色");
            editor.RangeProperty(Find("_OceanGradientPower"), "海水渐变曲线");
            editor.RangeProperty(Find("_OceanVariation"), "海水细微变化");
        });

        bool cloudsEnabled = DrawFeatureToggle("_CloudGroup", "云");
        if (cloudsEnabled)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                editor.ColorProperty(Find("_CloudColor"), "云亮部颜色");
                editor.ColorProperty(Find("_CloudShadowColor"), "云阴影颜色");
                editor.RangeProperty(Find("_CloudCoverage"), "云量");
                editor.RangeProperty(Find("_CloudScale"), "云尺寸");
                editor.RangeProperty(Find("_CloudSoftness"), "云边缘柔和");
                editor.RangeProperty(Find("_CloudAltitude"), "云层高度");
                editor.RangeProperty(Find("_CloudSpread"), "云层范围");
                editor.RangeProperty(Find("_CloudSpeed"), "云移动速度");
                editor.RangeProperty(Find("_CloudOpacity"), "云透明度");
                editor.RangeProperty(Find("_CloudFadeStrength"), "渐变显隐强度");
                editor.RangeProperty(Find("_CloudFadeSpeed"), "渐变显隐速度");
            }
        }

        bool islandsEnabled = DrawFeatureToggle("_IslandGroup", "下方海岛");
        if (islandsEnabled)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                editor.RangeProperty(Find("_IslandCount"), "岛屿数量");
                editor.RangeProperty(Find("_IslandScale"), "岛屿尺寸");
                editor.RangeProperty(Find("_IslandDepth"), "岛屿向下分布");
                editor.RangeProperty(Find("_IslandOpacity"), "岛屿可见度");
                editor.ColorProperty(Find("_GrassColor"), "草地颜色");
                editor.ColorProperty(Find("_GrassLightColor"), "草地亮部");
                editor.ColorProperty(Find("_SandColor"), "沙滩颜色");
                editor.ColorProperty(Find("_RockColor"), "岩石颜色");
                editor.ColorProperty(Find("_ShallowWaterColor"), "浅滩颜色");
            }
        }

        bool windEnabled = DrawFeatureToggle("_WindGroup", "风带");
        if (windEnabled)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                editor.ColorProperty(Find("_WindColor"), "风带颜色");
                editor.RangeProperty(Find("_WindIntensity"), "风带强度");
                DrawIntegerSlider("_WindCount", "风带数量", 1, 8);
                editor.RangeProperty(Find("_WindPositionX"), "水平位置");
                editor.RangeProperty(Find("_WindPositionY"), "垂直位置");
                editor.RangeProperty(Find("_WindVerticalSpread"), "垂直分散");
                editor.RangeProperty(Find("_WindWidth"), "笔触宽度");
                editor.RangeProperty(Find("_WindLength"), "笔触长度");
                editor.RangeProperty(Find("_WindTaper"), "笔锋收束");
                editor.RangeProperty(Find("_WindBreakup"), "边缘破碎");
                editor.RangeProperty(Find("_WindCurvature"), "蛇形摆幅");
                editor.RangeProperty(Find("_WindWaveCount"), "蛇形波数");
                editor.RangeProperty(Find("_WindWaveSpeed"), "摆动速度");
                editor.RangeProperty(Find("_WindFadeDuration"), "渐入渐出时长");
                editor.RangeProperty(Find("_WindSpeed"), "前进速度");
            }
        }

        DrawSection("方向与场景", () =>
        {
            editor.RangeProperty(Find("_Rotation"), "水平旋转");
            if (GUILayout.Button("设为当前场景天空盒"))
            {
                Material material = materialEditor.target as Material;
                if (material != null)
                {
                    RenderSettings.skybox = material;
                    DynamicGI.UpdateEnvironment();
                    SceneView.RepaintAll();
                }
            }
        });

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

    protected override void SetupMaterialKeywords(Material material)
    {
        SetKeyword(material, "_CLOUDS", material.GetFloat("_CloudGroup") > 0.5f);
        SetKeyword(material, "_ISLANDS", material.GetFloat("_IslandGroup") > 0.5f);
        SetKeyword(material, "_WINDBANDS", material.GetFloat("_WindGroup") > 0.5f);
    }

    private void DrawIntegerSlider(string propertyName, string label, int minimum, int maximum)
    {
        MaterialProperty property = Find(propertyName);
        EditorGUI.BeginChangeCheck();
        int value = EditorGUILayout.IntSlider(label, Mathf.RoundToInt(property.floatValue), minimum, maximum);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo(label);
            property.floatValue = value;
        }
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
