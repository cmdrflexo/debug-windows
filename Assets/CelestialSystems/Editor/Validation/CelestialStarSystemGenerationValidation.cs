/*
 * Runs the complete data-only star-system guide across many seeds and verifies cross-stage invariants.
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialStarSystemGenerationValidation
    {
        private const string GuidePath =
            "Assets/CelestialSystems/Definitions/Basic Prototype Star System Guide.asset";

        private const string EnvironmentPath =
            "Assets/CelestialSystems/Definitions/Galactic Environments/Solar-Neighborhood Thin Disk.asset";

        private const int SampleCount = 256;

        [MenuItem(
            "Tools/Celestial Systems/Validate Star System Generation")]
        public static void Validate()
        {
            var guide =
                AssetDatabase.LoadAssetAtPath<BasicSystemGenerationGuide>(
                    GuidePath);
            var environment =
                AssetDatabase.LoadAssetAtPath<CelestialGalacticEnvironmentDefinition>(
                    EnvironmentPath);

            if (guide == null ||
                environment == null)
            {
                Debug.LogError(
                    "Star-system generation validation could not load its guide or galactic environment asset.");
                return;
            }

            var totalPlanets = 0;
            var totalMoons = 0;
            var planetlessSystems = 0;
            var remnantSystems = 0;
            var minimumPlanets = int.MaxValue;
            var maximumPlanets = 0;

            for (var seed = 1;
                seed <= SampleCount;
                seed++)
            {
                CelestialStarSystemPlan plan = null;

                try
                {
                    if (!guide.TryGenerate(
                            new CelestialStarSystemGenerationRequest(
                                seed,
                                environment),
                            out plan,
                            out var error))
                    {
                        Fail(
                            seed,
                            error);
                        return;
                    }

                    if (!TryValidatePlan(
                            plan,
                            seed,
                            out var planets,
                            out var moons,
                            out var isRemnant,
                            out error))
                    {
                        Fail(
                            seed,
                            error);
                        return;
                    }

                    totalPlanets += planets;
                    totalMoons += moons;
                    minimumPlanets =
                        Math.Min(
                            minimumPlanets,
                            planets);
                    maximumPlanets =
                        Math.Max(
                            maximumPlanets,
                            planets);

                    if (planets == 0)
                    {
                        planetlessSystems++;
                    }

                    if (isRemnant)
                    {
                        remnantSystems++;
                    }
                }
                finally
                {
                    DestroyPlanObjects(
                        plan);
                }
            }

            if (!TryValidateDeterminism(
                    guide,
                    environment,
                    512,
                    out var determinismError))
            {
                Debug.LogError(
                    determinismError);
                return;
            }

            Debug.Log(
                $"Star-system generation PASS. {SampleCount} complete deterministic plans; physical values, formation histories, descriptions, references, trajectories, and non-crossing planetary orbits passed. " +
                $"Generated {totalPlanets} planets and {totalMoons} modeled major moons; {planetlessSystems} planetless systems; {remnantSystems} remnant-star systems; planet range {minimumPlanets}-{maximumPlanets}.");
        }

        private static bool TryValidatePlan(
            CelestialStarSystemPlan plan,
            int seed,
            out int planetCount,
            out int moonCount,
            out bool isRemnant,
            out string error)
        {
            planetCount = 0;
            moonCount = 0;
            isRemnant = false;

            if (plan == null ||
                string.IsNullOrWhiteSpace(
                    plan.DefinitionId) ||
                plan.BodySystems == null ||
                plan.BodySystems.Count == 0 ||
                !plan.FormationDisk.HasValue)
            {
                error =
                    "The guide returned an incomplete star-system plan.";
                return false;
            }

            var systemIds =
                new HashSet<string>(
                    StringComparer.Ordinal);
            var planetOrbits =
                new List<OrbitRange>();
            var stellarBodyCount = 0;
            var stellarMasses =
                new List<double>();
            var stellarTrajectories =
                new List<CelestialTrajectoryDefinition>();
            var formedSolidMassEarth = 0.0;
            var disk =
                plan.FormationDisk.Value;

            if (!PositiveFinite(
                    disk.InitialGasMassSolar) ||
                !PositiveFinite(
                    disk.InitialSolidMassEarth) ||
                !PositiveFinite(
                    disk.InnerBoundaryAstronomicalUnits) ||
                !PositiveFinite(
                    disk.OuterBoundaryAstronomicalUnits) ||
                disk.OuterBoundaryAstronomicalUnits <=
                    disk.InnerBoundaryAstronomicalUnits ||
                !PositiveFinite(
                    disk.FrostLineAstronomicalUnits) ||
                !PositiveFinite(
                    disk.PlanetFormationWindowMegayears) ||
                !Fraction(
                    disk.EnvironmentalRetentionFraction))
            {
                error =
                    "The generated formation disk contains invalid boundaries or material values.";
                return false;
            }

            foreach (var system in plan.BodySystems)
            {
                if (system == null ||
                    string.IsNullOrWhiteSpace(
                        system.InstanceId) ||
                    !systemIds.Add(
                        system.InstanceId) ||
                    system.Definition == null)
                {
                    error =
                        "A body-system plan is missing, duplicated, or invalid.";
                    return false;
                }

                if (!system.Definition.TryValidate(
                        out error))
                {
                    return false;
                }

                if (system.Trajectory != null &&
                    !system.Trajectory.TryValidate(
                        out error))
                {
                    return false;
                }

                var moonOrbits =
                    new List<OrbitRange>();
                var moonMassEarth = 0.0;
                var moonMassBudgetEarth = 0.0;

                foreach (var entry in system.Definition.Bodies)
                {
                    var body =
                        entry.Definition;

                    if (body == null ||
                        !body.HasValidPhysicalSettings ||
                        string.IsNullOrWhiteSpace(
                            body.Description) &&
                        (body.HasStellarProperties ||
                            body.HasPlanetFormationProperties ||
                            body.HasMoonFormationProperties))
                    {
                        error =
                            $"Body '{entry.InstanceId}' has invalid physical data or a missing generated description.";
                        return false;
                    }

                    if (body.HasStellarProperties)
                    {
                        stellarBodyCount++;
                        stellarMasses.Add(
                            body.MassKilograms);
                        stellarTrajectories.Add(
                            system.Trajectory);
                        isRemnant =
                            isRemnant ||
                            body.StellarEvolutionState !=
                                CelestialStellarEvolutionState.MainSequence;

                        if (!PositiveFinite(
                                body.MassKilograms) ||
                            !PositiveFinite(
                                body.ReferenceRadiusMeters) ||
                            !PositiveFinite(
                                body.StellarInitialMassSolar) ||
                            !NonNegativeFinite(
                                body.StellarLuminositySolar) ||
                            !NonNegativeFinite(
                                body.StellarEffectiveTemperatureKelvin))
                        {
                            error =
                                "The generated star contains invalid physical or evolutionary values.";
                            return false;
                        }
                    }

                    else if (body.HasPlanetFormationProperties)
                    {
                        planetCount++;

                        if (!TryValidatePlanet(
                                body,
                                system,
                                out var orbit,
                                out error))
                        {
                            return false;
                        }

                        planetOrbits.Add(
                            orbit);
                        formedSolidMassEarth +=
                            body.PlanetSolidCoreMassEarth;
                    }
                    else if (body.HasMoonFormationProperties)
                    {
                        if (!TryValidateMoon(
                                body,
                                entry,
                                out var moonOrbit,
                                out error))
                        {
                            return false;
                        }

                        moonCount++;
                        moonMassEarth +=
                            body.MassKilograms /
                            5.9722e24;
                        moonMassBudgetEarth =
                            body.MoonSystemMassBudgetEarth;
                        moonOrbits.Add(
                            moonOrbit);
                    }
                    else
                    {
                        error =
                            $"Body '{body.DefinitionId}' has no generated stellar, planetary, or moon identity.";
                        return false;
                    }
                }

                moonOrbits.Sort(
                    (left, right) =>
                        left.PeriapsisMeters.CompareTo(
                            right.PeriapsisMeters));

                for (var moonIndex = 1;
                    moonIndex < moonOrbits.Count;
                    moonIndex++)
                {
                    if (moonOrbits[moonIndex - 1].ApoapsisMeters >=
                        moonOrbits[moonIndex].PeriapsisMeters)
                    {
                        error =
                            $"Moon trajectories overlap in body system '{system.InstanceId}'.";
                        return false;
                    }
                }

                if (moonMassEarth >
                    moonMassBudgetEarth +
                        1.0e-9)
                {
                    error =
                        $"Moons in body system '{system.InstanceId}' exceed their generated satellite mass budget.";
                    return false;
                }
            }

            if (stellarBodyCount < 1 ||
                stellarBodyCount > 2)
            {
                error =
                    $"Expected one or two generated stellar bodies, but found {stellarBodyCount}.";
                return false;
            }

            if (stellarBodyCount == 2 &&
                !TryValidateTwoBodyPair(
                    stellarMasses,
                    stellarTrajectories,
                    out error))
            {
                return false;
            }

            if (formedSolidMassEarth >
                disk.InitialSolidMassEarth +
                    1.0e-9)
            {
                error =
                    $"Surviving planets contain {formedSolidMassEarth:0.###} Earth masses of modeled solids, exceeding the disk budget of {disk.InitialSolidMassEarth:0.###}.";
                return false;
            }

            planetOrbits.Sort(
                (left, right) =>
                    left.PeriapsisMeters.CompareTo(
                        right.PeriapsisMeters));

            for (var index = 1;
                index < planetOrbits.Count;
                index++)
            {
                if (planetOrbits[index - 1].ApoapsisMeters >=
                    planetOrbits[index].PeriapsisMeters)
                {
                    error =
                        $"Planetary trajectories overlap: apoapsis {planetOrbits[index - 1].ApoapsisMeters:0.###e+0} m reaches periapsis {planetOrbits[index].PeriapsisMeters:0.###e+0} m.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateTwoBodyPair(
            IReadOnlyList<double> masses,
            IReadOnlyList<CelestialTrajectoryDefinition> trajectories,
            out string error)
        {
            error = string.Empty;

            if (trajectories.Count != 2 ||
                trajectories[0] == null ||
                trajectories[1] == null ||
                trajectories[0].Kind !=
                    CelestialTrajectoryKind.TwoBodyBarycentricComponent ||
                trajectories[1].Kind !=
                    CelestialTrajectoryKind.TwoBodyBarycentricComponent ||
                !ReferenceEquals(
                    trajectories[0].TwoBodyOrbit,
                    trajectories[1].TwoBodyOrbit) ||
                trajectories[0].TwoBodyComponent ==
                    trajectories[1].TwoBodyComponent)
            {
                error =
                    "A generated two-body system does not share one complementary barycentric orbit.";
                return false;
            }

            var pair =
                trajectories[0].TwoBodyOrbit;
            var massA =
                trajectories[0].TwoBodyComponent ==
                    CelestialTwoBodyComponent.BodyA
                        ? masses[0]
                        : masses[1];
            var massB =
                trajectories[0].TwoBodyComponent ==
                    CelestialTwoBodyComponent.BodyB
                        ? masses[0]
                        : masses[1];

            if (Math.Abs(massA - pair.BodyAMassKilograms) >
                    pair.BodyAMassKilograms * 1.0e-12 ||
                Math.Abs(massB - pair.BodyBMassKilograms) >
                    pair.BodyBMassKilograms * 1.0e-12 ||
                !CelestialTrajectoryEvaluator.TryCalculatePeriodSeconds(
                    trajectories[0],
                    pair.TotalMassKilograms,
                    masses[0],
                    out var period,
                    out error))
            {
                return false;
            }

            var sampleFractions =
                new[]
                {
                    0.0,
                    0.071,
                    0.25,
                    0.5,
                    0.913,
                    1.0
                };

            foreach (var fraction in sampleFractions)
            {
                var time =
                    pair.RelativeTrajectory.EpochUniversalTimeSeconds +
                    period *
                    fraction;

                if (!CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                        trajectories[0],
                        pair.TotalMassKilograms,
                        masses[0],
                        time,
                        out var position0,
                        out var velocity0,
                        out error) ||
                    !CelestialTrajectoryEvaluator.TryEvaluateRelativeState(
                        trajectories[1],
                        pair.TotalMassKilograms,
                        masses[1],
                        time,
                        out var position1,
                        out var velocity1,
                        out error))
                {
                    return false;
                }

                var weightedPosition =
                    position0 * masses[0] +
                    position1 * masses[1];
                var weightedVelocity =
                    velocity0 * masses[0] +
                    velocity1 * masses[1];
                var positionScale =
                    (position0 - position1).Magnitude *
                    pair.TotalMassKilograms;
                var velocityScale =
                    (velocity0 - velocity1).Magnitude *
                    pair.TotalMassKilograms;

                if (weightedPosition.Magnitude >
                        Math.Max(1.0, positionScale) * 1.0e-12 ||
                    weightedVelocity.Magnitude >
                        Math.Max(1.0, velocityScale) * 1.0e-12)
                {
                    error =
                        "A generated two-body trajectory moved its mass-weighted barycenter.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidatePlanet(
            CelestialBodyDefinition body,
            CelestialStarSystemPlan.BodySystemPlan system,
            out OrbitRange orbit,
            out string error)
        {
            orbit = default;

            if (!body.HasPostMainSequenceProperties ||
                !body.HasPlanetaryEvolutionProperties ||
                !body.HasRotationProperties ||
                !PositiveFinite(
                    body.RotationPeriodHours) ||
                !NonNegativeFinite(
                    body.AxialTiltDegrees) ||
                body.AxialTiltDegrees > 180.0 ||
                !PositiveFinite(
                    body.PlanetInitialOrbitAstronomicalUnits) ||
                !PositiveFinite(
                    body.PlanetFinalOrbitAstronomicalUnits) ||
                body.PlanetFinalOrbitAstronomicalUnits >
                    body.PlanetInitialOrbitAstronomicalUnits ||
                !PositiveFinite(
                    body.PlanetPresentOrbitAstronomicalUnits) ||
                !PositiveFinite(
                    body.PlanetSolidCoreMassEarth) ||
                !Fraction(
                    body.PlanetVolatileMassFraction) ||
                !Fraction(
                    body.PlanetHydrogenHeliumEnvelopeFraction) ||
                !Fraction(
                    body.PlanetInwardMigrationFraction) ||
                !Fraction(
                    body.PlanetAssumedBondAlbedo) ||
                !PositiveFinite(
                    body.PlanetEquilibriumTemperatureKelvin) ||
                !PositiveFinite(
                    body.PlanetEscapeVelocityMetersPerSecond))
            {
                error =
                    $"Planet '{body.DefinitionId}' contains contradictory formation or evolution values.";
                return false;
            }

            if (system.Trajectory == null ||
                system.Trajectory.Kind !=
                    CelestialTrajectoryKind.KeplerianConic ||
                system.Trajectory.Conic == null ||
                !system.Trajectory.Conic.IsClosed)
            {
                error =
                    $"Planet '{body.DefinitionId}' requires a closed Keplerian trajectory.";
                return false;
            }

            var conic =
                system.Trajectory.Conic;
            orbit =
                new OrbitRange(
                    conic.PeriapsisDistanceMeters,
                    conic.SemiMajorAxisMeters *
                        (1.0 + conic.Eccentricity));

            if (!PositiveFinite(
                    orbit.PeriapsisMeters) ||
                !PositiveFinite(
                    orbit.ApoapsisMeters))
            {
                error =
                    $"Planet '{body.DefinitionId}' contains an invalid orbital range.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateMoon(
            CelestialBodyDefinition body,
            CelestialBodySystemDefinition.BodyEntry entry,
            out OrbitRange orbit,
            out string error)
        {
            orbit = default;

            if (!body.HasMoonEvolutionProperties ||
                !body.HasRotationProperties ||
                !PositiveFinite(
                    body.RotationPeriodHours) ||
                !NonNegativeFinite(
                    body.AxialTiltDegrees) ||
                body.AxialTiltDegrees > 180.0 ||
                body.MoonLikelyTidallyLocked !=
                    body.IsSpinOrbitSynchronous ||
                !PositiveFinite(
                    body.MassKilograms) ||
                !PositiveFinite(
                    body.ReferenceRadiusMeters) ||
                !Fraction(
                    body.MoonVolatileMassFraction) ||
                !PositiveFinite(
                    body.MoonOrbitalRadiusMeters) ||
                !NonNegativeFinite(
                    body.MoonOrbitalEccentricity) ||
                body.MoonOrbitalEccentricity >= 1.0 ||
                !NonNegativeFinite(
                    body.MoonOrbitalInclinationDegrees) ||
                body.MoonOrbitalInclinationDegrees > 180.0 ||
                !PositiveFinite(
                    body.MoonRocheLimitMeters) ||
                !PositiveFinite(
                    body.MoonStableOuterLimitMeters) ||
                !PositiveFinite(
                    body.MoonHillRadiusMeters) ||
                !PositiveFinite(
                    body.MoonSystemMassBudgetEarth) ||
                !Fraction(
                    body.MoonAssumedBondAlbedo) ||
                !PositiveFinite(
                    body.MoonEquilibriumTemperatureKelvin) ||
                !PositiveFinite(
                    body.MoonEscapeVelocityMetersPerSecond) ||
                !NonNegativeFinite(
                    body.MoonRelativeTidalHeatingIndex) ||
                entry.Trajectory == null ||
                entry.Trajectory.Kind !=
                    CelestialTrajectoryKind.KeplerianConic ||
                entry.Trajectory.Conic == null)
            {
                error =
                    $"Moon '{body.DefinitionId}' contains invalid formation or trajectory data.";
                return false;
            }

            var conic =
                entry.Trajectory.Conic;
            var expectedPeriapsis =
                body.MoonOrbitalRadiusMeters *
                (1.0 -
                    body.MoonOrbitalEccentricity);
            var apoapsis =
                body.MoonOrbitalRadiusMeters *
                (1.0 +
                    body.MoonOrbitalEccentricity);
            var tolerance =
                Math.Max(
                    1.0e-6,
                    expectedPeriapsis *
                        1.0e-12);

            if (Math.Abs(
                    conic.PeriapsisDistanceMeters -
                        expectedPeriapsis) >
                    tolerance ||
                conic.Eccentricity !=
                    body.MoonOrbitalEccentricity ||
                conic.Direction !=
                    body.MoonOrbitDirection ||
                expectedPeriapsis <=
                    body.MoonRocheLimitMeters ||
                apoapsis >=
                    body.MoonStableOuterLimitMeters)
            {
                error =
                    $"Moon '{body.DefinitionId}' contradicts its modeled orbit or stable boundaries.";
                return false;
            }

            orbit =
                new OrbitRange(
                    expectedPeriapsis,
                    apoapsis);
            error = string.Empty;
            return true;
        }

        private static bool TryValidateDeterminism(
            BasicSystemGenerationGuide guide,
            CelestialGalacticEnvironmentDefinition environment,
            int seed,
            out string error)
        {
            CelestialStarSystemPlan first = null;
            CelestialStarSystemPlan repeated = null;

            try
            {
                if (!guide.TryGenerate(
                        new CelestialStarSystemGenerationRequest(
                            seed,
                            environment),
                        out first,
                        out error) ||
                    !guide.TryGenerate(
                        new CelestialStarSystemGenerationRequest(
                            seed,
                            environment),
                        out repeated,
                        out error))
                {
                    return false;
                }

                if (!string.Equals(
                        BuildSignature(first),
                        BuildSignature(repeated),
                        StringComparison.Ordinal))
                {
                    error =
                        $"Star-system generation validation failed: seed {seed} did not reproduce the same complete plan.";
                    return false;
                }

                error = string.Empty;
                return true;
            }
            finally
            {
                DestroyPlanObjects(first);
                DestroyPlanObjects(repeated);
            }
        }

        private static string BuildSignature(
            CelestialStarSystemPlan plan)
        {
            var result =
                new StringBuilder();
            result.Append(plan.DefinitionId);

            foreach (var system in plan.BodySystems)
            {
                result.Append('|').Append(system.InstanceId);
                AppendTrajectory(result, system.Trajectory);

                foreach (var entry in system.Definition.Bodies)
                {
                    var body = entry.Definition;
                    result.Append('|').Append(entry.InstanceId)
                        .Append(':').Append(body.DefinitionId)
                        .Append(':').Append(body.GenerationSeed)
                        .Append(':').Append(body.MassKilograms.ToString("R"))
                        .Append(':').Append(body.ReferenceRadiusMeters.ToString("R"))
                        .Append(':').Append(body.Description);

                    if (body.HasRotationProperties)
                    {
                        result.Append(':').Append(body.RotationPeriodHours.ToString("R"))
                            .Append(':').Append(body.AxialTiltDegrees.ToString("R"))
                            .Append(':').Append(body.SpinDirection)
                            .Append(':').Append(body.SpinState);
                    }

                    if (body.HasMoonEvolutionProperties)
                    {
                        result.Append(':').Append(body.MoonAssumedBondAlbedo.ToString("R"))
                            .Append(':').Append(body.MoonEquilibriumTemperatureKelvin.ToString("R"))
                            .Append(':').Append(body.MoonEscapeVelocityMetersPerSecond.ToString("R"))
                            .Append(':').Append(body.MoonRelativeTidalHeatingIndex.ToString("R"))
                            .Append(':').Append(body.MoonLikelyTidallyLocked)
                            .Append(':').Append(body.MoonTidalHeating)
                            .Append(':').Append(body.MoonTidalMigrationSensitivity);
                    }

                    AppendTrajectory(result, entry.Trajectory);
                }
            }

            return result.ToString();
        }

        private static void AppendTrajectory(
            StringBuilder result,
            CelestialTrajectoryDefinition trajectory)
        {
            if (trajectory == null)
            {
                result.Append(":no-trajectory");
                return;
            }

            result.Append(':').Append(trajectory.Kind)
                .Append(':').Append(trajectory.ReferenceInstanceId);

            if (trajectory.Conic != null)
            {
                result.Append(':').Append(trajectory.Conic.PeriapsisDistanceMeters.ToString("R"))
                    .Append(':').Append(trajectory.Conic.Eccentricity.ToString("R"))
                    .Append(':').Append(trajectory.Conic.MeanAnomalyAtEpochDegrees.ToString("R"))
                    .Append(':').Append(trajectory.Conic.InclinationDegrees.ToString("R"));
            }
        }

        private static void DestroyPlanObjects(
            CelestialStarSystemPlan plan)
        {
            if (plan == null)
            {
                return;
            }

            var objects =
                new HashSet<UnityEngine.Object>();

            foreach (var system in plan.BodySystems)
            {
                if (system?.Definition == null)
                {
                    continue;
                }

                if ((system.Definition.hideFlags &
                        HideFlags.DontSave) != 0)
                {
                    objects.Add(
                        system.Definition);
                }

                foreach (var entry in system.Definition.Bodies)
                {
                    if (entry?.Definition != null &&
                        (entry.Definition.hideFlags &
                            HideFlags.DontSave) != 0)
                    {
                        objects.Add(
                            entry.Definition);
                    }
                }
            }

            foreach (var runtimeObject in objects)
            {
                UnityEngine.Object.DestroyImmediate(
                    runtimeObject);
            }
        }

        private static bool PositiveFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }

        private static bool Fraction(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0.0 &&
                value <= 1.0;
        }

        private static bool NonNegativeFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0.0;
        }

        private static void Fail(
            int seed,
            string error)
        {
            Debug.LogError(
                $"Star-system generation validation failed for seed {seed}: {error}");
        }

        private readonly struct OrbitRange
        {
            public OrbitRange(
                double periapsisMeters,
                double apoapsisMeters)
            {
                PeriapsisMeters = periapsisMeters;
                ApoapsisMeters = apoapsisMeters;
            }

            public double PeriapsisMeters { get; }

            public double ApoapsisMeters { get; }
        }
    }
}
