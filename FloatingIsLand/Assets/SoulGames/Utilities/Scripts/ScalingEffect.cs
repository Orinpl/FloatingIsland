using System.Collections;
using UnityEngine;

namespace SoulGames.Utilities
{
    public class ScalingEffect : MonoBehaviour
    {
        [Header("Scale Effect Axes")]
        [SerializeField] private bool useScaleEffectX;
        [SerializeField] private AnimationCurve ScaleEffectXAnimationCurve = null;

        [SerializeField] private bool useScaleEffectY;
        [SerializeField] private AnimationCurve ScaleEffectYAnimationCurve = null;

        [SerializeField] private bool useScaleEffectZ;
        [SerializeField] private AnimationCurve ScaleEffectZAnimationCurve = null;
        
        [Header("Delayed Start Settings")]
        [SerializeField] private bool enableDelayedStart = false;
        [SerializeField] private Vector2 randomDelayRange = new Vector2(0f, 1f);

        [Header("Scaling Settings")]
        [SerializeField] private Vector3 targetScale;
        [SerializeField] private float stopTime = 1f;
        [SerializeField] private bool destroyComponentAfterStop = false;

        private float time;
        private bool effectStarted = false;

        private void Start()
        {
            if (enableDelayedStart)
            {
                float delay = Random.Range(randomDelayRange.x, randomDelayRange.y);
                StartCoroutine(DelayedStart(delay));
            }
            else effectStarted = true;
        }

        private void Update()
        {
            if (!effectStarted) return;

            if (time >= stopTime)
            {
                if (destroyComponentAfterStop) Destroy(this);
                else return;
            }

            if (useScaleEffectX) ScaleEffectX();
            if (useScaleEffectY) ScaleEffectY();
            if (useScaleEffectZ) ScaleEffectZ();
        }

        private IEnumerator DelayedStart(float delay)
        {
            yield return new WaitForSeconds(delay);
            effectStarted = true;
        }

        private void ScaleEffectX()
        {
            time += Time.deltaTime;
            transform.localScale = new Vector3(ScaleEffectXAnimationCurve.Evaluate(time), targetScale.y, targetScale.z);
        }

        private void ScaleEffectY()
        {
            time += Time.deltaTime;
            transform.localScale = new Vector3(targetScale.x, ScaleEffectYAnimationCurve.Evaluate(time), targetScale.z);
        }

        private void ScaleEffectZ()
        {
            time += Time.deltaTime;
            transform.localScale = new Vector3(targetScale.x, targetScale.y, ScaleEffectZAnimationCurve.Evaluate(time));
        }
    }
}