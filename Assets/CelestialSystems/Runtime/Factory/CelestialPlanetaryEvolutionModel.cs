/*
 * Converts a planet-formation outcome and its present stellar environment into deterministic, approximate present-day planetary properties.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialAtmosphereRetention
    {
        None = 0,
        Thin = 1,
        Substantial = 2,
        Massive = 3
    }

    public enum CelestialPlanetDifferentiation
    {
        Undifferentiated = 0,
        PartiallyDifferentiated = 1,
        Differentiated = 2
    }

    public enum CelestialPlanetVolatileState
    {
        Depleted = 0,
        Frozen = 1,
        CondensedPotential = 2,
        VaporDominated = 3,
        DeepEnvelope = 4
    }

    public readonly struct CelestialPlanetaryEvolutionResult
    {
        public CelestialPlanetaryEvolutionResult(
            double assumedBondAlbedo,
            double equilibriumTemperatureKelvin,
            double escapeVelocityMetersPerSecond,
            CelestialAtmosphereRetention atmosphereRetention,
            CelestialPlanetDifferentiation differentiation,
            CelestialPlanetVolatileState volatileState)
        {
            AssumedBondAlbedo = assumedBondAlbedo;
            EquilibriumTemperatureKelvin =
                equilibriumTemperatureKelvin;
            EscapeVelocityMetersPerSecond =
                escapeVelocityMetersPerSecond;
            AtmosphereRetention = atmosphereRetention;
            Differentiation = differentiation;
            VolatileState = volatileState;
        }

        public double AssumedBondAlbedo { get; }
        public double EquilibriumTemperatureKelvin { get; }
        public double EscapeVelocityMetersPerSecond { get; }
        public CelestialAtmosphereRetention AtmosphereRetention { get; }
        public CelestialPlanetDifferentiation Differentiation { get; }
        public CelestialPlanetVolatileState VolatileState { get; }
    }

    public static class CelestialPlanetaryEvolutionModel
    {
        public const int ModelVersion = 1;

        private const double EarthEscapeVelocityMetersPerSecond =
            11186.0;
        private const double SolarEquilibriumTemperatureKelvin =
            278.5;

        public static bool TryEvaluate(
            int seed,
            CelestialPlanetFormationResult formation,
            CelestialStellarEvolutionResult star,
            double systemAgeGigayears,
            out CelestialPlanetaryEvolutionResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(
                    formation.TotalMassEarth) ||
                !IsFinitePositive(
                    formation.RadiusEarth) ||
                !IsFinitePositive(
                    formation.FinalOrbitAstronomicalUnits) ||
                !IsFinitePositive(
                    star.LuminositySolar) ||
                !IsFiniteNonNegative(
                    systemAgeGigayears))
            {
                error =
                    "Planetary evolution requires valid planet, orbit, stellar-luminosity, and age values.";
                return false;
            }

            var albedo =
                ResolveAssumedBondAlbedo(
                    seed,
                    formation.FormationClass);
            var equilibriumTemperature =
                SolarEquilibriumTemperatureKelvin *
                Math.Pow(
                    star.LuminositySolar,
                    0.25) /
                Math.Sqrt(
                    formation.FinalOrbitAstronomicalUnits) *
                Math.Pow(
                    (1.0 - albedo) /
                    0.7,
                    0.25);
            var escapeVelocity =
                EarthEscapeVelocityMetersPerSecond *
                Math.Sqrt(
                    formation.TotalMassEarth /
                    formation.RadiusEarth);
            var atmosphereRetention =
                ResolveAtmosphereRetention(
                    formation,
                    equilibriumTemperature,
                    escapeVelocity,
                    systemAgeGigayears);
            var differentiation =
                ResolveDifferentiation(
                    formation.SolidCoreMassEarth,
                    systemAgeGigayears);
            var volatileState =
                ResolveVolatileState(
                    formation,
                    equilibriumTemperature,
                    atmosphereRetention);

            result =
                new CelestialPlanetaryEvolutionResult(
                    albedo,
                    equilibriumTemperature,
                    escapeVelocity,
                    atmosphereRetention,
                    differentiation,
                    volatileState);

            if (!HasValidResult(
                    result))
            {
                result = default;
                error =
                    "The planetary-evolution model produced invalid properties.";
                return false;
            }

            return true;
        }

        private static double ResolveAssumedBondAlbedo(
            int seed,
            CelestialPlanetFormationClass formationClass)
        {
            var random =
                new DeterministicRandom(
                    seed);

            switch (formationClass)
            {
                case CelestialPlanetFormationClass.Rocky:
                    return random.NextRange(
                        0.08,
                        0.38);
                case CelestialPlanetFormationClass.VolatileRich:
                    return random.NextRange(
                        0.25,
                        0.65);
                case CelestialPlanetFormationClass.GasRich:
                    return random.NextRange(
                        0.12,
                        0.45);
                case CelestialPlanetFormationClass.Giant:
                    return random.NextRange(
                        0.2,
                        0.55);
                default:
                    return 0.3;
            }
        }

        private static CelestialAtmosphereRetention ResolveAtmosphereRetention(
            CelestialPlanetFormationResult formation,
            double equilibriumTemperatureKelvin,
            double escapeVelocityMetersPerSecond,
            double systemAgeGigayears)
        {
            if (formation.HydrogenHeliumEnvelopeFraction >=
                0.5)
            {
                return CelestialAtmosphereRetention.Massive;
            }

            if (formation.HydrogenHeliumEnvelopeFraction >=
                0.01)
            {
                return CelestialAtmosphereRetention.Substantial;
            }

            var thermalEscapePressure =
                Math.Sqrt(
                    Math.Max(
                        equilibriumTemperatureKelvin,
                        50.0) /
                    288.0);
            var ageLoss =
                1.0 +
                Math.Min(
                    systemAgeGigayears,
                    13.8) *
                0.035;
            var retentionIndex =
                escapeVelocityMetersPerSecond /
                EarthEscapeVelocityMetersPerSecond /
                thermalEscapePressure /
                ageLoss;

            if (retentionIndex < 0.35)
            {
                return CelestialAtmosphereRetention.None;
            }

            if (retentionIndex < 0.75)
            {
                return CelestialAtmosphereRetention.Thin;
            }

            return CelestialAtmosphereRetention.Substantial;
        }

        private static CelestialPlanetDifferentiation ResolveDifferentiation(
            double solidCoreMassEarth,
            double systemAgeGigayears)
        {
            var thermalHistory =
                solidCoreMassEarth *
                Math.Max(
                    systemAgeGigayears,
                    0.01);

            if (solidCoreMassEarth < 0.015 ||
                thermalHistory < 0.002)
            {
                return CelestialPlanetDifferentiation.Undifferentiated;
            }

            if (solidCoreMassEarth < 0.08 ||
                thermalHistory < 0.02)
            {
                return CelestialPlanetDifferentiation.PartiallyDifferentiated;
            }

            return CelestialPlanetDifferentiation.Differentiated;
        }

        private static CelestialPlanetVolatileState ResolveVolatileState(
            CelestialPlanetFormationResult formation,
            double equilibriumTemperatureKelvin,
            CelestialAtmosphereRetention atmosphereRetention)
        {
            if (formation.FormationClass ==
                CelestialPlanetFormationClass.Giant ||
                formation.HydrogenHeliumEnvelopeFraction >=
                    0.5)
            {
                return CelestialPlanetVolatileState.DeepEnvelope;
            }

            if (formation.VolatileMassFraction < 0.005)
            {
                return CelestialPlanetVolatileState.Depleted;
            }

            if (equilibriumTemperatureKelvin < 180.0)
            {
                return CelestialPlanetVolatileState.Frozen;
            }

            if (equilibriumTemperatureKelvin <= 330.0 &&
                atmosphereRetention !=
                    CelestialAtmosphereRetention.None)
            {
                return CelestialPlanetVolatileState.CondensedPotential;
            }

            return CelestialPlanetVolatileState.VaporDominated;
        }

        private static bool HasValidResult(
            CelestialPlanetaryEvolutionResult result)
        {
            return
                IsFiniteInRange(
                    result.AssumedBondAlbedo,
                    0.0,
                    1.0) &&
                IsFinitePositive(
                    result.EquilibriumTemperatureKelvin) &&
                IsFinitePositive(
                    result.EscapeVelocityMetersPerSecond);
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static bool IsFiniteNonNegative(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0.0;
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
