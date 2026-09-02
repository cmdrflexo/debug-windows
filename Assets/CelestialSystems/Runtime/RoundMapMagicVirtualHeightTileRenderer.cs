/*
 * Renders cached MapMagic surface samples as curved cube-sphere meshes without creating Unity Terrains.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(300)]
    public sealed class RoundMapMagicVirtualHeightTileRenderer :
        MonoBehaviour
    {
        private const int MaximumAdaptedLayerCount =
            4;

        private const string DefaultLayerShaderName =
            "jcan/Celestial Systems/Curved MapMagic Terrain Layers";

        private struct TileKey :
            IEquatable<TileKey>
        {
            public CubeSphereFace Face;
            public int TileX;
            public int TileZ;

            public TileKey(
                CubeSphereFace face,
                int tileX,
                int tileZ)
            {
                Face = face;
                TileX = tileX;
                TileZ = tileZ;
            }

            public bool Equals(TileKey other)
            {
                return
                    Face == other.Face &&
                    TileX == other.TileX &&
                    TileZ == other.TileZ;
            }

            public override bool Equals(object value)
            {
                return
                    value is TileKey other &&
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

        private sealed class TileRuntime
        {
            public GameObject MeshObject;
            public MeshFilter MeshFilter;
            public MeshRenderer MeshRenderer;
            public MeshCollider MeshCollider;
            public Mesh CurvedMesh;
            public RoundMapMagicVirtualHeightSample Sample;
            public RoundMapMagicVirtualHeightSample MaterialSample;
            public Material GeneratedMaterial;
            public Material TemplateMaterial;
            public Texture2D ControlTexture;
            public DoubleVector3 TileCenterDirection;
            public double PlanetRadiusMeters;
            public double SurfaceOffsetMeters;
        }

        [Header("Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private RoundMapMagicVirtualSampleStream sampleStream =
            RoundMapMagicVirtualSampleStream.Mid;

        [SerializeField]
        private RoundMapMagicVirtualHeightSampler heightSampler;

        [SerializeField]
        private Material meshMaterial;

        [SerializeField]
        private Color fallbackMeshColor =
            new Color(
                0.25f,
                0.45f,
                0.2f,
                1.0f);

        [SerializeField]
        [Tooltip("Master switch for collider generation. Local collider distance is resolved separately from the quality profile.")]
        private bool generateMeshCollider;

        [SerializeField]
        [Tooltip("Moves these validation meshes radially without changing their sampled heights. Zero places them on the generated surface.")]
        private double surfaceOffsetMeters;

        [Header("Runtime")]
        [SerializeField]
        private bool hasRenderedTile;

        [SerializeField]
        private int expectedRenderedTileCount;

        [SerializeField]
        private int activeRenderedTileCount;

        [SerializeField]
        private CubeSphereFace renderedFace;

        [SerializeField]
        private int renderedVirtualTileX;

        [SerializeField]
        private int renderedVirtualTileZ;

        [SerializeField]
        private double renderedTileSizeMeters;

        [SerializeField]
        private int renderedResolution;

        [SerializeField]
        private int vertexCount;

        [SerializeField]
        private int triangleCount;

        [SerializeField]
        private int texturedRenderedTileCount;

        [SerializeField]
        private bool hasDirectTextureData;

        [SerializeField]
        private int renderedControlResolution;

        [SerializeField]
        private int renderedTerrainLayerCount;

        [SerializeField]
        private Texture renderedControlTexture;

        [SerializeField]
        private double resolvedColliderCoverageRadiusMeters;

        [SerializeField]
        private int colliderRenderedTileCount;

        private readonly Dictionary<TileKey, TileRuntime>
            tiles =
                new Dictionary<TileKey, TileRuntime>();
        private readonly HashSet<TileKey>
            desiredKeys =
                new HashSet<TileKey>();
        private readonly List<TileKey>
            removalBuffer =
                new List<TileKey>();
        private readonly List<RoundMapMagicVirtualHeightSample>
            sampleBuffer =
                new List<RoundMapMagicVirtualHeightSample>();

        private Material runtimeFallbackMaterial;
        private bool missingShaderLogged;

        public bool HasRenderedTile =>
            hasRenderedTile;

        private void Reset()
        {
            ResolveLocalReferences();
        }

        private void Start()
        {
            ResolveLocalReferences();

            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "The virtual height tile renderer requires a planet surface frame.",
                    this);
            }

            if (surfaceSession == null)
            {
                Debug.LogError(
                    "The virtual height tile renderer requires the shared Round MapMagic surface session.",
                    this);
            }

            if (heightSampler == null)
            {
                Debug.LogError(
                    "The virtual height tile renderer requires a virtual height sampler.",
                    this);
            }
        }

        private void LateUpdate()
        {
            ResolveLocalReferences();

            if (!ConfigurationIsValid() ||
                !heightSampler.StreamingActive ||
                !surfaceFrame.TryGetPlanetCenterScenePosition(
                    out var planetCenterScenePosition))
            {
                ClearRenderedTiles();
                ClearRuntimeState();
                return;
            }

            heightSampler.CopyCurrentSamplesTo(
                sampleBuffer);
            desiredKeys.Clear();

            for (var index = 0;
                index < sampleBuffer.Count;
                index++)
            {
                var sample =
                    sampleBuffer[index];
                var key =
                    new TileKey(
                        sample.Face,
                        sample.TileX,
                        sample.TileZ);

                desiredKeys.Add(key);
                EnsureTile(
                    key,
                    sample);
            }

            RemoveUndesiredTiles();

            foreach (var runtime in
                tiles.Values)
            {
                UpdateTilePose(
                    runtime,
                    planetCenterScenePosition);
                ApplyMaterial(
                    runtime);
                ApplyMeshCollider(
                    runtime,
                    false);
            }

            RefreshRuntimeState();
        }

        private void EnsureTile(
            TileKey key,
            RoundMapMagicVirtualHeightSample sample)
        {
            if (!tiles.TryGetValue(
                    key,
                    out var runtime))
            {
                runtime =
                    new TileRuntime();
                tiles.Add(
                    key,
                    runtime);
            }

            var planetRadiusMeters =
                surfaceFrame.PlanetRadiusMeters;

            if (runtime.CurvedMesh == null ||
                !ReferenceEquals(
                    runtime.Sample,
                    sample) ||
                runtime.PlanetRadiusMeters !=
                    planetRadiusMeters ||
                runtime.SurfaceOffsetMeters !=
                    surfaceOffsetMeters)
            {
                BuildCurvedTile(
                    runtime,
                    sample,
                    planetRadiusMeters);
            }
        }

        private void BuildCurvedTile(
            TileRuntime runtime,
            RoundMapMagicVirtualHeightSample sample,
            double planetRadiusMeters)
        {
            EnsureMeshObjects(
                runtime);

            var resolution =
                sample.Resolution;
            var drawingRadiusMeters =
                planetRadiusMeters +
                surfaceOffsetMeters;
            var face =
                sample.Face;
            var faceNormal =
                CubeSphereTopology.GetFaceNormal(
                    face);
            var faceUAxis =
                CubeSphereTopology.GetFaceUAxis(
                    face);
            var faceVAxis =
                CubeSphereTopology.GetFaceVAxis(
                    face);
            var tileCenterUMeters =
                sample.WorldOriginXMeters +
                sample.WorldSizeXMeters *
                    0.5;
            var tileCenterVMeters =
                -(sample.WorldOriginZMeters +
                    sample.WorldSizeZMeters *
                        0.5);
            var tileCenterAddress =
                new CubeSphereAddress(
                    face,
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
            var meshReferencePosition =
                tileCenterDirection *
                drawingRadiusMeters;
            var vertices =
                new Vector3[
                    resolution *
                    resolution];
            var uv =
                new Vector2[
                    vertices.Length];
            var triangles =
                new int[
                    (resolution - 1) *
                    (resolution - 1) *
                    6];

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
                    sample.WorldOriginZMeters +
                    sample.WorldSizeZMeters *
                    normalizedZ;
                var faceVMeters =
                    -worldZ;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(
                            resolution -
                            1);
                    var faceUMeters =
                        sample.WorldOriginXMeters +
                        sample.WorldSizeXMeters *
                            normalizedX;
                    var heightMeters =
                        sample.GetHeightMeters(
                            x,
                            z);
                    var address =
                        new CubeSphereAddress(
                            face,
                            CubeSphereMapping.MetersToFaceCoordinate(
                                faceUMeters,
                                planetRadiusMeters),
                            CubeSphereMapping.MetersToFaceCoordinate(
                                faceVMeters,
                                planetRadiusMeters),
                            heightMeters);
                    var direction =
                        CubeSphereMapping.AddressToDirection(
                            address);
                    var surfaceRadiusMeters =
                        drawingRadiusMeters +
                        heightMeters;
                    var delta =
                        direction *
                            surfaceRadiusMeters -
                        meshReferencePosition;
                    var vertexIndex =
                        z *
                        resolution +
                        x;

                    vertices[vertexIndex] =
                        new Vector3(
                            (float)Dot(
                                delta,
                                faceUAxis),
                            (float)Dot(
                                delta,
                                faceNormal),
                            (float)-Dot(
                                delta,
                                faceVAxis));
                    uv[vertexIndex] =
                        new Vector2(
                            (float)normalizedX,
                            (float)normalizedZ);
                }
            }

            var triangleIndex = 0;

            for (var z = 0;
                z < resolution - 1;
                z++)
            {
                for (var x = 0;
                    x < resolution - 1;
                    x++)
                {
                    var lowerLeft =
                        z *
                        resolution +
                        x;
                    var upperLeft =
                        lowerLeft +
                        resolution;
                    var lowerRight =
                        lowerLeft +
                        1;
                    var upperRight =
                        upperLeft +
                        1;

                    triangles[triangleIndex++] =
                        lowerLeft;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        upperRight;
                }
            }

            runtime.CurvedMesh.Clear();
            runtime.CurvedMesh.indexFormat =
                vertices.Length >
                    65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
            runtime.CurvedMesh.vertices =
                vertices;
            runtime.CurvedMesh.uv =
                uv;
            runtime.CurvedMesh.triangles =
                triangles;
            runtime.CurvedMesh.RecalculateNormals();
            runtime.CurvedMesh.RecalculateTangents();
            runtime.CurvedMesh.RecalculateBounds();

            runtime.MeshObject.name =
                $"{ResolveStreamName()} Curved Tile {sample.TileX},{sample.TileZ}";
            runtime.Sample =
                sample;
            runtime.TileCenterDirection =
                tileCenterDirection;
            runtime.PlanetRadiusMeters =
                planetRadiusMeters;
            runtime.SurfaceOffsetMeters =
                surfaceOffsetMeters;
            ApplyMeshCollider(
                runtime,
                true);
        }

        private void EnsureMeshObjects(
            TileRuntime runtime)
        {
            if (runtime.MeshObject == null)
            {
                runtime.MeshObject =
                    new GameObject(
                        "Mid Curved Tile");
                runtime.MeshObject.transform.SetParent(
                    surfaceFrame.transform,
                    false);
                runtime.MeshObject.transform.localScale =
                    Vector3.one;
                runtime.MeshFilter =
                    runtime.MeshObject.AddComponent<MeshFilter>();
                runtime.MeshRenderer =
                    runtime.MeshObject.AddComponent<MeshRenderer>();
                runtime.MeshRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;
                runtime.MeshRenderer.receiveShadows =
                    false;
            }

            if (runtime.CurvedMesh == null)
            {
                runtime.CurvedMesh =
                    new Mesh
                    {
                        name =
                            $"Virtual MapMagic {ResolveStreamName()} Curved Tile"
                    };
                runtime.MeshFilter.sharedMesh =
                runtime.CurvedMesh;
            }
        }

        private void ApplyMeshCollider(
            TileRuntime runtime,
            bool forceRefresh)
        {
            if (!ShouldGenerateMeshCollider(
                    runtime) ||
                runtime.MeshObject == null ||
                runtime.CurvedMesh == null)
            {
                if (runtime.MeshCollider != null)
                {
                    DestroyUnityObject(
                        runtime.MeshCollider);
                }

                runtime.MeshCollider =
                    null;
                return;
            }

            if (runtime.MeshCollider == null)
            {
                runtime.MeshCollider =
                    runtime.MeshObject.AddComponent<MeshCollider>();
                forceRefresh =
                    true;
            }

            if (forceRefresh ||
                runtime.MeshCollider.sharedMesh !=
                    runtime.CurvedMesh)
            {
                runtime.MeshCollider.sharedMesh =
                    null;
                runtime.MeshCollider.sharedMesh =
                    runtime.CurvedMesh;
            }
        }

        private bool ShouldGenerateMeshCollider(
            TileRuntime runtime)
        {
            if (!generateMeshCollider ||
                sampleStream !=
                    RoundMapMagicVirtualSampleStream.Local ||
                runtime == null ||
                runtime.Sample == null ||
                !surfaceFrame.HasAnchorAddress)
            {
                return false;
            }

            var qualityProfile =
                surfaceSession.ConfiguredQualityProfile;
            var coverageRadiusMeters =
                qualityProfile != null &&
                qualityProfile.HasValidSettings
                    ? qualityProfile
                        .LocalColliderCoverageRadiusMeters
                    : 0.0;

            if (coverageRadiusMeters <=
                0.0)
            {
                return false;
            }

            var anchorDirection =
                CubeSphereMapping.AddressToDirection(
                    surfaceFrame.AnchorAddress);
            var directionDot =
                Math.Max(
                    -1.0,
                    Math.Min(
                        1.0,
                        Dot(
                            anchorDirection,
                            runtime.TileCenterDirection)));
            var centerDistanceMeters =
                Math.Acos(
                    directionDot) *
                surfaceFrame.PlanetRadiusMeters;
            var tileHalfDiagonalMeters =
                Math.Sqrt(
                    runtime.Sample.WorldSizeXMeters *
                        runtime.Sample.WorldSizeXMeters +
                    runtime.Sample.WorldSizeZMeters *
                        runtime.Sample.WorldSizeZMeters) *
                0.5;

            return
                centerDistanceMeters <=
                coverageRadiusMeters +
                    tileHalfDiagonalMeters;
        }

        private string ResolveStreamName()
        {
            return
                sampleStream ==
                    RoundMapMagicVirtualSampleStream.Local
                    ? "Local"
                    : "Mid";
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

            if (heightSampler != null &&
                heightSampler.SampleStream ==
                    sampleStream)
            {
                return;
            }

            heightSampler =
                null;
            var samplers =
                GetComponents<RoundMapMagicVirtualHeightSampler>();

            for (var index = 0;
                index < samplers.Length;
                index++)
            {
                if (samplers[index].SampleStream !=
                    sampleStream)
                {
                    continue;
                }

                heightSampler =
                    samplers[index];
                break;
            }
        }

        private void UpdateTilePose(
            TileRuntime runtime,
            Vector3 planetCenterScenePosition)
        {
            if (runtime.MeshObject == null ||
                runtime.Sample == null)
            {
                return;
            }

            var sample =
                runtime.Sample;
            var drawingRadiusMeters =
                surfaceFrame.PlanetRadiusMeters +
                surfaceOffsetMeters;
            var centerDirection =
                ToVector3(
                    runtime.TileCenterDirection).normalized;
            var faceNormal =
                ToVector3(
                    CubeSphereTopology.GetFaceNormal(
                        sample.Face)).normalized;
            var forward =
                -ToVector3(
                    CubeSphereTopology.GetFaceVAxis(
                        sample.Face)).normalized;

            runtime.MeshObject.transform.SetPositionAndRotation(
                planetCenterScenePosition +
                    centerDirection *
                        (float)drawingRadiusMeters,
                Quaternion.LookRotation(
                    forward,
                    faceNormal));
        }

        private void ApplyMaterial(
            TileRuntime runtime)
        {
            if (runtime.MeshRenderer == null)
            {
                return;
            }

            var surfaceDefinition =
                surfaceSession.ActiveSurfaceDefinition != null
                    ? surfaceSession.ActiveSurfaceDefinition
                    : surfaceSession.ConfiguredSurfaceDefinition;
            var resolvedMaterial =
                meshMaterial != null
                    ? meshMaterial
                    : surfaceDefinition != null
                        ? surfaceDefinition.Material
                        : null;

            if (CanAdaptTextureSample(
                    runtime.Sample))
            {
                var material =
                    ResolveGeneratedMaterial(
                        runtime,
                        resolvedMaterial);

                if (material != null)
                {
                    runtime.MeshRenderer.sharedMaterial =
                        material;
                    return;
                }
            }

            ReleaseGeneratedMaterial(
                runtime);

            runtime.MeshRenderer.sharedMaterial =
                resolvedMaterial != null
                    ? resolvedMaterial
                    : ResolveFallbackMaterial();
        }

        private static bool CanAdaptTextureSample(
            RoundMapMagicVirtualHeightSample sample)
        {
            if (sample == null ||
                !sample.HasTextureData ||
                sample.TerrainLayerCount < 1 ||
                sample.TerrainLayerCount >
                    MaximumAdaptedLayerCount)
            {
                return false;
            }

            for (var index = 0;
                index < sample.TerrainLayerCount;
                index++)
            {
                if (sample.GetTerrainLayer(
                        index) == null)
                {
                    return false;
                }
            }

            return true;
        }

        private Material ResolveGeneratedMaterial(
            TileRuntime runtime,
            Material templateMaterial)
        {
            if (runtime.GeneratedMaterial != null &&
                runtime.ControlTexture != null &&
                runtime.TemplateMaterial ==
                    templateMaterial &&
                ReferenceEquals(
                    runtime.MaterialSample,
                    runtime.Sample))
            {
                return
                    runtime.GeneratedMaterial;
            }

            ReleaseGeneratedMaterial(
                runtime);
            var usesDefinitionMaterial =
                IsCompatibleTemplate(
                    templateMaterial);
            Material generatedMaterial;

            if (usesDefinitionMaterial)
            {
                generatedMaterial =
                    new Material(
                        templateMaterial);
            }
            else
            {
                var shader =
                    Shader.Find(
                        DefaultLayerShaderName);

                if (shader == null)
                {
                    if (!missingShaderLogged)
                    {
                        Debug.LogError(
                            $"Shader '{DefaultLayerShaderName}' was not found for the virtual height tile renderer.",
                            this);
                        missingShaderLogged =
                            true;
                    }

                    return null;
                }

                generatedMaterial =
                    new Material(
                        shader);
            }

            var controlTexture =
                CreateControlTexture(
                    runtime.Sample);
            generatedMaterial.name =
                $"Virtual MapMagic Terrain Layers {runtime.Sample.TileX},{runtime.Sample.TileZ}";
            generatedMaterial.hideFlags =
                HideFlags.HideAndDontSave;
            ConfigureGeneratedMaterial(
                generatedMaterial,
                controlTexture,
                runtime.Sample);
            runtime.GeneratedMaterial =
                generatedMaterial;
            runtime.TemplateMaterial =
                templateMaterial;
            runtime.ControlTexture =
                controlTexture;
            runtime.MaterialSample =
                runtime.Sample;
            missingShaderLogged =
                false;
            return
                generatedMaterial;
        }

        private static Texture2D CreateControlTexture(
            RoundMapMagicVirtualHeightSample sample)
        {
            var resolution =
                sample.ControlResolution;
            var pixels =
                new Color[
                    resolution *
                    resolution];

            for (var z = 0;
                z < resolution;
                z++)
            {
                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    pixels[
                        z *
                        resolution +
                        x] =
                        new Color(
                            ResolveControlWeight(
                                sample,
                                x,
                                z,
                                0),
                            ResolveControlWeight(
                                sample,
                                x,
                                z,
                                1),
                            ResolveControlWeight(
                                sample,
                                x,
                                z,
                                2),
                            ResolveControlWeight(
                                sample,
                                x,
                                z,
                                3));
                }
            }

            var texture =
                new Texture2D(
                    resolution,
                    resolution,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name =
                        $"Virtual MapMagic Control {sample.TileX},{sample.TileZ}",
                    filterMode =
                        FilterMode.Bilinear,
                    wrapMode =
                        TextureWrapMode.Clamp,
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            texture.SetPixels(
                pixels);
            texture.Apply(
                false,
                false);
            return
                texture;
        }

        private static float ResolveControlWeight(
            RoundMapMagicVirtualHeightSample sample,
            int x,
            int z,
            int layer)
        {
            return
                layer < sample.TerrainLayerCount
                    ? sample.GetControlWeight(
                        x,
                        z,
                        layer)
                    : 0.0f;
        }

        private static bool IsCompatibleTemplate(
            Material material)
        {
            return
                material != null &&
                material.HasProperty(
                    "_Control") &&
                material.HasProperty(
                    "_LayerCount") &&
                material.HasProperty(
                    "_Splat0");
        }

        private static void ConfigureGeneratedMaterial(
            Material material,
            Texture2D controlTexture,
            RoundMapMagicVirtualHeightSample sample)
        {
            ApplyTexture(
                material,
                "_Control",
                controlTexture,
                Vector2.one,
                Vector2.zero);
            SetFloatIfPresent(
                material,
                "_LayerCount",
                sample.TerrainLayerCount);

            for (var index = 0;
                index < MaximumAdaptedLayerCount;
                index++)
            {
                ConfigureLayer(
                    material,
                    sample,
                    index < sample.TerrainLayerCount
                        ? sample.GetTerrainLayer(
                            index)
                        : null,
                    index);
            }
        }

        private static void ConfigureLayer(
            Material material,
            RoundMapMagicVirtualHeightSample sample,
            TerrainLayer terrainLayer,
            int layerIndex)
        {
            var suffix =
                layerIndex.ToString();

            if (terrainLayer == null)
            {
                ApplyTexture(
                    material,
                    "_Splat" + suffix,
                    Texture2D.whiteTexture,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    material,
                    "_Normal" + suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                ApplyTexture(
                    material,
                    "_Mask" + suffix,
                    null,
                    Vector2.one,
                    Vector2.zero);
                SetFloatIfPresent(
                    material,
                    "_HasNormal" + suffix,
                    0.0f);
                SetFloatIfPresent(
                    material,
                    "_HasMask" + suffix,
                    0.0f);
                return;
            }

            ResolveTextureTransform(
                sample,
                terrainLayer,
                out var textureScale,
                out var textureOffset);
            var layerDiffuse =
                terrainLayer.diffuseTexture != null
                    ? terrainLayer.diffuseTexture
                    : Texture2D.whiteTexture;
            var layerNormal =
                terrainLayer.normalMapTexture;
            var layerMask =
                terrainLayer.maskMapTexture;

            ApplyTexture(
                material,
                "_Splat" + suffix,
                layerDiffuse,
                textureScale,
                textureOffset);
            ApplyTexture(
                material,
                "_Normal" + suffix,
                layerNormal,
                textureScale,
                textureOffset);
            ApplyTexture(
                material,
                "_Mask" + suffix,
                layerMask,
                textureScale,
                textureOffset);
            SetFloatIfPresent(
                material,
                "_HasNormal" + suffix,
                layerNormal != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_HasMask" + suffix,
                layerMask != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_NormalScale" + suffix,
                terrainLayer.normalScale);
            SetFloatIfPresent(
                material,
                "_Metallic" + suffix,
                terrainLayer.metallic);
            SetFloatIfPresent(
                material,
                "_Smoothness" + suffix,
                terrainLayer.smoothness);
        }

        private static void ResolveTextureTransform(
            RoundMapMagicVirtualHeightSample sample,
            TerrainLayer terrainLayer,
            out Vector2 scale,
            out Vector2 offset)
        {
            var tileSizeX =
                SafeTileSize(
                    terrainLayer.tileSize.x);
            var tileSizeZ =
                SafeTileSize(
                    terrainLayer.tileSize.y);
            scale =
                new Vector2(
                    (float)(
                        sample.WorldSizeXMeters /
                        tileSizeX),
                    (float)(
                        sample.WorldSizeZMeters /
                        tileSizeZ));
            offset =
                new Vector2(
                    Repeat01(
                        (sample.WorldOriginXMeters +
                            terrainLayer.tileOffset.x) /
                        tileSizeX),
                    Repeat01(
                        (sample.WorldOriginZMeters +
                            terrainLayer.tileOffset.y) /
                        tileSizeZ));
        }

        private static void ApplyTexture(
            Material material,
            string propertyName,
            Texture texture,
            Vector2 scale,
            Vector2 offset)
        {
            if (!material.HasProperty(
                    propertyName))
            {
                return;
            }

            material.SetTexture(
                propertyName,
                texture);
            material.SetTextureScale(
                propertyName,
                scale);
            material.SetTextureOffset(
                propertyName,
                offset);
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetFloat(
                    propertyName,
                    value);
            }
        }

        private static float SafeTileSize(
            float value)
        {
            return
                IsFinite(
                    value) &&
                Mathf.Abs(
                    value) >
                    Mathf.Epsilon
                    ? Mathf.Abs(
                        value)
                    : 1.0f;
        }

        private static float Repeat01(
            double value)
        {
            return
                (float)(
                    value -
                    Math.Floor(
                        value));
        }

        private static void ReleaseGeneratedMaterial(
            TileRuntime runtime)
        {
            if (runtime.GeneratedMaterial != null)
            {
                DestroyUnityObject(
                    runtime.GeneratedMaterial);
            }

            if (runtime.ControlTexture != null)
            {
                DestroyUnityObject(
                    runtime.ControlTexture);
            }

            runtime.GeneratedMaterial =
                null;
            runtime.TemplateMaterial =
                null;
            runtime.ControlTexture =
                null;
            runtime.MaterialSample =
                null;
        }

        private Material ResolveFallbackMaterial()
        {
            if (runtimeFallbackMaterial != null)
            {
                SetMaterialColor(
                    runtimeFallbackMaterial,
                    fallbackMeshColor);
                return runtimeFallbackMaterial;
            }

            var shader =
                ResolveFallbackShader();

            if (shader == null)
            {
                if (!missingShaderLogged)
                {
                    Debug.LogError(
                        "No compatible fallback shader was found for the virtual height tile renderer.",
                        this);
                    missingShaderLogged = true;
                }

                return null;
            }

            runtimeFallbackMaterial =
                new Material(
                    shader)
                {
                    name =
                        "Virtual Height Tile Material",
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            SetMaterialColor(
                runtimeFallbackMaterial,
                fallbackMeshColor);
            missingShaderLogged = false;
            return runtimeFallbackMaterial;
        }

        private static Shader ResolveFallbackShader()
        {
            var renderPipeline =
                GraphicsSettings.currentRenderPipeline;

            if (renderPipeline == null)
            {
                return
                    Shader.Find(
                        "Unlit/Color") ??
                    Shader.Find(
                        "Standard");
            }

            var pipelineName =
                renderPipeline.GetType().Name;

            if (pipelineName.IndexOf(
                    "Universal",
                    StringComparison.OrdinalIgnoreCase) >=
                0)
            {
                return
                    Shader.Find(
                        "Universal Render Pipeline/Unlit");
            }

            if (pipelineName.IndexOf(
                    "HDRender",
                    StringComparison.OrdinalIgnoreCase) >=
                0 ||
                pipelineName.IndexOf(
                    "HighDefinition",
                    StringComparison.OrdinalIgnoreCase) >=
                0)
            {
                return
                    Shader.Find(
                        "HDRP/Unlit");
            }

            return null;
        }

        private static void SetMaterialColor(
            Material material,
            Color color)
        {
            if (material.HasProperty(
                    "_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    color);
            }

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    color);
            }
        }

        private void RemoveUndesiredTiles()
        {
            removalBuffer.Clear();

            foreach (var entry in
                tiles)
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
                RemoveTile(
                    removalBuffer[index]);
            }
        }

        private void RemoveTile(
            TileKey key)
        {
            if (!tiles.TryGetValue(
                    key,
                    out var runtime))
            {
                return;
            }

            if (runtime.MeshObject != null)
            {
                DestroyUnityObject(
                    runtime.MeshObject);
            }

            ReleaseGeneratedMaterial(
                runtime);

            if (runtime.CurvedMesh != null)
            {
                DestroyUnityObject(
                    runtime.CurvedMesh);
            }

            tiles.Remove(key);
        }

        private void RefreshRuntimeState()
        {
            expectedRenderedTileCount =
                heightSampler.ExpectedSampleCount;
            activeRenderedTileCount =
                tiles.Count;
            hasRenderedTile =
                tiles.Count > 0;
            var primarySample =
                heightSampler.CurrentSample;

            if (primarySample == null &&
                sampleBuffer.Count > 0)
            {
                primarySample =
                    sampleBuffer[0];
            }

            renderedFace =
                primarySample != null
                    ? primarySample.Face
                    : default;
            renderedVirtualTileX =
                primarySample != null
                    ? primarySample.TileX
                    : default;
            renderedVirtualTileZ =
                primarySample != null
                    ? primarySample.TileZ
                    : default;
            renderedTileSizeMeters =
                primarySample != null
                    ? primarySample.WorldSizeXMeters
                    : default;
            renderedResolution =
                primarySample != null
                    ? primarySample.Resolution
                    : default;
            vertexCount =
                primarySample != null
                    ? primarySample.VertexCount
                    : default;
            triangleCount =
                primarySample != null
                    ? (primarySample.Resolution - 1) *
                        (primarySample.Resolution - 1) *
                        2
                    : default;
            texturedRenderedTileCount =
                0;
            var qualityProfile =
                surfaceSession.ConfiguredQualityProfile;
            resolvedColliderCoverageRadiusMeters =
                qualityProfile != null &&
                qualityProfile.HasValidSettings
                    ? qualityProfile
                        .LocalColliderCoverageRadiusMeters
                    : 0.0;
            colliderRenderedTileCount =
                0;

            foreach (var runtime in
                tiles.Values)
            {
                if (runtime.GeneratedMaterial != null)
                {
                    texturedRenderedTileCount++;
                }

                if (runtime.MeshCollider != null &&
                    runtime.MeshCollider.sharedMesh != null)
                {
                    colliderRenderedTileCount++;
                }
            }

            var primaryRuntime =
                primarySample != null &&
                tiles.TryGetValue(
                    new TileKey(
                        primarySample.Face,
                        primarySample.TileX,
                        primarySample.TileZ),
                    out var resolvedPrimaryRuntime)
                    ? resolvedPrimaryRuntime
                    : null;
            hasDirectTextureData =
                primaryRuntime != null &&
                primaryRuntime.GeneratedMaterial != null;
            renderedControlResolution =
                hasDirectTextureData
                    ? primaryRuntime.Sample.ControlResolution
                    : default;
            renderedTerrainLayerCount =
                hasDirectTextureData
                    ? primaryRuntime.Sample.TerrainLayerCount
                    : default;
            renderedControlTexture =
                hasDirectTextureData
                    ? primaryRuntime.ControlTexture
                    : null;
        }

        private bool ConfigurationIsValid()
        {
            return
                surfaceFrame != null &&
                surfaceSession != null &&
                heightSampler != null &&
                IsFinite(
                    surfaceFrame.PlanetRadiusMeters) &&
                surfaceFrame.PlanetRadiusMeters >
                    0.0 &&
                IsFinite(
                    surfaceOffsetMeters) &&
                surfaceFrame.PlanetRadiusMeters +
                    surfaceOffsetMeters >
                    0.0;
        }

        private void ClearRenderedTiles()
        {
            removalBuffer.Clear();

            foreach (var key in
                tiles.Keys)
            {
                removalBuffer.Add(key);
            }

            for (var index = 0;
                index < removalBuffer.Count;
                index++)
            {
                RemoveTile(
                    removalBuffer[index]);
            }

            desiredKeys.Clear();
            sampleBuffer.Clear();
        }

        private void ClearRuntimeState()
        {
            hasRenderedTile = false;
            expectedRenderedTileCount = default;
            activeRenderedTileCount = default;
            renderedFace = default;
            renderedVirtualTileX = default;
            renderedVirtualTileZ = default;
            renderedTileSizeMeters = default;
            renderedResolution = default;
            vertexCount = default;
            triangleCount = default;
            texturedRenderedTileCount = default;
            hasDirectTextureData = false;
            renderedControlResolution = default;
            renderedTerrainLayerCount = default;
            renderedControlTexture = null;
            resolvedColliderCoverageRadiusMeters = default;
            colliderRenderedTileCount = default;
        }

        private void OnDisable()
        {
            ClearRenderedTiles();
            ClearRuntimeState();
        }

        private void OnDestroy()
        {
            ClearRenderedTiles();

            if (runtimeFallbackMaterial != null)
            {
                DestroyUnityObject(
                    runtimeFallbackMaterial);
                runtimeFallbackMaterial = null;
            }
        }

        private static Vector3 ToVector3(
            DoubleVector3 value)
        {
            return new Vector3(
                (float)value.x,
                (float)value.y,
                (float)value.z);
        }

        private static double Dot(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return
                first.x *
                    second.x +
                first.y *
                    second.y +
                first.z *
                    second.z;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private static void DestroyUnityObject(
            UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
