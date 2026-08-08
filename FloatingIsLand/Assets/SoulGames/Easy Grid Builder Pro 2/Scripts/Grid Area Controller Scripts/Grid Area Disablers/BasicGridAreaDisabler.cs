using UnityEngine;
using SoulGames.Utilities;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Basic Grid Area Disabler", 3)]
    public class BasicGridAreaDisabler : MonoBehaviour
    {
        [SerializeField] private bool blockGridObjects;
        [SerializeField] private bool blockEdgeObjects;
        [SerializeField] private bool blockCornerObjects;
        [SerializeField] private bool blockFreeObjects;

        #if UNITY_EDITOR
        [SerializeField] private bool enableGizmos = true;
        [SerializeField] private Color gizmoColor = new Color(1f, 0f, 0f, 0.25f);
        #endif

        private BoxCollider boxCollider;

        void Start()
        {
            if (!TryGetComponent<BoxCollider>(out BoxCollider collider)) boxCollider = gameObject.AddComponent<BoxCollider>();
            else boxCollider = collider;

            boxCollider.isTrigger = enabled;
        }
        
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

        public bool GetIsBlockGridObjects() => blockGridObjects;

        public bool GetIsBlockEdgeObjects() => blockEdgeObjects;

        public bool GetIsBlockCornerObjects() => blockCornerObjects;

        public bool GetIsBlockFreeObjects() => blockFreeObjects;
    }
}