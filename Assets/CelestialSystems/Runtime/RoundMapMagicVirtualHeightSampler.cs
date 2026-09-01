/*
 * Generates and caches a larger low-resolution height sample directly from the active MapMagic graph without creating a Unity Terrain.
 */

using System;
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

            public bool Equals(
                SampleKey other)
            {
                return
                    Face == other.Face &&
                    TileX == other.TileX &&
                    TileZ == other.TileZ;
            }
        }

        [Header("Configuration")]
        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private CubeSphereMapMagicRootPool rootPool;

        [SerializeField]
        private bool generateAutomatically = true;

        [Header("Runtime Request")]
        [SerializeField]
        private bool isGenerating;

        [SerializeField]
        private int generationRequestCount;

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

        private RoundMapMagicVirtualHeightSample currentSample;
        private bool hasAttemptedKey;
        private SampleKey attemptedKey;

        public bool HasSample =>
            hasSample;

        public RoundMapMagicVirtualHeightSample CurrentSample =>
            currentSample;

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
            if (!generateAutomatically ||
                surfaceSession == null ||
                rootPool == null ||
                !surfaceSession.HasActiveSession)
            {
                ClearCurrentSample();
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
                return;
            }

            var tileSizeMultiplier =
                qualityProfile.MidTileSizeMultiplier;
            var nextKey =
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

            if (hasAttemptedKey &&
                attemptedKey.Equals(
                    nextKey))
            {
                return;
            }

            attemptedKey =
                nextKey;
            hasAttemptedKey = true;
            GenerateSample(
                primaryRoot,
                qualityProfile,
                nextKey);
        }

        public bool TryGetCurrentSample(
            CubeSphereFace face,
            int tileX,
            int tileZ,
            out RoundMapMagicVirtualHeightSample sample)
        {
            sample =
                currentSample;

            return
                sample != null &&
                sample.Face == face &&
                sample.TileX == tileX &&
                sample.TileZ == tileZ;
        }

        private void GenerateSample(
            CubeSphereMapMagicCoordinateDriver primaryRoot,
            RoundMapMagicSurfaceQualityProfile qualityProfile,
            SampleKey key)
        {
            var mapMagicObject =
                primaryRoot.MapMagicObject;
            var graph =
                mapMagicObject.graph;

            if (graph == null)
            {
                lastError =
                    "The active MapMagic root has no graph.";
                return;
            }

            var stopwatch =
                System.Diagnostics.Stopwatch.StartNew();
            isGenerating = true;
            generationRequestCount++;

            try
            {
                var resolution =
                    qualityProfile.MidMeshResolution;
                var tileSizeMultiplier =
                    qualityProfile.MidTileSizeMultiplier;
                var virtualTileSize =
                    new Vector2D(
                        mapMagicObject.tileSize.x *
                            tileSizeMultiplier,
                        mapMagicObject.tileSize.z *
                            tileSizeMultiplier);
                var area =
                    new Area(
                        new Coord(
                            key.TileX,
                            key.TileZ),
                        resolution,
                        Math.Max(
                            0,
                            mapMagicObject.draftMargins),
                        virtualTileSize);
                var data =
                    new TileData
                    {
                        area =
                            area,
                        globals =
                            mapMagicObject.globals,
                        random =
                            graph.random,
                        isPreview =
                            false,
                        isDraft =
                            true
                    };
                var stop =
                    new StopToken();

                try
                {
                    graph.Prepare(
                        data,
                        null);
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

                    currentSample =
                        CreateSample(
                            key,
                            area,
                            data.heights,
                            resolution);
                }
                finally
                {
                    data.Clear(
                        clearApply: true,
                        inSubs: true);
                }

                sampleFace =
                    key.Face;
                sourceTileX =
                    primaryRoot.MapMagicTileX;
                sourceTileZ =
                    primaryRoot.MapMagicTileZ;
                virtualTileX =
                    key.TileX;
                virtualTileZ =
                    key.TileZ;
                virtualTileSizeMeters =
                    currentSample.WorldSizeXMeters;
                resolvedMidResolution =
                    currentSample.Resolution;
                sampleVertexCount =
                    currentSample.VertexCount;
                minimumHeightMeters =
                    currentSample.MinimumHeightMeters;
                maximumHeightMeters =
                    currentSample.MaximumHeightMeters;
                centerHeightMeters =
                    currentSample.CenterHeightMeters;
                hasSample = true;
                lastError =
                    string.Empty;
            }
            catch (Exception exception)
            {
                currentSample = null;
                hasSample = false;
                lastError =
                    exception.GetBaseException().Message;

                Debug.LogError(
                    $"Virtual MapMagic height generation failed: {lastError}",
                    this);
            }
            finally
            {
                stopwatch.Stop();
                generationMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds;
                isGenerating = false;
            }
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

        private void ClearCurrentSample()
        {
            currentSample = null;
            hasSample = false;
            isGenerating = false;
            sampleVertexCount = default;
            minimumHeightMeters = default;
            maximumHeightMeters = default;
            centerHeightMeters = default;
            hasAttemptedKey = false;
        }

        private void OnDisable()
        {
            ClearCurrentSample();
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
                    nameof(
                        destination));
            }

            if (destination.Length <
                heightsMeters.Length)
            {
                throw new ArgumentException(
                    "The destination array is too small for this height sample.",
                    nameof(
                        destination));
            }

            Array.Copy(
                heightsMeters,
                destination,
                heightsMeters.Length);
        }
    }
}
