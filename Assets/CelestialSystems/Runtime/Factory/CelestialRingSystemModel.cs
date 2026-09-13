/*
 * Produces deterministic ring-system data after moon formation so ring
 * boundaries can respect the finished major-moon architecture.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialRingFormationOrigin
    {
        PrimordialDebris = 0,
        DisruptedSatellite = 1,
        ImpactDebris = 2
    }

    public enum CelestialRingSystemMorphology
    {
        Broad = 0,
        NarrowDark = 1
    }

    public readonly struct CelestialRingDivision
    {
        public CelestialRingDivision(
            double centerFraction,
            double halfWidthFraction,
            double densityMultiplier)
        {
            CenterFraction = centerFraction;
            HalfWidthFraction = halfWidthFraction;
            DensityMultiplier = densityMultiplier;
        }

        public double CenterFraction { get; }

        public double HalfWidthFraction { get; }

        public double DensityMultiplier { get; }
    }

    public readonly struct CelestialRingBand
    {
        public CelestialRingBand(
            double innerRadiusMeters,
            double outerRadiusMeters,
            CelestialRingDivision[] divisions = null)
        {
            InnerRadiusMeters = innerRadiusMeters;
            OuterRadiusMeters = outerRadiusMeters;
            Divisions = divisions ?? Array.Empty<CelestialRingDivision>();
        }

        public double InnerRadiusMeters { get; }

        public double OuterRadiusMeters { get; }

        public CelestialRingDivision[] Divisions { get; }
    }

    public readonly struct CelestialRingSystemResult
    {
        public CelestialRingSystemResult(
            bool hasRings,
            CelestialRingFormationOrigin origin,
            CelestialRingSystemMorphology morphology,
            double innerRadiusMeters,
            double outerRadiusMeters,
            double opticalDepth,
            double iceMassFraction,
            CelestialRingBand[] bands,
            int outerShepherdMoonCount)
        {
            HasRings = hasRings;
            Origin = origin;
            Morphology = morphology;
            InnerRadiusMeters = innerRadiusMeters;
            OuterRadiusMeters = outerRadiusMeters;
            OpticalDepth = opticalDepth;
            IceMassFraction = iceMassFraction;
            Bands = bands ?? Array.Empty<CelestialRingBand>();
            OuterShepherdMoonCount = outerShepherdMoonCount;
        }

        public bool HasRings { get; }

        public CelestialRingFormationOrigin Origin { get; }

        public CelestialRingSystemMorphology Morphology { get; }

        public double InnerRadiusMeters { get; }

        public double OuterRadiusMeters { get; }

        public double OpticalDepth { get; }

        public double IceMassFraction { get; }

        public CelestialRingBand[] Bands { get; }

        public int OuterShepherdMoonCount { get; }
    }

    public static class CelestialRingSystemModel
    {
        public const int ModelVersion = 1;

        private const double EarthRadiusMeters = 6371000.0;
        private const double EarthMeanDensityKilogramsPerCubicMeter = 5514.0;

        public static bool TryGenerate(
            int seed,
            CelestialPlanetFormationResult planet,
            CelestialMoonSystemFormationResult moonSystem,
            out CelestialRingSystemResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!HasValidPlanet(planet) ||
                !HasValidMoonSystem(moonSystem))
            {
                error =
                    "Ring formation requires valid planet and moon-system properties.";
                return false;
            }

            var random = new RingDeterministicRandom(seed ^ 0x4A17C3D);
            var chance = SelectRingChance(planet);

            if (chance <= 0.0 || random.Next01() >= chance)
            {
                result = Empty();
                return true;
            }

            var planetRadiusMeters = planet.RadiusEarth * EarthRadiusMeters;
            var planetDensity =
                EarthMeanDensityKilogramsPerCubicMeter *
                planet.TotalMassEarth /
                Math.Pow(planet.RadiusEarth, 3.0);
            var particleDensity =
                random.NextRange(650.0, 1250.0);
            var rocheLimitMeters =
                2.44 * planetRadiusMeters *
                Math.Pow(planetDensity / particleDensity, 1.0 / 3.0);
            var innerRadiusMeters =
                planetRadiusMeters *
                random.NextRange(1.25, 1.75);
            var outerRadiusMeters =
                rocheLimitMeters *
                random.NextRange(0.72, 0.96);

            var firstMoonPeriapsisMeters =
                FindFirstMoonPeriapsis(moonSystem.Moons);

            if (firstMoonPeriapsisMeters > 0.0)
            {
                outerRadiusMeters =
                    Math.Min(
                        outerRadiusMeters,
                        firstMoonPeriapsisMeters * 0.88);
            }

            if (!IsFinitePositive(innerRadiusMeters) ||
                !IsFinitePositive(outerRadiusMeters) ||
                outerRadiusMeters <= innerRadiusMeters * 1.18)
            {
                result = Empty();
                return true;
            }

            var origin = SelectOrigin(planet, firstMoonPeriapsisMeters, rocheLimitMeters, ref random);
            var morphology = SelectMorphology(planet, origin, ref random);
            var bandCount =
                morphology == CelestialRingSystemMorphology.NarrowDark
                    ? random.NextInclusive(2, 5)
                    : outerRadiusMeters > innerRadiusMeters * 1.45 &&
                        random.Next01() < 0.48
                        ? 2
                        : 1;
            var bands = morphology == CelestialRingSystemMorphology.NarrowDark
                ? BuildNarrowBands(innerRadiusMeters, outerRadiusMeters, bandCount, ref random)
                : BuildBands(innerRadiusMeters, outerRadiusMeters, bandCount, ref random);
            var opticalDepth =
                morphology == CelestialRingSystemMorphology.NarrowDark
                    ? random.NextRange(0.04, 0.22)
                    : SelectOpticalDepth(planet, origin, ref random);
            var iceMassFraction =
                morphology == CelestialRingSystemMorphology.NarrowDark
                    ? random.NextRange(0.02, 0.24)
                    : SelectIceMassFraction(planet, origin, ref random);
            for (var index = 0; index < bands.Length; index++)
            {
                bands[index] = new CelestialRingBand(
                    bands[index].InnerRadiusMeters,
                    bands[index].OuterRadiusMeters,
                    CreateDivisions(
                        origin,
                        bands[index],
                        ref random));
            }

            var shepherdCount =
                CountOuterShepherds(
                    moonSystem.Moons,
                    outerRadiusMeters);

            result = new CelestialRingSystemResult(
                true,
                origin,
                morphology,
                innerRadiusMeters,
                outerRadiusMeters,
                opticalDepth,
                iceMassFraction,
                bands,
                shepherdCount);

            if (!IsValid(result))
            {
                error =
                    "Ring formation produced invalid ring-system properties.";
                result = default;
                return false;
            }

            return true;
        }

        private static CelestialRingSystemResult Empty()
        {
            return new CelestialRingSystemResult(
                false,
                CelestialRingFormationOrigin.PrimordialDebris,
                CelestialRingSystemMorphology.Broad,
                0.0,
                0.0,
                0.0,
                0.0,
                Array.Empty<CelestialRingBand>(),
                0);
        }

        private static double SelectRingChance(
            CelestialPlanetFormationResult planet)
        {
            switch (planet.FormationClass)
            {
                case CelestialPlanetFormationClass.Giant:
                    return 0.68;
                case CelestialPlanetFormationClass.GasRich:
                    return 0.42;
                case CelestialPlanetFormationClass.VolatileRich:
                    return planet.TotalMassEarth >= 2.0 ? 0.18 : 0.06;
                case CelestialPlanetFormationClass.Rocky:
                    return planet.TotalMassEarth >= 0.3 ? 0.07 : 0.0;
                default:
                    return 0.0;
            }
        }

        private static CelestialRingFormationOrigin SelectOrigin(
            CelestialPlanetFormationResult planet,
            double firstMoonPeriapsisMeters,
            double rocheLimitMeters,
            ref RingDeterministicRandom random)
        {
            if (firstMoonPeriapsisMeters > 0.0 &&
                firstMoonPeriapsisMeters < rocheLimitMeters * 1.45 &&
                random.Next01() < 0.55)
            {
                return CelestialRingFormationOrigin.DisruptedSatellite;
            }

            if (planet.FormationClass == CelestialPlanetFormationClass.Rocky)
            {
                return CelestialRingFormationOrigin.ImpactDebris;
            }

            return random.Next01() < 0.18
                ? CelestialRingFormationOrigin.ImpactDebris
                : CelestialRingFormationOrigin.PrimordialDebris;
        }

        private static CelestialRingSystemMorphology SelectMorphology(
            CelestialPlanetFormationResult planet,
            CelestialRingFormationOrigin origin,
            ref RingDeterministicRandom random)
        {
            // Prototype calibration: sparse dark-ring occurrence by broad
            // formation class until disruption history is modeled directly.
            var chance =
                planet.FormationClass == CelestialPlanetFormationClass.VolatileRich
                    ? 0.12
                    : planet.FormationClass == CelestialPlanetFormationClass.GasRich
                        ? 0.05
                        : planet.FormationClass == CelestialPlanetFormationClass.Giant
                            ? 0.025
                            : 0.04;

            if (origin == CelestialRingFormationOrigin.ImpactDebris)
            {
                chance *= 1.5;
            }

            return random.Next01() < chance
                ? CelestialRingSystemMorphology.NarrowDark
                : CelestialRingSystemMorphology.Broad;
        }

        private static CelestialRingBand[] BuildNarrowBands(
            double innerRadiusMeters,
            double outerRadiusMeters,
            int bandCount,
            ref RingDeterministicRandom random)
        {
            var result = new CelestialRingBand[bandCount];
            var span = outerRadiusMeters - innerRadiusMeters;

            for (var index = 0; index < bandCount; index++)
            {
                var center = innerRadiusMeters +
                    span * ((index + 0.5) / bandCount);
                var width = span *
                    random.NextRange(0.025, 0.075) /
                    bandCount;
                result[index] = new CelestialRingBand(
                    center - width * 0.5,
                    center + width * 0.5);
            }

            return result;
        }

        private static CelestialRingBand[] BuildBands(
            double innerRadiusMeters,
            double outerRadiusMeters,
            int bandCount,
            ref RingDeterministicRandom random)
        {
            if (bandCount == 1)
            {
                return new[]
                {
                    new CelestialRingBand(
                        innerRadiusMeters,
                        outerRadiusMeters)
                };
            }

            var gapCenter =
                innerRadiusMeters +
                (outerRadiusMeters - innerRadiusMeters) *
                random.NextRange(0.38, 0.62);
            var gapWidth =
                (outerRadiusMeters - innerRadiusMeters) *
                random.NextRange(0.035, 0.10);

            return new[]
            {
                new CelestialRingBand(
                    innerRadiusMeters,
                    gapCenter - gapWidth * 0.5),
                new CelestialRingBand(
                    gapCenter + gapWidth * 0.5,
                    outerRadiusMeters)
            };
        }

        private static CelestialRingDivision[] CreateDivisions(
            CelestialRingFormationOrigin origin,
            CelestialRingBand band,
            ref RingDeterministicRandom random)
        {
            // Prototype division counts and widths; later moon resonances will
            // supply specific gap locations instead of this stochastic detail.
            var count =
                origin == CelestialRingFormationOrigin.DisruptedSatellite
                    ? random.NextInclusive(1, 4)
                    : origin == CelestialRingFormationOrigin.PrimordialDebris
                        ? random.NextInclusive(0, 3)
                        : random.NextInclusive(0, 2);
            var result = new CelestialRingDivision[count];

            for (var index = 0; index < count; index++)
            {
                result[index] = new CelestialRingDivision(
                    random.NextRange(0.08, 0.92),
                    random.NextRange(0.006, 0.035),
                    random.NextRange(0.0, 0.12));
            }

            return result;
        }

        private static double SelectOpticalDepth(
            CelestialPlanetFormationResult planet,
            CelestialRingFormationOrigin origin,
            ref RingDeterministicRandom random)
        {
            var baseDepth =
                origin == CelestialRingFormationOrigin.DisruptedSatellite
                    ? 0.65
                    : origin == CelestialRingFormationOrigin.ImpactDebris
                        ? 0.30
                        : 0.48;

            if (planet.FormationClass == CelestialPlanetFormationClass.Giant)
            {
                baseDepth += 0.10;
            }

            return Clamp01(baseDepth + random.NextRange(-0.18, 0.18));
        }

        private static double SelectIceMassFraction(
            CelestialPlanetFormationResult planet,
            CelestialRingFormationOrigin origin,
            ref RingDeterministicRandom random)
        {
            var baseFraction =
                planet.FormationClass == CelestialPlanetFormationClass.Giant
                    ? 0.72
                    : planet.FormationClass == CelestialPlanetFormationClass.GasRich
                        ? 0.55
                        : planet.FormationClass == CelestialPlanetFormationClass.VolatileRich
                            ? 0.38
                            : 0.12;

            if (origin == CelestialRingFormationOrigin.ImpactDebris)
            {
                baseFraction *= 0.55;
            }

            return Clamp01(baseFraction + random.NextRange(-0.16, 0.16));
        }

        private static int CountOuterShepherds(
            CelestialMoonFormationResult[] moons,
            double outerRingRadiusMeters)
        {
            var result = 0;

            foreach (var moon in moons)
            {
                var periapsis =
                    moon.OrbitalRadiusMeters *
                    (1.0 - moon.Eccentricity);

                if (periapsis >= outerRingRadiusMeters * 1.02 &&
                    periapsis <= outerRingRadiusMeters * 1.35 &&
                    moon.InclinationDegrees <= 8.0)
                {
                    result++;
                }
            }

            return result;
        }

        private static double FindFirstMoonPeriapsis(
            CelestialMoonFormationResult[] moons)
        {
            var result = double.MaxValue;

            foreach (var moon in moons)
            {
                var periapsis =
                    moon.OrbitalRadiusMeters *
                    (1.0 - moon.Eccentricity);

                if (IsFinitePositive(periapsis))
                {
                    result = Math.Min(result, periapsis);
                }
            }

            return result == double.MaxValue ? 0.0 : result;
        }

        private static bool HasValidPlanet(
            CelestialPlanetFormationResult planet)
        {
            return IsFinitePositive(planet.TotalMassEarth) &&
                IsFinitePositive(planet.RadiusEarth) &&
                planet.FormationClass >= CelestialPlanetFormationClass.Rocky &&
                planet.FormationClass <= CelestialPlanetFormationClass.Giant;
        }

        private static bool HasValidMoonSystem(
            CelestialMoonSystemFormationResult system)
        {
            if (!IsFinitePositive(system.HillRadiusMeters) ||
                system.Moons == null)
            {
                return false;
            }

            foreach (var moon in system.Moons)
            {
                if (!IsFinitePositive(moon.OrbitalRadiusMeters) ||
                    !IsFinitePositive(moon.RocheLimitMeters) ||
                    moon.Eccentricity < 0.0 ||
                    moon.Eccentricity >= 1.0)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsValid(CelestialRingSystemResult result)
        {
            if (!result.HasRings)
            {
                return result.Bands.Length == 0;
            }

            if (!IsFinitePositive(result.InnerRadiusMeters) ||
                !IsFinitePositive(result.OuterRadiusMeters) ||
                result.OuterRadiusMeters <= result.InnerRadiusMeters ||
                !IsFraction(result.OpticalDepth) ||
                !IsFraction(result.IceMassFraction) ||
                result.Bands.Length < 1)
            {
                return false;
            }

            var previousOuter = result.InnerRadiusMeters;

            foreach (var band in result.Bands)
            {
                if (!IsFinitePositive(band.InnerRadiusMeters) ||
                    !IsFinitePositive(band.OuterRadiusMeters) ||
                    band.InnerRadiusMeters < previousOuter ||
                    band.OuterRadiusMeters <= band.InnerRadiusMeters ||
                    band.OuterRadiusMeters > result.OuterRadiusMeters)
                {
                    return false;
                }

                previousOuter = band.OuterRadiusMeters;
            }

            return true;
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static bool IsFraction(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0.0 &&
                value <= 1.0;
        }

        private static double Clamp01(double value)
        {
            return Math.Max(0.0, Math.Min(1.0, value));
        }

        // Kept local so this model has no dependency on the guide's private
        // generation stream or its draw order.
        private struct RingDeterministicRandom
        {
            private uint state;

            public RingDeterministicRandom(int seed)
            {
                state = unchecked((uint)seed);

                if (state == 0)
                {
                    state = 0x6D2B79F5u;
                }
            }

            public int NextInclusive(
                int minimum,
                int maximum)
            {
                return minimum +
                    (int)(NextUInt() %
                        (uint)(maximum - minimum + 1));
            }

            public double Next01()
            {
                return (NextUInt() >> 8) *
                    (1.0 / 16777216.0);
            }

            public double NextRange(
                double minimum,
                double maximum)
            {
                return minimum +
                    (maximum - minimum) * Next01();
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
