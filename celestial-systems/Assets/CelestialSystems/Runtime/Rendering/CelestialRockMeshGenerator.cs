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
        [Range(0.0f, 0.5f)] public float BroadDeformation;
        [Range(0.0f, 0.5f)] public float CellularFacetStrength;
        [Range(0.0f, 0.25f)] public float MediumBreakup;
        public bool SmoothNormals;

        public static CelestialRockMeshSettings Default => new CelestialRockMeshSettings
        {
            Seed = 1u,
            Subdivisions = 3,
            AxisScale = new Vector3(1.0f, 0.9f, 1.1f),
            BroadDeformation = 0.14f,
            CellularFacetStrength = 0.18f,
            MediumBreakup = 0.035f,
            SmoothNormals = true
        };
    }

    public static class CelestialRockMeshGenerator
    {
        private const int CellularSiteCount = 18;

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

            var cellularSites = CreateCellularSites(settings.Seed);
            for (var index = 0; index < vertices.Count; index++)
            {
                var direction = vertices[index].normalized;
                var radius = EvaluateRadius(direction, settings, cellularSites);
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

        private static float EvaluateRadius(
            Vector3 direction,
            CelestialRockMeshSettings settings,
            CellularSite[] cellularSites)
        {
            var broad = 0.0f;
            for (var lobe = 0; lobe < 4; lobe++)
            {
                var axis = SeedDirection(settings.Seed, (uint)(lobe + 1));
                var prominence = Mathf.Lerp(
                    0.45f,
                    1.0f,
                    Hash01(settings.Seed + (uint)(31 + lobe)));
                var facing = Mathf.Max(0.0f, Vector3.Dot(direction, axis));
                broad += facing * facing * prominence;
            }
            // Center the positive lobe sum, keeping the overall volume stable.
            broad = (broad - 0.70f) * settings.BroadDeformation;

            var nearest = -2.0f;
            var secondNearest = -2.0f;
            var nearestIndex = 0;
            for (var index = 0; index < cellularSites.Length; index++)
            {
                var proximity = Vector3.Dot(direction, cellularSites[index].Direction);
                if (proximity > nearest)
                {
                    secondNearest = nearest;
                    nearest = proximity;
                    nearestIndex = index;
                }
                else if (proximity > secondNearest)
                {
                    secondNearest = proximity;
                }
            }

            // The difference is zero on a Voronoi boundary and rises toward the
            // centre of a cell. Low boundaries and differently raised centres
            // produce the broad plates and creases seen on fractured asteroids.
            var centreWeight = Mathf.SmoothStep(
                0.015f,
                0.150f,
                nearest - secondNearest);
            var cellHeight = cellularSites[nearestIndex].Height;
            var cellular = Mathf.Lerp(
                -0.18f,
                cellHeight,
                centreWeight) * settings.CellularFacetStrength;

            var medium = 0.0f;
            for (var layer = 0; layer < 3; layer++)
            {
                var axis = SeedDirection(settings.Seed, (uint)(301 + layer));
                var phase = Hash01(settings.Seed + (uint)(331 + layer)) * Mathf.PI * 2.0f;
                medium += (Mathf.Abs(Mathf.Sin(
                    Vector3.Dot(direction, axis) * (9.0f + layer * 4.0f) + phase))
                    - 0.5f) * (0.60f / (layer + 1.0f));
            }
            medium *= settings.MediumBreakup;

            return Mathf.Max(0.45f, 1.0f + broad + cellular + medium);
        }

        private static CellularSite[] CreateCellularSites(uint seed)
        {
            var sites = new CellularSite[CellularSiteCount];
            for (var index = 0; index < sites.Length; index++)
            {
                var salt = (uint)(101 + index);
                sites[index] = new CellularSite
                {
                    Direction = SeedDirection(seed, salt),
                    Height = Mathf.Lerp(
                        0.18f,
                        0.95f,
                        Hash01(seed + salt * 0x9E3779B9u))
                };
            }

            return sites;
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

        private struct CellularSite
        {
            public Vector3 Direction;
            public float Height;
        }
    }
}
