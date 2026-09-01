/*
 * Incrementally generates and caches low-resolution height samples directly from the active MapMagic graph without creating Unity Terrains.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Core;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicVirtualHeightSampler :
        MonoBehaviour
    {
        private struct SampleKey :
            IEquatable<SampleKey>
        {
            public CubeSphereFace Face;
            public int TileX;
            public int TileZ;

            public bool Equals(SampleKey other)
            {
                return
                    Face == other.Face &&
                    TileX == other.TileX &&
                    TileZ == other.TileZ;
            }

            public override bool Equals(object value)
            {
                return
                    value is SampleKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = (int)Face;
                    hash = (hash * 397) ^ TileX;
                    hash = (hash * 397) ^ TileZ;
                    return hash;
                }
            }
        }

        private sealed class GenerationResult
        {
            public int Version;
            public SampleKey Key;
            public RoundMapMagicVirtualHeightSample Sample;
            public string Error;
            public double Milliseconds;
        }

        [Header("Configuration")]
        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private CubeSphereMapMagicRootPool rootPool;

        [SerializeField]
        private bool generateAutomatically = true;

        [SerializeField]
        [Range(0, 4)]
        [Tooltip("Temporary streamed radius for validating the incremental mid-detail cache. One creates a 3 by 3 virtual-tile grid.")]
        private int streamedTileRadius = 1;

        [Header("Runtime Request")]
        [SerializeField]
        private bool isGenerating;

        [SerializeField]
        private int generationRequestCount;

        [SerializeField]
        private int expectedSampleCount;

        [SerializeField]
        private int cachedSampleCount;

        [SerializeField]
        private int queuedSampleCount;

        [SerializeField]
        private CubeSphereFace sampleFace;

        [SerializeField]
        private int sourceTileX;

        [SerializeField]
        private int sourceTileZ;

        [SerializeField]
        private int virtualTileX;

        [SerializeField]
        private int virtualTileZ;

        [SerializeField]
        private double virtualTileSizeMeters;

        [SerializeField]
        private int resolvedMidResolution;

        [Header("Runtime Sample")]
        [SerializeField]
        private bool hasSample;

        [SerializeField]
        private int sampleVertexCount;

        [SerializeField]
        private float minimumHeightMeters;

        [SerializeField]
        private float maximumHeightMeters;

        [SerializeField]
        private float centerHeightMeters;

        [SerializeField]
        private double generationMilliseconds;

        [SerializeField]
        private string lastError;

        private readonly Dictionary<SampleKey, RoundMapMagicVirtualHeightSample>
            samples =
                new Dictionary<SampleKey, RoundMapMagicVirtualHeightSample>();
        private readonly HashSet<SampleKey>
            desiredKeys =
                new HashSet<SampleKey>();
        private readonly Queue<SampleKey>
            generationQueue =
                new Queue<SampleKey>();
        private readonly List<SampleKey>
            removalBuffer =
                new List<SampleKey>();

        private RoundMapMagicVirtualHeightSample currentSample;
        private Task<GenerationResult> generationTask;
        private StopToken generationStop;
        private MapMagicObject generationSource;
        private SampleKey centerKey;
        private bool hasCenterKey;
        private int generationVersion;
        private int desiredRadius = -1;
        private int desiredResolution;
        private double desiredTileSizeX;
        private double desiredTileSizeZ;
        private int desiredMargins;

        public bool HasSample =>
            hasSample;

        public RoundMapMagicVirtualHeightSample CurrentSample =>
            currentSample;

        public int CachedSampleCount =>
            samples.Count;

        public int ExpectedSampleCount =>
            expectedSampleCount;

        private void Reset()
        {
            surfaceSession =
                GetComponent<RoundMapMagicSurfaceSession>();
            rootPool =
                GetComponent<CubeSphereMapMagicRootPool>();
        }

        private void Start()
        {
            if (surfaceSession == null)
            {
                Debug.LogError(
                    "The virtual MapMagic height sampler requires the shared Round MapMagic surface session.",
                    this);
            }

            if (rootPool == null)
            {
                Debug.LogError(
                    "The virtual MapMagic height sampler requires the shared MapMagic root pool.",
                    this);
            }
        }

        private void LateUpdate()
        {
            CompleteGeneration();

            if (!generateAutomatically ||
                surfaceSession == null ||
                rootPool == null ||
                !surfaceSession.HasActiveSession)
            {
                ClearStreamingState();
                return;
            }

            var qualityProfile =
                surfaceSession.ConfiguredQualityProfile;
            var primaryRoot =
                rootPool.PrimaryAssignedRoot;

            if (qualityProfile == null ||
                !qualityProfile.HasValidSettings ||
                primaryRoot == null ||
                !primaryRoot.HasMapMagicCoordinate ||
                primaryRoot.MapMagicObject == null)
            {
                ClearStreamingState();
                return;
            }

            var mapMagicObject =
                primaryRoot.MapMagicObject;
            var tileSizeMultiplier =
                qualityProfile.MidTileSizeMultiplier;
            var nextCenterKey =
                new SampleKey
                {
                    Face =
                        primaryRoot.ActiveFace,
                    TileX =
                        FloorDivide(
                            primaryRoot.MapMagicTileX,
                            tileSizeMultiplier),
                    TileZ =
                        FloorDivide(
                            primaryRoot.MapMagicTileZ,
                            tileSizeMultiplier)
                };
            var nextResolution =
                qualityProfile.MidMeshResolution;
            var nextTileSizeX =
                mapMagicObject.tileSize.x *
                tileSizeMultiplier;
            var nextTileSizeZ =
                mapMagicObject.tileSize.z *
                tileSizeMultiplier;
            var nextMargins =
                Math.Max(
                    0,
                    mapMagicObject.draftMargins);
            var nextRadius =
                Mathf.Clamp(
                    streamedTileRadius,
                    0,
                    4);

            if (DesiredGridChanged(
                    mapMagicObject,
                    nextCenterKey,
                    nextRadius,
                    nextResolution,
                    nextTileSizeX,
                    nextTileSizeZ,
                    nextMargins))
            {
                RebuildDesiredGrid(
                    mapMagicObject,
                    nextCenterKey,
                    nextRadius,
                    nextResolution,
                    nextTileSizeX,
                    nextTileSizeZ,
                    nextMargins);
            }

            sampleFace =
                nextCenterKey.Face;
            sourceTileX =
                primaryRoot.MapMagicTileX;
            sourceTileZ =
                primaryRoot.MapMagicTileZ;
            virtualTileX =
                nextCenterKey.TileX;
            virtualTileZ =
                nextCenterKey.TileZ;
            virtualTileSizeMeters =
                nextTileSizeX;
            resolvedMidResolution =
                nextResolution;

            RefreshSampleDiagnostics();
            StartNextGeneration(
                mapMagicObject,
                nextResolution,
                nextTileSizeX,
                nextTileSizeZ,
                nextMargins);
        }

        public void CopyCurrentSamplesTo(
            List<RoundMapMagicVirtualHeightSample> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(
                    nameof(destination));
            }

            destination.Clear();

            foreach (var sample in
                samples.Values)
            {
                destination.Add(sample);
            }
        }

        public bool TryGetCurrentSample(
            CubeSphereFace face,
            int tileX,
            int tileZ,
            out RoundMapMagicVirtualHeightSample sample)
        {
            return samples.TryGetValue(
                new SampleKey
                {
                    Face = face,
                    TileX = tileX,
                    TileZ = tileZ
                },
                out sample);
        }

        private bool DesiredGridChanged(
            MapMagicObject mapMagicObject,
            SampleKey nextCenterKey,
            int nextRadius,
            int nextResolution,
            double nextTileSizeX,
            double nextTileSizeZ,
            int nextMargins)
        {
            return
                !hasCenterKey ||
                !centerKey.Equals(nextCenterKey) ||
                generationSource != mapMagicObject ||
                desiredRadius != nextRadius ||
                desiredResolution != nextResolution ||
                desiredTileSizeX != nextTileSizeX ||
                desiredTileSizeZ != nextTileSizeZ ||
                desiredMargins != nextMargins;
        }

        private void RebuildDesiredGrid(
            MapMagicObject mapMagicObject,
            SampleKey nextCenterKey,
            int nextRadius,
            int nextResolution,
            double nextTileSizeX,
            double nextTileSizeZ,
            int nextMargins)
        {
            var sourceSettingsChanged =
                generationSource != mapMagicObject ||
                desiredResolution != nextResolution ||
                desiredTileSizeX != nextTileSizeX ||
                desiredTileSizeZ != nextTileSizeZ ||
                desiredMargins != nextMargins;

            generationVersion++;

            if (generationStop != null)
            {
                generationStop.stop = true;
            }

            generationSource =
                mapMagicObject;
            centerKey =
                nextCenterKey;
            hasCenterKey = true;
            desiredRadius =
                nextRadius;
            desiredResolution =
                nextResolution;
            desiredTileSizeX =
                nextTileSizeX;
            desiredTileSizeZ =
                nextTileSizeZ;
            desiredMargins =
                nextMargins;
            desiredKeys.Clear();
            generationQueue.Clear();

            for (var offsetZ = -nextRadius;
                offsetZ <= nextRadius;
                offsetZ++)
            {
                for (var offsetX = -nextRadius;
                    offsetX <= nextRadius;
                    offsetX++)
                {
                    desiredKeys.Add(
                        new SampleKey
                        {
                            Face =
                                nextCenterKey.Face,
                            TileX =
                                nextCenterKey.TileX +
                                offsetX,
                            TileZ =
                                nextCenterKey.TileZ +
                                offsetZ
                        });
                }
            }

            if (sourceSettingsChanged)
            {
                samples.Clear();
            }
            else
            {
                removalBuffer.Clear();

                foreach (var entry in
                    samples)
                {
                    if (!desiredKeys.Contains(
                            entry.Key))
                    {
                        removalBuffer.Add(
                            entry.Key);
                    }
                }

                for (var index = 0;
                    index < removalBuffer.Count;
                    index++)
                {
                    samples.Remove(
                        removalBuffer[index]);
                }
            }

            EnqueueMissingSamplesByRing(
                nextCenterKey,
                nextRadius);
            expectedSampleCount =
                desiredKeys.Count;
            cachedSampleCount =
                samples.Count;
            queuedSampleCount =
                generationQueue.Count;
        }

        private void EnqueueMissingSamplesByRing(
            SampleKey center,
            int radius)
        {
            for (var ring = 0;
                ring <= radius;
                ring++)
            {
                for (var offsetZ = -ring;
                    offsetZ <= ring;
                    offsetZ++)
                {
                    for (var offsetX = -ring;
                        offsetX <= ring;
                        offsetX++)
                    {
                        if (Math.Max(
                                Math.Abs(offsetX),
                                Math.Abs(offsetZ)) !=
                            ring)
                        {
                            continue;
                        }

                        var key =
                            new SampleKey
                            {
                                Face =
                                    center.Face,
                                TileX =
                                    center.TileX +
                                    offsetX,
                                TileZ =
                                    center.TileZ +
                                    offsetZ
                            };

                        if (!samples.ContainsKey(key))
                        {
                            generationQueue.Enqueue(key);
                        }
                    }
                }
            }
        }

        private void StartNextGeneration(
            MapMagicObject mapMagicObject,
            int resolution,
            double tileSizeX,
            double tileSizeZ,
            int margins)
        {
            if (generationTask != null)
            {
                return;
            }

            SampleKey key;

            do
            {
                if (generationQueue.Count == 0)
                {
                    queuedSampleCount = 0;
                    return;
                }

                key =
                    generationQueue.Dequeue();
            }
            while (!desiredKeys.Contains(key) ||
                samples.ContainsKey(key));

            var graph =
                mapMagicObject.graph;

            if (graph == null)
            {
                lastError =
                    "The active MapMagic root has no graph.";
                generationQueue.Clear();
                queuedSampleCount = 0;
                return;
            }

            var data =
                new TileData
                {
                    area =
                        new Area(
                            new Coord(
                                key.TileX,
                                key.TileZ),
                            resolution,
                            margins,
                            new Vector2D(
                                tileSizeX,
                                tileSizeZ)),
                    globals =
                        mapMagicObject.globals,
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
                lastError =
                    exception.GetBaseException().Message;
                Debug.LogError(
                    $"Virtual MapMagic height preparation failed: {lastError}",
                    this);
                return;
            }

            var requestVersion =
                generationVersion;
            var stop =
                new StopToken();

            isGenerating = true;
            generationRequestCount++;
            queuedSampleCount =
                generationQueue.Count;
            generationStop =
                stop;
            generationTask =
                Task.Run(
                    () => GenerateSample(
                        graph,
                        data,
                        stop,
                        key,
                        resolution,
                        requestVersion));
        }

        private static GenerationResult GenerateSample(
            MapMagic.Nodes.Graph graph,
            TileData data,
            StopToken stop,
            SampleKey key,
            int resolution,
            int requestVersion)
        {
            var result =
                new GenerationResult
                {
                    Version =
                        requestVersion,
                    Key =
                        key
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
                            "MapMagic stopped the virtual height request.");
                    }

                    if (data.heights == null)
                    {
                        throw new InvalidOperationException(
                            "The MapMagic graph did not produce a finalized Height output for the virtual request.");
                    }

                    result.Sample =
                        CreateSample(
                            key,
                            data.area,
                            data.heights,
                            resolution);
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

        private void CompleteGeneration()
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
                        Error =
                            exception.GetBaseException().Message
                    };
            }

            generationTask = null;
            generationStop = null;
            isGenerating = false;
            generationMilliseconds =
                result.Milliseconds;

            if (result.Version != generationVersion ||
                !desiredKeys.Contains(result.Key))
            {
                RefreshSampleDiagnostics();
                return;
            }

            if (!string.IsNullOrEmpty(
                    result.Error))
            {
                lastError =
                    result.Error;
                Debug.LogError(
                    $"Virtual MapMagic height generation failed: {lastError}",
                    this);
                RefreshSampleDiagnostics();
                return;
            }

            samples[result.Key] =
                result.Sample;
            lastError =
                string.Empty;
            RefreshSampleDiagnostics();
        }

        private static RoundMapMagicVirtualHeightSample CreateSample(
            SampleKey key,
            Area area,
            MatrixWorld heightMatrix,
            int resolution)
        {
            var heightsMeters =
                new float[
                    resolution *
                    resolution];
            var worldOriginX =
                area.active.worldPos.x;
            var worldOriginZ =
                area.active.worldPos.z;
            var worldSizeX =
                area.active.worldSize.x;
            var worldSizeZ =
                area.active.worldSize.z;
            var heightScaleMeters =
                heightMatrix.worldSize.y;
            var minimumHeight =
                float.PositiveInfinity;
            var maximumHeight =
                float.NegativeInfinity;

            for (var z = 0;
                z < resolution;
                z++)
            {
                var normalizedZ =
                    z /
                    (double)(
                        resolution -
                        1);
                var worldZ =
                    worldOriginZ +
                    worldSizeZ *
                    normalizedZ;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(
                            resolution -
                            1);
                    var worldX =
                        worldOriginX +
                        worldSizeX *
                        normalizedX;
                    var heightMeters =
                        heightMatrix.GetWorldInterpolatedValue(
                            (float)worldX,
                            (float)worldZ) *
                        heightScaleMeters;
                    var index =
                        z *
                        resolution +
                        x;

                    heightsMeters[index] =
                        heightMeters;
                    minimumHeight =
                        Math.Min(
                            minimumHeight,
                            heightMeters);
                    maximumHeight =
                        Math.Max(
                            maximumHeight,
                            heightMeters);
                }
            }

            return
                new RoundMapMagicVirtualHeightSample(
                    key.Face,
                    key.TileX,
                    key.TileZ,
                    resolution,
                    worldOriginX,
                    worldOriginZ,
                    worldSizeX,
                    worldSizeZ,
                    heightsMeters,
                    minimumHeight,
                    maximumHeight);
        }

        private void RefreshSampleDiagnostics()
        {
            cachedSampleCount =
                samples.Count;
            queuedSampleCount =
                generationQueue.Count;
            currentSample = null;

            if (hasCenterKey)
            {
                samples.TryGetValue(
                    centerKey,
                    out currentSample);
            }

            hasSample =
                samples.Count > 0;
            var diagnosticSample =
                currentSample;

            if (diagnosticSample == null)
            {
                foreach (var sample in
                    samples.Values)
                {
                    diagnosticSample =
                        sample;
                    break;
                }
            }

            sampleVertexCount =
                diagnosticSample != null
                    ? diagnosticSample.VertexCount
                    : 0;
            minimumHeightMeters =
                diagnosticSample != null
                    ? diagnosticSample.MinimumHeightMeters
                    : 0.0f;
            maximumHeightMeters =
                diagnosticSample != null
                    ? diagnosticSample.MaximumHeightMeters
                    : 0.0f;
            centerHeightMeters =
                diagnosticSample != null
                    ? diagnosticSample.CenterHeightMeters
                    : 0.0f;
        }

        private void ClearStreamingState()
        {
            if (hasCenterKey ||
                desiredKeys.Count > 0 ||
                generationQueue.Count > 0 ||
                samples.Count > 0)
            {
                generationVersion++;

                if (generationStop != null)
                {
                    generationStop.stop = true;
                }
            }

            generationSource = null;
            hasCenterKey = false;
            desiredRadius = -1;
            desiredResolution = default;
            desiredTileSizeX = default;
            desiredTileSizeZ = default;
            desiredMargins = default;
            desiredKeys.Clear();
            generationQueue.Clear();
            samples.Clear();
            currentSample = null;
            expectedSampleCount = default;
            cachedSampleCount = default;
            queuedSampleCount = default;
            hasSample = false;
            sampleVertexCount = default;
            minimumHeightMeters = default;
            maximumHeightMeters = default;
            centerHeightMeters = default;

            if (generationTask == null)
            {
                isGenerating = false;
            }
        }

        private void OnDisable()
        {
            generationVersion++;

            if (generationStop != null)
            {
                generationStop.stop = true;
            }

            ClearStreamingState();
        }

        private static int FloorDivide(
            int value,
            int divisor)
        {
            return
                (int)Math.Floor(
                    value /
                    (double)divisor);
        }
    }

    public sealed class RoundMapMagicVirtualHeightSample
    {
        private readonly float[] heightsMeters;

        public CubeSphereFace Face { get; }

        public int TileX { get; }

        public int TileZ { get; }

        public int Resolution { get; }

        public double WorldOriginXMeters { get; }

        public double WorldOriginZMeters { get; }

        public double WorldSizeXMeters { get; }

        public double WorldSizeZMeters { get; }

        public int VertexCount =>
            heightsMeters.Length;

        public float MinimumHeightMeters { get; }

        public float MaximumHeightMeters { get; }

        public float CenterHeightMeters =>
            heightsMeters[
                Resolution /
                    2 *
                    Resolution +
                Resolution /
                    2];

        internal RoundMapMagicVirtualHeightSample(
            CubeSphereFace face,
            int tileX,
            int tileZ,
            int resolution,
            double worldOriginXMeters,
            double worldOriginZMeters,
            double worldSizeXMeters,
            double worldSizeZMeters,
            float[] heightsMeters,
            float minimumHeightMeters,
            float maximumHeightMeters)
        {
            Face =
                face;
            TileX =
                tileX;
            TileZ =
                tileZ;
            Resolution =
                resolution;
            WorldOriginXMeters =
                worldOriginXMeters;
            WorldOriginZMeters =
                worldOriginZMeters;
            WorldSizeXMeters =
                worldSizeXMeters;
            WorldSizeZMeters =
                worldSizeZMeters;
            this.heightsMeters =
                heightsMeters;
            MinimumHeightMeters =
                minimumHeightMeters;
            MaximumHeightMeters =
                maximumHeightMeters;
        }

        public float GetHeightMeters(
            int x,
            int z)
        {
            if (x < 0 ||
                x >= Resolution ||
                z < 0 ||
                z >= Resolution)
            {
                throw new ArgumentOutOfRangeException(
                    $"Height coordinate ({x}, {z}) is outside resolution {Resolution}.");
            }

            return
                heightsMeters[
                    z *
                    Resolution +
                    x];
        }

        public bool TryGetInterpolatedHeightMeters(
            double worldXMeters,
            double worldZMeters,
            out float heightMeters)
        {
            var percentX =
                (worldXMeters -
                    WorldOriginXMeters) /
                WorldSizeXMeters;
            var percentZ =
                (worldZMeters -
                    WorldOriginZMeters) /
                WorldSizeZMeters;

            if (percentX < 0.0 ||
                percentX > 1.0 ||
                percentZ < 0.0 ||
                percentZ > 1.0)
            {
                heightMeters = default;
                return false;
            }

            var sampleX =
                percentX *
                (Resolution -
                    1);
            var sampleZ =
                percentZ *
                (Resolution -
                    1);
            var lowerX =
                (int)Math.Floor(
                    sampleX);
            var lowerZ =
                (int)Math.Floor(
                    sampleZ);
            var upperX =
                Math.Min(
                    lowerX +
                        1,
                    Resolution -
                        1);
            var upperZ =
                Math.Min(
                    lowerZ +
                        1,
                    Resolution -
                        1);
            var blendX =
                (float)(
                    sampleX -
                    lowerX);
            var blendZ =
                (float)(
                    sampleZ -
                    lowerZ);
            var lowerHeight =
                Mathf.Lerp(
                    GetHeightMeters(
                        lowerX,
                        lowerZ),
                    GetHeightMeters(
                        upperX,
                        lowerZ),
                    blendX);
            var upperHeight =
                Mathf.Lerp(
                    GetHeightMeters(
                        lowerX,
                        upperZ),
                    GetHeightMeters(
                        upperX,
                        upperZ),
                    blendX);

            heightMeters =
                Mathf.Lerp(
                    lowerHeight,
                    upperHeight,
                    blendZ);
            return true;
        }

        public void CopyHeightsTo(
            float[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(
                    nameof(destination));
            }

            if (destination.Length <
                heightsMeters.Length)
            {
                throw new ArgumentException(
                    "The destination array is too small for this height sample.",
                    nameof(destination));
            }

            Array.Copy(
                heightsMeters,
                destination,
                heightsMeters.Length);
        }
    }
}
