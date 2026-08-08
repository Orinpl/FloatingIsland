using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Buildables/Buildable Object Configurator", 0)]
    public class BuildableObjectConfigurator : MonoBehaviour
    {
        #if UNITY_EDITOR
        [SerializeField] private GridAxis gridAxis = GridAxis.XZ;
        [SerializeField] private BuildableObjectType buildableObjectType = BuildableObjectType.BuildableGridObject;
        [SerializeField] private GameObject _gameObject;
        
        public void ConfigureBuildableObject()
        {
            if (_gameObject == null)
            {
                Debug.Log("<color=orange><b>No GameObject assigned!</b></color> Please add a GameObject containing the 3D model or 2D image you wish to use for configuring the Buildable Object.");
                return;
            }

            transform.position = Vector3.zero;        // Resets position to (0, 0, 0)
            transform.rotation = Quaternion.identity; // Resets rotation to (0, 0, 0)
            transform.localScale = Vector3.one;       // Resets scale to (1, 1, 1)

            Transform instantiatedGameObject = Instantiate(_gameObject).transform;
            instantiatedGameObject.parent = transform;

            BuildableObject buildableObject;
            switch (buildableObjectType)
            {
                case BuildableObjectType.BuildableGridObject: buildableObject = gameObject.AddComponent<BuildableGridObject>(); break;
                case BuildableObjectType.BuildableEdgeObject: buildableObject = gameObject.AddComponent<BuildableEdgeObject>(); break;
                case BuildableObjectType.BuildableCornerObject: buildableObject = gameObject.AddComponent<BuildableCornerObject>(); break;
                case BuildableObjectType.BuildableFreeObject: buildableObject = gameObject.AddComponent<BuildableFreeObject>(); break;
                default: buildableObject = default; break;
            }

            buildableObject.SetGridAxis(gridAxis);
            buildableObject.AutoCalculatePivotAndSize();

            instantiatedGameObject.transform.position -= buildableObject.GetObjectCustomPivot();

            buildableObject.AutoCalculatePivotAndSize();
            buildableObject.SetLockAutoGenerationAndValues(true);

            DestroyImmediate(this);
        }
        #endif
    }
}