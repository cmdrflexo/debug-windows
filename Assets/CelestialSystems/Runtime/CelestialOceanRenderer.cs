/*
 * Generates a definition-driven cube-sphere ocean shell that shares a round body's reference-radius datum and floating scene-space center.
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(295)]
    [DisallowMultipleComponent]
    [RequireComponent(
        typeof(GePlanetSurfaceFrame))]
    public sealed class CelestialOceanRenderer :
        MonoBehaviour
    {
        private sealed class FaceRuntime
        {
            public CubeSphereFace Face;
            public GameObject MeshObject;
            public Mesh Mesh;
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
        [Range(3, 257)]
        private int meshResolution = 65;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForBody;

        [SerializeField]
        private bool hasBuiltOcean;

        [SerializeField]
        private OceanDefinition activeOceanDefinition;

        [SerializeField]
        private Material resolvedMaterial;

        [SerializeField]
        private double resolvedOceanRadiusMeters;

        [SerializeField]
        private int generatedFaceCount;

        [SerializeField]
        private int generatedVertexCount;

        [SerializeField]
        private int generatedTriangleCount;

        [SerializeField]
        private string lastError;

        private readonly FaceRuntime[] faceRuntimes =
            new FaceRuntime[6];

        private OceanDefinition builtOceanDefinition;
        private Material builtMaterial;
        private double builtReferenceRadiusMeters;
        private double builtSurfaceElevationMeters;
        private int builtMeshResolution;

        public bool WaitingForBody =>
            waitingForBody;

        public bool HasBuiltOcean =>
            hasBuiltOcean;

        public OceanDefinition ActiveOceanDefinition =>
            activeOceanDefinition;

        public double ResolvedOceanRadiusMeters =>
            resolvedOceanRadiusMeters;

        public int GeneratedFaceCount =>
            generatedFaceCount;

        public int GeneratedVertexCount =>
            generatedVertexCount;

        public int GeneratedTriangleCount =>
            generatedTriangleCount;

        public string LastError =>
            lastError;

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

            if (!TryResolveInputs(
                    out var planetCenterScenePosition,
                    out var referenceRadiusMeters,
                    out var oceanDefinition,
                    out var oceanRadiusMeters))
            {
                return;
            }

            waitingForBody = false;

            if (NeedsRebuild(
                    referenceRadiusMeters,
                    oceanDefinition))
            {
                BuildOcean(
                    referenceRadiusMeters,
                    oceanDefinition,
                    oceanRadiusMeters);
            }

            if (hasBuiltOcean)
            {
                UpdateFacePoses(
                    planetCenterScenePosition,
                    oceanRadiusMeters);
            }
        }

        [ContextMenu("Rebuild Ocean")]
        private void RebuildOcean()
        {
            ClearOcean();
        }

        private bool TryResolveInputs(
            out Vector3 planetCenterScenePosition,
            out double referenceRadiusMeters,
            out OceanDefinition oceanDefinition,
            out double oceanRadiusMeters)
        {
            planetCenterScenePosition = default;
            referenceRadiusMeters = default;
            oceanDefinition = null;
            oceanRadiusMeters = default;

            var bodyContext =
                surfaceFrame != null
                    ? surfaceFrame.BodyContext
                    : null;
            var bodyDefinition =
                bodyContext != null
                    ? bodyContext.Definition
                    : null;

            if (bodyDefinition == null)
            {
                waitingForBody = true;
                activeOceanDefinition = null;
                ClearOceanIfPresent();
                return false;
            }

            oceanDefinition =
                bodyDefinition.OceanDefinition;
            activeOceanDefinition =
                oceanDefinition;

            if (oceanDefinition == null)
            {
                waitingForBody = false;
                lastError = string.Empty;
                ClearOceanIfPresent();
                return false;
            }

            referenceRadiusMeters =
                bodyDefinition.ReferenceRadiusMeters;
            oceanRadiusMeters =
                referenceRadiusMeters +
                oceanDefinition.GlobalSurfaceElevationMeters;

            if (!bodyDefinition.HasValidPhysicalSettings ||
                !oceanDefinition.HasValidSettings ||
                !IsFinite(
                    oceanRadiusMeters) ||
                oceanRadiusMeters <= 0.0 ||
                oceanRadiusMeters >
                    float.MaxValue)
            {
                waitingForBody = false;
                ClearOceanIfPresent();
                SetError(
                    "The ocean renderer requires valid body and ocean definitions whose combined radius is positive and within Unity's scene-space range.");
                return false;
            }

            if (!surfaceFrame.TryGetPlanetCenterScenePosition(
                    out planetCenterScenePosition))
            {
                waitingForBody = true;
                return false;
            }

            return true;
        }

        private bool NeedsRebuild(
            double referenceRadiusMeters,
            OceanDefinition oceanDefinition)
        {
            return
                !hasBuiltOcean ||
                generatedFaceCount !=
                    Faces.Length ||
                builtOceanDefinition !=
                    oceanDefinition ||
                builtMaterial !=
                    oceanDefinition.Material ||
                builtReferenceRadiusMeters !=
                    referenceRadiusMeters ||
                builtSurfaceElevationMeters !=
                    oceanDefinition.GlobalSurfaceElevationMeters ||
                builtMeshResolution !=
                    ResolveMeshResolution();
        }

        private void BuildOcean(
            double referenceRadiusMeters,
            OceanDefinition oceanDefinition,
            double oceanRadiusMeters)
        {
            ClearOcean();

            try
            {
                var resolution =
                    ResolveMeshResolution();

                for (var index = 0;
                    index < Faces.Length;
                    index++)
                {
                    faceRuntimes[index] =
                        BuildFace(
                            Faces[index],
                            resolution,
                            oceanRadiusMeters,
                            oceanDefinition.Material);
                    generatedFaceCount++;
                }

                builtOceanDefinition =
                    oceanDefinition;
                builtMaterial =
                    oceanDefinition.Material;
                builtReferenceRadiusMeters =
                    referenceRadiusMeters;
                builtSurfaceElevationMeters =
                    oceanDefinition.GlobalSurfaceElevationMeters;
                builtMeshResolution =
                    resolution;
                activeOceanDefinition =
                    oceanDefinition;
                resolvedMaterial =
                    oceanDefinition.Material;
                resolvedOceanRadiusMeters =
                    oceanRadiusMeters;
                hasBuiltOcean = true;
                lastError = string.Empty;
            }
            catch (Exception exception)
            {
                ClearOcean();
                SetError(
                    exception.GetBaseException().Message);
            }
        }

        private FaceRuntime BuildFace(
            CubeSphereFace face,
            int resolution,
            double oceanRadiusMeters,
            Material material)
        {
            var runtime =
                new FaceRuntime
                {
                    Face =
                        face
                };
            var meshObject =
                new GameObject(
                    $"Ocean {face}");
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
                    resolution,
                    oceanRadiusMeters);
            meshFilter.sharedMesh =
                mesh;
            meshRenderer.sharedMaterial =
                material;

            runtime.MeshObject =
                meshObject;
            runtime.Mesh =
                mesh;
            generatedVertexCount +=
                mesh.vertexCount;
            generatedTriangleCount +=
                (resolution - 1) *
                (resolution - 1) *
                2;
            return runtime;
        }

        private static Mesh CreateFaceMesh(
            CubeSphereFace face,
            int resolution,
            double oceanRadiusMeters)
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
            var referencePosition =
                faceNormal *
                oceanRadiusMeters;
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
                    var delta =
                        direction *
                            oceanRadiusMeters -
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

            var triangleIndex =
                0;

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
                        $"Celestial Ocean {face}",
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
            mesh.RecalculateTangents();
            return mesh;
        }

        private void UpdateFacePoses(
            Vector3 planetCenterScenePosition,
            double oceanRadiusMeters)
        {
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
                            (float)oceanRadiusMeters,
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
        }

        private void ClearOceanIfPresent()
        {
            if (hasBuiltOcean ||
                generatedFaceCount > 0)
            {
                ClearOcean();
            }
        }

        private void ClearOcean()
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

                faceRuntimes[index] =
                    null;
            }

            hasBuiltOcean = false;
            resolvedMaterial = null;
            resolvedOceanRadiusMeters = default;
            generatedFaceCount = default;
            generatedVertexCount = default;
            generatedTriangleCount = default;
            builtOceanDefinition = null;
            builtMaterial = null;
            builtReferenceRadiusMeters = default;
            builtSurfaceElevationMeters = default;
            builtMeshResolution = default;
        }

        private void OnDisable()
        {
            ClearOcean();
            waitingForBody = false;
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
                $"Celestial ocean rendering failed: {error}",
                this);
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
