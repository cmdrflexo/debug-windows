/*
 * Stores serializable, seed-friendly trajectory data that defines a body's prescribed circular or Keplerian conic orbit.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum CelestialTrajectoryKind
    {
        CircularOrbit = 0,
        KeplerianConic = 1,
        TwoBodyBarycentricComponent = 2
    }

    public enum CelestialOrbitDirection
    {
        Prograde = 1,
        Retrograde = -1
    }

    [Serializable]
    public sealed class CelestialTrajectoryDefinition
    {
        [SerializeField]
        private CelestialTrajectoryKind kind =
            CelestialTrajectoryKind.CircularOrbit;

        [SerializeField]
        [Tooltip("Body or virtual barycenter whose evaluated state this trajectory is relative to.")]
        private string referenceInstanceId;

        [SerializeField]
        [Min(1.0f)]
        private double orbitalRadiusMeters =
            1000000.0;

        [SerializeField]
        private double epochUniversalTimeSeconds;

        [SerializeField]
        private double phaseAtEpochDegrees;

        [SerializeField]
        [Range(0.0f, 180.0f)]
        private double inclinationDegrees;

        [SerializeField]
        private double longitudeOfAscendingNodeDegrees;

        [SerializeField]
        private CelestialOrbitDirection direction =
            CelestialOrbitDirection.Prograde;

        [SerializeField]
        [Tooltip("Used only when Kind is Keplerian Conic. Its reference ID is authoritative.")]
        private KeplerianConicTrajectory conic;

        [SerializeField]
        [Tooltip("Shared relative orbit used by a generic two-body barycentric pair.")]
        private CelestialTwoBodyBarycentricOrbit twoBodyOrbit;

        [SerializeField]
        private CelestialTwoBodyComponent twoBodyComponent;

        public CelestialTrajectoryDefinition(
            KeplerianConicTrajectory conic)
        {
            kind = CelestialTrajectoryKind.KeplerianConic;
            this.conic = conic;
        }

        public CelestialTrajectoryDefinition(
            CelestialTwoBodyBarycentricOrbit twoBodyOrbit,
            CelestialTwoBodyComponent twoBodyComponent)
        {
            kind =
                CelestialTrajectoryKind.TwoBodyBarycentricComponent;
            this.twoBodyOrbit =
                twoBodyOrbit;
            this.twoBodyComponent =
                twoBodyComponent;
        }

        public KeplerianConicTrajectory Conic =>
            kind == CelestialTrajectoryKind.TwoBodyBarycentricComponent
                ? twoBodyOrbit?.RelativeTrajectory
                : conic;

        public CelestialTwoBodyBarycentricOrbit TwoBodyOrbit =>
            twoBodyOrbit;

        public CelestialTwoBodyComponent TwoBodyComponent =>
            twoBodyComponent;

        public CelestialTrajectoryDefinition(
            string referenceInstanceId,
            double orbitalRadiusMeters,
            double epochUniversalTimeSeconds,
            double phaseAtEpochDegrees,
            double inclinationDegrees,
            double longitudeOfAscendingNodeDegrees,
            CelestialOrbitDirection direction)
        {
            kind =
                CelestialTrajectoryKind.CircularOrbit;
            this.referenceInstanceId =
                referenceInstanceId;
            this.orbitalRadiusMeters =
                orbitalRadiusMeters;
            this.epochUniversalTimeSeconds =
                epochUniversalTimeSeconds;
            this.phaseAtEpochDegrees =
                phaseAtEpochDegrees;
            this.inclinationDegrees =
                inclinationDegrees;
            this.longitudeOfAscendingNodeDegrees =
                longitudeOfAscendingNodeDegrees;
            this.direction =
                direction;
        }

        public CelestialTrajectoryKind Kind =>
            kind;

        public string ReferenceInstanceId =>
            kind == CelestialTrajectoryKind.KeplerianConic
                ? conic?.ReferenceInstanceId
                : kind == CelestialTrajectoryKind.TwoBodyBarycentricComponent
                    ? twoBodyOrbit?.BarycenterReferenceInstanceId
                    : referenceInstanceId;

        /// <summary>Legacy circular radius. For conics use Conic.PeriapsisDistanceMeters.</summary>
        public double OrbitalRadiusMeters =>
            orbitalRadiusMeters;

        public double EpochUniversalTimeSeconds =>
            Conic != null
                ? Conic.EpochUniversalTimeSeconds
                : epochUniversalTimeSeconds;

        /// <summary>Legacy circular phase. For conics use Conic.MeanAnomalyAtEpochDegrees.</summary>
        public double PhaseAtEpochDegrees =>
            phaseAtEpochDegrees;

        public double InclinationDegrees =>
            Conic != null
                ? Conic.InclinationDegrees
                : inclinationDegrees;

        public double LongitudeOfAscendingNodeDegrees =>
            Conic != null
                ? Conic.LongitudeOfAscendingNodeDegrees
                : longitudeOfAscendingNodeDegrees;

        public CelestialOrbitDirection Direction =>
            Conic != null
                ? Conic.Direction
                : direction;

        public bool HasValidSettings =>
            TryValidate(
                out _);

        public bool TryValidate(
            out string error)
        {
            if (kind == CelestialTrajectoryKind.KeplerianConic)
            {
                if (conic == null)
                {
                    error = "A Keplerian conic trajectory requires conic settings.";
                    return false;
                }
                return conic.TryValidate(out error);
            }

            if (kind == CelestialTrajectoryKind.TwoBodyBarycentricComponent)
            {
                if (twoBodyOrbit == null)
                {
                    error =
                        "A two-body component trajectory requires a shared barycentric orbit.";
                    return false;
                }

                if (twoBodyComponent != CelestialTwoBodyComponent.BodyA &&
                    twoBodyComponent != CelestialTwoBodyComponent.BodyB)
                {
                    error =
                        "A two-body component trajectory requires a valid component.";
                    return false;
                }

                return twoBodyOrbit.TryValidate(
                    out error);
            }

            if (kind !=
                CelestialTrajectoryKind.CircularOrbit)
            {
                error =
                    $"Unsupported celestial trajectory kind '{kind}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    referenceInstanceId))
            {
                error =
                    "A circular celestial trajectory requires a reference body or barycenter instance ID.";
                return false;
            }

            if (!IsFinite(
                    orbitalRadiusMeters) ||
                orbitalRadiusMeters <= 0.0)
            {
                error =
                    "A circular celestial trajectory requires a finite orbital radius greater than zero.";
                return false;
            }

            if (!IsFinite(
                    epochUniversalTimeSeconds) ||
                !IsFinite(
                    phaseAtEpochDegrees) ||
                !IsFinite(
                    inclinationDegrees) ||
                !IsFinite(
                    longitudeOfAscendingNodeDegrees))
            {
                error =
                    "A circular celestial trajectory contains a non-finite orbital value.";
                return false;
            }

            if (inclinationDegrees < 0.0 ||
                inclinationDegrees > 180.0)
            {
                error =
                    "A circular celestial trajectory requires an inclination from 0 to 180 degrees.";
                return false;
            }

            if (direction !=
                    CelestialOrbitDirection.Prograde &&
                direction !=
                    CelestialOrbitDirection.Retrograde)
            {
                error =
                    "A circular celestial trajectory requires a valid orbital direction.";
                return false;
            }

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
