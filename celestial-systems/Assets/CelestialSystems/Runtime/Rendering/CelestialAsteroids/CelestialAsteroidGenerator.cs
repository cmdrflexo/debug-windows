/*
 * Independent procedural asteroid generator for Celestial Systems.
 * This implementation uses original base forms and noise code. It does not use
 * the Blender add-on's cage recipes, PRNG, texture implementation, or source.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    public enum CelestialAsteroidBaseForm
    {
        Auto,
        Rounded,
        Flattened,
        Elongated,
        Craggy
    }

    [Serializable]
    public sealed class CelestialAsteroidSettings
    {
        public uint Seed = 12u;
        public Vector2 ScaleX = new Vector2(1f, 5f);
        public Vector2 ScaleY = new Vector2(1f, 5f);
        public Vector2 ScaleZ = new Vector2(1f, 5f);
        [Range(-1f, 1f)] public Vector3 Skew = Vector3.zero;
        public bool NormalizeNoiseCoordinates;
        public Vector3 NoiseScale = Vector3.one;
        [Range(0f, 50f)] public float Deformation = 7.5f;
        [Range(0f, 50f)] public float Roughness = 1.5f;
        [Range(0f, 1f)] public float SmoothFactor;
        [Range(0, 10)] public int SmoothIterations;
        [Range(1, 4)] public int ViewportDetail = 3;
        [Range(1, 4)] public int RenderDetail = 4;
        public CelestialAsteroidBaseForm BaseForm = CelestialAsteroidBaseForm.Auto;
        [Min(0.0001f)] public float MetersPerUnit = 1f;
        public bool SmoothNormals = true;

        [Header("Generation Optimizations")]
        [Tooltip("Use one subdivision stage per detail level instead of two.")]
        public bool LowCostTopology;
        [Tooltip("Use a 3x3x3 cellular search rather than the higher-quality 5x5x5 search.")]
        public bool FastCellularNoise = true;
        public bool ApplyMediumDetail = true;
        public bool ApplyFineDetail = true;
        public bool GenerateUVs = true;
        public bool GenerateTangents;
        [Tooltip("Return a shared cached mesh for identical settings. Do not destroy a shared mesh.")]
        public bool UseRuntimeMeshCache;
    }

    public static class CelestialAsteroidGenerator
    {
        private static readonly Dictionary<int, Mesh> MeshCache = new Dictionary<int, Mesh>();

        public static Mesh Create(CelestialAsteroidSettings settings, bool renderDetail = false)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Validate(settings);

            var cacheKey = GetCacheKey(settings, renderDetail);
            if (settings.UseRuntimeMeshCache &&
                MeshCache.TryGetValue(cacheKey, out var cached) &&
                cached != null)
            {
                return cached;
            }

            var mesh = CreateUncached(settings, renderDetail);
            if (settings.UseRuntimeMeshCache)
            {
                MeshCache[cacheKey] = mesh;
            }

            return mesh;
        }

        public static void ClearRuntimeMeshCache()
        {
            foreach (var mesh in MeshCache.Values)
            {
                if (mesh != null) UnityEngine.Object.Destroy(mesh);
            }

            MeshCache.Clear();
        }

        private static Mesh CreateUncached(CelestialAsteroidSettings settings, bool renderDetail)
        {
            var random = new RandomStream(settings.Seed);
            var detail = Mathf.Clamp(renderDetail ? settings.RenderDetail : settings.ViewportDetail, 1, 4);
            var stages = detail * (settings.LowCostTopology ? 1 : 2);

            CreateIcosphere(out var vertices, out var triangles);
            for (var stage = 0; stage < stages; stage++)
            {
                Subdivide(vertices, triangles);
            }

            var axisScale = SelectAxisScale(settings, ref random);
            var form = settings.BaseForm == CelestialAsteroidBaseForm.Auto
                ? (CelestialAsteroidBaseForm)(1 + random.Range(0, 4))
                : settings.BaseForm;
            ApplyBaseForm(vertices, form, axisScale, ref random);

            var textureScale = settings.NormalizeNoiseCoordinates
                ? Vector3.one
                : Vector3.Scale(axisScale, ClampPositive(settings.NoiseScale));
            var layers = CreateLayers(settings, ref random);

            ApplyDisplacement(vertices, triangles, layers[0], textureScale, settings.FastCellularNoise);
            ApplyDisplacement(vertices, triangles, layers[1], textureScale, settings.FastCellularNoise);
            if (settings.ApplyMediumDetail)
                ApplyDisplacement(vertices, triangles, layers[2], textureScale, settings.FastCellularNoise);
            if (settings.ApplyFineDetail)
                ApplyDisplacement(vertices, triangles, layers[3], textureScale, settings.FastCellularNoise);

            if (settings.SmoothFactor > 0f && settings.SmoothIterations > 0)
                Smooth(vertices, triangles, settings.SmoothFactor, settings.SmoothIterations);

            for (var index = 0; index < vertices.Count; index++)
                vertices[index] *= settings.MetersPerUnit;

            return BuildMesh(vertices, triangles, settings);
        }

        private static void ApplyBaseForm(
            List<Vector3> vertices,
            CelestialAsteroidBaseForm form,
            Vector3 scale,
            ref RandomStream random)
        {
            var lobeA = random.UnitVector();
            var lobeB = random.UnitVector();
            var lobeC = random.UnitVector();
            var baseRoughness = form == CelestialAsteroidBaseForm.Craggy ? 0.18f : 0.10f;

            for (var index = 0; index < vertices.Count; index++)
            {
                var direction = vertices[index].normalized;
                var lobes = Mathf.Max(0f, Vector3.Dot(direction, lobeA)) * 0.16f +
                            Mathf.Max(0f, Vector3.Dot(direction, lobeB)) * 0.11f -
                            Mathf.Max(0f, Vector3.Dot(direction, lobeC)) * 0.08f;
                var radius = Mathf.Max(0.5f, 1f + lobes + SignedHash(direction, random.Seed) * baseRoughness);
                vertices[index] = Vector3.Scale(direction * radius, scale);
            }
        }

        private static void ApplyDisplacement(
            List<Vector3> vertices,
            List<int> triangles,
            DisplacementLayer layer,
            Vector3 textureScale,
            bool fastCellular)
        {
            if (Mathf.Approximately(layer.Strength, 0f)) return;

            var normals = CalculateNormals(vertices, triangles);
            for (var index = 0; index < vertices.Count; index++)
            {
                var samplePosition = Divide(vertices[index], textureScale);
                var value = layer.Sample(samplePosition, fastCellular);
                vertices[index] += normals[index] * ((value - layer.Midpoint) * layer.Strength);
            }
        }

        private static DisplacementLayer[] CreateLayers(CelestialAsteroidSettings settings, ref RandomStream random)
        {
            var deformation = settings.Deformation * 0.1f;
            var roughness = settings.Roughness * 0.01f;
            return new[]
            {
                new DisplacementLayer
                {
                    Kind = FieldKind.Fbm,
                    Scale = random.Range(0.52f, 0.74f),
                    Strength = random.Gaussian(deformation * 0.008f, deformation * 0.002f),
                    Midpoint = 0f,
                    Octaves = 1,
                    Seed = random.NextUInt()
                },
                new DisplacementLayer
                {
                    Kind = FieldKind.Cellular,
                    Scale = random.Range(0.52f, 0.74f),
                    Strength = Mathf.Max(0f, random.Gaussian(deformation, deformation * 0.22f)),
                    Midpoint = 0f,
                    Seed = random.NextUInt()
                },
                new DisplacementLayer
                {
                    Kind = FieldKind.Ridged,
                    Scale = random.Range(0.18f, 0.36f),
                    Strength = Mathf.Max(0f, random.Gaussian(roughness * 1.8f, roughness * 0.35f)),
                    Midpoint = 0.5f,
                    Octaves = 4,
                    Seed = random.NextUInt()
                },
                new DisplacementLayer
                {
                    Kind = FieldKind.Fbm,
                    Scale = random.Range(0.08f, 0.18f),
                    Strength = Mathf.Max(0f, random.Gaussian(roughness, roughness * 0.25f)),
                    Midpoint = 0.5f,
                    Octaves = 5,
                    Seed = random.NextUInt()
                }
            };
        }

        private static Mesh BuildMesh(List<Vector3> vertices, List<int> triangles, CelestialAsteroidSettings settings)
        {
            List<Vector2> uvs = null;
            if (settings.GenerateUVs)
                SplitSphericalSeam(ref vertices, ref triangles, out uvs);

            var mesh = new Mesh
            {
                name = $"Celestial Asteroid {settings.Seed:X8}",
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            if (uvs != null) mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            if (!settings.SmoothNormals) MakeFlatShaded(mesh);
            mesh.RecalculateBounds();
            if (settings.GenerateUVs && settings.GenerateTangents) mesh.RecalculateTangents();
            return mesh;
        }

        private static void SplitSphericalSeam(
            ref List<Vector3> vertices,
            ref List<int> triangles,
            out List<Vector2> uvs)
        {
            uvs = new List<Vector2>(vertices.Count);
            for (var index = 0; index < vertices.Count; index++)
                uvs.Add(SphericalUV(vertices[index]));

            var outputVertices = new List<Vector3>();
            var outputUvs = new List<Vector2>();
            var outputTriangles = new List<int>(triangles.Count);
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = triangles[index];
                var b = triangles[index + 1];
                var c = triangles[index + 2];
                var ua = uvs[a].x;
                var ub = uvs[b].x;
                var uc = uvs[c].x;
                var crossesSeam = Mathf.Max(ua, Mathf.Max(ub, uc)) - Mathf.Min(ua, Mathf.Min(ub, uc)) > 0.5f;

                foreach (var source in new[] { a, b, c })
                {
                    var uv = uvs[source];
                    if (crossesSeam && uv.x < 0.5f) uv.x += 1f;
                    outputVertices.Add(vertices[source]);
                    outputUvs.Add(uv);
                    outputTriangles.Add(outputVertices.Count - 1);
                }
            }

            vertices = outputVertices;
            triangles = outputTriangles;
            uvs = outputUvs;
        }

        private static Vector2 SphericalUV(Vector3 position)
        {
            var direction = position.normalized;
            return new Vector2(
                Mathf.Atan2(direction.z, direction.x) / (Mathf.PI * 2f) + 0.5f,
                Mathf.Asin(direction.y) / Mathf.PI + 0.5f);
        }

        private static void MakeFlatShaded(Mesh mesh)
        {
            var sourceVertices = mesh.vertices;
            var sourceTriangles = mesh.triangles;
            var vertices = new Vector3[sourceTriangles.Length];
            var triangles = new int[sourceTriangles.Length];
            var uvs = mesh.uv;
            var hasUvs = uvs != null && uvs.Length == sourceVertices.Length;
            var flatUvs = hasUvs ? new Vector2[sourceTriangles.Length] : null;

            for (var index = 0; index < sourceTriangles.Length; index++)
            {
                vertices[index] = sourceVertices[sourceTriangles[index]];
                triangles[index] = index;
                if (hasUvs) flatUvs[index] = uvs[sourceTriangles[index]];
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            if (hasUvs) mesh.uv = flatUvs;
            mesh.RecalculateNormals();
        }

        private static List<Vector3> CalculateNormals(List<Vector3> vertices, List<int> triangles)
        {
            var normals = new Vector3[vertices.Count];
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = triangles[index];
                var b = triangles[index + 1];
                var c = triangles[index + 2];
                var normal = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += normal;
                normals[b] += normal;
                normals[c] += normal;
            }

            var result = new List<Vector3>(vertices.Count);
            for (var index = 0; index < normals.Length; index++)
                result.Add(normals[index].normalized);
            return result;
        }

        private static void Smooth(List<Vector3> vertices, List<int> triangles, float factor, int iterations)
        {
            var adjacency = new List<int>[vertices.Count];
            for (var index = 0; index < adjacency.Length; index++) adjacency[index] = new List<int>();
            for (var index = 0; index < triangles.Count; index += 3)
            {
                AddNeighbor(adjacency, triangles[index], triangles[index + 1]);
                AddNeighbor(adjacency, triangles[index + 1], triangles[index + 2]);
                AddNeighbor(adjacency, triangles[index + 2], triangles[index]);
            }

            for (var iteration = 0; iteration < iterations; iteration++)
            {
                var next = new Vector3[vertices.Count];
                for (var index = 0; index < vertices.Count; index++)
                {
                    var average = Vector3.zero;
                    foreach (var neighbor in adjacency[index]) average += vertices[neighbor];
                    average /= Mathf.Max(1, adjacency[index].Count);
                    next[index] = Vector3.Lerp(vertices[index], average, factor);
                }
                for (var index = 0; index < vertices.Count; index++) vertices[index] = next[index];
            }
        }

        private static void AddNeighbor(List<int>[] adjacency, int a, int b)
        {
            if (!adjacency[a].Contains(b)) adjacency[a].Add(b);
            if (!adjacency[b].Contains(a)) adjacency[b].Add(a);
        }

        private static Vector3 SelectAxisScale(CelestialAsteroidSettings settings, ref RandomStream random)
        {
            return new Vector3(
                SampleSkewed(Ordered(settings.ScaleX), settings.Skew.x, ref random),
                SampleSkewed(Ordered(settings.ScaleY), settings.Skew.y, ref random),
                SampleSkewed(Ordered(settings.ScaleZ), settings.Skew.z, ref random)) * 0.5f;
        }

        private static float SampleSkewed(Vector2 range, float skew, ref RandomStream random)
        {
            var bias = Mathf.Clamp01((skew + 1f) * 0.5f);
            var centered = random.Gaussian(bias, 0.16f);
            return Mathf.Lerp(range.x, range.y, Mathf.Clamp01(centered));
        }

        private static void CreateIcosphere(out List<Vector3> vertices, out List<int> triangles)
        {
            var g = (1f + Mathf.Sqrt(5f)) * 0.5f;
            vertices = new List<Vector3>
            {
                new Vector3(-1,g,0).normalized, new Vector3(1,g,0).normalized,
                new Vector3(-1,-g,0).normalized, new Vector3(1,-g,0).normalized,
                new Vector3(0,-1,g).normalized, new Vector3(0,1,g).normalized,
                new Vector3(0,-1,-g).normalized, new Vector3(0,1,-g).normalized,
                new Vector3(g,0,-1).normalized, new Vector3(g,0,1).normalized,
                new Vector3(-g,0,-1).normalized, new Vector3(-g,0,1).normalized
            };
            triangles = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
            };
        }

        private static void Subdivide(List<Vector3> vertices, List<int> triangles)
        {
            var cache = new Dictionary<ulong, int>();
            var output = new List<int>(triangles.Count * 4);
            for (var index = 0; index < triangles.Count; index += 3)
            {
                var a = triangles[index]; var b = triangles[index + 1]; var c = triangles[index + 2];
                var ab = Midpoint(a, b, vertices, cache);
                var bc = Midpoint(b, c, vertices, cache);
                var ca = Midpoint(c, a, vertices, cache);
                output.AddRange(new[] { a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca });
            }
            triangles.Clear();
            triangles.AddRange(output);
        }

        private static int Midpoint(int a, int b, List<Vector3> vertices, Dictionary<ulong, int> cache)
        {
            var key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
            if (cache.TryGetValue(key, out var value)) return value;
            value = vertices.Count;
            vertices.Add((vertices[a] + vertices[b]).normalized);
            cache[key] = value;
            return value;
        }

        private static Vector3 Divide(Vector3 value, Vector3 divisor) =>
            new Vector3(value.x / Mathf.Max(0.001f, divisor.x), value.y / Mathf.Max(0.001f, divisor.y), value.z / Mathf.Max(0.001f, divisor.z));
        private static Vector3 ClampPositive(Vector3 value) =>
            new Vector3(Mathf.Max(0.001f, value.x), Mathf.Max(0.001f, value.y), Mathf.Max(0.001f, value.z));
        private static Vector2 Ordered(Vector2 value) => new Vector2(Mathf.Min(value.x, value.y), Mathf.Max(value.x, value.y));

        private static void Validate(CelestialAsteroidSettings settings)
        {
            if (settings.MetersPerUnit <= 0f || float.IsNaN(settings.MetersPerUnit))
                throw new ArgumentException("Meters Per Unit must be finite and positive.");
        }

        private static int GetCacheKey(CelestialAsteroidSettings s, bool render)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + (int)s.Seed;
                hash = hash * 31 + s.ViewportDetail;
                hash = hash * 31 + s.RenderDetail;
                hash = hash * 31 + (int)s.BaseForm;
                hash = hash * 31 + (render ? 1 : 0);
                hash = hash * 31 + (s.LowCostTopology ? 1 : 0);
                hash = hash * 31 + (s.FastCellularNoise ? 1 : 0);
                hash = hash * 31 + (s.ApplyMediumDetail ? 1 : 0);
                hash = hash * 31 + (s.ApplyFineDetail ? 1 : 0);
                hash = hash * 31 + (s.GenerateUVs ? 1 : 0);
                hash = hash * 31 + (s.GenerateTangents ? 1 : 0);
                hash = hash * 31 + (s.SmoothNormals ? 1 : 0);
                hash = hash * 31 + s.ScaleX.GetHashCode();
                hash = hash * 31 + s.ScaleY.GetHashCode();
                hash = hash * 31 + s.ScaleZ.GetHashCode();
                hash = hash * 31 + s.Skew.GetHashCode();
                hash = hash * 31 + s.NormalizeNoiseCoordinates.GetHashCode();
                hash = hash * 31 + s.NoiseScale.GetHashCode();
                hash = hash * 31 + s.Deformation.GetHashCode();
                hash = hash * 31 + s.Roughness.GetHashCode();
                hash = hash * 31 + s.SmoothFactor.GetHashCode();
                hash = hash * 31 + s.SmoothIterations;
                hash = hash * 31 + s.MetersPerUnit.GetHashCode();
                return hash;
            }
        }

        private enum FieldKind { Fbm, Ridged, Cellular }

        private struct DisplacementLayer
        {
            public FieldKind Kind;
            public float Scale;
            public float Strength;
            public float Midpoint;
            public int Octaves;
            public uint Seed;

            public float Sample(Vector3 position, bool fastCellular)
            {
                position /= Mathf.Max(0.01f, Scale);
                switch (Kind)
                {
                    case FieldKind.Cellular:
                        return Cellular(position, Seed, fastCellular ? 1 : 2);
                    case FieldKind.Ridged:
                        return Ridged(position, Seed, Octaves);
                    default:
                        return Fbm(position, Seed, Octaves);
                }
            }
        }

        private static float Fbm(Vector3 p, uint seed, int octaves)
        {
            var value = 0f;
            var amplitude = 1f;
            var total = 0f;
            for (var octave = 0; octave < octaves; octave++)
            {
                value += GradientNoise(p, seed + (uint)octave * 101u) * amplitude;
                total += amplitude;
                p *= 2.05f;
                amplitude *= 0.5f;
            }
            return value / total;
        }

        private static float Ridged(Vector3 p, uint seed, int octaves)
        {
            var value = 0f;
            var amplitude = 1f;
            var total = 0f;
            for (var octave = 0; octave < octaves; octave++)
            {
                value += (1f - Mathf.Abs(GradientNoise(p, seed + (uint)octave * 131u) * 2f - 1f)) * amplitude;
                total += amplitude;
                p *= 2.1f;
                amplitude *= 0.5f;
            }
            return value / total;
        }

        private static float Cellular(Vector3 p, uint seed, int radius)
        {
            var baseX = Mathf.FloorToInt(p.x);
            var baseY = Mathf.FloorToInt(p.y);
            var baseZ = Mathf.FloorToInt(p.z);
            var nearest = float.MaxValue;
            for (var z = baseZ - radius; z <= baseZ + radius; z++)
            for (var y = baseY - radius; y <= baseY + radius; y++)
            for (var x = baseX - radius; x <= baseX + radius; x++)
            {
                var point = new Vector3(
                    x + Hash01(x, y, z, seed),
                    y + Hash01(x, y, z, seed + 1u),
                    z + Hash01(x, y, z, seed + 2u));
                nearest = Mathf.Min(nearest, (point - p).magnitude);
            }
            return Mathf.Clamp01(nearest * 0.85f);
        }

        private static float GradientNoise(Vector3 p, uint seed)
        {
            var x = Mathf.FloorToInt(p.x);
            var y = Mathf.FloorToInt(p.y);
            var z = Mathf.FloorToInt(p.z);
            var f = new Vector3(p.x - x, p.y - y, p.z - z);
            var u = new Vector3(Fade(f.x), Fade(f.y), Fade(f.z));
            float Sample(int ox, int oy, int oz) => Hash01(x + ox, y + oy, z + oz, seed);
            var a = Mathf.Lerp(Sample(0,0,0), Sample(1,0,0), u.x);
            var b = Mathf.Lerp(Sample(0,1,0), Sample(1,1,0), u.x);
            var c = Mathf.Lerp(Sample(0,0,1), Sample(1,0,1), u.x);
            var d = Mathf.Lerp(Sample(0,1,1), Sample(1,1,1), u.x);
            return Mathf.Lerp(Mathf.Lerp(a,b,u.y), Mathf.Lerp(c,d,u.y), u.z);
        }

        private static float SignedHash(Vector3 direction, uint seed) =>
            Hash01(Mathf.RoundToInt(direction.x * 7919f), Mathf.RoundToInt(direction.y * 104729f), Mathf.RoundToInt(direction.z * 15485863f), seed) * 2f - 1f;
        private static float Fade(float value) => value * value * (3f - 2f * value);
        private static float Hash01(int x, int y, int z, uint seed)
        {
            unchecked
            {
                var value = (uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu ^ (uint)z * 0xC2B2AE35u ^ seed;
                value ^= value >> 16; value *= 0x7FEB352Du; value ^= value >> 15; value *= 0x846CA68Bu; value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777216f;
            }
        }

        private struct RandomStream
        {
            private uint state;
            public uint Seed { get; }
            public RandomStream(uint seed) { Seed = seed == 0u ? 1u : seed; state = Seed; }
            public uint NextUInt() { state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state; }
            public float Unit() => ((NextUInt() >> 8) + 0.5f) / 16777216f;
            public int Range(int min, int maxExclusive) => min + Mathf.Min(maxExclusive - min - 1, Mathf.FloorToInt(Unit() * (maxExclusive - min)));
            public float Range(float min, float max) => Mathf.Lerp(min, max, Unit());
            public float Gaussian(float mean, float standardDeviation) => mean + standardDeviation * Mathf.Sqrt(-2f * Mathf.Log(Unit())) * Mathf.Cos(2f * Mathf.PI * Unit());
            public Vector3 UnitVector()
            {
                var z = Range(-1f, 1f);
                var angle = Range(0f, Mathf.PI * 2f);
                var radial = Mathf.Sqrt(1f - z * z);
                return new Vector3(Mathf.Cos(angle) * radial, z, Mathf.Sin(angle) * radial);
            }
        }
    }
}
