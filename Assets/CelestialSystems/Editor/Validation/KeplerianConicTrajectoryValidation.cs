using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class KeplerianConicTrajectoryValidation
    {
        private const double M = 5.9722e24;
        private const double m = 7.342e22;
        private const double Q = 384400000.0;
        private static double Mu => CelestialTrajectoryEvaluator.GravitationalConstant * (M + m);

        [MenuItem("Tools/Celestial Systems/Validate Keplerian Conic Trajectory")]
        public static void Validate()
        {
            CelestialTrajectoryValidation.Validate();
            foreach (var direction in new[] { CelestialOrbitDirection.Prograde, CelestialOrbitDirection.Retrograde })
            {
                var circular = new CelestialTrajectoryDefinition("earth", Q, 125, 37, 23.5, 71, direction);
                var conic = Make(0, direction, 37, 23.5, 71);
                foreach (var t in new[] { -100000.0, 125.0, 345678.0 })
                {
                    Check(CelestialTrajectoryEvaluator.TryEvaluateRelativeState(circular, M, m, t,
                        out var cp, out var cv, out var ce), ce);
                    State(conic, t, out var p, out var v);
                    Near(p, cp, 1e-5, "circular position agreement");
                    Near(v, cv, 1e-9, "circular velocity agreement");
                }
            }

            foreach (var e in new[] { 0.0, 0.5, 0.95, 0.999999, 1.000001, 1.5, 4.0 })
            {
                var d = Make(e);
                State(d, 125, out var p, out var v);
                Near(Length(p), Q, Q * 1e-12, "periapsis distance");
                Near(Length(v), Math.Sqrt(Mu * (1 + e) / Q), 1e-8, "periapsis speed");
                if (e < 1)
                {
                    Check(KeplerianConicTrajectoryEvaluator.TryCalculatePeriodSeconds(d, M, m,
                        out var period, out var error), error);
                    State(d, 125 + period * 0.5, out p, out v);
                    Near(Length(p), Q * (1 + e) / (1 - e), Length(p) * 1e-12, "apoapsis");
                    State(d, 125 + period, out p, out v);
                    Near(p, new DoubleVector3(Q, 0, 0), 0.02, "orbit closure");
                }
                else
                {
                    Check(!KeplerianConicTrajectoryEvaluator.TryCalculatePeriodSeconds(d, M, m,
                        out _, out _), "hyperbola must have no period");
                    State(d, -999875, out var inbound, out var iv);
                    State(d, 1000125, out var outbound, out var ov);
                    Check(Dot(inbound, iv) < 0 && Dot(outbound, ov) > 0, "flyby inbound/outbound");
                    Near(inbound.x, outbound.x, 1e-5, "flyby symmetry x");
                    Near(inbound.z, -outbound.z, 1e-5, "flyby symmetry z");
                }

                var oriented = Make(e, CelestialOrbitDirection.Retrograde, 0, 43, 71, 29);
                foreach (var t in new[] { -999875.0, 125.0, 1000125.0 })
                {
                    State(oriented, t, out p, out v);
                    State(oriented, t - 1, out var before, out _);
                    State(oriented, t + 1, out var after, out _);
                    Near(v, (after + before * -1.0) * 0.5, Math.Max(1e-5, Length(v) * 2e-7),
                        "position derivative");
                    var energy = Dot(v, v) * 0.5 - Mu / Length(p);
                    Near(energy, -Mu * (1 - e) / (2 * Q), Mu / Q * 2e-10, "specific energy");
                    Near(Length(Cross(p, v)), Math.Sqrt(Mu * Q * (1 + e)),
                        Math.Sqrt(Mu * Q * (1 + e)) * 2e-10, "angular momentum");
                }
            }

            var rotated = Make(0.5, CelestialOrbitDirection.Prograde, 0, 90, 90, 90);
            State(rotated, 125, out var rp, out var rv);
            Near(rp, new DoubleVector3(0, Q, 0), 1e-5, "oriented periapsis");
            Check(rv.z > 0, "oriented periapsis velocity");
            Check(!Make(1).TryValidate(out _), "reject parabola");
            Check(!Make(-0.1).TryValidate(out _), "reject negative eccentricity");
            Check(!Make(double.NaN).TryValidate(out _), "reject NaN eccentricity");
            Check(!KeplerianConicTrajectoryEvaluator.TryEvaluateRelativeState(Make(0.5), M, m,
                double.PositiveInfinity, out _, out _, out _), "reject infinite time");
            Check(!KeplerianConicTrajectoryEvaluator.TryEvaluateRelativeState(Make(0.5), -M, m,
                0, out _, out _, out _), "reject negative mass");
            Check(!KeplerianConicTrajectoryEvaluator.TryEvaluateRelativeState(null, M, m,
                0, out _, out _, out _), "reject null definition");
            Debug.Log("Keplerian conic trajectory PASS. Circular agreement, ellipse closure, periapsis/apoapsis, " +
                "hyperbolic flyby, near-parabolic cases, orientation, retrograde, velocity derivative, " +
                "energy, angular momentum, and invalid-input checks passed.");
        }

        private static KeplerianConicTrajectory Make(double e,
            CelestialOrbitDirection direction = CelestialOrbitDirection.Prograde,
            double mean = 0, double inclination = 0, double node = 0, double periapsis = 0) =>
            new KeplerianConicTrajectory("earth", Q, e, 125, mean, inclination, node, periapsis, direction);

        private static void State(KeplerianConicTrajectory d, double t, out DoubleVector3 p, out DoubleVector3 v)
        {
            Check(KeplerianConicTrajectoryEvaluator.TryEvaluateRelativeState(d, M, m, t,
                out p, out v, out var error), error);
        }
        private static double Dot(DoubleVector3 a, DoubleVector3 b) => a.x*b.x + a.y*b.y + a.z*b.z;
        private static double Length(DoubleVector3 a) => Math.Sqrt(Dot(a, a));
        private static DoubleVector3 Cross(DoubleVector3 a, DoubleVector3 b) =>
            new DoubleVector3(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
        private static void Near(DoubleVector3 a, DoubleVector3 b, double tolerance, string label) =>
            Check(Length(a + b * -1.0) <= tolerance, label + ": vector mismatch");
        private static void Near(double a, double b, double tolerance, string label) =>
            Check(Math.Abs(a-b) <= tolerance, label + ": expected " + b + ", got " + a);
        private static void Check(bool pass, string label)
        {
            if (!pass) throw new InvalidOperationException("Keplerian conic validation failed: " + label);
        }
    }
}
