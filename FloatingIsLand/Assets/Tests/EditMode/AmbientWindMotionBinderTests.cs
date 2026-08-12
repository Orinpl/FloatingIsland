using FloatingIsLand.View.Environment;
using NUnit.Framework;
using UnityEngine;

namespace FloatingIsLand.Tests
{
    public sealed class AmbientWindMotionBinderTests
    {
        [Test]
        public void WindmillMotionIsAddedOnlyToBladeNode()
        {
            var root = new GameObject("giantWindmill");
            var body = new GameObject("Body");
            var blade = new GameObject("BladePivot");
            body.transform.SetParent(root.transform, false);
            blade.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Element/giantWindmill");

                Assert.IsNull(root.GetComponent<WindmillBladeRotator>(), "Windmill root should not rotate.");
                Assert.IsNull(body.GetComponent<WindmillBladeRotator>(), "Non-blade nodes should not rotate.");
                Assert.IsNotNull(blade.GetComponent<WindmillBladeRotator>(), "Blade node should receive the rotator.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SailPrefabDoesNotReceiveTransformShakeComponent()
        {
            var root = new GameObject("sail_01");
            var cloth = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cloth.name = "SailCloth";
            cloth.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Building/sail_01");

                Assert.AreEqual(0, root.GetComponentsInChildren<MonoBehaviour>(true).Length);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RealWindmillPrefabReceivesBladeRotator()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefab/Element/giantWindmill");
            Assert.IsNotNull(prefab, "Missing Resources/Prefab/Element/giantWindmill.prefab");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                AmbientWindMotionBinder.Apply(instance, "Prefab/Element/giantWindmill");

                Assert.IsNotNull(
                    instance.GetComponentInChildren<WindmillBladeRotator>(true),
                    "Real windmill prefab should expose a blade-like node for rotation.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void RealSailPrefabStaysShaderDriven()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefab/Building/sail_01");
            Assert.IsNotNull(prefab, "Missing Resources/Prefab/Building/sail_01.prefab");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                AmbientWindMotionBinder.Apply(instance, "Prefab/Building/sail_01");

                Assert.AreEqual(
                    0,
                    instance.GetComponentsInChildren<MonoBehaviour>(true).Length,
                    "Sail wind motion should be shader-driven, not component-driven.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
