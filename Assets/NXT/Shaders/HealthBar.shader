Shader "NXT/HealthBar"
{
    Properties
    {
        _Fill ("Fill", range(0,1)) = 1
        _Color ("Color", color) = (0, 1, 0, 1)
        _Color2 ("Color 2", color) = (1, 0, 0, 1)
        _BgColor ("Background Color", color) = (0.2, 0.2, 0.2, 1)
    }
    SubShader
    {
        Tags
        {
            "Queue"="Overlay" "RenderType"="Opaque"
        }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float health : FLOAT;
                // INSTANCE ....
            };

            UNITY_INSTANCING_BUFFER_START(Prop)
            UNITY_DEFINE_INSTANCED_PROP(float, _Fill)
            UNITY_INSTANCING_BUFFER_END(Prop)
            fixed4 _Color;
            fixed4 _Color2;
            fixed4 _BgColor;

            v2f vert(appdata v)
            {                
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                const float fill = UNITY_ACCESS_INSTANCED_PROP(Prop, _Fill);
                float scale_x = length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x));
                float scale_y = length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y));
                
                o.vertex = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_MV, float4(0.0, 0.0, 0.0, 1.0)) + float4(v.vertex.x, v.vertex.y, 0.0, 1.0) * float4(scale_x, scale_y, 1.0, 0.0));
                o.uv = v.uv;
                o.health = fill;
                o.uv.x -= fill;
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = lerp(_Color, _Color2, 1 - i.health);
                return lerp(col, _BgColor, i.uv.x > 0);
            }
            ENDCG
        }
    }
}