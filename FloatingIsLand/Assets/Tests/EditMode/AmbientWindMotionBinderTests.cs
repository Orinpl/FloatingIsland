using FloatingIsLand.View.Environment;
using NUnit.Framework;
using UnityEngine;

namespace FloatingIsLand.Tests
{
    public sealed class AmbientWindMotionBinderTests
    {
        [Test]
        public void WindmillIsNotTouchedByBinder()
        {
            var root = new GameObject("giantWindmill");
            var body = new GameObject("Body");
            var blade = new GameObject("giantWindmill_blade");
            body.transform.SetParent(root.transform, false);
            blade.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Element/giantWindmill");

                Assert.AreEqual(
                    0,
                    root.GetComponentsInChildren<MonoBehaviour>(true).Length,
                    "Blade rotation is authored into the prefab now; adding it again at spawn " +
                    "time would double the rotation.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SailClothReceivesScaleBinderOnlyOnTheClothNode()
        {
            var root = new GameObject("sail_01");
            var mast = new GameObject("mast");
            var cloth = new GameObject("sail_cloth");
            mast.transform.SetParent(root.transform, false);
            cloth.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Building/sail_01");

                Assert.IsNotNull(
                    cloth.GetComponent<SailWindObjectScaleBinder>(),
                    "Cloth should get the scale binder that feeds FI/Sail Wind.");
                Assert.IsNull(root.GetComponent<SailWindObjectScaleBinder>(), "Root should stay clean.");
                Assert.IsNull(mast.GetComponent<SailWindObjectScaleBinder>(), "Mast should stay clean.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SailBindingIsIdempotent()
        {
            var root = new GameObject("sail_01");
            var cloth = new GameObject("sail_cloth");
            cloth.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Building/sail_01");
                AmbientWindMotionBinder.Apply(root, "Prefab/Building/sail_01");

                Assert.AreEqual(
                    1,
                    root.GetComponentsInChildren<SailWindObjectScaleBinder>(true).Length,
                    "Re-applying must not stack duplicate binders.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealWindmillPrefabsCarryAnAuthoredBladeRotator()
        {
            string[] prefabs = { "Prefab/Element/giantWindmill", "Prefab/Building/windmill_01" };
            foreach (string path in prefabs)
            {
                GameObject prefab = Resources.Load<GameObject>(path);
                Assert.IsNotNull(prefab, $"Missing Resources/{path}.prefab");

                WindmillBladeRotator rotator = prefab.GetComponentInChildren<WindmillBladeRotator>(true);
                Assert.IsNotNull(rotator, $"{path} should carry a blade rotator authored in the prefab.");
                Assert.AreNotSame(
                    prefab.transform,
                    rotator.transform,
                    $"{path}: the rotator belongs on the blade node, not the root.");
                Assert.Greater(rotator.Rpm, 0f, $"{path}: blades should turn.");
            }
        }

        [Test]
        public void RealSailClothUsesSailWindShader()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefab/Building/sail_01");
            Assert.IsNotNull(prefab, "Missing Resources/Prefab/Building/sail_01.prefab");

            Transform cloth = FindDeep(prefab.transform, "sail_cloth");
            Assert.IsNotNull(cloth, "sail_01 should expose a sail_cloth node.");

            var renderer = cloth.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "sail_cloth should have a renderer.");
            Assert.AreEqual(
                "FI/Sail Wind",
                renderer.sharedMaterial.shader.name,
                "The cloth is what the wind shader is supposed to displace.");
        }

        /// <summary>
        /// FI/Sail Wind takes its wind UV from uv2. Without that channel the mask evaluates to a
        /// constant zero and the cloth stays perfectly rigid — which looks like the shader simply
        /// not working, so guard the channel rather than the symptom.
        /// </summary>
        [Test]
        public void RealSailClothMeshHasUv2ForTheWindShader()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefab/Building/sail_01");
            Assert.IsNotNull(prefab, "Missing Resources/Prefab/Building/sail_01.prefab");

            Transform cloth = FindDeep(prefab.transform, "sail_cloth");
            Assert.IsNotNull(cloth, "sail_01 should expose a sail_cloth node.");

            Mesh mesh = cloth.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsNotNull(mesh, "sail_cloth should have a mesh.");
            Assert.AreEqual(
                mesh.vertexCount,
                mesh.uv2.Length,
                "sail_cloth needs a uv2 channel or FI/Sail Wind cannot displace it.");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }
    }
}
