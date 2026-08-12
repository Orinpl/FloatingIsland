using UnityEngine;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Lightweight visual flutter for sail cloth. It only changes the target transform locally.
    /// </summary>
    public sealed class SailWindShake : MonoBehaviour
    {
        [SerializeField] private Transform shakeTarget;
        [SerializeField] private float windStrength = 1f;
        [SerializeField] private float positionAmplitude = 0.02f;
        [SerializeField] private float rotationAmplitude = 2f;
        [SerializeField] private float frequency = 1.2f;
        [SerializeField] private float flutterFrequency = 7f;
        [SerializeField] private float flutterAmount = 0.2f;
        [SerializeField] private Vector3 localMoveAxis = Vector3.right;
        [SerializeField] private Vector3 localRotateAxis = Vector3.forward;

        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private float _phase;
        private bool _hasBasePose;

        public Transform ShakeTarget
        {
            get { return shakeTarget != null ? shakeTarget : transform; }
        }

        public float WindStrength
        {
            get { return windStrength; }
            set { windStrength = Mathf.Max(0f, value); }
        }

        private void OnEnable()
        {
            CacheBasePose();
            _phase = Random.Range(0f, 100f);
        }

        private void OnDisable()
        {
            RestoreBasePose();
        }

        private void Update()
        {
            if (!_hasBasePose)
            {
                CacheBasePose();
            }

            float t = Time.time + _phase;
            float slow = Mathf.Sin(t * frequency);
            float flutter = Mathf.Sin(t * flutterFrequency) * flutterAmount;
            float wave = (slow + flutter) * Mathf.Max(0f, windStrength);
            Transform target = ShakeTarget;

            Vector3 moveAxis = localMoveAxis.sqrMagnitude > 0f ? localMoveAxis.normalized : Vector3.right;
            Vector3 rotateAxis = localRotateAxis.sqrMagnitude > 0f ? localRotateAxis.normalized : Vector3.forward;

            target.localPosition = _baseLocalPosition + moveAxis * (wave * positionAmplitude);
            target.localRotation = _baseLocalRotation * Quaternion.AngleAxis(wave * rotationAmplitude, rotateAxis);
        }

        public void SetWindStrength(float value)
        {
            WindStrength = value;
        }

        public void ResetBasePose()
        {
            CacheBasePose();
        }

        private void CacheBasePose()
        {
            Transform target = ShakeTarget;
            _baseLocalPosition = target.localPosition;
            _baseLocalRotation = target.localRotation;
            _hasBasePose = true;
        }

        private void RestoreBasePose()
        {
            if (!_hasBasePose || shakeTarget == null && transform == null)
            {
                return;
            }

            Transform target = ShakeTarget;
            target.localPosition = _baseLocalPosition;
            target.localRotation = _baseLocalRotation;
        }
    }
}
