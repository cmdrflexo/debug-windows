/*
 * Runs evaluator checks in Unity without generating terrain or modifying graph assets.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class SphericalContourFibersValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Contour Fibers")]
        public static void Validate()
        {
            var random = new System.Random(431);
            for (var i = 0; i < 10000; i++)
            {
                var n = new DoubleVector3(
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1,
                    random.NextDouble() * 2 - 1);
                var a = Sample(n);
                Require(!double.IsNaN(a) && a >= 0 && a <= 1, "Output range");
                Require(a == Sample(n), "Determinism");
                Require(Math.Abs(a - Sample(n * 7)) < 1e-9, "Direction normalization");
                var scaled = RoundMapMagicSphericalContourFibers.EvaluateNormalized(
                    n, 1392680000, 18548, 360000000, 24, 16000000,
                    0.35, 1.2, SphericalContourFiberOutput.Fibers);
                Require(Math.Abs(a - scaled) < 1e-9, "Physical scale invariance");
            }
            var maximumGap = 0.0;
            // All 12 shared cube-edge ray families. This checks the evaluator,
            // not the production tile mapping, LOD filtering, or texture upload.
            for (var i = 0; i < 3; i++)
            for (var j = i + 1; j < 3; j++)
            for (var si = -1; si <= 1; si += 2)
            for (var sj = -1; sj <= 1; sj += 2)
            for (var t = 0; t <= 256; t++)
            {
                var v = new double[3];
                v[i] = si;
                v[j] = sj;
                v[3-i-j] = t / 128.0 - 1;
                var a = new DoubleVector3(v[0], v[1], v[2]);
                v[j] *= 1 - 1e-10;
                var left = new DoubleVector3(v[0], v[1], v[2]);
                v[j] = sj;
                v[i] *= 1 - 1e-10;
                var right = new DoubleVector3(v[0], v[1], v[2]);
                maximumGap = Math.Max(maximumGap, Math.Abs(Sample(left) - Sample(right)));
                Require(!double.IsNaN(Sample(a)), "Edge finite");
            }
            Require(maximumGap < 1e-6, "Edge continuity");
            Require(Sample(DoubleVector3.zero) == 0, "Zero direction");
            Debug.Log("Streamline Fibers PASS: 10,000 evaluator samples and all 12 edge families. Max gap: " +
                maximumGap.ToString("G6") + ". Rendered tile seams still require visual testing.");
        }

        private static double Sample(DoubleVector3 n)
        {
            return RoundMapMagicSphericalContourFibers.EvaluateNormalized(
                n, 696340000, 18548, 180000000, 24, 8000000,
                0.35, 1.2, SphericalContourFiberOutput.Fibers);
        }

        private static void Require(bool passed, string check)
        {
            if (!passed) throw new InvalidOperationException("Contour Fibers failed: " + check);
        }
    }
}
