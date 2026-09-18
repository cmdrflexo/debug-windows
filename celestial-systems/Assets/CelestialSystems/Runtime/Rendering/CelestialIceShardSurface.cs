/*
 * Adds deterministic, faceted ice-crystal geometry to any readable source mesh.
 * The generated shards are combined into one mesh and renderer so the feature
 * can be reused by near-detail ring chunks without one draw call per shard.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CelestialIceShardSurface :
        MonoBehaviour
    {
        private const string GeneratedObjectName =
            "Generated Ice Shards";
        private const string IceShaderName =
            "jcan/Celestial Systems/Celestial Ring Ice Chunk";

        [Header("Source")]
        [SerializeField]
        private MeshFilter sourceMeshFilter;

        [SerializeField]
        private Material shardMaterial;

        [SerializeField]
        private bool generateOnEnable =
            true;

        [Header("Distribution")]
        [SerializeField]
        private int seed =
            12345;

        // PROTOTYPE VALUE: tune against the final near-detail chunk meshes.
        [SerializeField]
        [Range(0, 256)]
        private int shardCount =
            28;

        // PROTOTYPE VALUE: several nearby candidates are tested per shard.
        [SerializeField]
        [Range(0, 1)]
        private float clustering =
            0.62f;

        [SerializeField]
        [Range(1, 12)]
        private int clusterCount =
            4;

        [Header("Shape (fraction of mesh size)")]
        [SerializeField]
        private Vector2 lengthRange =
            new Vector2(
                0.08f,
                0.24f);

        [SerializeField]
        private Vector2 widthRange =
            new Vector2(
                0.018f,
                0.055f);

        [SerializeField]
        [Range(0, 55)]
        private float maximumTiltDegrees =
            24.0f;

        [SerializeField]
        [Range(0, 0.5f)]
        private float surfaceInset =
            0.08f;

        [Header("Rendering")]
        [SerializeField]
        private ShadowCastingMode shadowCasting =
            ShadowCastingMode.On;

        [SerializeField]
        private bool receiveShadows =
            true;

        [SerializeField]
        [HideInInspector]
        private Transform generatedTransform;

        private Mesh runtimeMesh;
        private Material runtimeFallbackMaterial;

        private struct SurfacePoint
        {
            public Vector3 Position;
            public Vector3 Normal;
        }

        private void OnEnable()
        {
            if (generateOnEnable)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            ReleaseGeneratedResources();
        }

        private void OnDestroy()
        {
            ReleaseGeneratedResources();
        }

        private void OnValidate()
        {
            shardCount =
                Mathf.Max(
                    0,
                    shardCount);
            clusterCount =
                Mathf.Clamp(
                    clusterCount,
                    1,
                    12);
            lengthRange =
                SortPositiveRange(
                    lengthRange,
                    0.001f);
            widthRange =
                SortPositiveRange(
                    widthRange,
                    0.0001f);
        }

        [ContextMenu("Rebuild Ice Shards")]
        public void Rebuild()
        {
            ReleaseGeneratedResources();

            var filter =
                sourceMeshFilter != null
                    ? sourceMeshFilter
                    : GetComponent<MeshFilter>();
            var sourceMesh =
                filter != null
                    ? filter.sharedMesh
                    : null;

            if (sourceMesh == null ||
                shardCount < 1)
            {
                return;
            }

            Vector3[] sourceVertices;
            Vector3[] sourceNormals;
            int[] sourceTriangles;

            try
            {
                sourceVertices =
                    sourceMesh.vertices;
                sourceNormals =
                    sourceMesh.normals;
                sourceTriangles =
                    sourceMesh.triangles;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Ice shard generation requires a readable source mesh. {exception.Message}",
                    this);
                return;
            }

            if (sourceVertices.Length < 3 ||
                sourceTriangles.Length < 3)
            {
                return;
            }

            var cumulativeAreas =
                BuildCumulativeTriangleAreas(
                    sourceVertices,
                    sourceTriangles,
                    out var totalArea);

            if (totalArea <=
                Mathf.Epsilon)
            {
                return;
            }

            var random =
                new System.Random(
                    seed);
            var anchors =
                new SurfacePoint[
                    Mathf.Max(
                        1,
                        clusterCount)];

            for (var index = 0;
                index < anchors.Length;
                index++)
            {
                anchors[index] =
                    SampleSurfacePoint(
                        sourceVertices,
                        sourceNormals,
                        sourceTriangles,
                        cumulativeAreas,
                        totalArea,
                        random);
            }

            var vertices =
                new List<Vector3>(
                    shardCount * 54);
            var normals =
                new List<Vector3>(
                    shardCount * 54);
            var triangles =
                new List<int>(
                    shardCount * 54);
            var characteristicSize =
                Mathf.Max(
                    sourceMesh.bounds.size.x,
                    Mathf.Max(
                        sourceMesh.bounds.size.y,
                        sourceMesh.bounds.size.z));

            for (var index = 0;
                index < shardCount;
                index++)
            {
                var surfacePoint =
                    SelectClusteredSurfacePoint(
                        anchors,
                        sourceVertices,
                        sourceNormals,
                        sourceTriangles,
                        cumulativeAreas,
                        totalArea,
                        random);
                var length =
                    Mathf.Lerp(
                        lengthRange.x,
                        lengthRange.y,
                        NextFloat(random)) *
                    characteristicSize;
                var width =
                    Mathf.Lerp(
                        widthRange.x,
                        widthRange.y,
                        NextFloat(random)) *
                    characteristicSize;
                var tilt =
                    NextSignedFloat(
                        random) *
                    maximumTiltDegrees;
                var tiltAzimuth =
                    NextFloat(random) *
                    Mathf.PI *
                    2.0f;
                var twist =
                    NextFloat(random) *
                    360.0f;

                AddShard(
                    surfacePoint,
                    length,
                    width,
                    tilt,
                    tiltAzimuth,
                    twist,
                    vertices,
                    normals,
                    triangles);
            }

            runtimeMesh =
                new Mesh
                {
                    name =
                        $"{name} Ice Shards (Runtime)",
                    indexFormat =
                        vertices.Count > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };
            runtimeMesh.SetVertices(
                vertices);
            runtimeMesh.SetNormals(
                normals);
            runtimeMesh.SetTriangles(
                triangles,
                0,
                true);
            runtimeMesh.RecalculateBounds();

            var generatedObject =
                new GameObject(
                    GeneratedObjectName);
            generatedTransform =
                generatedObject.transform;
            generatedTransform.SetParent(
                transform,
                false);

            var generatedFilter =
                generatedObject.AddComponent<MeshFilter>();
            generatedFilter.sharedMesh =
                runtimeMesh;

            var generatedRenderer =
                generatedObject.AddComponent<MeshRenderer>();
            generatedRenderer.sharedMaterial =
                ResolveMaterial();
            generatedRenderer.shadowCastingMode =
                shadowCasting;
            generatedRenderer.receiveShadows =
                receiveShadows;

            var properties =
                new MaterialPropertyBlock();
            properties.SetFloat(
                "_ChunkSeed",
                seed);
            generatedRenderer.SetPropertyBlock(
                properties);
        }

        private Material ResolveMaterial()
        {
            if (shardMaterial != null)
            {
                return shardMaterial;
            }

            var sourceRenderer =
                GetComponent<Renderer>();

            if (sourceRenderer != null &&
                sourceRenderer.sharedMaterial != null &&
                sourceRenderer.sharedMaterial.shader != null &&
                sourceRenderer.sharedMaterial.shader.name ==
                    IceShaderName)
            {
                return sourceRenderer.sharedMaterial;
            }

            var shader =
                Shader.Find(
                    IceShaderName);

            if (shader == null)
            {
                return sourceRenderer != null
                    ? sourceRenderer.sharedMaterial
                    : null;
            }

            runtimeFallbackMaterial =
                new Material(
                    shader)
                {
                    name =
                        "Celestial Ice Shard Fallback (Runtime)"
                };
            runtimeFallbackMaterial.SetFloat(
                "_Opacity",
                0.68f);
            runtimeFallbackMaterial.SetFloat(
                "_TranslucencyStrength",
                1.45f);
            runtimeFallbackMaterial.SetFloat(
                "_DirtAmount",
                0.04f);
            return runtimeFallbackMaterial;
        }

        private SurfacePoint SelectClusteredSurfacePoint(
            SurfacePoint[] anchors,
            Vector3[] vertices,
            Vector3[] sourceNormals,
            int[] sourceTriangles,
            float[] cumulativeAreas,
            float totalArea,
            System.Random random)
        {
            var anchor =
                anchors[
                    random.Next(
                        anchors.Length)];
            var attempts =
                Mathf.RoundToInt(
                    Mathf.Lerp(
                        1.0f,
                        12.0f,
                        clustering));
            var selected =
                SampleSurfacePoint(
                    vertices,
                    sourceNormals,
                    sourceTriangles,
                    cumulativeAreas,
                    totalArea,
                    random);
            var selectedDistance =
                (selected.Position -
                    anchor.Position).sqrMagnitude;

            for (var attempt = 1;
                attempt < attempts;
                attempt++)
            {
                var candidate =
                    SampleSurfacePoint(
                        vertices,
                        sourceNormals,
                        sourceTriangles,
                        cumulativeAreas,
                        totalArea,
                        random);
                var candidateDistance =
                    (candidate.Position -
                        anchor.Position).sqrMagnitude;

                if (candidateDistance <
                    selectedDistance)
                {
                    selected =
                        candidate;
                    selectedDistance =
                        candidateDistance;
                }
            }

            return selected;
        }

        private static SurfacePoint SampleSurfacePoint(
            Vector3[] vertices,
            Vector3[] normals,
            int[] triangles,
            float[] cumulativeAreas,
            float totalArea,
            System.Random random)
        {
            var areaSample =
                NextFloat(random) *
                totalArea;
            var triangleIndex =
                Array.BinarySearch(
                    cumulativeAreas,
                    areaSample);

            if (triangleIndex < 0)
            {
                triangleIndex =
                    ~triangleIndex;
            }

            triangleIndex =
                Mathf.Clamp(
                    triangleIndex,
                    0,
                    cumulativeAreas.Length - 1);
            var triangleOffset =
                triangleIndex *
                3;
            var indexA =
                triangles[triangleOffset];
            var indexB =
                triangles[triangleOffset + 1];
            var indexC =
                triangles[triangleOffset + 2];
            var rootU =
                Mathf.Sqrt(
                    NextFloat(random));
            var barycentricA =
                1.0f -
                rootU;
            var barycentricB =
                rootU *
                (1.0f -
                    NextFloat(random));
            var barycentricC =
                1.0f -
                barycentricA -
                barycentricB;
            var position =
                vertices[indexA] *
                    barycentricA +
                vertices[indexB] *
                    barycentricB +
                vertices[indexC] *
                    barycentricC;
            var faceNormal =
                Vector3.Cross(
                    vertices[indexB] -
                        vertices[indexA],
                    vertices[indexC] -
                        vertices[indexA]).normalized;
            var normal =
                normals != null &&
                normals.Length ==
                    vertices.Length
                    ? (
                        normals[indexA] *
                            barycentricA +
                        normals[indexB] *
                            barycentricB +
                        normals[indexC] *
                            barycentricC).normalized
                    : faceNormal;

            if (normal.sqrMagnitude <
                0.000001f)
            {
                normal =
                    faceNormal;
            }

            return new SurfacePoint
            {
                Position =
                    position,
                Normal =
                    normal
            };
        }

        private static float[] BuildCumulativeTriangleAreas(
            Vector3[] vertices,
            int[] triangles,
            out float totalArea)
        {
            var triangleCount =
                triangles.Length /
                3;
            var cumulative =
                new float[triangleCount];
            totalArea =
                0.0f;

            for (var triangle = 0;
                triangle < triangleCount;
                triangle++)
            {
                var offset =
                    triangle *
                    3;
                var edgeA =
                    vertices[
                        triangles[offset + 1]] -
                    vertices[
                        triangles[offset]];
                var edgeB =
                    vertices[
                        triangles[offset + 2]] -
                    vertices[
                        triangles[offset]];
                totalArea +=
                    Vector3.Cross(
                        edgeA,
                        edgeB).magnitude *
                    0.5f;
                cumulative[triangle] =
                    totalArea;
            }

            return cumulative;
        }

        private void AddShard(
            SurfacePoint surface,
            float length,
            float width,
            float tiltDegrees,
            float tiltAzimuth,
            float twistDegrees,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            var up =
                surface.Normal.normalized;
            var reference =
                Mathf.Abs(
                    Vector3.Dot(
                        up,
                        Vector3.forward)) >
                    0.92f
                    ? Vector3.right
                    : Vector3.forward;
            var tangent =
                Vector3.Cross(
                    reference,
                    up).normalized;
            var bitangent =
                Vector3.Cross(
                    up,
                    tangent).normalized;
            var tiltAxis =
                tangent *
                    Mathf.Cos(
                        tiltAzimuth) +
                bitangent *
                    Mathf.Sin(
                        tiltAzimuth);
            var direction =
                Quaternion.AngleAxis(
                    tiltDegrees,
                    tiltAxis) *
                up;
            var align =
                Quaternion.FromToRotation(
                    up,
                    direction);
            tangent =
                Quaternion.AngleAxis(
                    twistDegrees,
                    direction) *
                (align * tangent);
            bitangent =
                Vector3.Cross(
                    direction,
                    tangent).normalized;
            var baseCenter =
                surface.Position -
                up *
                length *
                surfaceInset;
            var shoulderCenter =
                baseCenter +
                direction *
                length *
                0.72f;
            var tip =
                baseCenter +
                direction *
                length;
            const int sideCount =
                6;

            for (var side = 0;
                side < sideCount;
                side++)
            {
                var next =
                    (side + 1) %
                    sideCount;
                var baseA =
                    CrystalRingPoint(
                        baseCenter,
                        tangent,
                        bitangent,
                        width,
                        side,
                        sideCount);
                var baseB =
                    CrystalRingPoint(
                        baseCenter,
                        tangent,
                        bitangent,
                        width,
                        next,
                        sideCount);
                var shoulderA =
                    CrystalRingPoint(
                        shoulderCenter,
                        tangent,
                        bitangent,
                        width * 0.72f,
                        side,
                        sideCount);
                var shoulderB =
                    CrystalRingPoint(
                        shoulderCenter,
                        tangent,
                        bitangent,
                        width * 0.72f,
                        next,
                        sideCount);

                AddFlatTriangle(
                    baseA,
                    baseB,
                    shoulderB,
                    vertices,
                    normals,
                    triangles);
                AddFlatTriangle(
                    baseA,
                    shoulderB,
                    shoulderA,
                    vertices,
                    normals,
                    triangles);
                AddFlatTriangle(
                    shoulderA,
                    shoulderB,
                    tip,
                    vertices,
                    normals,
                    triangles);
            }
        }

        private static Vector3 CrystalRingPoint(
            Vector3 center,
            Vector3 tangent,
            Vector3 bitangent,
            float radius,
            int side,
            int sideCount)
        {
            var angle =
                side *
                Mathf.PI *
                2.0f /
                sideCount;
            return center +
                tangent *
                    Mathf.Cos(angle) *
                    radius +
                bitangent *
                    Mathf.Sin(angle) *
                    radius;
        }

        private static void AddFlatTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles)
        {
            var normal =
                Vector3.Cross(
                    b - a,
                    c - a).normalized;
            var start =
                vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private void ReleaseGeneratedResources()
        {
            if (generatedTransform == null)
            {
                generatedTransform =
                    transform.Find(
                        GeneratedObjectName);
            }

            if (generatedTransform != null)
            {
                DestroyObject(
                    generatedTransform.gameObject);
                generatedTransform =
                    null;
            }

            if (runtimeMesh != null)
            {
                DestroyObject(
                    runtimeMesh);
                runtimeMesh =
                    null;
            }

            if (runtimeFallbackMaterial != null)
            {
                DestroyObject(
                    runtimeFallbackMaterial);
                runtimeFallbackMaterial =
                    null;
            }
        }

        private static void DestroyObject(
            UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private static Vector2 SortPositiveRange(
            Vector2 range,
            float minimum)
        {
            var low =
                Mathf.Max(
                    minimum,
                    Mathf.Min(
                        range.x,
                        range.y));
            var high =
                Mathf.Max(
                    low,
                    Mathf.Max(
                        range.x,
                        range.y));
            return new Vector2(
                low,
                high);
        }

        private static float NextFloat(
            System.Random random)
        {
            return (float)
                random.NextDouble();
        }

        private static float NextSignedFloat(
            System.Random random)
        {
            return NextFloat(random) *
                2.0f -
                1.0f;
        }
    }
}
