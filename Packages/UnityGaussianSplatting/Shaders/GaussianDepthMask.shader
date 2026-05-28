// SPDX-License-Identifier: MIT
Shader "Hidden/Gaussian Splatting/Depth Mask"
{
    Properties
    {
        _DepthMaskAlphaThreshold ("Alpha Threshold", Float) = 0.01
        _DepthMaskLocalAlphaEpsilon ("Local Alpha Epsilon", Float) = 0.0039215686
        _DepthMaskEdgeShrinkPixels ("Edge Shrink Pixels", Float) = 1.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-100" }

        Pass
        {
            ZWrite Off
            ZTest Always
            ColorMask A
            Blend OneMinusDstAlpha One
            Cull Off

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
float _GaussianSplatClipFlipY;

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
    if (_GaussianSplatClipFlipY > 0.5)
        o.vertex.y = -o.vertex.y;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    half alpha = exp(-dot(i.pos, i.pos)) * i.alpha;
    if (alpha < 1.0 / 255.0)
        discard;
    return half4(0, 0, 0, alpha);
}
ENDCG
        }

        Pass
        {
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Blend Zero One
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
sampler2D _GaussianDepthCoverageRT;
float _DepthMaskAlphaThreshold;
float _DepthMaskLocalAlphaEpsilon;
float _DepthMaskEdgeShrinkPixels;
float _GaussianSplatClipFlipY;

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
    if (_GaussianSplatClipFlipY > 0.5)
        o.vertex.y = -o.vertex.y;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    half localAlpha = exp(-dot(i.pos, i.pos)) * i.alpha;
    clip(localAlpha - _DepthMaskLocalAlphaEpsilon);

    float2 uv = i.vertex.xy / _ScreenParams.xy;
    float2 texel = _DepthMaskEdgeShrinkPixels / _ScreenParams.xy;
    half accumulatedAlpha = tex2D(_GaussianDepthCoverageRT, uv).a;
    accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(texel.x, 0)).a);
    accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - float2(texel.x, 0)).a);
    accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(0, texel.y)).a);
    accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - float2(0, texel.y)).a);
    half alpha = accumulatedAlpha;
    clip(alpha - _DepthMaskAlphaThreshold);
    return 0;
}
ENDCG
        }
    }
}
