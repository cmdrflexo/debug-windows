/*
 * Project-owned asynchronous ice-body mesh generation. Worker jobs build raw
 * mesh arrays only; Unity Mesh creation is deliberately kept on the caller's
 * main thread.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [Serializable]
    public sealed class CelestialIceBodyGenerationSettings
    {
        [Range(1, 4)]
        public int Detail = 3;

        [Min(4)]
        public int CellularFeatureCount = 18;

        [Range(0.0f, 1.0f)]
        public float BroadDeformation = 0.22f;

        [Range(0.0f, 1.0f)]
        public float CellularFacetStrength = 0.20f;

        [Range(0.0f, 1.0f)]
        public float CreaseStrength = 0.12f;

        [Range(0.0f, 1.0f)]
        public float FineDetailStrength = 0.04f;

        public Vector3 AxisScale =
            new Vector3(1.0f, 0.9f, 1.1f);

        public bool SmoothNormals = true;

        public bool GenerateUvs = true;

        public CelestialIceBodyGenerationSettings CloneForDetail(
            int detail)
        {
            return new CelestialIceBodyGenerationSettings
            {
                Detail = Mathf.Clamp(
                    detail,
                    1,
                    4),
                CellularFeatureCount =
                    Mathf.Max(
                        4,
                        CellularFeatureCount),
                BroadDeformation =
                    Mathf.Clamp01(
                        BroadDeformation),
                CellularFacetStrength =
                    Mathf.Clamp01(
                        CellularFacetStrength),
                CreaseStrength =
                    Mathf.Clamp01(
                        CreaseStrength),
                FineDetailStrength =
                    Mathf.Clamp01(
                        FineDetailStrength),
                AxisScale =
                    new Vector3(
                        Mathf.Max(
                            0.01f,
                            AxisScale.x),
                        Mathf.Max(
                            0.01f,
                            AxisScale.y),
                        Mathf.Max(
                            0.01f,
                            AxisScale.z)),
                SmoothNormals =
                    SmoothNormals,
                GenerateUvs =
                    GenerateUvs
            };
        }
    }

    public sealed class CelestialIceBodyMeshData
    {
        public float[] Positions { get; }

        public float[] Normals { get; }

        public float[] Uvs { get; }

        public int[] Triangles { get; }

        public CelestialIceBodyMeshData(
            float[] positions,
            float[] normals,
            float[] uvs,
            int[] triangles)
        {
            Positions = positions;
            Normals = normals;
            Uvs = uvs;
            Triangles = triangles;
        }
    }

    public static class CelestialIceBodyMeshGenerator
    {
        private struct Float3
        {
            public float X;
            public float Y;
            public float Z;

            public Float3(
                float x,
                float y,
                float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public static Float3 operator +(
                Float3 left,
                Float3 right)
            {
                return new Float3(
                    left.X + right.X,
                    left.Y + right.Y,
                    left.Z + right.Z);
            }

            public static Float3 operator -(
                Float3 left,
                Float3 right)
            {
                return new Float3(
                    left.X - right.X,
                    left.Y - right.Y,
                    left.Z - right.Z);
            }

            public static Float3 operator *(
                Float3 value,
                float scalar)
            {
                return new Float3(
                    value.X * scalar,
                    value.Y * scalar,
                    value.Z * scalar);
            }
        }

        public static Task<CelestialIceBodyMeshData> GenerateAsync(
            uint seed,
            CelestialIceBodyGenerationSettings settings,
            CancellationToken cancellationToken)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(
                    nameof(settings));
            }

            var snapshot =
                settings.CloneForDetail(
                    settings.Detail);
            return Task.Run(
                () => Generate(
                    seed,
                    snapshot,
                    cancellationToken),
                cancellationToken);
        }

        public static Mesh CreateMesh(
            CelestialIceBodyMeshData data,
            string meshName)
        {
            if (data == null)
            {
                throw new ArgumentNullException(
                    nameof(data));
            }

            var vertexCount =
                data.Positions.Length / 3;
            var vertices =
                new Vector3[vertexCount];
            var normals =
                new Vector3[vertexCount];

            for (var index = 0;
                index < vertexCount;
                index++)
            {
                var sourceIndex =
                    index * 3;
                vertices[index] =
                    new Vector3(
                        data.Positions[
                            sourceIndex],
                        data.Positions[
                            sourceIndex + 1],
                        data.Positions[
                            sourceIndex + 2]);
                normals[index] =
                    new Vector3(
                        data.Normals[
                            sourceIndex],
                        data.Normals[
                            sourceIndex + 1],
                        data.Normals[
                            sourceIndex + 2]);
            }

            var mesh =
                new Mesh
                {
                    name = meshName,
                    indexFormat =
                        vertexCount > 65535
                            ? IndexFormat.UInt32
                            : IndexFormat.UInt16
                };
            mesh.vertices =
                vertices;
            mesh.normals =
                normals;
            mesh.triangles =
                data.Triangles;

            if (data.Uvs != null)
            {
                var uvs =
                    new Vector2[vertexCount];

                for (var index = 0;
                    index < vertexCount;
                    index++)
                {
                    var sourceIndex =
                        index * 2;
                    uvs[index] =
                        new Vector2(
                            data.Uvs[
                                sourceIndex],
                            data.Uvs[
                                sourceIndex + 1]);
                }

                mesh.uv =
                    uvs;
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static CelestialIceBodyMeshData Generate(
            uint seed,
            CelestialIceBodyGenerationSettings settings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var vertices =
                CreateIcosahedronVertices();
            var triangles =
                CreateIcosahedronTriangles();

            for (var step = 0;
                step < settings.Detail;
                step++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Subdivide(
                    vertices,
                    triangles);
            }

            var cellCenters =
                CreateCellCenters(
                    seed,
                    settings.CellularFeatureCount);
            var positionCount =
                vertices.Count;
            var radialDisplacement =
                new float[positionCount];

            for (var index = 0;
                index < positionCount;
                index++)
            {
                if ((index & 255) == 0)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }

                var direction =
                    Normalize(
                        vertices[index]);
                radialDisplacement[index] =
                    EvaluateRadialDisplacement(
                        direction,
                        seed,
                        settings,
                        cellCenters);
                vertices[index] =
                    ApplyAxisScale(
                        direction *
                        (1.0f +
                            radialDisplacement[
                                index]),
                        settings.AxisScale);
            }

            return BuildMeshData(
                vertices,
                triangles,
                settings,
                cancellationToken);
        }

        private static List<Float3> CreateIcosahedronVertices()
        {
            var goldenRatio =
                (1.0f +
                    (float)Math.Sqrt(
                        5.0)) *
                0.5f;
            var vertices =
                new List<Float3>
                {
                    new Float3(-1, goldenRatio, 0),
                    new Float3(1, goldenRatio, 0),
                    new Float3(-1, -goldenRatio, 0),
                    new Float3(1, -goldenRatio, 0),
                    new Float3(0, -1, goldenRatio),
                    new Float3(0, 1, goldenRatio),
                    new Float3(0, -1, -goldenRatio),
                    new Float3(0, 1, -goldenRatio),
                    new Float3(goldenRatio, 0, -1),
                    new Float3(goldenRatio, 0, 1),
                    new Float3(-goldenRatio, 0, -1),
                    new Float3(-goldenRatio, 0, 1)
                };

            for (var index = 0;
                index < vertices.Count;
                index++)
            {
                vertices[index] =
                    Normalize(
                        vertices[index]);
            }

            return vertices;
        }

        private static List<int> CreateIcosahedronTriangles()
        {
            return new List<int>
            {
                0, 11, 5,
                0, 5, 1,
                0, 1, 7,
                0, 7, 10,
                0, 10, 11,
                1, 5, 9,
                5, 11, 4,
                11, 10, 2,
                10, 7, 6,
                7, 1, 8,
                3, 9, 4,
                3, 4, 2,
                3, 2, 6,
                3, 6, 8,
                3, 8, 9,
                4, 9, 5,
                2, 4, 11,
                6, 2, 10,
                8, 6, 7,
                9, 8, 1
            };
        }

        private static void Subdivide(
            List<Float3> vertices,
            List<int> triangles)
        {
            var midpointCache =
                new Dictionary<ulong, int>();
            var nextTriangles =
                new List<int>(
                    triangles.Count * 4);

            for (var index = 0;
                index < triangles.Count;
                index += 3)
            {
                var a = triangles[index];
                var b = triangles[index + 1];
                var c = triangles[index + 2];
                var ab = GetMidpoint(
                    a,
                    b,
                    vertices,
                    midpointCache);
                var bc = GetMidpoint(
                    b,
                    c,
                    vertices,
                    midpointCache);
                var ca = GetMidpoint(
                    c,
                    a,
                    vertices,
                    midpointCache);

                nextTriangles.AddRange(
                    new[]
                    {
                        a, ab, ca,
                        b, bc, ab,
                        c, ca, bc,
                        ab, bc, ca
                    });
            }

            triangles.Clear();
            triangles.AddRange(
                nextTriangles);
        }

        private static int GetMidpoint(
            int a,
            int b,
            List<Float3> vertices,
            Dictionary<ulong, int> cache)
        {
            var low =
                Math.Min(
                    a,
                    b);
            var high =
                Math.Max(
                    a,
                    b);
            var key =
                ((ulong)(uint)low << 32) |
                (uint)high;

            if (cache.TryGetValue(
                    key,
                    out var existing))
            {
                return existing;
            }

            var midpoint =
                Normalize(
                    (vertices[a] +
                        vertices[b]) *
                    0.5f);
            var index =
                vertices.Count;
            vertices.Add(
                midpoint);
            cache.Add(
                key,
                index);
            return index;
        }

        private static Float3[] CreateCellCenters(
            uint seed,
            int count)
        {
            var centers =
                new Float3[
                    Math.Max(
                        4,
                        count)];

            for (var index = 0;
                index < centers.Length;
                index++)
            {
                var u =
                    HashToUnitFloat(
                        seed +
                        (uint)index *
                        0x9E3779B9u);
                var v =
                    HashToUnitFloat(
                        seed ^
                        ((uint)index *
                            0x85EBCA6Bu));
                var z =
                    1.0f -
                    2.0f * u;
                var radial =
                    (float)Math.Sqrt(
                        Math.Max(
                            0.0,
                            1.0 -
                                z * z));
                var angle =
                    v * Mathf.PI *
                    2.0f;
                centers[index] =
                    new Float3(
                        radial *
                            (float)Math.Cos(
                                angle),
                        z,
                        radial *
                            (float)Math.Sin(
                                angle));
            }

            return centers;
        }

        private static float EvaluateRadialDisplacement(
            Float3 direction,
            uint seed,
            CelestialIceBodyGenerationSettings settings,
            Float3[] cellCenters)
        {
            var nearestIndex = 0;
            var nearestDistance =
                float.MaxValue;
            var secondDistance =
                float.MaxValue;

            for (var index = 0;
                index < cellCenters.Length;
                index++)
            {
                var delta =
                    direction -
                    cellCenters[index];
                var distance =
                    Dot(
                        delta,
                        delta);

                if (distance <
                    nearestDistance)
                {
                    secondDistance =
                        nearestDistance;
                    nearestDistance =
                        distance;
                    nearestIndex =
                        index;
                }
                else if (distance <
                    secondDistance)
                {
                    secondDistance =
                        distance;
                }
            }

            var cellValue =
                HashToSignedFloat(
                    seed ^
                    ((uint)nearestIndex *
                        0x27D4EB2Du));
            var edgeDifference =
                Math.Max(
                    0.0f,
                    secondDistance -
                    nearestDistance);
            var edgeFactor =
                (float)Math.Exp(
                    -edgeDifference *
                    32.0f);
            var broad =
                FractalNoise(
                    direction,
                    seed,
                    3,
                    1.35f) *
                settings.BroadDeformation;
            var fine =
                FractalNoise(
                    direction,
                    seed ^
                    0xA5A5A5A5u,
                    4,
                    7.0f) *
                settings.FineDetailStrength;
            var facets =
                cellValue *
                settings.CellularFacetStrength;
            var crease =
                -edgeFactor *
                settings.CreaseStrength;

            return broad +
                fine +
                facets +
                crease;
        }

        private static float FractalNoise(
            Float3 point,
            uint seed,
            int octaves,
            float frequency)
        {
            var sum = 0.0f;
            var weight = 1.0f;
            var totalWeight = 0.0f;

            for (var octave = 0;
                octave < octaves;
                octave++)
            {
                var phaseX =
                    HashToSignedFloat(
                        seed +
                        (uint)octave *
                        0x9E3779B9u) *
                    6.2831853f;
                var phaseY =
                    HashToSignedFloat(
                        seed ^
                        ((uint)octave *
                            0x85EBCA6Bu)) *
                    6.2831853f;
                var phaseZ =
                    HashToSignedFloat(
                        seed +
                        (uint)octave *
                        0xC2B2AE35u) *
                    6.2831853f;
                var sample =
                    (float)Math.Sin(
                        point.X *
                            frequency +
                        phaseX) *
                    (float)Math.Cos(
                        point.Y *
                            frequency *
                            1.17f +
                        phaseY) *
                    (float)Math.Sin(
                        point.Z *
                            frequency *
                            0.83f +
                        phaseZ);
                sum +=
                    sample * weight;
                totalWeight +=
                    weight;
                weight *=
                    0.5f;
                frequency *=
                    2.03f;
            }

            return
                totalWeight > 0.0f
                    ? sum / totalWeight
                    : 0.0f;
        }

        private static CelestialIceBodyMeshData BuildMeshData(
            List<Float3> vertices,
            List<int> triangles,
            CelestialIceBodyGenerationSettings settings,
            CancellationToken cancellationToken)
        {
            var normals =
                CalculateSmoothNormals(
                    vertices,
                    triangles,
                    cancellationToken);

            if (!settings.SmoothNormals)
            {
                return BuildFlatMeshData(
                    vertices,
                    triangles,
                    settings.GenerateUvs);
            }

            var positions =
                new float[
                    vertices.Count * 3];
            var outputNormals =
                new float[
                    normals.Length * 3];
            var uvs =
                settings.GenerateUvs
                    ? new float[
                        vertices.Count * 2]
                    : null;

            for (var index = 0;
                index < vertices.Count;
                index++)
            {
                var positionIndex =
                    index * 3;
                positions[positionIndex] =
                    vertices[index].X;
                positions[positionIndex + 1] =
                    vertices[index].Y;
                positions[positionIndex + 2] =
                    vertices[index].Z;
                outputNormals[positionIndex] =
                    normals[index].X;
                outputNormals[positionIndex + 1] =
                    normals[index].Y;
                outputNormals[positionIndex + 2] =
                    normals[index].Z;

                if (uvs != null)
                {
                    var uv =
                        CalculateUv(
                            vertices[index]);
                    var uvIndex =
                        index * 2;
                    uvs[uvIndex] =
                        uv.x;
                    uvs[uvIndex + 1] =
                        uv.y;
                }
            }

            return new CelestialIceBodyMeshData(
                positions,
                outputNormals,
                uvs,
                triangles.ToArray());
        }

        private static CelestialIceBodyMeshData BuildFlatMeshData(
            List<Float3> vertices,
            List<int> triangles,
            bool generateUvs)
        {
            var positions =
                new float[
                    triangles.Count * 3];
            var normals =
                new float[
                    triangles.Count * 3];
            var uvs =
                generateUvs
                    ? new float[
                        triangles.Count * 2]
                    : null;
            var outputTriangles =
                new int[
                    triangles.Count];

            for (var index = 0;
                index < triangles.Count;
                index += 3)
            {
                var a =
                    vertices[
                        triangles[index]];
                var b =
                    vertices[
                        triangles[index + 1]];
                var c =
                    vertices[
                        triangles[index + 2]];
                var normal =
                    Normalize(
                        Cross(
                            b - a,
                            c - a));

                for (var corner = 0;
                    corner < 3;
                    corner++)
                {
                    var vertexIndex =
                        index + corner;
                    var position =
                        corner == 0
                            ? a
                            : corner == 1
                                ? b
                                : c;
                    var positionOffset =
                        vertexIndex * 3;
                    positions[positionOffset] =
                        position.X;
                    positions[positionOffset + 1] =
                        position.Y;
                    positions[positionOffset + 2] =
                        position.Z;
                    normals[positionOffset] =
                        normal.X;
                    normals[positionOffset + 1] =
                        normal.Y;
                    normals[positionOffset + 2] =
                        normal.Z;
                    outputTriangles[vertexIndex] =
                        vertexIndex;

                    if (uvs != null)
                    {
                        var uv =
                            CalculateUv(
                                position);
                        var uvOffset =
                            vertexIndex * 2;
                        uvs[uvOffset] =
                            uv.x;
                        uvs[uvOffset + 1] =
                            uv.y;
                    }
                }
            }

            return new CelestialIceBodyMeshData(
                positions,
                normals,
                uvs,
                outputTriangles);
        }

        private static Float3[] CalculateSmoothNormals(
            List<Float3> vertices,
            List<int> triangles,
            CancellationToken cancellationToken)
        {
            var normals =
                new Float3[
                    vertices.Count];

            for (var index = 0;
                index < triangles.Count;
                index += 3)
            {
                if ((index & 1023) == 0)
                {
                    cancellationToken
                        .ThrowIfCancellationRequested();
                }

                var a =
                    vertices[
                        triangles[index]];
                var b =
                    vertices[
                        triangles[index + 1]];
                var c =
                    vertices[
                        triangles[index + 2]];
                var normal =
                    Cross(
                        b - a,
                        c - a);
                normals[triangles[index]] =
                    normals[triangles[index]] +
                    normal;
                normals[triangles[index + 1]] =
                    normals[triangles[index + 1]] +
                    normal;
                normals[triangles[index + 2]] =
                    normals[triangles[index + 2]] +
                    normal;
            }

            for (var index = 0;
                index < normals.Length;
                index++)
            {
                normals[index] =
                    Normalize(
                        normals[index]);
            }

            return normals;
        }

        private static Float3 ApplyAxisScale(
            Float3 value,
            Vector3 scale)
        {
            return new Float3(
                value.X * scale.x,
                value.Y * scale.y,
                value.Z * scale.z);
        }

        private static Vector2 CalculateUv(
            Float3 point)
        {
            var direction =
                Normalize(
                    point);
            var u =
                (float)Math.Atan2(
                    direction.X,
                    direction.Z) /
                (2.0f * Mathf.PI) +
                0.5f;
            var v =
                (float)Math.Asin(
                    Math.Max(
                        -1.0f,
                        Math.Min(
                            1.0f,
                            direction.Y))) /
                Mathf.PI +
                0.5f;
            return new Vector2(
                u,
                v);
        }

        private static Float3 Normalize(
            Float3 value)
        {
            var length =
                (float)Math.Sqrt(
                    Dot(
                        value,
                        value));

            return length > 0.000001f
                ? value *
                    (1.0f / length)
                : new Float3(
                    0.0f,
                    1.0f,
                    0.0f);
        }

        private static Float3 Cross(
            Float3 left,
            Float3 right)
        {
            return new Float3(
                left.Y * right.Z -
                    left.Z * right.Y,
                left.Z * right.X -
                    left.X * right.Z,
                left.X * right.Y -
                    left.Y * right.X);
        }

        private static float Dot(
            Float3 left,
            Float3 right)
        {
            return
                left.X * right.X +
                left.Y * right.Y +
                left.Z * right.Z;
        }

        private static float HashToUnitFloat(
            uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return
                (value & 0x00FFFFFFu) /
                16777215.0f;
        }

        private static float HashToSignedFloat(
            uint value)
        {
            return
                HashToUnitFloat(
                    value) *
                2.0f -
                1.0f;
        }
    }
}
