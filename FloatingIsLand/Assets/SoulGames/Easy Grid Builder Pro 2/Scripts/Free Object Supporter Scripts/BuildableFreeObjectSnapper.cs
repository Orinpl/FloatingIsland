using System.Collections.Generic;
using SoulGames.Utilities;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Buildable Free Object Snapper", 3)]
    public class BuildableFreeObjectSnapper : MonoBehaviour
    {
        #if UNITY_EDITOR
        [SerializeField] private bool enableGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(0f, 178f, 255f, 0.25f);
        #endif

        [SerializeField] private bool snapAllFreeObjects = true;
        [SerializeField] List<BuildableFreeObjectCategorySO> snapFreeObjectCategoriesList;

        private BoxCollider boxCollider;

        private void Start()
        {
            if (!TryGetComponent<BoxCollider>(out BoxCollider collider)) boxCollider = gameObject.AddComponent<BoxCollider>();
            else boxCollider = collider;

            boxCollider.isTrigger = enabled;
        }

        public bool GetSnapAllFreeObjects() => snapAllFreeObjects;

        public List<BuildableFreeObjectCategorySO> GetSnapFreeObjectCategoriesList() => snapFreeObjectCategoriesList;

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableGizmos) return;
            DrawWireBox();
        }

        private void DrawWireBox()
        {
            Gizmos.color = gizmoColor;

            Gizmos.DrawCube(transform.position, transform.localScale);
            CustomGizmosUtilities.DrawAAPolyWireCube(transform.position, transform.localScale, Quaternion.identity, 2, new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 255));
        }
        #endif
    }
}