/*
 * Deterministically samples a stellar birth mass from a versioned Kroupa-like initial mass function.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialStellarEvolutionState
    {
        MainSequence = 0,
        WhiteDwarf = 1,
        NeutronStar = 2,
        BlackHole = 3
    }

    public readonly struct CelestialStellarPopulationSample
    {
        public CelestialStellarPopulationSample(
            double initialMassSolar,
            double estimatedMainSequenceLifetimeGigayears,
            CelestialStellarEvolutionState evolutionState)
        {
            InitialMassSolar = initialMassSolar;
            EstimatedMainSequenceLifetimeGigayears =
                estimatedMainSequenceLifetimeGigayears;
            EvolutionState = evolutionState;
        }

        public double InitialMassSolar { get; }

        public double EstimatedMainSequenceLifetimeGigayears { get; }

        public CelestialStellarEvolutionState EvolutionState { get; }
    }

    public static class CelestialStellarPopulationSampler
    {
        public const int ModelVersion = 1;

        private const double MinimumMassSolar = 0.08;
        private const double BreakMassSolar = 0.5;
        private const double MaximumMassSolar = 100.0;
        private const double LowMassSlope = 1.3;
        private const double HighMassSlope = 2.3;
        private const double HighMassContinuityFactor = 0.5;

        public static bool TrySamplePrimary(
            int seed,
            CelestialGalacticEnvironmentDefinition environment,
            out CelestialStellarPopulationSample sample,
            out string error)
        {
            sample = default;

            if (environment == null)
            {
                error =
                    "Realistic stellar-population sampling requires a galactic environment.";
                return false;
            }

            if (!environment.TryValidate(
                    out error))
            {
                return false;
            }

            var random =
                new DeterministicRandom(
                    seed);
            var lowWeight =
                IntegratePowerLaw(
                    MinimumMassSolar,
                    BreakMassSolar,
                    LowMassSlope);
            var highWeight =
                HighMassContinuityFactor *
                IntegratePowerLaw(
                    BreakMassSolar,
                    MaximumMassSolar,
                    HighMassSlope);
            var selection =
                random.Next01() *
                (lowWeight + highWeight);
            double initialMassSolar;

            if (selection < lowWeight)
            {
                initialMassSolar =
                    SamplePowerLaw(
                        MinimumMassSolar,
                        BreakMassSolar,
                        LowMassSlope,
                        selection /
                            lowWeight);
            }
            else
            {
                initialMassSolar =
                    SamplePowerLaw(
                        BreakMassSolar,
                        MaximumMassSolar,
                        HighMassSlope,
                        (selection - lowWeight) /
                            highWeight);
            }

            var lifetimeGigayears =
                10.0 *
                Math.Pow(
                    initialMassSolar,
                    -2.5);
            var evolutionState =
                ResolveEvolutionState(
                    initialMassSolar,
                    lifetimeGigayears,
                    environment.SystemAgeGigayears);

            sample =
                new CelestialStellarPopulationSample(
                    initialMassSolar,
                    lifetimeGigayears,
                    evolutionState);
            error = string.Empty;
            return true;
        }

        private static CelestialStellarEvolutionState ResolveEvolutionState(
            double initialMassSolar,
            double lifetimeGigayears,
            double systemAgeGigayears)
        {
            if (systemAgeGigayears <=
                lifetimeGigayears)
            {
                return
                    CelestialStellarEvolutionState.MainSequence;
            }

            if (initialMassSolar < 8.0)
            {
                return
                    CelestialStellarEvolutionState.WhiteDwarf;
            }

            if (initialMassSolar < 25.0)
            {
                return
                    CelestialStellarEvolutionState.NeutronStar;
            }

            return
                CelestialStellarEvolutionState.BlackHole;
        }

        private static double IntegratePowerLaw(
            double minimum,
            double maximum,
            double slope)
        {
            var exponent =
                1.0 - slope;
            return
                (Math.Pow(
                    maximum,
                    exponent) -
                Math.Pow(
                    minimum,
                    exponent)) /
                exponent;
        }

        private static double SamplePowerLaw(
            double minimum,
            double maximum,
            double slope,
            double fraction)
        {
            var exponent =
                1.0 - slope;
            var minimumPower =
                Math.Pow(
                    minimum,
                    exponent);
            var maximumPower =
                Math.Pow(
                    maximum,
                    exponent);
            return
                Math.Pow(
                    minimumPower +
                    (maximumPower - minimumPower) *
                    fraction,
                    1.0 /
                    exponent);
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(
                int seed)
            {
                state =
                    unchecked((uint)seed);

                if (state == 0)
                {
                    state =
                        0x6D2B79F5u;
                }
            }

            public double Next01()
            {
                return
                    (NextUInt() >> 8) *
                    (1.0 / 16777216.0);
            }

            private uint NextUInt()
            {
                var value =
                    state;
                value ^=
                    value << 13;
                value ^=
                    value >> 17;
                value ^=
                    value << 5;
                state =
                    value;
                return value;
            }
        }
    }
}
