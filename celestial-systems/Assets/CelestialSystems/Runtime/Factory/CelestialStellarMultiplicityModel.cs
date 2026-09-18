/*
 * Produces deterministic, approximate stellar-companion architectures from primary birth conditions.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialStellarMultiplicity
    {
        Single = 0,
        Binary = 1,
        HierarchicalTriple = 2
    }

    public readonly struct CelestialStellarCompanionFormation
    {
        public CelestialStellarCompanionFormation(
            double massRatio,
            double birthMassSolar,
            double semiMajorAxisAstronomicalUnits,
            double eccentricity,
            double inclinationDegrees,
            bool orbitsInnerPairBarycenter)
        {
            MassRatio = massRatio;
            BirthMassSolar = birthMassSolar;
            SemiMajorAxisAstronomicalUnits =
                semiMajorAxisAstronomicalUnits;
            Eccentricity = eccentricity;
            InclinationDegrees = inclinationDegrees;
            OrbitsInnerPairBarycenter =
                orbitsInnerPairBarycenter;
        }

        public double MassRatio { get; }
        public double BirthMassSolar { get; }
        public double SemiMajorAxisAstronomicalUnits { get; }
        public double Eccentricity { get; }
        public double InclinationDegrees { get; }
        public bool OrbitsInnerPairBarycenter { get; }

        public double PeriapsisAstronomicalUnits =>
            SemiMajorAxisAstronomicalUnits *
            (1.0 - Eccentricity);

        public double ApoapsisAstronomicalUnits =>
            SemiMajorAxisAstronomicalUnits *
            (1.0 + Eccentricity);
    }

    public readonly struct CelestialStellarMultiplicityResult
    {
        public CelestialStellarMultiplicityResult(
            CelestialStellarMultiplicity multiplicity,
            CelestialStellarCompanionFormation[] companions)
        {
            Multiplicity = multiplicity;
            Companions = companions ??
                Array.Empty<CelestialStellarCompanionFormation>();
        }

        public CelestialStellarMultiplicity Multiplicity { get; }
        public CelestialStellarCompanionFormation[] Companions { get; }
        public int StarCount => Companions.Length + 1;
    }

    public static class CelestialStellarMultiplicityModel
    {
        public const int ModelVersion = 1;

        private const double MinimumStellarMassSolar = 0.08;

        public static bool TryGenerate(
            int seed,
            double primaryBirthMassSolar,
            double ironMetallicityDex,
            double birthStellarDensityPerCubicParsec,
            out CelestialStellarMultiplicityResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(primaryBirthMassSolar) ||
                !IsFiniteInRange(ironMetallicityDex, -4.0, 1.0) ||
                !IsFiniteNonNegative(birthStellarDensityPerCubicParsec))
            {
                error =
                    "Stellar multiplicity requires valid primary mass, metallicity, and birth-density values.";
                return false;
            }

            var random = new DeterministicRandom(seed);
            var multipleProbability =
                ResolveMultipleProbability(
                    primaryBirthMassSolar,
                    ironMetallicityDex);

            if (random.Next01() >= multipleProbability)
            {
                result =
                    new CelestialStellarMultiplicityResult(
                        CelestialStellarMultiplicity.Single,
                        Array.Empty<CelestialStellarCompanionFormation>());
                return true;
            }

            var triple =
                random.Next01() <
                ResolveTripleFractionAmongMultiples(
                    primaryBirthMassSolar);
            var inner =
                GenerateInnerCompanion(
                    ref random,
                    primaryBirthMassSolar,
                    birthStellarDensityPerCubicParsec);

            if (!triple)
            {
                result =
                    new CelestialStellarMultiplicityResult(
                        CelestialStellarMultiplicity.Binary,
                        new[]
                        {
                            inner
                        });
                return HasValidResult(result, out error);
            }

            var outer =
                GenerateOuterCompanion(
                    ref random,
                    primaryBirthMassSolar,
                    inner,
                    birthStellarDensityPerCubicParsec);
            result =
                new CelestialStellarMultiplicityResult(
                    CelestialStellarMultiplicity.HierarchicalTriple,
                    new[]
                    {
                        inner,
                        outer
                    });
            return HasValidResult(result, out error);
        }

        private static CelestialStellarCompanionFormation GenerateInnerCompanion(
            ref DeterministicRandom random,
            double primaryMassSolar,
            double birthDensity)
        {
            var massRatio =
                GenerateMassRatio(
                    ref random,
                    primaryMassSolar);
            var maximumAxis =
                ResolveEnvironmentLimitedMaximumAxis(
                    300.0,
                    birthDensity);
            var semiMajorAxis =
                RandomLogRange(
                    ref random,
                    0.03,
                    maximumAxis);
            var eccentricity =
                ResolveEccentricity(
                    ref random,
                    semiMajorAxis);

            return
                new CelestialStellarCompanionFormation(
                    massRatio,
                    primaryMassSolar * massRatio,
                    semiMajorAxis,
                    eccentricity,
                    random.NextRange(0.0, 15.0),
                    false);
        }

        private static CelestialStellarCompanionFormation GenerateOuterCompanion(
            ref DeterministicRandom random,
            double primaryMassSolar,
            CelestialStellarCompanionFormation inner,
            double birthDensity)
        {
            var massRatio =
                GenerateMassRatio(
                    ref random,
                    primaryMassSolar);
            var eccentricity =
                random.NextRange(0.0, 0.75);
            var minimumPeriapsis =
                inner.ApoapsisAstronomicalUnits *
                5.0;
            var minimumAxis =
                minimumPeriapsis /
                (1.0 - eccentricity);
            var maximumAxis =
                Math.Max(
                    minimumAxis * 1.25,
                    ResolveEnvironmentLimitedMaximumAxis(
                        5000.0,
                        birthDensity));
            var semiMajorAxis =
                RandomLogRange(
                    ref random,
                    minimumAxis,
                    maximumAxis);

            return
                new CelestialStellarCompanionFormation(
                    massRatio,
                    primaryMassSolar * massRatio,
                    semiMajorAxis,
                    eccentricity,
                    random.NextRange(20.0, 160.0),
                    true);
        }

        private static double ResolveMultipleProbability(
            double primaryMassSolar,
            double ironMetallicityDex)
        {
            double baseProbability;

            if (primaryMassSolar < 0.5)
            {
                baseProbability = 0.24;
            }
            else if (primaryMassSolar < 1.5)
            {
                baseProbability = 0.44;
            }
            else if (primaryMassSolar < 5.0)
            {
                baseProbability = 0.62;
            }
            else
            {
                baseProbability = 0.82;
            }

            var metallicityAdjustment =
                -0.05 *
                Math.Max(
                    -1.0,
                    Math.Min(
                        0.5,
                        ironMetallicityDex));
            return Clamp(
                baseProbability + metallicityAdjustment,
                0.1,
                0.95);
        }

        private static double ResolveTripleFractionAmongMultiples(
            double primaryMassSolar)
        {
            if (primaryMassSolar < 0.5)
            {
                return 0.06;
            }

            if (primaryMassSolar < 1.5)
            {
                return 0.12;
            }

            if (primaryMassSolar < 5.0)
            {
                return 0.2;
            }

            return 0.3;
        }

        private static double GenerateMassRatio(
            ref DeterministicRandom random,
            double primaryMassSolar)
        {
            var minimumRatio =
                Math.Min(
                    1.0,
                    Math.Max(
                        0.1,
                        MinimumStellarMassSolar /
                            primaryMassSolar));
            var selection =
                random.Next01();
            var shaped =
                primaryMassSolar < 0.5
                    ? Math.Sqrt(selection)
                    : primaryMassSolar >= 5.0
                        ? selection * selection
                        : selection;
            return
                minimumRatio +
                (1.0 - minimumRatio) *
                shaped;
        }

        private static double ResolveEnvironmentLimitedMaximumAxis(
            double isolatedMaximum,
            double birthDensity)
        {
            var densityCompression =
                1.0 /
                Math.Sqrt(
                    1.0 +
                    birthDensity /
                        100.0);
            return Math.Max(
                5.0,
                isolatedMaximum *
                    densityCompression);
        }

        private static double ResolveEccentricity(
            ref DeterministicRandom random,
            double semiMajorAxis)
        {
            if (semiMajorAxis < 0.1)
            {
                return random.NextRange(0.0, 0.08);
            }

            return
                Math.Sqrt(
                    random.Next01()) *
                0.8;
        }

        private static bool HasValidResult(
            CelestialStellarMultiplicityResult result,
            out string error)
        {
            if (result.Companions == null ||
                result.Companions.Length !=
                    result.StarCount - 1 ||
                result.Companions.Length > 2)
            {
                error =
                    "The multiplicity model produced an invalid companion count.";
                return false;
            }

            for (var index = 0;
                index < result.Companions.Length;
                index++)
            {
                var companion =
                    result.Companions[index];

                if (!IsFiniteInRange(companion.MassRatio, 0.0, 1.0) ||
                    companion.MassRatio <= 0.0 ||
                    companion.BirthMassSolar < MinimumStellarMassSolar ||
                    !IsFinitePositive(companion.SemiMajorAxisAstronomicalUnits) ||
                    !IsFiniteInRange(companion.Eccentricity, 0.0, 1.0) ||
                    companion.Eccentricity >= 1.0 ||
                    !IsFiniteInRange(companion.InclinationDegrees, 0.0, 180.0))
                {
                    error =
                        "The multiplicity model produced invalid companion properties.";
                    return false;
                }
            }

            if (result.Multiplicity ==
                    CelestialStellarMultiplicity.HierarchicalTriple &&
                result.Companions[1].PeriapsisAstronomicalUnits <=
                    result.Companions[0].ApoapsisAstronomicalUnits *
                        5.0)
            {
                error =
                    "The multiplicity model produced an unstable stellar hierarchy.";
                return false;
            }

            error = string.Empty;
            return true;
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

        private static bool IsFinitePositive(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static bool IsFiniteNonNegative(double value)
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

            public DeterministicRandom(int seed)
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
