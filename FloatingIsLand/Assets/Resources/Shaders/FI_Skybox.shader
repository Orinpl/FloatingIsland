Shader "FI/Skybox Procedural"
{
    Properties
    {
        _SkyTopColor ("Sky Top", Color) = (0.16, 0.58, 0.92, 1)
        _SkyHorizonColor ("Sky Horizon", Color) = (0.55, 0.90, 0.94, 1)
        _SkyGradientPower ("Sky Gradient", Range(0.1, 4)) = 0.8
        _Exposure ("Exposure", Range(0, 4)) = 1
        _Saturation ("Saturation", Range(0, 2)) = 1.1
        _Contrast ("Contrast", Range(0, 2)) = 1

        _HorizonLevel ("Horizon Level", Range(-0.15, 0.15)) = 0
        _HorizonWidth ("Horizon Haze", Range(0.001, 0.08)) = 0.015
        [HDR] _HorizonGlowColor ("Horizon Glow", Color) = (1.0, 0.86, 0.62, 1)
        _HorizonGlowStrength ("Horizon Glow Strength", Range(0, 1)) = 0.18
        _OceanHorizonColor ("Ocean Horizon", Color) = (0.18, 0.76, 0.78, 1)
        _OceanDeepColor ("Ocean Deep", Color) = (0.02, 0.42, 0.62, 1)
        _OceanGradientPower ("Ocean Gradient", Range(0.1, 4)) = 0.75
        _OceanVariation ("Ocean Variation", Range(0, 0.15)) = 0.025

        _CloudGroup ("Clouds", Float) = 1
        [HDR] _CloudColor ("Cloud Color", Color) = (1, 0.95, 0.82, 1)
        _CloudShadowColor ("Cloud Shadow", Color) = (0.56, 0.72, 0.80, 1)
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.58
        _CloudScale ("Cloud Scale", Range(1, 12)) = 5
        _CloudSoftness ("Cloud Softness", Range(0.01, 0.3)) = 0.09
        _CloudAltitude ("Cloud Altitude", Range(0.02, 0.4)) = 0.15
        _CloudSpread ("Cloud Spread", Range(0.03, 0.35)) = 0.18
        _CloudSpeed ("Cloud Speed", Range(-0.1, 0.1)) = 0.008
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.8
        _CloudFadeStrength ("Cloud Fade Strength", Range(0, 1)) = 0.65
        _CloudFadeSpeed ("Cloud Fade Speed", Range(0, 3)) = 0.35

        _IslandGroup ("Islands", Float) = 1
        _IslandCount ("Island Count", Range(0, 8)) = 8
        _IslandScale ("Island Scale", Range(0.4, 2)) = 1
        _IslandOpacity ("Island Opacity", Range(0, 1)) = 0.95
        _IslandDepth ("Island Depth", Range(0.05, 0.3)) = 0.16
        _GrassColor ("Grass", Color) = (0.25, 0.72, 0.18, 1)
        _GrassLightColor ("Grass Light", Color) = (0.58, 0.86, 0.20, 1)
        _SandColor ("Sand", Color) = (0.94, 0.74, 0.33, 1)
        _RockColor ("Rock", Color) = (0.40, 0.34, 0.28, 1)
        _ShallowWaterColor ("Shallow Water", Color) = (0.25, 0.88, 0.78, 1)

        _WindGroup ("Wind Bands", Float) = 1
        [HDR] _WindColor ("Wind Color", Color) = (1, 1, 1, 1)
        _WindIntensity ("Wind Intensity", Range(0, 1)) = 0.32
        _WindCount ("Wind Stroke Count", Range(1, 8)) = 4
        _WindPositionX ("Wind Horizontal Position", Range(0, 1)) = 0
        _WindPositionY ("Wind Vertical Position", Range(0.03, 0.4)) = 0.14
        _WindVerticalSpread ("Wind Vertical Spread", Range(0, 0.18)) = 0.1
        _WindWidth ("Wind Width", Range(0.0005, 0.015)) = 0.004
        _WindLength ("Wind Stroke Length", Range(0.03, 0.3)) = 0.13
        _WindTaper ("Wind Stroke Taper", Range(0.1, 1)) = 0.65
        _WindBreakup ("Wind Stroke Breakup", Range(0, 1)) = 0.35
        _WindCurvature ("Wind Snake Amplitude", Range(0, 0.08)) = 0.025
        _WindWaveCount ("Wind Snake Waves", Range(0.5, 4)) = 1.6
        _WindWaveSpeed ("Wind Snake Speed", Range(-5, 5)) = 1.4
        _WindFadeDuration ("Wind Fade Duration", Range(0.02, 0.45)) = 0.18
        _WindSpeed ("Wind Forward Speed", Range(-0.1, 0.1)) = 0.015

        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Background"
            "RenderType" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Skybox"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "Skybox"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FISkyboxVertex
            #pragma fragment FISkyboxFragment
            #pragma shader_feature_local_fragment _CLOUDS
            #pragma shader_feature_local_fragment _ISLANDS
            #pragma shader_feature_local_fragment _WINDBANDS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            half4 _SkyTopColor;
            half4 _SkyHorizonColor;
            half4 _HorizonGlowColor;
            half4 _OceanHorizonColor;
            half4 _OceanDeepColor;
            half4 _CloudColor;
            half4 _CloudShadowColor;
            half4 _GrassColor;
            half4 _GrassLightColor;
            half4 _SandColor;
            half4 _RockColor;
            half4 _ShallowWaterColor;
            half4 _WindColor;
            float _SkyGradientPower;
            float _Exposure;
            float _Saturation;
            float _Contrast;
            float _HorizonLevel;
            float _HorizonWidth;
            float _HorizonGlowStrength;
            float _OceanGradientPower;
            float _OceanVariation;
            float _CloudCoverage;
            float _CloudScale;
            float _CloudSoftness;
            float _CloudAltitude;
            float _CloudSpread;
            float _CloudSpeed;
            float _CloudOpacity;
            float _CloudFadeStrength;
            float _CloudFadeSpeed;
            float _IslandCount;
            float _IslandScale;
            float _IslandOpacity;
            float _IslandDepth;
            float _WindIntensity;
            float _WindCount;
            float _WindPositionX;
            float _WindPositionY;
            float _WindVerticalSpread;
            float _WindWidth;
            float _WindLength;
            float _WindTaper;
            float _WindBreakup;
            float _WindCurvature;
            float _WindWaveCount;
            float _WindWaveSpeed;
            float _WindFadeDuration;
            float _WindSpeed;
            float _Rotation;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
            };

            float3 FI_RotateAroundY(float3 direction, float degrees)
            {
                float sine;
                float cosine;
                sincos(radians(degrees), sine, cosine);
                return float3(
                    direction.x * cosine - direction.z * sine,
                    direction.y,
                    direction.x * sine + direction.z * cosine);
            }

            float FI_Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            float FI_Noise3(float3 value)
            {
                float3 cell = floor(value);
                float3 fraction = frac(value);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);

                float n000 = FI_Hash31(cell + float3(0, 0, 0));
                float n100 = FI_Hash31(cell + float3(1, 0, 0));
                float n010 = FI_Hash31(cell + float3(0, 1, 0));
                float n110 = FI_Hash31(cell + float3(1, 1, 0));
                float n001 = FI_Hash31(cell + float3(0, 0, 1));
                float n101 = FI_Hash31(cell + float3(1, 0, 1));
                float n011 = FI_Hash31(cell + float3(0, 1, 1));
                float n111 = FI_Hash31(cell + float3(1, 1, 1));

                float bottom = lerp(lerp(n000, n100, fraction.x), lerp(n010, n110, fraction.x), fraction.y);
                float top = lerp(lerp(n001, n101, fraction.x), lerp(n011, n111, fraction.x), fraction.y);
                return lerp(bottom, top, fraction.z);
            }

            float FI_Fbm(float3 value)
            {
                float result = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int index = 0; index < 4; index++)
                {
                    result += FI_Noise3(value) * amplitude;
                    value = value * 2.03 + 7.17;
                    amplitude *= 0.5;
                }
                return result;
            }

            float FI_WrappedDelta(float value, float center)
            {
                return frac(value - center + 0.5) - 0.5;
            }

            void FI_DrawIsland(
                inout half3 color,
                float2 panoramaUV,
                float2 center,
                float2 size,
                float phase,
                float enabled)
            {
                float2 offset = float2(FI_WrappedDelta(panoramaUV.x, center.x), panoramaUV.y - center.y);
                float2 shapePosition = offset / max(size * _IslandScale, 0.0001);
                float angle = atan2(shapePosition.y, shapePosition.x);
                float shapeNoise = sin(angle * 3.0 + phase) * 0.07 + sin(angle * 5.0 - phase) * 0.035;
                float distanceToIsland = length(shapePosition) + shapeNoise;

                float shallow = (1.0 - smoothstep(1.02, 1.32, distanceToIsland)) * smoothstep(0.82, 1.02, distanceToIsland);
                float sand = 1.0 - smoothstep(0.88, 1.03, distanceToIsland);
                float grass = 1.0 - smoothstep(0.64, 0.88, distanceToIsland);

                float detail = FI_Noise3(float3(shapePosition * 3.1, phase));
                float rock = grass * smoothstep(0.66, 0.83, detail) * smoothstep(-0.8, 0.45, shapePosition.y);
                float grassLight = grass * smoothstep(0.35, 0.72, FI_Noise3(float3(shapePosition * 2.2, phase + 4.0)));

                half3 islandColor = lerp(_SandColor.rgb, _GrassColor.rgb, grass);
                islandColor = lerp(islandColor, _GrassLightColor.rgb, grassLight * 0.55);
                islandColor = lerp(islandColor, _RockColor.rgb, rock * 0.75);

                float distanceFade = saturate((0.5 + _HorizonLevel - center.y) / max(_IslandDepth, 0.001));
                float opacity = enabled * _IslandOpacity * lerp(0.5, 1.0, distanceFade);
                color = lerp(color, _ShallowWaterColor.rgb, shallow * opacity * 0.55);
                color = lerp(color, islandColor, sand * opacity);
            }

            float FI_WindTrailStroke(
                float azimuth,
                float latitude,
                float headPosition,
                float height,
                float lengthScale,
                float widthScale,
                float seed)
            {
                float directionSign = _WindSpeed < 0.0 ? -1.0 : 1.0;
                float travelCycle = frac(headPosition);
                float fadeDuration = min(_WindFadeDuration, 0.49);
                float lifecycleFade = smoothstep(0.0, fadeDuration, travelCycle) *
                    (1.0 - smoothstep(1.0 - fadeDuration, 1.0, travelCycle));
                float wrappedHeadPosition = frac(headPosition);
                float trailLength = max(_WindLength * lengthScale, 0.001);
                float distanceBehindHead = frac((wrappedHeadPosition - azimuth) * directionSign + 1.0);
                float trailPosition = distanceBehindHead / trailLength;
                float trailMask = 1.0 - smoothstep(0.94, 1.0, trailPosition);
                float headToTail = saturate(1.0 - trailPosition);

                float tailTaper = smoothstep(0.0, max(0.08, _WindTaper), headToTail);
                float headCap = 1.0 - smoothstep(0.92, 1.0, headToTail);
                float brushProfile = max(tailTaper * headCap, 0.035);
                float trailOpacity = pow(headToTail, 0.72);

                float snakePhase = _Time.y * _WindWaveSpeed * directionSign -
                    trailPosition * _WindWaveCount * 6.2831853 + seed;
                float snakePrimary = sin(snakePhase);
                float snakeSecondary = sin(snakePhase * 1.87 + seed * 0.73) * 0.22;
                float headMotion = smoothstep(0.0, 0.16, headToTail);
                float brushLine = height + (snakePrimary + snakeSecondary) * _WindCurvature * headMotion;
                float edgeNoise = FI_Noise3(float3(
                    azimuth * 180.0 + seed * 11.0,
                    latitude * 260.0,
                    seed + trailPosition * 5.0));
                float raggedWidth = lerp(1.0, lerp(0.62, 1.18, edgeNoise), _WindBreakup);
                float width = max(_WindWidth * widthScale * brushProfile * raggedWidth, 0.00015);
                float distanceToLine = abs(latitude - brushLine);
                float body = 1.0 - smoothstep(width * 0.42, width, distanceToLine);

                float bristleLine = abs(latitude - (brushLine - width * 1.45));
                float bristle = 1.0 - smoothstep(width * 0.10, width * 0.34, bristleLine);
                bristle *= smoothstep(0.12, 0.40, headToTail) * (1.0 - smoothstep(0.72, 0.96, headToTail));
                bristle *= lerp(0.15, 0.6, edgeNoise) * _WindBreakup;

                return saturate(body + bristle) * trailMask * trailOpacity * lifecycleFade;
            }

            Varyings FISkyboxVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = FI_RotateAroundY(input.positionOS.xyz, _Rotation);
                return output;
            }

            half4 FISkyboxFragment(Varyings input) : SV_Target
            {
                const float inversePi = 0.31830988618;
                float3 direction = normalize(input.direction);
                float latitude = asin(clamp(direction.y, -1.0, 1.0)) * inversePi;
                float azimuth = atan2(direction.x, direction.z) * (0.5 * inversePi) + 0.5;
                float2 panoramaUV = float2(azimuth, latitude + 0.5);
                float horizonLatitude = _HorizonLevel;

                float skyAmount = saturate((latitude - horizonLatitude) / max(0.5 - horizonLatitude, 0.001));
                skyAmount = pow(skyAmount, _SkyGradientPower);
                half3 skyColor = lerp(_SkyHorizonColor.rgb, _SkyTopColor.rgb, skyAmount);

                float oceanDepth = saturate((horizonLatitude - latitude) / max(0.5 + horizonLatitude, 0.001));
                oceanDepth = pow(oceanDepth, _OceanGradientPower);
                half3 oceanColor = lerp(_OceanHorizonColor.rgb, _OceanDeepColor.rgb, oceanDepth);
                float oceanNoise = FI_Noise3(direction * 5.0 + float3(0, _Time.y * 0.01, 0)) - 0.5;
                oceanColor *= 1.0 + oceanNoise * _OceanVariation;

                float oceanMask = step(latitude, horizonLatitude);
                half3 color = lerp(skyColor, oceanColor, oceanMask);

                float horizon = 1.0 - smoothstep(0.0, max(_HorizonWidth, 0.001), abs(latitude - horizonLatitude));
                color = lerp(color, _HorizonGlowColor.rgb, horizon * _HorizonGlowStrength);

#if defined(_CLOUDS)
                float cloudCenter = horizonLatitude + _CloudAltitude;
                float cloudBand = exp(-pow((latitude - cloudCenter) / max(_CloudSpread, 0.001), 2.0));
                float3 cloudPosition = direction * _CloudScale + float3(_Time.y * _CloudSpeed, 0, _Time.y * _CloudSpeed * 0.37);
                float cloudNoise = FI_Fbm(cloudPosition);
                float cloudFadePhase = FI_Noise3(direction * 2.4 + 19.7) * 6.2831853 + _Time.y * _CloudFadeSpeed;
                float cloudFadeWave = 0.5 + 0.5 * sin(cloudFadePhase);
                float cloudVisibility = lerp(1.0, smoothstep(0.0, 1.0, cloudFadeWave), _CloudFadeStrength);
                float cloud = smoothstep(_CloudCoverage, _CloudCoverage + _CloudSoftness, cloudNoise) * cloudBand * cloudVisibility;
                float cloudShadow = smoothstep(_CloudCoverage - 0.09, _CloudCoverage + _CloudSoftness, cloudNoise) * cloudBand * cloudVisibility;
                color = lerp(color, _CloudShadowColor.rgb, cloudShadow * _CloudOpacity * 0.22 * (1.0 - cloud));
                color = lerp(color, _CloudColor.rgb, cloud * _CloudOpacity);
#endif

#if defined(_ISLANDS)
                float horizonV = 0.5 + horizonLatitude;
                float depth = _IslandDepth;
                FI_DrawIsland(color, panoramaUV, float2(0.07, horizonV - depth * 0.72), float2(0.040, 0.023), 0.7, step(0.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.19, horizonV - depth * 1.18), float2(0.053, 0.030), 2.1, step(1.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.31, horizonV - depth * 0.52), float2(0.030, 0.017), 4.4, step(2.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.43, horizonV - depth * 1.55), float2(0.060, 0.035), 1.3, step(3.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.57, horizonV - depth * 0.92), float2(0.045, 0.026), 5.6, step(4.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.69, horizonV - depth * 1.42), float2(0.056, 0.032), 3.2, step(5.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.81, horizonV - depth * 0.64), float2(0.034, 0.020), 6.1, step(6.5, _IslandCount));
                FI_DrawIsland(color, panoramaUV, float2(0.92, horizonV - depth * 1.12), float2(0.049, 0.028), 0.2, step(7.5, _IslandCount));
#endif

#if defined(_WINDBANDS)
                float windTime = _Time.y * _WindSpeed;
                float windBaseHeight = horizonLatitude + _WindPositionY;
                float strokes = 0.0;
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.03 + windTime, windBaseHeight - _WindVerticalSpread * 0.42, 0.92, 0.82, 1.3) * step(0.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.17 + windTime, windBaseHeight + _WindVerticalSpread * 0.18, 1.18, 1.00, 3.7) * step(1.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.31 + windTime, windBaseHeight - _WindVerticalSpread * 0.08, 0.74, 0.70, 6.2) * step(2.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.45 + windTime, windBaseHeight + _WindVerticalSpread * 0.52, 1.04, 0.88, 8.9) * step(3.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.58 + windTime, windBaseHeight - _WindVerticalSpread * 0.62, 0.86, 0.76, 11.4) * step(4.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.70 + windTime, windBaseHeight + _WindVerticalSpread * 0.78, 1.26, 0.94, 14.1) * step(5.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.82 + windTime, windBaseHeight - _WindVerticalSpread * 0.28, 0.68, 0.66, 16.8) * step(6.5, _WindCount);
                strokes += FI_WindTrailStroke(azimuth, latitude, _WindPositionX + 0.93 + windTime, windBaseHeight + _WindVerticalSpread * 0.35, 1.08, 0.84, 19.5) * step(7.5, _WindCount);
                float windSkyMask = smoothstep(horizonLatitude + 0.005, horizonLatitude + 0.025, latitude);
                color = lerp(color, _WindColor.rgb, saturate(strokes) * windSkyMask * _WindIntensity);
#endif

                half luminance = dot(color, half3(0.2126, 0.7152, 0.0722));
                color = lerp(luminance.xxx, color, _Saturation);
                color = (color - 0.5h) * _Contrast + 0.5h;
                color *= _Exposure;
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "FI_SkyboxShaderGUI"
}
