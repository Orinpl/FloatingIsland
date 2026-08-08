using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(BuildableCornerObjectSO))]
    public class BuildableCornerObjectSOEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;

        private SerializedProperty setSpawnedObjectLayerProperty;
        private Toggle setSpawnedObjectLayerField;
        private VisualElement spawnedObjectLayerVisualElement;

        private SerializedProperty snapToTheClosestVerticalGridHeightProperty;
        private Toggle snapToTheClosestVerticalGridHeightField;
        private VisualElement minimumHeightThresholdPercentageVisualElement;

        private SerializedProperty placementTypeProperty;
        private EnumField placementTypeField;
        private VisualElement intervalBetweenEachPlacementVisualElement;
        private VisualElement spawnOnlyAtEndPointsVisualElement;

        private SerializedProperty rotationTypeProperty;
        private EnumField rotationTypeField;
        private VisualElement freeRotationSpeedVisualElement;

        private SerializedProperty freezeRotationProperty;
        private Toggle freezeRotationField;
        private VisualElement fourDirectionalRotationVisualElement;
        private VisualElement eightDirectionalRotationVisualElement;
        private VisualElement freeRotationVisualElement;

        private void OnEnable()
        {
            setSpawnedObjectLayerProperty = serializedObject.FindProperty("setSpawnedObjectLayer");
            snapToTheClosestVerticalGridHeightProperty = serializedObject.FindProperty("snapToTheClosestVerticalGridHeight");
            placementTypeProperty = serializedObject.FindProperty("placementType");
            rotationTypeProperty = serializedObject.FindProperty("rotationType");
            freezeRotationProperty = serializedObject.FindProperty("freezeRotation");
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "BuildableCornerObjectSOEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            spawnedObjectLayerVisualElement = root.Q<VisualElement>("Spawned_Object_Layer");
            setSpawnedObjectLayerField = root.Q<Toggle>("Set_Spawned_Object_Layer");
            setSpawnedObjectLayerField.RegisterCallback<ChangeEvent<bool>>(OnActivateSetSpawnObjectLayerFieldBoolChange);

            minimumHeightThresholdPercentageVisualElement = root.Q<VisualElement>("Minimum_Height_Threshold_Percentage");
            snapToTheClosestVerticalGridHeightField = root.Q<Toggle>("Snap_To_The_Closest_Vertical_Grid_Height");
            snapToTheClosestVerticalGridHeightField.RegisterCallback<ChangeEvent<bool>>(OnActivateSnapToTheClosestVerticalGridHeightFieldBoolChange);

            intervalBetweenEachPlacementVisualElement = root.Q<VisualElement>("Interval_Between_Each_Placement");
            spawnOnlyAtEndPointsVisualElement = root.Q<VisualElement>("Spawn_Only_At_End_Points");
            placementTypeField = root.Q<EnumField>("Placement_Type");
            placementTypeField.RegisterValueChangedCallback(OnPlacementTypeValueChange);

            freeRotationSpeedVisualElement = root.Q<VisualElement>("Free_Rotation_Speed");
            rotationTypeField = root.Q<EnumField>("Rotation_Type");
            rotationTypeField.RegisterValueChangedCallback(OnRotationTypeValueChange);

            fourDirectionalRotationVisualElement = root.Q<VisualElement>("Four_Directional_Rotation");
            eightDirectionalRotationVisualElement = root.Q<VisualElement>("Eight_Directional_Rotation");
            freeRotationVisualElement = root.Q<VisualElement>("Free_Rotation");
            freezeRotationField = root.Q<Toggle>("Freeze_Rotation");
            freezeRotationField.RegisterCallback<ChangeEvent<bool>>(OnActivateFreezeRotationFieldBoolChange);

            return root;
        }

        private void OnActivateSetSpawnObjectLayerFieldBoolChange(ChangeEvent<bool> evt)
        {
            if (setSpawnedObjectLayerProperty.boolValue == false) spawnedObjectLayerVisualElement.style.display = DisplayStyle.None;
            else spawnedObjectLayerVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnActivateSnapToTheClosestVerticalGridHeightFieldBoolChange(ChangeEvent<bool> evt)
        {
            if (snapToTheClosestVerticalGridHeightProperty.boolValue == false) minimumHeightThresholdPercentageVisualElement.style.display = DisplayStyle.None;
            else minimumHeightThresholdPercentageVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnPlacementTypeValueChange(ChangeEvent<Enum> evt)
        {
            if (placementTypeProperty.enumValueIndex == (int)CornerObjectPlacementType.SinglePlacement || placementTypeProperty.enumValueFlag == (int)CornerObjectPlacementType.PaintPlacement) 
            {
                intervalBetweenEachPlacementVisualElement.style.display = DisplayStyle.None;
                spawnOnlyAtEndPointsVisualElement.style.display = DisplayStyle.None;
            }    
            else 
            {   
                intervalBetweenEachPlacementVisualElement.style.display = DisplayStyle.Flex;
                spawnOnlyAtEndPointsVisualElement.style.display = DisplayStyle.Flex;
            }
        }

        private void OnRotationTypeValueChange(ChangeEvent<Enum> evt)
        {
            if (rotationTypeProperty.enumValueIndex != (int)CornerObjectRotationType.FreeRotation) freeRotationSpeedVisualElement.style.display = DisplayStyle.None;
            else freeRotationSpeedVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnActivateFreezeRotationFieldBoolChange(ChangeEvent<bool> evt)
        {
            if (freezeRotationProperty.boolValue == false) 
            {
                fourDirectionalRotationVisualElement.style.display = DisplayStyle.None;
                eightDirectionalRotationVisualElement.style.display = DisplayStyle.None;
                freeRotationVisualElement.style.display = DisplayStyle.None;
            }
            else 
            {
                fourDirectionalRotationVisualElement.style.display = DisplayStyle.Flex;
                eightDirectionalRotationVisualElement.style.display = DisplayStyle.Flex;
                freeRotationVisualElement.style.display = DisplayStyle.Flex;
            }
        }
    }
}