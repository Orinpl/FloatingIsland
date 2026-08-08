using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(GridHeatMapManager))]
    public class GridHeatMapManagerEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;

        private Label headerField;
        private EnumField heatMapValueReadMethodField;
        private VisualElement specificGridModifierSOField;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "GridHeatMapManagerEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            MonoScript script = MonoScript.FromMonoBehaviour((GridHeatMapManager)target);
            headerField = root.Q<Label>("Header");
            headerField.RegisterCallback<ClickEvent>(evt => EditorGUIUtility.PingObject(script));

            specificGridModifierSOField = root.Q<VisualElement>("Specific_Grid_Modifier_SO");
            heatMapValueReadMethodField = root.Q<EnumField>("Heat_Map_Value_Read_Method");
            heatMapValueReadMethodField.RegisterValueChangedCallback(evt => ToggleSetEnable(specificGridModifierSOField, heatMapValueReadMethodField));
        
            return root;
        }

        private void ToggleSetEnable(VisualElement field, EnumField baseEnumField)
        {
            if (baseEnumField.value is HeatMapValueReadMethod.ReadAllValues)
            {
                field.style.display = DisplayStyle.None;
            }
            else field.style.display = DisplayStyle.Flex;
        }
    }
}