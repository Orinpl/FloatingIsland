// 世界空间飘分数字（TextMesh 用）。等价于内置的 GUI/Text Shader，但 ZTest Always ——
// 数字标在建筑头顶，被山体或别的建筑挡住就白标了，这类提示一律画在场景几何之上。
//
// 动态字体的图集只有 alpha 通道有效，颜色必须自己给：所以采样只取 .a，再乘顶点色。
// 队列 Transparent+3：排在辉光（+2）之后，数字永远在最上层。
Shader "FloatingIsLand/WorldLabel"
{
    Properties
    {
        _MainTex ("字体图集", 2D) = "white" {}
        _Color ("整体染色", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+3"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 动态字体图集只有 alpha 有效，rgb 全取顶点色
                fixed alpha = tex2D(_MainTex, i.uv).a;
                return fixed4(i.color.rgb, i.color.a * alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
