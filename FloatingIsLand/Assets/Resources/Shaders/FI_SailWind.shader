Shader "FI/Sail Wind"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (0.82, 0.9, 0.62, 1)
        _TopColor ("Top Light Tint", Color) = (1, 0.94, 0.72, 1)
        _ShadowColor ("Cool Shadow Tint", Color) = (0.46, 0.54, 0.72, 1)

        _LightDirection ("Light Direction", Vector) = (0.35, 0.75, 0.45, 0)
        _Ambient ("Ambient", Range(0, 1)) = 0.35
        _LightSteps ("Lowpoly Light Steps", Range(1, 5)) = 3
        _TopTint ("Upward Tint", Range(0, 1)) = 0.18

        [HDR]_HighlightColor ("Highlight Color", Color) = (1, 0.86, 0.48, 1)
        _HighlightStrength ("Face Highlight", Range(0, 2)) = 0.25
        _HighlightSize ("Face Highlight Size", Range(8, 128)) = 36
        _BevelHighlight ("Bevel Highlight", Range(0, 2)) = 0.45
        _BevelSharpness ("Bevel Sharpness", Range(2, 64)) = 18
        _BevelWidth ("Fake Bevel Width", Range(0, 4)) = 0.8
        _RimStrength ("Rim Highlight", Range(0, 1)) = 0.08
        _RimPower ("Rim Power", Range(0.5, 8)) = 3

        _SailSwayAmplitude ("Sail Sway Amplitude", Range(0, 0.5)) = 0.08
        _SailWaveSpeed ("Sail Wave Speed", Range(0, 8)) = 1.2
        _SailWaveScale ("Sail Wave Scale", Range(0, 8)) = 0.8
        _SailClothAmplitude ("Sail Cloth Amplitude", Range(0, 0.25)) = 0.03
        _SailClothFrequency ("Sail Cloth Frequency", Range(0, 12)) = 2.5
        _SailFlutterAmplitude ("Sail Flutter Amplitude", Range(0, 0.08)) = 0.01
        _SailFlutterSpeed ("Sail Flutter Speed", Range(0, 32)) = 8
        _SailFlutterScale ("Sail Flutter Scale", Range(0, 16)) = 4
        _SailWindPush ("Wind Push", Range(0, 2)) = 1
        _SailNormalPush ("Normal Push", Range(0, 2)) = 0.35
        [Enum(Vertex Color, 0, Mask Texture, 1)]_SailMaskMode ("Displacement Control Mode", Float) = 0
        [NoScaleOffset]_SailMaskTex ("Displacement Mask", 2D) = "white" {}
        _SailMaskInvert ("Invert Mask", Range(0, 1)) = 0
        _SailMaskStart ("Fixed Edge", Range(0, 1)) = 0.08
        _SailMaskEnd ("Full Movement", Range(0, 1)) = 1
        _SailMaskStrength ("Mask Strength", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardSailWind"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "FI_LitCore.hlsl"

            sampler3D _GlobalWindField3D;
            float4 _GlobalWindFieldOrigin;
            float4 _GlobalWindFieldSize;
            float4 _GlobalWindDirection;
            float4 _GlobalWindFieldScrollSpeed;
            float _GlobalWindStrength;
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
            float _SailMaskMode;
            sampler2D _SailMaskTex;
            float _SailMaskInvert;
            float _SailMaskStart;
            float _SailMaskEnd;
            float _SailMaskStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
            };

            float FI_SailMask(appdata v)
            {
                float vertexColorMask = v.color.r;
                float textureMask = tex2Dlod(_SailMaskTex, float4(v.uv, 0.0, 0.0)).r;
                float mask = lerp(vertexColorMask, textureMask, step(0.5, _SailMaskMode));
                mask = saturate(mask);
                mask = lerp(mask, 1.0 - mask, step(0.5, _SailMaskInvert));

                // Both modes share the same remap so the attachment strip can be
                // exactly zero while the rest of the sail transitions smoothly.
                float maskStart = saturate(_SailMaskStart);
                float maskEnd = max(maskStart + 0.0001, saturate(_SailMaskEnd));
                float remappedMask = smoothstep(maskStart, maskEnd, mask);
                return saturate(remappedMask * _SailMaskStrength);
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
                    // Vertex shaders have no implicit screen-space derivatives.
                    // Explicit LOD is required here; tex3D() fails to compile on D3D.
                    float4 sample = tex3Dlod(_GlobalWindField3D, float4(uvw, 0.0));
                    float3 sampledDir = sample.rgb * 2.0 - 1.0;

                    windDir = FI_SafeNormalize(sampledDir);
                    windStrength *= sample.a;
                }
            }

            float3 FI_SailWindOffset(appdata v, float3 worldPos, float3 worldNormal)
            {
                float mask = FI_SailMask(v);
                float3 windDir;
                float windStrength;
                FI_SampleWind(worldPos, windDir, windStrength);

                float effectiveWind = sqrt(saturate(windStrength));
                float2 windXZ = FI_SafeNormalize(float3(windDir.x, 0.0, windDir.z)).xz;
                float phase = dot(worldPos.xz, windXZ) * _SailWaveScale + _Time.y * _SailWaveSpeed;
                float baseWave = sin(phase) * _SailSwayAmplitude;
                float clothWave = sin(phase * _SailClothFrequency + mask * 3.14159) * _SailClothAmplitude;
                float flutter = sin(_Time.y * _SailFlutterSpeed + worldPos.y * _SailFlutterScale + worldPos.x * 1.37);
                flutter *= _SailFlutterAmplitude * effectiveWind;

                float wave = (baseWave + clothWave + flutter) * effectiveWind * mask;
                float3 pushDir = FI_SafeNormalize(windDir * _SailWindPush + worldNormal * _SailNormalPush);
                return pushDir * wave;
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                worldPos += FI_SailWindOffset(v, worldPos, worldNormal);

                o.vertex = UnityWorldToClipPos(worldPos);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = FI_LitFragment(i.uv, i.worldPos, i.worldNormal);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    FallBack "FI_Lit"
    CustomEditor "FI_SailWindShaderGUI"
}
