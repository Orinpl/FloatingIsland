Shader "FI_Lit"
{
    Properties
    {
        _BaseGroup ("Base Settings", Float) = 1
        _MainTex ("Base Map", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)

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
        _AdditionalLightIntensity ("Additional Light Intensity", Range(0, 2)) = 1

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
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
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
        #include "FI_LitCore.hlsl"

        float3 _LightDirection;
        float3 _LightPosition;

        struct FIAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
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
            #pragma vertex FIForwardVertex
            #pragma fragment FIForwardFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _SPECULAR
            #pragma shader_feature_local _RIMLIGHT
            #pragma shader_feature_local _MATCAP
            #pragma shader_feature_local _IBL
            #pragma shader_feature_local _IBL_USE_BUILTIN
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _USE_SHADOW_RAMP

            struct FIForwardVaryings
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
                half3 vertexLighting : TEXCOORD8;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIForwardVaryings FIForwardVertex(FIAttributes input)
            {
                FIForwardVaryings output = (FIForwardVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                half tangentSign = input.tangentOS.w * GetOddNegativeScale();

                output.positionCS = positionInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = positionInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.tangentWS = normalInput.tangentWS;
                output.bitangentWS = cross(normalInput.normalWS, normalInput.tangentWS) * tangentSign;
                output.viewDirWS = GetWorldSpaceViewDir(positionInput.positionWS);
                output.shadowCoord = GetShadowCoord(positionInput);
                output.fogFactor = ComputeFogFactor(positionInput.positionCS.z);
                output.vertexLighting = VertexLighting(positionInput.positionWS, normalInput.normalWS);
                return output;
            }

            half4 FIForwardFragment(FIForwardVaryings input) : SV_Target
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
                    input.positionCS,
                    input.vertexLighting);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                color.rgb = FI_ApplyGlobalHeightFog(color.rgb, input.positionWS);
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
            #pragma vertex FIOutlineVertex
            #pragma fragment FIOutlineFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma shader_feature_local _OUTLINE
            #pragma shader_feature_local _SMOOTH_NORMAL_OFF _SMOOTH_NORMAL _SMOOTH_NORMAL_VERTEXCOLOR

            struct FIOutlineVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 FIOutlineNormalOS(FIAttributes input)
            {
#if defined(_SMOOTH_NORMAL)
                return input.tangentOS.xyz;
#elif defined(_SMOOTH_NORMAL_VERTEXCOLOR)
                return normalize(input.color.rgb * 2.0 - 1.0);
#else
                return input.normalOS;
#endif
            }

            FIOutlineVaryings FIOutlineVertex(FIAttributes input)
            {
                FIOutlineVaryings output = (FIOutlineVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

#if defined(_OUTLINE)
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(FIOutlineNormalOS(input));
                float3 normalVS = mul((float3x3)GetWorldToViewMatrix(), normalWS);
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

            half4 FIOutlineFragment(FIOutlineVaryings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
#if defined(_OUTLINE)
                half3 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).rgb * _Color.rgb;
                half3 color = lerp(_OutlineColor.rgb, mainColor, _OutlineColorMix);
                color = MixFog(color, input.fogFactor);
                return half4(FI_ApplyGlobalHeightFog(color, input.positionWS), 1);
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
            #pragma vertex FIShadowVertex
            #pragma fragment FIShadowFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            struct FIShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIShadowVaryings FIShadowVertex(FIAttributes input)
            {
                FIShadowVaryings output = (FIShadowVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
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

            half4 FIShadowFragment(FIShadowVaryings input) : SV_Target
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
            #pragma vertex FIDepthVertex
            #pragma fragment FIDepthFragment
            #pragma multi_compile_instancing

            struct FIDepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIDepthVaryings FIDepthVertex(FIAttributes input)
            {
                FIDepthVaryings output = (FIDepthVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half FIDepthFragment(FIDepthVaryings input) : SV_Target
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
            #pragma vertex FIDepthNormalsVertex
            #pragma fragment FIDepthNormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct FIDepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            FIDepthNormalsVaryings FIDepthNormalsVertex(FIAttributes input)
            {
                FIDepthNormalsVaryings output = (FIDepthNormalsVaryings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 FIDepthNormalsFragment(FIDepthNormalsVaryings input) : SV_Target
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
    CustomEditor "FI_LitShaderGUI"
}
