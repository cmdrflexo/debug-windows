/*
 * Runtime fallback meshes for planetary-ring object families. A small,
 * deterministic variant cache avoids authored placeholders while retaining a
 * practical mesh count for the later instanced renderer.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public static class CelestialRingProceduralMeshes
    {
        private const int VariantsPerFamily = 32;

        private static readonly Dictionary<int, Mesh> cachedMeshes =
            new Dictionary<int, Mesh>();

        public static Mesh GetMesh(
            CelestialRingObjectFamilyKind kind,
            uint seed)
        {
            var variant =
                (int)(seed %
                    VariantsPerFamily);
            var key =
                (int)kind *
                    VariantsPerFamily +
                variant;

            if (cachedMeshes.TryGetValue(
                    key,
                    out var mesh) &&
                mesh != null)
            {
                return mesh;
            }

            mesh =
                CreateMesh(
                    kind,
                    MixSeed(
                        (uint)variant,
                        (uint)kind));
            cachedMeshes[key] =
                mesh;
            return mesh;
        }

        private static Mesh CreateMesh(
            CelestialRingObjectFamilyKind kind,
            uint seed)
        {
            var random =
                new DeterministicRandom(
                    seed);
            GetShapeParameters(
                kind,
                ref random,
                out var segments,
                out var rings,
                out var axisScale,
                out var roughness,
                out var facetBias);
            var vertices =
                new List<Vector3>();
            var triangles =
                new List<int>();
            var ringStarts =
                new int[rings];

            vertices.Add(
                CreatePole(
                    -1.0f,
                    axisScale,
                    roughness,
                    facetBias,
                    ref random));

            for (var ring = 0;
                ring < rings;
                ring++)
            {
                ringStarts[ring] =
                    vertices.Count;
                var latitude =
                    (ring + 1.0f) /
                    (rings + 1.0f) *
                    Mathf.PI -
                    Mathf.PI * 0.5f;

                for (var segment = 0;
                    segment < segments;
                    segment++)
                {
                    var longitude =
                        segment /
                        (float)segments *
                        Mathf.PI * 2.0f;
                    vertices.Add(
                        CreateSurfacePoint(
                            latitude,
                            longitude,
                            axisScale,
                            roughness,
                            facetBias,
                            ref random));
                }
            }

            var topIndex =
                vertices.Count;
            vertices.Add(
                CreatePole(
                    1.0f,
                    axisScale,
                    roughness,
                    facetBias,
                    ref random));

            for (var segment = 0;
                segment < segments;
                segment++)
            {
                var next =
                    (segment + 1) %
                    segments;
                triangles.Add(0);
                triangles.Add(
                    ringStarts[0] +
                    next);
                triangles.Add(
                    ringStarts[0] +
                    segment);
            }

            for (var ring = 0;
                ring < rings - 1;
                ring++)
            {
                var current =
                    ringStarts[ring];
                var nextRing =
                    ringStarts[ring + 1];

                for (var segment = 0;
                    segment < segments;
                    segment++)
                {
                    var next =
                        (segment + 1) %
                        segments;
                    triangles.Add(
                        current + segment);
                    triangles.Add(
                        current + next);
                    triangles.Add(
                        nextRing + next);
                    triangles.Add(
                        current + segment);
                    triangles.Add(
                        nextRing + next);
                    triangles.Add(
                        nextRing + segment);
                }
            }

            var lastRing =
                ringStarts[rings - 1];

            for (var segment = 0;
                segment < segments;
                segment++)
            {
                var next =
                    (segment + 1) %
                    segments;
                triangles.Add(
                    lastRing + segment);
                triangles.Add(
                    lastRing + next);
                triangles.Add(
                    topIndex);
            }

            if (kind ==
                CelestialRingObjectFamilyKind.IceChunk)
            {
                AppendIceShardCluster(
                    vertices,
                    triangles,
                    axisScale,
                    ref random);
            }

            var mesh =
                new Mesh
                {
                    name =
                        $"Procedural Ring {kind} {seed:X8}"
                };
            mesh.SetVertices(
                vertices);
            mesh.SetTriangles(
                triangles,
                0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 CreatePole(
            float verticalSign,
            Vector3 axisScale,
            float roughness,
            float facetBias,
            ref DeterministicRandom random)
        {
            var radius =
                1.0f +
                random.NextSigned() *
                roughness;
            return new Vector3(
                random.NextSigned() *
                    facetBias *
                    axisScale.x,
                verticalSign *
                    radius *
                    axisScale.y,
                random.NextSigned() *
                    facetBias *
                    axisScale.z);
        }

        private static Vector3 CreateSurfacePoint(
            float latitude,
            float longitude,
            Vector3 axisScale,
            float roughness,
            float facetBias,
            ref DeterministicRandom random)
        {
            var broadVariation =
                Mathf.Sin(
                    longitude * 3.0f +
                    random.NextSigned()) *
                facetBias;
            var radius =
                Mathf.Max(
                    0.25f,
                    1.0f +
                    broadVariation +
                    random.NextSigned() *
                    roughness);
            var horizontal =
                Mathf.Cos(
                    latitude) *
                radius;

            return new Vector3(
                Mathf.Cos(longitude) *
                    horizontal *
                    axisScale.x,
                Mathf.Sin(latitude) *
                    radius *
                    axisScale.y,
                Mathf.Sin(longitude) *
                    horizontal *
                    axisScale.z);
        }

        private static void AppendIceShardCluster(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 axisScale,
            ref DeterministicRandom random)
        {
            // Prototype ranges. These should eventually be driven by the
            // generated ice fraction and an ice-crystallization descriptor.
            var shardCount =
                Mathf.RoundToInt(
                    random.NextRange(
                        3.0f,
                        8.0f));

            for (var shardIndex = 0;
                shardIndex < shardCount;
                shardIndex++)
            {
                var direction =
                    RandomUnitVector(
                        ref random);
                var baseCenter =
                    new Vector3(
                        direction.x * axisScale.x,
                        direction.y * axisScale.y,
                        direction.z * axisScale.z);
                var surfaceNormal =
                    new Vector3(
                        direction.x /
                            Mathf.Max(
                                axisScale.x,
                                0.001f),
                        direction.y /
                            Mathf.Max(
                                axisScale.y,
                                0.001f),
                        direction.z /
                            Mathf.Max(
                                axisScale.z,
                                0.001f))
                        .normalized;
                var tangent =
                    Vector3.Cross(
                        surfaceNormal,
                        Mathf.Abs(
                            surfaceNormal.y) <
                            0.92f
                                ? Vector3.up
                                : Vector3.right)
                        .normalized;
                var bitangent =
                    Vector3.Cross(
                        surfaceNormal,
                        tangent);
                var tiltedAxis =
                    (surfaceNormal +
                        tangent *
                            random.NextSigned() *
                            0.28f +
                        bitangent *
                            random.NextSigned() *
                            0.28f)
                        .normalized;
                var length =
                    random.NextRange(
                        0.22f,
                        0.72f);
                var radius =
                    length *
                    random.NextRange(
                        0.10f,
                        0.22f);

                AppendCrystalShard(
                    vertices,
                    triangles,
                    baseCenter -
                        tiltedAxis * radius * 0.45f,
                    tiltedAxis,
                    radius,
                    length,
                    ref random);
            }
        }

        private static void AppendCrystalShard(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 baseCenter,
            Vector3 axis,
            float radius,
            float length,
            ref DeterministicRandom random)
        {
            const int sideCount = 6;
            var tangent =
                Vector3.Cross(
                    axis,
                    Mathf.Abs(axis.y) <
                        0.92f
                            ? Vector3.up
                            : Vector3.right)
                    .normalized;
            var bitangent =
                Vector3.Cross(
                    axis,
                    tangent);
            var start =
                vertices.Count;
            var phase =
                random.NextRange(
                    0.0f,
                    Mathf.PI * 2.0f);
            var shoulderCenter =
                baseCenter +
                axis * length * 0.72f;

            for (var side = 0;
                side < sideCount;
                side++)
            {
                var angle =
                    phase +
                    side /
                        (float)sideCount *
                        Mathf.PI *
                        2.0f;
                var radial =
                    tangent *
                        Mathf.Cos(angle) +
                    bitangent *
                        Mathf.Sin(angle);
                var widthVariation =
                    random.NextRange(
                        0.86f,
                        1.14f);
                vertices.Add(
                    baseCenter +
                    radial *
                        radius *
                        widthVariation);
                vertices.Add(
                    shoulderCenter +
                    radial *
                        radius *
                        0.72f *
                        widthVariation);
            }

            var tip =
                vertices.Count;
            vertices.Add(
                baseCenter +
                axis * length);

            for (var side = 0;
                side < sideCount;
                side++)
            {
                var next =
                    (side + 1) %
                    sideCount;
                var baseIndex =
                    start +
                    side * 2;
                var nextBase =
                    start +
                    next * 2;
                var shoulder =
                    baseIndex + 1;
                var nextShoulder =
                    nextBase + 1;

                triangles.Add(
                    baseIndex);
                triangles.Add(
                    nextBase);
                triangles.Add(
                    nextShoulder);
                triangles.Add(
                    baseIndex);
                triangles.Add(
                    nextShoulder);
                triangles.Add(
                    shoulder);
                triangles.Add(
                    shoulder);
                triangles.Add(
                    nextShoulder);
                triangles.Add(
                    tip);
            }
        }

        private static Vector3 RandomUnitVector(
            ref DeterministicRandom random)
        {
            var y =
                random.NextSigned();
            var angle =
                random.NextRange(
                    0.0f,
                    Mathf.PI * 2.0f);
            var horizontal =
                Mathf.Sqrt(
                    Mathf.Max(
                        0.0f,
                        1.0f -
                        y * y));
            return new Vector3(
                Mathf.Cos(angle) *
                    horizontal,
                y,
                Mathf.Sin(angle) *
                    horizontal);
        }

        private static void GetShapeParameters(
            CelestialRingObjectFamilyKind kind,
            ref DeterministicRandom random,
            out int segments,
            out int rings,
            out Vector3 axisScale,
            out float roughness,
            out float facetBias)
        {
            segments = 8;
            rings = 4;
            roughness =
                0.22f;
            facetBias =
                0.10f;
            axisScale =
                Vector3.one;

            switch (kind)
            {
                case CelestialRingObjectFamilyKind.FineIce:
                    segments = 6;
                    rings = 2;
                    axisScale =
                        new Vector3(
                            random.NextRange(
                                1.1f,
                                2.3f),
                            random.NextRange(
                                0.10f,
                                0.24f),
                            random.NextRange(
                                0.45f,
                                0.9f));
                    roughness = 0.16f;
                    facetBias = 0.20f;
                    break;

                case CelestialRingObjectFamilyKind.IceChunk:
                    segments = 8;
                    rings = 4;
                    axisScale =
                        new Vector3(
                            random.NextRange(
                                0.70f,
                                1.35f),
                            random.NextRange(
                                0.55f,
                                1.15f),
                            random.NextRange(
                                0.70f,
                                1.35f));
                    roughness = 0.30f;
                    facetBias = 0.18f;
                    break;

                case CelestialRingObjectFamilyKind.DarkRubble:
                    segments = 7;
                    rings = 3;
                    axisScale =
                        new Vector3(
                            random.NextRange(
                                0.85f,
                                1.55f),
                            random.NextRange(
                                0.22f,
                                0.65f),
                            random.NextRange(
                                0.65f,
                                1.25f));
                    roughness = 0.38f;
                    facetBias = 0.22f;
                    break;

                case CelestialRingObjectFamilyKind.DustCluster:
                    segments = 6;
                    rings = 3;
                    axisScale =
                        new Vector3(
                            random.NextRange(
                                0.45f,
                                0.85f),
                            random.NextRange(
                                0.30f,
                                0.65f),
                            random.NextRange(
                                0.45f,
                                0.85f));
                    roughness = 0.42f;
                    facetBias = 0.28f;
                    break;

                case CelestialRingObjectFamilyKind.LargeClump:
                    segments = 10;
                    rings = 5;
                    axisScale =
                        new Vector3(
                            random.NextRange(
                                0.8f,
                                1.7f),
                            random.NextRange(
                                0.5f,
                                1.25f),
                            random.NextRange(
                                0.8f,
                                1.7f));
                    roughness = 0.48f;
                    facetBias = 0.28f;
                    break;
            }
        }

        private static uint MixSeed(
            uint value,
            uint salt)
        {
            var result =
                value ^ salt *
                0x9E3779B9u;
            result ^= result >> 16;
            result *= 0x85EBCA6Bu;
            result ^= result >> 13;
            return result;
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(
                uint seed)
            {
                state = seed == 0u
                    ? 0xA341316Cu
                    : seed;
            }

            public float NextSigned()
            {
                return Next01() *
                    2.0f - 1.0f;
            }

            public float NextRange(
                float minimum,
                float maximum)
            {
                return Mathf.Lerp(
                    minimum,
                    maximum,
                    Next01());
            }

            private float Next01()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state >> 8) *
                    (1.0f /
                        16777216.0f);
            }
        }
    }
}
