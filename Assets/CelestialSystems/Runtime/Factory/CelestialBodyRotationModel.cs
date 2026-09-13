/*
 * Produces deterministic, approximate present-day rotation states for generated planets and major moons.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialSpinDirection
    {
        Prograde = 0,
        Retrograde = 1
    }

    public enum CelestialSpinState
    {
        FreeRotating = 0,
        SpinOrbitSynchronous = 1
    }

    public readonly struct CelestialBodyRotationResult
    {
        public CelestialBodyRotationResult(
            double rotationPeriodHours,
            double axialTiltDegrees,
            CelestialSpinDirection spinDirection,
            CelestialSpinState spinState)
        {
            RotationPeriodHours = rotationPeriodHours;
            AxialTiltDegrees = axialTiltDegrees;
            SpinDirection = spinDirection;
            SpinState = spinState;
        }

        public double RotationPeriodHours { get; }
        public double AxialTiltDegrees { get; }
        public CelestialSpinDirection SpinDirection { get; }
        public CelestialSpinState SpinState { get; }
        public bool IsSynchronous => SpinState == CelestialSpinState.SpinOrbitSynchronous;
    }

    public static class CelestialBodyRotationModel
    {
        public const int ModelVersion = 1;

        private const double GravitationalConstant = 6.67430e-11;
        private const double EarthMassKilograms = 5.9722e24;
        private const double SolarMassKilograms = 1.98847e30;
        private const double AstronomicalUnitMeters = 149597870700.0;

        public static bool TryEvaluateStar(int seed, CelestialStellarEvolutionResult stellar, out CelestialBodyRotationResult result, out string error)
        {
            result = default; error = string.Empty;
            if (!IsFinitePositive(stellar.CurrentMassSolar)) { error = "Stellar rotation requires a valid present-day stellar mass."; return false; }
            var random = new DeterministicRandom(seed);
            var remnant = stellar.EvolutionState != CelestialStellarEvolutionState.MainSequence;
            result = new CelestialBodyRotationResult(RandomLogRange(ref random, remnant ? 0.1 : 8.0, remnant ? 240.0 : 720.0), random.NextRange(0.0, remnant ? 60.0 : 45.0), random.Next01() < (remnant ? 0.02 : 0.05) ? CelestialSpinDirection.Retrograde : CelestialSpinDirection.Prograde, CelestialSpinState.FreeRotating);
            if (!HasValidResult(result)) { result = default; error = "The rotation model produced an invalid stellar rotation state."; return false; }
            return true;
        }

        public static bool TryEvaluatePlanet(
            int seed,
            CelestialPlanetFormationResult planet,
            double presentOrbitAstronomicalUnits,
            double stellarMassSolar,
            double systemAgeGigayears,
            out CelestialBodyRotationResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(planet.TotalMassEarth) ||
                !IsFinitePositive(planet.RadiusEarth) ||
                !IsFinitePositive(presentOrbitAstronomicalUnits) ||
                !IsFinitePositive(stellarMassSolar) ||
                !IsFiniteNonNegative(systemAgeGigayears))
            {
                error = "Planet rotation requires valid planet, stellar-mass, orbital-distance, and system-age values.";
                return false;
            }

            var random = new DeterministicRandom(seed);
            var synchronous = IsPlanetSynchronous(
                planet,
                presentOrbitAstronomicalUnits,
                stellarMassSolar,
                systemAgeGigayears);
            double periodHours;
            double axialTilt;
            CelestialSpinDirection direction;

            if (synchronous)
            {
                periodHours = CalculateOrbitalPeriodHours(
                    presentOrbitAstronomicalUnits * AstronomicalUnitMeters,
                    stellarMassSolar * SolarMassKilograms);
                axialTilt = random.NextRange(0.0, 5.0);
                direction = CelestialSpinDirection.Prograde;
            }
            else
            {
                ResolveFreePlanetRotation(
                    ref random,
                    planet.FormationClass,
                    out periodHours,
                    out axialTilt,
                    out direction);
            }

            result = new CelestialBodyRotationResult(
                periodHours,
                axialTilt,
                direction,
                synchronous
                    ? CelestialSpinState.SpinOrbitSynchronous
                    : CelestialSpinState.FreeRotating);

            if (!HasValidResult(result))
            {
                result = default;
                error = "The rotation model produced an invalid planetary rotation state.";
                return false;
            }

            return true;
        }

        public static bool TryEvaluateMoon(
            int seed,
            CelestialMoonFormationResult moon,
            CelestialMoonEvolutionResult evolution,
            double planetMassEarth,
            out CelestialBodyRotationResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(moon.MassEarth) ||
                !IsFinitePositive(moon.RadiusEarth) ||
                !IsFinitePositive(moon.OrbitalRadiusMeters) ||
                !IsFinitePositive(planetMassEarth))
            {
                error = "Moon rotation requires valid moon and parent-planet properties.";
                return false;
            }

            var random = new DeterministicRandom(seed);
            var direction = moon.Direction == CelestialOrbitDirection.Retrograde
                ? CelestialSpinDirection.Retrograde
                : CelestialSpinDirection.Prograde;
            var synchronous = evolution.LikelyTidallyLocked;
            var periodHours = synchronous
                ? CalculateOrbitalPeriodHours(
                    moon.OrbitalRadiusMeters,
                    planetMassEarth * EarthMassKilograms)
                : RandomLogRange(ref random, 6.0, 120.0);
            var axialTilt = synchronous
                ? random.NextRange(0.0, 3.0)
                : random.NextRange(0.0, 45.0);

            result = new CelestialBodyRotationResult(
                periodHours,
                axialTilt,
                direction,
                synchronous
                    ? CelestialSpinState.SpinOrbitSynchronous
                    : CelestialSpinState.FreeRotating);

            if (!HasValidResult(result))
            {
                result = default;
                error = "The rotation model produced an invalid lunar rotation state.";
                return false;
            }

            return true;
        }

        private static bool IsPlanetSynchronous(
            CelestialPlanetFormationResult planet,
            double presentOrbitAstronomicalUnits,
            double stellarMassSolar,
            double systemAgeGigayears)
        {
            var exposure =
                (Math.Max(systemAgeGigayears, 0.000001) / 4.6) *
                Math.Pow(stellarMassSolar, 2.0) *
                Math.Pow(planet.RadiusEarth, 3.0) /
                planet.TotalMassEarth *
                Math.Pow(0.05 / presentOrbitAstronomicalUnits, 6.0);

            return exposure >= 1.0;
        }

        private static void ResolveFreePlanetRotation(
            ref DeterministicRandom random,
            CelestialPlanetFormationClass formationClass,
            out double periodHours,
            out double axialTiltDegrees,
            out CelestialSpinDirection direction)
        {
            double minimumHours;
            double maximumHours;

            switch (formationClass)
            {
                case CelestialPlanetFormationClass.Giant:
                    minimumHours = 5.0;
                    maximumHours = 20.0;
                    break;
                case CelestialPlanetFormationClass.GasRich:
                    minimumHours = 6.0;
                    maximumHours = 30.0;
                    break;
                case CelestialPlanetFormationClass.VolatileRich:
                    minimumHours = 10.0;
                    maximumHours = 100.0;
                    break;
                default:
                    minimumHours = 8.0;
                    maximumHours = 80.0;
                    break;
            }

            periodHours = RandomLogRange(
                ref random,
                minimumHours,
                maximumHours);
            var retrograde = random.Next01() < 0.08;
            direction = retrograde
                ? CelestialSpinDirection.Retrograde
                : CelestialSpinDirection.Prograde;
            axialTiltDegrees = retrograde
                ? random.NextRange(145.0, 180.0)
                : random.NextRange(0.0, 35.0);
        }

        private static double CalculateOrbitalPeriodHours(
            double orbitalRadiusMeters,
            double primaryMassKilograms)
        {
            return 2.0 * Math.PI *
                Math.Sqrt(
                    Math.Pow(orbitalRadiusMeters, 3.0) /
                    (GravitationalConstant * primaryMassKilograms)) /
                3600.0;
        }

        private static double RandomLogRange(
            ref DeterministicRandom random,
            double minimum,
            double maximum)
        {
            return Math.Exp(
                Math.Log(minimum) +
                (Math.Log(maximum) - Math.Log(minimum)) *
                random.Next01());
        }

        private static bool HasValidResult(CelestialBodyRotationResult result)
        {
            return IsFinitePositive(result.RotationPeriodHours) &&
                IsFiniteInRange(result.AxialTiltDegrees, 0.0, 180.0);
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0.0;
        }

        private static bool IsFiniteInRange(
            double value,
            double minimum,
            double maximum)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= minimum &&
                value <= maximum;
        }

        private struct DeterministicRandom
        {
            private uint state;

            public DeterministicRandom(int seed)
            {
                var value = unchecked((uint)seed) + 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                state = value == 0 ? 0x6D2B79F5u : value;
            }

            public double NextRange(double minimum, double maximum)
            {
                return minimum + (maximum - minimum) * Next01();
            }

            public double Next01()
            {
                return (NextUInt() >> 8) * (1.0 / 16777216.0);
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
