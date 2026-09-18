/*
 * Converts a formed moon and its present environment into deterministic, approximate present-day properties.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialMoonTidalHeating
    {
        Negligible = 0,
        Mild = 1,
        Significant = 2,
        Extreme = 3
    }

    public enum CelestialMoonTidalMigrationSensitivity
    {
        Low = 0,
        Moderate = 1,
        High = 2
    }

    public readonly struct CelestialMoonEvolutionResult
    {
        public CelestialMoonEvolutionResult(
            double assumedBondAlbedo,
            double equilibriumTemperatureKelvin,
            double escapeVelocityMetersPerSecond,
            CelestialAtmosphereRetention atmosphereRetention,
            CelestialPlanetDifferentiation differentiation,
            CelestialPlanetVolatileState volatileState,
            bool likelyTidallyLocked,
            double relativeTidalHeatingIndex,
            CelestialMoonTidalHeating tidalHeating,
            CelestialMoonTidalMigrationSensitivity migrationSensitivity)
        {
            AssumedBondAlbedo = assumedBondAlbedo;
            EquilibriumTemperatureKelvin =
                equilibriumTemperatureKelvin;
            EscapeVelocityMetersPerSecond =
                escapeVelocityMetersPerSecond;
            AtmosphereRetention = atmosphereRetention;
            Differentiation = differentiation;
            VolatileState = volatileState;
            LikelyTidallyLocked = likelyTidallyLocked;
            RelativeTidalHeatingIndex =
                relativeTidalHeatingIndex;
            TidalHeating = tidalHeating;
            MigrationSensitivity = migrationSensitivity;
        }

        public double AssumedBondAlbedo { get; }

        public double EquilibriumTemperatureKelvin { get; }

        public double EscapeVelocityMetersPerSecond { get; }

        public CelestialAtmosphereRetention AtmosphereRetention { get; }

        public CelestialPlanetDifferentiation Differentiation { get; }

        public CelestialPlanetVolatileState VolatileState { get; }

        public bool LikelyTidallyLocked { get; }

        public double RelativeTidalHeatingIndex { get; }

        public CelestialMoonTidalHeating TidalHeating { get; }

        public CelestialMoonTidalMigrationSensitivity MigrationSensitivity { get; }
    }

    public static class CelestialMoonEvolutionModel
    {
        public const int ModelVersion = 1;

        private const double EarthEscapeVelocityMetersPerSecond =
            11186.0;

        private const double SolarEquilibriumTemperatureKelvin =
            278.5;

        private const double EarthRadiusMeters =
            6371000.0;

        private const double IoMassEarth =
            0.01495;

        private const double IoRadiusEarth =
            0.286;

        private const double IoOrbitMeters =
            421700000.0;

        private const double IoEccentricity =
            0.0041;

        private const double JupiterMassEarth =
            317.8;

        private const double LunarMassEarth =
            0.0123;

        private const double LunarRadiusEarth =
            0.2727;

        private const double LunarOrbitMeters =
            384400000.0;

        public static bool TryEvaluate(
            int seed,
            CelestialMoonFormationResult moon,
            CelestialPlanetFormationResult planet,
            CelestialStellarEvolutionResult star,
            double systemAgeGigayears,
            double presentPlanetOrbitAstronomicalUnits,
            out CelestialMoonEvolutionResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!HasValidMoon(
                    moon) ||
                !IsFinitePositive(
                    planet.TotalMassEarth) ||
                !IsFinitePositive(
                    planet.RadiusEarth) ||
                !IsFinitePositive(
                    star.LuminositySolar) ||
                !IsFiniteNonNegative(
                    systemAgeGigayears) ||
                !IsFinitePositive(
                    presentPlanetOrbitAstronomicalUnits))
            {
                error =
                    "Moon evolution requires valid moon, planet, stellar-luminosity, system-age, and planetary-orbit values.";
                return false;
            }

            var albedo =
                ResolveAssumedBondAlbedo(
                    seed,
                    moon);
            var equilibriumTemperature =
                SolarEquilibriumTemperatureKelvin *
                Math.Pow(
                    star.LuminositySolar,
                    0.25) /
                Math.Sqrt(
                    presentPlanetOrbitAstronomicalUnits) *
                Math.Pow(
                    1.0 -
                        albedo,
                    0.25);
            var escapeVelocity =
                EarthEscapeVelocityMetersPerSecond *
                Math.Sqrt(
                    moon.MassEarth /
                    moon.RadiusEarth);
            var atmosphereRetention =
                ResolveAtmosphereRetention(
                    escapeVelocity,
                    equilibriumTemperature,
                    systemAgeGigayears);
            var relativeTidalHeatingIndex =
                CalculateRelativeTidalHeatingIndex(
                    moon,
                    planet.TotalMassEarth);
            var tidalHeating =
                ResolveTidalHeating(
                    relativeTidalHeatingIndex);
            var likelyTidallyLocked =
                ResolveLikelyTidallyLocked(
                    moon,
                    planet.TotalMassEarth,
                    systemAgeGigayears);
            var differentiation =
                ResolveDifferentiation(
                    moon.MassEarth,
                    systemAgeGigayears,
                    tidalHeating);
            var volatileState =
                ResolveVolatileState(
                    moon.VolatileMassFraction,
                    equilibriumTemperature,
                    atmosphereRetention,
                    tidalHeating);
            var migrationSensitivity =
                ResolveMigrationSensitivity(
                    moon,
                    planet.TotalMassEarth);

            result =
                new CelestialMoonEvolutionResult(
                    albedo,
                    equilibriumTemperature,
                    escapeVelocity,
                    atmosphereRetention,
                    differentiation,
                    volatileState,
                    likelyTidallyLocked,
                    relativeTidalHeatingIndex,
                    tidalHeating,
                    migrationSensitivity);

            if (!HasValidResult(
                    result))
            {
                result = default;
                error =
                    "The moon-evolution model produced invalid properties.";
                return false;
            }

            return true;
        }

        private static double ResolveAssumedBondAlbedo(
            int seed,
            CelestialMoonFormationResult moon)
        {
            var random =
                new DeterministicRandom(
                    seed);
            var iceInfluence =
                Clamp01(
                    moon.VolatileMassFraction /
                        0.6);
            var minimum =
                Lerp(
                    0.08,
                    0.28,
                    iceInfluence);
            var maximum =
                Lerp(
                    0.32,
                    0.68,
                    iceInfluence);

            return random.NextRange(
                minimum,
                maximum);
        }

        private static CelestialAtmosphereRetention ResolveAtmosphereRetention(
            double escapeVelocityMetersPerSecond,
            double equilibriumTemperatureKelvin,
            double systemAgeGigayears)
        {
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
                0.04;
            var retentionIndex =
                escapeVelocityMetersPerSecond /
                EarthEscapeVelocityMetersPerSecond /
                thermalEscapePressure /
                ageLoss;

            if (retentionIndex < 0.22)
            {
                return CelestialAtmosphereRetention.None;
            }

            if (retentionIndex < 0.55)
            {
                return CelestialAtmosphereRetention.Thin;
            }

            return CelestialAtmosphereRetention.Substantial;
        }

        private static double CalculateRelativeTidalHeatingIndex(
            CelestialMoonFormationResult moon,
            double planetMassEarth)
        {
            if (moon.Eccentricity <= 0.0)
            {
                return 0.0;
            }

            return
                Math.Pow(
                    planetMassEarth /
                        JupiterMassEarth,
                    2.0) *
                Math.Pow(
                    moon.RadiusEarth /
                        IoRadiusEarth,
                    3.0) *
                Math.Pow(
                    moon.Eccentricity /
                        IoEccentricity,
                    2.0) *
                Math.Pow(
                    IoOrbitMeters /
                        moon.OrbitalRadiusMeters,
                    6.0);
        }

        private static CelestialMoonTidalHeating ResolveTidalHeating(
            double relativeIndex)
        {
            if (relativeIndex < 0.01)
            {
                return CelestialMoonTidalHeating.Negligible;
            }

            if (relativeIndex < 0.25)
            {
                return CelestialMoonTidalHeating.Mild;
            }

            if (relativeIndex < 2.0)
            {
                return CelestialMoonTidalHeating.Significant;
            }

            return CelestialMoonTidalHeating.Extreme;
        }

        private static bool ResolveLikelyTidallyLocked(
            CelestialMoonFormationResult moon,
            double planetMassEarth,
            double systemAgeGigayears)
        {
            var lockingExposure =
                Math.Max(
                    systemAgeGigayears,
                    0.000001) /
                    0.1 *
                Math.Pow(
                    planetMassEarth,
                    2.0) *
                Math.Pow(
                    moon.RadiusEarth /
                        LunarRadiusEarth,
                    3.0) *
                (LunarMassEarth /
                    moon.MassEarth) *
                Math.Pow(
                    LunarOrbitMeters /
                        moon.OrbitalRadiusMeters,
                    6.0);

            return lockingExposure >= 1.0;
        }

        private static CelestialPlanetDifferentiation ResolveDifferentiation(
            double moonMassEarth,
            double systemAgeGigayears,
            CelestialMoonTidalHeating tidalHeating)
        {
            var thermalHistory =
                moonMassEarth *
                Math.Max(
                    systemAgeGigayears,
                    0.01);

            if (tidalHeating ==
                    CelestialMoonTidalHeating.Extreme ||
                moonMassEarth >= 0.02)
            {
                return CelestialPlanetDifferentiation.Differentiated;
            }

            if (tidalHeating ==
                    CelestialMoonTidalHeating.Significant ||
                moonMassEarth >= 0.003 ||
                thermalHistory >= 0.004)
            {
                return CelestialPlanetDifferentiation.PartiallyDifferentiated;
            }

            return CelestialPlanetDifferentiation.Undifferentiated;
        }

        private static CelestialPlanetVolatileState ResolveVolatileState(
            double volatileMassFraction,
            double equilibriumTemperatureKelvin,
            CelestialAtmosphereRetention atmosphereRetention,
            CelestialMoonTidalHeating tidalHeating)
        {
            if (volatileMassFraction < 0.005)
            {
                return CelestialPlanetVolatileState.Depleted;
            }

            if (tidalHeating ==
                CelestialMoonTidalHeating.Extreme)
            {
                return CelestialPlanetVolatileState.VaporDominated;
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

        private static CelestialMoonTidalMigrationSensitivity ResolveMigrationSensitivity(
            CelestialMoonFormationResult moon,
            double planetMassEarth)
        {
            var periapsis =
                moon.OrbitalRadiusMeters *
                (1.0 -
                    moon.Eccentricity);
            var apoapsis =
                moon.OrbitalRadiusMeters *
                (1.0 +
                    moon.Eccentricity);
            var innerMargin =
                periapsis /
                moon.RocheLimitMeters;
            var outerFraction =
                apoapsis /
                moon.StableOuterLimitMeters;
            var massRatio =
                moon.MassEarth /
                planetMassEarth;

            if (innerMargin < 1.5 ||
                outerFraction > 0.7 ||
                massRatio > 0.01)
            {
                return CelestialMoonTidalMigrationSensitivity.High;
            }

            if (innerMargin < 2.5 ||
                outerFraction > 0.45 ||
                massRatio > 0.001)
            {
                return CelestialMoonTidalMigrationSensitivity.Moderate;
            }

            return CelestialMoonTidalMigrationSensitivity.Low;
        }

        private static bool HasValidMoon(
            CelestialMoonFormationResult moon)
        {
            return
                IsFinitePositive(
                    moon.MassEarth) &&
                IsFinitePositive(
                    moon.RadiusEarth) &&
                IsFraction(
                    moon.VolatileMassFraction) &&
                IsFinitePositive(
                    moon.OrbitalRadiusMeters) &&
                IsFiniteInRange(
                    moon.Eccentricity,
                    0.0,
                    1.0) &&
                moon.Eccentricity < 1.0 &&
                IsFinitePositive(
                    moon.RocheLimitMeters) &&
                IsFinitePositive(
                    moon.StableOuterLimitMeters);
        }

        private static bool HasValidResult(
            CelestialMoonEvolutionResult result)
        {
            return
                IsFraction(
                    result.AssumedBondAlbedo) &&
                IsFinitePositive(
                    result.EquilibriumTemperatureKelvin) &&
                IsFinitePositive(
                    result.EscapeVelocityMetersPerSecond) &&
                IsFiniteNonNegative(
                    result.RelativeTidalHeatingIndex);
        }

        private static double Lerp(
            double minimum,
            double maximum,
            double fraction)
        {
            return
                minimum +
                (maximum -
                    minimum) *
                Clamp01(
                    fraction);
        }

        private static double Clamp01(
            double value)
        {
            return Math.Max(
                0.0,
                Math.Min(
                    1.0,
                    value));
        }

        private static bool IsFraction(
            double value)
        {
            return IsFiniteInRange(
                value,
                0.0,
                1.0);
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

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(
                int seed)
            {
                var value =
                    unchecked((uint)seed) +
                    0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
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
                    (maximum -
                        minimum) *
                    Next01();
            }

            private double Next01()
            {
                return
                    (NextUInt() >> 8) *
                    (1.0 /
                        16777216.0);
            }

            private uint NextUInt()
            {
                var value = state;
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                state = value;
                return value;
            }
        }
    }
}
