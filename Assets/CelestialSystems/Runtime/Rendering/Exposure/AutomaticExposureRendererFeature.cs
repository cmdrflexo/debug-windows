/*
 * Meters HDR camera luminance with a GPU histogram and applies temporally adapted exposure before URP post-processing.
 */

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace jcan.CelestialSystems
{
    public sealed class AutomaticExposureRendererFeature :
        ScriptableRendererFeature
    {
        [Header("Shaders")]
        [SerializeField]
        private ComputeShader exposureComputeShader;

        [SerializeField]
        private Shader exposureShader;

        private AutomaticExposurePass exposurePass;

        public override void Create()
        {
            exposurePass?.Dispose();
            exposurePass =
                null;

            if (exposureComputeShader == null ||
                exposureShader == null)
            {
                return;
            }

            exposurePass =
                new AutomaticExposurePass(
                    exposureComputeShader,
                    exposureShader);
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            if (exposurePass == null ||
                !SystemInfo.supportsComputeShaders ||
                renderingData.cameraData.cameraType !=
                    CameraType.Game ||
                renderingData.cameraData.renderType !=
                    CameraRenderType.Base ||
                renderingData.cameraData.camera.stereoEnabled)
            {
                return;
            }

            renderer.EnqueuePass(
                exposurePass);
        }

        protected override void Dispose(
            bool disposing)
        {
            exposurePass?.Dispose();
            exposurePass =
                null;
        }

        private sealed class AutomaticExposurePass :
            ScriptableRenderPass
        {
            private const int HistogramBinCount =
                256;

            private const int MeteringWidth =
                256;

            private const int MeteringHeight =
                144;

            private static readonly int HistogramId =
                Shader.PropertyToID(
                    "_Histogram");

            private static readonly int ExposureStateId =
                Shader.PropertyToID(
                    "_ExposureState");

            private static readonly int MeteringTextureId =
                Shader.PropertyToID(
                    "_MeteringTexture");

            private static readonly int MeteringWidthId =
                Shader.PropertyToID(
                    "_MeteringWidth");

            private static readonly int MeteringHeightId =
                Shader.PropertyToID(
                    "_MeteringHeight");

            private static readonly int HistogramMinimumLogId =
                Shader.PropertyToID(
                    "_HistogramMinimumLog");

            private static readonly int HistogramMaximumLogId =
                Shader.PropertyToID(
                    "_HistogramMaximumLog");

            private static readonly int LowPercentId =
                Shader.PropertyToID(
                    "_LowPercent");

            private static readonly int HighPercentId =
                Shader.PropertyToID(
                    "_HighPercent");

            private static readonly int MiddleGrayId =
                Shader.PropertyToID(
                    "_MiddleGray");

            private static readonly int CompensationId =
                Shader.PropertyToID(
                    "_Compensation");

            private static readonly int MinimumExposureId =
                Shader.PropertyToID(
                    "_MinimumExposure");

            private static readonly int MaximumExposureId =
                Shader.PropertyToID(
                    "_MaximumExposure");

            private static readonly int BrightenSpeedId =
                Shader.PropertyToID(
                    "_BrightenSpeed");

            private static readonly int DarkenSpeedId =
                Shader.PropertyToID(
                    "_DarkenSpeed");

            private static readonly int DeltaTimeId =
                Shader.PropertyToID(
                    "_DeltaTime");

            private readonly ComputeShader computeShader;
            private readonly Material exposureMaterial;
            private readonly GraphicsBuffer histogramBuffer;
            private readonly GraphicsBuffer exposureStateBuffer;
            private readonly int clearHistogramKernel;
            private readonly int buildHistogramKernel;
            private readonly int computeExposureKernel;

            public AutomaticExposurePass(
                ComputeShader exposureComputeShader,
                Shader exposureShader)
            {
                computeShader =
                    exposureComputeShader;
                exposureMaterial =
                    CoreUtils.CreateEngineMaterial(
                        exposureShader);
                histogramBuffer =
                    new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        HistogramBinCount,
                        sizeof(uint));
                exposureStateBuffer =
                    new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        4,
                        sizeof(float));
                exposureStateBuffer.SetData(
                    new[]
                    {
                        0.0f,
                        0.0f,
                        0.0f,
                        -1.0f
                    });
                exposureMaterial.SetBuffer(
                    ExposureStateId,
                    exposureStateBuffer);

                clearHistogramKernel =
                    computeShader.FindKernel(
                        "ClearHistogram");
                buildHistogramKernel =
                    computeShader.FindKernel(
                        "BuildHistogram");
                computeExposureKernel =
                    computeShader.FindKernel(
                        "ComputeExposure");
                renderPassEvent =
                    RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void Dispose()
            {
                histogramBuffer?.Dispose();
                exposureStateBuffer?.Dispose();
                CoreUtils.Destroy(
                    exposureMaterial);
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                var settings =
                    VolumeManager.instance.stack
                        .GetComponent<AutomaticExposure>();

                if (settings == null ||
                    !settings.IsActive())
                {
                    return;
                }

                var resourceData =
                    frameData.Get<UniversalResourceData>();

                if (resourceData.isActiveTargetBackBuffer)
                {
                    return;
                }

                var source =
                    resourceData.activeColorTexture;

                if (!source.IsValid())
                {
                    return;
                }

                var meteringDescriptor =
                    renderGraph.GetTextureDesc(
                        source);
                meteringDescriptor.name =
                    "Automatic Exposure Metering";
                meteringDescriptor.width =
                    MeteringWidth;
                meteringDescriptor.height =
                    MeteringHeight;
                meteringDescriptor.depthBufferBits =
                    0;
                meteringDescriptor.msaaSamples =
                    MSAASamples.None;
                meteringDescriptor.format =
                    GraphicsFormat.R16_SFloat;
                meteringDescriptor.clearBuffer =
                    false;
                var meteringTexture =
                    renderGraph.CreateTexture(
                        meteringDescriptor);

                var meteringParameters =
                    new RenderGraphUtils.BlitMaterialParameters(
                        source,
                        meteringTexture,
                        exposureMaterial,
                        0);
                renderGraph.AddBlitPass(
                    meteringParameters,
                    "Automatic Exposure Metering");

                var histogramHandle =
                    renderGraph.ImportBuffer(
                        histogramBuffer);
                var exposureStateHandle =
                    renderGraph.ImportBuffer(
                        exposureStateBuffer);

                using (var builder =
                    renderGraph.AddComputePass<HistogramPassData>(
                        "Automatic Exposure Histogram",
                        out var passData))
                {
                    passData.computeShader =
                        computeShader;
                    passData.meteringTexture =
                        meteringTexture;
                    passData.histogram =
                        histogramHandle;
                    passData.exposureState =
                        exposureStateHandle;
                    passData.clearHistogramKernel =
                        clearHistogramKernel;
                    passData.buildHistogramKernel =
                        buildHistogramKernel;
                    passData.computeExposureKernel =
                        computeExposureKernel;
                    passData.minimumLogLuminance =
                        -16.0f;
                    passData.maximumLogLuminance =
                        16.0f;
                    passData.lowPercent =
                        Mathf.Clamp01(
                            settings.lowPercent.value /
                            100.0f);
                    passData.highPercent =
                        Mathf.Clamp(
                            settings.highPercent.value /
                                100.0f,
                            passData.lowPercent +
                                0.001f,
                            1.0f);
                    passData.middleGray =
                        settings.middleGray.value;
                    passData.compensation =
                        settings.compensation.value;
                    passData.minimumExposure =
                        Mathf.Min(
                            settings.minimumExposure.value,
                            settings.maximumExposure.value);
                    passData.maximumExposure =
                        Mathf.Max(
                            settings.minimumExposure.value,
                            settings.maximumExposure.value);
                    passData.brightenSpeed =
                        settings.brightenSpeed.value;
                    passData.darkenSpeed =
                        settings.darkenSpeed.value;
                    passData.deltaTime =
                        Mathf.Min(
                            Time.unscaledDeltaTime,
                            0.1f);

                    builder.UseTexture(
                        meteringTexture,
                        AccessFlags.Read);
                    builder.UseBuffer(
                        histogramHandle,
                        AccessFlags.ReadWrite);
                    builder.UseBuffer(
                        exposureStateHandle,
                        AccessFlags.ReadWrite);
                    builder.SetRenderFunc(
                        static (
                            HistogramPassData data,
                            ComputeGraphContext context) =>
                            ExecuteHistogramPass(
                                data,
                                context));
                }

                var destinationDescriptor =
                    renderGraph.GetTextureDesc(
                        source);
                destinationDescriptor.name =
                    "Camera Color After Automatic Exposure";
                destinationDescriptor.depthBufferBits =
                    0;
                destinationDescriptor.msaaSamples =
                    MSAASamples.None;
                destinationDescriptor.clearBuffer =
                    false;
                var destination =
                    renderGraph.CreateTexture(
                        destinationDescriptor);

                using (var builder =
                    renderGraph.AddRasterRenderPass<ApplyPassData>(
                        "Apply Automatic Exposure",
                        out var passData))
                {
                    passData.source =
                        source;
                    passData.material =
                        exposureMaterial;
                    builder.UseTexture(
                        source,
                        AccessFlags.Read);
                    builder.UseBuffer(
                        exposureStateHandle,
                        AccessFlags.Read);
                    builder.SetRenderAttachment(
                        destination,
                        0,
                        AccessFlags.Write);
                    builder.SetRenderFunc(
                        static (
                            ApplyPassData data,
                            RasterGraphContext context) =>
                            ExecuteApplyPass(
                                data,
                                context));
                }

                resourceData.cameraColor =
                    destination;
            }

            private static void ExecuteHistogramPass(
                HistogramPassData data,
                ComputeGraphContext context)
            {
                var commandBuffer =
                    context.cmd;

                commandBuffer.SetComputeBufferParam(
                    data.computeShader,
                    data.clearHistogramKernel,
                    HistogramId,
                    data.histogram);
                commandBuffer.DispatchCompute(
                    data.computeShader,
                    data.clearHistogramKernel,
                    1,
                    1,
                    1);

                commandBuffer.SetComputeTextureParam(
                    data.computeShader,
                    data.buildHistogramKernel,
                    MeteringTextureId,
                    data.meteringTexture);
                commandBuffer.SetComputeBufferParam(
                    data.computeShader,
                    data.buildHistogramKernel,
                    HistogramId,
                    data.histogram);
                commandBuffer.SetComputeIntParam(
                    data.computeShader,
                    MeteringWidthId,
                    MeteringWidth);
                commandBuffer.SetComputeIntParam(
                    data.computeShader,
                    MeteringHeightId,
                    MeteringHeight);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    HistogramMinimumLogId,
                    data.minimumLogLuminance);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    HistogramMaximumLogId,
                    data.maximumLogLuminance);
                commandBuffer.DispatchCompute(
                    data.computeShader,
                    data.buildHistogramKernel,
                    Mathf.CeilToInt(
                        MeteringWidth /
                        8.0f),
                    Mathf.CeilToInt(
                        MeteringHeight /
                        8.0f),
                    1);

                commandBuffer.SetComputeBufferParam(
                    data.computeShader,
                    data.computeExposureKernel,
                    HistogramId,
                    data.histogram);
                commandBuffer.SetComputeBufferParam(
                    data.computeShader,
                    data.computeExposureKernel,
                    ExposureStateId,
                    data.exposureState);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    HistogramMinimumLogId,
                    data.minimumLogLuminance);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    HistogramMaximumLogId,
                    data.maximumLogLuminance);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    LowPercentId,
                    data.lowPercent);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    HighPercentId,
                    data.highPercent);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    MiddleGrayId,
                    data.middleGray);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    CompensationId,
                    data.compensation);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    MinimumExposureId,
                    data.minimumExposure);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    MaximumExposureId,
                    data.maximumExposure);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    BrightenSpeedId,
                    data.brightenSpeed);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    DarkenSpeedId,
                    data.darkenSpeed);
                commandBuffer.SetComputeFloatParam(
                    data.computeShader,
                    DeltaTimeId,
                    data.deltaTime);
                commandBuffer.DispatchCompute(
                    data.computeShader,
                    data.computeExposureKernel,
                    1,
                    1,
                    1);
            }

            private static void ExecuteApplyPass(
                ApplyPassData data,
                RasterGraphContext context)
            {
                Blitter.BlitTexture(
                    context.cmd,
                    data.source,
                    new Vector4(
                        1.0f,
                        1.0f,
                        0.0f,
                        0.0f),
                    data.material,
                    1);
            }

            private sealed class HistogramPassData
            {
                public ComputeShader computeShader;
                public TextureHandle meteringTexture;
                public BufferHandle histogram;
                public BufferHandle exposureState;
                public int clearHistogramKernel;
                public int buildHistogramKernel;
                public int computeExposureKernel;
                public float minimumLogLuminance;
                public float maximumLogLuminance;
                public float lowPercent;
                public float highPercent;
                public float middleGray;
                public float compensation;
                public float minimumExposure;
                public float maximumExposure;
                public float brightenSpeed;
                public float darkenSpeed;
                public float deltaTime;
            }

            private sealed class ApplyPassData
            {
                public TextureHandle source;
                public Material material;
            }
        }
    }
}
