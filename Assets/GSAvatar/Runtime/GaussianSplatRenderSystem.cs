// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace GSAvatar.Runtime
{
    class GaussianSplatRenderSystem
    {
        // ReSharper disable MemberCanBePrivate.Global - used by HDRP/URP features that are not always compiled
        internal static readonly ProfilerMarker s_ProfDraw = new(ProfilerCategory.Render, "GaussianSplat.Draw", MarkerFlags.SampleGPU);
        internal static readonly ProfilerMarker s_ProfCompose = new(ProfilerCategory.Render, "GaussianSplat.Compose", MarkerFlags.SampleGPU);
        internal static readonly ProfilerMarker s_ProfCalcView = new(ProfilerCategory.Render, "GaussianSplat.CalcView", MarkerFlags.SampleGPU);
        // ReSharper restore MemberCanBePrivate.Global

        public static GaussianSplatRenderSystem instance => ms_Instance ??= new GaussianSplatRenderSystem();
        static GaussianSplatRenderSystem ms_Instance;

        readonly Dictionary<GSAvatarRenderer, MaterialPropertyBlock> m_Splats = new();
        readonly HashSet<Camera> m_CameraCommandBuffersDone = new();
        readonly List<(GSAvatarRenderer, MaterialPropertyBlock)> m_ActiveSplats = new();

        CommandBuffer m_CommandBuffer;

        public void RegisterSplat(GSAvatarRenderer r)
        {
            if (m_Splats.Count == 0)
            {
                if (GraphicsSettings.currentRenderPipeline == null)
                    Camera.onPreCull += OnPreCullCamera;
            }

            m_Splats.Add(r, new MaterialPropertyBlock());
        }

        public void UnregisterSplat(GSAvatarRenderer r)
        {
            if (!m_Splats.ContainsKey(r))
                return;
            m_Splats.Remove(r);
            if (m_Splats.Count == 0)
            {
                if (m_CameraCommandBuffersDone != null)
                {
                    if (m_CommandBuffer != null)
                    {
                        foreach (var cam in m_CameraCommandBuffersDone)
                        {
                            if (cam)
                                cam.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, m_CommandBuffer);
                        }
                    }
                    m_CameraCommandBuffersDone.Clear();
                }

                m_ActiveSplats.Clear();
                m_CommandBuffer?.Dispose();
                m_CommandBuffer = null;
                Camera.onPreCull -= OnPreCullCamera;
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global - used by HDRP/URP features that are not always compiled
        public bool GatherSplatsForCamera(Camera cam)
        {
            if (cam.cameraType == CameraType.Preview)
                return false;
            // gather all active & valid splat objects
            m_ActiveSplats.Clear();
            foreach (var kvp in m_Splats)
            {
                var gs = kvp.Key;
                if (gs == null || !gs.isActiveAndEnabled || !gs.HasValidAsset || !gs.HasValidRenderSetup)
                    continue;
                m_ActiveSplats.Add((kvp.Key, kvp.Value));
            }
            if (m_ActiveSplats.Count == 0)
                return false;

            // sort them by depth from camera
            var camTr = cam.transform;
            m_ActiveSplats.Sort((a, b) =>
            {
                var trA = a.Item1.transform;
                var trB = b.Item1.transform;
                var posA = camTr.InverseTransformPoint(trA.position);
                var posB = camTr.InverseTransformPoint(trB.position);
                return posA.z.CompareTo(posB.z);
            });

            return true;
        }

        // ReSharper disable once MemberCanBePrivate.Global - used by HDRP/URP features that are not always compiled
        public Material SortAndRenderSplats(Camera cam, CommandBuffer cmb)
        {
            Material matComposite = null;
            foreach (var kvp in m_ActiveSplats)
            {
                var gs = kvp.Item1;
                matComposite = gs.m_MatComposite;
                var mpb = kvp.Item2;

                // sort
                var matrix = gs.transform.localToWorldMatrix;
                if (gs.m_FrameCounter % gs.m_SortNthFrame == 0)
                    gs.SortPoints(cmb, cam, matrix);
                ++gs.m_FrameCounter;

                // cache view
                kvp.Item2.Clear();
                Material displayMat = gs.m_RenderMode switch
                {
                    GSAvatarRenderer.RenderMode.DebugPoints => gs.m_MatDebugPoints,
                    GSAvatarRenderer.RenderMode.DebugPointIndices => gs.m_MatDebugPoints,
                    GSAvatarRenderer.RenderMode.DebugBoxes => gs.m_MatDebugBoxes,
                    GSAvatarRenderer.RenderMode.DebugChunkBounds => gs.m_MatDebugBoxes,
                    _ => gs.m_MatSplats
                };
                if (displayMat == null)
                    continue;
                        // Use updated buffer if available
                if (gs.updatedPositionsBuffer != null)
                {
                    mpb.SetBuffer("_SplatPos", gs.updatedPositionsBuffer);
                }
                else
                {
                    mpb.SetBuffer("_SplatPos", gs.m_GpuPosData);
                }
                gs.SetAssetDataOnMaterial(mpb);
                mpb.SetBuffer(GSAvatarRenderer.Props.SplatChunks, gs.m_GpuChunks);

                mpb.SetBuffer(GSAvatarRenderer.Props.SplatViewData, gs.m_GpuView);

                mpb.SetBuffer(GSAvatarRenderer.Props.OrderBuffer, gs.m_GpuSortKeys);
                mpb.SetFloat(GSAvatarRenderer.Props.SplatScale, gs.m_SplatScale);
                mpb.SetFloat(GSAvatarRenderer.Props.SplatOpacityScale, gs.m_OpacityScale);
                mpb.SetFloat(GSAvatarRenderer.Props.SplatSize, gs.m_PointDisplaySize);
                mpb.SetInteger(GSAvatarRenderer.Props.SHOrder, gs.m_SHOrder);
                mpb.SetInteger(GSAvatarRenderer.Props.SHOnly, gs.m_SHOnly ? 1 : 0);
                mpb.SetInteger(GSAvatarRenderer.Props.DisplayIndex, gs.m_RenderMode == GSAvatarRenderer.RenderMode.DebugPointIndices ? 1 : 0);
                mpb.SetInteger(GSAvatarRenderer.Props.DisplayChunks, gs.m_RenderMode == GSAvatarRenderer.RenderMode.DebugChunkBounds ? 1 : 0);

                cmb.BeginSample(s_ProfCalcView);
                gs.CalcViewData(cmb, cam, matrix);
                cmb.EndSample(s_ProfCalcView);

                // draw
                int indexCount = 6;
                int instanceCount = gs.splatCount;
                MeshTopology topology = MeshTopology.Triangles;
                if (gs.m_RenderMode is GSAvatarRenderer.RenderMode.DebugBoxes or GSAvatarRenderer.RenderMode.DebugChunkBounds)
                    indexCount = 36;
                if (gs.m_RenderMode == GSAvatarRenderer.RenderMode.DebugChunkBounds)
                    instanceCount = gs.m_GpuChunksValid ? gs.m_GpuChunks.count : 0;

                cmb.BeginSample(s_ProfDraw);
                cmb.DrawProcedural(gs.m_GpuIndexBuffer, matrix, displayMat, 0, topology, indexCount, instanceCount, mpb);
                cmb.EndSample(s_ProfDraw);
            }
            return matComposite;
        }

        // ReSharper disable once MemberCanBePrivate.Global - used by HDRP/URP features that are not always compiled
        // ReSharper disable once UnusedMethodReturnValue.Global - used by HDRP/URP features that are not always compiled
        public CommandBuffer InitialClearCmdBuffer(Camera cam)
        {
            m_CommandBuffer ??= new CommandBuffer {name = "RenderGaussianSplats"};
            if (GraphicsSettings.currentRenderPipeline == null && cam != null && !m_CameraCommandBuffersDone.Contains(cam))
            {
                cam.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, m_CommandBuffer);
                m_CameraCommandBuffersDone.Add(cam);
            }

            // get render target for all splats
            m_CommandBuffer.Clear();
            return m_CommandBuffer;
        }

        void OnPreCullCamera(Camera cam)
        {
            if (!GatherSplatsForCamera(cam))
                return;

            InitialClearCmdBuffer(cam);

            m_CommandBuffer.GetTemporaryRT(GSAvatarRenderer.Props.GaussianSplatRT, -1, -1, 0, FilterMode.Point, GraphicsFormat.R16G16B16A16_SFloat);
            m_CommandBuffer.SetRenderTarget(GSAvatarRenderer.Props.GaussianSplatRT, BuiltinRenderTextureType.CurrentActive);
            m_CommandBuffer.ClearRenderTarget(RTClearFlags.Color, new Color(0, 0, 0, 0), 0, 0);

            // add sorting, view calc and drawing commands for each splat object
            Material matComposite = SortAndRenderSplats(cam, m_CommandBuffer);

            // compose
            m_CommandBuffer.BeginSample(s_ProfCompose);
            m_CommandBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
            m_CommandBuffer.DrawProcedural(Matrix4x4.identity, matComposite, 0, MeshTopology.Triangles, 3, 1);
            m_CommandBuffer.EndSample(s_ProfCompose);
            m_CommandBuffer.ReleaseTemporaryRT(GSAvatarRenderer.Props.GaussianSplatRT);
        }
    }
}