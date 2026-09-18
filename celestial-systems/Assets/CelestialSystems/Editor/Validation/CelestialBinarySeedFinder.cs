/*
 * Finds randomized deterministic seeds that produce visible binary systems.
 */

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialBinarySeedFinder
    {
        private const string EnvironmentPath =
            "Assets/CelestialSystems/Definitions/Galactic Environments/Solar-Neighborhood Thin Disk.asset";
        private const int TargetCount = 10;
        private const int MaximumAttempts = 100000;

        [MenuItem("Tools/Celestial Systems/Find 10 Binary Seeds")]
        public static void Find()
        {
            var environment =
                AssetDatabase.LoadAssetAtPath<CelestialGalacticEnvironmentDefinition>(
                    EnvironmentPath);

            if (environment == null)
            {
                Debug.LogError(
                    "Binary seed finder could not load the Solar-Neighborhood Thin Disk environment.");
                return;
            }

            var random =
                new System.Random(
                    unchecked(
                        Environment.TickCount ^
                        Guid.NewGuid().GetHashCode()));
            var output =
                new StringBuilder(
                    "Binary seeds (2 visible stars):");
            var found = 0;

            for (var attempt = 0;
                attempt < MaximumAttempts && found < TargetCount;
                attempt++)
            {
                var seed = random.Next(
                    1,
                    int.MaxValue);

                if (!CelestialStellarPopulationSampler.TrySamplePrimary(
                        seed,
                        environment,
                        out var primaryPopulation,
                        out _) ||
                    !CelestialStellarEvolutionModel.TryEvaluate(
                        primaryPopulation,
                        environment,
                        out var primary,
                        out _) ||
                    !CelestialStellarMultiplicityModel.TryGenerate(
                        seed,
                        primaryPopulation.InitialMassSolar,
                        environment.IronMetallicityDex,
                        environment.BirthStellarDensityPerCubicParsec,
                        out var multiplicity,
                        out _) ||
                    multiplicity.Multiplicity !=
                        CelestialStellarMultiplicity.Binary)
                {
                    continue;
                }

                var formation =
                    multiplicity.Companions[0];
                var companionPopulation =
                    CreatePopulationSample(
                        formation.BirthMassSolar,
                        environment.SystemAgeGigayears);

                if (!CelestialStellarEvolutionModel.TryEvaluate(
                        companionPopulation,
                        environment,
                        out var companion,
                        out _) ||
                    !BasicSystemGenerationGuide.IsBinarySceneCandidate(
                        primary,
                        companion,
                        formation))
                {
                    continue;
                }

                found++;
                output.AppendLine()
                    .Append(seed)
                    .Append(" — 2 stars: ")
                    .Append(Describe(primary))
                    .Append("; ")
                    .Append(Describe(companion));
            }

            if (found < TargetCount)
            {
                Debug.LogError(
                    $"Binary seed finder found only {found} eligible systems in {MaximumAttempts} attempts.");
                return;
            }

            Debug.Log(
                output.ToString());
        }

        private static CelestialStellarPopulationSample CreatePopulationSample(
            double birthMassSolar,
            double systemAgeGigayears)
        {
            var lifetime =
                10.0 *
                Math.Pow(
                    birthMassSolar,
                    -2.5);
            var state =
                systemAgeGigayears <= lifetime
                    ? CelestialStellarEvolutionState.MainSequence
                    : birthMassSolar < 8.0
                        ? CelestialStellarEvolutionState.WhiteDwarf
                        : birthMassSolar < 25.0
                            ? CelestialStellarEvolutionState.NeutronStar
                            : CelestialStellarEvolutionState.BlackHole;
            return
                new CelestialStellarPopulationSample(
                    birthMassSolar,
                    lifetime,
                    state);
        }

        private static string Describe(
            CelestialStellarEvolutionResult star)
        {
            return
                $"{FormatState(star.EvolutionState)}, {star.CurrentMassSolar:0.###} solar masses";
        }

        private static string FormatState(
            CelestialStellarEvolutionState state)
        {
            switch (state)
            {
                case CelestialStellarEvolutionState.MainSequence:
                    return "main-sequence";
                case CelestialStellarEvolutionState.WhiteDwarf:
                    return "white dwarf";
                case CelestialStellarEvolutionState.NeutronStar:
                    return "neutron star";
                case CelestialStellarEvolutionState.BlackHole:
                    return "black hole";
                default:
                    return state.ToString();
            }
        }
    }
}
