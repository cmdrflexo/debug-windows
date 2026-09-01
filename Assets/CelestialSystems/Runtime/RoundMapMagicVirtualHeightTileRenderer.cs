/*
 * Renders the cached mid-detail MapMagic height samples as curved cube-sphere meshes without creating Unity Terrains.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicVirtualHeightTileRenderer :
        MonoBehaviour
    {
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
            public Mesh CurvedMesh;
            public RoundMapMagicVirtualHeightSample Sample;
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
            surfaceFrame =
                GetComponent<GePlanetSurfaceFrame>();
            surfaceSession =
                GetComponent<RoundMapMagicSurfaceSession>();
            heightSampler =
                GetComponent<RoundMapMagicVirtualHeightSampler>();
        }

        private void Start()
        {
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
            if (!ConfigurationIsValid() ||
                !surfaceSession.HasActiveSession ||
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
                $"Mid Curved Tile {sample.TileX},{sample.TileZ}";
            runtime.Sample =
                sample;
            runtime.TileCenterDirection =
                tileCenterDirection;
            runtime.PlanetRadiusMeters =
                planetRadiusMeters;
            runtime.SurfaceOffsetMeters =
                surfaceOffsetMeters;
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
                            "Virtual MapMagic Mid Curved Tile"
                    };
                runtime.MeshFilter.sharedMesh =
                    runtime.CurvedMesh;
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
                surfaceSession.ActiveSurfaceDefinition;
            var resolvedMaterial =
                meshMaterial != null
                    ? meshMaterial
                    : surfaceDefinition != null
                        ? surfaceDefinition.Material
                        : null;

            runtime.MeshRenderer.sharedMaterial =
                resolvedMaterial != null
                    ? resolvedMaterial
                    : ResolveFallbackMaterial();
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
