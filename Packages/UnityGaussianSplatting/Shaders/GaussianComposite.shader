// SPDX-License-Identifier: MIT
Shader "Hidden/Gaussian Splatting/Composite"
{
    SubShader
    {
CGINCLUDE
#include "UnityCG.cginc"

struct v2f
{
    float4 vertex : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert (uint vtxID : SV_VertexID)
{
    v2f o = (v2f)0;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    float2 quadPos = float2(vtxID&1, (vtxID>>1)&1) * 4.0 - 1.0;
	o.vertex = float4(quadPos, 1, 1);
    return o;
}

Texture2D _GaussianSplatRT;

half4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 col = _GaussianSplatRT.Load(int3(i.vertex.xy, 0));
    col.rgb = GammaToLinearSpace(col.rgb);
    col.a = saturate(col.a * 1.5);
    return col;
}

ENDCG

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma require compute
#pragma use_dxc
ENDCG
        }
    }
}
