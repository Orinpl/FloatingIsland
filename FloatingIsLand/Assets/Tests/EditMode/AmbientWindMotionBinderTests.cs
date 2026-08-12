using FloatingIsLand.View.Environment;
using NUnit.Framework;
using UnityEngine;

namespace FloatingIsLand.Tests
{
    public sealed class AmbientWindMotionBinderTests
    {
        [Test]
        public void 风车动态_只挂到扇叶节点()
        {
            var root = new GameObject("giantWindmill");
            var body = new GameObject("Body");
            var blade = new GameObject("BladePivot");
            body.transform.SetParent(root.transform, false);
            blade.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Element/giantWindmill");

                Assert.IsNull(root.GetComponent<WindmillBladeRotator>(), "风车根节点不该自己旋转");
                Assert.IsNull(body.GetComponent<WindmillBladeRotator>(), "非扇叶节点不该被挂旋转组件");
                Assert.IsNotNull(blade.GetComponent<WindmillBladeRotator>(), "扇叶节点应当被挂上旋转组件");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 风帆动态_跳过根节点名字命中()
        {
            var root = new GameObject("sail_01");
            var cloth = GameObject.CreatePrimitive(PrimitiveType.Quad);
            cloth.name = "SailCloth";
            cloth.transform.SetParent(root.transform, false);

            try
            {
                AmbientWindMotionBinder.Apply(root, "Prefab/Building/sail_01");

                Assert.IsNull(root.GetComponent<SailWindShake>(), "Prefab 根节点叫 sail_01，但不该让整栋建筑抖动");
                Assert.IsNotNull(cloth.GetComponent<SailWindShake>(), "风帆布面节点应当被挂上抖动组件");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void 真实风车Prefab_实例化后能挂上扇叶旋转()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefab/Element/giantWindmill");
            Assert.IsNotNull(prefab, "缺少 Resources/Prefab/Element/giantWindmill.prefab");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                AmbientWindMotionBinder.Apply(instance, "Prefab/Element/giantWindmill");

                Assert.IsNotNull(
                    instance.GetComponentInChildren<WindmillBladeRotator>(true),
                    "真实风车 Prefab 没有找到可挂载的扇叶节点，请检查模型子节点命名");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void 真实风帆Prefab_实例化后能挂上布面抖动()
        {
            GameObject prefab = Resources.Load<GameObject>("Prefab/Building/sail_01");
            Assert.IsNotNull(prefab, "缺少 Resources/Prefab/Building/sail_01.prefab");

            GameObject instance = Object.Instantiate(prefab);
            try
            {
                AmbientWindMotionBinder.Apply(instance, "Prefab/Building/sail_01");

                Assert.IsNotNull(
                    instance.GetComponentInChildren<SailWindShake>(true),
                    "真实风帆 Prefab 没有找到可挂载的布面节点");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
