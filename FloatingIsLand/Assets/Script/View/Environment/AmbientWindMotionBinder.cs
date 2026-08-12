using System;
using UnityEngine;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Adds small ambient motion to known wind-related prefabs after they are instantiated.
    /// </summary>
    public static class AmbientWindMotionBinder
    {
        private const string GiantWindmillPath = "Prefab/Element/giantWindmill";
        private const string SailPath = "Prefab/Building/sail_01";

        private static readonly string[] BladeNameHints =
        {
            "blade",
            "blades",
            "fan",
            "rotor",
            "propeller",
            "wheel",
        };

        private static readonly string[] SailNameHints =
        {
            "sail",
            "cloth",
            "canvas",
            "flag",
            "plane",
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
                return;
            }

            if (string.Equals(prefabPath, SailPath, StringComparison.OrdinalIgnoreCase))
            {
                ApplySail(instance);
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
                    $"[表现] {instance.name} 未找到扇叶节点，已跳过风车扇叶转动。请给扇叶节点命名包含 blade/fan/rotor 后再生成。",
                    instance);
                return;
            }

            var rotator = blade.gameObject.AddComponent<WindmillBladeRotator>();
            rotator.Rpm = 45f;
        }

        private static void ApplySail(GameObject instance)
        {
            if (instance.GetComponentInChildren<SailWindShake>(true) != null)
            {
                return;
            }

            Transform sail = FindRendererByNameHints(instance.transform, SailNameHints);
            if (sail == null)
            {
                sail = FindLargestRendererTransform(instance.transform);
            }
            if (sail == null)
            {
                Debug.LogWarning($"[表现] {instance.name} 未找到风帆布面节点，已跳过风帆抖动。", instance);
                return;
            }

            sail.gameObject.AddComponent<SailWindShake>();
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

        private static Transform FindRendererByNameHints(Transform root, string[] hints)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                string objectName = renderers[i].name;
                for (int j = 0; j < hints.Length; j++)
                {
                    if (objectName.IndexOf(hints[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return renderers[i].transform;
                    }
                }
            }

            return null;
        }

        private static Transform FindLargestRendererTransform(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            Renderer largest = null;
            float largestArea = 0f;

            for (int i = 0; i < renderers.Length; i++)
            {
                Bounds bounds = renderers[i].bounds;
                float area = bounds.size.x * bounds.size.y + bounds.size.x * bounds.size.z + bounds.size.y * bounds.size.z;
                if (area > largestArea)
                {
                    largestArea = area;
                    largest = renderers[i];
                }
            }

            return largest != null ? largest.transform : null;
        }
    }
}
