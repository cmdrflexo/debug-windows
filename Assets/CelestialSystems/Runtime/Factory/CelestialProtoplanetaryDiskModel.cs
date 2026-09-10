/*
 * Produces a deterministic, approximate model of the disk from which a planetary system formed.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialPlanetFormationOutlook
    {
        Suppressed = 0,
        Limited = 1,
        Favorable = 2
    }

    public readonly struct CelestialProtoplanetaryDiskResult
    {
        public CelestialProtoplanetaryDiskResult(
            double initialGasMassSolar,
            double initialSolidMassEarth,
            double innerBoundaryAstronomicalUnits,
            double outerBoundaryAstronomicalUnits,
            double frostLineAstronomicalUnits,
            double planetFormationWindowMegayears,
            double environmentalRetentionFraction,
            CelestialPlanetFormationOutlook formationOutlook)
        {
            InitialGasMassSolar = initialGasMassSolar;
            InitialSolidMassEarth = initialSolidMassEarth;
            InnerBoundaryAstronomicalUnits =
                innerBoundaryAstronomicalUnits;
            OuterBoundaryAstronomicalUnits =
                outerBoundaryAstronomicalUnits;
            FrostLineAstronomicalUnits =
                frostLineAstronomicalUnits;
            PlanetFormationWindowMegayears =
                planetFormationWindowMegayears;
            EnvironmentalRetentionFraction =
                environmentalRetentionFraction;
            FormationOutlook = formationOutlook;
        }

        public double InitialGasMassSolar { get; }

        public double InitialSolidMassEarth { get; }

        public double InnerBoundaryAstronomicalUnits { get; }

        public double OuterBoundaryAstronomicalUnits { get; }

        public double FrostLineAstronomicalUnits { get; }

        public double PlanetFormationWindowMegayears { get; }

        public double EnvironmentalRetentionFraction { get; }

        public CelestialPlanetFormationOutlook FormationOutlook { get; }
    }

    public static class CelestialProtoplanetaryDiskModel
    {
        public const int ModelVersion = 1;

        private const double EarthMassesPerSolarMass = 332946.0;

        public static bool TryGenerate(
            int seed,
            CelestialStellarPopulationSample population,
            CelestialStellarEvolutionResult evolution,
            CelestialGalacticEnvironmentDefinition environment,
            out CelestialProtoplanetaryDiskResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (environment == null ||
                !environment.TryValidate(
                    out error))
            {
                if (environment == null)
                {
                    error =
                        "Protoplanetary-disk generation requires a galactic environment.";
                }

                return false;
            }

            if (!IsFinitePositive(
                    population.InitialMassSolar) ||
                !IsFinitePositive(
                    evolution.InitialMassSolar))
            {
                error =
                    "Protoplanetary-disk generation requires valid stellar formation properties.";
                return false;
            }

            var random =
                new DeterministicRandom(
                    seed);
            var hostMassSolar =
                population.InitialMassSolar;
            var diskMassScatterDex =
                random.NextRange(
                    -0.7,
                    0.7);
            var diskToStarMassRatio =
                Clamp(
                    0.005 *
                    Math.Pow(
                        10.0,
                        diskMassScatterDex),
                    0.001,
                    0.1);
            var unperturbedGasMassSolar =
                hostMassSolar *
                diskToStarMassRatio;
            var radiationPenalty =
                1.0 /
                (1.0 +
                    0.22 *
                    Math.Log10(
                        Math.Max(
                            1.0,
                            environment.BirthFarUltravioletFieldG0)));
            var densityPenalty =
                1.0 /
                (1.0 +
                    0.08 *
                    Math.Log10(
                        Math.Max(
                            1.0,
                            environment.BirthStellarDensityPerCubicParsec)));
            var retention =
                Clamp(
                    radiationPenalty *
                    densityPenalty,
                    0.1,
                    1.0);
            var gasMassSolar =
                unperturbedGasMassSolar *
                retention;
            var refractoryScaling =
                Math.Pow(
                    10.0,
                    environment.IronMetallicityDex);
            var alphaScaling =
                Math.Pow(
                    10.0,
                    Math.Max(
                        0.0,
                        environment.AlphaEnhancementDex) *
                    0.35);
            var solidFraction =
                Clamp(
                    0.01 *
                    refractoryScaling *
                    alphaScaling,
                    0.0001,
                    0.05);
            var solidMassEarth =
                gasMassSolar *
                EarthMassesPerSolarMass *
                solidFraction;
            var formationLuminositySolar =
                EstimateFormationLuminositySolar(
                    hostMassSolar);
            var frostLineAu =
                2.7 *
                Math.Sqrt(
                    formationLuminositySolar);
            var innerBoundaryAu =
                Math.Max(
                    0.01,
                    0.04 *
                    Math.Sqrt(
                        formationLuminositySolar));
            var unperturbedOuterBoundaryAu =
                100.0 *
                Math.Pow(
                    hostMassSolar,
                    0.3) *
                random.NextRange(
                    0.55,
                    1.65);
            var outerBoundaryAu =
                Math.Max(
                    innerBoundaryAu *
                        2.0,
                    unperturbedOuterBoundaryAu *
                        retention);
            var stellarLifetimeFactor =
                hostMassSolar > 1.3
                    ? 1.0 /
                        Math.Sqrt(
                            hostMassSolar /
                            1.3)
                    : 1.0;
            var formationWindowMegayears =
                Clamp(
                    2.5 *
                    stellarLifetimeFactor *
                    Math.Sqrt(
                        retention) *
                    random.NextRange(
                        0.65,
                        1.55),
                    0.25,
                    10.0);
            var outlook =
                ResolveOutlook(
                    solidMassEarth,
                    outerBoundaryAu,
                    formationWindowMegayears);

            result =
                new CelestialProtoplanetaryDiskResult(
                    gasMassSolar,
                    solidMassEarth,
                    innerBoundaryAu,
                    outerBoundaryAu,
                    frostLineAu,
                    formationWindowMegayears,
                    retention,
                    outlook);

            if (!HasValidResult(
                    result))
            {
                error =
                    "The protoplanetary-disk model produced invalid formation properties.";
                result = default;
                return false;
            }

            return true;
        }

        private static CelestialPlanetFormationOutlook ResolveOutlook(
            double solidMassEarth,
            double outerBoundaryAu,
            double formationWindowMegayears)
        {
            if (solidMassEarth < 1.0 ||
                outerBoundaryAu < 1.0 ||
                formationWindowMegayears < 0.75)
            {
                return
                    CelestialPlanetFormationOutlook.Suppressed;
            }

            if (solidMassEarth < 10.0 ||
                formationWindowMegayears < 1.5)
            {
                return
                    CelestialPlanetFormationOutlook.Limited;
            }

            return
                CelestialPlanetFormationOutlook.Favorable;
        }

        private static double EstimateFormationLuminositySolar(
            double massSolar)
        {
            if (massSolar < 0.43)
            {
                return
                    0.23 *
                    Math.Pow(
                        massSolar,
                        2.3);
            }

            if (massSolar < 2.0)
            {
                return
                    Math.Pow(
                        massSolar,
                        4.0);
            }

            if (massSolar < 55.0)
            {
                return
                    1.4 *
                    Math.Pow(
                        massSolar,
                        3.5);
            }

            return
                32000.0 *
                massSolar;
        }

        private static bool HasValidResult(
            CelestialProtoplanetaryDiskResult result)
        {
            return
                IsFinitePositive(
                    result.InitialGasMassSolar) &&
                IsFiniteNonNegative(
                    result.InitialSolidMassEarth) &&
                IsFinitePositive(
                    result.InnerBoundaryAstronomicalUnits) &&
                IsFinitePositive(
                    result.OuterBoundaryAstronomicalUnits) &&
                result.OuterBoundaryAstronomicalUnits >
                    result.InnerBoundaryAstronomicalUnits &&
                IsFinitePositive(
                    result.FrostLineAstronomicalUnits) &&
                IsFinitePositive(
                    result.PlanetFormationWindowMegayears) &&
                IsFinitePositive(
                    result.EnvironmentalRetentionFraction) &&
                result.EnvironmentalRetentionFraction <= 1.0;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return
                Math.Max(
                    minimum,
                    Math.Min(
                        maximum,
                        value));
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                IsFinite(value) &&
                value > 0.0;
        }

        private static bool IsFiniteNonNegative(
            double value)
        {
            return
                IsFinite(value) &&
                value >= 0.0;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(
                int seed)
            {
                var value =
                    unchecked((uint)seed) +
                    0x9E3779B9u;
                value ^=
                    value >> 16;
                value *=
                    0x85EBCA6Bu;
                value ^=
                    value >> 13;
                value *=
                    0xC2B2AE35u;
                value ^=
                    value >> 16;
                state =
                    value == 0
                        ? 0x6D2B79F5u
                        : value;
            }

            public double NextRange(
                double minimum,
                double maximum)
            {
                return
                    minimum +
                    (maximum - minimum) *
                    Next01();
            }

            private double Next01()
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
