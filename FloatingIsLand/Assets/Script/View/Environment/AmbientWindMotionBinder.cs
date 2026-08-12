using System;
using UnityEngine;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Adds small ambient motion to known wind-related prefabs after they are instantiated.
    /// Sail cloth wind is shader-driven through GlobalWindFieldController, so this binder
    /// intentionally does not add per-sail motion scripts.
    /// </summary>
    public static class AmbientWindMotionBinder
    {
        private const string GiantWindmillPath = "Prefab/Element/giantWindmill";

        private static readonly string[] BladeNameHints =
        {
            "blade",
            "blades",
            "fan",
            "rotor",
            "propeller",
            "wheel",
        };

        public static void Apply(GameObject instance, string prefabPath)
        {
            if (instance == null || string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            if (string.Equals(prefabPath, GiantWindmillPath, StringComparison.OrdinalIgnoreCase))
            {
                ApplyWindmill(instance);
            }
        }

        private static void ApplyWindmill(GameObject instance)
        {
            if (instance.GetComponentInChildren<WindmillBladeRotator>(true) != null)
            {
                return;
            }

            Transform blade = FindByNameHints(instance.transform, BladeNameHints);
            if (blade == null)
            {
                Debug.LogWarning(
                    $"[View] {instance.name} has no blade node. Windmill blade rotation was skipped. Rename the blade node to include blade/fan/rotor.",
                    instance);
                return;
            }

            var rotator = blade.gameObject.AddComponent<WindmillBladeRotator>();
            rotator.Rpm = 45f;
        }

        private static Transform FindByNameHints(Transform root, string[] hints)
        {
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == root)
                {
                    continue;
                }

                string objectName = children[i].name;
                for (int j = 0; j < hints.Length; j++)
                {
                    if (objectName.IndexOf(hints[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return children[i];
                    }
                }
            }

            return null;
        }
    }
}
