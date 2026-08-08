using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(BuildableEdgeObjectSO))]
    public class BuildableEdgeObjectSOEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;

        private SerializedProperty setSpawnedObjectLayerProperty;
        private Toggle setSpawnedObjectLayerField;
        private VisualElement spawnedObjectLayerVisualElement;

        private SerializedProperty snapToTheClosestVerticalGridHeightProperty;
        private Toggle snapToTheClosestVerticalGridHeightField;
        private VisualElement minimumHeightThresholdPercentageVisualElement;

        private SerializedProperty freezeRotationProperty;
        private Toggle freezeRotationField;
        private VisualElement fourDirectionalRotationVisualElement;

        private SerializedProperty placementTypeProperty;
        private EnumField placementTypeField;
        private VisualElement intervalBetweenEachPlacementVisualElement;

        private SerializedProperty mergeWithBuildableCornerObjectProperty;
        private Toggle mergeWithBuildableCornerObjectField;
        private VisualElement buildableCornerObjectSOVisualElement;
        private VisualElement onlyUsedWithGhostObjectVisualElement;

        private void OnEnable()
        {
            setSpawnedObjectLayerProperty = serializedObject.FindProperty("setSpawnedObjectLayer");
            snapToTheClosestVerticalGridHeightProperty = serializedObject.FindProperty("snapToTheClosestVerticalGridHeight");
            mergeWithBuildableCornerObjectProperty = serializedObject.FindProperty("mergeWithBuildableCornerObject");
            placementTypeProperty = serializedObject.FindProperty("placementType");
            freezeRotationProperty = serializedObject.FindProperty("freezeRotation");
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "BuildableEdgeObjectSOEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);
            
            spawnedObjectLayerVisualElement = root.Q<VisualElement>("Spawned_Object_Layer");
            setSpawnedObjectLayerField = root.Q<Toggle>("Set_Spawned_Object_Layer");
            setSpawnedObjectLayerField.RegisterCallback<ChangeEvent<bool>>(OnActivateSetSpawnObjectLayerFieldBoolChange);

            minimumHeightThresholdPercentageVisualElement = root.Q<VisualElement>("Minimum_Height_Threshold_Percentage");
            snapToTheClosestVerticalGridHeightField = root.Q<Toggle>("Snap_To_The_Closest_Vertical_Grid_Height");
            snapToTheClosestVerticalGridHeightField.RegisterCallback<ChangeEvent<bool>>(OnActivateSnapToTheClosestVerticalGridHeightFieldBoolChange);

            buildableCornerObjectSOVisualElement = root.Q<VisualElement>("Buildable_Corner_Object_SO");
            onlyUsedWithGhostObjectVisualElement = root.Q<VisualElement>("Only_Used_With_Ghost_Object");
            mergeWithBuildableCornerObjectField = root.Q<Toggle>("Merge_With_Buildable_Corner_Object");
            mergeWithBuildableCornerObjectField.RegisterCallback<ChangeEvent<bool>>(OnActivateMergeWithBuildableCornerObjectFieldBoolChange);

            intervalBetweenEachPlacementVisualElement = root.Q<VisualElement>("Interval_Between_Each_Placement");
            placementTypeField = root.Q<EnumField>("Placement_Type");
            placementTypeField.RegisterValueChangedCallback(OnPlacementTypeValueChange);;

            fourDirectionalRotationVisualElement = root.Q<VisualElement>("Four_Directional_Rotation");
            freezeRotationField = root.Q<Toggle>("Freeze_Rotation");
            freezeRotationField.RegisterCallback<ChangeEvent<bool>>(OnActivateFreezeRotationFieldBoolChange);
            
            return root;
        }

        private void OnActivateSetSpawnObjectLayerFieldBoolChange(ChangeEvent<bool> evt)
        {
            if(setSpawnedObjectLayerProperty.boolValue == false) spawnedObjectLayerVisualElement.style.display = DisplayStyle.None;
            else spawnedObjectLayerVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnActivateSnapToTheClosestVerticalGridHeightFieldBoolChange(ChangeEvent<bool> evt)
        {
            if(snapToTheClosestVerticalGridHeightProperty.boolValue == false) minimumHeightThresholdPercentageVisualElement.style.display = DisplayStyle.None;
            else minimumHeightThresholdPercentageVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnActivateMergeWithBuildableCornerObjectFieldBoolChange(ChangeEvent<bool> evt)
        {
            if(mergeWithBuildableCornerObjectProperty.boolValue == false) 
            {
                buildableCornerObjectSOVisualElement.style.display = DisplayStyle.None; 
                onlyUsedWithGhostObjectVisualElement.style.display = DisplayStyle.None; 
            }
            else 
            {
                buildableCornerObjectSOVisualElement.style.display = DisplayStyle.Flex;
                onlyUsedWithGhostObjectVisualElement.style.display = DisplayStyle.Flex;
            }
        }

        private void OnPlacementTypeValueChange(ChangeEvent<Enum> evt)
        {
            if(placementTypeProperty.enumValueIndex == (int)CornerObjectPlacementType.SinglePlacement || placementTypeProperty.enumValueFlag == (int)CornerObjectPlacementType.PaintPlacement) 
            {
                intervalBetweenEachPlacementVisualElement.style.display = DisplayStyle.None;
            }    
            else intervalBetweenEachPlacementVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnActivateFreezeRotationFieldBoolChange(ChangeEvent<bool> evt)
        {
            if(freezeRotationProperty.boolValue == false) fourDirectionalRotationVisualElement.style.display = DisplayStyle.None; 
            else fourDirectionalRotationVisualElement.style.display = DisplayStyle.Flex;
        }
    }
}