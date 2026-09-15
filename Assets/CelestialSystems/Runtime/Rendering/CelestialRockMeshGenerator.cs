/*
 * Deterministic, runtime-safe asteroid-style mesh generator. This is an
 * independent implementation inspired by layered broad-to-fine displacement,
 * not a port of Blender's GPL rock add-on.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialRockMeshSettings
    {
        public uint Seed;
        [Range(0, 4)] public int Subdivisions;
        public Vector3 AxisScale;
        [Range(0.0f, 0.8f)] public float BroadDeformation;
        [Range(0.0f, 0.5f)] public float CavityStrength;
        [Range(0.0f, 0.35f)] public float MediumBreakup;
        public bool SmoothNormals;

        public static CelestialRockMeshSettings Default => new CelestialRockMeshSettings
        {
            Seed = 1u,
            Subdivisions = 2,
            AxisScale = new Vector3(1.0f, 0.9f, 1.1f),
            BroadDeformation = 0.22f,
            CavityStrength = 0.17f,
            MediumBreakup = 0.06f,
            SmoothNormals = true
        };
    }

    public static class CelestialRockMeshGenerator
    {
        public static Mesh Create(CelestialRockMeshSettings settings)
        {
            settings.Subdivisions = Mathf.Clamp(settings.Subdivisions, 0, 4);
            settings.AxisScale = new Vector3(
                Mathf.Max(0.05f, settings.AxisScale.x),
                Mathf.Max(0.05f, settings.AxisScale.y),
                Mathf.Max(0.05f, settings.AxisScale.z));

            CreateIcosphere(out var vertices, out var triangles);
            for (var subdivision = 0; subdivision < settings.Subdivisions; subdivision++)
            {
                Subdivide(vertices, triangles);
            }

            for (var index = 0; index < vertices.Count; index++)
            {
                var direction = vertices[index].normalized;
                var radius = EvaluateRadius(direction, settings);
                vertices[index] = Vector3.Scale(
                    direction * radius,
                    settings.AxisScale);
            }

            if (!settings.SmoothNormals)
            {
                MakeFlatShaded(ref vertices, ref triangles);
            }

            var mesh = new Mesh
            {
                name = $"Procedural Rock {settings.Seed:X8}",
                indexFormat = vertices.Count > 65535
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float EvaluateRadius(Vector3 direction, CelestialRockMeshSettings settings)
        {
            var broad = 0.0f;
            for (var layer = 0; layer < 3; layer++)
            {
                var axis = SeedDirection(settings.Seed, (uint)(layer + 1));
                var phase = Hash01(settings.Seed + (uint)(31 + layer)) * Mathf.PI * 2.0f;
                broad += Mathf.Sin(
                    Vector3.Dot(direction, axis) * (2.0f + layer) * Mathf.PI + phase)
                    * (0.65f / (layer + 1.0f));
            }
            broad *= settings.BroadDeformation;

            var cavity = 0.0f;
            for (var cavityIndex = 0; cavityIndex < 5; cavityIndex++)
            {
                var center = SeedDirection(settings.Seed, (uint)(101 + cavityIndex));
                var extent = Mathf.Lerp(
                    0.55f,
                    0.80f,
                    Hash01(settings.Seed + (uint)(151 + cavityIndex)));
                var coverage = Mathf.Clamp01(
                    (Vector3.Dot(direction, center) - extent) / (1.0f - extent));
                cavity += coverage * coverage *
                    Mathf.Lerp(0.45f, 1.0f,
                        Hash01(settings.Seed + (uint)(201 + cavityIndex)));
            }
            cavity *= settings.CavityStrength;

            var medium = 0.0f;
            for (var layer = 0; layer < 2; layer++)
            {
                var axis = SeedDirection(settings.Seed, (uint)(251 + layer));
                var phase = Hash01(settings.Seed + (uint)(271 + layer)) * Mathf.PI * 2.0f;
                medium += Mathf.Abs(Mathf.Sin(
                    Vector3.Dot(direction, axis) * (7.0f + layer * 3.0f) + phase)) - 0.5f;
            }
            medium *= settings.MediumBreakup;

            return Mathf.Max(0.28f, 1.0f + broad - cavity + medium);
        }

        private static void CreateIcosphere(
            out List<Vector3> vertices,
            out List<int> triangles)
        {
            var goldenRatio = (1.0f + Mathf.Sqrt(5.0f)) * 0.5f;
            vertices = new List<Vector3>
            {
                new Vector3(-1, goldenRatio, 0).normalized,
                new Vector3(1, goldenRatio, 0).normalized,
                new Vector3(-1, -goldenRatio, 0).normalized,
                new Vector3(1, -goldenRatio, 0).normalized,
                new Vector3(0, -1, goldenRatio).normalized,
                new Vector3(0, 1, goldenRatio).normalized,
                new Vector3(0, -1, -goldenRatio).normalized,
                new Vector3(0, 1, -goldenRatio).normalized,
                new Vector3(goldenRatio, 0, -1).normalized,
                new Vector3(goldenRatio, 0, 1).normalized,
                new Vector3(-goldenRatio, 0, -1).normalized,
                new Vector3(-goldenRatio, 0, 1).normalized
            };

            triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
        }

        private static void Subdivide(List<Vector3> vertices, List<int> triangles)
        {
            var midpointCache = new Dictionary<ulong, int>();
            var subdivided = new List<int>(triangles.Count * 4);

            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = triangles[index];
                var b = triangles[index + 1];
                var c = triangles[index + 2];
                var ab = GetMidpoint(a, b, vertices, midpointCache);
                var bc = GetMidpoint(b, c, vertices, midpointCache);
                var ca = GetMidpoint(c, a, vertices, midpointCache);

                subdivided.AddRange(new[]
                {
                    a, ab, ca,
                    b, bc, ab,
                    c, ca, bc,
                    ab, bc, ca
                });
            }

            triangles.Clear();
            triangles.AddRange(subdivided);
        }

        private static int GetMidpoint(
            int first,
            int second,
            List<Vector3> vertices,
            Dictionary<ulong, int> cache)
        {
            var low = (uint)Mathf.Min(first, second);
            var high = (uint)Mathf.Max(first, second);
            var key = ((ulong)low << 32) | high;
            if (cache.TryGetValue(key, out var midpoint))
            {
                return midpoint;
            }

            midpoint = vertices.Count;
            vertices.Add((vertices[first] + vertices[second]).normalized);
            cache.Add(key, midpoint);
            return midpoint;
        }

        private static void MakeFlatShaded(
            ref List<Vector3> vertices,
            ref List<int> triangles)
        {
            var flatVertices = new List<Vector3>(triangles.Count);
            var flatTriangles = new List<int>(triangles.Count);
            for (var index = 0; index < triangles.Count; index++)
            {
                flatVertices.Add(vertices[triangles[index]]);
                flatTriangles.Add(index);
            }

            vertices = flatVertices;
            triangles = flatTriangles;
        }

        private static Vector3 SeedDirection(uint seed, uint salt)
        {
            var x = Hash01(seed ^ (salt * 0x9E3779B9u)) * 2.0f - 1.0f;
            var y = Hash01(seed ^ (salt * 0x85EBCA6Bu)) * 2.0f - 1.0f;
            var z = Hash01(seed ^ (salt * 0xC2B2AE35u)) * 2.0f - 1.0f;
            return new Vector3(x, y, z).normalized;
        }

        private static float Hash01(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216.0f;
        }
    }
}
