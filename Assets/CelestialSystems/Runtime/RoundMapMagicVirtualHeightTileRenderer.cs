/*
 * Renders one curved mid-detail cube-sphere tile directly from the current cached MapMagic virtual height sample.
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicVirtualHeightTileRenderer :
        MonoBehaviour
    {
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
        [Tooltip("Moves this validation mesh radially without changing its sampled heights. Zero places it on the generated surface.")]
        private double surfaceOffsetMeters;

        [Header("Runtime")]
        [SerializeField]
        private bool hasRenderedTile;

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

        private GameObject meshObject;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh curvedMesh;
        private Material runtimeFallbackMaterial;
        private RoundMapMagicVirtualHeightSample renderedSample;
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
                !heightSampler.HasSample ||
                !surfaceFrame.TryGetPlanetCenterScenePosition(
                    out var planetCenterScenePosition))
            {
                ClearRenderedTile();
                return;
            }

            var sample =
                heightSampler.CurrentSample;

            if (sample == null)
            {
                ClearRenderedTile();
                return;
            }

            if (!ReferenceEquals(
                    renderedSample,
                    sample))
            {
                BuildCurvedTile(
                    sample);
            }

            UpdateTilePose(
                sample,
                planetCenterScenePosition);
            ApplyMaterial();
        }

        private void BuildCurvedTile(
            RoundMapMagicVirtualHeightSample sample)
        {
            EnsureMeshObjects();

            var resolution =
                sample.Resolution;
            var planetRadiusMeters =
                surfaceFrame.PlanetRadiusMeters;
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
                    var index =
                        z *
                        resolution +
                        x;

                    vertices[index] =
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
                    uv[index] =
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
                vertices.Length >
                    65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16;
            curvedMesh.vertices =
                vertices;
            curvedMesh.uv =
                uv;
            curvedMesh.triangles =
                triangles;
            curvedMesh.RecalculateNormals();
            curvedMesh.RecalculateTangents();
            curvedMesh.RecalculateBounds();

            meshObject.name =
                $"Mid Curved Tile {sample.TileX},{sample.TileZ}";
            renderedSample =
                sample;
            hasRenderedTile = true;
            renderedFace =
                sample.Face;
            renderedVirtualTileX =
                sample.TileX;
            renderedVirtualTileZ =
                sample.TileZ;
            renderedTileSizeMeters =
                sample.WorldSizeXMeters;
            renderedResolution =
                resolution;
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
                        "Mid Curved Tile");
                meshObject.transform.SetParent(
                    surfaceFrame.transform,
                    false);
                meshObject.transform.localScale =
                    Vector3.one;
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
                            "Virtual MapMagic Mid Curved Tile"
                    };
                meshFilter.sharedMesh =
                    curvedMesh;
            }
        }

        private void UpdateTilePose(
            RoundMapMagicVirtualHeightSample sample,
            Vector3 planetCenterScenePosition)
        {
            if (meshObject == null)
            {
                return;
            }

            var planetRadiusMeters =
                surfaceFrame.PlanetRadiusMeters;
            var drawingRadiusMeters =
                planetRadiusMeters +
                surfaceOffsetMeters;
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
                    sample.Face,
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
            var centerDirection =
                ToVector3(
                    tileCenterDirection).normalized;
            var faceNormal =
                ToVector3(
                    CubeSphereTopology.GetFaceNormal(
                        sample.Face)).normalized;
            var forward =
                -ToVector3(
                    CubeSphereTopology.GetFaceVAxis(
                        sample.Face)).normalized;

            meshObject.transform.SetPositionAndRotation(
                planetCenterScenePosition +
                    centerDirection *
                        (float)drawingRadiusMeters,
                Quaternion.LookRotation(
                    forward,
                    faceNormal));
        }

        private void ApplyMaterial()
        {
            if (meshRenderer == null)
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

            meshRenderer.sharedMaterial =
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

        private void ClearRenderedTile()
        {
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
            curvedMesh = null;
            renderedSample = null;
            hasRenderedTile = false;
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
            ClearRenderedTile();
        }

        private void OnDestroy()
        {
            ClearRenderedTile();

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
