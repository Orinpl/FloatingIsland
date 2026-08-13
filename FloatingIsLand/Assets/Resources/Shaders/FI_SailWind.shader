Shader "FI/Sail Wind"
{
    Properties
    {
        _BaseGroup ("基础设置", Float) = 1
        _MainTex ("主贴图", 2D) = "white" {}
        _Color ("主颜色", Color) = (1, 1, 1, 1)

        _WindGroup ("风帆风动", Float) = 1
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

        _NormalMapGroup ("法线贴图", Float) = 0
        _BumpMap ("法线贴图", 2D) = "bump" {}
        _BumpScale ("法线强度", Range(0, 2)) = 1.0

        _ShadowGroup ("阴影设置", Float) = 1
        _ShadowColor ("1st 阴影颜色", Color) = (0.75, 0.65, 0.80, 1)
        _Shadow2ndColor ("2nd 阴影颜色", Color) = (0.55, 0.45, 0.65, 1)
        _ShadowBorderRange ("阴影边缘柔和度", Range(0.001, 1)) = 0.3
        _ShadowBorderPosition ("阴影位置偏移", Range(-1, 1)) = 0.0
        _Shadow2ndBorderRange ("2nd阴影柔和度", Range(0.001, 1)) = 0.15
        _Shadow2ndBorderPosition ("2nd阴影位置", Range(-1, 1)) = -0.3
        _UseShadowRamp ("使用渐变贴图", Float) = 0
        _ShadowRampTex ("阴影渐变贴图", 2D) = "white" {}
        _ShadowReceive ("接收阴影强度", Range(0, 1)) = 0.5
        _ShadowEnvStrength ("环境光影响阴影", Range(0, 1)) = 0.2

        _SpecularGroup ("高光", Float) = 0
        _SpecularColor ("高光颜色", Color) = (1, 1, 1, 1)
        _SpecularPower ("高光锐度", Range(1, 256)) = 64
        _SpecularIntensity ("高光强度", Range(0, 5)) = 0.5
        _SpecularSmoothness ("高光边缘柔和", Range(0.001, 0.5)) = 0.05
        _SpecularMask ("高光遮罩", 2D) = "white" {}

        _RimLightGroup ("边缘光", Float) = 0
        _RimColor ("边缘光颜色", Color) = (0.7, 0.85, 1.0, 1)
        _RimPower ("边缘光范围", Range(0.5, 16)) = 3.0
        _RimIntensity ("边缘光强度", Range(0, 3)) = 0.6
        _RimSmoothness ("边缘光软硬度", Range(0.001, 0.5)) = 0.1
        _RimLightDirOffset ("光方向偏移", Range(0, 1)) = 0.0
        _RimCustomOffset ("自定义偏移 (XYZ世界方向)", Vector) = (0, 0, 0, 0)
        _RimShadowMask ("阴影区域遮蔽", Range(0, 1)) = 0.5

        _MatCapGroup ("MatCap", Float) = 0
        _MatCapTex ("MatCap贴图", 2D) = "black" {}
        _MatCapColor ("MatCap颜色", Color) = (1, 1, 1, 1)
        _MatCapIntensity ("MatCap强度", Range(0, 2)) = 0.5
        _MatCapBlendMode ("MatCap混合模式", Float) = 0

        _IBLGroup ("IBL 环境反射", Float) = 0
        _IBLUseBuiltin ("使用内置IBL (Reflection Probe)", Float) = 1
        _IBLCubemap ("自定义环境Cubemap", Cube) = "" {}
        _IBLColor ("IBL 颜色", Color) = (1, 1, 1, 1)
        _IBLIntensity ("IBL 强度", Range(0, 5)) = 0.5
        _IBLRoughness ("IBL 粗糙度", Range(0, 1)) = 0.3
        _IBLMetallic ("金属度 (影响IBL混合)", Range(0, 1)) = 0.0
        _IBLBlendMode ("IBL 混合模式", Float) = 0

        _EmissionGroup ("自发光", Float) = 0
        _EmissionMap ("自发光贴图", 2D) = "black" {}
        [HDR] _EmissionColor ("自发光颜色", Color) = (0, 0, 0, 1)

        _OutlineGroup ("描边", Float) = 0
        _OutlineColor ("描边颜色", Color) = (0.2, 0.15, 0.25, 1)
        _OutlineWidth ("描边宽度", Range(0, 20)) = 0.8
        _OutlineWidthMask ("描边宽度遮罩", 2D) = "white" {}
        _OutlineColorMix ("描边混合主色比例", Range(0, 1)) = 0.2
        _OutlineZOffset ("描边深度偏移", Range(-1, 1)) = 0.0
        _SmoothNormalMode ("软法线模式 (需烘焙)", Float) = 0

        _RenderingGroup ("渲染设置", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("剔除模式", Float) = 0
        [Enum(Off, 0, On, 1)] _ZWrite ("深度写入", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("源混合", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("目标混合", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "Lighting.cginc"
        #include "FI_LitCore.hlsl"

        sampler3D _GlobalWindField3D;
        float4 _GlobalWindFieldOrigin;
        float4 _GlobalWindFieldSize;
        float4 _GlobalWindDirection;
        float4 _GlobalWindFieldScrollSpeed;
        float _GlobalWindStrength;
        float _GlobalWindMainDirectionWeight;
        float _GlobalWindFieldEnabled;

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
        sampler2D _SailMaskTex;
        float _SailMaskInvert;
        float _SailMaskStart;
        float _SailMaskEnd;
        float _SailMaskStrength;

        struct FIWindAppData
        {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float4 tangent : TANGENT;
            float2 uv : TEXCOORD0;
            float2 uv2 : TEXCOORD1;
            float4 color : COLOR;
        };

        float2 FI_SailWindUv(FIWindAppData v)
        {
            return v.uv2;
        }

        float FI_SailDisplacementScale()
        {
            float2 sailScale = max(abs(_SailObjectScale.xy), float2(0.0001, 0.0001));
            return max(sailScale.x, sailScale.y);
        }

        float FI_SailMask(FIWindAppData v)
        {
            float2 windUv = FI_SailWindUv(v);
            float uvMask = windUv.y;
            float uvUEdgeMask = min(saturate(windUv.x), saturate(1.0 - windUv.x)) * 2.0;
            float vertexColorMask = v.color.r;
            float textureMask = tex2Dlod(_SailMaskTex, float4(windUv, 0, 0)).r;
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
                float4 sample = tex3Dlod(_GlobalWindField3D, float4(uvw, 0.0));
                float3 sampledDir = sample.rgb * 2.0 - 1.0;
                windDir = FI_SafeNormalize(lerp(sampledDir, windDir, saturate(_GlobalWindMainDirectionWeight)));
                windStrength *= sample.a;
            }
        }

        float3 FI_SailWindOffset(FIWindAppData v, float3 worldPos, float3 worldNormal)
        {
            float mask = FI_SailMask(v);
            float3 windDir;
            float windStrength;
            float3 objectPivotWS = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
            FI_SampleWind(objectPivotWS, windDir, windStrength);

            float effectiveWind = sqrt(saturate(windStrength));
            float3 planarWindDir = FI_SafeNormalize(float3(windDir.x, 0, windDir.z));
            float2 windXZ = planarWindDir.xz;
            float2 windUv = FI_SailWindUv(v);
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
        ENDCG

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "ForwardBase" }

            Cull [_Cull]
            ZWrite [_ZWrite]
            Blend [_SrcBlend] [_DstBlend]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _SPECULAR
            #pragma shader_feature_local _RIMLIGHT
            #pragma shader_feature_local _MATCAP
            #pragma shader_feature_local _IBL
            #pragma shader_feature_local _IBL_USE_BUILTIN
            #pragma shader_feature_local _EMISSION
            #pragma shader_feature_local _USE_SHADOW_RAMP

            #include "AutoLight.cginc"

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 viewDirWS : TEXCOORD5;
                UNITY_FOG_COORDS(6)
                SHADOW_COORDS(7)
            };

            v2f vert(FIWindAppData v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                worldPos += FI_SailWindOffset(v, worldPos, worldNormal);

                o.pos = UnityWorldToClipPos(worldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.positionWS = worldPos;
                o.normalWS = worldNormal;
                o.tangentWS = UnityObjectToWorldDir(v.tangent.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * v.tangent.w * unity_WorldTransformParams.w;
                o.viewDirWS = _WorldSpaceCameraPos.xyz - o.positionWS;
                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = FI_ToonFragment(
                    i.uv,
                    i.positionWS,
                    i.normalWS,
                    i.tangentWS,
                    i.bitangentWS,
                    i.viewDirWS,
                    SHADOW_ATTENUATION(i));
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZWrite [_ZWrite]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma shader_feature_local _OUTLINE
            #pragma shader_feature_local _SMOOTH_NORMAL_OFF _SMOOTH_NORMAL _SMOOTH_NORMAL_VERTEXCOLOR

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            float3 OutlineNormal(FIWindAppData v)
            {
#if defined(_SMOOTH_NORMAL)
                return v.tangent.xyz;
#elif defined(_SMOOTH_NORMAL_VERTEXCOLOR)
                return normalize(v.color.rgb * 2.0 - 1.0);
#else
                return v.normal;
#endif
            }

            v2f vert(FIWindAppData v)
            {
                v2f o;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
#if defined(_OUTLINE)
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                worldPos += FI_SailWindOffset(v, worldPos, worldNormal);
                float4 pos = UnityWorldToClipPos(worldPos);
                float3 outlineNormalWS = UnityObjectToWorldNormal(OutlineNormal(v));
                float3 normalVS = mul((float3x3)UNITY_MATRIX_V, outlineNormalWS);
                float2 dir = normalize(normalVS.xy + 1e-5);
                float mask = tex2Dlod(_OutlineWidthMask, float4(v.uv, 0, 0)).r;
                pos.xy += dir * _OutlineWidth * 0.001 * mask * pos.w;
#if UNITY_REVERSED_Z
                pos.z -= _OutlineZOffset * 0.001 * pos.w;
#else
                pos.z += _OutlineZOffset * 0.001 * pos.w;
#endif
                o.pos = pos;
#else
                o.pos = float4(0, 0, 0, 1);
#endif
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
#if defined(_OUTLINE)
                fixed3 mainCol = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                fixed4 col = fixed4(lerp(_OutlineColor.rgb, mainCol, _OutlineColorMix), 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
#else
                discard;
                return 0;
#endif
            }
            ENDCG
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster

            struct v2f
            {
                V2F_SHADOW_CASTER;
            };

            v2f vert(FIWindAppData v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                worldPos += FI_SailWindOffset(v, worldPos, worldNormal);
                v.vertex = mul(unity_WorldToObject, float4(worldPos, 1));
                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }

    FallBack "FI_Lit"
    CustomEditor "FI_SailWindShaderGUI"
}
