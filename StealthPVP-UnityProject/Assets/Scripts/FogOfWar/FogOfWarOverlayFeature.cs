using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System;

/// <summary>
/// URP renderer feature that draws the fog overlay as a full-screen pass using depth to reconstruct world XZ.
/// Add this feature to your URP Renderer asset and assign the fog overlay material.
/// </summary>
public class FogOfWarOverlayFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material overlayMaterial;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
    }

    private class FogOfWarOverlayPass : ScriptableRenderPass
    {
        private readonly Material material;
        private static readonly ProfilingSampler sampler = new ProfilingSampler("FogOfWarOverlay");
        private RTHandle cameraColorTarget;

        public FogOfWarOverlayPass(Material material, RenderPassEvent passEvent)
        {
            this.material = material;
            renderPassEvent = passEvent;
            ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            cameraColorTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;
            ConfigureTarget(cameraColorTarget);
            ConfigureClear(ClearFlag.None, Color.black);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null)
            {
                return;
            }

            Camera cam = renderingData.cameraData.camera;
            FogOfWarCameraBinder binder = cam ? cam.GetComponent<FogOfWarCameraBinder>() : null;
            FogOfWarManager fog = binder ? binder.FogManager : null;
            if (!fog)
            {
                return;
            }

            fog.PushShaderProperties();

            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, sampler))
            {
                cmd.SetRenderTarget(cameraColorTarget);
                CoreUtils.DrawFullScreen(cmd, material);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private class PassData
        {
            public Material material;
            public FogOfWarManager fog;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
            {
                return;
            }

            const string passName = "FogOfWarOverlay";
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            Camera cam = cameraData?.camera;
            FogOfWarCameraBinder binder = cam ? cam.GetComponent<FogOfWarCameraBinder>() : null;
            FogOfWarManager fog = binder ? binder.FogManager : null;
            if (!fog)
            {
                return;
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData, sampler))
            {
                passData.material = material;
                passData.fog = fog;

                // Target the active color attachment, keep depth read-only for depth texture sampling.
                builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Read);
                builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    var cmd = context.cmd;
                    using (new ProfilingScope(cmd, sampler))
                    {
                        data.fog.PushShaderProperties();
                        CoreUtils.DrawFullScreen(cmd, data.material);
                    }
                });
            }
        }
    }

    public Settings settings = new Settings();
    private FogOfWarOverlayPass overlayPass;

    public override void Create()
    {
        overlayPass = new FogOfWarOverlayPass(settings.overlayMaterial, settings.renderPassEvent);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.overlayMaterial == null)
        {
            return;
        }

        renderer.EnqueuePass(overlayPass);
    }
}
