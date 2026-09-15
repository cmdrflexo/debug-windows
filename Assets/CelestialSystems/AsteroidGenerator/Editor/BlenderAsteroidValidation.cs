using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class BlenderAsteroidValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Blender Asteroid Generator")]
        public static void Validate()
        {
            // Exercises the actual generator in Unity, including seams and degenerate input handling.
            int cases = 0;
            for (int shape = 0; shape < 12; shape++)
            {
                foreach (bool scaled in new[] { false, true })
                {
                    var s = new BlenderAsteroidSettings { BaseShape = shape, Seed = (uint)(12 + shape), ViewportDetail = 1, ScaleTextures = scaled };
                    Mesh a = null, b = null;
                    try
                    {
                        a = BlenderAsteroidGenerator.Create(s); b = BlenderAsteroidGenerator.Create(s);
                        Check(a); Check(b);
                        var av = a.vertices; var bv = b.vertices;
                        Require(av.Length == bv.Length, "Deterministic vertex count");
                        for (int i = 0; i < av.Length; i++) Require(av[i].Equals(bv[i]), "Deterministic positions");
                        var at = a.triangles; var bt = b.triangles;
                        Require(at.Length == bt.Length, "Deterministic index count");
                        for (int i = 0; i < at.Length; i++) Require(at[i] == bt[i], "Deterministic indices");
                        cases++;
                    }
                    finally { if (a != null) UnityEngine.Object.DestroyImmediate(a); if (b != null) UnityEngine.Object.DestroyImmediate(b); }
                }
            }
            var invalid = new BlenderAsteroidSettings { Deformation = float.NaN };
            bool rejected = false;
            try { var unexpected = BlenderAsteroidGenerator.Create(invalid); UnityEngine.Object.DestroyImmediate(unexpected); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Reject non-finite settings");
            Debug.Log($"Blender Asteroid validation PASS: {cases} cage/texture-scale cases; finite geometry, closed welded topology, outward winding, UVs, normals, repeatable seeds and invalid input rejection.");
        }
        private static void Check(Mesh mesh)
        {
            var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; var triangles = mesh.triangles;
            Require(v.Length > 0 && triangles.Length > 0 && n.Length == v.Length && uv.Length == v.Length, "Mesh channels");
            var welded = new Dictionary<Vector3, int>(); var ids = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                Require(Finite(v[i].x) && Finite(v[i].y) && Finite(v[i].z), "Finite vertices");
                Require(Finite(n[i].x) && Finite(n[i].y) && Finite(n[i].z) && n[i].sqrMagnitude > 0.5f, "Valid normals");
                Require(Finite(uv[i].x) && Finite(uv[i].y) && uv[i].x >= 0 && uv[i].x <= 1 && uv[i].y >= 0 && uv[i].y <= 1, "UV range");
                if (!welded.TryGetValue(v[i], out int id)) { id = welded.Count; welded.Add(v[i], id); }
                ids[i] = id;
            }
            double volume = 0; var counts = new Dictionary<ulong, int>(); var winding = new Dictionary<ulong, int>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i+1], c = triangles[i+2];
                Require(a >= 0 && a < v.Length && b >= 0 && b < v.Length && c >= 0 && c < v.Length, "Index range");
                Require(Vector3.Cross(v[b]-v[a],v[c]-v[a]).sqrMagnitude > 1e-16f, "Nondegenerate triangles");
                volume += Vector3.Dot(v[a], Vector3.Cross(v[b], v[c]));
                Count(ids[a],ids[b],counts,winding); Count(ids[b],ids[c],counts,winding); Count(ids[c],ids[a],counts,winding);
            }
            Require(volume > 0, "Outward total winding");
            foreach (var edge in counts) Require(edge.Value == 2 && winding[edge.Key] == 0, "Closed oriented welded topology");
        }
        private static void Count(int a, int b, Dictionary<ulong,int> counts, Dictionary<ulong,int> winding)
        {
            ulong key = ((ulong)(uint)Math.Min(a,b) << 32) | (uint)Math.Max(a,b);
            counts.TryGetValue(key, out int count); counts[key] = count+1;
            winding.TryGetValue(key, out int w); winding[key] = w + (a < b ? 1 : -1);
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Asteroid validation failed: " + message); }
    }
}
