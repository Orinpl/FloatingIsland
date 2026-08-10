// 受影响建筑 / 地图元素的辉光：把对方的网格再画一遍，只出一层菲涅尔边缘光叠加上去。
// 加法混合 + 不写深度，所以**完全不碰对方的原材质**——原模型该什么样还是什么样。
//
// 用途是「这一栋给你加/扣了分」的弱提示，强度刻意压低；加分与扣分靠 _Color 分色，
// 但不用落点格那套绿/红（那是"能不能建"的语义，混用玩家会读错）。
//
// 沿法线外推一点点做描边感，外推**在世界空间做**：本工程的 FBX 实例带着 ≈210 的 localScale，
// 在物体空间外推会被一起放大两百倍，模型直接炸开。
Shader "FloatingIsLand/HighlightGlow"
{
    Properties
    {
        _Color ("辉光颜色", Color) = (1, 0.92, 0.45, 1)
        _RimPower ("边缘收束", Range(0.5, 8)) = 2.2
        _Intensity ("强度", Range(0, 3)) = 0.8
        _Outline ("沿法线外推（世界单位）", Range(0, 0.2)) = 0.02
        _Pulse ("呼吸幅度", Range(0, 1)) = 0.25
        _PulseSpeed ("呼吸速度", Float) = 2.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+2"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha One   // 加法：只往上加光，不会把对方压暗
            ZWrite Off
            ZTest LEqual
            Cull Back
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldView : TEXCOORD1;
            };

            fixed4 _Color;
            float _RimPower;
            float _Intensity;
            float _Outline;
            float _Pulse;
            float _PulseSpeed;

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(UnityObjectToWorldNormal(v.normal));

                // 世界空间外推，不受模型自身缩放影响
                worldPos += worldNormal * _Outline;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.worldNormal = worldNormal;
                o.worldView = _WorldSpaceCameraPos - worldPos;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float ndotv = saturate(dot(normalize(i.worldNormal), normalize(i.worldView)));
                float rim = pow(saturate(1.0 - ndotv), _RimPower);

                // 轻微呼吸，让"被选中"这件事在静止画面里也看得出来
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _Pulse;

                float strength = rim * _Intensity * pulse;
                return fixed4(_Color.rgb * strength, saturate(strength) * _Color.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
