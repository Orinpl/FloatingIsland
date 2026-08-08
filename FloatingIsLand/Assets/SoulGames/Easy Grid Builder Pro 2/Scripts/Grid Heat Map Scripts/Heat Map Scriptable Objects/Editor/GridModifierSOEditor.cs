using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(GridModifierSO))]
    public class GridModifierSOEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset;
        private VisualElement root;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "GridModifierSOEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            return root;
        }
    }
}