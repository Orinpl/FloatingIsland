#ifndef FI_LIT_CORE_INCLUDED
#define FI_LIT_CORE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/AmbientOcclusion.hlsl"

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
TEXTURE2D(_BumpMap);
SAMPLER(sampler_BumpMap);
TEXTURE2D(_ShadowRampTex);
SAMPLER(sampler_ShadowRampTex);
TEXTURE2D(_SpecularMask);
SAMPLER(sampler_SpecularMask);
TEXTURE2D(_MatCapTex);
SAMPLER(sampler_MatCapTex);
TEXTURECUBE(_IBLCubemap);
SAMPLER(sampler_IBLCubemap);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_EmissionMap);
TEXTURE2D(_OutlineWidthMask);
SAMPLER(sampler_OutlineWidthMask);

#if defined(FI_SAIL_WIND)
TEXTURE2D(_SailMaskTex);
SAMPLER(sampler_SailMaskTex);
#endif

CBUFFER_START(UnityPerMaterial)
float4 _MainTex_ST;
half4 _Color;

float _BumpScale;

half4 _ShadowColor;
half4 _Shadow2ndColor;
float _ShadowBorderRange;
float _ShadowBorderPosition;
float _Shadow2ndBorderRange;
float _Shadow2ndBorderPosition;
float _UseShadowRamp;
float _ShadowReceive;
float _ShadowEnvStrength;

half4 _SpecularColor;
float _SpecularPower;
float _SpecularIntensity;
float _SpecularSmoothness;

half4 _RimColor;
float _RimPower;
float _RimIntensity;
float _RimSmoothness;
float _RimLightDirOffset;
float4 _RimCustomOffset;
float _RimShadowMask;

half4 _MatCapColor;
float _MatCapIntensity;
float _MatCapBlendMode;

half4 _IBLColor;
float _IBLIntensity;
float _IBLRoughness;
float _IBLMetallic;
float _IBLBlendMode;
float _IBLUseBuiltin;

half4 _EmissionColor;

half4 _OutlineColor;
float _OutlineWidth;
float _OutlineColorMix;
float _OutlineZOffset;

#if defined(FI_SAIL_WIND)
float _SailSwayAmplitude;
float _SailWaveSpeed;
float _SailWaveScale;
float _SailClothAmplitude;
float _SailClothFrequency;
float _SailFlutterAmplitude;
float _SailFlutterSpeed;
float _SailFlutterScale;
float _SailWindPush;
float _SailNormalPush;
float _SailMaxDisplacement;
float4 _SailObjectScale;
float _SailMaskMode;
float _SailMaskInvert;
float _SailMaskStart;
float _SailMaskEnd;
float _SailMaskStrength;
#endif
CBUFFER_END

float3 FI_SafeNormalize(float3 value)
{
    return normalize(value + 1e-5);
}

float3 FI_NormalWS(float2 uv, float3 normalWS, float3 tangentWS, float3 bitangentWS)
{
    normalWS = FI_SafeNormalize(normalWS);
#if defined(_NORMALMAP)
    float3 normalTS = UnpackNormalScale(
        SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv),
        _BumpScale);
    float3x3 tbn = float3x3(
        FI_SafeNormalize(tangentWS),
        FI_SafeNormalize(bitangentWS),
        normalWS);
    normalWS = FI_SafeNormalize(mul(normalTS, tbn));
#endif
    return normalWS;
}

float FI_ShadowStep(float halfLambert, float position, float range)
{
    float border = 0.5 + position;
    return saturate((halfLambert - border + range * 0.5) / max(range, 0.001));
}

half3 FI_BlendMode(half3 baseColor, half3 blendColor, float mode, float intensity)
{
    blendColor *= intensity;
    if (mode < 0.5)
    {
        return baseColor + blendColor;
    }
    if (mode < 1.5)
    {
        return baseColor * (1.0 + blendColor);
    }
    if (mode < 2.5)
    {
        return baseColor + blendColor - baseColor * blendColor;
    }
    return lerp(baseColor, blendColor, saturate(intensity));
}

half4 FI_ToonFragment(
    float2 uv,
    float3 positionWS,
    float3 normalWS,
    float3 tangentWS,
    float3 bitangentWS,
    float3 viewDirWS,
    float4 shadowCoord,
    float4 positionCS)
{
    half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    half4 baseColor = mainTex * _Color;

    normalWS = FI_NormalWS(uv, normalWS, tangentWS, bitangentWS);
    viewDirWS = FI_SafeNormalize(viewDirWS);

    float2 normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    AmbientOcclusionFactor aoFactor = GetScreenSpaceAmbientOcclusion(normalizedScreenSpaceUV);

    Light mainLight = GetMainLight(shadowCoord);
    float3 lightDirWS = FI_SafeNormalize(mainLight.direction);
    half3 lightColor = mainLight.color * mainLight.distanceAttenuation * aoFactor.directAmbientOcclusion;
    float halfLambert = dot(normalWS, lightDirWS) * 0.5 + 0.5;
    float receivedShadow = lerp(1.0, saturate(mainLight.shadowAttenuation), saturate(_ShadowReceive));
    halfLambert *= receivedShadow;

    float shadow1 = FI_ShadowStep(halfLambert, _ShadowBorderPosition, _ShadowBorderRange);
    float shadow2 = FI_ShadowStep(halfLambert, _Shadow2ndBorderPosition, _Shadow2ndBorderRange);

#if defined(_USE_SHADOW_RAMP)
    half3 rampColor = SAMPLE_TEXTURE2D(
        _ShadowRampTex,
        sampler_ShadowRampTex,
        float2(saturate(halfLambert), 0.5)).rgb;
    baseColor.rgb *= rampColor;
#else
    half3 shadow2nd = lerp(_Shadow2ndColor.rgb * baseColor.rgb, baseColor.rgb, shadow2);
    half3 shadow1st = lerp(_ShadowColor.rgb * baseColor.rgb, baseColor.rgb, shadow1);
    baseColor.rgb = min(shadow1st, shadow2nd);
#endif

    half3 ambient = SampleSH(normalWS) * aoFactor.indirectAmbientOcclusion;
    baseColor.rgb += ambient * _ShadowEnvStrength * mainTex.rgb;
    baseColor.rgb *= lerp(half3(1, 1, 1), lightColor, 0.5);

#if defined(_SPECULAR)
    float specMask = SAMPLE_TEXTURE2D(_SpecularMask, sampler_SpecularMask, uv).r;
    float3 halfDir = FI_SafeNormalize(lightDirWS + viewDirWS);
    float specTerm = pow(saturate(dot(normalWS, halfDir)), _SpecularPower);
    specTerm = smoothstep(0.5 - _SpecularSmoothness, 0.5 + _SpecularSmoothness, specTerm);
    baseColor.rgb += specTerm * _SpecularColor.rgb * _SpecularIntensity * receivedShadow * specMask;
#endif

#if defined(_RIMLIGHT)
    float3 rimNormal = FI_SafeNormalize(normalWS + lightDirWS * _RimLightDirOffset + _RimCustomOffset.xyz);
    float rimRaw = pow(1.0 - saturate(dot(rimNormal, viewDirWS)), _RimPower);
    float rimTerm = smoothstep(0.5 - _RimSmoothness, 0.5 + _RimSmoothness, rimRaw);
    float rimMask = lerp(1.0, shadow1, _RimShadowMask);
    baseColor.rgb += rimTerm * _RimColor.rgb * _RimIntensity * rimMask;
#endif

#if defined(_MATCAP)
    float3 viewNormal = mul((float3x3)GetWorldToViewMatrix(), normalWS);
    float2 matCapUV = viewNormal.xy * 0.5 + 0.5;
    half3 matCap = SAMPLE_TEXTURE2D(_MatCapTex, sampler_MatCapTex, matCapUV).rgb * _MatCapColor.rgb;
    baseColor.rgb = FI_BlendMode(baseColor.rgb, matCap, _MatCapBlendMode, _MatCapIntensity);
#endif

#if defined(_IBL)
    float3 reflectDir = reflect(-viewDirWS, normalWS);
    half3 ibl;
    if (_IBLUseBuiltin > 0.5)
    {
        ibl = GlossyEnvironmentReflection(reflectDir, positionWS, saturate(_IBLRoughness), 1.0);
    }
    else
    {
        float mipLevel = saturate(_IBLRoughness) * 6.0;
        ibl = SAMPLE_TEXTURECUBE_LOD(_IBLCubemap, sampler_IBLCubemap, reflectDir, mipLevel).rgb;
    }
    ibl *= _IBLColor.rgb * aoFactor.indirectAmbientOcclusion;
    ibl = lerp(ibl, ibl * mainTex.rgb * _Color.rgb, saturate(_IBLMetallic));
    baseColor.rgb = FI_BlendMode(baseColor.rgb, ibl, _IBLBlendMode, _IBLIntensity);
#endif

#if defined(_EMISSION)
    half4 emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
    baseColor.rgb += emission.rgb * _EmissionColor.rgb;
#endif

    return half4(baseColor.rgb, baseColor.a);
}

#endif
