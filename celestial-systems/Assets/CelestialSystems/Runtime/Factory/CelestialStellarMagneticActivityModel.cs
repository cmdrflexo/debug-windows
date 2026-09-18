/*
 * Produces deterministic, game-facing stellar magnetic activity and visible-beam properties.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialStellarMagneticActivity
    {
        None = 0,
        Quiet = 1,
        Active = 2,
        Strong = 3,
        Pulsar = 4
    }

    public readonly struct CelestialStellarMagneticActivityResult
    {
        public CelestialStellarMagneticActivityResult(
            CelestialStellarMagneticActivity activity,
            bool hasVisibleBeam,
            double magneticAxisTiltDegrees,
            double beamStrength)
        {
            Activity = activity;
            HasVisibleBeam = hasVisibleBeam;
            MagneticAxisTiltDegrees = magneticAxisTiltDegrees;
            BeamStrength = beamStrength;
        }

        public CelestialStellarMagneticActivity Activity { get; }
        public bool HasVisibleBeam { get; }
        public double MagneticAxisTiltDegrees { get; }
        public double BeamStrength { get; }
    }

    public static class CelestialStellarMagneticActivityModel
    {
        public const int ModelVersion = 1;

        public static bool TryEvaluate(
            int seed,
            CelestialStellarEvolutionResult stellar,
            CelestialBodyRotationResult rotation,
            out CelestialStellarMagneticActivityResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(stellar.CurrentMassSolar) ||
                !IsFinitePositive(rotation.RotationPeriodHours) ||
                !IsFiniteInRange(rotation.AxialTiltDegrees, 0.0, 180.0))
            {
                error = "Stellar magnetic activity requires valid stellar evolution and rotation properties.";
                return false;
            }

            var random = new DeterministicRandom(seed);
            switch (stellar.EvolutionState)
            {
                case CelestialStellarEvolutionState.BlackHole:
                    result = new CelestialStellarMagneticActivityResult(
                        CelestialStellarMagneticActivity.None,
                        false,
                        0.0,
                        0.0);
                    return true;

                case CelestialStellarEvolutionState.NeutronStar:
                    return CreateNeutronStarResult(ref random, out result);

                case CelestialStellarEvolutionState.WhiteDwarf:
                    return CreateWhiteDwarfResult(ref random, out result);

                case CelestialStellarEvolutionState.MainSequence:
                    return CreateMainSequenceResult(
                        ref random,
                        stellar,
                        rotation,
                        out result);

                default:
                    error = "Unsupported stellar evolution state for magnetic activity.";
                    return false;
            }
        }

        private static bool CreateNeutronStarResult(
            ref DeterministicRandom random,
            out CelestialStellarMagneticActivityResult result)
        {
            var beam = random.Next01() < 0.72;
            result = new CelestialStellarMagneticActivityResult(
                beam
                    ? CelestialStellarMagneticActivity.Pulsar
                    : CelestialStellarMagneticActivity.Strong,
                beam,
                beam ? random.NextRange(5.0, 55.0) : 0.0,
                beam ? random.NextRange(0.75, 1.0) : 0.0);
            return true;
        }

        private static bool CreateWhiteDwarfResult(
            ref DeterministicRandom random,
            out CelestialStellarMagneticActivityResult result)
        {
            var beam = random.Next01() < 0.14;
            result = new CelestialStellarMagneticActivityResult(
                beam
                    ? CelestialStellarMagneticActivity.Strong
                    : CelestialStellarMagneticActivity.Quiet,
                beam,
                beam ? random.NextRange(5.0, 65.0) : 0.0,
                beam ? random.NextRange(0.45, 0.85) : 0.0);
            return true;
        }

        private static bool CreateMainSequenceResult(
            ref DeterministicRandom random,
            CelestialStellarEvolutionResult stellar,
            CelestialBodyRotationResult rotation,
            out CelestialStellarMagneticActivityResult result)
        {
            var turnoverDays = stellar.CurrentMassSolar < 0.35 ? 100.0 :
                stellar.CurrentMassSolar < 0.7 ? 55.0 :
                stellar.CurrentMassSolar < 1.1 ? 20.0 : 6.0;
            var rossby = rotation.RotationPeriodHours / 24.0 / turnoverDays;
            var activityScore = Clamp01((0.8 - rossby) / 0.7);
            var activity = activityScore < 0.15
                ? CelestialStellarMagneticActivity.Quiet
                : activityScore < 0.60
                    ? CelestialStellarMagneticActivity.Active
                    : CelestialStellarMagneticActivity.Strong;
            var baseChance = stellar.CurrentMassSolar < 0.7 ? 0.30 : 0.12;
            var beamChance = activity == CelestialStellarMagneticActivity.Strong
                ? baseChance * activityScore
                : 0.0;
            var beam = random.Next01() < beamChance;
            result = new CelestialStellarMagneticActivityResult(
                activity,
                beam,
                beam ? random.NextRange(4.0, 55.0) : 0.0,
                beam ? random.NextRange(0.20, 0.70) : 0.0);
            return true;
        }

        private static double Clamp01(double value) => Math.Max(0.0, Math.Min(1.0, value));
        private static bool IsFinitePositive(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        private static bool IsFiniteInRange(double value, double minimum, double maximum) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;

        private struct DeterministicRandom
        {
            private uint state;
            public DeterministicRandom(int seed) { var value = unchecked((uint)seed) + 0x9E3779B9u; value ^= value >> 16; value *= 0x85EBCA6Bu; value ^= value >> 13; state = value == 0 ? 0x6D2B79F5u : value; }
            public double NextRange(double minimum, double maximum) => minimum + (maximum - minimum) * Next01();
            public double Next01() => (NextUInt() >> 8) * (1.0 / 16777216.0);
            private uint NextUInt() { var value = state; value ^= value << 13; value ^= value >> 17; value ^= value << 5; state = value; return value; }
        }
    }
}
