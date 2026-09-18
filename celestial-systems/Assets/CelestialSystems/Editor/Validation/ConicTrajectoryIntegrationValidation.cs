using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class ConicTrajectoryIntegrationValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Conic Trajectory Integration")]
        public static void Validate()
        {
            KeplerianConicTrajectoryValidation.Validate();
            foreach (var eccentricity in new[] { 0.0, 0.5, 1.5 })
            {
                var conic = new KeplerianConicTrajectory("parent", 384400000, eccentricity,
                    125, 37, 23, 71, 19, CelestialOrbitDirection.Retrograde);
                var wrapped = new CelestialTrajectoryDefinition(conic);
                var restored = JsonUtility.FromJson<CelestialTrajectoryDefinition>(JsonUtility.ToJson(wrapped));
                Require(restored.TryValidate(out var error), error);
                Require(restored.Kind == CelestialTrajectoryKind.KeplerianConic &&
                    restored.ReferenceInstanceId == "parent", "Conic kind/reference did not survive serialization.");
                foreach (var time in new[] { -100000.0, 125.0, 345678.0 })
                {
                    Require(CelestialTrajectoryEvaluator.TryEvaluateRelativeState(restored, 5.9722e24,
                        7.342e22, time, out var p, out var v, out error), error);
                    Require(KeplerianConicTrajectoryEvaluator.TryEvaluateRelativeState(conic, 5.9722e24,
                        7.342e22, time, out var cp, out var cv, out error), error);
                    Require((p - cp).Magnitude < 1e-5 && (v - cv).Magnitude < 1e-9,
                        "Shared evaluator differs from direct conic evaluation.");
                }
                var closed = CelestialTrajectoryEvaluator.TryCalculatePeriodSeconds(restored,
                    5.9722e24, 7.342e22, out var period, out error);
                Require(closed == (eccentricity < 1), "Incorrect conic period dispatch.");
                if (closed) Require(period > 0, "Invalid closed-orbit period.");
            }
            var legacy = JsonUtility.FromJson<CelestialTrajectoryDefinition>(
                "{\"kind\":0,\"referenceInstanceId\":\"parent\",\"orbitalRadiusMeters\":384400000," +
                "\"epochUniversalTimeSeconds\":0,\"phaseAtEpochDegrees\":0,\"inclinationDegrees\":0," +
                "\"longitudeOfAscendingNodeDegrees\":0,\"direction\":1}");
            Require(legacy.TryValidate(out var legacyError), legacyError);
            Require(CelestialTrajectoryEvaluator.TryEvaluateRelativeState(legacy, 5.9722e24,
                7.342e22, 0, out var lp, out _, out legacyError), legacyError);
            Require((lp - new DoubleVector3(384400000, 0, 0)).Magnitude < 1e-5,
                "Legacy serialized circular orbit changed.");
            Require(!new CelestialTrajectoryDefinition((KeplerianConicTrajectory)null).TryValidate(out _),
                "Missing conic settings must fail validation.");
            Debug.Log("Conic trajectory integration PASS. Shared evaluator dispatch, conic serialization, " +
                "reference IDs, period dispatch, and legacy circular data passed.");
        }

        private static void Require(bool condition, string error)
        {
            if (!condition) throw new InvalidOperationException("Conic integration validation failed: " + error);
        }
    }
}
