using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(BuildableEdgeObject))]
    public class BuildableEdgeObjectEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;

        private BuildableEdgeObject buildableEdgeObject;

        private Label headerField;
        private Toggle activateGizmosToggle;
        private VisualElement gimzomsWarningElement;

        private Button autoCalculateRelativeScaleField;
        private Toggle autoCalculateRelativeScaleAndPivotToggle;
        private SerializedProperty autoCalculateRelativeScaleAndPivotToggleProperty;

        private Vector3Field objectScaleField;
        private Vector3Field objectCenterField;
        private Vector3Field objectCustomPivotField;
        
        private FloatField gridCellSizeField;
        private Button calculateAndDisplayDebugInfoField;
        private IntegerField objectLengthRelativeToCellSizeField;

        private const float MIN_CELL_SIZE = 0;

        private void OnEnable()
        {
            buildableEdgeObject = (BuildableEdgeObject)target;
            autoCalculateRelativeScaleAndPivotToggleProperty = serializedObject.FindProperty("lockAutoGenerationAndValues");
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "BuildableEdgeObjectEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            MonoScript script = MonoScript.FromMonoBehaviour((BuildableEdgeObject)target);
            headerField = root.Q<Label>("Header");
            headerField.RegisterCallback<ClickEvent>(evt => EditorGUIUtility.PingObject(script));

            activateGizmosToggle = root.Q<Toggle>("Activate_Gizmos");
            activateGizmosToggle.RegisterCallback<ChangeEvent<bool>>(OnActivateGizmosBoolChange);
            gimzomsWarningElement = root.Q<VisualElement>("Gizmos_Warning");
            gimzomsWarningElement.style.display = DisplayStyle.Flex;

            autoCalculateRelativeScaleField = root.Q<Button>("Auto_Calculate_Relative_Scale");
            autoCalculateRelativeScaleField.RegisterCallback<ClickEvent>(CallFunctionAutoCalculateSize);
            autoCalculateRelativeScaleAndPivotToggle = root.Q<Toggle>("Lock_Auto_Generation_And_Values");
            autoCalculateRelativeScaleAndPivotToggle.RegisterCallback<ChangeEvent<bool>>(OnAutoCalculateRelativeScaleAndPivotBoolChange);

            objectScaleField = root.Q<Vector3Field>("Object_Scale");
            objectScaleField.RegisterValueChangedCallback(evt => ClampVector3ToThreeDecimalPoints(objectScaleField));
            objectCenterField = root.Q<Vector3Field>("Object_Center");
            objectCenterField.RegisterValueChangedCallback(evt => ClampVector3ToThreeDecimalPoints(objectCenterField));
            objectCustomPivotField = root.Q<Vector3Field>("Object_Custom_Pivot");
            objectCustomPivotField.RegisterValueChangedCallback(evt => ClampVector3ToThreeDecimalPoints(objectCustomPivotField));
            objectCustomPivotField.SetEnabled(false);

            gridCellSizeField = root.Q<FloatField>("Debug_Cell_Size");
            gridCellSizeField.RegisterValueChangedCallback(evt => ValidateMinValue(gridCellSizeField, MIN_CELL_SIZE));

            calculateAndDisplayDebugInfoField = root.Q<Button>("Calculate_And_Display_Debug_Info");
            calculateAndDisplayDebugInfoField.RegisterCallback<ClickEvent>(callCalculateAndDisplayDebugInfo);

            objectLengthRelativeToCellSizeField = root.Q<IntegerField>("Object_Length_Relative_To_Cell_Size");
            objectLengthRelativeToCellSizeField.SetEnabled(false);

            if (EditorGUI.EndChangeCheck()) SetComponentDirty();
            return root;
        }

        private void ValidateMinValue(FloatField field, float minValue)
        {
            if (field.value < minValue) field.value = minValue;
        }

        private void ClampVector3ToThreeDecimalPoints(Vector3Field vector3Field)
        {
            vector3Field.value = new Vector3(Mathf.Round(vector3Field.value.x * 1000f) / 1000f, Mathf.Round(vector3Field.value.y * 1000f) / 1000f, Mathf.Round(vector3Field.value.z * 1000f) / 1000f);
        }

        
        private void CallFunctionAutoCalculateSize(ClickEvent evt)
        {
            buildableEdgeObject.AutoCalculateSize();
            SetComponentDirty();
        }

        private void callCalculateAndDisplayDebugInfo(ClickEvent evt)
        {
            buildableEdgeObject.GetObjectSizeRelativeToCellSizeEditor();
            SetComponentDirty();
        }


        private void SetComponentDirty()
        {
            Undo.RecordObject(buildableEdgeObject, "Modified MyComponent");
            EditorUtility.SetDirty(buildableEdgeObject);

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null) EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        }

        private void OnActivateGizmosBoolChange(ChangeEvent<bool> evt)
        {
            if (evt.newValue == true) gimzomsWarningElement.style.display = DisplayStyle.Flex;
            else gimzomsWarningElement.style.display = DisplayStyle.None;
        }

        private void OnAutoCalculateRelativeScaleAndPivotBoolChange(ChangeEvent<bool> evt)
        {
            if (autoCalculateRelativeScaleAndPivotToggleProperty.boolValue == true)
            {
                autoCalculateRelativeScaleField.SetEnabled(false);
                objectScaleField.SetEnabled(false);
                objectCenterField.SetEnabled(false);
            }
            else
            {
                autoCalculateRelativeScaleField.SetEnabled(true);
                objectScaleField.SetEnabled(true);
                objectCenterField.SetEnabled(true);
            }
        }
    }
}
