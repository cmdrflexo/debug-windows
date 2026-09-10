/*
 * Checks circular trajectory period, orientation, direction, and full-orbit closure without a running scene.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialTrajectoryValidation
    {
        private const double EarthMassKilograms =
            5.9722e24;

        private const double MoonMassKilograms =
            7.342e22;

        private const double EarthMoonDistanceMeters =
            384400000.0;

        [MenuItem("Tools/Celestial Systems/Validate Circular Trajectory")]
        public static void Validate()
        {
            var trajectory =
                new CelestialTrajectoryDefinition(
                    "earth",
                    EarthMoonDistanceMeters,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    CelestialOrbitDirection.Prograde);

            Require(
                CelestialTrajectoryEvaluator.TryCalculatePeriodSeconds(
                    trajectory,
                    EarthMassKilograms,
                    MoonMassKilograms,
                    out var periodSeconds,
                    out var periodError),
                periodError);
            Require(
                periodSeconds >
                    27.0 *
                    86400.0 &&
                periodSeconds <
                    28.0 *
                    86400.0,
                $"Earth-Moon-like period was outside the expected range: {periodSeconds} seconds.");

            RequireState(
                trajectory,
                0.0,
                new DoubleVector3(
                    EarthMoonDistanceMeters,
                    0.0,
                    0.0),
                new DoubleVector3(
                    0.0,
                    0.0,
                    CircularSpeed()),
                "epoch");

            RequireState(
                trajectory,
                periodSeconds *
                    0.25,
                new DoubleVector3(
                    0.0,
                    0.0,
                    EarthMoonDistanceMeters),
                new DoubleVector3(
                    -CircularSpeed(),
                    0.0,
                    0.0),
                "quarter period");

            RequireState(
                trajectory,
                periodSeconds,
                new DoubleVector3(
                    EarthMoonDistanceMeters,
                    0.0,
                    0.0),
                new DoubleVector3(
                    0.0,
                    0.0,
                    CircularSpeed()),
                "full-period closure");

            var retrograde =
                new CelestialTrajectoryDefinition(
                    "earth",
                    EarthMoonDistanceMeters,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    CelestialOrbitDirection.Retrograde);

            RequireState(
                retrograde,
                periodSeconds *
                    0.25,
                new DoubleVector3(
                    0.0,
                    0.0,
                    -EarthMoonDistanceMeters),
                new DoubleVector3(
                    -CircularSpeed(),
                    0.0,
                    0.0),
                "retrograde quarter period");

            ValidateInclinedPlane(
                periodSeconds);

            Debug.Log(
                $"Circular trajectory PASS. Period: {periodSeconds / 86400.0:F6} days; " +
                "epoch, quarter-period, full-period, retrograde, and inclined-plane checks passed.");
        }

        private static void ValidateInclinedPlane(
            double periodSeconds)
        {
            var trajectory =
                new CelestialTrajectoryDefinition(
                    "earth",
                    EarthMoonDistanceMeters,
                    125.0,
                    37.0,
                    23.5,
                    71.0,
                    CelestialOrbitDirection.Prograde);

            Require(
                CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                    trajectory,
                    EarthMassKilograms,
                    MoonMassKilograms,
                    125.0 +
                        periodSeconds *
                        0.381,
                    out var position,
                    out var velocity,
                    out var error),
                error);

            RequireNear(
                Magnitude(position),
                EarthMoonDistanceMeters,
                1e-6,
                "inclined orbit radius");
            RequireNear(
                Dot(
                    position,
                    velocity),
                0.0,
                0.1,
                "inclined circular radial velocity");
        }

        private static void RequireState(
            CelestialTrajectoryDefinition trajectory,
            double universalTimeSeconds,
            DoubleVector3 expectedPosition,
            DoubleVector3 expectedVelocity,
            string description)
        {
            Require(
                CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                    trajectory,
                    EarthMassKilograms,
                    MoonMassKilograms,
                    universalTimeSeconds,
                    out var position,
                    out var velocity,
                    out var error),
                error);
            RequireNear(
                position,
                expectedPosition,
                1e-5,
                description +
                    " position");
            RequireNear(
                velocity,
                expectedVelocity,
                1e-9,
                description +
                    " velocity");
        }

        private static double CircularSpeed()
        {
            return Math.Sqrt(
                CelestialTrajectoryEvaluator.GravitationalConstant *
                (EarthMassKilograms +
                    MoonMassKilograms) /
                EarthMoonDistanceMeters);
        }

        private static double Magnitude(
            DoubleVector3 value)
        {
            return Math.Sqrt(
                Dot(
                    value,
                    value));
        }

        private static double Dot(
            DoubleVector3 left,
            DoubleVector3 right)
        {
            return
                left.x *
                    right.x +
                left.y *
                    right.y +
                left.z *
                    right.z;
        }

        private static void RequireNear(
            DoubleVector3 actual,
            DoubleVector3 expected,
            double tolerance,
            string description)
        {
            Require(
                Math.Abs(
                    actual.x -
                    expected.x) <=
                    tolerance &&
                Math.Abs(
                    actual.y -
                    expected.y) <=
                    tolerance &&
                Math.Abs(
                    actual.z -
                    expected.z) <=
                    tolerance,
                $"{description}: expected {expected}, received {actual}.");
        }

        private static void RequireNear(
            double actual,
            double expected,
            double tolerance,
            string description)
        {
            Require(
                Math.Abs(
                    actual -
                    expected) <=
                    tolerance,
                $"{description}: expected {expected}, received {actual}.");
        }

        private static void Require(
            bool condition,
            string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException(
                    "Circular trajectory validation failed: " +
                    description);
            }
        }
    }
}
