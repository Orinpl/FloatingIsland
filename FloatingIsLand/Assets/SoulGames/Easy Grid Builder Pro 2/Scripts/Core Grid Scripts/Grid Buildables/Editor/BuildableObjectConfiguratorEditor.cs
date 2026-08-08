using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(BuildableObjectConfigurator))]
    public class BuildableObjectConfiguratorEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;
        
        private BuildableObjectConfigurator buildableObjectConfigurator;

        private Label headerField;
        private Button configureBuildableObject;

        private void OnEnable()
        {
            buildableObjectConfigurator = (BuildableObjectConfigurator)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "BuildableObjectConfiguratorEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            MonoScript script = MonoScript.FromMonoBehaviour((BuildableObjectConfigurator)target);
            headerField = root.Q<Label>("Header");
            headerField.RegisterCallback<ClickEvent>(evt => EditorGUIUtility.PingObject(script));

            configureBuildableObject = root.Q<Button>("Configure_Buildable_Object");
            configureBuildableObject.RegisterCallback<ClickEvent>(CallFunctionConfigureBuildableObject);

            return root;
        }

        private void CallFunctionConfigureBuildableObject(ClickEvent evt)
        {
            buildableObjectConfigurator.ConfigureBuildableObject();
        }
    }
}
