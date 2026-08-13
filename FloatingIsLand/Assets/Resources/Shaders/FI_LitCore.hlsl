#ifndef FI_LIT_CORE_INCLUDED
#define FI_LIT_CORE_INCLUDED

sampler2D _MainTex;
float4 _MainTex_ST;
fixed4 _Color;

sampler2D _BumpMap;
float _BumpScale;

fixed4 _ShadowColor;
fixed4 _Shadow2ndColor;
float _ShadowBorderRange;
float _ShadowBorderPosition;
float _Shadow2ndBorderRange;
float _Shadow2ndBorderPosition;
float _UseShadowRamp;
sampler2D _ShadowRampTex;
float _ShadowReceive;
float _ShadowEnvStrength;

fixed4 _SpecularColor;
float _SpecularPower;
float _SpecularIntensity;
float _SpecularSmoothness;
sampler2D _SpecularMask;

fixed4 _RimColor;
float _RimPower;
float _RimIntensity;
float _RimSmoothness;
float _RimLightDirOffset;
float4 _RimCustomOffset;
float _RimShadowMask;

sampler2D _MatCapTex;
fixed4 _MatCapColor;
float _MatCapIntensity;
float _MatCapBlendMode;

samplerCUBE _IBLCubemap;
fixed4 _IBLColor;
float _IBLIntensity;
float _IBLRoughness;
float _IBLMetallic;
float _IBLBlendMode;
float _IBLUseBuiltin;

sampler2D _EmissionMap;
fixed4 _EmissionColor;

fixed4 _OutlineColor;
float _OutlineWidth;
sampler2D _OutlineWidthMask;
float _OutlineColorMix;
float _OutlineZOffset;

float3 FI_SafeNormalize(float3 value)
{
    return normalize(value + 1e-5);
}

float3 FI_NormalWS(float2 uv, float3 normalWS, float3 tangentWS, float3 bitangentWS)
{
    normalWS = FI_SafeNormalize(normalWS);
#if defined(_NORMALMAP)
    float3 normalTS = UnpackNormal(tex2D(_BumpMap, uv));
    normalTS.xy *= _BumpScale;
    normalTS.z = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
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

float3 FI_BlendMode(float3 baseColor, float3 blendColor, float mode, float intensity)
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

fixed4 FI_ToonFragment(
    float2 uv,
    float3 positionWS,
    float3 normalWS,
    float3 tangentWS,
    float3 bitangentWS,
    float3 viewDirWS,
    float shadowAttenuation)
{
    fixed4 mainTex = tex2D(_MainTex, uv);
    fixed4 baseColor = mainTex * _Color;

    normalWS = FI_NormalWS(uv, normalWS, tangentWS, bitangentWS);
    viewDirWS = FI_SafeNormalize(viewDirWS);

    float3 lightDirWS = FI_SafeNormalize(_WorldSpaceLightPos0.xyz);
    float3 lightColor = _LightColor0.rgb;
    float halfLambert = dot(normalWS, lightDirWS) * 0.5 + 0.5;
    float receivedShadow = lerp(1.0, saturate(shadowAttenuation), saturate(_ShadowReceive));
    halfLambert *= receivedShadow;

    float shadow1 = FI_ShadowStep(halfLambert, _ShadowBorderPosition, _ShadowBorderRange);
    float shadow2 = FI_ShadowStep(halfLambert, _Shadow2ndBorderPosition, _Shadow2ndBorderRange);

#if defined(_USE_SHADOW_RAMP)
    fixed3 rampColor = tex2D(_ShadowRampTex, float2(saturate(halfLambert), 0.5)).rgb;
    baseColor.rgb *= rampColor;
#else
    fixed3 shadow2nd = lerp(_Shadow2ndColor.rgb * baseColor.rgb, baseColor.rgb, shadow2);
    fixed3 shadow1st = lerp(_ShadowColor.rgb * baseColor.rgb, baseColor.rgb, shadow1);
    baseColor.rgb = min(shadow1st, shadow2nd);
#endif

    fixed3 ambient = ShadeSH9(float4(normalWS, 1.0));
    baseColor.rgb += ambient * _ShadowEnvStrength * mainTex.rgb;
    baseColor.rgb *= lerp(fixed3(1, 1, 1), lightColor, 0.5);

#if defined(_SPECULAR)
    float specMask = tex2D(_SpecularMask, uv).r;
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
    float3 viewNormal = mul((float3x3)UNITY_MATRIX_V, normalWS);
    float2 matCapUV = viewNormal.xy * 0.5 + 0.5;
    fixed3 matCap = tex2D(_MatCapTex, matCapUV).rgb * _MatCapColor.rgb;
    baseColor.rgb = FI_BlendMode(baseColor.rgb, matCap, _MatCapBlendMode, _MatCapIntensity);
#endif

#if defined(_IBL)
    float3 reflectDir = reflect(-viewDirWS, normalWS);
    float mipLevel = saturate(_IBLRoughness) * 6.0;
    fixed3 ibl = fixed3(0, 0, 0);
    if (_IBLUseBuiltin > 0.5)
    {
        fixed4 encoded = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflectDir, mipLevel);
        ibl = DecodeHDR(encoded, unity_SpecCube0_HDR);
    }
    else
    {
        ibl = texCUBElod(_IBLCubemap, float4(reflectDir, mipLevel)).rgb;
    }
    ibl *= _IBLColor.rgb;
    ibl = lerp(ibl, ibl * mainTex.rgb * _Color.rgb, saturate(_IBLMetallic));
    baseColor.rgb = FI_BlendMode(baseColor.rgb, ibl, _IBLBlendMode, _IBLIntensity);
#endif

#if defined(_EMISSION)
    fixed4 emission = tex2D(_EmissionMap, uv);
    baseColor.rgb += emission.rgb * _EmissionColor.rgb;
#endif

    return fixed4(baseColor.rgb, baseColor.a);
}

#endif
