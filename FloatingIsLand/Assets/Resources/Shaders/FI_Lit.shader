Shader "FI_Lit"
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
        _HighlightStrength ("Face Highlight", Range(0, 2)) = 0.35
        _HighlightSize ("Face Highlight Size", Range(8, 128)) = 42
        _BevelHighlight ("Bevel Highlight", Range(0, 2)) = 0.85
        _BevelSharpness ("Bevel Sharpness", Range(2, 64)) = 18
        _BevelWidth ("Fake Bevel Width", Range(0, 4)) = 1.4
        _RimStrength ("Rim Highlight", Range(0, 1)) = 0.12
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLowpoly"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "FI_LitCore.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD2;
                float3 worldNormal : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
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

    FallBack "Unlit/Texture"
    CustomEditor "FI_LitShaderGUI"
}
