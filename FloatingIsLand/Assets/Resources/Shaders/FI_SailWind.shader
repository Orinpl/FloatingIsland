Shader "FI/Sail Wind"
{
    Properties
    {
        _BaseGroup ("Base Settings", Float) = 1
        _MainTex ("Base Map", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)

        _WindGroup ("Sail Wind", Float) = 1
        _SailSwayAmplitude ("Sway Amplitude", Range(0, 0.5)) = 0.035
        _SailWaveSpeed ("Wave Speed", Range(0, 8)) = 1.2
        _SailWaveScale ("Wave Scale", Range(0, 8)) = 0.8
        _SailClothAmplitude ("Cloth Amplitude", Range(0, 0.25)) = 0.015
        _SailClothFrequency ("Cloth Frequency", Range(0, 12)) = 2.5
        _SailFlutterAmplitude ("Flutter Amplitude", Range(0, 0.08)) = 0.004
        _SailFlutterSpeed ("Flutter Speed", Range(0, 32)) = 8
        _SailFlutterScale ("Flutter Scale", Range(0, 16)) = 4
        _SailWindPush ("Wind Push", Range(0, 2)) = 1
        _SailNormalPush ("Normal Push", Range(0, 2)) = 0
        _SailMaxDisplacement ("Max Displacement", Range(0, 10)) = 10
        [HideInInspector] _SailObjectScale ("Sail Object Scale", Vector) = (1, 1, 1, 1)
        _SailMaskMode ("Displacement Control Mode", Float) = 3
        _SailMaskTex ("Displacement Mask", 2D) = "white" {}
        _SailMaskInvert ("Invert Mask", Range(0, 1)) = 0
        _SailMaskStart ("Fixed Edge", Range(0, 1)) = 0.08
        _SailMaskEnd ("Full Movement", Range(0, 1)) = 1
        _SailMaskStrength ("Mask Strength", Range(0, 1)) = 1

        _NormalMapGroup ("Normal Map", Float) = 0
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1.0

        _ShadowGroup ("Shadow Settings", Float) = 1
        _ShadowColor ("1st Shadow Color", Color) = (0.75, 0.65, 0.80, 1)
        _Shadow2ndColor ("2nd Shadow Color", Color) = (0.55, 0.45, 0.65, 1)
        _ShadowBorderRange ("1st Shadow Softness", Range(0.001, 1)) = 0.3
        _ShadowBorderPosition ("1st Shadow Position", Range(-1, 1)) = 0.0
        _Shadow2ndBorderRange ("2nd Shadow Softness", Range(0.001, 1)) = 0.15
        _Shadow2ndBorderPosition ("2nd Shadow Position", Range(-1, 1)) = -0.3
        _UseShadowRamp ("Use Shadow Ramp", Float) = 0
        _ShadowRampTex ("Shadow Ramp", 2D) = "white" {}
        _ShadowReceive ("Receive Shadow Strength", Range(0, 1)) = 0.5
        _ShadowEnvStrength ("Environment Light in Shadow", Range(0, 1)) = 0.2

        _SpecularGroup ("Specular", Float) = 0
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularPower ("Specular Power", Range(1, 256)) = 64
        _SpecularIntensity ("Specular Intensity", Range(0, 5)) = 0.5
        _SpecularSmoothness ("Specular Softness", Range(0.001, 0.5)) = 0.05
        _SpecularMask ("Specular Mask", 2D) = "white" {}

        _RimLightGroup ("Rim Light", Float) = 0
        _RimColor ("Rim Color", Color) = (0.7, 0.85, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 16)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0, 3)) = 0.6
        _RimSmoothness ("Rim Softness", Range(0.001, 0.5)) = 0.1
        _RimLightDirOffset ("Light Direction Offset", Range(0, 1)) = 0.0
        _RimCustomOffset ("Custom World Direction Offset", Vector) = (0, 0, 0, 0)
        _RimShadowMask ("Shadow Area Mask", Range(0, 1)) = 0.5

        _MatCapGroup ("MatCap", Float) = 0
        _MatCapTex ("MatCap Map", 2D) = "black" {}
        _MatCapColor ("MatCap Color", Color) = (1, 1, 1, 1)
        _MatCapIntensity ("MatCap Intensity", Range(0, 2)) = 0.5
        _MatCapBlendMode ("MatCap Blend Mode", Float) = 0

        _IBLGroup ("IBL Reflection", Float) = 0
        _IBLUseBuiltin ("Use Reflection Probes", Float) = 1
        _IBLCubemap ("Custom Environment Cubemap", Cube) = "" {}
        _IBLColor ("IBL Color", Color) = (1, 1, 1, 1)
        _IBLIntensity ("IBL Intensity", Range(0, 5)) = 0.5
        _IBLRoughness ("IBL Roughness", Range(0, 1)) = 0.3
        _IBLMetallic ("IBL Metallic", Range(0, 1)) = 0.0
        _IBLBlendMode ("IBL Blend Mode", Float) = 0

        _EmissionGroup ("Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        _OutlineGroup ("Outline", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0.2, 0.15, 0.25, 1)
        _OutlineWidth ("Outline Width", Range(0, 20)) = 0.8
        _OutlineWidthMask ("Outline Width Mask", 2D) = "white" {}
        _OutlineColorMix ("Base Color Mix", Range(0, 1)) = 0.2
        _OutlineZOffset ("Outline Depth Offset", Range(-1, 1)) = 0.0
        _SmoothNormalMode ("Smooth Normal Mode", Float) = 0

        _RenderingGroup ("Rendering Settings", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #define FI_SAIL_WIND 1
        #include "FI_LitCore.hlsl"

        TEXTURE3D(_GlobalWindField3D);
        SAMPLER(sampler_GlobalWindField3D);
        float4 _GlobalWindFieldOrigin;
        float4 _GlobalWindFieldSize;
        float4 _GlobalWindDirection;
        float4 _GlobalWindFieldScrollSpeed;
        float _GlobalWindStrength;
        float _GlobalWindMainDirectionWeight;
        float _GlobalWindFieldEnabled;

        float3 _LightDirection;
        float3 _LightPosition;

        struct FIWindAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float2 FI_SailWindUv(FIWindAttributes input)
        {
            return input.uv2;
        }

        float FI_SailDisplacementScale()
        {
            float2 sailScale = max(abs(_SailObjectScale.xy), float2(0.0001, 0.0001));
            return max(sailScale.x, sailScale.y);
        }

        float FI_SailMask(FIWindAttributes input)
        {
            float2 windUv = FI_SailWindUv(input);
            float uvMask = windUv.y;
            float uvUEdgeMask = min(saturate(windUv.x), saturate(1.0 - windUv.x)) * 2.0;
            float vertexColorMask = input.color.r;
            float textureMask = SAMPLE_TEXTURE2D_LOD(
                _SailMaskTex,
                sampler_SailMaskTex,
                windUv,
                0).r;
            float mask = uvMask;
            if (_SailMaskMode > 2.5)
            {
                mask = uvUEdgeMask;
            }
            else if (_SailMaskMode > 1.5)
            {
                mask = textureMask;
            }
            else if (_SailMaskMode > 0.5)
            {
                mask = vertexColorMask;
            }
            mask = saturate(mask);
            mask = lerp(mask, 1.0 - mask, step(0.5, _SailMaskInvert));

            float maskStart = saturate(_SailMaskStart);
            float maskEnd = max(maskStart + 0.0001, saturate(_SailMaskEnd));
            return saturate(smoothstep(maskStart, maskEnd, mask) * _SailMaskStrength);
        }

        void FI_SampleWind(float3 worldPos, out float3 windDir, out float windStrength)
        {
            windDir = FI_SafeNormalize(_GlobalWindDirection.xyz);
            windStrength = max(0.0, _GlobalWindStrength);

            if (_GlobalWindFieldEnabled > 0.5)
            {
                float3 size = max(abs(_GlobalWindFieldSize.xyz), float3(0.001, 0.001, 0.001));
                float3 uvw = frac(
                    (worldPos - _GlobalWindFieldOrigin.xyz) / size +
                    _GlobalWindFieldScrollSpeed.xyz * _Time.y);
                float4 windSample = SAMPLE_TEXTURE3D_LOD(
                    _GlobalWindField3D,
                    sampler_GlobalWindField3D,
                    uvw,
                    0);
                float3 sampledDir = windSample.rgb * 2.0 - 1.0;
                windDir = FI_SafeNormalize(lerp(sampledDir, windDir, saturate(_GlobalWindMainDirectionWeight)));
                windStrength *= windSample.a;
            }
        }

        float3 FI_SailWindOffset(FIWindAttributes input, float3 worldPos, float3 worldNormal)
        {
            float mask = FI_SailMask(input);
            float3 windDir;
            float windStrength;
            float3 objectPivotWS = TransformObjectToWorld(float3(0, 0, 0));
            FI_SampleWind(objectPivotWS, windDir, windStrength);

            float effectiveWind = sqrt(saturate(windStrength));
            float3 planarWindDir = FI_SafeNormalize(float3(windDir.x, 0, windDir.z));
            float2 windXZ = planarWindDir.xz;
            float2 windUv = FI_SailWindUv(input);
            float2 windUvDir = normalize(windXZ + float2(1e-5, 1e-5));
            float phase = dot(windUv, windUvDir) * _SailWaveScale + _Time.y * _SailWaveSpeed;
            float broadPulse = saturate(
                0.55 +
                0.25 * sin(phase) +
                0.12 * sin(phase * 1.73 + 1.2) +
                0.08 * sin(phase * 2.41 + windUv.y * 0.37));
            float swayAmount = saturate(_SailSwayAmplitude * 2.0);
            float clothAmount = saturate(_SailClothAmplitude * 4.0);
            float flutterAmount = saturate(_SailFlutterAmplitude * 12.5);
            float clothWave = sin(phase * _SailClothFrequency + mask * 3.14159 + windUv.y * 0.35) * clothAmount;
            float fineNoise =
                sin(_Time.y * (_SailFlutterSpeed * 1.31 + 0.17) + dot(windUv, float2(3.1, 5.3))) +
                0.55 * sin(_Time.y * (_SailFlutterSpeed * 1.91 + 0.43) + dot(windUv, float2(7.7, 2.4))) +
                0.3 * sin(_Time.y * (_SailFlutterSpeed * 2.47 + 0.61) + dot(windUv, float2(13.1, 8.6)));
            fineNoise /= 1.85;
            float flutter = fineNoise * flutterAmount * effectiveWind;

            float displacementLimit = max(_SailMaxDisplacement, 0.0) * FI_SailDisplacementScale();
            float forwardAmount = (swayAmount * broadPulse + clothWave * 0.25 + flutter * 0.35) * effectiveWind * mask;
            float forwardOffset = saturate(max(0.0, forwardAmount)) * displacementLimit;
            float3 pushDir = FI_SafeNormalize(windDir * _SailWindPush + worldNormal * _SailNormalPush);
            float3 sideDir = FI_SafeNormalize(cross(float3(0, 1, 0), planarWindDir));
            float verticalUv = windUv.y - 0.5;
            float liftAmount = (clothWave * 0.45 + flutter * 0.8 + verticalUv * clothAmount * 0.35) * effectiveWind * mask;
            float sideAmount = flutter * 0.45 * mask;
            float liftOffset = clamp(liftAmount, -0.45, 0.45) * displacementLimit;
            float sideOffset = clamp(sideAmount, -0.35, 0.35) * displacementLimit;
            return pushDir * forwardOffset + float3(0, 1, 0) * liftOffset + sideDir * sideOffset;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FIWindForwardVertex
            #pragma fragment FIWindForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _SPECULAR
            #pragma shader_feature_local _RIMLIGHT
            #pragma shader_feature_local _MATCAP
            #pragma shader_feature_local _IBL
            #pragma shader_feature_local _IBL_USE_BUILTIN
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _USE_SHADOW_RAMP

            struct FIWindForwardVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half3 tangentWS : TEXCOORD3;
                half3 bitangentWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                half fogFactor : TEXCOORD7;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIWindForwardVaryings FIWindForwardVertex(FIWindAttributes input)
            {
                FIWindForwardVaryings output = (FIWindForwardVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += FI_SailWindOffset(input, positionWS, normalWS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half tangentSign = input.tangentOS.w * GetOddNegativeScale();
                VertexPositionInputs positionInput = (VertexPositionInputs)0;
                positionInput.positionWS = positionWS;
                positionInput.positionCS = positionCS;

                output.positionCS = positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = cross(normalInput.normalWS, normalInput.tangentWS) * tangentSign;
                output.viewDirWS = GetWorldSpaceViewDir(positionWS);
                output.shadowCoord = GetShadowCoord(positionInput);
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 FIWindForwardFragment(FIWindForwardVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 color = FI_ToonFragment(
                    input.uv,
                    input.positionWS,
                    input.normalWS,
                    input.tangentWS,
                    input.bitangentWS,
                    input.viewDirWS,
                    input.shadowCoord,
                    input.positionCS);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FIWindOutlineVertex
            #pragma fragment FIWindOutlineFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _OUTLINE
            #pragma shader_feature_local _SMOOTH_NORMAL_OFF _SMOOTH_NORMAL _SMOOTH_NORMAL_VERTEXCOLOR

            struct FIWindOutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 FIWindOutlineNormalOS(FIWindAttributes input)
            {
#if defined(_SMOOTH_NORMAL)
                return input.tangentOS.xyz;
#elif defined(_SMOOTH_NORMAL_VERTEXCOLOR)
                return normalize(input.color.rgb * 2.0 - 1.0);
#else
                return input.normalOS;
#endif
            }

            FIWindOutlineVaryings FIWindOutlineVertex(FIWindAttributes input)
            {
                FIWindOutlineVaryings output = (FIWindOutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

#if defined(_OUTLINE)
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += FI_SailWindOffset(input, positionWS, normalWS);
                float4 positionCS = TransformWorldToHClip(positionWS);
                float3 outlineNormalWS = TransformObjectToWorldNormal(FIWindOutlineNormalOS(input));
                float3 normalVS = mul((float3x3)GetWorldToViewMatrix(), outlineNormalWS);
                float2 direction = normalize(normalVS.xy + 1e-5);
                float mask = SAMPLE_TEXTURE2D_LOD(
                    _OutlineWidthMask,
                    sampler_OutlineWidthMask,
                    input.uv,
                    0).r;
                positionCS.xy += direction * _OutlineWidth * 0.001 * mask * positionCS.w;
#if UNITY_REVERSED_Z
                positionCS.z -= _OutlineZOffset * 0.001 * positionCS.w;
#else
                positionCS.z += _OutlineZOffset * 0.001 * positionCS.w;
#endif
                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
#else
                output.positionCS = float4(0, 0, 0, 1);
#endif
                return output;
            }

            half4 FIWindOutlineFragment(FIWindOutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(_OUTLINE)
                half3 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;
                half3 color = lerp(_OutlineColor.rgb, mainColor, _OutlineColorMix);
                return half4(MixFog(color, input.fogFactor), 1);
#else
                discard;
                return 0;
#endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FIWindShadowVertex
            #pragma fragment FIWindShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            struct FIWindShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIWindShadowVaryings FIWindShadowVertex(FIWindAttributes input)
            {
                FIWindShadowVaryings output = (FIWindShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += FI_SailWindOffset(input, positionWS, normalWS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
                float3 lightDirectionWS = _LightDirection;
#endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
#if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
                output.positionCS = positionCS;
                return output;
            }

            half4 FIWindShadowFragment(FIWindShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FIWindDepthVertex
            #pragma fragment FIWindDepthFragment
            #pragma multi_compile_instancing

            struct FIWindDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIWindDepthVaryings FIWindDepthVertex(FIWindAttributes input)
            {
                FIWindDepthVaryings output = (FIWindDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += FI_SailWindOffset(input, positionWS, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half FIWindDepthFragment(FIWindDepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FIWindDepthNormalsVertex
            #pragma fragment FIWindDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct FIWindDepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIWindDepthNormalsVaryings FIWindDepthNormalsVertex(FIWindAttributes input)
            {
                FIWindDepthNormalsVaryings output = (FIWindDepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS += FI_SailWindOffset(input, positionWS, normalWS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = normalWS;
                return output;
            }

            half4 FIWindDepthNormalsFragment(FIWindDepthNormalsVaryings input) : SV_Target
            {
                float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remappedOctNormalWS), 0);
#else
                return half4(normalWS, 0);
#endif
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "FI_SailWindShaderGUI"
}
