using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using SoulGames.EasyGridBuilderPro;

namespace SoulGames.Utilities
{
    public class MouseInteractionUtilities
    {
        private static Vector3 lastValidPosition = new Vector3(-99999, -99999, -99999);

        public static Vector3 GetMouseWorldPosition(float maxDistance = 99999)
        {
            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, maxDistance))
            {
                return raycastHit.point;
            }
            else return new Vector3(-99999, -99999, -99999);
        }

        public static Vector3 GetMouseWorldPosition(LayerMask mouseColliderLayerMask, float maxDistance = 99999)
        {
            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, maxDistance, mouseColliderLayerMask))
            {
                return raycastHit.point;
            }
            else return new Vector3(-99999, -99999, -99999);
        }

        public static Vector3 GetMouseWorldPosition(LayerMask mouseColliderLayerMask, Vector3 secondRayDirection,  out Quaternion hitRotation, float maxDistance = 99999)
        {
            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            float calculatedMaxDistance = GridManager.Instance.GetActiveCameraMode() == CameraMode.TopDown ? maxDistance : GridManager.Instance.GetMaximumDistance();
            Vector3 firstRayEndPoint = ray.GetPoint(calculatedMaxDistance);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, calculatedMaxDistance, mouseColliderLayerMask))
            {
                hitRotation = Quaternion.FromToRotation(Vector3.up, raycastHit.normal);
                return raycastHit.point;
            }
            else
            {
                if (Physics.Raycast(firstRayEndPoint, secondRayDirection, out RaycastHit downRaycastHit, calculatedMaxDistance, mouseColliderLayerMask))
                {
                    hitRotation = Quaternion.FromToRotation(Vector3.up, downRaycastHit.normal);
                    return downRaycastHit.point;
                }
            }

            hitRotation = Quaternion.identity;
            return new Vector3(-99999, -99999, -99999);
        }

        public static Vector3 GetMouseWorldPosition(LayerMask mouseColliderLayerMask, out bool isHit, float maxDistance = 99999)
        {
            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, maxDistance, mouseColliderLayerMask))
            {
                isHit = true;
                return raycastHit.point;
            }
            else
            {
                isHit = false;
                return new Vector3(-99999, -99999, -99999);
            }
        }

        public static bool TryGetMouseWorldPosition(LayerMask mouseColliderLayerMask, out Vector3 worldPosition, float maxDistance = 99999)
        {
            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, maxDistance, mouseColliderLayerMask))
            {
                worldPosition =  raycastHit.point;
                return true;
            }
            else
            {
                worldPosition = new Vector3(-99999, -99999, -99999);
                return false;
            }
        }

        public static Vector3 GetMouseWorldPositionForBuildableFreeObject(LayerMask mouseColliderLayerMask, BuildableFreeObjectSO buildableFreeObjectSO, Vector3 secondRayDirection, out Vector3 hitNormals, float maxDistance = 99999)
        {
            hitNormals = default;

            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            float calculatedMaxDistance = GridManager.Instance.GetActiveCameraMode() == CameraMode.TopDown ? maxDistance : GridManager.Instance.GetMaximumDistance();
            Vector3 firstRayEndPoint = ray.GetPoint(calculatedMaxDistance);
            
            if (Physics.Raycast(ray, out RaycastHit raycastHit, calculatedMaxDistance, mouseColliderLayerMask))
            {
                if (((1 << raycastHit.collider.gameObject.layer) & buildableFreeObjectSO.surfaceNormalDirectionLayerMask) != 0) hitNormals = raycastHit.normal;
                if (buildableFreeObjectSO.affectByFreeObjectSnappers && raycastHit.collider.TryGetComponent<BuildableFreeObjectSnapper>(out BuildableFreeObjectSnapper buildableFreeObjectSnapper))
                {
                    if (buildableFreeObjectSnapper.GetSnapAllFreeObjects() || buildableFreeObjectSnapper.GetSnapFreeObjectCategoriesList().Contains(buildableFreeObjectSO.buildableFreeObjectCategorySO))
                    {
                        return buildableFreeObjectSnapper.transform.position;
                    }
                }
                return raycastHit.point;
            }
            else
            {
                if (Physics.Raycast(firstRayEndPoint, secondRayDirection, out RaycastHit downRaycastHit, calculatedMaxDistance, mouseColliderLayerMask))
                {
                    if (((1 << downRaycastHit.collider.gameObject.layer) & buildableFreeObjectSO.surfaceNormalDirectionLayerMask) != 0) hitNormals = downRaycastHit.normal;
                    if (buildableFreeObjectSO.affectByFreeObjectSnappers && downRaycastHit.collider.TryGetComponent<BuildableFreeObjectSnapper>(out BuildableFreeObjectSnapper buildableFreeObjectSnapper))
                    {
                        if (buildableFreeObjectSnapper.GetSnapAllFreeObjects() || buildableFreeObjectSnapper.GetSnapFreeObjectCategoriesList().Contains(buildableFreeObjectSO.buildableFreeObjectCategorySO))
                        {
                            return buildableFreeObjectSnapper.transform.position;
                        }
                    }
                    return downRaycastHit.point;
                }
            }
            return new Vector3(-99999, -99999, -99999);
        }

        public static SoulGames.EasyGridBuilderPro.EasyGridBuilderPro GetEasyGridBuilderProWithCustomSurface(bool useMinimumDistance, LayerMask firstMouseColliderLayerMask, LayerMask secondMouseColliderLayerMask, Vector3 secondRayDirection, 
            float maxDistance = 99999)
        {
            SoulGames.EasyGridBuilderPro.EasyGridBuilderPro easyGridBuilderPro = default;

            Vector3 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            float distance = useMinimumDistance ? GridManager.Instance.GetMinimumDistance() : GridManager.Instance.GetMaximumDistance();
            float calculatedMaxDistance = GridManager.Instance.GetActiveCameraMode() == CameraMode.TopDown ? maxDistance : distance;
            Vector3 firstRayEndPoint = ray.GetPoint(calculatedMaxDistance);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, calculatedMaxDistance, firstMouseColliderLayerMask | secondMouseColliderLayerMask))
            {
                if (raycastHit.collider.TryGetComponent< SoulGames.EasyGridBuilderPro.EasyGridBuilderPro>(out easyGridBuilderPro)) return easyGridBuilderPro;

                if (Physics.Raycast(raycastHit.point, secondRayDirection, out RaycastHit secondRaycastHit, maxDistance, secondMouseColliderLayerMask))
                {
                    if (secondRaycastHit.collider.TryGetComponent< SoulGames.EasyGridBuilderPro.EasyGridBuilderPro>(out easyGridBuilderPro)) return easyGridBuilderPro;
                }
            }
            else
            {
                if (Physics.Raycast(firstRayEndPoint, Vector3.down * 99999, out RaycastHit downRaycastHit, calculatedMaxDistance, firstMouseColliderLayerMask | secondMouseColliderLayerMask))
                {
                    if (downRaycastHit.collider.TryGetComponent< SoulGames.EasyGridBuilderPro.EasyGridBuilderPro>(out easyGridBuilderPro)) return easyGridBuilderPro;

                    if (Physics.Raycast(downRaycastHit.point, secondRayDirection, out RaycastHit secondRaycastHit, maxDistance, secondMouseColliderLayerMask))
                    {
                        if (secondRaycastHit.collider.TryGetComponent< SoulGames.EasyGridBuilderPro.EasyGridBuilderPro>(out easyGridBuilderPro)) return easyGridBuilderPro;
                    }
                }
            }

            return easyGridBuilderPro;
        }

        public static Vector3 GetMouseWorldPositionWithCustomSurface(LayerMask firstMouseColliderLayerMask, LayerMask secondMouseColliderLayerMask, Vector3 secondRayDirection, out bool directHitGridCollisionLayer, 
            out Vector3 firstCollisionWorldPosition, float maxDistance = 99999)
        {
            directHitGridCollisionLayer = false;
            firstCollisionWorldPosition = default;

            Vector3 inputPosition = GetCurrentMousePosition();

            // Return cached position if no input detected
            if (inputPosition == Vector3.zero) return lastValidPosition;

            Ray ray = Camera.main.ScreenPointToRay(inputPosition);
            float calculatedMaxDistance = GridManager.Instance.GetActiveCameraMode() == CameraMode.TopDown ? maxDistance : GridManager.Instance.GetMaximumDistance();
            Vector3 firstRayEndPoint = ray.GetPoint(calculatedMaxDistance);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, calculatedMaxDistance, firstMouseColliderLayerMask | secondMouseColliderLayerMask))
            {
                firstCollisionWorldPosition = raycastHit.point;

                if (raycastHit.collider.TryGetComponent<EasyGridBuilderProXZ>(out _)) 
                {
                    directHitGridCollisionLayer = true;
                    lastValidPosition = raycastHit.point; // Cache the position
                    return raycastHit.point;
                }
                if (raycastHit.collider.TryGetComponent<EasyGridBuilderProXY>(out _))
                {
                    directHitGridCollisionLayer = true;
                    lastValidPosition = raycastHit.point; // Cache the position
                    return raycastHit.point;
                }

                if (Physics.Raycast(raycastHit.point, secondRayDirection, out RaycastHit secondRaycastHit, maxDistance, secondMouseColliderLayerMask))
                {
                    directHitGridCollisionLayer = false;
                    lastValidPosition = secondRaycastHit.point; // Cache the position
                    return secondRaycastHit.point;
                }
            }
            else
            {
                if (Physics.Raycast(firstRayEndPoint, Vector3.down * 99999, out RaycastHit downRaycastHit, calculatedMaxDistance, firstMouseColliderLayerMask | secondMouseColliderLayerMask))
                {
                    firstCollisionWorldPosition = downRaycastHit.point;

                    if (downRaycastHit.collider.TryGetComponent<EasyGridBuilderProXZ>(out _)) 
                    {
                        directHitGridCollisionLayer = true;
                        lastValidPosition = downRaycastHit.point; // Cache the position
                        return downRaycastHit.point;
                    }
                    if (downRaycastHit.collider.TryGetComponent<EasyGridBuilderProXY>(out _))
                    {
                        directHitGridCollisionLayer = true;
                        lastValidPosition = downRaycastHit.point; // Cache the position
                        return downRaycastHit.point;
                    }

                    if (Physics.Raycast(downRaycastHit.point, secondRayDirection, out RaycastHit secondRaycastHit, maxDistance, secondMouseColliderLayerMask))
                    {
                        directHitGridCollisionLayer = false;
                        lastValidPosition = secondRaycastHit.point; // Cache the position
                        return secondRaycastHit.point;
                    }
                }
            }
            
            return lastValidPosition;
        }

        public static Vector3 GetScreenCenterRaycastHitPosition(LayerMask targetLayerMask)
        {
            // Define the center of the screen as a point (width / 2, height / 2)
            Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            // Create a ray from the camera through the screen center
            Ray ray = Camera.main.ScreenPointToRay(screenCenter);

            // Perform the raycast
            if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, targetLayerMask))
            {
                // Return the hit position if the ray hit an object in the specified layer
                return hitInfo.point;
            }

            // Return null if nothing was hit
            return new Vector3(-99999, -99999, -99999);
        }

        public static bool TryGetBuildableObject(LayerMask mouseColliderLayerMask, out BuildableObject buildableObject, float maxDistance = 99999)
        {
            Vector2 inputPosition = GetCurrentMousePosition();
            Ray ray = Camera.main.ScreenPointToRay(inputPosition);

            if (Physics.Raycast(ray, out RaycastHit raycastHit, maxDistance, mouseColliderLayerMask))
            {
                // Check the GameObject itself
                if (raycastHit.collider.TryGetComponent<BuildableObject>(out buildableObject)) return true;
                
                // Check all parents
                Transform currentParent = raycastHit.collider.transform.parent;
                while (currentParent != null)
                {
                    if (currentParent.TryGetComponent<BuildableObject>(out buildableObject)) return true;
                    currentParent = currentParent.parent;
                }

                // Check all children recursively
                foreach (Transform child in raycastHit.collider.transform)
                {
                    if (child.TryGetComponent<BuildableObject>(out buildableObject)) return true;
                }
            }
            
            buildableObject = null;
            return false;
        }

        public static bool IsMousePointerOverUI() 
        {
            // Create PointerEventData with the current mouse position
            PointerEventData eventData = new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() };
            
            // Prepare a list to receive the raycast results
            List<RaycastResult> raycastResults = new List<RaycastResult>();

            // Perform the raycast using the event system
            EventSystem.current.RaycastAll(eventData, raycastResults);

            // Return true if any raycast hit a UI element with a RectTransform component
            return raycastResults.Any(result => result.gameObject.GetComponent<RectTransform>() != null);
        }

        public static Vector3 GetCurrentMousePosition()
        {
            // Get the input position from either the mouse or touch
            Vector2 inputPosition;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed) inputPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            else if (Mouse.current != null) inputPosition = Mouse.current.position.ReadValue();
            else return default;

            return inputPosition;
        }

        public static Vector3 GetScreenToWorldPosition(Vector3 screenPosition)
        {
            return Camera.main.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, Camera.main.nearClipPlane));
        }

        public static Vector3 GetWorldToScreenPosition(Vector3 worldPosition)
        {
            return Camera.main.WorldToScreenPoint(worldPosition);
        }

        public static Vector3 ClampWorldPositionToScreenPosition(Vector3 worldPosition)
        {
            Camera mainCamera = Camera.main;

            // Convert world position to screen position
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            // Clamp the screen position
            screenPosition.x = Mathf.Clamp(screenPosition.x, 0, Screen.width);
            screenPosition.y = Mathf.Clamp(screenPosition.y, 0, Screen.height);

            // Convert screen position back to world position
            Vector3 clampedWorldPosition = mainCamera.ScreenToWorldPoint(screenPosition);

            // Ensure the z-coordinate is preserved
            clampedWorldPosition.z = worldPosition.z;

            return clampedWorldPosition;
        }
    }
}
