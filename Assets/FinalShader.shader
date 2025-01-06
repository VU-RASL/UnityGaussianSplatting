Shader "Custom/FinalShader"
{
    Properties {
            _MainTex1 ("Texture 1", 2D) = "white" {}
            _MainTex2 ("Texture 2", 2D) = "white" {}
            _Alpha ("Alpha", Range(0, 1)) = 0.5
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex1;
            sampler2D _MainTex2;
            float _Alpha;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target {
                float4 color1 = tex2D(_MainTex1, i.uv);
                float4 color2 = tex2D(_MainTex2, i.uv);
                return lerp(color1, color2, _Alpha);
            }
            ENDCG
        }
    }
}
