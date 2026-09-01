/*
 * Converts the active MapMagic main tile into a height-only curved cube-sphere mesh and matching runtime collider.
 */

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
        [Tooltip("Zero uses the real planet radius. A positive value overrides only this prototype mesh's curvature.")]
        private double curvatureRadiusOverrideMeters;

        [SerializeField]
        private Color fallbackMeshColor =
            new Color(0.35f, 0.55f, 0.25f, 1.0f);

        [SerializeField]
        private bool hideSourceTerrain = true;

        [SerializeField]
        private bool generateMeshCollider = true;

        [Header("Resolved Surface Definition")]
        [SerializeField]
        private int resolvedMeshResolution;

        [SerializeField]
        private Material resolvedMeshMaterial;

        [Header("Runtime")]
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

        private GameObject meshObject;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private MeshCollider meshCollider;
        private Mesh curvedMesh;
        private Material runtimeFallbackMaterial;
        private TerrainData builtTerrainData;
        private CubeSphereFace builtFace;
        private int builtTileX;
        private int builtTileZ;
        private int builtResolution;
        private double builtMeshRadiusMeters;
        private bool sourceWasReady;
        private Terrain hiddenTerrain;
        private TerrainCollider hiddenTerrainCollider;
        private bool hiddenTerrainWasEnabled;
        private bool hiddenColliderWasEnabled;
        private bool missingShaderLogged;

        private void Reset()
        {
            coordinateDriver =
                GetComponent<CubeSphereMapMagicCoordinateDriver>();
            rootPose =
                GetComponent<CubeSphereMapMagicRootPose>();
            mapMagicObject =
                GetComponent<MapMagicObject>();
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
            resolvedMeshResolution = default;
            resolvedMeshMaterial = null;

            if (!ConfigurationIsValid(
                    surfaceDefinition) ||
                !coordinateDriver.HasMapMagicCoordinate ||
                !rootPose.HasPose)
            {
                ClearCurvedTile();
                sourceWasReady = false;
                return;
            }

            var nextFace =
                coordinateDriver.ActiveFace;
            var nextTileX =
                coordinateDriver.MapMagicTileX;
            var nextTileZ =
                coordinateDriver.MapMagicTileZ;
            resolvedMeshResolution =
                Mathf.Clamp(
                    surfaceDefinition.MeshResolution,
                    3,
                    257);
            resolvedMeshMaterial =
                surfaceDefinition.Material;

            var nextResolution =
                resolvedMeshResolution;
            var nextMeshRadiusMeters =
                ResolveMeshRadiusMeters();

            if (hasCurvedTile &&
                (builtFace != nextFace ||
                builtTileX != nextTileX ||
                builtTileZ != nextTileZ))
            {
                ClearCurvedTile();
                sourceWasReady = false;
            }

            var tile =
                mapMagicObject.tiles[
                    new Coord(
                        nextTileX,
                        nextTileZ)];

            if (tile == null ||
                tile.main == null ||
                !tile.main.applyReady ||
                tile.main.terrain == null ||
                tile.main.terrain.terrainData == null)
            {
                sourceWasReady = false;
                return;
            }

            var sourceTerrain =
                tile.main.terrain;
            var terrainData =
                sourceTerrain.terrainData;

            if (!sourceWasReady ||
                curvedMesh == null ||
                builtTerrainData != terrainData ||
                builtFace != nextFace ||
                builtTileX != nextTileX ||
                builtTileZ != nextTileZ ||
                builtResolution != nextResolution ||
                builtMeshRadiusMeters !=
                    nextMeshRadiusMeters)
            {
                BuildCurvedTile(
                    sourceTerrain,
                    terrainData,
                    nextFace,
                    nextTileX,
                    nextTileZ,
                    nextResolution,
                    nextMeshRadiusMeters);
            }

            sourceWasReady = true;

            if (hideSourceTerrain)
            {
                HideSourceTerrain(
                    sourceTerrain);
            }
            else
            {
                RestoreSourceTerrain();
            }

            ApplyMaterial();
            ApplyMeshCollider(
                false);
        }

        private void BuildCurvedTile(
            Terrain sourceTerrain,
            TerrainData terrainData,
            CubeSphereFace face,
            int tileX,
            int tileZ,
            int resolution,
            double meshRadiusMeters)
        {
            RestoreSourceTerrain();
            EnsureMeshObjects();

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

            for (var z = 0;
                z < resolution;
                z++)
            {
                var normalizedZ =
                    z /
                    (double)(resolution - 1);
                var localZ =
                    tileSizeZ *
                    normalizedZ;
                var faceVMeters =
                    -(tileZ *
                    tileSizeZ +
                    localZ);

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(resolution - 1);
                    var localX =
                        tileSizeX *
                        normalizedX;
                    var faceUMeters =
                        tileX *
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
                        faceNormal *
                            rootRadiusMeters;
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

            curvedMesh.Clear();
            curvedMesh.indexFormat =
                vertices.Length > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
            curvedMesh.vertices =
                vertices;
            curvedMesh.uv =
                uv;
            curvedMesh.triangles =
                triangles;
            curvedMesh.RecalculateNormals();
            curvedMesh.RecalculateBounds();
            ApplyMeshCollider(
                true);

            meshObject.name =
                $"Curved Tile {tileX},{tileZ}";
            builtTerrainData =
                terrainData;
            builtFace =
                face;
            builtTileX =
                tileX;
            builtTileZ =
                tileZ;
            builtResolution =
                resolution;
            builtMeshRadiusMeters =
                meshRadiusMeters;
            hasCurvedTile = true;
            activeFace =
                face;
            effectiveMeshRadiusMeters =
                meshRadiusMeters;
            sourceTileX =
                tileX;
            sourceTileZ =
                tileZ;
            vertexCount =
                vertices.Length;
            triangleCount =
                triangles.Length /
                3;
        }

        private void EnsureMeshObjects()
        {
            if (meshObject == null)
            {
                meshObject =
                    new GameObject(
                        "Curved Tile");
                meshObject.transform.SetParent(
                    transform,
                    false);
                meshFilter =
                    meshObject.AddComponent<MeshFilter>();
                meshRenderer =
                    meshObject.AddComponent<MeshRenderer>();
                meshRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;
                meshRenderer.receiveShadows =
                    false;
            }

            if (curvedMesh == null)
            {
                curvedMesh =
                    new Mesh
                    {
                        name =
                            "Curved MapMagic Tile Prototype"
                    };
                meshFilter.sharedMesh =
                    curvedMesh;
            }
        }

        private void ApplyMeshCollider(
            bool forceRefresh)
        {
            if (!generateMeshCollider ||
                meshObject == null ||
                curvedMesh == null)
            {
                if (meshCollider != null)
                {
                    DestroyUnityObject(
                        meshCollider);
                }

                meshCollider = null;
                hasMeshCollider = false;
                return;
            }

            if (meshCollider == null)
            {
                meshCollider =
                    meshObject.AddComponent<MeshCollider>();
                forceRefresh = true;
            }

            if (forceRefresh ||
                meshCollider.sharedMesh !=
                    curvedMesh)
            {
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh =
                    curvedMesh;
            }

            hasMeshCollider =
                meshCollider.sharedMesh ==
                    curvedMesh;
        }

        private void ApplyMaterial()
        {
            if (meshRenderer == null)
            {
                return;
            }

            meshRenderer.sharedMaterial =
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
                return runtimeFallbackMaterial;
            }

            var shader =
                Shader.Find(
                    "Universal Render Pipeline/Unlit") ??
                Shader.Find(
                    "HDRP/Unlit") ??
                Shader.Find(
                    "Unlit/Color") ??
                Shader.Find(
                    "Standard");

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
                new Material(shader)
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
            return runtimeFallbackMaterial;
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
            Terrain terrain)
        {
            if (hiddenTerrain != terrain)
            {
                RestoreSourceTerrain();
                hiddenTerrain =
                    terrain;
                hiddenTerrainWasEnabled =
                    terrain.enabled;
                hiddenTerrainCollider =
                    terrain.GetComponent<TerrainCollider>();

                if (hiddenTerrainCollider != null)
                {
                    hiddenColliderWasEnabled =
                        hiddenTerrainCollider.enabled;
                }
            }

            hiddenTerrain.enabled =
                false;

            if (hiddenTerrainCollider != null)
            {
                hiddenTerrainCollider.enabled =
                    false;
            }
        }

        private void RestoreSourceTerrain()
        {
            if (hiddenTerrain != null)
            {
                hiddenTerrain.enabled =
                    hiddenTerrainWasEnabled;
            }

            if (hiddenTerrainCollider != null)
            {
                hiddenTerrainCollider.enabled =
                    hiddenColliderWasEnabled;
            }

            hiddenTerrain = null;
            hiddenTerrainCollider = null;
            hiddenTerrainWasEnabled = false;
            hiddenColliderWasEnabled = false;
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

        private double ResolveMeshRadiusMeters()
        {
            return curvatureRadiusOverrideMeters > 0.0
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
                surfaceFrame.PlanetRadiusMeters > 0.0 &&
                IsFinite(
                    curvatureRadiusOverrideMeters) &&
                curvatureRadiusOverrideMeters >= 0.0 &&
                IsFinite(
                    rootPose.RadialOffsetMeters);
        }

        private void ClearCurvedTile()
        {
            RestoreSourceTerrain();

            if (meshObject != null)
            {
                DestroyUnityObject(
                    meshObject);
            }

            if (curvedMesh != null)
            {
                DestroyUnityObject(
                    curvedMesh);
            }

            meshObject = null;
            meshFilter = null;
            meshRenderer = null;
            meshCollider = null;
            curvedMesh = null;
            builtTerrainData = null;
            builtFace = default;
            builtTileX = default;
            builtTileZ = default;
            builtResolution = default;
            builtMeshRadiusMeters = default;
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
            ClearCurvedTile();
            sourceWasReady = false;
        }

        private void OnDestroy()
        {
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
                first.x * second.x +
                first.y * second.y +
                first.z * second.z;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private static void DestroyUnityObject(
            Object value)
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
