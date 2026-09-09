/*
 * Evaluates double-precision position and velocity for prescribed celestial trajectories at universal time.
 */

using System;

namespace jcan.CelestialSystems
{
    public static class CelestialTrajectoryEvaluator
    {
        public const double GravitationalConstant =
            6.67430e-11;

        public static bool TryEvaluateRelativeState(
            CelestialTrajectoryDefinition definition,
            double referenceMassKilograms,
            double orbitingMassKilograms,
            double universalTimeSeconds,
            out DoubleVector3 positionMeters,
            out DoubleVector3 velocityMetersPerSecond,
            out string error)
        {
            positionMeters =
                new DoubleVector3();
            velocityMetersPerSecond =
                new DoubleVector3();

            if (definition == null)
            {
                error =
                    "A celestial trajectory definition is required for evaluation.";
                return false;
            }

            if (!definition.TryValidate(
                    out error))
            {
                return false;
            }

            if (!IsFinite(
                    referenceMassKilograms) ||
                referenceMassKilograms <= 0.0 ||
                !IsFinite(
                    orbitingMassKilograms) ||
                orbitingMassKilograms <= 0.0)
            {
                error =
                    "Circular trajectory evaluation requires finite positive reference and orbiting masses.";
                return false;
            }

            if (!IsFinite(
                    universalTimeSeconds))
            {
                error =
                    "Circular trajectory evaluation requires a finite universal time.";
                return false;
            }

            var gravitationalParameter =
                GravitationalConstant *
                (referenceMassKilograms +
                    orbitingMassKilograms);
            var radius =
                definition.OrbitalRadiusMeters;
            var angularSpeed =
                Math.Sqrt(
                    gravitationalParameter /
                    (radius *
                        radius *
                        radius));
            var direction =
                definition.Direction ==
                    CelestialOrbitDirection.Retrograde
                        ? -1.0
                        : 1.0;
            var elapsedSeconds =
                universalTimeSeconds -
                definition.EpochUniversalTimeSeconds;
            var phaseRadians =
                definition.PhaseAtEpochDegrees *
                Math.PI /
                180.0 +
                direction *
                angularSpeed *
                elapsedSeconds;
            var inclinationRadians =
                definition.InclinationDegrees *
                Math.PI /
                180.0;
            var ascendingNodeRadians =
                definition.LongitudeOfAscendingNodeDegrees *
                Math.PI /
                180.0;
            var cosineNode =
                Math.Cos(
                    ascendingNodeRadians);
            var sineNode =
                Math.Sin(
                    ascendingNodeRadians);
            var cosineInclination =
                Math.Cos(
                    inclinationRadians);
            var sineInclination =
                Math.Sin(
                    inclinationRadians);
            var phaseZeroDirection =
                new DoubleVector3(
                    cosineNode,
                    0.0,
                    -sineNode);
            var phaseQuarterDirection =
                new DoubleVector3(
                    sineNode *
                        cosineInclination,
                    sineInclination,
                    cosineNode *
                        cosineInclination);
            var cosinePhase =
                Math.Cos(
                    phaseRadians);
            var sinePhase =
                Math.Sin(
                    phaseRadians);
            var tangent =
                phaseZeroDirection *
                    -sinePhase +
                phaseQuarterDirection *
                    cosinePhase;

            positionMeters =
                (phaseZeroDirection *
                    cosinePhase +
                phaseQuarterDirection *
                    sinePhase) *
                radius;
            velocityMetersPerSecond =
                tangent *
                (direction *
                    angularSpeed *
                    radius);
            error = string.Empty;
            return true;
        }

        public static bool TryCalculatePeriodSeconds(
            CelestialTrajectoryDefinition definition,
            double referenceMassKilograms,
            double orbitingMassKilograms,
            out double periodSeconds,
            out string error)
        {
            periodSeconds = 0.0;
            error = string.Empty;

            if (definition == null ||
                !definition.TryValidate(
                    out error))
            {
                if (definition == null)
                {
                    error =
                        "A celestial trajectory definition is required for period calculation.";
                }

                return false;
            }

            if (!IsFinite(
                    referenceMassKilograms) ||
                referenceMassKilograms <= 0.0 ||
                !IsFinite(
                    orbitingMassKilograms) ||
                orbitingMassKilograms <= 0.0)
            {
                error =
                    "Circular trajectory period calculation requires finite positive reference and orbiting masses.";
                return false;
            }

            var gravitationalParameter =
                GravitationalConstant *
                (referenceMassKilograms +
                    orbitingMassKilograms);
            var radius =
                definition.OrbitalRadiusMeters;

            periodSeconds =
                Math.PI *
                2.0 *
                Math.Sqrt(
                    radius *
                    radius *
                    radius /
                    gravitationalParameter);
            error = string.Empty;
            return true;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
