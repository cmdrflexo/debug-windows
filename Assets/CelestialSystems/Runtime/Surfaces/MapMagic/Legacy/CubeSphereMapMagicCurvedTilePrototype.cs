/*
 * Converts ready MapMagic tiles into tile-centered curved cube-sphere meshes and matching runtime colliders.
 */

using System;
using System.Collections.Generic;
using Den.Tools;
using MapMagic.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicCurvedTilePrototype :
        MonoBehaviour
    {
        public struct CurvedTileData
        {
            public int TileX { get; }

            public int TileZ { get; }

            public MeshRenderer Renderer { get; }

            public TerrainData TerrainData { get; }

            internal CurvedTileData(
                int tileX,
                int tileZ,
                MeshRenderer renderer,
                TerrainData terrainData)
            {
                TileX =
                    tileX;
                TileZ =
                    tileZ;
                Renderer =
                    renderer;
                TerrainData =
                    terrainData;
            }
        }

        private sealed class CurvedTileRuntime
        {
            public int TileX;
            public int TileZ;
            public GameObject MeshObject;
            public MeshFilter MeshFilter;
            public MeshRenderer MeshRenderer;
            public MeshCollider MeshCollider;
            public Mesh CurvedMesh;
            public Terrain SourceTerrain;
            public TerrainData TerrainData;
            public CubeSphereFace Face;
            public DoubleVector3 TileCenterDirection;
            public int Resolution;
            public double MeshRadiusMeters;
            public bool SourceTerrainWasEnabled;
            public TerrainCollider SourceTerrainCollider;
            public bool SourceColliderWasEnabled;
        }

        private struct TileKey :
            IEquatable<TileKey>
        {
            public int X;
            public int Z;

            public TileKey(
                int x,
                int z)
            {
                X = x;
                Z = z;
            }

            public bool Equals(
                TileKey other)
            {
                return
                    X == other.X &&
                    Z == other.Z;
            }

            public override bool Equals(
                object value)
            {
                return
                    value is TileKey other &&
                    Equals(
                        other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (X * 397) ^
                        Z;
                }
            }
        }

        [Header("Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver coordinateDriver;

        [SerializeField]
        private CubeSphereMapMagicRootPose rootPose;

        [SerializeField]
        private MapMagicObject mapMagicObject;

        [SerializeField]
        [Range(0, 4)]
        [Tooltip("Fallback tile radius used only when the surface session has no valid quality profile.")]
        private int curvedTileRadius = 2;

        [SerializeField]
        [Tooltip("Zero uses the real planet radius. A positive value overrides only these prototype meshes' curvature.")]
        private double curvatureRadiusOverrideMeters;

        [SerializeField]
        private Color fallbackMeshColor =
            new Color(
                0.35f,
                0.55f,
                0.25f,
                1.0f);

        [SerializeField]
        private bool hideSourceTerrain = true;

        [SerializeField]
        private bool generateMeshCollider = true;

        [Header("Resolved Surface Definition")]
        [SerializeField]
        private int resolvedMeshResolution;

        [SerializeField]
        private Material resolvedMeshMaterial;

        [Header("Resolved Quality Profile")]
        [SerializeField]
        private bool usingQualityProfile;

        [SerializeField]
        private double resolvedLocalCoverageRadiusMeters;

        [SerializeField]
        private int resolvedLocalTileRadius;

        [Header("Runtime")]
        [SerializeField]
        private int expectedCurvedTileCount;

        [SerializeField]
        private int readySourceTileCount;

        [SerializeField]
        private int activeCurvedTileCount;

        [SerializeField]
        private int hiddenSourceTerrainCount;

        [SerializeField]
        private bool hasCurvedTile;

        [SerializeField]
        private CubeSphereFace activeFace;

        [SerializeField]
        private double effectiveMeshRadiusMeters;

        [SerializeField]
        private int sourceTileX;

        [SerializeField]
        private int sourceTileZ;

        [SerializeField]
        private int vertexCount;

        [SerializeField]
        private int triangleCount;

        [SerializeField]
        private bool hasMeshCollider;

        private readonly Dictionary<TileKey, CurvedTileRuntime>
            curvedTiles =
                new Dictionary<TileKey, CurvedTileRuntime>();

        private readonly HashSet<TileKey>
            desiredTiles =
                new HashSet<TileKey>();

        private readonly List<TileKey>
            removalBuffer =
                new List<TileKey>();

        private CurvedTileRuntime primaryTile;
        private Material runtimeFallbackMaterial;
        private bool missingShaderLogged;
        private RoundMapMagicSurfaceSession surfaceSession;

        public bool HasCurvedTile =>
            hasCurvedTile;

        public MeshRenderer CurvedTileRenderer =>
            primaryTile != null
                ? primaryTile.MeshRenderer
                : null;

        public TerrainData SourceTerrainData =>
            primaryTile != null
                ? primaryTile.TerrainData
                : null;

        public int SourceTileX =>
            sourceTileX;

        public int SourceTileZ =>
            sourceTileZ;

        public Material ResolvedMeshMaterial =>
            resolvedMeshMaterial;

        public void CopyCurvedTilesTo(
            List<CurvedTileData> destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(
                    nameof(
                        destination));
            }

            destination.Clear();

            foreach (var runtime in
                curvedTiles.Values)
            {
                if (runtime.MeshRenderer == null ||
                    runtime.TerrainData == null)
                {
                    continue;
                }

                destination.Add(
                    new CurvedTileData(
                        runtime.TileX,
                        runtime.TileZ,
                        runtime.MeshRenderer,
                        runtime.TerrainData));
            }
        }

        private void Reset()
        {
            coordinateDriver =
                GetComponent<CubeSphereMapMagicCoordinateDriver>();
            rootPose =
                GetComponent<CubeSphereMapMagicRootPose>();
            mapMagicObject =
                GetComponent<MapMagicObject>();
            ResolveSurfaceSession();
        }

        private void Start()
        {
            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "The curved MapMagic tile prototype requires a planet surface frame.",
                    this);
            }

            if (coordinateDriver == null)
            {
                Debug.LogError(
                    "The curved MapMagic tile prototype requires a coordinate driver.",
                    this);
            }

            if (rootPose == null)
            {
                Debug.LogError(
                    "The curved MapMagic tile prototype requires a root pose.",
                    this);
            }

            if (mapMagicObject == null)
            {
                Debug.LogError(
                    "The curved MapMagic tile prototype requires a MapMagic object.",
                    this);
            }
        }

        private void LateUpdate()
        {
            var surfaceDefinition =
                ResolveSurfaceDefinition();
            var qualityProfile =
                ResolveQualityProfile();
            resolvedMeshResolution = default;
            resolvedMeshMaterial = null;
            usingQualityProfile = false;
            resolvedLocalCoverageRadiusMeters = default;
            resolvedLocalTileRadius = default;

            if (!ConfigurationIsValid(
                    surfaceDefinition) ||
                !coordinateDriver.HasMapMagicCoordinate ||
                !rootPose.HasPose)
            {
                ClearCurvedTiles();
                ClearRuntimeState();
                return;
            }

            var nextFace =
                coordinateDriver.ActiveFace;
            var centerTileX =
                coordinateDriver.MapMagicTileX;
            var centerTileZ =
                coordinateDriver.MapMagicTileZ;
            usingQualityProfile =
                qualityProfile != null;
            resolvedMeshResolution =
                Mathf.Clamp(
                    usingQualityProfile
                        ? qualityProfile.LocalMeshResolution
                        : surfaceDefinition.MeshResolution,
                    3,
                    257);
            resolvedMeshMaterial =
                surfaceDefinition.Material;
            var nextMeshRadiusMeters =
                ResolveMeshRadiusMeters();
            var clampedTileRadius =
                ResolveLocalTileRadius(
                    surfaceDefinition,
                    qualityProfile);
            resolvedLocalCoverageRadiusMeters =
                usingQualityProfile
                    ? qualityProfile.LocalCoverageRadiusMeters
                    : (clampedTileRadius + 0.5) *
                        surfaceDefinition.TileSizeMeters;
            resolvedLocalTileRadius =
                clampedTileRadius;
            var tileDiameter =
                clampedTileRadius *
                    2 +
                1;

            expectedCurvedTileCount =
                tileDiameter *
                tileDiameter;
            readySourceTileCount = 0;
            desiredTiles.Clear();

            for (var offsetZ =
                -clampedTileRadius;
                offsetZ <= clampedTileRadius;
                offsetZ++)
            {
                for (var offsetX =
                    -clampedTileRadius;
                    offsetX <= clampedTileRadius;
                    offsetX++)
                {
                    var tileX =
                        centerTileX +
                        offsetX;
                    var tileZ =
                        centerTileZ +
                        offsetZ;
                    var key =
                        new TileKey(
                            tileX,
                            tileZ);
                    desiredTiles.Add(
                        key);

                    var mapMagicTile =
                        mapMagicObject.tiles[
                            new Coord(
                                tileX,
                                tileZ)];

                    if (mapMagicTile == null ||
                        mapMagicTile.main == null ||
                        !mapMagicTile.main.applyReady ||
                        mapMagicTile.main.terrain == null ||
                        mapMagicTile.main.terrain.terrainData ==
                            null)
                    {
                        continue;
                    }

                    readySourceTileCount++;
                    EnsureCurvedTile(
                        key,
                        mapMagicTile.main.terrain,
                        nextFace,
                        resolvedMeshResolution,
                        nextMeshRadiusMeters);
                }
            }

            RemoveUndesiredTiles();

            hiddenSourceTerrainCount = 0;
            surfaceFrame.TryGetPlanetCenterScenePosition(
                out var planetCenterScenePosition);
            var tileRotation =
                rootPose.transform.rotation;

            foreach (var runtime in
                curvedTiles.Values)
            {
                UpdateCurvedTilePose(
                    runtime,
                    planetCenterScenePosition,
                    tileRotation);

                if (hideSourceTerrain)
                {
                    HideSourceTerrain(
                        runtime);

                    if (runtime.SourceTerrain != null)
                    {
                        hiddenSourceTerrainCount++;
                    }
                }
                else
                {
                    RestoreSourceTerrain(
                        runtime);
                }

                ApplyMaterial(
                    runtime);
                ApplyMeshCollider(
                    runtime,
                    false);
            }

            ResolvePrimaryRuntime(
                nextFace,
                centerTileX,
                centerTileZ,
                nextMeshRadiusMeters);
            activeCurvedTileCount =
                curvedTiles.Count;
        }

        private void EnsureCurvedTile(
            TileKey key,
            Terrain sourceTerrain,
            CubeSphereFace face,
            int resolution,
            double meshRadiusMeters)
        {
            if (!curvedTiles.TryGetValue(
                    key,
                    out var runtime))
            {
                runtime =
                    new CurvedTileRuntime
                    {
                        TileX =
                            key.X,
                        TileZ =
                            key.Z
                    };
                curvedTiles.Add(
                    key,
                    runtime);
            }

            var terrainData =
                sourceTerrain.terrainData;

            if (runtime.CurvedMesh == null ||
                runtime.SourceTerrain !=
                    sourceTerrain ||
                runtime.TerrainData !=
                    terrainData ||
                runtime.Face != face ||
                runtime.Resolution != resolution ||
                runtime.MeshRadiusMeters !=
                    meshRadiusMeters)
            {
                BuildCurvedTile(
                    runtime,
                    sourceTerrain,
                    terrainData,
                    face,
                    resolution,
                    meshRadiusMeters);
            }
        }

        private void BuildCurvedTile(
            CurvedTileRuntime runtime,
            Terrain sourceTerrain,
            TerrainData terrainData,
            CubeSphereFace face,
            int resolution,
            double meshRadiusMeters)
        {
            RestoreSourceTerrain(
                runtime);
            EnsureMeshObjects(
                runtime);

            var planetRadiusMeters =
                meshRadiusMeters;
            var rootRadiusMeters =
                planetRadiusMeters +
                rootPose.RadialOffsetMeters;
            var tileSizeX =
                mapMagicObject.tileSize.x;
            var tileSizeZ =
                mapMagicObject.tileSize.z;
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
                ((double)runtime.TileX + 0.5) *
                tileSizeX;
            var tileCenterVMeters =
                -((double)runtime.TileZ + 0.5) *
                tileSizeZ;
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
                rootRadiusMeters;

            for (var z = 0;
                z < resolution;
                z++)
            {
                var normalizedZ =
                    z /
                    (double)(
                        resolution -
                        1);
                var localZ =
                    tileSizeZ *
                    normalizedZ;
                var faceVMeters =
                    -(runtime.TileZ *
                    tileSizeZ +
                    localZ);

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(
                            resolution -
                            1);
                    var localX =
                        tileSizeX *
                        normalizedX;
                    var faceUMeters =
                        runtime.TileX *
                        tileSizeX +
                        localX;
                    var heightMeters =
                        terrainData.GetInterpolatedHeight(
                            (float)normalizedX,
                            (float)normalizedZ);
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
                        planetRadiusMeters +
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
            ApplyMeshCollider(
                runtime,
                true);

            runtime.MeshObject.name =
                $"Curved Tile {runtime.TileX},{runtime.TileZ}";
            runtime.SourceTerrain =
                sourceTerrain;
            runtime.TerrainData =
                terrainData;
            runtime.Face =
                face;
            runtime.TileCenterDirection =
                tileCenterDirection;
            runtime.Resolution =
                resolution;
            runtime.MeshRadiusMeters =
                meshRadiusMeters;
        }

        private void EnsureMeshObjects(
            CurvedTileRuntime runtime)
        {
            if (runtime.MeshObject == null)
            {
                runtime.MeshObject =
                    new GameObject(
                        "Curved Tile");
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
                            $"Curved MapMagic Tile {runtime.TileX},{runtime.TileZ}"
                    };
                runtime.MeshFilter.sharedMesh =
                    runtime.CurvedMesh;
            }
        }

        private void UpdateCurvedTilePose(
            CurvedTileRuntime runtime,
            Vector3 planetCenterScenePosition,
            Quaternion tileRotation)
        {
            if (runtime.MeshObject == null)
            {
                return;
            }

            var drawingRadiusMeters =
                surfaceFrame.PlanetRadiusMeters +
                rootPose.RadialOffsetMeters;

            if (!IsFinite(
                    drawingRadiusMeters) ||
                drawingRadiusMeters <= 0.0 ||
                drawingRadiusMeters >
                    float.MaxValue)
            {
                return;
            }

            var tileCenterDirection =
                new Vector3(
                    (float)runtime.TileCenterDirection.x,
                    (float)runtime.TileCenterDirection.y,
                    (float)runtime.TileCenterDirection.z).normalized;
            var tileCenterScenePosition =
                planetCenterScenePosition +
                tileCenterDirection *
                    (float)drawingRadiusMeters;

            runtime.MeshObject.transform.SetPositionAndRotation(
                tileCenterScenePosition,
                tileRotation);
        }

        private void ApplyMeshCollider(
            CurvedTileRuntime runtime,
            bool forceRefresh)
        {
            if (!generateMeshCollider ||
                runtime.MeshObject == null ||
                runtime.CurvedMesh == null)
            {
                if (runtime.MeshCollider != null)
                {
                    DestroyUnityObject(
                        runtime.MeshCollider);
                }

                runtime.MeshCollider = null;
                return;
            }

            if (runtime.MeshCollider == null)
            {
                runtime.MeshCollider =
                    runtime.MeshObject.AddComponent<MeshCollider>();
                forceRefresh = true;
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

        private void ApplyMaterial(
            CurvedTileRuntime runtime)
        {
            if (runtime.MeshRenderer == null)
            {
                return;
            }

            runtime.MeshRenderer.sharedMaterial =
                resolvedMeshMaterial != null
                    ? resolvedMeshMaterial
                    : ResolveFallbackMaterial();
        }

        private Material ResolveFallbackMaterial()
        {
            if (runtimeFallbackMaterial != null)
            {
                SetMaterialColor(
                    runtimeFallbackMaterial,
                    fallbackMeshColor);
                return
                    runtimeFallbackMaterial;
            }

            var shader =
                ResolveFallbackShader();

            if (shader == null)
            {
                if (!missingShaderLogged)
                {
                    Debug.LogError(
                        "No compatible fallback shader was found for the curved MapMagic tile prototype.",
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
                        "Curved Tile Prototype Material",
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            SetMaterialColor(
                runtimeFallbackMaterial,
                fallbackMeshColor);
            missingShaderLogged = false;
            return
                runtimeFallbackMaterial;
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

        private void HideSourceTerrain(
            CurvedTileRuntime runtime)
        {
            var terrain =
                runtime.SourceTerrain;

            if (terrain == null)
            {
                return;
            }

            if (terrain.enabled)
            {
                runtime.SourceTerrainWasEnabled =
                    true;
            }

            if (runtime.SourceTerrainCollider == null)
            {
                runtime.SourceTerrainCollider =
                    terrain.GetComponent<TerrainCollider>();

                if (runtime.SourceTerrainCollider != null)
                {
                    runtime.SourceColliderWasEnabled =
                        runtime.SourceTerrainCollider.enabled;
                }
            }

            terrain.enabled =
                false;

            if (runtime.SourceTerrainCollider != null)
            {
                runtime.SourceTerrainCollider.enabled =
                    false;
            }
        }

        private static void RestoreSourceTerrain(
            CurvedTileRuntime runtime)
        {
            if (runtime.SourceTerrain != null)
            {
                runtime.SourceTerrain.enabled =
                    runtime.SourceTerrainWasEnabled;
            }

            if (runtime.SourceTerrainCollider != null)
            {
                runtime.SourceTerrainCollider.enabled =
                    runtime.SourceColliderWasEnabled;
            }

            runtime.SourceTerrainCollider = null;
            runtime.SourceTerrainWasEnabled = false;
            runtime.SourceColliderWasEnabled = false;
        }

        private void RemoveUndesiredTiles()
        {
            removalBuffer.Clear();

            foreach (var entry in
                curvedTiles)
            {
                if (!desiredTiles.Contains(
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
                RemoveCurvedTile(
                    removalBuffer[index]);
            }
        }

        private void RemoveCurvedTile(
            TileKey key)
        {
            if (!curvedTiles.TryGetValue(
                    key,
                    out var runtime))
            {
                return;
            }

            RestoreSourceTerrain(
                runtime);

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

            curvedTiles.Remove(
                key);
        }

        private void ResolvePrimaryRuntime(
            CubeSphereFace face,
            int tileX,
            int tileZ,
            double meshRadiusMeters)
        {
            curvedTiles.TryGetValue(
                new TileKey(
                    tileX,
                    tileZ),
                out primaryTile);
            hasCurvedTile =
                primaryTile != null &&
                primaryTile.CurvedMesh != null;
            activeFace =
                face;
            effectiveMeshRadiusMeters =
                meshRadiusMeters;
            sourceTileX =
                tileX;
            sourceTileZ =
                tileZ;
            vertexCount =
                hasCurvedTile
                    ? primaryTile.CurvedMesh.vertexCount
                    : 0;
            triangleCount =
                hasCurvedTile
                    ? (int)(
                        primaryTile.CurvedMesh.GetIndexCount(
                            0) /
                        3)
                    : 0;
            hasMeshCollider =
                hasCurvedTile &&
                primaryTile.MeshCollider != null &&
                primaryTile.MeshCollider.sharedMesh ==
                    primaryTile.CurvedMesh;
        }

        private RoundMapMagicSurfaceDefinition ResolveSurfaceDefinition()
        {
            var bodyContext =
                surfaceFrame != null
                    ? surfaceFrame.BodyContext
                    : null;
            var definition =
                bodyContext != null
                    ? bodyContext.Definition
                    : null;

            if (definition == null ||
                definition.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic)
            {
                return null;
            }

            return
                definition.RoundMapMagicSurface;
        }

        private RoundMapMagicSurfaceQualityProfile ResolveQualityProfile()
        {
            ResolveSurfaceSession();

            var qualityProfile =
                surfaceSession != null
                    ? surfaceSession.ConfiguredQualityProfile
                    : null;

            return
                qualityProfile != null &&
                qualityProfile.HasValidSettings
                    ? qualityProfile
                    : null;
        }

        private void ResolveSurfaceSession()
        {
            if (surfaceSession == null &&
                surfaceFrame != null)
            {
                surfaceSession =
                    surfaceFrame.GetComponent<RoundMapMagicSurfaceSession>();
            }
        }

        private int ResolveLocalTileRadius(
            RoundMapMagicSurfaceDefinition surfaceDefinition,
            RoundMapMagicSurfaceQualityProfile qualityProfile)
        {
            if (qualityProfile == null)
            {
                return
                    Mathf.Clamp(
                        curvedTileRadius,
                        0,
                        4);
            }

            var tileSizeMeters =
                surfaceDefinition.TileSizeMeters;
            var fullyCoveredCenterRadiusMeters =
                qualityProfile.LocalCoverageRadiusMeters -
                tileSizeMeters *
                    0.5;
            var tileRadius =
                fullyCoveredCenterRadiusMeters >
                    0.0
                    ? (int)Math.Floor(
                        fullyCoveredCenterRadiusMeters /
                        tileSizeMeters)
                    : 0;

            return
                Mathf.Clamp(
                    tileRadius,
                    0,
                    4);
        }

        private double ResolveMeshRadiusMeters()
        {
            return
                curvatureRadiusOverrideMeters >
                    0.0
                    ? curvatureRadiusOverrideMeters
                    : surfaceFrame.PlanetRadiusMeters;
        }

        private bool ConfigurationIsValid(
            RoundMapMagicSurfaceDefinition surfaceDefinition)
        {
            return
                surfaceFrame != null &&
                coordinateDriver != null &&
                rootPose != null &&
                mapMagicObject != null &&
                surfaceDefinition != null &&
                surfaceDefinition.HasValidSettings &&
                surfaceFrame.PlanetRadiusMeters >
                    0.0 &&
                IsFinite(
                    curvatureRadiusOverrideMeters) &&
                curvatureRadiusOverrideMeters >=
                    0.0 &&
                IsFinite(
                    rootPose.RadialOffsetMeters);
        }

        private void ClearCurvedTiles()
        {
            removalBuffer.Clear();

            foreach (var key in
                curvedTiles.Keys)
            {
                removalBuffer.Add(
                    key);
            }

            for (var index = 0;
                index < removalBuffer.Count;
                index++)
            {
                RemoveCurvedTile(
                    removalBuffer[index]);
            }

            desiredTiles.Clear();
            primaryTile = null;
        }

        private void ClearRuntimeState()
        {
            usingQualityProfile = false;
            resolvedLocalCoverageRadiusMeters = default;
            resolvedLocalTileRadius = default;
            expectedCurvedTileCount = default;
            readySourceTileCount = default;
            activeCurvedTileCount = default;
            hiddenSourceTerrainCount = default;
            hasCurvedTile = false;
            activeFace = default;
            effectiveMeshRadiusMeters = default;
            sourceTileX = default;
            sourceTileZ = default;
            vertexCount = default;
            triangleCount = default;
            hasMeshCollider = false;
        }

        private void OnDisable()
        {
            ClearCurvedTiles();
            ClearRuntimeState();
        }

        private void OnDestroy()
        {
            ClearCurvedTiles();

            if (runtimeFallbackMaterial != null)
            {
                DestroyUnityObject(
                    runtimeFallbackMaterial);
                runtimeFallbackMaterial = null;
            }
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
                Destroy(
                    value);
            }
            else
            {
                DestroyImmediate(
                    value);
            }
        }
    }
}
