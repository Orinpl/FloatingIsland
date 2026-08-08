using UnityEngine;

namespace SoulGames.Utilities
{
    public class OrbitAroundObject : MonoBehaviour
    {
        [Header("Orbit Settings")]
        public Transform target; // The object to orbit around
        public float orbitSpeed = 10f; // Speed of the orbit
        public float orbitSmoothness = 5f; // Smoothness of the orbit interpolation
        public float orbitDistance = 5f; // Distance from the target

        private float currentAngle = 0f;

        void Start()
        {
            if (target != null)
            {
                // Calculate the initial angle based on the current position
                Vector3 direction = transform.position - target.position;
                currentAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            }
        }

        void Update()
        {
            if (target != null)
            {
                // Update the angle based on the orbit speed
                currentAngle += orbitSpeed * Time.deltaTime;

                // Calculate the new position
                float radianAngle = currentAngle * Mathf.Deg2Rad;
                Vector3 targetPosition = new Vector3(Mathf.Cos(radianAngle) * orbitDistance, 0f, Mathf.Sin(radianAngle) * orbitDistance);

                targetPosition += target.position; // Offset by the target's position

                // Smoothly interpolate the object's position towards the target position
                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * orbitSmoothness);
            }
        }
    }
}