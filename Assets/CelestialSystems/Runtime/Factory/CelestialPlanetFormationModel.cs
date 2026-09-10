/*
 * Converts a star's disk and orbital slots into deterministic, approximate planet-formation outcomes.
 */

using System;
using System.Collections.Generic;

namespace jcan.CelestialSystems
{
    public enum CelestialPlanetFormationClass
    {
        Rocky = 0,
        VolatileRich = 1,
        GasRich = 2,
        Giant = 3
    }

    public readonly struct CelestialPlanetFormationResult
    {
        public CelestialPlanetFormationResult(
            CelestialPlanetFormationClass formationClass,
            double initialOrbitAstronomicalUnits,
            double finalOrbitAstronomicalUnits,
            double solidCoreMassEarth,
            double totalMassEarth,
            double radiusEarth,
            double volatileMassFraction,
            double hydrogenHeliumEnvelopeFraction,
            double inwardMigrationFraction)
        {
            FormationClass = formationClass;
            InitialOrbitAstronomicalUnits =
                initialOrbitAstronomicalUnits;
            FinalOrbitAstronomicalUnits =
                finalOrbitAstronomicalUnits;
            SolidCoreMassEarth = solidCoreMassEarth;
            TotalMassEarth = totalMassEarth;
            RadiusEarth = radiusEarth;
            VolatileMassFraction = volatileMassFraction;
            HydrogenHeliumEnvelopeFraction =
                hydrogenHeliumEnvelopeFraction;
            InwardMigrationFraction =
                inwardMigrationFraction;
        }

        public CelestialPlanetFormationClass FormationClass { get; }

        public double InitialOrbitAstronomicalUnits { get; }

        public double FinalOrbitAstronomicalUnits { get; }

        public double SolidCoreMassEarth { get; }

        public double TotalMassEarth { get; }

        public double RadiusEarth { get; }

        public double VolatileMassFraction { get; }

        public double HydrogenHeliumEnvelopeFraction { get; }

        public double InwardMigrationFraction { get; }
    }

    public static class CelestialPlanetFormationModel
    {
        public const int ModelVersion = 1;

        public static bool TryGenerate(
            int seed,
            CelestialProtoplanetaryDiskResult disk,
            CelestialStellarPopulationSample population,
            IReadOnlyList<double> initialOrbitAstronomicalUnits,
            out CelestialPlanetFormationResult[] planets,
            out string error)
        {
            planets =
                Array.Empty<CelestialPlanetFormationResult>();
            error = string.Empty;

            if (!IsFinitePositive(
                    disk.InitialGasMassSolar) ||
                !IsFinitePositive(
                    disk.InitialSolidMassEarth) ||
                !IsFinitePositive(
                    disk.FrostLineAstronomicalUnits) ||
                !IsFinitePositive(
                    disk.PlanetFormationWindowMegayears) ||
                !IsFinitePositive(
                    population.InitialMassSolar))
            {
                error =
                    "Planet formation requires valid disk and stellar formation properties.";
                return false;
            }

            if (initialOrbitAstronomicalUnits == null ||
                initialOrbitAstronomicalUnits.Count == 0)
            {
                return true;
            }

            var random =
                new DeterministicRandom(
                    seed);
            var weights =
                new double[
                    initialOrbitAstronomicalUnits.Count];
            var totalWeight =
                0.0;

            for (var index = 0;
                index < weights.Length;
                index++)
            {
                var orbitAu =
                    initialOrbitAstronomicalUnits[index];

                if (!IsFinitePositive(
                        orbitAu))
                {
                    error =
                        $"Planet formation received an invalid orbital slot at index {index}.";
                    return false;
                }

                var iceEnhancement =
                    orbitAu >=
                        disk.FrostLineAstronomicalUnits
                            ? 2.5
                            : 1.0;
                weights[index] =
                    iceEnhancement *
                    Math.Pow(
                        orbitAu,
                        -0.5) *
                    random.NextRange(
                        0.65,
                        1.45);
                totalWeight +=
                    weights[index];
            }

            if (!IsFinitePositive(
                    totalWeight))
            {
                error =
                    "Planet formation could not allocate the disk's solid material.";
                return false;
            }

            var formationEfficiency =
                ResolveFormationEfficiency(
                    disk.FormationCapacity) *
                random.NextRange(
                    0.8,
                    1.2);
            formationEfficiency =
                Clamp(
                    formationEfficiency,
                    0.05,
                    0.8);
            var capturedSolidMassEarth =
                disk.InitialSolidMassEarth *
                formationEfficiency;
            var generated =
                new CelestialPlanetFormationResult[
                    weights.Length];

            for (var index = 0;
                index < generated.Length;
                index++)
            {
                var initialOrbitAu =
                    initialOrbitAstronomicalUnits[index];
                var solidCoreMassEarth =
                    capturedSolidMassEarth *
                    weights[index] /
                    totalWeight;
                var beyondFrostLine =
                    initialOrbitAu >=
                        disk.FrostLineAstronomicalUnits;
                var volatileFraction =
                    beyondFrostLine
                        ? random.NextRange(
                            0.3,
                            0.65)
                        : random.NextRange(
                            0.0,
                            0.08);
                var envelopeFraction =
                    ResolveEnvelopeFraction(
                        solidCoreMassEarth,
                        disk.PlanetFormationWindowMegayears,
                        beyondFrostLine,
                        ref random);
                var totalMassEarth =
                    solidCoreMassEarth /
                    (1.0 -
                        envelopeFraction);
                var migrationPotential =
                    Clamp01(
                        disk.PlanetFormationWindowMegayears /
                            5.0 *
                        Math.Sqrt(
                            disk.InitialGasMassSolar /
                            0.03));
                var migrationFraction =
                    random.Next01() <
                        migrationPotential *
                        0.6
                            ? random.NextRange(
                                0.02,
                                0.55) *
                                migrationPotential
                            : 0.0;
                var finalOrbitAu =
                    Math.Max(
                        disk.InnerBoundaryAstronomicalUnits,
                        initialOrbitAu *
                        (1.0 -
                            migrationFraction));
                var formationClass =
                    ResolveFormationClass(
                        volatileFraction,
                        envelopeFraction);
                var radiusEarth =
                    EstimateRadiusEarth(
                        formationClass,
                        totalMassEarth,
                        volatileFraction,
                        envelopeFraction);

                generated[index] =
                    new CelestialPlanetFormationResult(
                        formationClass,
                        initialOrbitAu,
                        finalOrbitAu,
                        solidCoreMassEarth,
                        totalMassEarth,
                        radiusEarth,
                        volatileFraction,
                        envelopeFraction,
                        migrationFraction);

                if (!HasValidResult(
                        generated[index]))
                {
                    error =
                        $"The planet-formation model produced invalid properties at index {index}.";
                    planets =
                        Array.Empty<CelestialPlanetFormationResult>();
                    return false;
                }
            }

            planets = generated;
            return true;
        }

        private static double ResolveFormationEfficiency(
            CelestialPlanetFormationCapacity capacity)
        {
            switch (capacity)
            {
                case CelestialPlanetFormationCapacity.StronglyInhibited:
                    return 0.15;
                case CelestialPlanetFormationCapacity.Low:
                    return 0.28;
                case CelestialPlanetFormationCapacity.Typical:
                    return 0.45;
                case CelestialPlanetFormationCapacity.High:
                    return 0.6;
                default:
                    return 0.1;
            }
        }

        private static double ResolveEnvelopeFraction(
            double solidCoreMassEarth,
            double formationWindowMegayears,
            bool beyondFrostLine,
            ref DeterministicRandom random)
        {
            if (formationWindowMegayears < 0.75 ||
                solidCoreMassEarth < 1.5)
            {
                return 0.0;
            }

            var timeFactor =
                Clamp01(
                    (formationWindowMegayears -
                        0.75) /
                    3.0);
            var locationFactor =
                beyondFrostLine
                    ? 1.0
                    : 0.55;

            if (solidCoreMassEarth >= 8.0)
            {
                var runawayChance =
                    Clamp01(
                        (solidCoreMassEarth -
                            6.0) /
                        8.0 *
                        timeFactor *
                        locationFactor);

                if (random.Next01() <
                    runawayChance)
                {
                    return
                        random.NextRange(
                            0.7,
                            0.96);
                }
            }

            var retainedEnvelope =
                (solidCoreMassEarth -
                    1.5) /
                30.0 *
                timeFactor *
                locationFactor *
                random.NextRange(
                    0.6,
                    1.4);

            return
                Clamp(
                    retainedEnvelope,
                    0.0,
                    0.35);
        }

        private static CelestialPlanetFormationClass ResolveFormationClass(
            double volatileFraction,
            double envelopeFraction)
        {
            if (envelopeFraction >= 0.5)
            {
                return
                    CelestialPlanetFormationClass.Giant;
            }

            if (envelopeFraction >= 0.01)
            {
                return
                    CelestialPlanetFormationClass.GasRich;
            }

            return
                volatileFraction >= 0.2
                    ? CelestialPlanetFormationClass.VolatileRich
                    : CelestialPlanetFormationClass.Rocky;
        }

        private static double EstimateRadiusEarth(
            CelestialPlanetFormationClass formationClass,
            double totalMassEarth,
            double volatileFraction,
            double envelopeFraction)
        {
            switch (formationClass)
            {
                case CelestialPlanetFormationClass.Rocky:
                    const double coreMassFraction = 0.3;
                    return
                        (1.07 -
                            0.21 *
                            coreMassFraction) *
                        Math.Pow(
                            totalMassEarth,
                            1.0 /
                            3.7);

                case CelestialPlanetFormationClass.VolatileRich:
                    return
                        (1.08 +
                            0.35 *
                            volatileFraction) *
                        Math.Pow(
                            totalMassEarth,
                            0.27);

                case CelestialPlanetFormationClass.GasRich:
                    return
                        (1.45 +
                            2.0 *
                            envelopeFraction) *
                        Math.Pow(
                            totalMassEarth,
                            0.2);

                case CelestialPlanetFormationClass.Giant:
                    return
                        Clamp(
                            10.5 *
                            Math.Pow(
                                totalMassEarth /
                                    100.0,
                                0.01),
                            8.0,
                            13.0);

                default:
                    return 0.0;
            }
        }

        private static bool HasValidResult(
            CelestialPlanetFormationResult result)
        {
            return
                IsFinitePositive(
                    result.InitialOrbitAstronomicalUnits) &&
                IsFinitePositive(
                    result.FinalOrbitAstronomicalUnits) &&
                result.FinalOrbitAstronomicalUnits <=
                    result.InitialOrbitAstronomicalUnits &&
                IsFinitePositive(
                    result.SolidCoreMassEarth) &&
                IsFinitePositive(
                    result.TotalMassEarth) &&
                result.TotalMassEarth >=
                    result.SolidCoreMassEarth &&
                IsFinitePositive(
                    result.RadiusEarth) &&
                IsFraction(
                    result.VolatileMassFraction) &&
                IsFraction(
                    result.HydrogenHeliumEnvelopeFraction) &&
                IsFraction(
                    result.InwardMigrationFraction);
        }

        private static bool IsFraction(
            double value)
        {
            return
                double.IsNaN(value) == false && double.IsInfinity(value) == false &&
                value >= 0.0 &&
                value <= 1.0;
        }

        private static double Clamp01(
            double value)
        {
            return
                Clamp(
                    value,
                    0.0,
                    1.0);
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
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
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

            public double Next01()
            {
                return
                    (NextUInt() >> 8) *
                    (1.0 / 16777216.0);
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
