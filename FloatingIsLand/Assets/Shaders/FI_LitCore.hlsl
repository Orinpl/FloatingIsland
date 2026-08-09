#ifndef FI_LIT_CORE_INCLUDED
#define FI_LIT_CORE_INCLUDED

sampler2D _MainTex;
float4 _MainTex_ST;

float4 _BaseColor;
float4 _TopColor;
float4 _ShadowColor;
float4 _LightDirection;
float _Ambient;
float _LightSteps;
float _TopTint;

float4 _HighlightColor;
float _HighlightStrength;
float _HighlightSize;
float _BevelHighlight;
float _BevelSharpness;
float _BevelWidth;
float _RimStrength;
float _RimPower;

float3 FI_SafeNormalize(float3 value)
{
    return normalize(value + 1e-5);
}

float3 FI_FlatNormal(float3 worldPos, float3 smoothNormal)
{
    float3 flatNormal = FI_SafeNormalize(cross(ddy(worldPos), ddx(worldPos)));
    return dot(flatNormal, smoothNormal) < 0.0 ? -flatNormal : flatNormal;
}

float FI_SteppedLight(float3 normalWS, float3 lightDirWS)
{
    float lightAmount = saturate(dot(normalWS, lightDirWS));
    float lightSteps = max(1.0, _LightSteps);
    lightAmount = floor(lightAmount * lightSteps) / lightSteps;
    return max(_Ambient, lightAmount);
}

float3 FI_BaseRamp(float3 baseColor, float3 flatNormal, float lightAmount)
{
    float upward = saturate(flatNormal.y * 0.5 + 0.5);
    float3 rampedColor = baseColor * lerp(_ShadowColor.rgb, _TopColor.rgb, lightAmount);
    return lerp(rampedColor, rampedColor * _TopColor.rgb, upward * _TopTint);
}

float FI_FaceHighlight(float3 flatNormal, float3 halfDir)
{
    return pow(saturate(dot(flatNormal, halfDir)), _HighlightSize) * _HighlightStrength;
}

float FI_BevelHighlight(float3 smoothNormal, float3 flatNormal, float3 halfDir)
{
    float normalDelta = saturate(length(smoothNormal - flatNormal) * _BevelWidth);
    float3 bevelNormal = FI_SafeNormalize(smoothNormal + flatNormal);
    float facing = pow(saturate(dot(bevelNormal, halfDir)), _BevelSharpness);
    return normalDelta * facing * _BevelHighlight;
}

float FI_RimHighlight(float3 normalWS, float3 viewDirWS)
{
    float rim = 1.0 - saturate(dot(normalWS, viewDirWS));
    return pow(rim, _RimPower) * _RimStrength;
}

fixed4 FI_LitFragment(float2 uv, float3 worldPos, float3 worldNormal)
{
    float3 smoothNormal = FI_SafeNormalize(worldNormal);
    float3 flatNormal = FI_FlatNormal(worldPos, smoothNormal);
    float3 lightDir = FI_SafeNormalize(_LightDirection.xyz);
    float3 viewDir = FI_SafeNormalize(_WorldSpaceCameraPos.xyz - worldPos);
    float3 halfDir = FI_SafeNormalize(lightDir + viewDir);

    fixed4 texColor = tex2D(_MainTex, uv);
    float3 baseColor = texColor.rgb * _BaseColor.rgb;
    float lightAmount = FI_SteppedLight(flatNormal, lightDir);
    float3 color = FI_BaseRamp(baseColor, flatNormal, lightAmount);

    float highlight = FI_FaceHighlight(flatNormal, halfDir);
    highlight += FI_BevelHighlight(smoothNormal, flatNormal, halfDir);
    highlight += FI_RimHighlight(flatNormal, viewDir);

    color += _HighlightColor.rgb * highlight;
    return fixed4(color, texColor.a * _BaseColor.a);
}

#endif
