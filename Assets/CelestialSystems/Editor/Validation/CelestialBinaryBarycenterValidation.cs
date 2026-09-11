/*
 * Validates mass-weighted binary trajectories around a shared center of mass.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialBinaryBarycenterValidation
    {
        private const double SolarMassKilograms = 1.98847e30;
        private const double AstronomicalUnitMeters = 149597870700.0;

        [MenuItem("Tools/Celestial Systems/Validate Binary Barycenter Model")]
        public static void Validate()
        {
            var primaryMass =
                1.0 *
                SolarMassKilograms;
            var secondaryMass =
                0.5 *
                SolarMassKilograms;

            if (!CelestialBinaryBarycenterModel.TryResolve(
                    primaryMass,
                    secondaryMass,
                    3.0 * AstronomicalUnitMeters,
                    0.25,
                    out var binary,
                    out var error) ||
                !CelestialBinaryBarycenterModel.TryResolve(
                    primaryMass,
                    secondaryMass,
                    3.0 * AstronomicalUnitMeters,
                    0.25,
                    out var repeated,
                    out error))
            {
                Debug.LogError(error);
                return;
            }

            if (binary.PrimarySemiMajorAxisMeters !=
                    repeated.PrimarySemiMajorAxisMeters ||
                binary.SecondarySemiMajorAxisMeters !=
                    repeated.SecondarySemiMajorAxisMeters ||
                binary.OrbitalPeriodSeconds !=
                    repeated.OrbitalPeriodSeconds ||
                Math.Abs(
                    binary.RelativeSemiMajorAxisMeters -
                    3.0 * AstronomicalUnitMeters) >
                        0.001)
            {
                Debug.LogError(
                    "Binary barycenter validation failed: deterministic axis partitioning was inconsistent.");
                return;
            }

            if (!ValidateApsis(
                    binary,
                    primaryMass,
                    secondaryMass,
                    false) ||
                !ValidateApsis(
                    binary,
                    primaryMass,
                    secondaryMass,
                    true))
            {
                Debug.LogError(
                    "Binary barycenter validation failed: the mass-weighted center moved away from the origin.");
                return;
            }

            var expectedPrimaryAxisAu = 1.0;
            var expectedSecondaryAxisAu = 2.0;
            var primaryAxisAu =
                binary.PrimarySemiMajorAxisMeters /
                AstronomicalUnitMeters;
            var secondaryAxisAu =
                binary.SecondarySemiMajorAxisMeters /
                AstronomicalUnitMeters;

            if (Math.Abs(primaryAxisAu - expectedPrimaryAxisAu) > 1.0e-9 ||
                Math.Abs(secondaryAxisAu - expectedSecondaryAxisAu) > 1.0e-9)
            {
                Debug.LogError(
                    "Binary barycenter validation failed: unequal stellar masses did not receive inverse-distance orbital axes.");
                return;
            }

            Debug.Log(
                $"Binary barycenter PASS. Model v{CelestialBinaryBarycenterModel.ModelVersion}; deterministic mass partition, shared period, relative separation, and fixed center-of-mass checks passed. " +
                $"For a 1.0 + 0.5 solar-mass pair separated by 3 AU: primary axis {primaryAxisAu:0.###} AU, secondary axis {secondaryAxisAu:0.###} AU, period {binary.OrbitalPeriodSeconds / 31557600.0:0.###} years.");
        }

        private static bool ValidateApsis(
            CelestialBinaryBarycenterResult binary,
            double primaryMass,
            double secondaryMass,
            bool apoapsis)
        {
            if (!CelestialBinaryBarycenterModel.TryEvaluateApsisOffsets(
                    binary,
                    primaryMass,
                    secondaryMass,
                    apoapsis,
                    out var primaryOffset,
                    out var secondaryOffset))
            {
                return false;
            }

            var weightedCenter =
                primaryMass *
                    primaryOffset +
                secondaryMass *
                    secondaryOffset;
            var normalization =
                (primaryMass + secondaryMass) *
                binary.RelativeSemiMajorAxisMeters;

            return
                Math.Abs(
                    weightedCenter /
                    normalization) <
                1.0e-14;
        }
    }
}
