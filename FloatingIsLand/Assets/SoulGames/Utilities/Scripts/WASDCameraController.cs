using UnityEngine;

namespace SoulGames.Utilities
{
    public class WASDCameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        [Tooltip("Camera transform")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Camera movement normal speed")]
        [SerializeField] private float normalSpeed = 1.5f;
        [Tooltip("Camera movement fast speed")]
        [SerializeField] private float fastSpeed = 3f;
        [Tooltip("Camera movement smooth time")]
        [SerializeField] private float movementLerpTime = 10f;

        [Header("Rotation Settings")]
        [Tooltip("Camera rotation amount")]
        [SerializeField] private float rotationAmount = 2f;
        [Tooltip("Camera rotation smooth time")]
        [SerializeField] private float rotationLerpTime = 10f;

        [Header("Zoom Settings")]
        [Tooltip("Camera zoom amount")]
        [SerializeField] private float zoomAmount = 0.5f;
        [Tooltip("Camera minimum zoomable distance")]
        [SerializeField] private float minZoom = 10f;
        [Tooltip("Camera maximum zoomable distance")]
        [SerializeField] private float maxZoom = 100f;
        [Tooltip("Camera zoom smooth time")]
        [SerializeField] private float zoomLerpTime = 5f;

        [Header("Height Adjustment")]
        [Tooltip("Enable to set camera height automatically using raycast to the ground")]
        [SerializeField] public bool setHeightByRaycast = false;
        [Tooltip("Layer mask for raycasting")]
        [SerializeField] private LayerMask raycastLayerMask;

        [Header("Input Keys")]
        [SerializeField] private KeyCode upKey = KeyCode.W;
        [SerializeField] private KeyCode downKey = KeyCode.S;
        [SerializeField] private KeyCode leftKey = KeyCode.A;
        [SerializeField] private KeyCode rightKey = KeyCode.D;
        [SerializeField] private KeyCode speedUpKey = KeyCode.LeftShift;
        [SerializeField] private KeyCode rotateLeftKey = KeyCode.Q;
        [SerializeField] private KeyCode rotateRightKey = KeyCode.E;
        [SerializeField] private KeyCode zoomInKey = KeyCode.Z;
        [SerializeField] private KeyCode zoomOutKey = KeyCode.X;

        private float movementSpeed;
        private Vector3 newPosition;
        private Quaternion newRotation;
        private float targetZoom;
        private Vector3 raycastHeight = Vector3.zero;

        private void Start()
        {
            newPosition = transform.position;
            newRotation = transform.rotation;
            targetZoom = cameraTransform.localPosition.y;
        }

        private void FixedUpdate()
        {
            HandleInput();

            if (setHeightByRaycast)
            {
                raycastHeight = GetRaycastHeight();
                raycastHeight = new Vector3(0, raycastHeight.y, 0);
            }

            // Apply transformations with smoothing
            cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition,new Vector3(0, targetZoom, -targetZoom) + raycastHeight,Time.deltaTime * zoomLerpTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * rotationLerpTime);
            transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementLerpTime);
        }

        private void HandleInput()
        {
            // Movement Speed
            movementSpeed = Input.GetKey(speedUpKey) ? fastSpeed : normalSpeed;

            // Movement
            if (Input.GetKey(upKey) || Input.GetKey(KeyCode.UpArrow)) newPosition += transform.forward * movementSpeed;
            if (Input.GetKey(downKey) || Input.GetKey(KeyCode.DownArrow)) newPosition += transform.forward * -movementSpeed;
            if (Input.GetKey(rightKey) || Input.GetKey(KeyCode.RightArrow)) newPosition += transform.right * movementSpeed;
            if (Input.GetKey(leftKey) || Input.GetKey(KeyCode.LeftArrow)) newPosition += transform.right * -movementSpeed;

            // Rotation
            if (Input.GetKey(rotateLeftKey)) newRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
            if (Input.GetKey(rotateRightKey)) newRotation *= Quaternion.Euler(Vector3.up * -rotationAmount);

            // Zoom
            if (Input.GetKey(zoomInKey)) targetZoom -= zoomAmount;
            if (Input.GetKey(zoomOutKey)) targetZoom += zoomAmount;

            // Clamp Zoom
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);
        }

        private Vector3 GetRaycastHeight()
        {
            if (Physics.Raycast(cameraTransform.position, Vector3.down, out RaycastHit hit, Mathf.Infinity, raycastLayerMask)) return hit.point;
            return Vector3.zero;
        }
    }
}