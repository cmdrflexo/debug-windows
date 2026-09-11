/*
 * Provides a deterministic prototype guide that varies body properties and builds simple free-simulation star systems.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Basic System Generation Guide",
        menuName = "Celestial Systems/Generation Guides/Basic Star System")]
    public sealed class BasicSystemGenerationGuide :
        CelestialSystemGenerationGuide
    {
        private const double GravitationalConstant =
            6.67430e-11;

        private const double SolarMassKilograms =
            1.98847e30;

        private const double SolarRadiusMeters =
            6.957e8;

        private const double AstronomicalUnitMeters =
            149597870700.0;

        private const double EarthMassKilograms =
            5.9722e24;

        private const double EarthRadiusMeters =
            6371000.0;

        [Header("Identity")]
        [SerializeField]
        private string planDefinitionId =
            "basic-star-system";

        [Header("Prototype Bodies")]
        [SerializeField]
        private CelestialBodyDefinition starDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile starQualityProfile;

        [SerializeField]
        private CelestialBodyDefinition planetDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile planetQualityProfile;

        [SerializeField]
        private CelestialBodyDefinition moonDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile moonQualityProfile;

        [Header("Prototype Orbits")]
        [SerializeField]
        [Range(0.0f, 45.0f)]
        private float maximumInclinationDegrees =
            5.0f;

        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float maximumPlanetEccentricity =
            0.12f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float retrogradeChance;

        private readonly struct GeneratedMoon
        {
            public GeneratedMoon(
                CelestialBodyDefinition definition,
                CelestialMoonFormationResult formation,
                double phaseRadians,
                double argumentOfPeriapsisRadians)
            {
                Definition = definition;
                Formation = formation;
                PhaseRadians = phaseRadians;
                ArgumentOfPeriapsisRadians =
                    argumentOfPeriapsisRadians;
            }

            public CelestialBodyDefinition Definition { get; }

            public CelestialMoonFormationResult Formation { get; }

            public double PhaseRadians { get; }

            public double ArgumentOfPeriapsisRadians { get; }
        }

        private readonly struct SurvivingPlanet
        {
            public SurvivingPlanet(
                CelestialPlanetFormationResult formation,
                CelestialPostMainSequencePlanetResult systemEvolution)
            {
                Formation = formation;
                SystemEvolution = systemEvolution;
            }

            public CelestialPlanetFormationResult Formation { get; }

            public CelestialPostMainSequencePlanetResult SystemEvolution { get; }
        }

        public override bool TryGenerate(
            CelestialStarSystemGenerationRequest request,
            out CelestialStarSystemPlan plan,
            out string error)
        {
            plan = null;

            if (!TryValidate(
                    request,
                    out error))
            {
                return false;
            }

            var random =
                new DeterministicRandom(
                    request.Seed);

            if (!CelestialStellarPopulationSampler.TrySamplePrimary(
                    request.Seed,
                    request.Environment,
                    out var stellarPopulation,
                    out error) ||
                !CelestialStellarEvolutionModel.TryEvaluate(
                    stellarPopulation,
                    request.Environment,
                    out var stellarProperties,
                    out error))
            {
                return false;
            }

            if (!CelestialProtoplanetaryDiskModel.TryGenerate(
                    request.Seed,
                    stellarPopulation,
                    stellarProperties,
                    request.Environment,
                    out var formationDisk,
                    out error))
            {
                return false;
            }

            var planetCount =
                SelectPlanetCount(
                    formationDisk.FormationCapacity,
                    ref random);
            var innerPlanetOrbitMeters =
                Math.Max(
                    formationDisk.InnerBoundaryAstronomicalUnits *
                        1.5,
                    0.01) *
                AstronomicalUnitMeters;
            var outerPlanetOrbitMeters =
                Math.Max(
                    formationDisk.FrostLineAstronomicalUnits *
                        1.5,
                    formationDisk.OuterBoundaryAstronomicalUnits *
                        0.45) *
                AstronomicalUnitMeters;
            outerPlanetOrbitMeters =
                Math.Max(
                    outerPlanetOrbitMeters,
                    innerPlanetOrbitMeters *
                        2.0);
            var initialPlanetOrbitsAu =
                new double[planetCount];

            for (var index = 0;
                index < planetCount;
                index++)
            {
                var nominalFraction =
                    ((double)index + 0.5) /
                    planetCount;
                var jitterRange =
                    0.3 /
                    planetCount;
                var orbitFraction =
                    Clamp01(
                        nominalFraction +
                        (random.Next01() * 2.0 - 1.0) *
                        jitterRange);
                initialPlanetOrbitsAu[index] =
                    LogarithmicLerp(
                        innerPlanetOrbitMeters,
                        outerPlanetOrbitMeters,
                        orbitFraction) /
                    AstronomicalUnitMeters;
            }

            if (!CelestialPlanetFormationModel.TryGenerate(
                    request.Seed,
                    formationDisk,
                    stellarPopulation,
                    initialPlanetOrbitsAu,
                    out var planetFormation,
                    out error))
            {
                return false;
            }

            var survivingPlanets =
                new List<SurvivingPlanet>(
                    planetFormation.Length);

            for (var index = 0;
                index < planetFormation.Length;
                index++)
            {
                if (!CelestialPostMainSequenceSystemEvolutionModel.TryEvaluatePlanet(
                        stellarProperties,
                        planetFormation[index].FinalOrbitAstronomicalUnits,
                        maximumPlanetEccentricity,
                        out var systemEvolution,
                        out error))
                {
                    return false;
                }

                if (systemEvolution.Survived)
                {
                    survivingPlanets.Add(
                        new SurvivingPlanet(
                            planetFormation[index],
                            systemEvolution));
                }
            }

            survivingPlanets.Sort(
                (left, right) =>
                    left.SystemEvolution.PresentOrbitAstronomicalUnits.CompareTo(
                        right.SystemEvolution.PresentOrbitAstronomicalUnits));

            var safePlanetEccentricityLimit =
                CalculateNonCrossingPlanetEccentricityLimit(
                    survivingPlanets);
            var bodySystems =
                new List<CelestialStarSystemPlan.BodySystemPlan>(
                    survivingPlanets.Count + 1);
            var ownedRuntimeObjects =
                new List<UnityEngine.Object>(
                    (survivingPlanets.Count + 1) *
                    2);
            var starDescription =
                CelestialObjectDescriptionGenerator.DescribeStar(
                    stellarPopulation,
                    stellarProperties,
                    request.Environment);
            var star =
                CreateStellarDefinition(
                    starDefinition,
                    "generated-star",
                    random.NextInt(),
                    stellarProperties,
                    starDescription);
            ownedRuntimeObjects.Add(
                star);

            bodySystems.Add(
                CreateSingleBodySystem(
                    "stellar",
                    "star",
                    star,
                    starQualityProfile,
                    new DoubleVector3(),
                    new DoubleVector3(),
                    ownedRuntimeObjects));

            for (var index = 0;
                index < survivingPlanets.Count;
                index++)
            {
                var formation =
                    survivingPlanets[index].Formation;
                var systemEvolution =
                    survivingPlanets[index].SystemEvolution;
                var orbitRadius =
                    systemEvolution.PresentOrbitAstronomicalUnits *
                    AstronomicalUnitMeters;
                var phaseRadians =
                    random.Next01() *
                    Math.PI *
                    2.0;
                var inclinationRadians =
                    (random.Next01() * 2.0 - 1.0) *
                    maximumInclinationDegrees *
                    Math.PI /
                    180.0;
                var direction =
                    random.Next01() <
                        retrogradeChance
                            ? -1.0
                            : 1.0;
                var eccentricity =
                    random.NextRange(
                        0.0,
                        Math.Min(
                            maximumPlanetEccentricity,
                            safePlanetEccentricityLimit));
                var argumentOfPeriapsisRadians =
                    random.Next01() *
                    Math.PI *
                    2.0;
                var orbitalSpeed =
                    Math.Sqrt(
                        GravitationalConstant *
                        star.MassKilograms /
                        orbitRadius);
                var cosinePhase =
                    Math.Cos(
                        phaseRadians);
                var sinePhase =
                    Math.Sin(
                        phaseRadians);
                var cosineInclination =
                    Math.Cos(
                        inclinationRadians);
                var sineInclination =
                    Math.Sin(
                        inclinationRadians);
                var position =
                    new DoubleVector3(
                        cosinePhase *
                            orbitRadius,
                        sinePhase *
                            sineInclination *
                            orbitRadius,
                        sinePhase *
                            cosineInclination *
                            orbitRadius);
                var velocity =
                    new DoubleVector3(
                        -sinePhase *
                            orbitalSpeed *
                            direction,
                        cosinePhase *
                            sineInclination *
                            orbitalSpeed *
                            direction,
                        cosinePhase *
                            cosineInclination *
                            orbitalSpeed *
                            direction);
                var instanceId =
                    $"planet-{index + 1}";
                var planetSeed =
                    random.NextInt();

                if (!CelestialPlanetaryEvolutionModel.TryEvaluate(
                        planetSeed,
                        formation,
                        stellarProperties,
                        request.Environment.SystemAgeGigayears,
                        systemEvolution.PresentOrbitAstronomicalUnits,
                        out var planetaryEvolution,
                        out error))
                {
                    ReleaseOwnedRuntimeObjects(
                        ownedRuntimeObjects);
                    return false;
                }

                var planet =
                    CreatePlanetDefinition(
                        planetDefinition,
                        $"generated-{instanceId}",
                        planetSeed,
                        formation,
                        systemEvolution,
                        planetaryEvolution);
                ownedRuntimeObjects.Add(
                    planet);

                if (!CelestialMoonFormationModel.TryGenerate(
                        planetSeed,
                        formation,
                        formation.FinalOrbitAstronomicalUnits,
                        stellarProperties.InitialMassSolar,
                        out var moonSystem,
                        out error))
                {
                    ReleaseOwnedRuntimeObjects(
                        ownedRuntimeObjects);
                    return false;
                }

                var generatedMoons =
                    new List<GeneratedMoon>(
                        moonSystem.Moons.Length);

                for (var moonIndex = 0;
                    moonIndex < moonSystem.Moons.Length;
                    moonIndex++)
                {
                    var moonFormation =
                        moonSystem.Moons[moonIndex];
                    var moonDiameterMeters =
                        moonFormation.RadiusEarth *
                        EarthRadiusMeters *
                        2.0;

                    if (moonDiameterMeters <
                        CelestialBodyDefinition.DefaultRoundBodyMinimumDiameterMeters)
                    {
                        continue;
                    }

                    var moonSeed =
                        random.NextInt();
                    var moon =
                        CreateMoonDefinition(
                            moonDefinition,
                            $"generated-{instanceId}-moon-{generatedMoons.Count + 1}",
                            moonSeed,
                            moonFormation,
                            moonSystem,
                            formation);
                    ownedRuntimeObjects.Add(
                        moon);
                    generatedMoons.Add(
                        new GeneratedMoon(
                            moon,
                            moonFormation,
                            random.Next01() *
                                Math.PI *
                                2.0,
                            random.Next01() *
                                Math.PI *
                                2.0));
                }

                bodySystems.Add(
                    CreatePlanetaryBodySystem(
                        instanceId,
                        planet,
                        planetQualityProfile,
                        generatedMoons,
                        moonQualityProfile,
                        position,
                        velocity,
                        orbitRadius,
                        phaseRadians,
                        inclinationRadians,
                        direction,
                        eccentricity,
                        argumentOfPeriapsisRadians,
                        ownedRuntimeObjects));
            }

            plan =
                new CelestialStarSystemPlan(
                    planDefinitionId,
                    bodySystems,
                    ownedRuntimeObjects,
                    formationDisk);
            error = string.Empty;
            return true;
        }

        private bool TryValidate(
            CelestialStarSystemGenerationRequest request,
            out string error)
        {
            if (request == null)
            {
                error =
                    "The basic system generation guide requires a generation request.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    planDefinitionId))
            {
                error =
                    "The basic system generation guide requires a plan definition ID.";
                return false;
            }

            if (!IsUsablePrototype(
                    starDefinition))
            {
                error =
                    "The basic system generation guide requires a valid, spawnable star body definition.";
                return false;
            }

            if (!IsUsablePrototype(
                    planetDefinition))
            {
                error =
                    "The basic system generation guide requires a valid, spawnable planet body definition.";
                return false;
            }

            if (!IsUsablePrototype(
                    moonDefinition))
            {
                error =
                    "The basic system generation guide requires a valid, spawnable moon body definition.";
                return false;
            }

            if (!IsFinite(
                    maximumPlanetEccentricity) ||
                maximumPlanetEccentricity < 0.0 ||
                maximumPlanetEccentricity >= 1.0)
            {
                error =
                    "The basic system generation guide requires a planet eccentricity limit from zero up to, but not including, one.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static CelestialBodyDefinition CreateStellarDefinition(
            CelestialBodyDefinition prototype,
            string definitionId,
            int generationSeed,
            CelestialStellarEvolutionResult properties,
            string description)
        {
            var definition =
                CreateInstance<CelestialBodyDefinition>();
            definition.name =
                definitionId;
            definition.hideFlags =
                HideFlags.DontSave;
            definition.ConfigureRuntime(
                definitionId,
                properties.CurrentMassSolar *
                    SolarMassKilograms,
                properties.RadiusSolar *
                    SolarRadiusMeters,
                generationSeed,
                prototype.NorthAxis,
                prototype.PoleReferenceAxis,
                prototype.SurfaceSystem,
                prototype.RoundMapMagicSurface,
                prototype.OceanDefinition);
            definition.ConfigureRuntimeStellarProperties(
                properties);
            definition.ConfigureRuntimeDescription(
                description);
            return definition;
        }

        private static void ReleaseOwnedRuntimeObjects(
            IList<UnityEngine.Object> ownedRuntimeObjects)
        {
            for (var index = 0;
                index < ownedRuntimeObjects.Count;
                index++)
            {
                if (ownedRuntimeObjects[index] == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(
                        ownedRuntimeObjects[index]);
                }
                else
                {
                    DestroyImmediate(
                        ownedRuntimeObjects[index]);
                }
            }

            ownedRuntimeObjects.Clear();
        }

        private static CelestialBodyDefinition CreatePlanetDefinition(
            CelestialBodyDefinition prototype,
            string definitionId,
            int generationSeed,
            CelestialPlanetFormationResult formation,
            CelestialPostMainSequencePlanetResult systemEvolution,
            CelestialPlanetaryEvolutionResult evolution)
        {
            var definition =
                CreateInstance<CelestialBodyDefinition>();
            definition.name =
                definitionId;
            definition.hideFlags =
                HideFlags.DontSave;
            definition.ConfigureRuntime(
                definitionId,
                formation.TotalMassEarth *
                    EarthMassKilograms,
                formation.RadiusEarth *
                    EarthRadiusMeters,
                generationSeed,
                prototype.NorthAxis,
                prototype.PoleReferenceAxis,
                prototype.SurfaceSystem,
                prototype.RoundMapMagicSurface,
                prototype.OceanDefinition);
            definition.ConfigureRuntimePlanetFormationProperties(
                formation);
            definition.ConfigureRuntimePostMainSequenceProperties(
                systemEvolution);
            definition.ConfigureRuntimePlanetaryEvolutionProperties(
                evolution);
            definition.ConfigureRuntimeDescription(
                CelestialObjectDescriptionGenerator.DescribePlanet(
                    formation,
                    evolution,
                    systemEvolution,
                    generationSeed));
            return definition;
        }

        private static CelestialBodyDefinition CreateMoonDefinition(
            CelestialBodyDefinition prototype,
            string definitionId,
            int generationSeed,
            CelestialMoonFormationResult formation,
            CelestialMoonSystemFormationResult system,
            CelestialPlanetFormationResult planet)
        {
            var definition =
                CreateInstance<CelestialBodyDefinition>();
            definition.name =
                definitionId;
            definition.hideFlags =
                HideFlags.DontSave;
            definition.ConfigureRuntime(
                definitionId,
                formation.MassEarth *
                    EarthMassKilograms,
                formation.RadiusEarth *
                    EarthRadiusMeters,
                generationSeed,
                prototype.NorthAxis,
                prototype.PoleReferenceAxis,
                prototype.SurfaceSystem,
                prototype.RoundMapMagicSurface,
                prototype.OceanDefinition);
            definition.ConfigureRuntimeMoonFormationProperties(
                formation,
                system);
            definition.ConfigureRuntimeDescription(
                CelestialObjectDescriptionGenerator.DescribeMoon(
                    formation,
                    planet,
                    generationSeed));
            return definition;
        }

        private static CelestialStarSystemPlan.BodySystemPlan CreateSingleBodySystem(
            string systemInstanceId,
            string bodyInstanceId,
            CelestialBodyDefinition definition,
            RoundMapMagicSurfaceQualityProfile qualityProfile,
            DoubleVector3 position,
            DoubleVector3 velocity,
            ICollection<UnityEngine.Object> ownedRuntimeObjects)
        {
            var entry =
                new CelestialBodySystemDefinition.BodyEntry(
                    bodyInstanceId,
                    definition,
                    qualityProfile,
                    string.Empty,
                    CelestialBodySpawnMode.FreeSimulation,
                    new DoubleVector3(),
                    new DoubleVector3(),
                    Vector3.zero,
                    new DoubleVector3());
            var definitionId =
                $"{systemInstanceId}-body-system";
            var systemDefinition =
                CelestialBodySystemDefinition.CreateRuntime(
                    definitionId,
                    new[]
                    {
                        entry
                    });
            ownedRuntimeObjects.Add(
                systemDefinition);

            return
                new CelestialStarSystemPlan.BodySystemPlan(
                    systemInstanceId,
                    systemDefinition,
                    position,
                    velocity,
                    Quaternion.identity,
                    CelestialBodySpawnMode.PrescribedTrajectory);
        }

        private static CelestialStarSystemPlan.BodySystemPlan CreatePlanetaryBodySystem(
            string systemInstanceId,
            CelestialBodyDefinition planet,
            RoundMapMagicSurfaceQualityProfile planetQualityProfile,
            IReadOnlyList<GeneratedMoon> moons,
            RoundMapMagicSurfaceQualityProfile moonQualityProfile,
            DoubleVector3 position,
            DoubleVector3 velocity,
            double planetOrbitRadius,
            double planetPhaseRadians,
            double planetInclinationRadians,
            double planetDirection,
            double planetEccentricity,
            double planetArgumentOfPeriapsisRadians,
            ICollection<UnityEngine.Object> ownedRuntimeObjects)
        {
            var planetPosition =
                new DoubleVector3();
            var planetVelocity =
                new DoubleVector3();
            var entries =
                new List<CelestialBodySystemDefinition.BodyEntry>(
                    moons.Count + 1);
            var totalMass =
                planet.MassKilograms;

            for (var index = 0;
                index < moons.Count;
                index++)
            {
                totalMass +=
                    moons[index].Definition.MassKilograms;
            }

            var relativePositions =
                new DoubleVector3[moons.Count];
            var relativeVelocities =
                new DoubleVector3[moons.Count];

            for (var index = 0;
                index < moons.Count;
                index++)
            {
                var moon =
                    moons[index];
                var orbitRadius =
                    moon.Formation.OrbitalRadiusMeters;
                var inclinationRadians =
                    moon.Formation.InclinationDegrees *
                    Math.PI /
                    180.0;
                var direction =
                    moon.Formation.Direction ==
                        CelestialOrbitDirection.Retrograde
                            ? -1.0
                            : 1.0;
                var relativeSpeed =
                    Math.Sqrt(
                        GravitationalConstant *
                        (planet.MassKilograms +
                            moon.Definition.MassKilograms) /
                        orbitRadius);
                var cosinePhase =
                    Math.Cos(
                        moon.PhaseRadians);
                var sinePhase =
                    Math.Sin(
                        moon.PhaseRadians);
                var cosineInclination =
                    Math.Cos(
                        inclinationRadians);
                var sineInclination =
                    Math.Sin(
                        inclinationRadians);
                var radialDirection =
                    new DoubleVector3(
                        cosinePhase,
                        sinePhase *
                            sineInclination,
                        sinePhase *
                            cosineInclination);
                var tangentDirection =
                    new DoubleVector3(
                        -sinePhase,
                        cosinePhase *
                            sineInclination,
                        cosinePhase *
                            cosineInclination);
                var relativePosition =
                    radialDirection *
                    orbitRadius;
                var relativeVelocity =
                    tangentDirection *
                    (relativeSpeed *
                        direction);

                relativePositions[index] =
                    relativePosition;
                relativeVelocities[index] =
                    relativeVelocity;
                planetPosition -=
                    relativePosition *
                    (moon.Definition.MassKilograms /
                        totalMass);
                planetVelocity -=
                    relativeVelocity *
                    (moon.Definition.MassKilograms /
                        totalMass);
            }

            entries.Add(
                new CelestialBodySystemDefinition.BodyEntry(
                    "planet",
                    planet,
                    planetQualityProfile,
                    string.Empty,
                    CelestialBodySpawnMode.FreeSimulation,
                    planetPosition,
                    planetVelocity,
                    Vector3.zero,
                    new DoubleVector3()));

            for (var index = 0;
                index < moons.Count;
                index++)
            {
                var moon =
                    moons[index];
                var moonInstanceId =
                    $"moon-{index + 1}";
                var direction =
                    moon.Formation.Direction ==
                        CelestialOrbitDirection.Retrograde
                            ? -1.0
                            : 1.0;

                entries.Add(
                    new CelestialBodySystemDefinition.BodyEntry(
                        moonInstanceId,
                        moon.Definition,
                        moonQualityProfile,
                        "planet",
                        CelestialBodySpawnMode.PrescribedTrajectory,
                        planetPosition +
                            relativePositions[index],
                        planetVelocity +
                            relativeVelocities[index],
                        Vector3.zero,
                        new DoubleVector3(),
                        CreateConicTrajectory(
                            "planet",
                            moon.Formation.OrbitalRadiusMeters *
                                (1.0 -
                                    moon.Formation.Eccentricity),
                            moon.Formation.Eccentricity,
                            moon.PhaseRadians,
                            moon.Formation.InclinationDegrees *
                                Math.PI /
                                180.0,
                            moon.ArgumentOfPeriapsisRadians,
                            direction)));
            }

            var definitionId =
                $"{systemInstanceId}-body-system";
            var systemDefinition =
                CelestialBodySystemDefinition.CreateRuntime(
                    definitionId,
                    entries);
            ownedRuntimeObjects.Add(
                systemDefinition);

            return
                new CelestialStarSystemPlan.BodySystemPlan(
                    systemInstanceId,
                    systemDefinition,
                    position,
                    velocity,
                    Quaternion.identity,
                    CelestialBodySpawnMode.PrescribedTrajectory,
                    "stellar",
                    "star",
                    CreateConicTrajectory(
                        "stellar/star",
                        planetOrbitRadius,
                        planetEccentricity,
                        planetPhaseRadians,
                        planetInclinationRadians,
                        planetArgumentOfPeriapsisRadians,
                        planetDirection));
        }

        private static CelestialTrajectoryDefinition CreateConicTrajectory(
            string referenceInstanceId,
            double periapsisDistanceMeters,
            double eccentricity,
            double meanAnomalyRadians,
            double inclinationRadians,
            double argumentOfPeriapsisRadians,
            double direction)
        {
            return
                new CelestialTrajectoryDefinition(
                    new KeplerianConicTrajectory(
                        referenceInstanceId,
                        periapsisDistanceMeters,
                        eccentricity,
                        0.0,
                        meanAnomalyRadians *
                            180.0 /
                            Math.PI,
                        Math.Abs(
                            inclinationRadians *
                            180.0 /
                            Math.PI),
                        0.0,
                        argumentOfPeriapsisRadians *
                            180.0 /
                            Math.PI,
                        direction < 0.0
                            ? CelestialOrbitDirection.Retrograde
                            : CelestialOrbitDirection.Prograde));
        }

        private static int SelectPlanetCount(
            CelestialPlanetFormationCapacity capacity,
            ref DeterministicRandom random)
        {
            switch (capacity)
            {
                case CelestialPlanetFormationCapacity.StronglyInhibited:
                    return random.NextInclusive(
                        0,
                        1);

                case CelestialPlanetFormationCapacity.Low:
                    return random.NextInclusive(
                        1,
                        3);

                case CelestialPlanetFormationCapacity.Typical:
                    return random.NextInclusive(
                        3,
                        8);

                case CelestialPlanetFormationCapacity.High:
                    return random.NextInclusive(
                        6,
                        12);

                default:
                    return 0;
            }
        }

        private double CalculateNonCrossingPlanetEccentricityLimit(
            IReadOnlyList<SurvivingPlanet> planets)
        {
            if (planets == null ||
                planets.Count <= 1)
            {
                return maximumPlanetEccentricity;
            }

            var limit =
                (double)maximumPlanetEccentricity;

            for (var index = 1;
                index < planets.Count;
                index++)
            {
                var innerOrbit =
                    planets[index - 1]
                        .SystemEvolution
                        .PresentOrbitAstronomicalUnits;
                var outerOrbit =
                    planets[index]
                        .SystemEvolution
                        .PresentOrbitAstronomicalUnits;

                if (innerOrbit >= outerOrbit)
                {
                    return 0.0;
                }

                var touchingLimit =
                    (outerOrbit - innerOrbit) /
                    (outerOrbit + innerOrbit);
                limit =
                    Math.Min(
                        limit,
                        touchingLimit *
                            0.9);
            }

            return Math.Max(
                0.0,
                limit);
        }

        private static bool IsUsablePrototype(
            CelestialBodyDefinition definition)
        {
            return
                definition != null &&
                definition.HasValidPhysicalSettings &&
                definition.HasValidResolvedSurfaceSettings;
        }

        private static bool IsValidScaleRange(
            double minimum,
            double maximum)
        {
            return
                IsFinite(minimum) &&
                IsFinite(maximum) &&
                minimum > 0.0 &&
                maximum >= minimum;
        }

        private static double LogarithmicLerp(
            double minimum,
            double maximum,
            double fraction)
        {
            if (minimum == maximum)
            {
                return minimum;
            }

            return Math.Exp(
                Math.Log(minimum) +
                (Math.Log(maximum) -
                    Math.Log(minimum)) *
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
                state =
                    unchecked((uint)seed);

                if (state == 0)
                {
                    state =
                        0x6D2B79F5u;
                }
            }

            public int NextInclusive(
                int minimum,
                int maximum)
            {
                var range =
                    (uint)(maximum -
                        minimum +
                        1);

                return
                    minimum +
                    (int)(NextUInt() %
                        range);
            }

            public int NextInt()
            {
                return
                    unchecked((int)NextUInt());
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
