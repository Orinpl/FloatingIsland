using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FloatingIsLand.EditorTools
{
    /// <summary>
    /// 把 L_Lighting 里调好的天空盒 / 环境光 / 主光 / 后处理搬到局内场景 Main。
    ///
    /// 为什么是「同步」而不是把数值写死在这里：L_Lighting 是美术调光用的参考场景，
    /// 它本来就该是唯一事实源。把参数抄进脚本，等于开了第二份真相，改一处忘一处就漂移。
    /// 这里读的是场景本身，所以在 L_Lighting 里怎么调好，跑一次菜单 Main 就是什么样。
    ///
    /// 天空盒与环境光存在 RenderSettings 上，是**逐场景**的资产，不随管线资产走 ——
    /// 也就是说远端把 URP 管线和 FI_Skybox 接进来之后，Main 并不会自动用上，
    /// 它一直还挂着 Unity 内置的 Default-Skybox。这就是本工具要补的那一步。
    /// </summary>
    public static class FI_LightingSync
    {
        private const string SourceScene = "Assets/Scenes/L_Lighting.unity";
        private const string TargetScene = "Assets/Scenes/Main.unity";
        private const string VolumeObjectName = "PostVolume";
        private const string ReportPath = "Temp/fi_lighting_sync.txt";

        /// <summary>
        /// 从参考场景抓下来的一整套光照，跨场景搬运用。
        ///
        /// 引用到的资产一律存**路径**而不是对象引用。切场景时 Unity 会卸载没人引用的资产，
        /// 手里那个 C# 引用随即变成「假 null」——等号右边看着还在，赋过去就是空。
        /// 实测就是这么丢掉过一次 VolumeProfile：抓取日志明明打出了 profile 名字，
        /// 写入时却成了「无」。路径是纯字符串，不受资产生命周期影响。
        /// </summary>
        private struct LightingSnapshot
        {
            public string SkyboxPath;
            public AmbientMode AmbientMode;
            public Color AmbientSky;
            public Color AmbientEquator;
            public Color AmbientGround;
            public float AmbientIntensity;
            public bool Fog;
            public Color FogColor;
            public FogMode FogMode;
            public float FogDensity;
            public DefaultReflectionMode ReflectionMode;
            public float ReflectionIntensity;

            public bool HasLight;
            public Color LightColor;
            public float LightIntensity;
            public Quaternion LightRotation;
            public LightShadows Shadows;
            public float ShadowStrength;
            public float ShadowBias;
            public float ShadowNormalBias;
            public float ShadowNearPlane;
            public float BounceIntensity;
            public SoftShadowQuality SoftShadowQuality;
            public int ShadowResolutionTier;
            public bool UsePipelineSettings;

            public bool HasVolume;
            public string ProfilePath;
            public bool VolumeIsGlobal;
            public float VolumePriority;
            public float VolumeWeight;
            public float VolumeBlendDistance;
        }

        [MenuItem("Tools/美术/把 L_Lighting 的光照同步到 Main 场景", false, 20)]
        public static void Sync()
        {
            // 脚本调 OpenScene 不会弹「是否保存」，它是直接丢弃改动的。
            // 所以脏场景必须自己挡住，否则这个菜单会安静地吃掉别人没保存的工作。
            Scene active = EditorSceneManager.GetActiveScene();
            if (active.isDirty)
            {
                EditorUtility.DisplayDialog(
                    "同步光照",
                    $"当前场景 {active.name} 有未保存的改动。\n" +
                    "本工具要切换场景，脚本切场景不会提示保存、改动会直接丢失。\n" +
                    "请先保存或放弃改动再跑。",
                    "知道了");
                return;
            }

            string originalScene = active.path;
            var log = new StringBuilder();

            LightingSnapshot snapshot = Capture(log);
            Apply(snapshot, log);

            // 回到原来那个场景：这个菜单不该顺手改掉别人正在看的东西。
            if (!string.IsNullOrEmpty(originalScene) && originalScene != TargetScene)
            {
                EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
                log.AppendLine($"已切回原场景 {originalScene}");
            }

            File.WriteAllText(ReportPath, log.ToString());
            Debug.Log($"[光照同步] 写入 {ReportPath}\n{log}");
        }

        private static LightingSnapshot Capture(StringBuilder log)
        {
            EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);

            var snapshot = new LightingSnapshot
            {
                SkyboxPath = AssetDatabase.GetAssetPath(RenderSettings.skybox),
                AmbientMode = RenderSettings.ambientMode,
                AmbientSky = RenderSettings.ambientSkyColor,
                AmbientEquator = RenderSettings.ambientEquatorColor,
                AmbientGround = RenderSettings.ambientGroundColor,
                AmbientIntensity = RenderSettings.ambientIntensity,
                Fog = RenderSettings.fog,
                FogColor = RenderSettings.fogColor,
                FogMode = RenderSettings.fogMode,
                FogDensity = RenderSettings.fogDensity,
                ReflectionMode = RenderSettings.defaultReflectionMode,
                ReflectionIntensity = RenderSettings.reflectionIntensity,
            };

            log.AppendLine($"读取 {SourceScene}");
            log.AppendLine($"  天空盒 = {Describe(snapshot.SkyboxPath)}");
            log.AppendLine($"  环境光模式 = {snapshot.AmbientMode}，雾 = {snapshot.Fog}");

            Light sun = FindDirectionalLight();
            if (sun != null)
            {
                snapshot.HasLight = true;
                snapshot.LightColor = sun.color;
                snapshot.LightIntensity = sun.intensity;
                snapshot.LightRotation = sun.transform.rotation;
                snapshot.Shadows = sun.shadows;
                snapshot.ShadowStrength = sun.shadowStrength;
                snapshot.ShadowBias = sun.shadowBias;
                snapshot.ShadowNormalBias = sun.shadowNormalBias;
                snapshot.ShadowNearPlane = sun.shadowNearPlane;
                snapshot.BounceIntensity = sun.bounceIntensity;

                var extra = sun.GetComponent<UniversalAdditionalLightData>();
                if (extra != null)
                {
                    snapshot.SoftShadowQuality = extra.softShadowQuality;
                    snapshot.ShadowResolutionTier = extra.additionalLightsShadowResolutionTier;
                    snapshot.UsePipelineSettings = extra.usePipelineSettings;
                }

                log.AppendLine(
                    $"  主光 {sun.name}：色 {ColorUtility.ToHtmlStringRGB(sun.color)}，" +
                    $"强度 {sun.intensity}，阴影 {sun.shadows}，" +
                    $"欧拉 {sun.transform.rotation.eulerAngles}");
            }
            else
            {
                log.AppendLine("  [warn] 参考场景里没有方向光");
            }

            Volume volume = Object.FindObjectOfType<Volume>();
            if (volume != null)
            {
                snapshot.HasVolume = true;
                snapshot.ProfilePath = AssetDatabase.GetAssetPath(volume.sharedProfile);
                snapshot.VolumeIsGlobal = volume.isGlobal;
                snapshot.VolumePriority = volume.priority;
                snapshot.VolumeWeight = volume.weight;
                snapshot.VolumeBlendDistance = volume.blendDistance;
                log.AppendLine(
                    $"  后处理 Volume：profile = {Describe(snapshot.ProfilePath)}，" +
                    $"global = {volume.isGlobal}");
            }
            else
            {
                log.AppendLine("  [warn] 参考场景里没有 Volume");
            }

            return snapshot;
        }

        private static void Apply(LightingSnapshot snapshot, StringBuilder log)
        {
            Scene target = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            log.AppendLine($"写入 {TargetScene}");

            // 资产在这里才加载：目标场景已经打开，引用不会再被卸载掉。
            var skybox = LoadIfAny<Material>(snapshot.SkyboxPath);

            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = snapshot.AmbientMode;
            RenderSettings.ambientSkyColor = snapshot.AmbientSky;
            RenderSettings.ambientEquatorColor = snapshot.AmbientEquator;
            RenderSettings.ambientGroundColor = snapshot.AmbientGround;
            RenderSettings.ambientIntensity = snapshot.AmbientIntensity;
            RenderSettings.fog = snapshot.Fog;
            RenderSettings.fogColor = snapshot.FogColor;
            RenderSettings.fogMode = snapshot.FogMode;
            RenderSettings.fogDensity = snapshot.FogDensity;
            RenderSettings.defaultReflectionMode = snapshot.ReflectionMode;
            RenderSettings.reflectionIntensity = snapshot.ReflectionIntensity;
            log.AppendLine($"  天空盒 -> {Describe(snapshot.SkyboxPath)}");

            if (snapshot.HasLight)
            {
                Light sun = FindDirectionalLight();
                if (sun == null)
                {
                    var go = new GameObject("Directional Light");
                    sun = go.AddComponent<Light>();
                    sun.type = LightType.Directional;
                    log.AppendLine("  Main 里没有方向光，已新建一盏");
                }

                sun.color = snapshot.LightColor;
                sun.intensity = snapshot.LightIntensity;
                // 只搬朝向不搬位置：方向光的位置对渲染没有意义，
                // 而 Main 里那盏灯的位置可能是别人摆场景时对齐过的。
                sun.transform.rotation = snapshot.LightRotation;
                sun.shadows = snapshot.Shadows;
                sun.shadowStrength = snapshot.ShadowStrength;
                sun.shadowBias = snapshot.ShadowBias;
                sun.shadowNormalBias = snapshot.ShadowNormalBias;
                sun.shadowNearPlane = snapshot.ShadowNearPlane;
                sun.bounceIntensity = snapshot.BounceIntensity;

                var extra = sun.GetComponent<UniversalAdditionalLightData>();
                if (extra == null)
                {
                    extra = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
                }

                extra.softShadowQuality = snapshot.SoftShadowQuality;
                extra.usePipelineSettings = snapshot.UsePipelineSettings;

                // additionalLightsShadowResolutionTier 只有 getter，URP 把它当成 Inspector
                // 驱动的序列化字段，只能从序列化层写。
                var extraSerialized = new SerializedObject(extra);
                SerializedProperty tier =
                    extraSerialized.FindProperty("m_AdditionalLightsShadowResolutionTier");
                if (tier != null)
                {
                    tier.intValue = snapshot.ShadowResolutionTier;
                    extraSerialized.ApplyModifiedPropertiesWithoutUndo();
                }

                // RenderSettings.sun 决定天空盒着色器拿到的太阳方向。
                // 不设它，程序化天空盒的太阳会和实际投影方向对不上。
                RenderSettings.sun = sun;

                EditorUtility.SetDirty(sun);
                log.AppendLine(
                    $"  主光 -> 色 {ColorUtility.ToHtmlStringRGB(sun.color)}，阴影 {sun.shadows}，" +
                    $"欧拉 {sun.transform.rotation.eulerAngles}");
            }

            if (snapshot.HasVolume)
            {
                Volume volume = Object.FindObjectOfType<Volume>();
                if (volume == null)
                {
                    var go = new GameObject(VolumeObjectName);
                    volume = go.AddComponent<Volume>();
                    log.AppendLine($"  新建 {VolumeObjectName}");
                }

                volume.sharedProfile = LoadIfAny<VolumeProfile>(snapshot.ProfilePath);
                volume.isGlobal = snapshot.VolumeIsGlobal;
                volume.priority = snapshot.VolumePriority;
                volume.weight = snapshot.VolumeWeight;
                volume.blendDistance = snapshot.VolumeBlendDistance;
                EditorUtility.SetDirty(volume);
                log.AppendLine($"  后处理 -> profile {Describe(snapshot.ProfilePath)}");

                if (volume.sharedProfile == null && !string.IsNullOrEmpty(snapshot.ProfilePath))
                {
                    log.AppendLine($"  [err] profile 加载失败：{snapshot.ProfilePath}");
                }

                EnsureCameraRendersPostProcessing(log);
            }

            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
            log.AppendLine("  已保存");
        }

        /// <summary>
        /// 让相机真的去跑后处理。这一项不是从参考场景抄的 —— L_Lighting 里根本没有相机，
        /// 它只是一间调光用的暗房。
        ///
        /// 而 URP 里 Volume 本身不产生任何画面：要相机上的 UniversalAdditionalCameraData
        /// 开了 renderPostProcessing 才会去采样 Volume 栈。这个组件缺席时 URP 会在运行时
        /// 补一个默认值的，而默认值是**关**。也就是说场景里摆好了 Volume、挂好了 profile、
        /// Inspector 看着一切正常，画面却一点变化都没有 —— 没有任何一处会报错。
        /// </summary>
        private static void EnsureCameraRendersPostProcessing(StringBuilder log)
        {
            Camera camera = Camera.main ?? Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                log.AppendLine("  [warn] 目标场景里没有相机，后处理无处生效");
                return;
            }

            if (camera.clearFlags != CameraClearFlags.Skybox)
            {
                log.AppendLine(
                    $"  [warn] 相机 {camera.name} 的 Clear Flags 是 {camera.clearFlags}，" +
                    "不是 Skybox，天空盒不会出现在画面里");
            }

            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null)
            {
                data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                log.AppendLine($"  相机 {camera.name} 补上 UniversalAdditionalCameraData");
            }

            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
            log.AppendLine($"  相机 {camera.name}：后处理已开启");
        }

        private static T LoadIfAny<T>(string assetPath) where T : Object
        {
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<T>(assetPath);
        }

        private static string Describe(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? "无" : Path.GetFileNameWithoutExtension(assetPath);
        }

        /// <summary>场景里的方向光；有多盏时取第一盏（本工程只该有一盏主光）。</summary>
        private static Light FindDirectionalLight()
        {
            foreach (Light light in Object.FindObjectsOfType<Light>(true))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }

            return null;
        }
    }
}
