// 摆放预览的「虚影」：保留建筑原材质的贴图与主色，套一层半透明 + 菲涅尔边缘光 + 扫描线，
// 再按合法性整体染绿 / 染红。不是把模型换成一片纯色——那样玩家看不出摆的是哪栋建筑，
// 也看不出转了没有（形状之外的门窗、屋顶、朝向全丢了）。
//
// 没写 LightMode 标签：Built-in 前向渲染下按 Unlit 走，URP 下会被当成 SRPDefaultUnlit，
// 本工程当前是 Built-in（ProjectSettings 里没有管线资产），但两边都能出图。
//
// 放在 Resources 下是为了保证进包：Shader.Find 在 Build 里只找得到被引用/被强制包含的 shader。
Shader "FloatingIsLand/BuildGhost"
{
    Properties
    {
        _MainTex ("原材质贴图", 2D) = "white" {}
        _Color ("原材质主色", Color) = (1, 1, 1, 1)
        _TintColor ("虚影染色（绿=可建 / 红=不可建）", Color) = (0.35, 1, 0.55, 1)
        _TintStrength ("染色强度", Range(0, 1)) = 0.45
        _Alpha ("整体不透明度", Range(0, 1)) = 0.55
        _RimPower ("边缘光收束", Range(0.5, 8)) = 2.5
        _RimStrength ("边缘光强度", Range(0, 4)) = 1.8
        _ScanDensity ("扫描线密度（每世界单位）", Float) = 3
        _ScanSpeed ("扫描线滚动速度", Float) = 1.2
        _ScanStrength ("扫描线强度", Range(0, 1)) = 0.2
    }

    SubShader
    {
        // Transparent+1：排在落点格标记（Transparent）之后，虚影才能盖在格子上而不是被格子盖住
        Tags
        {
            "Queue" = "Transparent+1"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldView : TEXCOORD2;
                float worldY : TEXCOORD3;
                fixed4 color : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TintColor;
            float _TintStrength;
            float _Alpha;
            float _RimPower;
            float _RimStrength;
            float _ScanDensity;
            float _ScanSpeed;
            float _ScanStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldView = _WorldSpaceCameraPos - worldPos;
                o.worldY = worldPos.y;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color * i.color;

                // 只往虚影色偏一部分，原材质的固有色仍然认得出来
                fixed3 body = lerp(albedo.rgb, _TintColor.rgb, _TintStrength);

                // 菲涅尔：正对镜头的面淡、掠射的轮廓亮。虚影的体积感全靠这一项，
                // 少了它半透明模型会糊成一团看不出边界
                float ndotv = saturate(dot(normalize(i.worldNormal), normalize(i.worldView)));
                float rim = pow(saturate(1.0 - ndotv), _RimPower);

                // 沿世界高度滚动的扫描线，让「这是预览不是实体」这件事一眼可辨
                float scan = (sin(i.worldY * _ScanDensity - _Time.y * _ScanSpeed) * 0.5 + 0.5) * _ScanStrength;

                fixed3 rgb = body + _TintColor.rgb * (rim * _RimStrength + scan);
                float alpha = saturate(_Alpha + rim * _RimStrength * 0.35 + scan) * albedo.a * _TintColor.a;
                return fixed4(rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
