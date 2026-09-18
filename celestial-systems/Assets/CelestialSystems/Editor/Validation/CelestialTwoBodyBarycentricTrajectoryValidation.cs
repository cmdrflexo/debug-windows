/*
 * Validates generic two-body barycentric component trajectories independently of body classification.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialTwoBodyBarycentricTrajectoryValidation
    {
        private const double AstronomicalUnitMeters = 1.495978707e11;
        private const double EarthMassKilograms = 5.9722e24;

        [MenuItem("Tools/Celestial Systems/Validate Two-Body Barycentric Trajectories")]
        public static void Validate()
        {
            if (!TryCase(
                    "equal circular",
                    EarthMassKilograms,
                    EarthMassKilograms,
                    0.01,
                    0.0,
                    0.0,
                    CelestialOrbitDirection.Prograde,
                    out var error) ||
                !TryCase(
                    "extreme ratio",
                    EarthMassKilograms * 1000000.0,
                    EarthMassKilograms,
                    2.0,
                    0.72,
                    63.0,
                    CelestialOrbitDirection.Retrograde,
                    out error) ||
                !TryCase(
                    "arbitrary eccentric",
                    EarthMassKilograms * 7.3,
                    EarthMassKilograms * 2.1,
                    0.4,
                    0.43,
                    127.0,
                    CelestialOrbitDirection.Prograde,
                    out error))
            {
                Debug.LogError(
                    error);
                return;
            }

            Debug.Log(
                "Two-body barycentric trajectory PASS. Shared relative motion, mass partition, fixed and moving barycenters, equal and extreme mass ratios, inclination, retrograde motion, shared period, and deterministic rewind passed.");
        }

        private static bool TryCase(
            string label,
            double massA,
            double massB,
            double periapsisAstronomicalUnits,
            double eccentricity,
            double inclinationDegrees,
            CelestialOrbitDirection direction,
            out string error)
        {
            var relative =
                new KeplerianConicTrajectory(
                    "test-barycenter",
                    periapsisAstronomicalUnits *
                        AstronomicalUnitMeters,
                    eccentricity,
                    1234.5,
                    37.0,
                    inclinationDegrees,
                    19.0,
                    71.0,
                    direction);
            var pair =
                new CelestialTwoBodyBarycentricOrbit(
                    label,
                    "test-barycenter",
                    massA,
                    massB,
                    relative);
            var componentA =
                new CelestialTrajectoryDefinition(
                    pair,
                    CelestialTwoBodyComponent.BodyA);
            var componentB =
                new CelestialTrajectoryDefinition(
                    pair,
                    CelestialTwoBodyComponent.BodyB);

            if (!componentA.TryValidate(
                    out error) ||
                !componentB.TryValidate(
                    out error) ||
                !CelestialTrajectoryEvaluator.TryCalculatePeriodSeconds(
                    componentA,
                    pair.TotalMassKilograms,
                    massA,
                    out var periodA,
                    out error) ||
                !CelestialTrajectoryEvaluator.TryCalculatePeriodSeconds(
                    componentB,
                    pair.TotalMassKilograms,
                    massB,
                    out var periodB,
                    out error) ||
                !NearlyEqual(
                    periodA,
                    periodB,
                    1.0e-12))
            {
                error =
                    $"{label}: components did not share one valid period. {error}";
                return false;
            }

            var parentPositionAtEpoch =
                new DoubleVector3(
                    4.0e14,
                    -2.0e14,
                    7.0e14);
            var parentVelocity =
                new DoubleVector3(
                    1700.0,
                    -900.0,
                    320.0);
            DoubleVector3 rewindA = default;
            DoubleVector3 rewindB = default;

            for (var index = 0;
                index <= 16;
                index++)
            {
                var time =
                    relative.EpochUniversalTimeSeconds +
                    periodA *
                    index /
                    16.0;

                if (!TryEvaluate(
                        componentA,
                        componentB,
                        pair,
                        massA,
                        massB,
                        time,
                        parentPositionAtEpoch,
                        parentVelocity,
                        out var absoluteA,
                        out var absoluteB,
                        out error))
                {
                    error =
                        $"{label}: {error}";
                    return false;
                }

                if (index == 0)
                {
                    rewindA =
                        absoluteA;
                    rewindB =
                        absoluteB;
                }
            }

            if (!TryEvaluate(
                    componentA,
                    componentB,
                    pair,
                    massA,
                    massB,
                    relative.EpochUniversalTimeSeconds,
                    parentPositionAtEpoch,
                    parentVelocity,
                    out var resetA,
                    out var resetB,
                    out error) ||
                (resetA - rewindA).Magnitude > 1.0e-6 ||
                (resetB - rewindB).Magnitude > 1.0e-6)
            {
                error =
                    $"{label}: resetting time did not reproduce both component positions.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryEvaluate(
            CelestialTrajectoryDefinition componentA,
            CelestialTrajectoryDefinition componentB,
            CelestialTwoBodyBarycentricOrbit pair,
            double massA,
            double massB,
            double time,
            DoubleVector3 parentPositionAtEpoch,
            DoubleVector3 parentVelocity,
            out DoubleVector3 absoluteA,
            out DoubleVector3 absoluteB,
            out string error)
        {
            absoluteA = default;
            absoluteB = default;

            if (!CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                    componentA,
                    pair.TotalMassKilograms,
                    massA,
                    time,
                    out var positionA,
                    out var velocityA,
                    out error) ||
                !CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                    componentB,
                    pair.TotalMassKilograms,
                    massB,
                    time,
                    out var positionB,
                    out var velocityB,
                    out error) ||
                !KeplerianConicTrajectoryEvaluator.TryEvaluateRelativeState(
                    pair.RelativeTrajectory,
                    massA,
                    massB,
                    time,
                    out var expectedSeparation,
                    out var expectedRelativeVelocity,
                    out error))
            {
                return false;
            }

            var elapsed =
                time -
                pair.RelativeTrajectory.EpochUniversalTimeSeconds;
            var barycenterPosition =
                parentPositionAtEpoch +
                parentVelocity *
                    elapsed;
            absoluteA =
                barycenterPosition +
                positionA;
            absoluteB =
                barycenterPosition +
                positionB;

            var reconstructedBarycenter =
                (absoluteA * massA +
                    absoluteB * massB) /
                pair.TotalMassKilograms;
            var reconstructedVelocity =
                (velocityA * massA +
                    velocityB * massB) /
                pair.TotalMassKilograms +
                parentVelocity;
            var separationError =
                ((positionB - positionA) -
                    expectedSeparation).Magnitude;
            var velocityError =
                ((velocityB - velocityA) -
                    expectedRelativeVelocity).Magnitude;
            var distanceScale =
                Math.Max(
                    1.0,
                    expectedSeparation.Magnitude);
            var velocityScale =
                Math.Max(
                    1.0,
                    expectedRelativeVelocity.Magnitude);

            if ((reconstructedBarycenter -
                    barycenterPosition).Magnitude >
                        distanceScale * 1.0e-11 ||
                (reconstructedVelocity -
                    parentVelocity).Magnitude >
                        velocityScale * 1.0e-11 ||
                separationError >
                    distanceScale * 1.0e-12 ||
                velocityError >
                    velocityScale * 1.0e-12)
            {
                error =
                    "The component states did not preserve their relative orbit or barycenter.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool NearlyEqual(
            double first,
            double second,
            double relativeTolerance)
        {
            return
                Math.Abs(first - second) <=
                Math.Max(
                    1.0,
                    Math.Max(
                        Math.Abs(first),
                        Math.Abs(second))) *
                relativeTolerance;
        }
    }
}
