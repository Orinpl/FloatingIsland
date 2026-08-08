using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Managers/Grid Built Objects Manager", 1)]
    [RequireComponent(typeof(GridManager))]
    public class GridBuiltObjectsManager : MonoBehaviour
    {
        [SerializeField] private List<BuildableObject> builtObjectList;

        private void Start() 
        {
            builtObjectList = new List<BuildableObject>();
            StartCoroutine(LateStart());
        }

        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();
            
            GridManager.Instance.OnBuildableObjectPlaced += OnBuildableObjectPlaced;
            if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.OnBuildableObjectDestroyedInternal += OnBuildableObjectDestroyedInternal;
        }

        private void OnDestroy()
        {
            GridManager.Instance.OnBuildableObjectPlaced += OnBuildableObjectPlaced;
            if (GridManager.Instance.TryGetBuildableObjectDestroyer(out BuildableObjectDestroyer buildableObjectDestroyer)) buildableObjectDestroyer.OnBuildableObjectDestroyedInternal -= OnBuildableObjectDestroyedInternal;
        }

        private void OnBuildableObjectPlaced(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject)
        {
            builtObjectList.Add(buildableObject);
        }

        private void OnBuildableObjectDestroyedInternal(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject)
        {
            builtObjectList.Remove(buildableObject);
        }

        public List<BuildableObject> GetBuiltObjectsList() => builtObjectList;
    }
}