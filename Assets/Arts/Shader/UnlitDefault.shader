Shader "Unlit/UnlitDefault"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}  // Base texture
        _BaseColor ("Tint Color", Color) = (1,1,1,1) // Default color
        [Toggle] _UseGPUInstancing ("Enable GPU Instancing", Float) = 1  // GPU Instancing Toggle
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {   
            ZWrite On 
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR; 

                UNITY_VERTEX_INPUT_INSTANCE_ID  
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(0)
                fixed4 color : COLOR; 

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _BaseColor; 

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _Color) 
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex); 
                o.color = v.color; 

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {                
                UNITY_SETUP_INSTANCE_ID(i);
                fixed4 texColor = tex2D(_MainTex, i.uv); 

                fixed4 instancedColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);

                instancedColor = instancedColor == fixed4(0,0,0,0) ? fixed4(1,1,1,1) : fixed4(1,1,1,1);

                fixed4 col = texColor * i.color;

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
