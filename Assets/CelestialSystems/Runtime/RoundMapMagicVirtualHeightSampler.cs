/*
 * Incrementally generates and caches configurable-detail surface samples directly from the active MapMagic graph without creating Unity Terrains.
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
using UnityEngine.Serialization;

namespace jcan.CelestialSystems
{
    public enum RoundMapMagicVirtualSampleStream
    {
        Mid,
        Local
    }

    [DefaultExecutionOrder(250)]
    public sealed class RoundMapMagicVirtualHeightSampler :
        MonoBehaviour
    {
        private const int MaximumStreamedTileRadius = 16;

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
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private CubeSphereTerrainAddressTracker addressTracker;

        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private CubeSphereMapMagicRootPool rootPool;

        [SerializeField]
        private RoundMapMagicVirtualSampleStream sampleStream =
            RoundMapMagicVirtualSampleStream.Mid;

        [SerializeField]
        private bool generateAutomatically = true;

        [Header("Generation Priority")]
        [SerializeField]
        private Transform generationView;

        [SerializeField]
        private bool prioritizeVisibleTiles = true;

        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float viewOverscan = 0.1f;

        [SerializeField]
        [Range(2, 32)]
        private int offscreenCatchUpInterval = 8;

        [Header("Runtime Request")]
        [FormerlySerializedAs("midStreamingActive")]
        [SerializeField]
        private bool streamingActive;

        [SerializeField]
        private double anchorAltitudeMeters;

        [FormerlySerializedAs("resolvedMidActivationAltitudeMeters")]
        [SerializeField]
        private double resolvedActivationAltitudeMeters;

        [FormerlySerializedAs("resolvedMidReleaseAltitudeMeters")]
        [SerializeField]
        private double resolvedReleaseAltitudeMeters;

        [SerializeField]
        private bool isGenerating;

        [SerializeField]
        private int generationRequestCount;

        [SerializeField]
        private int expectedSampleCount;

        [SerializeField]
        private int readyVisibleSampleCount;

        [SerializeField]
        private int expectedPrefetchSampleCount;

        [SerializeField]
        private int cachedSampleCount;

        [SerializeField]
        private int queuedSampleCount;

        [SerializeField]
        private bool hasGenerationView;

        [SerializeField]
        private int viewPrioritizedRequestCount;

        [SerializeField]
        private int catchUpRequestCount;

        [SerializeField]
        private int lastSelectedViewBand;

        [SerializeField]
        private int resolvedStreamedTileRadius;

        [SerializeField]
        private double resolvedVisibleCoverageRadiusMeters;

        [SerializeField]
        private double resolvedPrefetchCoverageRadiusMeters;

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

        [FormerlySerializedAs("resolvedMidResolution")]
        [SerializeField]
        private int resolvedStreamResolution;

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
        private bool hasTextureSample;

        [SerializeField]
        private int sampleControlResolution;

        [SerializeField]
        private int sampleTerrainLayerCount;

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
        private readonly HashSet<SampleKey>
            visibleKeys =
                new HashSet<SampleKey>();
        private readonly List<SampleKey>
            generationQueue =
                new List<SampleKey>();
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
        private double desiredVisibleCoverageRadiusMeters;
        private double desiredCoverageRadiusMeters;
        private double desiredPatchCenterWorldX;
        private double desiredPatchCenterWorldZ;
        private int desiredMargins;

        public bool HasSample =>
            hasSample;

        public RoundMapMagicVirtualHeightSample CurrentSample =>
            currentSample;

        public int CachedSampleCount =>
            samples.Count;

        public int ExpectedSampleCount =>
            expectedSampleCount;

        public RoundMapMagicVirtualSampleStream SampleStream =>
            sampleStream;

        public bool StreamingActive =>
            streamingActive;

        public bool MidStreamingActive =>
            sampleStream ==
                RoundMapMagicVirtualSampleStream.Mid &&
            streamingActive;

        public bool HasCompleteCoverage =>
            expectedSampleCount > 0 &&
            readyVisibleSampleCount ==
                expectedSampleCount;

        public CelestialBodyRuntimeContext TrackedBodyContext =>
            surfaceFrame != null
                ? surfaceFrame.BodyContext
                : null;

        public double AnchorAltitudeMeters =>
            anchorAltitudeMeters;

        private void Reset()
        {
            ResolveLocalReferences();
        }

        private void Awake()
        {
            ResolveLocalReferences();
        }

        private void Start()
        {
            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "The virtual MapMagic height sampler requires a planet surface frame.",
                    this);
            }

            if (addressTracker == null)
            {
                Debug.LogError(
                    "The virtual MapMagic height sampler requires the persistent cube-sphere terrain address tracker.",
                    this);
            }

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
                surfaceFrame == null ||
                addressTracker == null ||
                surfaceSession == null ||
                rootPool == null)
            {
                streamingActive = false;
                ClearStreamingState();
                return;
            }

            var qualityProfile =
                surfaceSession.ConfiguredQualityProfile;

            if (qualityProfile == null ||
                !qualityProfile.HasValidSettings)
            {
                streamingActive = false;
                ClearStreamingState();
                return;
            }

            UpdateStreamingState(
                qualityProfile);

            if (!streamingActive ||
                !addressTracker.HasPrimaryTileAddress)
            {
                ClearStreamingState();
                return;
            }

            var mapMagicObject =
                rootPool.GenerationSource;

            if (mapMagicObject == null ||
                !CubeSphereMapMagicCoordinateDriver
                    .TryTileAddressToMapMagicCoordinate(
                        addressTracker.PrimaryTileAddress,
                        out var nextSourceTileX,
                        out var nextSourceTileZ))
            {
                ClearStreamingState();
                return;
            }

            var tileSizeMultiplier =
                sampleStream ==
                    RoundMapMagicVirtualSampleStream.Local
                    ? 1
                    : qualityProfile.MidTileSizeMultiplier;
            var sourceAddress =
                addressTracker.PrimaryTileAddress;
            var nextCenterKey =
                new SampleKey
                {
                    Face =
                        sourceAddress.Face,
                    TileX =
                        FloorDivide(
                            nextSourceTileX,
                            tileSizeMultiplier),
                    TileZ =
                        FloorDivide(
                            nextSourceTileZ,
                            tileSizeMultiplier)
                };
            var nextResolution =
                sampleStream ==
                    RoundMapMagicVirtualSampleStream.Local
                    ? qualityProfile.LocalMeshResolution
                    : qualityProfile.MidMeshResolution;
            var nextTileSizeX =
                mapMagicObject.tileSize.x *
                tileSizeMultiplier;
            var nextTileSizeZ =
                mapMagicObject.tileSize.z *
                tileSizeMultiplier;
            var requestedVisibleCoverageRadiusMeters =
                sampleStream ==
                    RoundMapMagicVirtualSampleStream.Local
                    ? qualityProfile.LocalCoverageRadiusMeters
                    : qualityProfile.MidCoverageRadiusMeters;
            var prefetchMarginMeters =
                sampleStream ==
                    RoundMapMagicVirtualSampleStream.Local
                    ? qualityProfile.LocalPrefetchMarginMeters
                    : qualityProfile.MidPrefetchMarginMeters;
            var requestedPrefetchCoverageRadiusMeters =
                requestedVisibleCoverageRadiusMeters +
                prefetchMarginMeters;
            var nextVisibleCoverageRadiusMeters =
                ResolveSampleCenterCoverageRadius(
                    requestedVisibleCoverageRadiusMeters,
                    nextTileSizeX,
                    nextTileSizeZ);
            var nextPrefetchCoverageRadiusMeters =
                ResolveSampleCenterCoverageRadius(
                    requestedPrefetchCoverageRadiusMeters,
                    nextTileSizeX,
                    nextTileSizeZ);
            var nextPatchCenterWorldX =
                (nextSourceTileX + 0.5) *
                mapMagicObject.tileSize.x;
            var nextPatchCenterWorldZ =
                (nextSourceTileZ + 0.5) *
                mapMagicObject.tileSize.z;
            var nextMargins =
                Math.Max(
                    0,
                    sampleStream ==
                        RoundMapMagicVirtualSampleStream.Local
                        ? mapMagicObject.tileMargins
                        : mapMagicObject.draftMargins);
            var nextRadius =
                ResolveStreamedTileRadius(
                    nextPrefetchCoverageRadiusMeters,
                    nextTileSizeX,
                    nextTileSizeZ);

            if (DesiredGridChanged(
                    mapMagicObject,
                    nextCenterKey,
                    nextRadius,
                    nextResolution,
                    nextTileSizeX,
                    nextTileSizeZ,
                    nextVisibleCoverageRadiusMeters,
                    nextPrefetchCoverageRadiusMeters,
                    nextPatchCenterWorldX,
                    nextPatchCenterWorldZ,
                    nextMargins))
            {
                RebuildDesiredGrid(
                    mapMagicObject,
                    nextCenterKey,
                    nextRadius,
                    nextResolution,
                    nextTileSizeX,
                    nextTileSizeZ,
                    nextVisibleCoverageRadiusMeters,
                    nextPrefetchCoverageRadiusMeters,
                    nextPatchCenterWorldX,
                    nextPatchCenterWorldZ,
                    nextMargins);
            }

            sampleFace =
                nextCenterKey.Face;
            sourceTileX =
                nextSourceTileX;
            sourceTileZ =
                nextSourceTileZ;
            virtualTileX =
                nextCenterKey.TileX;
            virtualTileZ =
                nextCenterKey.TileZ;
            virtualTileSizeMeters =
                nextTileSizeX;
            resolvedStreamResolution =
                nextResolution;
            resolvedStreamedTileRadius =
                nextRadius;
            resolvedVisibleCoverageRadiusMeters =
                requestedVisibleCoverageRadiusMeters;
            resolvedPrefetchCoverageRadiusMeters =
                requestedPrefetchCoverageRadiusMeters;

            RefreshSampleDiagnostics();
            StartNextGeneration(
                mapMagicObject,
                nextResolution,
                nextTileSizeX,
                nextTileSizeZ,
                nextMargins);
        }

        private void UpdateStreamingState(
            RoundMapMagicSurfaceQualityProfile qualityProfile)
        {
            if (sampleStream ==
                RoundMapMagicVirtualSampleStream.Local)
            {
                resolvedActivationAltitudeMeters =
                    qualityProfile.LocalActivationAltitudeMeters >
                        0.0
                        ? qualityProfile.LocalActivationAltitudeMeters
                        : qualityProfile.LocalCoverageRadiusMeters;
                resolvedReleaseAltitudeMeters =
                    qualityProfile.LocalReleaseAltitudeMeters >
                        resolvedActivationAltitudeMeters
                        ? qualityProfile.LocalReleaseAltitudeMeters
                        : resolvedActivationAltitudeMeters +
                            Math.Max(
                                1000.0,
                                qualityProfile.TransitionOverlapMeters);
            }
            else
            {
                resolvedActivationAltitudeMeters =
                    qualityProfile.MidActivationAltitudeMeters >
                        0.0
                        ? qualityProfile.MidActivationAltitudeMeters
                        : qualityProfile.MidCoverageRadiusMeters;
                resolvedReleaseAltitudeMeters =
                    qualityProfile.MidReleaseAltitudeMeters >
                        resolvedActivationAltitudeMeters
                        ? qualityProfile.MidReleaseAltitudeMeters
                        : resolvedActivationAltitudeMeters +
                            Math.Max(
                                1000.0,
                                qualityProfile.TransitionOverlapMeters);
            }

            if (!surfaceFrame.HasAnchorAddress)
            {
                anchorAltitudeMeters = default;
                streamingActive = false;
                return;
            }

            anchorAltitudeMeters =
                surfaceFrame.AnchorAltitudeMeters;

            if (streamingActive)
            {
                if (anchorAltitudeMeters >
                    resolvedReleaseAltitudeMeters)
                {
                    streamingActive = false;
                }

                return;
            }

            if (anchorAltitudeMeters <=
                resolvedActivationAltitudeMeters)
            {
                streamingActive = true;
            }
        }

        private static int ResolveStreamedTileRadius(
            double coverageRadiusMeters,
            double tileSizeX,
            double tileSizeZ)
        {
            var smallestTileSize =
                Math.Min(
                    tileSizeX,
                    tileSizeZ);
            var largestTileSize =
                Math.Max(
                    tileSizeX,
                    tileSizeZ);
            var searchCoverageMeters =
                coverageRadiusMeters +
                largestTileSize *
                    0.5;

            var tileRadiusValue =
                Math.Ceiling(
                    searchCoverageMeters /
                    smallestTileSize);

            if (tileRadiusValue >=
                MaximumStreamedTileRadius)
            {
                return
                    MaximumStreamedTileRadius;
            }

            var tileRadius =
                (int)tileRadiusValue;

            return
                Mathf.Clamp(
                    tileRadius,
                    0,
                    MaximumStreamedTileRadius);
        }

        private double ResolveSampleCenterCoverageRadius(
            double requestedCoverageRadiusMeters,
            double tileSizeX,
            double tileSizeZ)
        {
            if (sampleStream !=
                RoundMapMagicVirtualSampleStream.Local)
            {
                return
                    requestedCoverageRadiusMeters;
            }

            return
                Math.Max(
                    0.0,
                    requestedCoverageRadiusMeters -
                    Math.Max(
                        tileSizeX,
                        tileSizeZ) *
                        0.5);
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

        public void CopyVisibleSamplesTo(
            List<RoundMapMagicVirtualHeightSample> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(
                    nameof(destination));
            }

            destination.Clear();

            foreach (var key in
                visibleKeys)
            {
                if (samples.TryGetValue(
                        key,
                        out var sample))
                {
                    destination.Add(sample);
                }
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
            double nextVisibleCoverageRadiusMeters,
            double nextCoverageRadiusMeters,
            double nextPatchCenterWorldX,
            double nextPatchCenterWorldZ,
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
                desiredVisibleCoverageRadiusMeters !=
                    nextVisibleCoverageRadiusMeters ||
                desiredCoverageRadiusMeters !=
                    nextCoverageRadiusMeters ||
                desiredPatchCenterWorldX !=
                    nextPatchCenterWorldX ||
                desiredPatchCenterWorldZ !=
                    nextPatchCenterWorldZ ||
                desiredMargins != nextMargins;
        }

        private void RebuildDesiredGrid(
            MapMagicObject mapMagicObject,
            SampleKey nextCenterKey,
            int nextRadius,
            int nextResolution,
            double nextTileSizeX,
            double nextTileSizeZ,
            double nextVisibleCoverageRadiusMeters,
            double nextCoverageRadiusMeters,
            double nextPatchCenterWorldX,
            double nextPatchCenterWorldZ,
            int nextMargins)
        {
            var sourceSettingsChanged =
                generationSource != mapMagicObject ||
                desiredResolution != nextResolution ||
                desiredTileSizeX != nextTileSizeX ||
                desiredTileSizeZ != nextTileSizeZ ||
                desiredMargins != nextMargins;

            if (sourceSettingsChanged)
            {
                generationVersion++;

                if (generationStop != null)
                {
                    generationStop.stop = true;
                }
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
            desiredVisibleCoverageRadiusMeters =
                nextVisibleCoverageRadiusMeters;
            desiredCoverageRadiusMeters =
                nextCoverageRadiusMeters;
            desiredPatchCenterWorldX =
                nextPatchCenterWorldX;
            desiredPatchCenterWorldZ =
                nextPatchCenterWorldZ;
            desiredMargins =
                nextMargins;
            desiredKeys.Clear();
            visibleKeys.Clear();
            generationQueue.Clear();

            for (var offsetZ = -nextRadius;
                offsetZ <= nextRadius;
                offsetZ++)
            {
                for (var offsetX = -nextRadius;
                    offsetX <= nextRadius;
                    offsetX++)
                {
                    var key =
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
                        };

                    if (IsSampleCenterWithinCoverage(
                            key,
                            nextTileSizeX,
                            nextTileSizeZ,
                            nextPatchCenterWorldX,
                            nextPatchCenterWorldZ,
                            nextCoverageRadiusMeters))
                    {
                        desiredKeys.Add(key);

                        if (IsSampleCenterWithinCoverage(
                                key,
                                nextTileSizeX,
                                nextTileSizeZ,
                                nextPatchCenterWorldX,
                                nextPatchCenterWorldZ,
                                nextVisibleCoverageRadiusMeters))
                        {
                            visibleKeys.Add(key);
                        }
                    }
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
                visibleKeys.Count;
            expectedPrefetchSampleCount =
                desiredKeys.Count;
            readyVisibleSampleCount =
                CountReadyVisibleSamples();
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

                        if (desiredKeys.Contains(key) &&
                            !samples.ContainsKey(key))
                        {
                            generationQueue.Add(key);
                        }
                    }
                }
            }
        }

        private static bool IsSampleCenterWithinCoverage(
            SampleKey key,
            double tileSizeX,
            double tileSizeZ,
            double patchCenterWorldX,
            double patchCenterWorldZ,
            double coverageRadiusMeters)
        {
            var sampleCenterWorldX =
                (key.TileX + 0.5) *
                tileSizeX;
            var sampleCenterWorldZ =
                (key.TileZ + 0.5) *
                tileSizeZ;
            var deltaX =
                sampleCenterWorldX -
                patchCenterWorldX;
            var deltaZ =
                sampleCenterWorldZ -
                patchCenterWorldZ;

            return
                deltaX * deltaX +
                    deltaZ * deltaZ <=
                coverageRadiusMeters *
                    coverageRadiusMeters;
        }

        private int ResolveNextGenerationIndex()
        {
            lastSelectedViewBand = default;
            var resolvedView =
                ResolveGenerationView();
            hasGenerationView =
                resolvedView != null;

            if (!prioritizeVisibleTiles ||
                generationQueue.Count <= 1 ||
                resolvedView == null)
            {
                return 0;
            }

            var catchUpInterval =
                Mathf.Max(
                    2,
                    offscreenCatchUpInterval);

            if (generationRequestCount > 0 &&
                generationRequestCount %
                    catchUpInterval ==
                    0)
            {
                catchUpRequestCount++;
                lastSelectedViewBand = -1;
                return 0;
            }

            var viewCamera =
                resolvedView.GetComponent<Camera>();
            var bestIndex = 0;
            var bestCoverageBand = int.MaxValue;
            var bestBand = int.MaxValue;
            var bestDistanceSquared =
                float.PositiveInfinity;
            var bestAlignment =
                float.NegativeInfinity;

            for (var index = 0;
                index < generationQueue.Count;
                index++)
            {
                var key =
                    generationQueue[index];

                if (!desiredKeys.Contains(key) ||
                    samples.ContainsKey(key) ||
                    !TryGetSampleCenterScenePosition(
                        key,
                        out var tileCenterScenePosition))
                {
                    continue;
                }

                var coverageBand =
                    visibleKeys.Contains(key)
                        ? 0
                        : 1;

                var viewBand =
                    ResolveViewBand(
                        resolvedView,
                        viewCamera,
                        tileCenterScenePosition,
                        out var distanceSquared,
                        out var alignment);

                if (coverageBand >
                        bestCoverageBand ||
                    coverageBand ==
                        bestCoverageBand &&
                    viewBand > bestBand ||
                    coverageBand ==
                        bestCoverageBand &&
                    viewBand == bestBand &&
                    distanceSquared >
                        bestDistanceSquared ||
                    coverageBand ==
                        bestCoverageBand &&
                    viewBand == bestBand &&
                    distanceSquared ==
                        bestDistanceSquared &&
                    alignment <= bestAlignment)
                {
                    continue;
                }

                bestIndex = index;
                bestCoverageBand =
                    coverageBand;
                bestBand = viewBand;
                bestDistanceSquared =
                    distanceSquared;
                bestAlignment =
                    alignment;
            }

            lastSelectedViewBand =
                bestBand != int.MaxValue
                    ? bestBand
                    : default;

            if (bestBand == 0)
            {
                viewPrioritizedRequestCount++;
            }

            return bestIndex;
        }

        private Transform ResolveGenerationView()
        {
            if (generationView != null &&
                generationView.gameObject.activeInHierarchy)
            {
                return generationView;
            }

            var mainCamera =
                Camera.main;

            return
                mainCamera != null
                    ? mainCamera.transform
                    : null;
        }

        private int ResolveViewBand(
            Transform resolvedView,
            Camera viewCamera,
            Vector3 tileCenterScenePosition,
            out float distanceSquared,
            out float alignment)
        {
            var offset =
                tileCenterScenePosition -
                resolvedView.position;
            distanceSquared =
                offset.sqrMagnitude;

            if (distanceSquared <=
                Mathf.Epsilon)
            {
                alignment = 1.0f;
                return 0;
            }

            alignment =
                Vector3.Dot(
                    resolvedView.forward,
                    offset /
                        Mathf.Sqrt(
                            distanceSquared));

            if (viewCamera != null)
            {
                var viewportPosition =
                    viewCamera.WorldToViewportPoint(
                        tileCenterScenePosition);
                var overscan =
                    Mathf.Clamp(
                        viewOverscan,
                        0.0f,
                        0.5f);

                if (viewportPosition.z > 0.0f &&
                    viewportPosition.x >= -overscan &&
                    viewportPosition.x <=
                        1.0f + overscan &&
                    viewportPosition.y >= -overscan &&
                    viewportPosition.y <=
                        1.0f + overscan)
                {
                    return 0;
                }
            }
            else if (alignment >= 0.5f)
            {
                return 0;
            }

            return
                alignment > 0.0f
                    ? 1
                    : 2;
        }

        private bool TryGetSampleCenterScenePosition(
            SampleKey key,
            out Vector3 scenePosition)
        {
            if (surfaceFrame == null ||
                desiredTileSizeX <= 0.0 ||
                desiredTileSizeZ <= 0.0 ||
                !surfaceFrame.TryGetPlanetCenterScenePosition(
                    out var planetCenterScenePosition))
            {
                scenePosition = default;
                return false;
            }

            var planetRadiusMeters =
                surfaceFrame.PlanetRadiusMeters;
            var tileCenterUMeters =
                (key.TileX + 0.5) *
                desiredTileSizeX;
            var tileCenterVMeters =
                -((key.TileZ + 0.5) *
                desiredTileSizeZ);
            var tileCenterAddress =
                new CubeSphereAddress(
                    key.Face,
                    CubeSphereMapping.MetersToFaceCoordinate(
                        tileCenterUMeters,
                        planetRadiusMeters),
                    CubeSphereMapping.MetersToFaceCoordinate(
                        tileCenterVMeters,
                        planetRadiusMeters),
                    0.0);
            var tileCenterDirection =
                CubeSphereMapping.AddressToDirection(
                    tileCenterAddress);

            scenePosition =
                planetCenterScenePosition +
                new Vector3(
                    (float)tileCenterDirection.x,
                    (float)tileCenterDirection.y,
                    (float)tileCenterDirection.z) *
                    (float)planetRadiusMeters;
            return true;
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

                var generationIndex =
                    ResolveNextGenerationIndex();
                key =
                    generationQueue[generationIndex];
                generationQueue.RemoveAt(
                    generationIndex);
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

            var activeSurfaceDefinition =
                surfaceSession != null
                    ? surfaceSession.ActiveSurfaceDefinition
                    : null;
            var configuredSurfaceDefinition =
                surfaceSession != null
                    ? surfaceSession.ConfiguredSurfaceDefinition
                    : null;
            var surfaceDefinition =
                activeSurfaceDefinition != null
                    ? activeSurfaceDefinition
                    : configuredSurfaceDefinition;
            var data =
                new RoundMapMagicSphericalTileData
                {
                    Face =
                        key.Face,
                    PlanetRadiusMeters =
                        surfaceFrame.PlanetRadiusMeters,
                    SurfaceSeed =
                        surfaceDefinition != null
                            ? surfaceDefinition.SurfaceSeed
                            : 0,
                    area =
                        new Area(
                            new Coord(
                                key.TileX,
                                key.TileZ),
                            resolution,
                            margins,
                            new Vector2D(
                                (float)tileSizeX,
                                (float)tileSizeZ)),
                    globals =
                        mapMagicObject.globals,
                    random =
                        graph.random,
                    isPreview =
                        false,
                    isDraft =
                        sampleStream !=
                            RoundMapMagicVirtualSampleStream.Local
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
                            data.ApplyOfType<
                                TexturesOutput200.ApplyData>(),
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
            TexturesOutput200.ApplyData textureData,
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
                    maximumHeight,
                    textureData != null
                        ? textureData.splats
                        : null,
                    textureData != null
                        ? textureData.prototypes
                        : null);
        }

        private void RefreshSampleDiagnostics()
        {
            readyVisibleSampleCount =
                CountReadyVisibleSamples();
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
            hasTextureSample =
                diagnosticSample != null &&
                diagnosticSample.HasTextureData;
            sampleControlResolution =
                diagnosticSample != null
                    ? diagnosticSample.ControlResolution
                    : 0;
            sampleTerrainLayerCount =
                diagnosticSample != null
                    ? diagnosticSample.TerrainLayerCount
                    : 0;
        }

        private int CountReadyVisibleSamples()
        {
            var count = 0;

            foreach (var key in
                visibleKeys)
            {
                if (samples.ContainsKey(key))
                {
                    count++;
                }
            }

            return count;
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
            desiredVisibleCoverageRadiusMeters = default;
            desiredCoverageRadiusMeters = default;
            desiredPatchCenterWorldX = default;
            desiredPatchCenterWorldZ = default;
            desiredMargins = default;
            desiredKeys.Clear();
            visibleKeys.Clear();
            generationQueue.Clear();
            samples.Clear();
            currentSample = null;
            expectedSampleCount = default;
            readyVisibleSampleCount = default;
            expectedPrefetchSampleCount = default;
            cachedSampleCount = default;
            queuedSampleCount = default;
            resolvedStreamedTileRadius = default;
            resolvedVisibleCoverageRadiusMeters = default;
            resolvedPrefetchCoverageRadiusMeters = default;
            hasGenerationView = false;
            lastSelectedViewBand = default;
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
            streamingActive = false;
            generationVersion++;

            if (generationStop != null)
            {
                generationStop.stop = true;
            }

            ClearStreamingState();
        }

        private void ResolveLocalReferences()
        {
            if (surfaceFrame == null)
            {
                surfaceFrame =
                    GetComponent<GePlanetSurfaceFrame>();
            }

            if (addressTracker == null)
            {
                addressTracker =
                    GetComponent<CubeSphereTerrainAddressTracker>();
            }

            if (surfaceSession == null)
            {
                surfaceSession =
                    GetComponent<RoundMapMagicSurfaceSession>();
            }

            if (rootPool == null)
            {
                rootPool =
                    GetComponent<CubeSphereMapMagicRootPool>();
            }
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
        private readonly float[,,] controlWeights;
        private readonly TerrainLayer[] terrainLayers;

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

        public bool HasTextureData =>
            controlWeights != null &&
            terrainLayers != null &&
            terrainLayers.Length > 0 &&
            controlWeights.GetLength(0) > 0 &&
            controlWeights.GetLength(1) > 0 &&
            controlWeights.GetLength(2) ==
                terrainLayers.Length;

        public int ControlResolution =>
            controlWeights != null
                ? controlWeights.GetLength(0)
                : 0;

        public int TerrainLayerCount =>
            terrainLayers != null
                ? terrainLayers.Length
                : 0;

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
            float maximumHeightMeters,
            float[,,] controlWeights,
            TerrainLayer[] terrainLayers)
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
            this.controlWeights =
                controlWeights;
            this.terrainLayers =
                terrainLayers;
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

        public float GetControlWeight(
            int x,
            int z,
            int layer)
        {
            if (controlWeights == null)
            {
                throw new InvalidOperationException(
                    "This virtual MapMagic sample does not contain texture control data.");
            }

            var resolutionZ =
                controlWeights.GetLength(0);
            var resolutionX =
                controlWeights.GetLength(1);
            var layerCount =
                controlWeights.GetLength(2);

            if (x < 0 ||
                x >= resolutionX ||
                z < 0 ||
                z >= resolutionZ ||
                layer < 0 ||
                layer >= layerCount)
            {
                throw new ArgumentOutOfRangeException(
                    $"Control coordinate ({x}, {z}, {layer}) is outside dimensions {resolutionX}x{resolutionZ}x{layerCount}.");
            }

            return
                controlWeights[
                    z,
                    x,
                    layer];
        }

        public TerrainLayer GetTerrainLayer(
            int index)
        {
            if (terrainLayers == null ||
                index < 0 ||
                index >= terrainLayers.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }

            return
                terrainLayers[index];
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
