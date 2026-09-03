/*
 * Renders the six cached spherical MapMagic height maps as a complete low-detail cube-sphere surface without Unity Terrains or colliders.
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(290)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicSphericalFarSurfaceRenderer :
        MonoBehaviour
    {
        private sealed class FaceRuntime
        {
            public CubeSphereFace Face;
            public Texture2D SourceTexture;
            public GameObject MeshObject;
            public Mesh Mesh;
            public MeshRenderer MeshRenderer;
            public Material Material;
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
        private RoundMapMagicSphericalFaceMapCache faceMapCache;

        [SerializeField]
        private Material meshMaterial;

        [SerializeField]
        [Range(3, 257)]
        private int meshResolution = 65;

        [SerializeField]
        [Tooltip("Multiplies only the generated MapMagic height so the coarse relief can be exaggerated while testing.")]
        [Min(0.0f)]
        private float heightMultiplier = 1.0f;

        [SerializeField]
        [Tooltip("Moves the whole validation surface radially without changing the cached heights.")]
        private double surfaceOffsetMeters;

        [SerializeField]
        [Tooltip("Displays each face's single-channel height map through the selected material when supported.")]
        private bool displayHeightMaps = true;

        [SerializeField]
        private Color fallbackColor =
            new Color(
                0.8f,
                0.15f,
                0.1f,
                1.0f);

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForFaceMaps;

        [SerializeField]
        private bool hasBuiltSurface;

        [SerializeField]
        private int generatedFaceCount;

        [SerializeField]
        private int generatedVertexCount;

        [SerializeField]
        private int generatedTriangleCount;

        [SerializeField]
        private int sourceFaceResolution;

        [SerializeField]
        private float resolvedHeightScaleMeters;

        [SerializeField]
        private double lastBuildMilliseconds;

        [SerializeField]
        private string lastError;

        private readonly FaceRuntime[] faceRuntimes =
            new FaceRuntime[6];

        private double builtPlanetRadiusMeters;
        private double builtSurfaceOffsetMeters;
        private float builtHeightMultiplier;
        private int builtMeshResolution;
        private bool builtDisplayHeightMaps;
        private Material builtMeshMaterial;

        public bool HasBuiltSurface =>
            hasBuiltSurface;

        private void Reset()
        {
            ResolveLocalReferences();
        }

        private void Awake()
        {
            ResolveLocalReferences();
        }

        private void LateUpdate()
        {
            ResolveLocalReferences();

            if (!TryResolveBuildInputs(
                    out var planetCenterScenePosition,
                    out var planetRadiusMeters,
                    out var heightScaleMeters))
            {
                return;
            }

            waitingForFaceMaps = false;

            if (NeedsRebuild(
                    planetRadiusMeters,
                    heightScaleMeters))
            {
                BuildSurface(
                    planetRadiusMeters,
                    heightScaleMeters);
            }

            if (hasBuiltSurface)
            {
                UpdateFacePoses(
                    planetCenterScenePosition,
                    planetRadiusMeters);
            }
        }

        [ContextMenu("Rebuild Spherical Far Surface")]
        private void RebuildSurface()
        {
            ClearSurface();
        }

        private bool TryResolveBuildInputs(
            out Vector3 planetCenterScenePosition,
            out double planetRadiusMeters,
            out float heightScaleMeters)
        {
            planetCenterScenePosition = default;
            planetRadiusMeters = default;
            heightScaleMeters = default;

            if (surfaceFrame == null ||
                faceMapCache == null)
            {
                waitingForFaceMaps = true;
                return false;
            }

            planetRadiusMeters =
                surfaceFrame.PlanetRadiusMeters;
            heightScaleMeters =
                faceMapCache.HeightScaleMeters;

            if (!faceMapCache.IsComplete)
            {
                waitingForFaceMaps = true;
                return false;
            }

            if (!IsFinite(
                    planetRadiusMeters) ||
                planetRadiusMeters <= 0.0 ||
                !IsFinite(
                    heightScaleMeters) ||
                heightScaleMeters <= 0.0f ||
                !IsFinite(
                    heightMultiplier) ||
                heightMultiplier < 0.0f ||
                !IsFinite(
                    surfaceOffsetMeters) ||
                planetRadiusMeters +
                    surfaceOffsetMeters <= 0.0)
            {
                SetError(
                    "The spherical far renderer requires a positive planet radius and height scale with finite displacement settings.");
                return false;
            }

            if (!surfaceFrame.TryGetPlanetCenterScenePosition(
                    out planetCenterScenePosition))
            {
                waitingForFaceMaps = true;
                return false;
            }

            return true;
        }

        private bool NeedsRebuild(
            double planetRadiusMeters,
            float heightScaleMeters)
        {
            if (!hasBuiltSurface ||
                generatedFaceCount !=
                    Faces.Length ||
                builtPlanetRadiusMeters !=
                    planetRadiusMeters ||
                builtSurfaceOffsetMeters !=
                    surfaceOffsetMeters ||
                builtHeightMultiplier !=
                    heightMultiplier ||
                builtMeshResolution !=
                    ResolveMeshResolution() ||
                builtDisplayHeightMaps !=
                    displayHeightMaps ||
                builtMeshMaterial !=
                    meshMaterial ||
                resolvedHeightScaleMeters !=
                    heightScaleMeters)
            {
                return true;
            }

            for (var index = 0;
                index < Faces.Length;
                index++)
            {
                var runtime =
                    faceRuntimes[index];
                var sourceTexture =
                    faceMapCache.GetHeightMap(
                        Faces[index]);

                if (runtime == null ||
                    runtime.SourceTexture !=
                        sourceTexture)
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildSurface(
            double planetRadiusMeters,
            float heightScaleMeters)
        {
            ClearSurface();
            var stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var resolvedMeshResolution =
                    ResolveMeshResolution();

                for (var index = 0;
                    index < Faces.Length;
                    index++)
                {
                    var face =
                        Faces[index];
                    var sourceTexture =
                        faceMapCache.GetHeightMap(
                            face);

                    if (sourceTexture == null ||
                        sourceTexture.width < 2 ||
                        sourceTexture.height < 2)
                    {
                        throw new InvalidOperationException(
                            $"The cached {face} height map is missing or invalid.");
                    }

                    faceRuntimes[index] =
                        BuildFace(
                            face,
                            sourceTexture,
                            resolvedMeshResolution,
                            planetRadiusMeters,
                            heightScaleMeters);
                    generatedFaceCount++;
                }

                builtPlanetRadiusMeters =
                    planetRadiusMeters;
                builtSurfaceOffsetMeters =
                    surfaceOffsetMeters;
                builtHeightMultiplier =
                    heightMultiplier;
                builtMeshResolution =
                    resolvedMeshResolution;
                builtDisplayHeightMaps =
                    displayHeightMaps;
                builtMeshMaterial =
                    meshMaterial;
                resolvedHeightScaleMeters =
                    heightScaleMeters;
                sourceFaceResolution =
                    faceRuntimes[0].SourceTexture.width;
                hasBuiltSurface = true;
                lastError = string.Empty;
            }
            catch (Exception exception)
            {
                ClearSurface();
                SetError(
                    exception.GetBaseException().Message);
            }
            finally
            {
                stopwatch.Stop();
                lastBuildMilliseconds =
                    stopwatch.Elapsed.TotalMilliseconds;
            }
        }

        private FaceRuntime BuildFace(
            CubeSphereFace face,
            Texture2D sourceTexture,
            int resolution,
            double planetRadiusMeters,
            float heightScaleMeters)
        {
            var runtime =
                new FaceRuntime
                {
                    Face =
                        face,
                    SourceTexture =
                        sourceTexture
                };
            var meshObject =
                new GameObject(
                    $"Far Surface {face}");
            meshObject.transform.SetParent(
                surfaceFrame.transform,
                false);
            meshObject.transform.localScale =
                Vector3.one;
            var meshFilter =
                meshObject.AddComponent<MeshFilter>();
            var meshRenderer =
                meshObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode =
                ShadowCastingMode.Off;
            meshRenderer.receiveShadows =
                false;
            var mesh =
                CreateFaceMesh(
                    face,
                    sourceTexture,
                    resolution,
                    planetRadiusMeters,
                    heightScaleMeters);
            var material =
                CreateFaceMaterial(
                    face,
                    sourceTexture);

            meshFilter.sharedMesh =
                mesh;
            meshRenderer.sharedMaterial =
                material;
            runtime.MeshObject =
                meshObject;
            runtime.Mesh =
                mesh;
            runtime.MeshRenderer =
                meshRenderer;
            runtime.Material =
                material;
            generatedVertexCount +=
                mesh.vertexCount;
            generatedTriangleCount +=
                mesh.triangles.Length /
                3;
            return runtime;
        }

        private Mesh CreateFaceMesh(
            CubeSphereFace face,
            Texture2D sourceTexture,
            int resolution,
            double planetRadiusMeters,
            float heightScaleMeters)
        {
            var faceNormal =
                CubeSphereTopology.GetFaceNormal(
                    face);
            var faceUAxis =
                CubeSphereTopology.GetFaceUAxis(
                    face);
            var faceVAxis =
                CubeSphereTopology.GetFaceVAxis(
                    face);
            var baseRadiusMeters =
                planetRadiusMeters +
                surfaceOffsetMeters;
            var referencePosition =
                faceNormal *
                baseRadiusMeters;
            var vertices =
                new Vector3[
                    resolution *
                    resolution];
            var normals =
                new Vector3[
                    vertices.Length];
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
                var faceV =
                    1.0 -
                    normalizedZ *
                    2.0;

                for (var x = 0;
                    x < resolution;
                    x++)
                {
                    var normalizedX =
                        x /
                        (double)(
                            resolution -
                            1);
                    var faceU =
                        normalizedX *
                        2.0 -
                        1.0;
                    var address =
                        new CubeSphereAddress(
                            face,
                            faceU,
                            faceV,
                            0.0);
                    var direction =
                        CubeSphereMapping.AddressToDirection(
                            address);
                    var normalizedHeight =
                        sourceTexture.GetPixelBilinear(
                            (float)normalizedX,
                            (float)normalizedZ).r;
                    var heightMeters =
                        normalizedHeight *
                        heightScaleMeters *
                        heightMultiplier;
                    var surfaceRadiusMeters =
                        baseRadiusMeters +
                        heightMeters;
                    var delta =
                        direction *
                            surfaceRadiusMeters -
                        referencePosition;
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
                    normals[vertexIndex] =
                        new Vector3(
                            (float)Dot(
                                direction,
                                faceUAxis),
                            (float)Dot(
                                direction,
                                faceNormal),
                            (float)-Dot(
                                direction,
                                faceVAxis)).normalized;
                    uv[vertexIndex] =
                        new Vector2(
                            (float)normalizedX,
                            (float)normalizedZ);
                }
            }

            var triangleIndex = 0;

            for (var z = 0;
                z < resolution -
                    1;
                z++)
            {
                for (var x = 0;
                    x < resolution -
                        1;
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

            var mesh =
                new Mesh
                {
                    name =
                        $"MapMagic Far Surface {face}",
                    indexFormat =
                        vertices.Length >
                            65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16,
                    vertices =
                        vertices,
                    normals =
                        normals,
                    uv =
                        uv,
                    triangles =
                        triangles
                };
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material CreateFaceMaterial(
            CubeSphereFace face,
            Texture2D sourceTexture)
        {
            Material material;

            if (meshMaterial != null)
            {
                material =
                    new Material(
                        meshMaterial);
            }
            else
            {
                var shader =
                    ResolveFallbackShader();

                if (shader == null)
                {
                    throw new InvalidOperationException(
                        "No compatible shader was found for the spherical far renderer.");
                }

                material =
                    new Material(
                        shader);
            }

            material.name =
                $"MapMagic Far Surface {face} Material";
            material.hideFlags =
                HideFlags.HideAndDontSave;

            if (displayHeightMaps)
            {
                SetMaterialColor(
                    material,
                    Color.white);
                SetMaterialTexture(
                    material,
                    sourceTexture);
            }
            else
            {
                SetMaterialColor(
                    material,
                    fallbackColor);
            }

            return material;
        }

        private static Shader ResolveFallbackShader()
        {
            var renderPipeline =
                GraphicsSettings.currentRenderPipeline;

            if (renderPipeline == null)
            {
                return
                    Shader.Find(
                        "Unlit/Texture") ??
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

        private static void SetMaterialTexture(
            Material material,
            Texture texture)
        {
            if (material.HasProperty(
                    "_BaseMap"))
            {
                material.SetTexture(
                    "_BaseMap",
                    texture);
            }

            if (material.HasProperty(
                    "_MainTex"))
            {
                material.SetTexture(
                    "_MainTex",
                    texture);
            }
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

        private void UpdateFacePoses(
            Vector3 planetCenterScenePosition,
            double planetRadiusMeters)
        {
            var baseRadiusMeters =
                planetRadiusMeters +
                surfaceOffsetMeters;

            for (var index = 0;
                index < faceRuntimes.Length;
                index++)
            {
                var runtime =
                    faceRuntimes[index];

                if (runtime == null ||
                    runtime.MeshObject == null)
                {
                    continue;
                }

                var faceNormal =
                    ToVector3(
                        CubeSphereTopology.GetFaceNormal(
                            runtime.Face)).normalized;
                var forward =
                    -ToVector3(
                        CubeSphereTopology.GetFaceVAxis(
                            runtime.Face)).normalized;

                runtime.MeshObject.transform.SetPositionAndRotation(
                    planetCenterScenePosition +
                        faceNormal *
                            (float)baseRadiusMeters,
                    Quaternion.LookRotation(
                        forward,
                        faceNormal));
            }
        }

        private int ResolveMeshResolution()
        {
            return
                Mathf.Clamp(
                    meshResolution,
                    3,
                    257);
        }

        private void ResolveLocalReferences()
        {
            if (surfaceFrame == null)
            {
                surfaceFrame =
                    GetComponent<GePlanetSurfaceFrame>();
            }

            if (faceMapCache == null)
            {
                faceMapCache =
                    GetComponent<RoundMapMagicSphericalFaceMapCache>();
            }
        }

        private void SetError(
            string error)
        {
            if (lastError ==
                error)
            {
                return;
            }

            lastError =
                error;
            Debug.LogError(
                $"Spherical MapMagic far rendering failed: {error}",
                this);
        }

        private void ClearSurface()
        {
            for (var index = 0;
                index < faceRuntimes.Length;
                index++)
            {
                var runtime =
                    faceRuntimes[index];

                if (runtime == null)
                {
                    continue;
                }

                if (runtime.MeshObject != null)
                {
                    DestroyUnityObject(
                        runtime.MeshObject);
                }

                if (runtime.Mesh != null)
                {
                    DestroyUnityObject(
                        runtime.Mesh);
                }

                if (runtime.Material != null)
                {
                    DestroyUnityObject(
                        runtime.Material);
                }

                faceRuntimes[index] =
                    null;
            }

            hasBuiltSurface = false;
            generatedFaceCount = default;
            generatedVertexCount = default;
            generatedTriangleCount = default;
            sourceFaceResolution = default;
            resolvedHeightScaleMeters = default;
            builtPlanetRadiusMeters = default;
            builtSurfaceOffsetMeters = default;
            builtHeightMultiplier = default;
            builtMeshResolution = default;
            builtDisplayHeightMaps = default;
            builtMeshMaterial = null;
        }

        private void OnDisable()
        {
            ClearSurface();
            waitingForFaceMaps = false;
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
