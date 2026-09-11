/*
 * Splits a relative binary-star orbit into two mass-weighted trajectories around the shared barycenter.
 */

using System;

namespace jcan.CelestialSystems
{
    public readonly struct CelestialBinaryBarycenterResult
    {
        public CelestialBinaryBarycenterResult(
            double primarySemiMajorAxisMeters,
            double secondarySemiMajorAxisMeters,
            double eccentricity,
            double orbitalPeriodSeconds)
        {
            PrimarySemiMajorAxisMeters =
                primarySemiMajorAxisMeters;
            SecondarySemiMajorAxisMeters =
                secondarySemiMajorAxisMeters;
            Eccentricity = eccentricity;
            OrbitalPeriodSeconds = orbitalPeriodSeconds;
        }

        public double PrimarySemiMajorAxisMeters { get; }
        public double SecondarySemiMajorAxisMeters { get; }
        public double Eccentricity { get; }
        public double OrbitalPeriodSeconds { get; }
        public double RelativeSemiMajorAxisMeters =>
            PrimarySemiMajorAxisMeters +
            SecondarySemiMajorAxisMeters;
    }

    public static class CelestialBinaryBarycenterModel
    {
        public const int ModelVersion = 1;

        private const double GravitationalConstant = 6.67430e-11;

        public static bool TryResolve(
            double primaryMassKilograms,
            double secondaryMassKilograms,
            double relativeSemiMajorAxisMeters,
            double eccentricity,
            out CelestialBinaryBarycenterResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(primaryMassKilograms) ||
                !IsFinitePositive(secondaryMassKilograms) ||
                !IsFinitePositive(relativeSemiMajorAxisMeters) ||
                !IsFiniteInRange(eccentricity, 0.0, 1.0) ||
                eccentricity >= 1.0)
            {
                error =
                    "A binary barycenter requires positive stellar masses and a valid closed relative orbit.";
                return false;
            }

            var totalMass =
                primaryMassKilograms +
                secondaryMassKilograms;
            var primaryAxis =
                relativeSemiMajorAxisMeters *
                secondaryMassKilograms /
                totalMass;
            var secondaryAxis =
                relativeSemiMajorAxisMeters *
                primaryMassKilograms /
                totalMass;
            var period =
                2.0 *
                Math.PI *
                Math.Sqrt(
                    Math.Pow(
                        relativeSemiMajorAxisMeters,
                        3.0) /
                    (GravitationalConstant *
                        totalMass));

            result =
                new CelestialBinaryBarycenterResult(
                    primaryAxis,
                    secondaryAxis,
                    eccentricity,
                    period);

            if (!IsFinitePositive(primaryAxis) ||
                !IsFinitePositive(secondaryAxis) ||
                !IsFinitePositive(period))
            {
                result = default;
                error =
                    "The binary barycenter model produced invalid trajectory values.";
                return false;
            }

            return true;
        }

        public static bool TryEvaluateApsisOffsets(
            CelestialBinaryBarycenterResult binary,
            double primaryMassKilograms,
            double secondaryMassKilograms,
            bool apoapsis,
            out double primaryOffsetMeters,
            out double secondaryOffsetMeters)
        {
            primaryOffsetMeters = 0.0;
            secondaryOffsetMeters = 0.0;

            if (!IsFinitePositive(primaryMassKilograms) ||
                !IsFinitePositive(secondaryMassKilograms) ||
                !IsFinitePositive(binary.PrimarySemiMajorAxisMeters) ||
                !IsFinitePositive(binary.SecondarySemiMajorAxisMeters) ||
                !IsFiniteInRange(binary.Eccentricity, 0.0, 1.0) ||
                binary.Eccentricity >= 1.0)
            {
                return false;
            }

            var apsisFactor =
                apoapsis
                    ? 1.0 + binary.Eccentricity
                    : 1.0 - binary.Eccentricity;
            primaryOffsetMeters =
                -binary.PrimarySemiMajorAxisMeters *
                apsisFactor;
            secondaryOffsetMeters =
                binary.SecondarySemiMajorAxisMeters *
                apsisFactor;
            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static bool IsFiniteInRange(
            double value,
            double minimum,
            double maximum)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= minimum &&
                value <= maximum;
        }
    }
}
