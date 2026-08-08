using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.Utilities
{
    [CustomEditor(typeof(TerrainDataManager))]
    public class TerrainDataManagerEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;

        private Label headerField;
        private Button findAllTerrainsInSceneButton;

        private TerrainDataManager terrainDataManager;

        private void OnEnable()
        {
            terrainDataManager = (TerrainDataManager)target;
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "TerrainDataManagerEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            MonoScript script = MonoScript.FromMonoBehaviour((TerrainDataManager)target);
            headerField = root.Q<Label>("Header");
            headerField.RegisterCallback<ClickEvent>(evt => EditorGUIUtility.PingObject(script));

            findAllTerrainsInSceneButton = root.Q<Button>("Find_All_Terrains_In_Scene");
            findAllTerrainsInSceneButton.RegisterCallback<ClickEvent>(CallFunctionAutoCalculateSize);

            return root;
        }

        private void CallFunctionAutoCalculateSize(ClickEvent evt)
        {
            terrainDataManager.FindAllTerrainsInScene();
        }
    }
}