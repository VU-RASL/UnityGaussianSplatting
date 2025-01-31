Shader "Custom/RenderUVWithVerticesAndBuffer"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Color Tint", Color) = (1, 1, 1, 1)
        _GaussianMask ("Gaussian Mask", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        Pass
        {
           Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            // ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            sampler2D _MainTex;
            sampler2D _GaussianMask;
            float4 _Color;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert (appdata_full v, uint vertexID : SV_VertexID)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
   

                half4 tex = tex2D(_MainTex, i.uv);
                return half4(tex.rgb , 1);
            }

            ENDCG
        }
    }
    FallBack "Diffuse"
}
