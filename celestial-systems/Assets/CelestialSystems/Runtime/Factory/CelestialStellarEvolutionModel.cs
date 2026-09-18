/*
 * Converts a sampled stellar birth mass and environment age into approximate present-day stellar properties.
 */

using System;

namespace jcan.CelestialSystems
{
    public readonly struct CelestialStellarEvolutionResult
    {
        public CelestialStellarEvolutionResult(
            CelestialStellarEvolutionState evolutionState,
            double initialMassSolar,
            double currentMassSolar,
            double radiusSolar,
            double luminositySolar,
            double effectiveTemperatureKelvin)
        {
            EvolutionState = evolutionState;
            InitialMassSolar = initialMassSolar;
            CurrentMassSolar = currentMassSolar;
            RadiusSolar = radiusSolar;
            LuminositySolar = luminositySolar;
            EffectiveTemperatureKelvin =
                effectiveTemperatureKelvin;
        }

        public CelestialStellarEvolutionState EvolutionState { get; }

        public double InitialMassSolar { get; }

        public double CurrentMassSolar { get; }

        public double RadiusSolar { get; }

        public double LuminositySolar { get; }

        public double EffectiveTemperatureKelvin { get; }
    }

    public static class CelestialStellarEvolutionModel
    {
        public const int ModelVersion = 1;

        private const double SolarTemperatureKelvin = 5772.0;
        private const double SolarRadiusMeters = 6.957e8;
        private const double SpeedOfLightMetersPerSecond = 299792458.0;
        private const double GravitationalConstant = 6.67430e-11;
        private const double SolarMassKilograms = 1.98847e30;

        public static bool TryEvaluate(
            CelestialStellarPopulationSample populationSample,
            CelestialGalacticEnvironmentDefinition environment,
            out CelestialStellarEvolutionResult result,
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
                        "Stellar evolution requires a galactic environment.";
                }

                return false;
            }

            if (!IsFinitePositive(
                    populationSample.InitialMassSolar) ||
                !IsFinitePositive(
                    populationSample.EstimatedMainSequenceLifetimeGigayears))
            {
                error =
                    "Stellar evolution requires a valid stellar population sample.";
                return false;
            }

            switch (populationSample.EvolutionState)
            {
                case CelestialStellarEvolutionState.MainSequence:
                    result =
                        EvaluateMainSequence(
                            populationSample.InitialMassSolar);
                    break;

                case CelestialStellarEvolutionState.WhiteDwarf:
                    result =
                        EvaluateWhiteDwarf(
                            populationSample,
                            environment.SystemAgeGigayears);
                    break;

                case CelestialStellarEvolutionState.NeutronStar:
                    result =
                        EvaluateNeutronStar(
                            populationSample.InitialMassSolar);
                    break;

                case CelestialStellarEvolutionState.BlackHole:
                    result =
                        EvaluateBlackHole(
                            populationSample.InitialMassSolar);
                    break;

                default:
                    error =
                        $"Unsupported stellar evolution state '{populationSample.EvolutionState}'.";
                    return false;
            }

            if (!HasValidResult(
                    result))
            {
                error =
                    "The stellar evolution model produced invalid present-day properties.";
                result = default;
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static CelestialStellarEvolutionResult EvaluateMainSequence(
            double massSolar)
        {
            double luminositySolar;

            if (massSolar < 0.43)
            {
                luminositySolar =
                    0.23 *
                    Math.Pow(
                        massSolar,
                        2.3);
            }
            else if (massSolar < 2.0)
            {
                luminositySolar =
                    Math.Pow(
                        massSolar,
                        4.0);
            }
            else if (massSolar < 55.0)
            {
                luminositySolar =
                    1.4 *
                    Math.Pow(
                        massSolar,
                        3.5);
            }
            else
            {
                luminositySolar =
                    32000.0 *
                    massSolar;
            }

            var radiusSolar =
                massSolar < 1.0
                    ? Math.Pow(
                        massSolar,
                        0.8)
                    : Math.Pow(
                        massSolar,
                        0.57);
            var temperatureKelvin =
                CalculateTemperatureKelvin(
                    luminositySolar,
                    radiusSolar);

            return
                new CelestialStellarEvolutionResult(
                    CelestialStellarEvolutionState.MainSequence,
                    massSolar,
                    massSolar,
                    radiusSolar,
                    luminositySolar,
                    temperatureKelvin);
        }

        private static CelestialStellarEvolutionResult EvaluateWhiteDwarf(
            CelestialStellarPopulationSample sample,
            double systemAgeGigayears)
        {
            var currentMassSolar =
                Clamp(
                    0.109 *
                        sample.InitialMassSolar +
                    0.394,
                    0.5,
                    1.35);
            const double chandrasekharMassSolar = 1.44;
            var massRatio =
                currentMassSolar /
                chandrasekharMassSolar;
            var radiusSolar =
                0.0112 *
                Math.Sqrt(
                    Math.Pow(
                        massRatio,
                        -2.0 / 3.0) -
                    Math.Pow(
                        massRatio,
                        2.0 / 3.0));
            var coolingAgeGigayears =
                Math.Max(
                    0.01,
                    systemAgeGigayears -
                        sample.EstimatedMainSequenceLifetimeGigayears);
            var luminositySolar =
                0.01 /
                Math.Pow(
                    coolingAgeGigayears + 0.1,
                    1.4);
            var temperatureKelvin =
                CalculateTemperatureKelvin(
                    luminositySolar,
                    radiusSolar);

            return
                new CelestialStellarEvolutionResult(
                    CelestialStellarEvolutionState.WhiteDwarf,
                    sample.InitialMassSolar,
                    currentMassSolar,
                    radiusSolar,
                    luminositySolar,
                    temperatureKelvin);
        }

        private static CelestialStellarEvolutionResult EvaluateNeutronStar(
            double initialMassSolar)
        {
            const double currentMassSolar = 1.4;
            var radiusSolar =
                12000.0 /
                SolarRadiusMeters;

            return
                new CelestialStellarEvolutionResult(
                    CelestialStellarEvolutionState.NeutronStar,
                    initialMassSolar,
                    currentMassSolar,
                    radiusSolar,
                    1.0e-5,
                    CalculateTemperatureKelvin(
                        1.0e-5,
                        radiusSolar));
        }

        private static CelestialStellarEvolutionResult EvaluateBlackHole(
            double initialMassSolar)
        {
            var currentMassSolar =
                Math.Max(
                    3.0,
                    initialMassSolar *
                        0.2);
            var schwarzschildRadiusMeters =
                2.0 *
                GravitationalConstant *
                currentMassSolar *
                SolarMassKilograms /
                (SpeedOfLightMetersPerSecond *
                    SpeedOfLightMetersPerSecond);

            return
                new CelestialStellarEvolutionResult(
                    CelestialStellarEvolutionState.BlackHole,
                    initialMassSolar,
                    currentMassSolar,
                    schwarzschildRadiusMeters /
                        SolarRadiusMeters,
                    0.0,
                    0.0);
        }

        private static double CalculateTemperatureKelvin(
            double luminositySolar,
            double radiusSolar)
        {
            return
                SolarTemperatureKelvin *
                Math.Pow(
                    luminositySolar /
                    (radiusSolar *
                        radiusSolar),
                    0.25);
        }

        private static bool HasValidResult(
            CelestialStellarEvolutionResult result)
        {
            return
                IsFinitePositive(
                    result.InitialMassSolar) &&
                IsFinitePositive(
                    result.CurrentMassSolar) &&
                IsFinitePositive(
                    result.RadiusSolar) &&
                IsFiniteNonNegative(
                    result.LuminositySolar) &&
                IsFiniteNonNegative(
                    result.EffectiveTemperatureKelvin);
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
    }
}
