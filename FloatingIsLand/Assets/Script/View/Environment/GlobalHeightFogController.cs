using UnityEngine;
using UnityEngine.Rendering;

namespace FloatingIsLand.View.Environment
{
    /// <summary>
    /// Publishes height-fog settings from the active FI skybox to compatible shaders.
    /// The skybox material is the single source of truth, so scenes need no component.
    /// </summary>
    public static class GlobalHeightFogController
    {
        private const string SkyboxShaderName = "FI/Skybox Procedural";

        private static readonly int FogColor = Shader.PropertyToID("_FI_GlobalHeightFogColor");
        private static readonly int FogParams = Shader.PropertyToID("_FI_GlobalHeightFogParams");
        private static readonly int FogDistance = Shader.PropertyToID("_FI_GlobalHeightFogDistance");

        private static readonly int SourceEnabled = Shader.PropertyToID("_GlobalFogGroup");
        private static readonly int SourceColor = Shader.PropertyToID("_GlobalFogColor");
        private static readonly int SourceDensity = Shader.PropertyToID("_GlobalFogDensity");
        private static readonly int SourceBaseHeight = Shader.PropertyToID("_GlobalFogBaseHeight");
        private static readonly int SourceHeightFalloff = Shader.PropertyToID("_GlobalFogHeightFalloff");
        private static readonly int SourceStartDistance = Shader.PropertyToID("_GlobalFogStartDistance");
        private static readonly int SourceMaxOpacity = Shader.PropertyToID("_GlobalFogMaxOpacity");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            PublishFromSkybox();
        }

        private static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            PublishFromSkybox();
        }

        public static void PublishFromSkybox()
        {
            Material skybox = RenderSettings.skybox;
            if (skybox == null || skybox.shader == null ||
                skybox.shader.name != SkyboxShaderName || !skybox.HasProperty(SourceEnabled))
            {
                Disable();
                return;
            }

            float enabled = skybox.GetFloat(SourceEnabled) > 0.5f ? 1f : 0f;
            float density = Mathf.Max(0f, skybox.GetFloat(SourceDensity));
            float baseHeight = skybox.GetFloat(SourceBaseHeight);
            float heightFalloff = Mathf.Max(0.01f, skybox.GetFloat(SourceHeightFalloff));
            float startDistance = Mathf.Max(0f, skybox.GetFloat(SourceStartDistance));
            float maxOpacity = Mathf.Clamp01(skybox.GetFloat(SourceMaxOpacity));

            Shader.SetGlobalColor(FogColor, skybox.GetColor(SourceColor));
            Shader.SetGlobalVector(FogParams, new Vector4(enabled, density, baseHeight, heightFalloff));
            Shader.SetGlobalVector(FogDistance, new Vector4(startDistance, maxOpacity, 0f, 0f));
        }

        private static void Disable()
        {
            Shader.SetGlobalVector(FogParams, Vector4.zero);
            Shader.SetGlobalVector(FogDistance, Vector4.zero);
        }
    }
}
