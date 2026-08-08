using UnityEngine;

namespace SoulGames.Utilities
{
    public class GlobalTimeTickManager : MonoBehaviour
    {
        public static event OnGlobalTimeTickManagerTickDelegate OnGlobalTimeTickManagerTick;
        public delegate void OnGlobalTimeTickManagerTickDelegate();

        private float tickTimer = 0f;
        private float tickInterval = 0.1f;

        private void Update()
        {
            tickTimer += Time.deltaTime;

            if (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                OnGlobalTimeTickManagerTick?.Invoke();
            }
        }
    }
}