/*
 * Produces deterministic, approximate natural-satellite systems from generated planet properties.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialMoonFormationOrigin
    {
        RegularDisk = 0,
        GiantImpact = 1,
        Captured = 2
    }

    public readonly struct CelestialMoonFormationResult
    {
        public CelestialMoonFormationResult(
            CelestialMoonFormationOrigin origin,
            double massEarth,
            double radiusEarth,
            double volatileMassFraction,
            double orbitalRadiusMeters,
            double eccentricity,
            double inclinationDegrees,
            CelestialOrbitDirection direction,
            double rocheLimitMeters,
            double stableOuterLimitMeters)
        {
            Origin = origin;
            MassEarth = massEarth;
            RadiusEarth = radiusEarth;
            VolatileMassFraction = volatileMassFraction;
            OrbitalRadiusMeters = orbitalRadiusMeters;
            Eccentricity = eccentricity;
            InclinationDegrees = inclinationDegrees;
            Direction = direction;
            RocheLimitMeters = rocheLimitMeters;
            StableOuterLimitMeters = stableOuterLimitMeters;
        }

        public CelestialMoonFormationOrigin Origin { get; }

        public double MassEarth { get; }

        public double RadiusEarth { get; }

        public double VolatileMassFraction { get; }

        public double OrbitalRadiusMeters { get; }

        public double Eccentricity { get; }

        public double InclinationDegrees { get; }

        public CelestialOrbitDirection Direction { get; }

        public double RocheLimitMeters { get; }

        public double StableOuterLimitMeters { get; }
    }

    public readonly struct CelestialMoonSystemFormationResult
    {
        public CelestialMoonSystemFormationResult(
            double hillRadiusMeters,
            double satelliteMassBudgetEarth,
            CelestialMoonFormationResult[] moons)
        {
            HillRadiusMeters = hillRadiusMeters;
            SatelliteMassBudgetEarth =
                satelliteMassBudgetEarth;
            Moons =
                moons ??
                Array.Empty<CelestialMoonFormationResult>();
        }

        public double HillRadiusMeters { get; }

        public double SatelliteMassBudgetEarth { get; }

        public CelestialMoonFormationResult[] Moons { get; }
    }

    public static class CelestialMoonFormationModel
    {
        public const int ModelVersion = 1;

        private const double AstronomicalUnitMeters =
            149597870700.0;

        private const double SolarMassEarth =
            332946.0;

        private const double EarthRadiusMeters =
            6371000.0;

        private const double EarthMeanDensityKilogramsPerCubicMeter =
            5514.0;

        public static bool TryGenerate(
            int seed,
            CelestialPlanetFormationResult planet,
            double planetOrbitAstronomicalUnits,
            double stellarMassSolar,
            out CelestialMoonSystemFormationResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!HasValidPlanet(
                    planet) ||
                !IsFinitePositive(
                    planetOrbitAstronomicalUnits) ||
                !IsFinitePositive(
                    stellarMassSolar))
            {
                error =
                    "Moon formation requires valid planet properties, a positive planetary orbit, and a positive stellar mass.";
                return false;
            }

            var planetRadiusMeters =
                planet.RadiusEarth *
                EarthRadiusMeters;
            var hillRadiusMeters =
                planetOrbitAstronomicalUnits *
                AstronomicalUnitMeters *
                Math.Pow(
                    planet.TotalMassEarth /
                        (3.0 *
                            stellarMassSolar *
                            SolarMassEarth),
                    1.0 /
                        3.0);

            if (!IsFinitePositive(
                    hillRadiusMeters))
            {
                error =
                    "Moon formation produced an invalid planetary Hill radius.";
                return false;
            }

            var random =
                new DeterministicRandom(
                    seed);
            var origin =
                SelectOrigin(
                    planet,
                    ref random);

            if (!origin.HasValue)
            {
                result =
                    new CelestialMoonSystemFormationResult(
                        hillRadiusMeters,
                        0.0,
                        Array.Empty<CelestialMoonFormationResult>());
                return true;
            }

            var direction =
                SelectDirection(
                    origin.Value,
                    ref random);
            var stableHillFraction =
                direction ==
                    CelestialOrbitDirection.Retrograde
                        ? 0.80
                        : 0.42;
            var stableOuterLimitMeters =
                hillRadiusMeters *
                stableHillFraction;
            var volatileFraction =
                SelectVolatileFraction(
                    origin.Value,
                    planet,
                    ref random);
            var moonDensity =
                Lerp(
                    3400.0,
                    1700.0,
                    volatileFraction);
            var planetDensity =
                EarthMeanDensityKilogramsPerCubicMeter *
                planet.TotalMassEarth /
                Math.Pow(
                    planet.RadiusEarth,
                    3.0);
            var rocheLimitMeters =
                2.44 *
                planetRadiusMeters *
                Math.Pow(
                    planetDensity /
                        moonDensity,
                    1.0 /
                        3.0);
            var innerOrbitMeters =
                Math.Max(
                    rocheLimitMeters *
                        1.12,
                    planetRadiusMeters *
                        2.6);
            var originOuterLimitMeters =
                stableOuterLimitMeters;

            if (origin.Value ==
                CelestialMoonFormationOrigin.RegularDisk)
            {
                originOuterLimitMeters =
                    Math.Min(
                        originOuterLimitMeters,
                        planetRadiusMeters *
                            80.0);
            }
            else if (origin.Value ==
                CelestialMoonFormationOrigin.GiantImpact)
            {
                originOuterLimitMeters =
                    Math.Min(
                        originOuterLimitMeters,
                        planetRadiusMeters *
                            30.0);
            }

            if (!IsFinitePositive(
                    rocheLimitMeters) ||
                originOuterLimitMeters <=
                    innerOrbitMeters *
                        1.25)
            {
                result =
                    new CelestialMoonSystemFormationResult(
                        hillRadiusMeters,
                        0.0,
                        Array.Empty<CelestialMoonFormationResult>());
                return true;
            }

            var massBudgetEarth =
                planet.TotalMassEarth *
                SelectMassRatio(
                    origin.Value,
                    ref random);
            var moonCount =
                SelectMoonCount(
                    origin.Value,
                    planet.TotalMassEarth,
                    ref random);
            var weights =
                new double[moonCount];
            var totalWeight = 0.0;

            for (var index = 0;
                index < moonCount;
                index++)
            {
                weights[index] =
                    random.NextRange(
                        0.45,
                        1.55);
                totalWeight +=
                    weights[index];
            }

            var orbitalRadii =
                new double[moonCount];

            for (var index = 0;
                index < moonCount;
                index++)
            {
                var fraction =
                    ((double)index +
                        0.5) /
                    moonCount;
                orbitalRadii[index] =
                    LogarithmicLerp(
                        innerOrbitMeters,
                        originOuterLimitMeters,
                        fraction);
            }

            var generated =
                new CelestialMoonFormationResult[moonCount];

            for (var index = 0;
                index < moonCount;
                index++)
            {
                var massEarth =
                    massBudgetEarth *
                    weights[index] /
                    totalWeight;
                var individualVolatileFraction =
                    Clamp01(
                        volatileFraction +
                        random.NextRange(
                            -0.08,
                            0.08));
                var radiusEarth =
                    Math.Pow(
                        massEarth,
                        0.27) *
                    (1.0 +
                        individualVolatileFraction *
                            0.15);
                var eccentricityLimit =
                    SelectEccentricityLimit(
                        origin.Value);

                if (moonCount > 1)
                {
                    var spacingLimit =
                        double.MaxValue;

                    if (index > 0)
                    {
                        spacingLimit =
                            Math.Min(
                                spacingLimit,
                                TouchingEccentricity(
                                    orbitalRadii[index - 1],
                                    orbitalRadii[index]));
                    }

                    if (index + 1 <
                        moonCount)
                    {
                        spacingLimit =
                            Math.Min(
                                spacingLimit,
                                TouchingEccentricity(
                                    orbitalRadii[index],
                                    orbitalRadii[index + 1]));
                    }

                    eccentricityLimit =
                        Math.Min(
                            eccentricityLimit,
                            spacingLimit *
                                0.7);
                }

                var boundaryEccentricityLimit =
                    Math.Min(
                        1.0 -
                            rocheLimitMeters /
                            orbitalRadii[index],
                        stableOuterLimitMeters /
                            orbitalRadii[index] -
                            1.0);
                eccentricityLimit =
                    Math.Min(
                        eccentricityLimit,
                        Math.Max(
                            0.0,
                            boundaryEccentricityLimit *
                                0.9));
                var eccentricity =
                    random.NextRange(
                        0.0,
                        Math.Max(
                            0.0,
                            eccentricityLimit));
                var inclinationDegrees =
                    SelectInclinationDegrees(
                        origin.Value,
                        direction,
                        ref random);

                generated[index] =
                    new CelestialMoonFormationResult(
                        origin.Value,
                        massEarth,
                        radiusEarth,
                        individualVolatileFraction,
                        orbitalRadii[index],
                        eccentricity,
                        inclinationDegrees,
                        direction,
                        rocheLimitMeters,
                        stableOuterLimitMeters);

                if (!HasValidMoon(
                        generated[index]))
                {
                    error =
                        $"The moon-formation model produced invalid properties at index {index}.";
                    result = default;
                    return false;
                }
            }

            result =
                new CelestialMoonSystemFormationResult(
                    hillRadiusMeters,
                    massBudgetEarth,
                    generated);
            return true;
        }

        private static CelestialMoonFormationOrigin? SelectOrigin(
            CelestialPlanetFormationResult planet,
            ref DeterministicRandom random)
        {
            switch (planet.FormationClass)
            {
                case CelestialPlanetFormationClass.Giant:
                    return
                        random.Next01() < 0.94
                            ? CelestialMoonFormationOrigin.RegularDisk
                            : (CelestialMoonFormationOrigin?)null;

                case CelestialPlanetFormationClass.GasRich:
                    if (random.Next01() < 0.78)
                    {
                        return CelestialMoonFormationOrigin.RegularDisk;
                    }
                    return
                        random.Next01() < 0.12
                            ? CelestialMoonFormationOrigin.Captured
                            : (CelestialMoonFormationOrigin?)null;

                case CelestialPlanetFormationClass.VolatileRich:
                    if (planet.TotalMassEarth >= 0.2 &&
                        random.Next01() < 0.48)
                    {
                        return CelestialMoonFormationOrigin.RegularDisk;
                    }
                    return
                        random.Next01() < 0.12
                            ? CelestialMoonFormationOrigin.Captured
                            : (CelestialMoonFormationOrigin?)null;

                case CelestialPlanetFormationClass.Rocky:
                    if (planet.TotalMassEarth >= 0.2 &&
                        planet.TotalMassEarth <= 12.0 &&
                        random.Next01() < 0.32)
                    {
                        return CelestialMoonFormationOrigin.GiantImpact;
                    }
                    return
                        random.Next01() < 0.08
                            ? CelestialMoonFormationOrigin.Captured
                            : (CelestialMoonFormationOrigin?)null;

                default:
                    return null;
            }
        }

        private static CelestialOrbitDirection SelectDirection(
            CelestialMoonFormationOrigin origin,
            ref DeterministicRandom random)
        {
            if (origin !=
                CelestialMoonFormationOrigin.Captured)
            {
                return CelestialOrbitDirection.Prograde;
            }

            return
                random.Next01() < 0.6
                    ? CelestialOrbitDirection.Retrograde
                    : CelestialOrbitDirection.Prograde;
        }

        private static double SelectMassRatio(
            CelestialMoonFormationOrigin origin,
            ref DeterministicRandom random)
        {
            switch (origin)
            {
                case CelestialMoonFormationOrigin.RegularDisk:
                    return random.NextRange(
                        0.00005,
                        0.0002);
                case CelestialMoonFormationOrigin.GiantImpact:
                    return random.NextRange(
                        0.003,
                        0.025);
                case CelestialMoonFormationOrigin.Captured:
                    return random.NextRange(
                        0.0000001,
                        0.00005);
                default:
                    return 0.0;
            }
        }

        private static int SelectMoonCount(
            CelestialMoonFormationOrigin origin,
            double planetMassEarth,
            ref DeterministicRandom random)
        {
            if (origin !=
                CelestialMoonFormationOrigin.RegularDisk)
            {
                return 1;
            }

            var maximum =
                planetMassEarth >= 50.0
                    ? 7
                    : planetMassEarth >= 5.0
                        ? 4
                        : 2;
            return random.NextInclusive(
                1,
                maximum);
        }

        private static double SelectVolatileFraction(
            CelestialMoonFormationOrigin origin,
            CelestialPlanetFormationResult planet,
            ref DeterministicRandom random)
        {
            switch (origin)
            {
                case CelestialMoonFormationOrigin.GiantImpact:
                    return Clamp(
                        planet.VolatileMassFraction *
                            random.NextRange(
                                0.05,
                                0.35),
                        0.0,
                        0.2);
                case CelestialMoonFormationOrigin.Captured:
                    return random.NextRange(
                        0.0,
                        0.7);
                default:
                    return Clamp(
                        planet.VolatileMassFraction +
                            random.NextRange(
                                0.15,
                                0.45),
                        0.05,
                        0.75);
            }
        }

        private static double SelectEccentricityLimit(
            CelestialMoonFormationOrigin origin)
        {
            switch (origin)
            {
                case CelestialMoonFormationOrigin.RegularDisk:
                    return 0.03;
                case CelestialMoonFormationOrigin.GiantImpact:
                    return 0.08;
                case CelestialMoonFormationOrigin.Captured:
                    return 0.45;
                default:
                    return 0.0;
            }
        }

        private static double SelectInclinationDegrees(
            CelestialMoonFormationOrigin origin,
            CelestialOrbitDirection direction,
            ref DeterministicRandom random)
        {
            switch (origin)
            {
                case CelestialMoonFormationOrigin.RegularDisk:
                    return random.NextRange(
                        0.0,
                        3.0);
                case CelestialMoonFormationOrigin.GiantImpact:
                    return random.NextRange(
                        0.0,
                        15.0);
                case CelestialMoonFormationOrigin.Captured:
                    return
                        direction ==
                            CelestialOrbitDirection.Retrograde
                                ? random.NextRange(
                                    100.0,
                                    170.0)
                                : random.NextRange(
                                    20.0,
                                    80.0);
                default:
                    return 0.0;
            }
        }

        private static double TouchingEccentricity(
            double innerRadius,
            double outerRadius)
        {
            return
                (outerRadius -
                    innerRadius) /
                (outerRadius +
                    innerRadius);
        }

        private static double LogarithmicLerp(
            double minimum,
            double maximum,
            double fraction)
        {
            return Math.Exp(
                Math.Log(minimum) +
                (Math.Log(maximum) -
                    Math.Log(minimum)) *
                fraction);
        }

        private static bool HasValidPlanet(
            CelestialPlanetFormationResult planet)
        {
            return
                IsFinitePositive(
                    planet.TotalMassEarth) &&
                IsFinitePositive(
                    planet.RadiusEarth) &&
                IsFraction(
                    planet.VolatileMassFraction) &&
                IsFraction(
                    planet.HydrogenHeliumEnvelopeFraction);
        }

        private static bool HasValidMoon(
            CelestialMoonFormationResult moon)
        {
            var periapsis =
                moon.OrbitalRadiusMeters *
                (1.0 -
                    moon.Eccentricity);
            var apoapsis =
                moon.OrbitalRadiusMeters *
                (1.0 +
                    moon.Eccentricity);

            return
                IsFinitePositive(
                    moon.MassEarth) &&
                IsFinitePositive(
                    moon.RadiusEarth) &&
                IsFraction(
                    moon.VolatileMassFraction) &&
                IsFinitePositive(
                    moon.OrbitalRadiusMeters) &&
                IsFinite(
                    moon.Eccentricity) &&
                moon.Eccentricity >= 0.0 &&
                moon.Eccentricity < 1.0 &&
                IsFinite(
                    moon.InclinationDegrees) &&
                moon.InclinationDegrees >= 0.0 &&
                moon.InclinationDegrees <= 180.0 &&
                periapsis >
                    moon.RocheLimitMeters &&
                apoapsis <
                    moon.StableOuterLimitMeters;
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
            return Clamp(
                value,
                0.0,
                1.0);
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value));
        }

        private static bool IsFraction(
            double value)
        {
            return
                IsFinite(value) &&
                value >= 0.0 &&
                value <= 1.0;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                IsFinite(value) &&
                value > 0.0;
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
                value ^= value >> 16;
                value *= 0x85EBCA6Bu;
                value ^= value >> 13;
                value *= 0xC2B2AE35u;
                value ^= value >> 16;
                state =
                    value == 0
                        ? 0x6D2B79F5u
                        : value;
            }

            public int NextInclusive(
                int minimum,
                int maximum)
            {
                return
                    minimum +
                    (int)(NextUInt() %
                        (uint)(maximum -
                            minimum +
                            1));
            }

            public double Next01()
            {
                return
                    (NextUInt() >> 8) *
                    (1.0 /
                        16777216.0);
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
