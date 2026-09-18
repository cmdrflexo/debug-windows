/*
 * Applies approximate post-main-sequence stellar evolution to a formed planet's orbit and survival.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum CelestialPostMainSequencePlanetOutcome
    {
        Unchanged = 0,
        ExpandedAfterMassLoss = 1,
        EngulfedDuringGiantPhase = 2,
        DisruptedByCoreCollapse = 3
    }

    public readonly struct CelestialPostMainSequencePlanetResult
    {
        public CelestialPostMainSequencePlanetResult(
            CelestialPostMainSequencePlanetOutcome outcome,
            double formationOrbitAstronomicalUnits,
            double presentOrbitAstronomicalUnits,
            double orbitalExpansionFactor,
            double estimatedMaximumGiantRadiusAstronomicalUnits,
            double minimumSurvivalPeriapsisAstronomicalUnits)
        {
            Outcome = outcome;
            FormationOrbitAstronomicalUnits =
                formationOrbitAstronomicalUnits;
            PresentOrbitAstronomicalUnits =
                presentOrbitAstronomicalUnits;
            OrbitalExpansionFactor =
                orbitalExpansionFactor;
            EstimatedMaximumGiantRadiusAstronomicalUnits =
                estimatedMaximumGiantRadiusAstronomicalUnits;
            MinimumSurvivalPeriapsisAstronomicalUnits =
                minimumSurvivalPeriapsisAstronomicalUnits;
        }

        public CelestialPostMainSequencePlanetOutcome Outcome { get; }

        public double FormationOrbitAstronomicalUnits { get; }

        public double PresentOrbitAstronomicalUnits { get; }

        public double OrbitalExpansionFactor { get; }

        public double EstimatedMaximumGiantRadiusAstronomicalUnits { get; }

        public double MinimumSurvivalPeriapsisAstronomicalUnits { get; }

        public bool Survived =>
            Outcome ==
                CelestialPostMainSequencePlanetOutcome.Unchanged ||
            Outcome ==
                CelestialPostMainSequencePlanetOutcome.ExpandedAfterMassLoss;
    }

    public static class CelestialPostMainSequenceSystemEvolutionModel
    {
        public const int ModelVersion = 1;

        private const double GiantEnvelopeSafetyFactor =
            1.15;

        public static bool TryEvaluatePlanet(
            CelestialStellarEvolutionResult star,
            double formationOrbitAstronomicalUnits,
            double eccentricity,
            out CelestialPostMainSequencePlanetResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (!IsFinitePositive(
                    star.InitialMassSolar) ||
                !IsFinitePositive(
                    star.CurrentMassSolar) ||
                !IsFinitePositive(
                    formationOrbitAstronomicalUnits) ||
                !IsFinite(
                    eccentricity) ||
                eccentricity < 0.0 ||
                eccentricity >= 1.0)
            {
                error =
                    "Post-main-sequence system evolution requires valid stellar masses, a positive formation orbit, and eccentricity from zero up to, but not including, one.";
                return false;
            }

            switch (star.EvolutionState)
            {
                case CelestialStellarEvolutionState.MainSequence:
                    result =
                        new CelestialPostMainSequencePlanetResult(
                            CelestialPostMainSequencePlanetOutcome.Unchanged,
                            formationOrbitAstronomicalUnits,
                            formationOrbitAstronomicalUnits,
                            1.0,
                            0.0,
                            0.0);
                    return true;

                case CelestialStellarEvolutionState.WhiteDwarf:
                    return
                        TryEvaluateWhiteDwarfPlanet(
                            star,
                            formationOrbitAstronomicalUnits,
                            eccentricity,
                            out result,
                            out error);

                case CelestialStellarEvolutionState.NeutronStar:
                case CelestialStellarEvolutionState.BlackHole:
                    result =
                        new CelestialPostMainSequencePlanetResult(
                            CelestialPostMainSequencePlanetOutcome.DisruptedByCoreCollapse,
                            formationOrbitAstronomicalUnits,
                            0.0,
                            0.0,
                            0.0,
                            0.0);
                    return true;

                default:
                    error =
                        $"Unsupported stellar evolution state '{star.EvolutionState}'.";
                    return false;
            }
        }

        private static bool TryEvaluateWhiteDwarfPlanet(
            CelestialStellarEvolutionResult star,
            double formationOrbitAstronomicalUnits,
            double eccentricity,
            out CelestialPostMainSequencePlanetResult result,
            out string error)
        {
            result = default;
            error = string.Empty;

            if (star.CurrentMassSolar >=
                star.InitialMassSolar)
            {
                error =
                    "White-dwarf system evolution requires the remnant mass to be lower than the progenitor mass.";
                return false;
            }

            var maximumGiantRadiusAu =
                EstimateMaximumGiantRadiusAstronomicalUnits(
                    star.InitialMassSolar);
            var minimumSurvivalPeriapsisAu =
                maximumGiantRadiusAu *
                GiantEnvelopeSafetyFactor;
            var formationPeriapsisAu =
                formationOrbitAstronomicalUnits *
                (1.0 -
                    eccentricity);

            if (formationPeriapsisAu <=
                minimumSurvivalPeriapsisAu)
            {
                result =
                    new CelestialPostMainSequencePlanetResult(
                        CelestialPostMainSequencePlanetOutcome.EngulfedDuringGiantPhase,
                        formationOrbitAstronomicalUnits,
                        0.0,
                        0.0,
                        maximumGiantRadiusAu,
                        minimumSurvivalPeriapsisAu);
                return true;
            }

            var expansionFactor =
                star.InitialMassSolar /
                star.CurrentMassSolar;
            var presentOrbitAu =
                formationOrbitAstronomicalUnits *
                expansionFactor;

            if (!IsFinitePositive(
                    presentOrbitAu) ||
                !IsFinitePositive(
                    expansionFactor))
            {
                error =
                    "Post-main-sequence system evolution produced an invalid expanded orbit.";
                result = default;
                return false;
            }

            result =
                new CelestialPostMainSequencePlanetResult(
                    CelestialPostMainSequencePlanetOutcome.ExpandedAfterMassLoss,
                    formationOrbitAstronomicalUnits,
                    presentOrbitAu,
                    expansionFactor,
                    maximumGiantRadiusAu,
                    minimumSurvivalPeriapsisAu);
            return true;
        }

        private static double EstimateMaximumGiantRadiusAstronomicalUnits(
            double initialMassSolar)
        {
            if (initialMassSolar <= 1.0)
            {
                return 1.2;
            }

            if (initialMassSolar <= 2.0)
            {
                return
                    Lerp(
                        1.2,
                        2.0,
                        initialMassSolar -
                            1.0);
            }

            if (initialMassSolar <= 4.0)
            {
                return
                    Lerp(
                        2.0,
                        3.5,
                        (initialMassSolar -
                            2.0) /
                        2.0);
            }

            return
                Lerp(
                    3.5,
                    5.0,
                    Clamp01(
                        (initialMassSolar -
                            4.0) /
                        4.0));
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
            return
                Math.Max(
                    0.0,
                    Math.Min(
                        1.0,
                        value));
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
    }
}
