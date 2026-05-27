// SPDX-License-Identifier: MIT
Shader "Hidden/Gaussian Splatting/Depth Mask"
{
    Properties
    {
        _DepthMaskAlphaThreshold ("Alpha Threshold", Float) = 0.01
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-100" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off
            Offset 1, 1

CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma require compute
#pragma use_dxc

StructuredBuffer<uint> _OrderBuffer;

struct SplatViewData
{
    float4 pos;
    float2 axis1;
    float2 axis2;
    uint2 color;
};

struct v2f
{
    half alpha : TEXCOORD0;
    float2 pos : TEXCOORD1;
    float4 vertex : SV_POSITION;
};

StructuredBuffer<SplatViewData> _SplatViewData;
float _DepthMaskAlphaThreshold;

v2f vert(uint vtxID : SV_VertexID, uint instID : SV_InstanceID)
{
    v2f o = (v2f)0;
    instID = _OrderBuffer[instID];
    SplatViewData view = _SplatViewData[instID];
    float4 centerClipPos = view.pos;

    if (centerClipPos.w <= 0)
    {
        o.vertex = asfloat(0x7fc00000);
        return o;
    }

    o.alpha = f16tof32(view.color.y);

    uint idx = vtxID;
    float2 quadPos = float2(idx & 1, (idx >> 1) & 1) * 2.0 - 1.0;
    quadPos *= 2;
    o.pos = quadPos;

    float2 deltaScreenPos = (quadPos.x * view.axis1 + quadPos.y * view.axis2) * 2 / _ScreenParams.xy;
    o.vertex = centerClipPos;
    o.vertex.xy += deltaScreenPos * centerClipPos.w;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    half alpha = exp(-dot(i.pos, i.pos)) * i.alpha;
    clip(alpha - _DepthMaskAlphaThreshold);
    return 0;
}
ENDCG
        }
    }
}
