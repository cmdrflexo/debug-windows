/*
 * Supplies one packaged adaptive surface with a bounded, local MapMagic patch queue during the Milestone 3 transition.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Core;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(285)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfacePatchGenerator :
        MonoBehaviour
    {
        private const int MaximumSurfaceLayerCount =
            4;

        private sealed class PendingRequest
        {
            public CubeSpherePatchAddress Address;
            public double Priority;
            public long Sequence;
        }

        private sealed class GenerationResult
        {
            public int Version;
            public CubeSpherePatchAddress Address;
            public int Resolution;
            public float[] ElevationsMeters;
            public int SurfaceLayerCount;
            public float[] SurfaceControlWeights;
            public TerrainLayer[] TerrainLayers;
            public double Milliseconds;
            public string Error;
        }

        [Header("Runtime Ownership")]
        [SerializeField]
        private CelestialSurfaceRuntime surfaceRuntime;

        [SerializeField]
        [Tooltip("Temporary Milestone 3 source. When empty, a compatible source is found on the existing MapMagic root pool.")]
        private CubeSphereMapMagicRootPool generationRootPool;

        [Header("Generation")]
        [SerializeField]
        [Range(0, 16)]
        private int margins = 2;

        [SerializeField]
        [Min(64)]
        private int maximumCachedPatchCount =
            4096;

        [Header("Runtime")]
        [SerializeField]
        private bool initialized;

        [SerializeField]
        private bool sourceReady;

        [SerializeField]
        private bool isGenerating;

        [SerializeField]
        private CubeSpherePatchAddress activeAddress;

        [SerializeField]
        private int queuedPatchCount;

        [SerializeField]
        private int cachedPatchCount;

        [SerializeField]
        private int failedPatchCount;

        [SerializeField]
        private int completedPatchCount;

        [SerializeField]
        private int terrainLayerCount;

        [SerializeField]
        private double lastGenerationMilliseconds;

        [SerializeField]
        private string generationSourceName;

        [SerializeField]
        private string lastError;

        private readonly Dictionary<
            CubeSpherePatchAddress,
            PendingRequest> pendingRequests =
                new Dictionary<
                    CubeSpherePatchAddress,
                    PendingRequest>();

        private readonly Dictionary<
            CubeSpherePatchAddress,
            CelestialSurfacePatchData> cachedPatches =
                new Dictionary<
                    CubeSpherePatchAddress,
                    CelestialSurfacePatchData>();

        private readonly HashSet<
            CubeSpherePatchAddress> failedPatches =
                new HashSet<
                    CubeSpherePatchAddress>();

        private TerrainLayer[] terrainLayers =
            Array.Empty<TerrainLayer>();
        private MapMagicObject generationSource;
        private Task<GenerationResult> generationTask;
        private StopToken generationStop;
        private long requestSequence;
        private int generationVersion;
        private bool hasResolvedTextureLayout;
        private float nextSourceSearchTime;

        public CelestialSurfaceRuntime SurfaceRuntime =>
            surfaceRuntime;

        public bool Initialized =>
            initialized;

        public bool SourceReady =>
            sourceReady;

        public bool IsGenerating =>
            isGenerating;

        public CubeSpherePatchAddress ActiveAddress =>
            activeAddress;

        public string ActiveRequest =>
            isGenerating &&
            activeAddress.IsValid
                ? activeAddress.ToString()
                : "None";

        public int QueuedPatchCount =>
            queuedPatchCount;

        public int CachedPatchCount =>
            cachedPatchCount;

        public int FailedPatchCount =>
            failedPatchCount;

        public int CompletedPatchCount =>
            completedPatchCount;

        public int TerrainLayerCount =>
            terrainLayerCount;

        public double LastGenerationMilliseconds =>
            lastGenerationMilliseconds;

        public string GenerationSourceName =>
            generationSourceName;

        public string LastError =>
            lastError;

        public bool Initialize(
            CelestialSurfaceRuntime newSurfaceRuntime,
            int generationMargins)
        {
            CancelGeneration();
            ClearRuntimeData();

            surfaceRuntime =
                newSurfaceRuntime;
            margins =
                Mathf.Clamp(
                    generationMargins,
                    0,
                    16);
            initialized =
                surfaceRuntime != null &&
                surfaceRuntime.FoundationReady;

            if (!initialized)
            {
                lastError =
                    "The patch generator requires an initialized celestial surface runtime.";
                return false;
            }

            lastError = string.Empty;
            ResolveGenerationSource(
                true);
            return true;
        }

        public bool RequestPatch(
            CubeSpherePatchAddress address,
            double priority)
        {
            if (!initialized ||
                !address.IsValid ||
                address.Level <
                    surfaceRuntime.MinimumLevel ||
                address.Level >
                    surfaceRuntime.MaximumLevel ||
                !IsFinite(priority))
            {
                return false;
            }

            if (cachedPatches.ContainsKey(
                    address))
            {
                return true;
            }

            if (failedPatches.Contains(
                    address))
            {
                return false;
            }

            if (isGenerating &&
                activeAddress == address)
            {
                return true;
            }

            if (pendingRequests.TryGetValue(
                    address,
                    out var pending))
            {
                pending.Priority =
                    Math.Max(
                        pending.Priority,
                        priority);
            }
            else
            {
                pendingRequests.Add(
                    address,
                    new PendingRequest
                    {
                        Address =
                            address,
                        Priority =
                            priority,
                        Sequence =
                            requestSequence++
                    });
            }

            queuedPatchCount =
                pendingRequests.Count;
            return true;
        }

        public CelestialSurfacePatchGenerationState GetPatchState(
            CubeSpherePatchAddress address)
        {
            if (cachedPatches.ContainsKey(
                    address))
            {
                return CelestialSurfacePatchGenerationState.Ready;
            }

            if (failedPatches.Contains(
                    address))
            {
                return CelestialSurfacePatchGenerationState.Failed;
            }

            if (isGenerating &&
                activeAddress == address)
            {
                return CelestialSurfacePatchGenerationState.Generating;
            }

            return pendingRequests.ContainsKey(
                    address)
                ? CelestialSurfacePatchGenerationState.Queued
                : CelestialSurfacePatchGenerationState.None;
        }

        public bool TryGetPatchData(
            CubeSpherePatchAddress address,
            out CelestialSurfacePatchData patchData)
        {
            return cachedPatches.TryGetValue(
                address,
                out patchData);
        }

        public TerrainLayer GetTerrainLayer(
            int index)
        {
            if (index < 0 ||
                index >= terrainLayers.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }

            return terrainLayers[index];
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            CompleteCurrentGeneration();

            if (generationTask != null ||
                pendingRequests.Count == 0)
            {
                return;
            }

            if (!ResolveGenerationSource(
                    false))
            {
                return;
            }

            StartNextGeneration();
        }

        private bool ResolveGenerationSource(
            bool force)
        {
            if (generationSource != null &&
                generationSource.graph ==
                    surfaceRuntime.SurfaceDefinition.Graph)
            {
                sourceReady = true;
                generationSourceName =
                    generationSource.name;
                return true;
            }

            sourceReady = false;
            generationSource = null;
            generationSourceName = string.Empty;

            if (!force &&
                Time.unscaledTime <
                    nextSourceSearchTime)
            {
                return false;
            }

            nextSourceSearchTime =
                Time.unscaledTime +
                1.0f;

            if (generationRootPool != null &&
                SourceMatchesSurface(
                    generationRootPool.GenerationSource))
            {
                generationSource =
                    generationRootPool.GenerationSource;
            }
            else
            {
                var rootPools =
                    FindObjectsByType<
                        CubeSphereMapMagicRootPool>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None);

                for (var index = 0;
                    index < rootPools.Length;
                    index++)
                {
                    var candidate =
                        rootPools[index];

                    if (candidate != null &&
                        SourceMatchesSurface(
                            candidate.GenerationSource))
                    {
                        generationRootPool =
                            candidate;
                        generationSource =
                            candidate.GenerationSource;
                        break;
                    }
                }
            }

            sourceReady =
                generationSource != null;

            if (sourceReady)
            {
                generationSourceName =
                    generationSource.name;
                lastError = string.Empty;
            }
            else
            {
                lastError =
                    "Waiting for an existing MapMagic generation source using this body's surface graph.";
            }

            return sourceReady;
        }

        private bool SourceMatchesSurface(
            MapMagicObject source)
        {
            return
                source != null &&
                source.graph != null &&
                surfaceRuntime != null &&
                surfaceRuntime.SurfaceDefinition != null &&
                source.graph ==
                    surfaceRuntime.SurfaceDefinition.Graph;
        }

        private void StartNextGeneration()
        {
            var pending =
                ResolveHighestPriorityRequest();

            if (pending == null)
            {
                return;
            }

            pendingRequests.Remove(
                pending.Address);
            queuedPatchCount =
                pendingRequests.Count;

            if (cachedPatches.Count >=
                Mathf.Max(
                    64,
                    maximumCachedPatchCount))
            {
                FailPatch(
                    pending.Address,
                    "The temporary Milestone 3 patch cache reached its configured limit.");
                return;
            }

            if (!surfaceRuntime.TryCreateMapMagicRequest(
                    pending.Address,
                    out var request,
                    out var requestError))
            {
                FailPatch(
                    pending.Address,
                    requestError);
                return;
            }

            var graph =
                surfaceRuntime.SurfaceDefinition.Graph;
            var resolvedResolution =
                request.Resolution;
            var area =
                new Area(
                    new Vector2D(
                        (float)request.MapWorldOriginXMeters,
                        (float)request.MapWorldOriginZMeters),
                    new Vector2D(
                        (float)request.MapWorldSizeXMeters,
                        (float)request.MapWorldSizeZMeters),
                    resolvedResolution,
                    margins);
            var data =
                new RoundMapMagicSphericalTileData
                {
                    Face =
                        pending.Address.Face,
                    PlanetRadiusMeters =
                        request.PlanetRadiusMeters,
                    SurfaceSeed =
                        request.SurfaceSeed,
                    MapWorldOriginXMeters =
                        request.MapWorldOriginXMeters,
                    MapWorldOriginZMeters =
                        request.MapWorldOriginZMeters,
                    MapWorldSizeXMeters =
                        request.MapWorldSizeXMeters,
                    MapWorldSizeZMeters =
                        request.MapWorldSizeZMeters,
                    area =
                        area,
                    globals =
                        generationSource.globals,
                    random =
                        graph.random,
                    isPreview =
                        false,
                    isDraft =
                        true
                };

            try
            {
                graph.Prepare(
                    data,
                    null);
            }
            catch (Exception exception)
            {
                data.Clear(
                    clearApply: true,
                    inSubs: true);
                FailPatch(
                    pending.Address,
                    exception.GetBaseException().Message);
                return;
            }

            activeAddress =
                pending.Address;
            isGenerating = true;
            generationStop =
                new StopToken();
            var requestVersion =
                generationVersion;
            var stop =
                generationStop;

            generationTask =
                Task.Run(
                    () => GeneratePatch(
                        graph,
                        data,
                        stop,
                        request,
                        requestVersion));
        }

        private PendingRequest ResolveHighestPriorityRequest()
        {
            PendingRequest best = null;

            foreach (var candidate in
                pendingRequests.Values)
            {
                if (best == null ||
                    candidate.Priority >
                        best.Priority ||
                    candidate.Priority ==
                        best.Priority &&
                    candidate.Sequence <
                        best.Sequence)
                {
                    best = candidate;
                }
            }

            return best;
        }

        private static GenerationResult GeneratePatch(
            MapMagic.Nodes.Graph graph,
            RoundMapMagicSphericalTileData data,
            StopToken stop,
            RoundMapMagicSurfacePatchRequest request,
            int requestVersion)
        {
            var result =
                new GenerationResult
                {
                    Version =
                        requestVersion,
                    Address =
                        request.Address,
                    Resolution =
                        request.Resolution
                };
            var stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            try
            {
                try
                {
                    graph.Generate(
                        data,
                        stop);
                    graph.Finalize(
                        data,
                        stop);

                    if (stop.stop)
                    {
                        throw new InvalidOperationException(
                            "MapMagic stopped the adaptive patch request.");
                    }

                    if (data.heights == null)
                    {
                        throw new InvalidOperationException(
                            "The MapMagic graph did not produce a finalized Height output for the adaptive patch request.");
                    }

                    result.ElevationsMeters =
                        CopyElevations(
                            data,
                            request);
                    CaptureSurfaceControls(
                        result,
                        data.ApplyOfType<
                            TexturesOutput200.ApplyData>(),
                        request);
                }
                finally
                {
                    data.Clear(
                        clearApply: true,
                        inSubs: true);
                }
            }
            catch (Exception exception)
            {
                result.Error =
                    exception.GetBaseException().Message;
            }
            finally
            {
                stopwatch.Stop();
                result.Milliseconds =
                    stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        private static float[] CopyElevations(
            RoundMapMagicSphericalTileData data,
            RoundMapMagicSurfacePatchRequest request)
        {
            var resolution =
                request.Resolution;
            var elevations =
                new float[
                    resolution *
                    resolution];
            var heightScaleMeters =
                data.heights.worldSize.y;

            for (var sampleY = 0;
                sampleY < resolution;
                sampleY++)
            {
                var mapPixelZ =
                    request.PatchSampleYToMapPixelZ(
                        sampleY);
                var pixelZ =
                    data.area.active.rect.offset.z +
                    mapPixelZ;

                for (var sampleX = 0;
                    sampleX < resolution;
                    sampleX++)
                {
                    var pixelX =
                        data.area.active.rect.offset.x +
                        sampleX;
                    var normalizedHeight =
                        data.heights[
                            pixelX,
                            pixelZ];

                    elevations[
                        sampleY *
                            resolution +
                        sampleX] =
                            normalizedHeight *
                                heightScaleMeters +
                            (float)request
                                .ElevationOffsetMeters;
                }
            }

            return elevations;
        }

        private static void CaptureSurfaceControls(
            GenerationResult result,
            TexturesOutput200.ApplyData textureData,
            RoundMapMagicSurfacePatchRequest request)
        {
            if (textureData == null ||
                textureData.splats == null ||
                textureData.prototypes == null ||
                textureData.prototypes.Length == 0)
            {
                return;
            }

            var resolution =
                request.Resolution;
            var layerCount =
                textureData.prototypes.Length;

            if (layerCount >
                MaximumSurfaceLayerCount)
            {
                throw new InvalidOperationException(
                    $"The adaptive surface currently supports up to {MaximumSurfaceLayerCount} terrain layers, but the graph produced {layerCount}.");
            }

            if (textureData.splats.GetLength(0) !=
                    resolution ||
                textureData.splats.GetLength(1) !=
                    resolution ||
                textureData.splats.GetLength(2) !=
                    layerCount)
            {
                throw new InvalidOperationException(
                    "The MapMagic texture-control dimensions do not match the adaptive patch request.");
            }

            var weights =
                new float[
                    resolution *
                    resolution *
                    layerCount];

            for (var sampleY = 0;
                sampleY < resolution;
                sampleY++)
            {
                var mapPixelZ =
                    request.PatchSampleYToMapPixelZ(
                        sampleY);

                for (var sampleX = 0;
                    sampleX < resolution;
                    sampleX++)
                {
                    for (var layer = 0;
                        layer < layerCount;
                        layer++)
                    {
                        weights[
                            (sampleY *
                                resolution +
                                sampleX) *
                                layerCount +
                            layer] =
                                textureData.splats[
                                    mapPixelZ,
                                    sampleX,
                                    layer];
                    }
                }
            }

            result.SurfaceLayerCount =
                layerCount;
            result.SurfaceControlWeights =
                weights;
            result.TerrainLayers =
                textureData.prototypes;
        }

        private void CompleteCurrentGeneration()
        {
            if (generationTask == null ||
                !generationTask.IsCompleted)
            {
                return;
            }

            GenerationResult result;

            try
            {
                result =
                    generationTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                result =
                    new GenerationResult
                    {
                        Version =
                            generationVersion,
                        Address =
                            activeAddress,
                        Error =
                            exception.GetBaseException().Message
                    };
            }

            generationTask = null;
            generationStop = null;
            isGenerating = false;
            activeAddress = default;

            if (result.Version !=
                generationVersion)
            {
                return;
            }

            if (!string.IsNullOrEmpty(
                    result.Error))
            {
                FailPatch(
                    result.Address,
                    result.Error);
                return;
            }

            if (!TryAcceptTextureLayout(
                    result,
                    out var textureError))
            {
                FailPatch(
                    result.Address,
                    textureError);
                return;
            }

            try
            {
                cachedPatches[result.Address] =
                    new CelestialSurfacePatchData(
                        surfaceRuntime.CacheKey,
                        result.Address,
                        result.Resolution,
                        result.ElevationsMeters,
                        result.SurfaceLayerCount,
                        result.SurfaceControlWeights);
            }
            catch (Exception exception)
            {
                FailPatch(
                    result.Address,
                    exception.GetBaseException().Message);
                return;
            }

            completedPatchCount++;
            cachedPatchCount =
                cachedPatches.Count;
            lastGenerationMilliseconds =
                result.Milliseconds;
            lastError = string.Empty;
        }

        private bool TryAcceptTextureLayout(
            GenerationResult result,
            out string error)
        {
            var hasTextureData =
                result.SurfaceLayerCount > 0 &&
                result.SurfaceControlWeights != null &&
                result.TerrainLayers != null &&
                result.TerrainLayers.Length ==
                    result.SurfaceLayerCount;

            if (!hasResolvedTextureLayout)
            {
                hasResolvedTextureLayout = true;
                terrainLayers =
                    hasTextureData
                        ? (TerrainLayer[])result
                            .TerrainLayers.Clone()
                        : Array.Empty<TerrainLayer>();
                terrainLayerCount =
                    terrainLayers.Length;
                error = string.Empty;
                return true;
            }

            if (hasTextureData !=
                (terrainLayerCount > 0))
            {
                error =
                    "The MapMagic graph produced inconsistent texture outputs between adaptive patches.";
                return false;
            }

            if (hasTextureData)
            {
                if (result.TerrainLayers.Length !=
                    terrainLayers.Length)
                {
                    error =
                        "The MapMagic graph produced inconsistent terrain-layer counts between adaptive patches.";
                    return false;
                }

                for (var index = 0;
                    index < terrainLayers.Length;
                    index++)
                {
                    if (terrainLayers[index] !=
                        result.TerrainLayers[index])
                    {
                        error =
                            "The MapMagic graph produced different terrain-layer prototypes between adaptive patches.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private void FailPatch(
            CubeSpherePatchAddress address,
            string error)
        {
            if (address.IsValid)
            {
                failedPatches.Add(
                    address);
            }

            failedPatchCount =
                failedPatches.Count;
            lastError =
                $"{address}: {error}";
        }

        private void CancelGeneration()
        {
            generationVersion++;

            if (generationStop != null)
            {
                generationStop.stop = true;
            }

            generationTask = null;
            generationStop = null;
            isGenerating = false;
            activeAddress = default;
        }

        private void ClearRuntimeData()
        {
            pendingRequests.Clear();
            cachedPatches.Clear();
            failedPatches.Clear();
            terrainLayers =
                Array.Empty<TerrainLayer>();
            generationSource = null;
            sourceReady = false;
            initialized = false;
            queuedPatchCount = 0;
            cachedPatchCount = 0;
            failedPatchCount = 0;
            completedPatchCount = 0;
            terrainLayerCount = 0;
            lastGenerationMilliseconds = 0.0;
            generationSourceName = string.Empty;
            hasResolvedTextureLayout = false;
            lastError = string.Empty;
        }

        private void OnDisable()
        {
            CancelGeneration();
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
