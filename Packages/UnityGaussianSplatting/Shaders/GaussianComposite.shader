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
float _GaussianSceneDepthAlphaThreshold;

half4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 col = _GaussianSplatRT.Load(int3(i.vertex.xy, 0));
    col.rgb = GammaToLinearSpace(col.rgb);
    col.a = saturate(col.a * 1.5);
    return col;
}

v2f vertNearDepth(uint vtxID : SV_VertexID)
{
    v2f o = vert(vtxID);
#if defined(UNITY_REVERSED_Z)
    o.vertex.z = o.vertex.w;
#else
    o.vertex.z = UNITY_NEAR_CLIP_VALUE * o.vertex.w;
#endif
    return o;
}

half4 fragDepthFromAlpha(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half4 col = _GaussianSplatRT.Load(int3(i.vertex.xy, 0));
    clip(col.a - _GaussianSceneDepthAlphaThreshold);
    return 0;
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

        Pass
        {
            ZWrite On
            ZTest Always
            ColorMask 0
            Cull Off
            Blend Zero One

CGPROGRAM
#pragma vertex vertNearDepth
#pragma fragment fragDepthFromAlpha
#pragma require compute
#pragma use_dxc
ENDCG
        }
    }
}
