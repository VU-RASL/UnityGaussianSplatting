// SMPLXMeshShader.hlsl
Shader "Custom/SMPLXMeshShader"
{
    Properties
    {
        _Color ("Color", Color) = (1,0,0,1)
        _Offset("Position Offset", Vector) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            // Vertex buffer from CPU
            StructuredBuffer<float3> _VertexBuffer;

            uniform float4 _Offset;

            struct appdata
            {
                uint vertexID : SV_VertexID; // Used to fetch vertices from the buffer
                float4 pos : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            { 
                v2f o;

                o.pos = UnityObjectToClipPos(v.pos+_Offset);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Simple flat color for now
                return fixed4(1, 1, 0, 1); // White
            }
            ENDCG
        }
    }
}
