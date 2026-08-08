using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace SoulGames.EasyGridBuilderPro
{
    [CustomEditor(typeof(BuildableFreeObjectSO))]
    public class BuildableFreeObjectSOEditor : Editor
    {
        public VisualTreeAsset visualTreeAsset = default;
        private VisualElement root;

        private SerializedProperty setSpawnedObjectLayerProperty;
        private Toggle setSpawnedObjectLayerField;
        private VisualElement spawnedObjectLayerVisualElement;

        private SerializedProperty placementTypeProperty;
        private EnumField placementTypeField;
        private VisualElement objectBaseSpacingAlongSplineVisualElement;
        private VisualElement objectMinSpacingAlongSplineVisualElement;
        private VisualElement objectMaxSpacingAlongSplineVisualElement;
        private VisualElement objectRotateToSplineDirectionVisualElement;
        private VisualElement spacingChangeSmoothnessVisualElement;
        private VisualElement splineTangetModeVisualElement;
        private VisualElement closedSplineVisualElement;
        private VisualElement faceCollidingSurfaceNormalDirectionVisualElement;
        private VisualElement surfaceNormalDirectionLayerMaskVisualElement;

        private SerializedProperty faceCollidingSurfaceNormalDirectionProperty;
        private Toggle faceCollidingSurfaceNormalDirectionField;

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
            placementTypeProperty = serializedObject.FindProperty("placementType");
            rotationTypeProperty = serializedObject.FindProperty("rotationType");
            freezeRotationProperty = serializedObject.FindProperty("freezeRotation");
            faceCollidingSurfaceNormalDirectionProperty = serializedObject.FindProperty("faceCollidingSurfaceNormalDirection");
        }

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            if (visualTreeAsset == null)
            {
                string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
                string scriptDirectory = System.IO.Path.GetDirectoryName(scriptPath);
                string uxmlPath = System.IO.Path.Combine(scriptDirectory, "BuildableFreeObjectSOEditor.uxml");
                visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            }
            visualTreeAsset.CloneTree(root);

            spawnedObjectLayerVisualElement = root.Q<VisualElement>("Spawned_Object_Layer");
            setSpawnedObjectLayerField = root.Q<Toggle>("Set_Spawned_Object_Layer");
            setSpawnedObjectLayerField.RegisterCallback<ChangeEvent<bool>>(OnActivateSetSpawnObjectLayerFieldBoolChange);

            objectBaseSpacingAlongSplineVisualElement = root.Q<VisualElement>("Object_Base_Spacing_Along_Spline");
            objectMinSpacingAlongSplineVisualElement = root.Q<VisualElement>("Object_Min_Spacing_Along_Spline");
            objectMaxSpacingAlongSplineVisualElement = root.Q<VisualElement>("Object_Max_Spacing_Along_Spline");
            objectRotateToSplineDirectionVisualElement = root.Q<VisualElement>("Object_Rotate_To_Spline_Direction");
            spacingChangeSmoothnessVisualElement = root.Q<VisualElement>("Spacing_Change_Smoothness");
            splineTangetModeVisualElement = root.Q<VisualElement>("Spline_Tanget_Mode");
            closedSplineVisualElement = root.Q<VisualElement>("Closed_Spline");
            faceCollidingSurfaceNormalDirectionVisualElement = root.Q<VisualElement>("Face_Colliding_Surface_Normal_Direction");
            surfaceNormalDirectionLayerMaskVisualElement = root.Q<VisualElement>("Surface_Normal_Direction_Layer_Mask");
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

            faceCollidingSurfaceNormalDirectionField = root.Q<Toggle>("Face_Colliding_Surface_Normal_Direction");
            faceCollidingSurfaceNormalDirectionField.RegisterCallback<ChangeEvent<bool>>(OnActivateFaceCollidingSurfaceNormalDirectionFieldBoolChange);

            return root;
        }
        
        private void OnActivateSetSpawnObjectLayerFieldBoolChange(ChangeEvent<bool> evt)
        {
            if (setSpawnedObjectLayerProperty.boolValue == false) spawnedObjectLayerVisualElement.style.display = DisplayStyle.None;
            else spawnedObjectLayerVisualElement.style.display = DisplayStyle.Flex;
        }

        private void OnPlacementTypeValueChange(ChangeEvent<Enum> evt)
        {
            if (placementTypeProperty.enumValueIndex == (int)CornerObjectPlacementType.SinglePlacement || placementTypeProperty.enumValueFlag == (int)CornerObjectPlacementType.PaintPlacement) 
            {
                objectBaseSpacingAlongSplineVisualElement.style.display = DisplayStyle.None;
                objectMinSpacingAlongSplineVisualElement.style.display = DisplayStyle.None;
                objectMaxSpacingAlongSplineVisualElement.style.display = DisplayStyle.None;
                objectRotateToSplineDirectionVisualElement.style.display = DisplayStyle.None;
                spacingChangeSmoothnessVisualElement.style.display = DisplayStyle.None;
                splineTangetModeVisualElement.style.display = DisplayStyle.None;
                closedSplineVisualElement.style.display = DisplayStyle.None;
                faceCollidingSurfaceNormalDirectionVisualElement.style.display = DisplayStyle.Flex;
                if (faceCollidingSurfaceNormalDirectionProperty.boolValue == true) surfaceNormalDirectionLayerMaskVisualElement.style.display = DisplayStyle.Flex;
            }    
            else 
            {   
                objectBaseSpacingAlongSplineVisualElement.style.display = DisplayStyle.Flex;
                objectMinSpacingAlongSplineVisualElement.style.display = DisplayStyle.Flex;
                objectMaxSpacingAlongSplineVisualElement.style.display = DisplayStyle.Flex;
                objectRotateToSplineDirectionVisualElement.style.display = DisplayStyle.Flex;
                spacingChangeSmoothnessVisualElement.style.display = DisplayStyle.Flex;
                splineTangetModeVisualElement.style.display = DisplayStyle.Flex;
                closedSplineVisualElement.style.display = DisplayStyle.Flex;
                faceCollidingSurfaceNormalDirectionVisualElement.style.display = DisplayStyle.None;
                surfaceNormalDirectionLayerMaskVisualElement.style.display = DisplayStyle.None;
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

        private void OnActivateFaceCollidingSurfaceNormalDirectionFieldBoolChange(ChangeEvent<bool> evt)
        {
            if (faceCollidingSurfaceNormalDirectionProperty.boolValue == false) surfaceNormalDirectionLayerMaskVisualElement.style.display = DisplayStyle.None;
            else surfaceNormalDirectionLayerMaskVisualElement.style.display = DisplayStyle.Flex;
        }
    }
}