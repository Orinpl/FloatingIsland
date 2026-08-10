// 建筑作用范围圆环：贴地一张四边形，范围形状全靠 fragment 里的圆角矩形 SDF 算出来。
// 中心透明、越靠边缘越白，边界处再压一圈亮边——即需求里说的「菲涅尔那种感觉」。
//
// 为什么是「一组矩形」而不是一个圆心 + 半径：领域层的范围是**自占地边缘起算**的最小距离
// （RangeMath），6×6 的船坞真实范围是圆角矩形不是圆。矩形由 RangeRingGeometry 把占地拆出来，
// 拆法保证在所有格中心处与 RangeMath 逐点相等。
//
// 队列 Transparent-1：要排在落点格标记（Transparent）之前，让落点格永远盖在环上面。
// 没写 LightMode 标签：Built-in 下按 Unlit 走，URP 下会被当成 SRPDefaultUnlit。
Shader "FloatingIsLand/RangeRing"
{
    Properties
    {
        _Color ("环颜色（乳白）", Color) = (0.95, 0.96, 1.0, 1.0)
        _Radius ("作用半径（世界单位）", Float) = 10
        _FillPower ("由内向外变白的收束", Range(0.5, 8)) = 2.2
        _FillAlpha ("环体最大不透明度", Range(0, 1)) = 0.28
        _RimStart ("亮边起始位置（占半径比例）", Range(0.5, 1)) = 0.86
        _RimAlpha ("亮边不透明度", Range(0, 1)) = 0.55
        _EdgeSoftness ("外沿羽化（世界单位）", Float) = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent-1"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // 与 RangeRingGeometry.MaxBoxes 必须一致
            #define MAX_BOXES 8

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 world : TEXCOORD0;
            };

            fixed4 _Color;
            float _Radius;
            float _FillPower;
            float _FillAlpha;
            float _RimStart;
            float _RimAlpha;
            float _EdgeSoftness;

            // xy = 世界 XZ 中心，zw = 世界 XZ 半长
            float4 _Boxes[MAX_BOXES];
            int _BoxCount;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.world = worldPos.xz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 到占地的最短距离：取所有矩形的最小值。矩形内部记 0——
                // 「离占地多远」在占地里面没有更近一说
                float d = 1e9;
                for (int b = 0; b < _BoxCount; b++)
                {
                    float2 q = abs(i.world - _Boxes[b].xy) - _Boxes[b].zw;
                    float outside = length(max(q, 0.0));
                    d = min(d, outside);
                }

                if (d > _Radius + _EdgeSoftness)
                {
                    discard;
                }

                float t = saturate(d / max(_Radius, 1e-4));

                // 环体：中心全透，越往外越白
                float fill = pow(t, _FillPower) * _FillAlpha;
                // 亮边：贴着边界那一圈
                float rim = smoothstep(_RimStart, 1.0, t) * _RimAlpha;
                // 外沿羽化，别切出一条硬边
                float fade = 1.0 - smoothstep(_Radius, _Radius + _EdgeSoftness, d);

                float alpha = saturate(fill + rim) * fade * _Color.a;
                return fixed4(_Color.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
