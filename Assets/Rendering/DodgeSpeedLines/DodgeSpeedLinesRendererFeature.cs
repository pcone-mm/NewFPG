using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace NewFPG.Rendering
{
    /// <summary>
    /// Draws a radial, procedural speed-line overlay from <see cref="DodgeSpeedLinesVolume"/>.
    /// </summary>
    public sealed class DodgeSpeedLinesRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("Shader used to draw the speed-line overlay.")]
            public Shader shader;

            [Tooltip("Draw after the camera stack has finished its post processing.")]
            public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;

            public bool affectSceneView;
        }

        [SerializeField] private Settings settings = new Settings();

        private Material material;
        private DodgeSpeedLinesPass pass;

        public override void Create()
        {
            DisposeMaterial();

            if (settings.shader == null)
            {
                pass = null;
                return;
            }

            material = CoreUtils.CreateEngineMaterial(settings.shader);
            pass = new DodgeSpeedLinesPass(material, settings)
            {
                renderPassEvent = settings.injectionPoint
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            CameraData cameraData = renderingData.cameraData;
            if (pass == null || material == null || cameraData.cameraType == CameraType.Preview ||
                cameraData.cameraType == CameraType.Reflection || !cameraData.resolveFinalTarget)
            {
                return;
            }

            if (!settings.affectSceneView && cameraData.isSceneViewCamera)
            {
                return;
            }

            DodgeSpeedLinesVolume volume = VolumeManager.instance.stack.GetComponent<DodgeSpeedLinesVolume>();
            if (volume == null || !volume.IsActive())
            {
                return;
            }

            pass.renderPassEvent = settings.injectionPoint;
            pass.UpdateSettings(volume);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose();
            pass = null;
            DisposeMaterial();
        }

        private void DisposeMaterial()
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class DodgeSpeedLinesPass : ScriptableRenderPass
        {
            private static readonly int OpacityId = Shader.PropertyToID("_Opacity");
            private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
            private static readonly int AnimationSpeedId = Shader.PropertyToID("_AnimationSpeed");
            private static readonly int LineThresholdId = Shader.PropertyToID("_LineThreshold");
            private static readonly int RadialScaleId = Shader.PropertyToID("_RadialScale");
            private static readonly int LineColorId = Shader.PropertyToID("_LineColor");

            private readonly Material material;
            private readonly Settings settings;
            private RTHandle temporaryColorTexture;

            public DodgeSpeedLinesPass(Material material, Settings settings)
            {
                this.material = material;
                this.settings = settings;
                profilingSampler = new ProfilingSampler("Dodge Speed Lines");
            }

            public void UpdateSettings(DodgeSpeedLinesVolume volume)
            {
                material.SetFloat(OpacityId, volume.opacity.value * volume.intensity.value);
                material.SetFloat(NoiseScaleId, volume.noiseScale.value);
                material.SetFloat(AnimationSpeedId, volume.animationSpeed.value);
                material.SetFloat(LineThresholdId, volume.lineThreshold.value);
                material.SetFloat(RadialScaleId, volume.radialScale.value);
                material.SetColor(LineColorId, volume.lineColor.value);
            }

#if URP_COMPATIBILITY_MODE
#pragma warning disable CS0618
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref temporaryColorTexture,
                    descriptor,
                    FilterMode.Bilinear,
                    TextureWrapMode.Clamp,
                    name: "_DodgeSpeedLinesTemporaryColor");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (temporaryColorTexture == null || material == null)
                {
                    return;
                }

                RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    Blitter.BlitCameraTexture(cmd, source, temporaryColorTexture);
                    Blitter.BlitCameraTexture(cmd, temporaryColorTexture, source, material, 0);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618
#endif

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle source = resourceData.activeColorTexture;
                if (!source.IsValid() || material == null)
                {
                    return;
                }

                TextureDesc temporaryDescriptor = renderGraph.GetTextureDesc(source);
                temporaryDescriptor.name = "_DodgeSpeedLinesTemporaryColor";
                temporaryDescriptor.clearBuffer = false;
                TextureHandle temporaryColor = renderGraph.CreateTexture(temporaryDescriptor);

                RenderGraphUtils.BlitMaterialParameters copyParameters = new(
                    source,
                    temporaryColor,
                    Blitter.GetBlitMaterial(TextureDimension.Tex2D),
                    0);
                renderGraph.AddBlitPass(copyParameters, "Dodge Speed Lines Copy Color");

                RenderGraphUtils.BlitMaterialParameters effectParameters = new(
                    temporaryColor,
                    source,
                    material,
                    0);
                renderGraph.AddBlitPass(effectParameters, "Dodge Speed Lines");
            }

            public void Dispose()
            {
                temporaryColorTexture?.Release();
                temporaryColorTexture = null;
            }
        }
    }
}
