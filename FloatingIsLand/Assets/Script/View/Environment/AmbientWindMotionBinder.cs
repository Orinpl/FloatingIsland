using System;
using UnityEngine;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Wires up wind-related behaviour that a prefab cannot carry on its own after it is
    /// instantiated.
    ///
    /// Windmills are deliberately NOT handled here. Their blades are separate nodes in the FBX
    /// now, so <see cref="WindmillBladeRotator"/> is authored straight onto the blade node in the
    /// prefab (see the editor menu Tools/FI/Rig Wind Prefabs). Adding it again at spawn time
    /// would double up the rotation.
    ///
    /// The sail still needs a runtime step: its cloth is displaced by the FI/Sail Wind shader,
    /// which has to be told how big the sail actually is. That scale is only known once the
    /// instance has been placed and scaled, so <see cref="SailWindObjectScaleBinder"/> is
    /// attached here rather than baked into the prefab.
    /// </summary>
    public static class AmbientWindMotionBinder
    {
        private const string SailPath = "Prefab/Building/sail_01";

        private static readonly string[] ClothNameHints =
        {
            "cloth",
            "canvas",
            "fabric",
        };

        public static void Apply(GameObject instance, string prefabPath)
        {
            if (instance == null || string.IsNullOrEmpty(prefabPath))
            {
                return;
            }

            if (string.Equals(prefabPath, SailPath, StringComparison.OrdinalIgnoreCase))
            {
                ApplySail(instance);
            }
        }

        private static void ApplySail(GameObject instance)
        {
            if (instance.GetComponentInChildren<SailWindObjectScaleBinder>(true) != null)
            {
                return;
            }

            Transform cloth = FindByNameHints(instance.transform, ClothNameHints);
            if (cloth == null)
            {
                Debug.LogWarning(
                    $"[View] {instance.name} has no cloth node. Sail wind scale was not bound. " +
                    "Rename the cloth node to include cloth/canvas/fabric.",
                    instance);
                return;
            }

            cloth.gameObject.AddComponent<SailWindObjectScaleBinder>();
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
