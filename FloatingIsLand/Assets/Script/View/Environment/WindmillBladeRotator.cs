using UnityEngine;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Simple visual-only rotation for windmill blades. This component must not feed back into wind simulation.
    /// </summary>
    public sealed class WindmillBladeRotator : MonoBehaviour
    {
        public enum LocalAxis
        {
            X,
            Y,
            Z,
            NegativeX,
            NegativeY,
            NegativeZ,
        }

        [SerializeField] private Transform rotationTarget;
        [SerializeField] private LocalAxis axis = LocalAxis.Z;
        [SerializeField] private float rpm = 45f;
        [SerializeField] private float accelerationRpmPerSecond = 180f;
        [SerializeField] private bool randomizeStartAngle = true;
        [SerializeField] private bool rotateInEditMode;

        private float _currentRpm;

        public Transform RotationTarget
        {
            get { return rotationTarget != null ? rotationTarget : transform; }
        }

        public float Rpm
        {
            get { return rpm; }
            set { rpm = value; }
        }

        private void OnEnable()
        {
            _currentRpm = rpm;

            if (randomizeStartAngle && Application.isPlaying)
            {
                RotationTarget.Rotate(AxisVector(axis), Random.Range(0f, 360f), Space.Self);
            }
        }

        private void Update()
        {
            if (!Application.isPlaying && !rotateInEditMode)
            {
                return;
            }

            float deltaTime = Application.isPlaying ? Time.deltaTime : Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            _currentRpm = Mathf.MoveTowards(_currentRpm, rpm, Mathf.Abs(accelerationRpmPerSecond) * deltaTime);
            RotationTarget.Rotate(AxisVector(axis), _currentRpm * 6f * deltaTime, Space.Self);
        }

        public void SetWindStrength(float windLevel, float maxWindLevel, float idleRpm, float maxWindRpm)
        {
            if (maxWindLevel <= 0f)
            {
                rpm = idleRpm;
                return;
            }

            float t = Mathf.Clamp01(windLevel / maxWindLevel);
            rpm = Mathf.Lerp(idleRpm, maxWindRpm, t);
        }

        private static Vector3 AxisVector(LocalAxis value)
        {
            switch (value)
            {
                case LocalAxis.X:
                    return Vector3.right;
                case LocalAxis.Y:
                    return Vector3.up;
                case LocalAxis.NegativeX:
                    return Vector3.left;
                case LocalAxis.NegativeY:
                    return Vector3.down;
                case LocalAxis.NegativeZ:
                    return Vector3.back;
                default:
                    return Vector3.forward;
            }
        }
    }
}
