using UnityEngine;
using SoulGames.Utilities;
using UnityEngine.Rendering.Universal;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Utilities/Grid Custom Surface Projector", 7)]
    [ExecuteAlways]
    public class GridCustomSurfaceProjector : MonoBehaviour
    {
        [SerializeField] private bool updateInEditor = true;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private LayerMask surfaceLayerMask;
        [SerializeField] private float minProjectionScale = 5f;  // Minimum scale for close distances
        [SerializeField] private float maxProjectionScale = 50f; // Maximum scale for far distances
        [SerializeField] private float maxDistance = 100f;       // Maximum distance to consider for scaling
        [SerializeField] float projectionDepth = 100f;
        [SerializeField] private DecalProjector decalProjector;
        [SerializeField] private Camera decalCamera;

        private Vector3 raycastPosition;
        
        private void Update()
        {
            if (!updateInEditor && !Application.isPlaying) return;
            if (!mainCamera) mainCamera = Camera.main;

            raycastPosition = MouseInteractionUtilities.GetScreenCenterRaycastHitPosition(surfaceLayerMask);
            transform.position = new Vector3(raycastPosition.x, mainCamera.transform.position.y, raycastPosition.z);

            // Calculate dynamic projectionScale based on distance
            float distance = Vector3.Distance(raycastPosition, mainCamera.transform.position);
            float projectionScale = Mathf.Lerp(minProjectionScale, maxProjectionScale, distance / maxDistance);

            if (decalCamera) decalCamera.orthographicSize = projectionScale;
            if (decalProjector) decalProjector.size = new Vector3(projectionScale * 2, projectionScale * 2, projectionDepth);
        }
    }   
}