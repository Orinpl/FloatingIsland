using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FI_WindLineEffect))]
public sealed class FI_WindLineEffectEditor : Editor
{
    private SerializedProperty controlLineWidths;
    private SerializedProperty overallWidth;
    private SerializedProperty layerWidthFalloff;
    private SerializedProperty speedMultiplier;
    private SerializedProperty timeOffsetStep;

    private void OnEnable()
    {
        controlLineWidths = serializedObject.FindProperty("controlLineWidths");
        overallWidth = serializedObject.FindProperty("overallWidth");
        layerWidthFalloff = serializedObject.FindProperty("layerWidthFalloff");
        speedMultiplier = serializedObject.FindProperty("speedMultiplier");
        timeOffsetStep = serializedObject.FindProperty("timeOffsetStep");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();

        DrawSection("整体宽度", () =>
        {
            EditorGUILayout.PropertyField(controlLineWidths, new GUIContent("统一控制宽度"));
            using (new EditorGUI.DisabledScope(!controlLineWidths.boolValue))
            {
                EditorGUILayout.PropertyField(overallWidth, new GUIContent("整体宽度"));
                EditorGUILayout.PropertyField(layerWidthFalloff, new GUIContent("层级宽度衰减"));
            }
        });

        DrawSection("动画", () =>
        {
            EditorGUILayout.PropertyField(speedMultiplier, new GUIContent("速度倍率"));
            EditorGUILayout.PropertyField(timeOffsetStep, new GUIContent("分层相位间隔"));
        });

        EditorGUILayout.HelpBox(
            "这里控制 LineRenderer 的真实几何宽度；材质中的“可见宽度”和“笔锋”负责轮廓裁切与尖端造型。",
            MessageType.Info);

        if (EditorGUI.EndChangeCheck())
        {
            serializedObject.ApplyModifiedProperties();
            foreach (Object item in targets)
            {
                if (item is FI_WindLineEffect effect)
                {
                    effect.ApplyProperties();
                    EditorUtility.SetDirty(effect);
                }
            }
        }
        else
        {
            serializedObject.ApplyModifiedProperties();
        }
    }

    private static void DrawSection(string title, System.Action draw)
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
}
