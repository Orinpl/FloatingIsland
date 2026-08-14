Shader "FI/VFX/Wind Line"
{
    Properties
    {
        [MainTexture] _MainTex ("风丝遮罩", 2D) = "white" {}
        [MainColor][HDR] _Color ("颜色", Color) = (0.55, 0.9, 1.0, 1.0)
        _Intensity ("亮度", Range(0, 8)) = 2.0
        _Alpha ("透明度", Range(0, 1)) = 0.8
        _Feather ("风丝羽化", Range(0, 1)) = 0.35

        _ScrollSpeed ("流动速度 XY", Vector) = (-0.45, 0.0, 0.0, 0.0)
        _DistortionStrength ("摆动强度", Range(0, 0.25)) = 0.035
        _DistortionScale ("摆动密度", Range(0.1, 12)) = 2.4
        _DistortionSpeed ("摆动速度", Range(-8, 8)) = 1.1

        _EdgePower ("宽度边缘柔和", Range(0.25, 8)) = 2.0
        _VisibleWidth ("可见宽度", Range(0.05, 1)) = 1.0
        _TipFade ("首尾淡出", Range(0.001, 0.49)) = 0.13
        _PulseStrength ("亮度脉冲", Range(0, 1)) = 0.35
        _PulseScale ("脉冲密度", Range(0.1, 12)) = 1.6
        _PulseSpeed ("脉冲速度", Range(-12, 12)) = 2.0
        _PulseSharpness ("脉冲锐度", Range(0.25, 8)) = 1.5

        _BrushStrength ("笔锋强度", Range(0, 1)) = 1.0
        _BrushHeadLength ("左端收尖", Range(0.001, 0.49)) = 0.22
        _BrushTailLength ("右端收尖", Range(0.001, 0.49)) = 0.22
        _BrushSharpness ("笔锋锐度", Range(0.25, 8)) = 1.6
        _BrushBias ("笔锋偏移", Range(-1, 1)) = 0.0

        [Enum(Alpha,0,Additive,1)] _BlendMode ("混合模式", Float) = 1
        [Toggle] _ZTestAlways ("始终显示", Float) = 0
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 5
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 1
        [HideInInspector] _ZTest ("ZTest", Float) = 4
        [HideInInspector] _TimeOffset ("Time Offset", Float) = 0
        [HideInInspector] _SpeedMultiplier ("Speed Multiplier", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "WindLine"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            Cull Off
            ZWrite Off
            ZTest [_ZTest]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WindVertex
            #pragma fragment WindFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _Intensity;
                half _Alpha;
                half _Feather;
                float4 _ScrollSpeed;
                half _DistortionStrength;
                half _DistortionScale;
                half _DistortionSpeed;
                half _EdgePower;
                half _VisibleWidth;
                half _TipFade;
                half _PulseStrength;
                half _PulseScale;
                half _PulseSpeed;
                half _PulseSharpness;
                half _BrushStrength;
                half _BrushHeadLength;
                half _BrushTailLength;
                half _BrushSharpness;
                half _BrushBias;
                half _BlendMode;
                half _ZTestAlways;
                half _TimeOffset;
                half _SpeedMultiplier;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                half fogFactor : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings WindVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half SampleWindMask(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r;
            }

            half4 WindFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = (_Time.y + _TimeOffset) * _SpeedMultiplier;
                float2 rawUV = input.uv;
                float2 flowUV = rawUV * _MainTex_ST.xy + _MainTex_ST.zw;
                float sway = sin((rawUV.x * _DistortionScale - time * _DistortionSpeed) * TWO_PI);
                sway += sin((rawUV.x * (_DistortionScale * 1.73) + time * _DistortionSpeed * 0.61) * TWO_PI) * 0.35;
                flowUV.y += sway * _DistortionStrength;
                flowUV += _ScrollSpeed.xy * time;

                half rawMask = SampleWindMask(flowUV);
                // 0 gives a graphic hard edge; 1 restores a broad, soft transition.
                // Keep a tiny derivative-based width so the hard setting remains antialiased.
                half featherWidth = max(_Feather * 0.35, fwidth(rawMask));
                half mask = smoothstep(0.35 - featherWidth, 0.35 + featherWidth, rawMask);

                half headTaper = pow(smoothstep(0.0, _BrushHeadLength, rawUV.x), _BrushSharpness);
                half tailTaper = pow(smoothstep(0.0, _BrushTailLength, 1.0 - rawUV.x), _BrushSharpness);
                half brushWidth = lerp(1.0, max(0.015, headTaper * tailTaper), _BrushStrength);
                half tipDirection = (1.0 - tailTaper) - (1.0 - headTaper);
                half brushCenter = 0.5 + tipDirection * _BrushBias * _BrushStrength * 0.25;
                half visibleWidth = max(0.01, _VisibleWidth * brushWidth);
                half normalizedWidth = abs((rawUV.y - brushCenter) * 2.0) / visibleWidth;
                half widthFade = pow(saturate(1.0 - normalizedWidth), _EdgePower);
                half tipFade = smoothstep(0.0, _TipFade, rawUV.x)
                    * smoothstep(0.0, _TipFade, 1.0 - rawUV.x);

                half pulseWave = saturate(0.5 + 0.5 * sin((rawUV.x * _PulseScale - time * _PulseSpeed) * TWO_PI));
                half pulse = lerp(1.0, pow(pulseWave, _PulseSharpness), _PulseStrength);
                half alpha = saturate(mask * widthFade * tipFade * pulse * _Alpha * input.color.a * _Color.a);

                half3 color = _Color.rgb * input.color.rgb * _Intensity;
                color *= lerp(0.65, 1.0, mask);
                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "FI_WindLineShaderGUI"
}
