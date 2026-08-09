// 落点格标记：一批贴地的方片，颜色全走顶点色（绿=可建 / 红=挡住了）。
//
// 关键是 ZTest Always：建筑模型正好压在自己的落点格上，用默认的 LEqual 会被模型挡掉大半，
// 而「哪几格被占了」恰恰是玩家最需要看清的信息，所以这层标记一律画在场景几何之上。
// 队列停在 Transparent（虚影是 Transparent+1），所以虚影仍然盖在标记上面，不会互相打架。
//
// 放在 Resources 下是为了保证进包（同 BuildGhost.shader）。
Shader "FloatingIsLand/CellOverlay"
{
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
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
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }

    Fallback Off
}
