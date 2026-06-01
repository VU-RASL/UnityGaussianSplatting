// SPDX-License-Identifier: MIT
Shader "Hidden/Gaussian Splatting/Depth Mask"
{
    Properties
    {
        _DepthMaskAlphaThreshold ("Alpha Threshold", Float) = 0.01
        _DepthMaskLocalAlphaEpsilon ("Local Alpha Epsilon", Float) = 0.0039215686
        _DepthMaskEdgeShrinkPixels ("Edge Shrink Pixels", Float) = 1.0
        _DepthMaskUseRawSplatData ("Use Raw Splat Data", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-100" }

CGINCLUDE
#include "UnityCG.cginc"
#include "GaussianSplatting.hlsl"

StructuredBuffer<uint> _OrderBuffer;

struct v2f
{
    half alpha : TEXCOORD0;
    float2 pos : TEXCOORD1;
    float4 vertex : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

StructuredBuffer<SplatViewData> _SplatViewData;
sampler2D _GaussianDepthCoverageRT;
float _DepthMaskAlphaThreshold;
float _DepthMaskLocalAlphaEpsilon;
float _DepthMaskEdgeShrinkPixels;
float _DepthMaskUseRawSplatData;
float _SplatScale;
float _SplatOpacityScale;
float _GaussianSplatClipFlipY;

void DecomposeCovarianceForDepth(float3 cov2d, out float2 v1, out float2 v2)
{
    float diag1 = cov2d.x, diag2 = cov2d.z, offDiag = cov2d.y;
    float mid = 0.5f * (diag1 + diag2);
    float radius = length(float2((diag1 - diag2) / 2.0, offDiag));
    float lambda1 = mid + radius;
    float lambda2 = max(mid - radius, 0.1);
    float2 diagVec = normalize(float2(offDiag, lambda1 - diag1));
    diagVec.y = -diagVec.y;
    float maxSize = 4096.0;
    v1 = min(sqrt(2.0 * lambda1), maxSize) * diagVec;
    v2 = min(sqrt(2.0 * lambda2), maxSize) * float2(diagVec.y, -diagVec.x);
}

v2f vert(uint vtxID : SV_VertexID, uint instID : SV_InstanceID)
{
    v2f o = (v2f)0;
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    instID = _OrderBuffer[instID];

    float4 centerClipPos;
    float2 axis1;
    float2 axis2;

    if (_DepthMaskUseRawSplatData > 0.5)
    {
        SplatData splat = LoadSplatData(instID);
        float3 centerWorldPos = mul(unity_ObjectToWorld, float4(splat.pos, 1)).xyz;
        centerClipPos = mul(UNITY_MATRIX_VP, float4(centerWorldPos, 1));

        float3x3 splatRotScaleMat = CalcMatrixFromRotationScale(splat.rot, splat.scale);
        float3 cov3d0, cov3d1;
        CalcCovariance3D(splatRotScaleMat, cov3d0, cov3d1);

        float splatScale2 = _SplatScale * _SplatScale;
        cov3d0 *= splatScale2;
        cov3d1 *= splatScale2;

        float3 cov2d = CalcCovariance2D(splat.pos, cov3d0, cov3d1, UNITY_MATRIX_MV, UNITY_MATRIX_P, _ScreenParams);
        DecomposeCovarianceForDepth(cov2d, axis1, axis2);
        o.alpha = min(splat.opacity * _SplatOpacityScale, 65000);
    }
    else
    {
        SplatViewData view = _SplatViewData[instID];
        centerClipPos = view.pos;
        axis1 = view.axis1;
        axis2 = view.axis2;
        o.alpha = f16tof32(view.color.y);
    }

    if (centerClipPos.w <= 0)
    {
        o.vertex = asfloat(0x7fc00000);
        return o;
    }

    uint idx = vtxID;
    float2 quadPos = float2(idx & 1, (idx >> 1) & 1) * 2.0 - 1.0;
    quadPos *= 2;
    o.pos = quadPos;

    float2 deltaScreenPos = (quadPos.x * axis1 + quadPos.y * axis2) * 2 / _ScreenParams.xy;
    o.vertex = centerClipPos;
    o.vertex.xy += deltaScreenPos * centerClipPos.w;
    if (_GaussianSplatClipFlipY > 0.5)
        o.vertex.y = -o.vertex.y;
    return o;
}

half4 fragCoverage(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half alpha = exp(-dot(i.pos, i.pos)) * i.alpha;
    if (alpha < 1.0 / 255.0)
        discard;
    return half4(0, 0, 0, alpha);
}

half4 fragDepth(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half localAlpha = exp(-dot(i.pos, i.pos)) * i.alpha;
    clip(localAlpha - _DepthMaskLocalAlphaEpsilon);

    if (_DepthMaskUseRawSplatData > 0.5)
        return 0;

    float2 uv = i.vertex.xy / _ScreenParams.xy;
    half accumulatedAlpha = tex2D(_GaussianDepthCoverageRT, uv).a;
    float2 texel = abs(_DepthMaskEdgeShrinkPixels) / _ScreenParams.xy;
    if (_DepthMaskEdgeShrinkPixels > 0.0)
    {
        accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(texel.x, 0)).a);
        accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - float2(texel.x, 0)).a);
        accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(0, texel.y)).a);
        accumulatedAlpha = min(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - float2(0, texel.y)).a);
    }
    else if (_DepthMaskEdgeShrinkPixels < 0.0)
    {
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(texel.x, 0)).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - float2(texel.x, 0)).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(0, texel.y)).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - float2(0, texel.y)).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + texel).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv - texel).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(texel.x, -texel.y)).a);
        accumulatedAlpha = max(accumulatedAlpha, tex2D(_GaussianDepthCoverageRT, uv + float2(-texel.x, texel.y)).a);
    }

    clip(accumulatedAlpha - _DepthMaskAlphaThreshold);
    return 0;
}

half4 fragDirectDepth(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    half localAlpha = exp(-dot(i.pos, i.pos)) * i.alpha;
    clip(localAlpha - _DepthMaskLocalAlphaEpsilon);
    return 0;
}
ENDCG

        Pass
        {
            ZWrite Off
            ZTest Always
            ColorMask A
            Blend OneMinusDstAlpha One
            Cull Off

CGPROGRAM
#pragma vertex vert
#pragma fragment fragCoverage
#pragma require compute
#pragma use_dxc
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
#pragma fragment fragDepth
#pragma require compute
#pragma use_dxc
ENDCG
        }

        Pass
        {
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Blend Zero One
            Cull Off

CGPROGRAM
#pragma vertex vert
#pragma fragment fragDirectDepth
#pragma require compute
#pragma use_dxc
ENDCG
        }
    }
}
