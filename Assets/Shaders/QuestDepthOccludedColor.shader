Shader "GSAC/Quest Depth Occluded Color"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _EnvironmentDepthBias("Environment Depth Bias", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            Blend One OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ HARD_OCCLUSION SOFT_OCCLUSION

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "Packages/com.meta.xr.sdk.core/Shaders/EnvironmentDepth/BiRP/EnvironmentOcclusionBiRP.cginc"

            fixed4 _Color;
            float _EnvironmentDepthBias;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                META_DEPTH_VERTEX_OUTPUT(2)
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                META_DEPTH_INITIALIZE_VERTEX_OUTPUT(o, v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half3 normal = normalize(i.worldNormal);
                half ndotl = saturate(dot(normal, normalize(_WorldSpaceLightPos0.xyz)));
                half3 litColor = _Color.rgb * (UNITY_LIGHTMODEL_AMBIENT.rgb + _LightColor0.rgb * (0.35 + 0.65 * ndotl));
                fixed4 color = fixed4(litColor, _Color.a);
                META_DEPTH_OCCLUDE_OUTPUT_PREMULTIPLY(i, color, _EnvironmentDepthBias);
                return color;
            }
            ENDCG
        }
    }

    FallBack Off
}
