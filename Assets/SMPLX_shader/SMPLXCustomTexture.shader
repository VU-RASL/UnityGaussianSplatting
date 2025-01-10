Shader "Custom/RenderUVWithVerticesAndBuffer"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {

            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma target 5.0 // Ensure Shader Model 5.0 support
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            float4 _Color;

            // StructuredBuffer for vertex position output
            RWStructuredBuffer<float3> _VertexOutput;

            // struct appdata_full
            // {
            //     float4 vertex : POSITION;
            //     float2 texcoord : TEXCOORD0;
            // };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata_full v, uint vertexID : SV_VertexID)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex); // Project to clip space
                o.uv = v.texcoord;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // Transform to world space

                // Write vertex position to the buffer
                _VertexOutput[vertexID] = o.worldPos;

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 tex = tex2D(_MainTex, i.uv);
// 
                
                return half4(tex.rgb * _Color.rgb, tex.a);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
