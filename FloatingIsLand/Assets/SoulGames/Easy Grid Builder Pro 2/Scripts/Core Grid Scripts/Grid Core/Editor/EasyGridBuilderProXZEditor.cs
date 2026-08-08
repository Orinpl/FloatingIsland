using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using System.Diagnostics;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(EasyGridBuilderProXZ))]
    public class EasyGridBuilderProXZEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset;
        private VisualElement root;
        
        private EasyGridBuilderProXZ easyGridBuilderProXZ;

        private Label headerField;

        private Button uniqueIDRefreshButton;
        private IntegerField gridWidthField;
        private IntegerField gridLengthField;
        private FloatField cellSizeField;
        private EnumField gridOriginTypeField;
        private BaseBoolField updateGridWidthAndLengthRuntimeField;
        private EnumField activeCameraModeField;
        private EnumField activeGridModeField;
        private Vector3Field activeGridOriginField;
        private ObjectField activeBuildableObjectSOField;
        private IntegerField activeVerticalGridIndexField;
        private IntegerField verticalGridCountField;
        private FloatField verticalGridHeightField;
        private Button randomSeedButton;
        private IntegerField borderTilesAmountField;
        private FloatField delayBetweenSpawns;

        private const int MIN_GRID_WIDTH = 1;
        private const int MIN_GRID_LENGTH = 1;
        private const float MIN_CELL_SIZE = 0;
        private const int MIN_VERTICAL_GRID_COUNT = 1;
        private const float MIN_VERTICAL_HEIGHT = 0.1f;
        private const int MIN_BORDER_TILES = 0;
        private const float MIN_SPAWN_DELAY = 0;

        private void OnEnable()
        {
            easyGridBuilderProXZ = (EasyGridBuilderProXZ)target;
        }
        
        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "EasyGridBuilderProXZEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);
            
            MonoScript script = MonoScript.FromMonoBehaviour((EasyGridBuilderProXZ)target);
            headerField = root.Q<Label>("Header");
            headerField.RegisterCallback<ClickEvent>(evt => EditorGUIUtility.PingObject(script));

            uniqueIDRefreshButton = root.Q<Button>("Unique_ID_Refresh");
            uniqueIDRefreshButton.RegisterCallback<ClickEvent>(CallFunctionUniqueIDRefresh);

            gridWidthField = root.Q<IntegerField>("Grid_Width");
            gridWidthField.RegisterValueChangedCallback(evt => ValidateMinValue(gridWidthField, MIN_GRID_WIDTH));
            gridLengthField = root.Q<IntegerField>("Grid_Length");
            gridLengthField.RegisterValueChangedCallback(evt => ValidateMinValue(gridLengthField, MIN_GRID_LENGTH));
            cellSizeField = root.Q<FloatField>("Cell_Size");
            cellSizeField.RegisterValueChangedCallback(evt => ValidateMinValue(cellSizeField, MIN_CELL_SIZE));
            
            gridOriginTypeField = root.Q<EnumField>("Grid_Origin_Type");
            updateGridWidthAndLengthRuntimeField = root.Q<BaseBoolField>("Update_Grid_Width_And_Length_Runtime");
            updateGridWidthAndLengthRuntimeField.RegisterValueChangedCallback(evt => ToggleSetEnable(gridOriginTypeField, updateGridWidthAndLengthRuntimeField));

            activeCameraModeField = root.Q<EnumField>("Active_Camera_Mode");
            activeCameraModeField.SetEnabled(false);
            activeGridModeField = root.Q<EnumField>("Active_Grid_Mode");
            activeGridModeField.SetEnabled(false);
            activeGridOriginField = root.Q<Vector3Field>("Active_Grid_Origin");
            activeGridOriginField.SetEnabled(false);
            activeBuildableObjectSOField = root.Q<ObjectField>("Active_Buildable_Object_SO");
            activeBuildableObjectSOField.SetEnabled(false);
            activeVerticalGridIndexField = root.Q<IntegerField>("Active_Vertical_Grid_Index");
            activeVerticalGridIndexField.SetEnabled(false);

            verticalGridCountField = root.Q<IntegerField>("Vertical_Grid_Count");
            verticalGridCountField.RegisterValueChangedCallback(evt => ValidateMinValue(verticalGridCountField, MIN_VERTICAL_GRID_COUNT));

            verticalGridHeightField = root.Q<FloatField>("Vertical_Grid_Height");
            verticalGridHeightField.RegisterValueChangedCallback(evt => ValidateMinValue(verticalGridHeightField, MIN_VERTICAL_HEIGHT));

            randomSeedButton = root.Q<Button>("Random_Seed");
            randomSeedButton.RegisterCallback<ClickEvent>(OnRandomSeedButtonClick);

            borderTilesAmountField = root.Q<IntegerField>("Border_Tiles_Amount");
            borderTilesAmountField.RegisterValueChangedCallback(evt => ValidateMinValue(borderTilesAmountField, MIN_BORDER_TILES));
            delayBetweenSpawns = root.Q<FloatField>("Delay_Between_Spawns");
            delayBetweenSpawns.RegisterValueChangedCallback(evt => ValidateMinValue(delayBetweenSpawns, MIN_SPAWN_DELAY));

            if (EditorGUI.EndChangeCheck()) SetComponentDirty();
            return root;
        }

        private void CallFunctionUniqueIDRefresh(ClickEvent evt)
        {
            easyGridBuilderProXZ.GenerateGridUniqueID(true);
            SetComponentDirty();
        }

        private void ValidateMinValue(IntegerField field, int minValue)
        {
            if (field.value < minValue)
            {
                field.value = minValue;
            }
        }

        private void ValidateMinValue(FloatField field, float minValue)
        {
            if (field.value < minValue)
            {
                field.value = minValue;
            }
        }

        private void ToggleSetEnable(EnumField field, BaseBoolField baseBoolField)
        {
            if (baseBoolField.value == true)
            {
                field.SetEnabled(false);
            }
            else field.SetEnabled(true);
        }

        public void OnRandomSeedButtonClick(ClickEvent clickEvent)
        {
            easyGridBuilderProXZ.GenerateRandomSeed();
        }

        private void SetComponentDirty()
        {
            Undo.RecordObject(easyGridBuilderProXZ, "Modified MyComponent");
            EditorUtility.SetDirty(easyGridBuilderProXZ);

            PrefabStage prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null) EditorSceneManager.MarkSceneDirty(prefabStage.scene);
        }
    }
}