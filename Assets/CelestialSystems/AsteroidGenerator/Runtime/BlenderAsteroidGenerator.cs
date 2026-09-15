using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [Serializable]
    public sealed class BlenderAsteroidSettings
    {
        public uint Seed = 12;
        [Tooltip("Per-corner coordinate ranges, as in Blender. These are not final mesh bounds.")]
        public Vector2 ScaleX = new Vector2(1, 5);
        public Vector2 ScaleY = new Vector2(1, 5);
        public Vector2 ScaleZ = new Vector2(1, 5);
        [Tooltip("Distribution bias per axis, -1 to +1. Zero is centered; this is not geometric shear.")]
        public Vector3 Skew = Vector3.zero;
        public bool ScaleTextures;
        public Vector3 TextureScale = Vector3.one;
        [Range(0, 50)] public float Deformation = 7.74f;
        [Range(0, 50)] public float Roughness = 1.56f;
        [Min(0)] public float SmoothFactor;
        [Range(0, 50)] public int SmoothIterations;
        [Range(1, 4)] public int ViewportDetail = 3;
        [Range(1, 4)] public int RenderDetail = 4;
        [Tooltip("-1 chooses one of the 12 original cages from the seed. 0..11 locks the cage.")]
        [Range(-1, 11)] public int BaseShape = -1;
        [Tooltip("Conversion applied after generation. Does not change the noise pattern.")]
        [Min(0.0001f)] public float MetersPerUnit = 1;
        public bool SmoothNormals = true;
        public bool GenerateUVs = true;
    }

    /// <summary>
    /// Unity reconstruction of the supplied Blender Rock Generator pipeline.
    /// Cage recipes and user-unit conversions follow the reference; noise hashes, PRNG,
    /// crease evaluation and some legacy texture kernels are approximations (see README).
    /// Pure mesh generation: no global random state, scene objects or editor dependencies.
    /// </summary>
    public static partial class BlenderAsteroidGenerator
    {
        public static Mesh Create(BlenderAsteroidSettings settings, bool renderDetail = false)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            Validate(settings);
            var rng = new RandomStream(settings.Seed);
            Vector3 restore;
            var surface = CreateCage(settings, rng, out restore);
            int detail = Mathf.Clamp(renderDetail ? settings.RenderDetail : settings.ViewportDetail, 1, 4);
            var layers = CreateLayers(settings, rng);
            float smooth = rng.Gaussian(Mathf.Clamp(settings.SmoothFactor, 0, 128), Mathf.Sqrt(Mathf.Clamp(settings.SmoothFactor, 0, 128)) / 12);
            // Blender adds TWO consecutive Catmull-Clark modifiers at the selected level.
            // Approximation: semisharp crease weights decay linearly, unlike Blender's version-specific mapping.
            for (int i = 0; i < detail * 2; i++) surface = surface.Subdivide();
            foreach (var layer in layers)
            {
                var normals = surface.Normals();
                var next = new List<Vector3>(surface.Vertices.Count);
                foreach (var p in surface.Vertices)
                    next.Add(p + normals[next.Count] * (layer.Sample(p) - layer.Midlevel) * layer.Strength);
                surface.Vertices = next;
            }
            if (settings.SmoothFactor > 0 && settings.SmoothIterations > 0)
                surface.Smooth(smooth, Mathf.Clamp(settings.SmoothIterations, 0, 50));
            for (int i = 0; i < surface.Vertices.Count; i++)
            {
                var p = Vector3.Scale(surface.Vertices[i], restore) * settings.MetersPerUnit;
                // Blender Z up -> Unity Y up; reflection requires reversing face winding below.
                surface.Vertices[i] = new Vector3(p.x, p.z, p.y);
            }
            foreach (var face in surface.Faces) Array.Reverse(face);
            return surface.ToMesh(settings.SmoothNormals, settings.GenerateUVs, settings.Seed);
        }

        private static void Validate(BlenderAsteroidSettings s)
        {
            foreach (float f in new[] { s.ScaleX.x, s.ScaleX.y, s.ScaleY.x, s.ScaleY.y, s.ScaleZ.x, s.ScaleZ.y,
                s.Skew.x, s.Skew.y, s.Skew.z, s.TextureScale.x, s.TextureScale.y, s.TextureScale.z,
                s.Deformation, s.Roughness, s.SmoothFactor, s.MetersPerUnit })
                if (float.IsNaN(f) || float.IsInfinity(f)) throw new ArgumentException("Asteroid settings must be finite.");
            if (s.MetersPerUnit <= 0) throw new ArgumentException("Meters Per Unit must be positive.");
        }

        private static Vector2 Ordered(Vector2 v) => new Vector2(Mathf.Max(0.001f, Mathf.Min(v.x, v.y)), Mathf.Max(0.001f, Mathf.Max(v.x, v.y)));
        private static float Average(List<float> a) { float v = 0; foreach (float x in a) v += x; return Mathf.Max(0.0001f, Mathf.Abs(v / a.Count)); }
        private static void Distribution(Vector2 bounds, float skew, out float mu, out float sigma, out bool upper)
        {
            float t = (Mathf.Clamp(skew, -1, 1) + 1) * 0.5f;
            mu = Mathf.Lerp(bounds.x, bounds.y, t); upper = t >= 0.5f;
            sigma = (upper ? mu - bounds.x : bounds.y - mu) / 3;
        }
        private static float SkewedGaussian(float mu, float sigma, Vector2 bounds, bool upper, RandomStream rng)
        {
            if (sigma <= 0) return mu;
            float raw = rng.Gaussian(mu, sigma);
            if (raw < mu && !upper) return mu + (raw - mu) * ((mu - bounds.x) / (3 * sigma));
            if (raw > mu && upper) return mu + (raw - mu) * ((bounds.y - mu) / (3 * sigma));
            return raw; // Reference intentionally does not clamp Gaussian tails.
        }
        private sealed class RandomStream
        {
            private uint state;
            public RandomStream(uint seed) { state = seed ^ 0xA511E9B3u; if (state == 0) state = 1; }
            public float Unit() { unchecked { state ^= state << 13; state ^= state >> 17; state ^= state << 5; } return ((state >> 8) + 0.5f) / 16777216f; }
            public int Integer(int count) => Mathf.Min(count - 1, (int)(Unit() * count));
            public float Gaussian(float mean, float sigma) => mean + sigma * Mathf.Sqrt(-2 * Mathf.Log(Unit())) * Mathf.Cos(2 * Mathf.PI * Unit());
            public float Beta38() { float a = 0, b = 0; for (int i = 0; i < 3; i++) a -= Mathf.Log(Unit()); for (int i = 0; i < 8; i++) b -= Mathf.Log(Unit()); return a / (a + b); }
        }
        private static ulong Key(int a, int b) => ((ulong)(uint)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
        private sealed class Edge
        {
            public int A, B, Index;
            public List<int> Faces = new List<int>(2);
        }
        private sealed class Surface
        {
            public List<Vector3> Vertices;
            public List<int[]> Faces;
            public Dictionary<ulong, float> Creases = new Dictionary<ulong, float>();
            public Surface(List<Vector3> v, List<int[]> f) { Vertices = v; Faces = f; }
            public List<Edge> Edges()
            {
                var result = new List<Edge>(); var map = new Dictionary<ulong, Edge>();
                for (int fi = 0; fi < Faces.Count; fi++)
                {
                    var f = Faces[fi];
                    for (int j = 0; j < f.Length; j++)
                    {
                        int a = f[j], b = f[(j + 1) % f.Length]; ulong key = Key(a, b);
                        if (!map.TryGetValue(key, out var edge)) { edge = new Edge { A = a, B = b, Index = result.Count }; result.Add(edge); map.Add(key, edge); }
                        edge.Faces.Add(fi);
                    }
                }
                return result;
            }
            public void Orient()
            {
                var edges = Edges(); var adjacency = new List<Edge>[Faces.Count];
                for (int i = 0; i < adjacency.Length; i++) adjacency[i] = new List<Edge>();
                foreach (var e in edges)
                {
                    if (e.Faces.Count != 2) throw new InvalidOperationException("Base cage is not a closed two-manifold.");
                    foreach (int f in e.Faces) adjacency[f].Add(e);
                }
                var seen = new bool[Faces.Count]; var queue = new Queue<int>(); seen[0] = true; queue.Enqueue(0);
                while (queue.Count > 0)
                {
                    int f = queue.Dequeue();
                    foreach (var e in adjacency[f])
                    {
                        int other = e.Faces[0] == f ? e.Faces[1] : e.Faces[0];
                        if (seen[other]) continue;
                        if (Direction(Faces[f], e.A, e.B) == Direction(Faces[other], e.A, e.B)) Array.Reverse(Faces[other]);
                        seen[other] = true; queue.Enqueue(other);
                    }
                }
                double volume = 0;
                foreach (var f in Faces) for (int i = 1; i + 1 < f.Length; i++) volume += Vector3.Dot(Vertices[f[0]], Vector3.Cross(Vertices[f[i]], Vertices[f[i + 1]]));
                if (volume < 0) foreach (var f in Faces) Array.Reverse(f);
            }
            private static int Direction(int[] face, int a, int b)
            { for (int i = 0; i < face.Length; i++) if (face[i] == a && face[(i + 1) % face.Length] == b) return 1; return -1; }
            public Surface Subdivide()
            {
                var edges = Edges(); var edgeMap = new Dictionary<ulong, Edge>();
                var facePoints = new Vector3[Faces.Count]; var faceSum = new Vector3[Vertices.Count];
                var faceCount = new int[Vertices.Count]; var incident = new List<Edge>[Vertices.Count];
                for (int i = 0; i < incident.Length; i++) incident[i] = new List<Edge>();
                for (int fi = 0; fi < Faces.Count; fi++)
                {
                    var f = Faces[fi]; foreach (int v in f) facePoints[fi] += Vertices[v]; facePoints[fi] /= f.Length;
                    foreach (int v in f) { faceSum[v] += facePoints[fi]; faceCount[v]++; }
                }
                foreach (var e in edges) { edgeMap.Add(Key(e.A, e.B), e); incident[e.A].Add(e); incident[e.B].Add(e); }
                var points = new List<Vector3>(Vertices.Count + edges.Count + Faces.Count);
                for (int i = 0; i < Vertices.Count; i++)
                {
                    Vector3 p = Vertices[i], mid = Vector3.zero, creaseNeighbors = Vector3.zero;
                    float strongest = 0, second = 0, third = 0; Vector3 firstP = Vector3.zero, secondP = Vector3.zero;
                    foreach (var e in incident[i])
                    {
                        Vector3 other = Vertices[e.A == i ? e.B : e.A]; mid += (p + other) * 0.5f;
                        Creases.TryGetValue(Key(e.A, e.B), out float c);
                        if (c > strongest) { third = second; second = strongest; secondP = firstP; strongest = c; firstP = other; }
                        else if (c > second) { third = second; second = c; secondP = other; }
                        else third = Mathf.Max(third, c);
                    }
                    int n = incident[i].Count;
                    Vector3 smooth = (faceSum[i] / faceCount[i] + 2 * mid / n + (n - 3) * p) / n;
                    creaseNeighbors = (6 * p + firstP + secondP) / 8;
                    points.Add(Vector3.Lerp(Vector3.Lerp(smooth, creaseNeighbors, Mathf.Clamp01(second)), p, Mathf.Clamp01(third)));
                }
                foreach (var e in edges)
                {
                    Vector3 mid = (Vertices[e.A] + Vertices[e.B]) * 0.5f;
                    Vector3 smooth = (Vertices[e.A] + Vertices[e.B] + facePoints[e.Faces[0]] + facePoints[e.Faces[1]]) * 0.25f;
                    Creases.TryGetValue(Key(e.A, e.B), out float c);
                    points.Add(Vector3.Lerp(smooth, mid, Mathf.Clamp01(c)));
                }
                points.AddRange(facePoints);
                var quads = new List<int[]>();
                for (int fi = 0; fi < Faces.Count; fi++)
                {
                    var f = Faces[fi]; int center = Vertices.Count + edges.Count + fi;
                    for (int j = 0; j < f.Length; j++)
                        quads.Add(new[] { f[j], Vertices.Count + edgeMap[Key(f[j], f[(j + 1) % f.Length])].Index, center, Vertices.Count + edgeMap[Key(f[(j + f.Length - 1) % f.Length], f[j])].Index });
                }
                var result = new Surface(points, quads);
                foreach (var e in edges)
                {
                    Creases.TryGetValue(Key(e.A, e.B), out float c);
                    c = Mathf.Max(0, c - 1); // Fractional semisharp blend for the first refinement.
                    if (c > 0) { result.Creases[Key(e.A, Vertices.Count + e.Index)] = c; result.Creases[Key(e.B, Vertices.Count + e.Index)] = c; }
                }
                return result;
            }
            public Vector3[] Normals()
            {
                var n = new Vector3[Vertices.Count];
                foreach (var f in Faces)
                {
                    Vector3 normal = Vector3.zero;
                    for (int j = 0; j < f.Length; j++) normal += Vector3.Cross(Vertices[f[j]], Vertices[f[(j + 1) % f.Length]]);
                    foreach (int v in f) n[v] += normal;
                }
                for (int i = 0; i < n.Length; i++) n[i] = n[i].normalized;
                return n;
            }
            public void Smooth(float factor, int iterations)
            {
                var edges = Edges();
                for (int k = 0; k < iterations; k++)
                {
                    var sum = new Vector3[Vertices.Count]; var count = new int[Vertices.Count];
                    foreach (var e in edges) { sum[e.A] += Vertices[e.B]; sum[e.B] += Vertices[e.A]; count[e.A]++; count[e.B]++; }
                    for (int i = 0; i < Vertices.Count; i++) Vertices[i] = Vector3.LerpUnclamped(Vertices[i], sum[i] / count[i], factor);
                }
            }
            public Mesh ToMesh(bool smooth, bool uvs, uint seed)
            {
                var normal = Normals(); var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
                var shared = new Dictionary<ulong, int>();
                // A single longitude/latitude chart is deliberately used here. The previous
                // normal-selected six-chart projection changed charts between neighbouring
                // displaced triangles, creating visible patchwork with tiled UV materials.
                // Only triangles crossing the rear longitude seam split their shared vertices.
                foreach (var f in Faces)
                {
                    for (int j = 1; j + 1 < f.Length; j++)
                    {
                        var tri = new[] { f[0], f[j], f[j + 1] };
                        Vector3 fn = Vector3.Cross(Vertices[tri[1]] - Vertices[tri[0]], Vertices[tri[2]] - Vertices[tri[0]]).normalized;
                        var triU = new float[3];
                        for (int k = 0; k < tri.Length; k++)
                        {
                            Vector3 p = Vertices[tri[k]];
                            triU[k] = Mathf.Repeat(Mathf.Atan2(p.x, p.z) / (2.0f * Mathf.PI) + 0.5f, 1.0f);
                        }
                        float minU = Mathf.Min(triU[0], Mathf.Min(triU[1], triU[2]));
                        float maxU = Mathf.Max(triU[0], Mathf.Max(triU[1], triU[2]));
                        bool crossesSeam = maxU - minU > 0.5f;
                        foreach (int index in tri)
                        {
                            Vector3 p = Vertices[index];
                            float u = Mathf.Repeat(Mathf.Atan2(p.x, p.z) / (2.0f * Mathf.PI) + 0.5f, 1.0f);
                            bool seamCopy = crossesSeam && u < 0.5f;
                            if (seamCopy) u += 1.0f;
                            ulong key = ((ulong)(uint)index << 1) | (uint)(uvs && seamCopy ? 1 : 0);
                            if (!smooth || !shared.TryGetValue(key, out int existing))
                            {
                                existing = v.Count; v.Add(Vertices[index]); n.Add(smooth ? normal[index] : fn);
                                if (uvs)
                                {
                                    float radius = Mathf.Max(0.000001f, p.magnitude);
                                    float vCoordinate = Mathf.Asin(Mathf.Clamp(p.y / radius, -1.0f, 1.0f)) / Mathf.PI + 0.5f;
                                    uv.Add(new Vector2(u, vCoordinate));
                                }
                                if (smooth) shared[key] = existing;
                            }
                            triangles.Add(existing);
                        }
                    }
                }
                var mesh = new Mesh { name = $"Blender Asteroid {seed}", indexFormat = v.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetTriangles(triangles, 0);
                if (uvs) { mesh.SetUVs(0, uv); mesh.RecalculateTangents(); }
                mesh.RecalculateBounds(); return mesh;
            }
        }
    }
}
