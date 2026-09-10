using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    /// <summary>Two-body conic relative to a reference body. SI units; angles in degrees.
    /// Direction reverses anomaly progression, matching the circular trajectory convention.
    /// Hyperbolic mean anomaly is unbounded and must not be wrapped.</summary>
    [Serializable]
    public sealed class KeplerianConicTrajectory
    {
        [SerializeField] private string referenceInstanceId;
        [SerializeField] private double periapsisDistanceMeters = 1000000.0;
        [SerializeField] private double eccentricity;
        [SerializeField] private double epochUniversalTimeSeconds;
        [SerializeField] private double meanAnomalyAtEpochDegrees;
        [SerializeField] private double inclinationDegrees;
        [SerializeField] private double longitudeOfAscendingNodeDegrees;
        [SerializeField] private double argumentOfPeriapsisDegrees;
        [SerializeField] private CelestialOrbitDirection direction = CelestialOrbitDirection.Prograde;

        public KeplerianConicTrajectory(string referenceInstanceId, double periapsisDistanceMeters,
            double eccentricity, double epochUniversalTimeSeconds, double meanAnomalyAtEpochDegrees,
            double inclinationDegrees, double longitudeOfAscendingNodeDegrees,
            double argumentOfPeriapsisDegrees, CelestialOrbitDirection direction)
        {
            this.referenceInstanceId = referenceInstanceId;
            this.periapsisDistanceMeters = periapsisDistanceMeters;
            this.eccentricity = eccentricity;
            this.epochUniversalTimeSeconds = epochUniversalTimeSeconds;
            this.meanAnomalyAtEpochDegrees = meanAnomalyAtEpochDegrees;
            this.inclinationDegrees = inclinationDegrees;
            this.longitudeOfAscendingNodeDegrees = longitudeOfAscendingNodeDegrees;
            this.argumentOfPeriapsisDegrees = argumentOfPeriapsisDegrees;
            this.direction = direction;
        }

        public string ReferenceInstanceId => referenceInstanceId;
        public double PeriapsisDistanceMeters => periapsisDistanceMeters;
        public double Eccentricity => eccentricity;
        public double EpochUniversalTimeSeconds => epochUniversalTimeSeconds;
        public double MeanAnomalyAtEpochDegrees => meanAnomalyAtEpochDegrees;
        public double InclinationDegrees => inclinationDegrees;
        public double LongitudeOfAscendingNodeDegrees => longitudeOfAscendingNodeDegrees;
        public double ArgumentOfPeriapsisDegrees => argumentOfPeriapsisDegrees;
        public CelestialOrbitDirection Direction => direction;
        public bool IsClosed => eccentricity < 1.0;
        /// <summary>Negative for hyperbolas; undefined for the unsupported parabola.</summary>
        public double SemiMajorAxisMeters => periapsisDistanceMeters / (1.0 - eccentricity);

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(referenceInstanceId))
                error = "A conic trajectory requires a reference instance ID.";
            else if (!Finite(periapsisDistanceMeters) || periapsisDistanceMeters <= 0.0)
                error = "Periapsis distance must be finite and positive.";
            else if (!Finite(eccentricity) || eccentricity < 0.0 || eccentricity == 1.0)
                error = "Eccentricity must be finite and nonnegative; parabolic trajectories (e = 1) are not yet supported.";
            else if (!Finite(epochUniversalTimeSeconds) || !Finite(meanAnomalyAtEpochDegrees) ||
                !Finite(inclinationDegrees) || !Finite(longitudeOfAscendingNodeDegrees) ||
                !Finite(argumentOfPeriapsisDegrees))
                error = "Conic epoch and angles must be finite.";
            else if (inclinationDegrees < 0.0 || inclinationDegrees > 180.0)
                error = "Inclination must be between 0 and 180 degrees.";
            else if (direction != CelestialOrbitDirection.Prograde && direction != CelestialOrbitDirection.Retrograde)
                error = "Invalid conic direction.";
            return error.Length == 0;
        }

        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
