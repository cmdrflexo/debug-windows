/*
 * Evaluates the active MapMagic graph into six coarse runtime height and surface-layer control maps without creating Unity Terrains.
 */

using System;
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
    [DefaultExecutionOrder(275)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicSphericalFaceMapCache :
        MonoBehaviour
    {
        private const int MaximumCachedLayerCount =
            4;

        private sealed class FaceGenerationResult
        {
            public int Version;
            public CubeSphereFace Face;
            public int Resolution;
            public float[] NormalizedHeights;
            public float HeightScaleMeters;
            public float[,,] ControlWeights;
            public TerrainLayer[] TerrainLayers;
            public float MinimumHeightMeters;
            public float MaximumHeightMeters;
            public double Milliseconds;
            public string Error;
        }

        private static readonly CubeSphereFace[] Faces =
        {
            CubeSphereFace.PositiveX,
            CubeSphereFace.NegativeX,
            CubeSphereFace.PositiveY,
            CubeSphereFace.NegativeY,
            CubeSphereFace.PositiveZ,
            CubeSphereFace.NegativeZ
        };

        [Header("Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private CubeSphereMapMagicRootPool rootPool;

        [SerializeField]
        private bool generateAutomatically = true;

        [SerializeField]
        [Range(17, 513)]
        private int faceResolution = 129;

        [SerializeField]
        [Range(0, 16)]
        private int margins = 2;

        [Header("Runtime Generation")]
        [SerializeField]
        private bool isGenerating;

        [SerializeField]
        private bool isComplete;

        [SerializeField]
        private int completedFaceCount;

        [SerializeField]
        private CubeSphereFace currentFace;

        [SerializeField]
        private int generatedSampleCount;

        [SerializeField]
        private float minimumHeightMeters;

        [SerializeField]
        private float maximumHeightMeters;

        [SerializeField]
        private float heightScaleMeters;

        [SerializeField]
        private double lastFaceGenerationMilliseconds;

        [SerializeField]
        private double totalGenerationMilliseconds;

        [SerializeField]
        private string lastError;

        [Header("Runtime Height Maps")]
        [SerializeField]
        private Texture2D positiveX;

        [SerializeField]
        private Texture2D negativeX;

        [SerializeField]
        private Texture2D positiveY;

        [SerializeField]
        private Texture2D negativeY;

        [SerializeField]
        private Texture2D positiveZ;

        [SerializeField]
        private Texture2D negativeZ;

        [Header("Runtime Surface Layers")]
        [SerializeField]
        private int terrainLayerCount;

        [SerializeField]
        private TerrainLayer[] terrainLayers =
            new TerrainLayer[0];

        [SerializeField]
        private Texture2D positiveXControl;

        [SerializeField]
        private Texture2D negativeXControl;

        [SerializeField]
        private Texture2D positiveYControl;

        [SerializeField]
        private Texture2D negativeYControl;

        [SerializeField]
        private Texture2D positiveZControl;

        [SerializeField]
        private Texture2D negativeZControl;

        private Task<FaceGenerationResult> generationTask;
        private StopToken generationStop;
        private bool generationRequested;
        private int nextFaceIndex;
        private int generationVersion;

        public bool IsComplete =>
            isComplete;

        public int CompletedFaceCount =>
            completedFaceCount;

        public float HeightScaleMeters =>
            heightScaleMeters;

        public bool HasTextureData =>
            isComplete &&
            terrainLayerCount > 0 &&
            terrainLayers != null &&
            terrainLayers.Length ==
                terrainLayerCount &&
            positiveXControl != null &&
            negativeXControl != null &&
            positiveYControl != null &&
            negativeYControl != null &&
            positiveZControl != null &&
            negativeZControl != null;

        public int TerrainLayerCount =>
            terrainLayerCount;

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

            return terrainLayers[index];
        }

        public Texture2D GetHeightMap(CubeSphereFace face)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                    return positiveX;

                case CubeSphereFace.NegativeX:
                    return negativeX;

                case CubeSphereFace.PositiveY:
                    return positiveY;

                case CubeSphereFace.NegativeY:
                    return negativeY;

                case CubeSphereFace.PositiveZ:
                    return positiveZ;

                case CubeSphereFace.NegativeZ:
                    return negativeZ;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        "Unknown cube-sphere face.");
            }
        }

        public Texture2D GetControlMap(CubeSphereFace face)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                    return positiveXControl;

                case CubeSphereFace.NegativeX:
                    return negativeXControl;

                case CubeSphereFace.PositiveY:
                    return positiveYControl;

                case CubeSphereFace.NegativeY:
                    return negativeYControl;

                case CubeSphereFace.PositiveZ:
                    return positiveZControl;

                case CubeSphereFace.NegativeZ:
                    return negativeZControl;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        "Unknown cube-sphere face.");
            }
        }

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
            if (generateAutomatically)
            {
                BeginGeneration();
            }
        }

        private void Update()
        {
            CompleteCurrentFace();

            if (generationRequested &&
                generationTask == null &&
                nextFaceIndex < Faces.Length)
            {
                StartNextFace();
            }
        }

        [ContextMenu("Generate Spherical Face Maps")]
        public void BeginGeneration()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Spherical face maps are runtime caches. Enter Play mode to generate them.",
                    this);
                return;
            }

            if (generationTask != null)
            {
                Debug.LogWarning(
                    "Spherical face-map generation is already running.",
                    this);
                return;
            }

            generationVersion++;
            generationRequested = true;
            nextFaceIndex = 0;
            isGenerating = false;
            isComplete = false;
            completedFaceCount = 0;
            generatedSampleCount = 0;
            minimumHeightMeters = 0.0f;
            maximumHeightMeters = 0.0f;
            heightScaleMeters = 0.0f;
            terrainLayerCount = 0;
            terrainLayers =
                new TerrainLayer[0];
            lastFaceGenerationMilliseconds = 0.0;
            totalGenerationMilliseconds = 0.0;
            lastError = string.Empty;
            DestroyGeneratedTextures();
        }

        private void StartNextFace()
        {
            var generationSource =
                rootPool != null
                    ? rootPool.GenerationSource
                    : null;
            var surfaceDefinition =
                ResolveSurfaceDefinition();
            var graph =
                surfaceDefinition != null &&
                surfaceDefinition.Graph != null
                    ? surfaceDefinition.Graph
                    : generationSource != null
                    ? generationSource.graph
                    : null;
            var planetRadiusMeters =
                surfaceFrame != null
                    ? surfaceFrame.PlanetRadiusMeters
                    : 0.0;

            if (generationSource == null ||
                graph == null ||
                !IsFinite(planetRadiusMeters) ||
                planetRadiusMeters <= 0.0)
            {
                FailGeneration(
                    "A valid surface frame and MapMagic generation source are required.");
                return;
            }

            var surfaceSeed =
                surfaceDefinition != null
                    ? surfaceDefinition.SurfaceSeed
                    : 0;
            var resolvedResolution =
                Mathf.Max(
                    2,
                    faceResolution);
            var resolvedMargins =
                Mathf.Max(
                    0,
                    margins);
            var halfFaceSizeMeters =
                CubeSphereMapping.FaceCoordinateToMeters(
                    1.0,
                    planetRadiusMeters);
            var faceSizeMeters =
                halfFaceSizeMeters *
                2.0;
            var face =
                Faces[nextFaceIndex];
            var area =
                new Area(
                    new Vector2D(
                        (float)-halfFaceSizeMeters,
                        (float)-halfFaceSizeMeters),
                    new Vector2D(
                        (float)faceSizeMeters,
                        (float)faceSizeMeters),
                    resolvedResolution,
                    resolvedMargins);
            var data =
                new RoundMapMagicSphericalTileData
                {
                    Face =
                        face,
                    PlanetRadiusMeters =
                        planetRadiusMeters,
                    SurfaceSeed =
                        surfaceSeed,
                    MapWorldOriginXMeters =
                        -halfFaceSizeMeters,
                    MapWorldOriginZMeters =
                        -halfFaceSizeMeters,
                    MapWorldSizeXMeters =
                        faceSizeMeters,
                    MapWorldSizeZMeters =
                        faceSizeMeters,
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
                FailGeneration(
                    exception.GetBaseException().Message);
                return;
            }

            currentFace =
                face;
            isGenerating = true;
            var stop =
                new StopToken();
            generationStop =
                stop;
            var requestVersion =
                generationVersion;

            generationTask =
                Task.Run(
                    () => GenerateFace(
                        graph,
                        data,
                        stop,
                        face,
                        resolvedResolution,
                        requestVersion));
        }

        private static FaceGenerationResult GenerateFace(
            MapMagic.Nodes.Graph graph,
            RoundMapMagicSphericalTileData data,
            StopToken stop,
            CubeSphereFace face,
            int resolution,
            int requestVersion)
        {
            var result =
                new FaceGenerationResult
                {
                    Version =
                        requestVersion,
                    Face =
                        face,
                    Resolution =
                        resolution
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
                            "MapMagic stopped the spherical face-map request.");
                    }

                    if (data.heights == null)
                    {
                        throw new InvalidOperationException(
                            "The MapMagic graph did not produce a finalized Height output for the spherical face-map request.");
                    }

                    result.NormalizedHeights =
                        CopyActiveHeights(
                            data,
                            resolution,
                            out var minimumHeight,
                            out var maximumHeight);
                    result.HeightScaleMeters =
                        data.heights.worldSize.y;
                    CaptureTextureData(
                        result,
                        data.ApplyOfType<
                            TexturesOutput200.ApplyData>(),
                        resolution);
                    result.MinimumHeightMeters =
                        minimumHeight;
                    result.MaximumHeightMeters =
                        maximumHeight;
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

        private static void CaptureTextureData(
            FaceGenerationResult result,
            TexturesOutput200.ApplyData textureData,
            int resolution)
        {
            if (textureData == null ||
                textureData.splats == null ||
                textureData.prototypes == null ||
                textureData.prototypes.Length == 0)
            {
                return;
            }

            var layerCount =
                textureData.prototypes.Length;

            if (layerCount >
                MaximumCachedLayerCount)
            {
                throw new InvalidOperationException(
                    $"The spherical face-map cache currently supports up to {MaximumCachedLayerCount} terrain layers, but the graph produced {layerCount}.");
            }

            if (textureData.splats.GetLength(0) !=
                    resolution ||
                textureData.splats.GetLength(1) !=
                    resolution ||
                textureData.splats.GetLength(2) !=
                    layerCount)
            {
                throw new InvalidOperationException(
                    "The MapMagic texture control dimensions do not match the spherical face-map request.");
            }

            result.ControlWeights =
                textureData.splats;
            result.TerrainLayers =
                textureData.prototypes;
        }

        private static float[] CopyActiveHeights(
            RoundMapMagicSphericalTileData data,
            int resolution,
            out float minimumHeightMeters,
            out float maximumHeightMeters)
        {
            var normalizedHeights =
                new float[
                    resolution *
                    resolution];
            var heightScaleMeters =
                data.heights.worldSize.y;
            minimumHeightMeters =
                float.PositiveInfinity;
            maximumHeightMeters =
                float.NegativeInfinity;

            for (var z = 0;
                z < resolution;
                z++)
            {
                var pixelZ =
                    data.area.active.rect.offset.z +
                    z;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var pixelX =
                        data.area.active.rect.offset.x +
                        x;
                    var normalizedHeight =
                        data.heights[
                            pixelX,
                            pixelZ];
                    var heightMeters =
                        normalizedHeight *
                        heightScaleMeters;
                    var index =
                        z *
                        resolution +
                        x;

                    normalizedHeights[index] =
                        normalizedHeight;
                    minimumHeightMeters =
                        Mathf.Min(
                            minimumHeightMeters,
                            heightMeters);
                    maximumHeightMeters =
                        Mathf.Max(
                            maximumHeightMeters,
                            heightMeters);
                }
            }

            return normalizedHeights;
        }

        private void CompleteCurrentFace()
        {
            if (generationTask == null ||
                !generationTask.IsCompleted)
            {
                return;
            }

            FaceGenerationResult result;

            try
            {
                result =
                    generationTask.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                result =
                    new FaceGenerationResult
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

            if (result.Version !=
                generationVersion)
            {
                return;
            }

            if (!string.IsNullOrEmpty(
                    result.Error))
            {
                FailGeneration(
                    result.Error);
                return;
            }

            if (!TryAcceptTextureData(
                    result,
                    out var textureError))
            {
                FailGeneration(
                    textureError);
                return;
            }

            SetHeightMap(
                result.Face,
                CreateHeightMapTexture(
                    result));
            completedFaceCount++;
            generatedSampleCount +=
                result.NormalizedHeights.Length;
            lastFaceGenerationMilliseconds =
                result.Milliseconds;
            totalGenerationMilliseconds +=
                result.Milliseconds;

            if (completedFaceCount == 1)
            {
                heightScaleMeters =
                    result.HeightScaleMeters;
                minimumHeightMeters =
                    result.MinimumHeightMeters;
                maximumHeightMeters =
                    result.MaximumHeightMeters;
            }
            else
            {
                minimumHeightMeters =
                    Mathf.Min(
                        minimumHeightMeters,
                        result.MinimumHeightMeters);
                maximumHeightMeters =
                    Mathf.Max(
                        maximumHeightMeters,
                        result.MaximumHeightMeters);
            }

            nextFaceIndex++;

            if (nextFaceIndex >=
                Faces.Length)
            {
                generationRequested = false;
                isComplete = true;
            }
        }

        private bool TryAcceptTextureData(
            FaceGenerationResult result,
            out string error)
        {
            var resultHasTextureData =
                result.ControlWeights != null &&
                result.TerrainLayers != null &&
                result.TerrainLayers.Length > 0;

            if (completedFaceCount == 0)
            {
                if (resultHasTextureData)
                {
                    terrainLayers =
                        result.TerrainLayers;
                    terrainLayerCount =
                        terrainLayers.Length;
                }
            }
            else
            {
                var cacheHasTextureData =
                    terrainLayerCount > 0;

                if (cacheHasTextureData !=
                    resultHasTextureData)
                {
                    error =
                        "The MapMagic graph produced inconsistent texture outputs across spherical faces.";
                    return false;
                }

                if (resultHasTextureData &&
                    !TerrainLayersMatch(
                        terrainLayers,
                        result.TerrainLayers))
                {
                    error =
                        "The MapMagic graph produced different terrain-layer prototypes across spherical faces.";
                    return false;
                }
            }

            if (resultHasTextureData)
            {
                SetControlMap(
                    result.Face,
                    CreateControlMapTexture(
                        result));
            }

            error = string.Empty;
            return true;
        }

        private static bool TerrainLayersMatch(
            TerrainLayer[] first,
            TerrainLayer[] second)
        {
            if (first == null ||
                second == null ||
                first.Length !=
                    second.Length)
            {
                return false;
            }

            for (var index = 0;
                index < first.Length;
                index++)
            {
                if (first[index] !=
                    second[index])
                {
                    return false;
                }
            }

            return true;
        }

        private static Texture2D CreateControlMapTexture(
            FaceGenerationResult result)
        {
            var weights =
                result.ControlWeights;
            var height =
                weights.GetLength(0);
            var width =
                weights.GetLength(1);
            var layerCount =
                weights.GetLength(2);
            var pixels =
                new Color[
                    width *
                    height];

            for (var z = 0;
                z < height;
                z++)
            {
                for (var x = 0;
                    x < width;
                    x++)
                {
                    pixels[
                        z *
                        width +
                        x] =
                        new Color(
                            layerCount > 0
                                ? weights[z, x, 0]
                                : 0.0f,
                            layerCount > 1
                                ? weights[z, x, 1]
                                : 0.0f,
                            layerCount > 2
                                ? weights[z, x, 2]
                                : 0.0f,
                            layerCount > 3
                                ? weights[z, x, 3]
                                : 0.0f);
                }
            }

            var texture =
                new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name =
                        $"Round MapMagic {result.Face} Control",
                    wrapMode =
                        TextureWrapMode.Clamp,
                    filterMode =
                        FilterMode.Bilinear
                };
            texture.SetPixels(
                pixels);
            texture.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            return texture;
        }

        private static Texture2D CreateHeightMapTexture(
            FaceGenerationResult result)
        {
            var texture =
                new Texture2D(
                    result.Resolution,
                    result.Resolution,
                    TextureFormat.RFloat,
                    false,
                    true)
                {
                    name =
                        $"Round MapMagic {result.Face} Height",
                    wrapMode =
                        TextureWrapMode.Clamp,
                    filterMode =
                        FilterMode.Bilinear
                };
            texture.SetPixelData(
                result.NormalizedHeights,
                0);
            texture.Apply(
                updateMipmaps: false,
                makeNoLongerReadable: false);
            return texture;
        }

        private void SetHeightMap(
            CubeSphereFace face,
            Texture2D texture)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                    positiveX = texture;
                    break;

                case CubeSphereFace.NegativeX:
                    negativeX = texture;
                    break;

                case CubeSphereFace.PositiveY:
                    positiveY = texture;
                    break;

                case CubeSphereFace.NegativeY:
                    negativeY = texture;
                    break;

                case CubeSphereFace.PositiveZ:
                    positiveZ = texture;
                    break;

                case CubeSphereFace.NegativeZ:
                    negativeZ = texture;
                    break;

                default:
                    Destroy(texture);
                    throw new ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        "Unknown cube-sphere face.");
            }
        }

        private void SetControlMap(
            CubeSphereFace face,
            Texture2D texture)
        {
            switch (face)
            {
                case CubeSphereFace.PositiveX:
                    positiveXControl = texture;
                    break;

                case CubeSphereFace.NegativeX:
                    negativeXControl = texture;
                    break;

                case CubeSphereFace.PositiveY:
                    positiveYControl = texture;
                    break;

                case CubeSphereFace.NegativeY:
                    negativeYControl = texture;
                    break;

                case CubeSphereFace.PositiveZ:
                    positiveZControl = texture;
                    break;

                case CubeSphereFace.NegativeZ:
                    negativeZControl = texture;
                    break;

                default:
                    Destroy(texture);
                    throw new ArgumentOutOfRangeException(
                        nameof(face),
                        face,
                        "Unknown cube-sphere face.");
            }
        }

        private RoundMapMagicSurfaceDefinition ResolveSurfaceDefinition()
        {
            if (surfaceSession == null)
            {
                return null;
            }

            return
                surfaceSession.ActiveSurfaceDefinition != null
                    ? surfaceSession.ActiveSurfaceDefinition
                    : surfaceSession.ConfiguredSurfaceDefinition;
        }

        private void FailGeneration(string error)
        {
            generationRequested = false;
            isGenerating = false;
            isComplete = false;
            lastError =
                error;
            Debug.LogError(
                $"Spherical MapMagic face-map generation failed: {error}",
                this);
        }

        private void ResolveLocalReferences()
        {
            if (surfaceFrame == null)
            {
                surfaceFrame =
                    GetComponent<GePlanetSurfaceFrame>();
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

        private void DestroyGeneratedTextures()
        {
            DestroyTexture(
                ref positiveX);
            DestroyTexture(
                ref negativeX);
            DestroyTexture(
                ref positiveY);
            DestroyTexture(
                ref negativeY);
            DestroyTexture(
                ref positiveZ);
            DestroyTexture(
                ref negativeZ);
            DestroyTexture(
                ref positiveXControl);
            DestroyTexture(
                ref negativeXControl);
            DestroyTexture(
                ref positiveYControl);
            DestroyTexture(
                ref negativeYControl);
            DestroyTexture(
                ref positiveZControl);
            DestroyTexture(
                ref negativeZControl);
        }

        private static void DestroyTexture(
            ref Texture2D texture)
        {
            if (texture == null)
            {
                return;
            }

            Destroy(texture);
            texture = null;
        }

        private void OnDisable()
        {
            generationRequested = false;
            generationVersion++;

            if (generationStop != null)
            {
                generationStop.stop = true;
            }

            DestroyGeneratedTextures();
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
