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

        [Header("Generated Stellar Visuals")]
        [SerializeField]
        private RoundMapMagicSurfaceDefinition stellarPhotosphereOrdinarySurface;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition stellarPhotosphereConvectiveSurface;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition stellarPhotosphereAtmosphereDominatedSurface;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition stellarCompactEmitterSurface;

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

            var hasBinary = false;
            var companionFormation = default(CelestialStellarCompanionFormation);
            var companionPopulation = default(CelestialStellarPopulationSample);
            var companionProperties = default(CelestialStellarEvolutionResult);

            if (!CelestialStellarMultiplicityModel.TryGenerate(
                    request.Seed,
                    stellarPopulation.InitialMassSolar,
                    request.Environment.IronMetallicityDex,
                    request.Environment.BirthStellarDensityPerCubicParsec,
                    out var multiplicity,
                    out error))
            {
                return false;
            }

            // Triples remain data-model outcomes until the binary runtime path is validated.
            if (multiplicity.Multiplicity == CelestialStellarMultiplicity.Binary)
            {
                companionFormation = multiplicity.Companions[0];
                companionPopulation =
                    CreatePopulationSample(
                        companionFormation.BirthMassSolar,
                        request.Environment.SystemAgeGigayears);

                if (!CelestialStellarEvolutionModel.TryEvaluate(
                        companionPopulation,
                        request.Environment,
                        out companionProperties,
                        out error))
                {
                    return false;
                }

                hasBinary =
                    IsBinarySceneCandidate(
                        stellarProperties,
                        companionProperties,
                        companionFormation);
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

            if (hasBinary)
            {
                var circumprimaryStableLimitMeters =
                    companionFormation.PeriapsisAstronomicalUnits *
                    0.2 *
                    AstronomicalUnitMeters;
                outerPlanetOrbitMeters =
                    Math.Min(
                        outerPlanetOrbitMeters,
                        circumprimaryStableLimitMeters);

                if (outerPlanetOrbitMeters <=
                    innerPlanetOrbitMeters * 1.25)
                {
                    planetCount = 0;
                }
            }

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

                var stableInBinary =
                    !hasBinary ||
                    systemEvolution.PresentOrbitAstronomicalUnits *
                        (1.0 + maximumPlanetEccentricity) <
                    companionFormation.PeriapsisAstronomicalUnits *
                        0.2;

                if (systemEvolution.Survived &&
                    stableInBinary)
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
                    survivingPlanets.Count + (hasBinary ? 2 : 1));
            var ownedRuntimeObjects =
                new List<UnityEngine.Object>(
                    (survivingPlanets.Count + (hasBinary ? 2 : 1)) *
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
                    starDescription,
                    SelectStellarSurfaceDefinition(
                        stellarProperties));
            ownedRuntimeObjects.Add(
                star);

            if (!CelestialBodyRotationModel.TryEvaluateStar(random.NextInt(), stellarProperties, out var starRotation, out error)) { ReleaseOwnedRuntimeObjects(ownedRuntimeObjects); return false; }
            star.ConfigureRuntimeRotationProperties(starRotation);
            if (!CelestialStellarMagneticActivityModel.TryEvaluate(random.NextInt(), stellarProperties, starRotation, out var starMagneticActivity, out error)) { ReleaseOwnedRuntimeObjects(ownedRuntimeObjects); return false; }
            star.ConfigureRuntimeStellarMagneticActivityProperties(starMagneticActivity);

            var referencePoints =
                new List<CelestialStarSystemPlan.ReferencePointPlan>();

            if (hasBinary)
            {
                var companionDescription =
                    CelestialObjectDescriptionGenerator.DescribeStar(
                        companionPopulation,
                        companionProperties,
                        request.Environment);
                star.ConfigureRuntimeDescription(
                    starDescription +
                    " It is the primary member of a binary stellar system.");
                var companion =
                    CreateStellarDefinition(
                        starDefinition,
                        "generated-star-companion",
                        random.NextInt(),
                        companionProperties,
                        companionDescription +
                        " It is the secondary member of a binary stellar system.",
                        SelectStellarSurfaceDefinition(
                            companionProperties));
                ownedRuntimeObjects.Add(
                    companion);

                if (!CelestialBodyRotationModel.TryEvaluateStar(random.NextInt(), companionProperties, out var companionRotation, out error)) { ReleaseOwnedRuntimeObjects(ownedRuntimeObjects); return false; }
                companion.ConfigureRuntimeRotationProperties(companionRotation);
                if (!CelestialStellarMagneticActivityModel.TryEvaluate(random.NextInt(), companionProperties, companionRotation, out var companionMagneticActivity, out error)) { ReleaseOwnedRuntimeObjects(ownedRuntimeObjects); return false; }
                companion.ConfigureRuntimeStellarMagneticActivityProperties(companionMagneticActivity);

                var totalStellarMassKilograms =
                    star.MassKilograms +
                    companion.MassKilograms;
                var phaseRadians =
                    random.Next01() *
                    Math.PI *
                    2.0;
                const string barycenterId =
                    "stellar-barycenter";
                referencePoints.Add(
                    new CelestialStarSystemPlan.ReferencePointPlan(
                        barycenterId,
                        totalStellarMassKilograms,
                        new DoubleVector3(),
                        new DoubleVector3()));

                var relativeTrajectory =
                    new KeplerianConicTrajectory(
                        barycenterId,
                        companionFormation.PeriapsisAstronomicalUnits *
                            AstronomicalUnitMeters,
                        companionFormation.Eccentricity,
                        0.0,
                        phaseRadians *
                            180.0 /
                            Math.PI,
                        companionFormation.InclinationDegrees,
                        0.0,
                        0.0,
                        CelestialOrbitDirection.Prograde);
                var twoBodyOrbit =
                    new CelestialTwoBodyBarycentricOrbit(
                        "stellar-pair",
                        barycenterId,
                        star.MassKilograms,
                        companion.MassKilograms,
                        relativeTrajectory);

                bodySystems.Add(
                    CreateSingleBodySystem(
                        "stellar",
                        "star",
                        star,
                        starQualityProfile,
                        new DoubleVector3(),
                        new DoubleVector3(),
                        ownedRuntimeObjects,
                        new CelestialTrajectoryDefinition(
                            twoBodyOrbit,
                            CelestialTwoBodyComponent.BodyA),
                        barycenterId));
                bodySystems.Add(
                    CreateSingleBodySystem(
                        "stellar-companion",
                        "star",
                        companion,
                        starQualityProfile,
                        new DoubleVector3(),
                        new DoubleVector3(),
                        ownedRuntimeObjects,
                        new CelestialTrajectoryDefinition(
                            twoBodyOrbit,
                            CelestialTwoBodyComponent.BodyB),
                        barycenterId));
            }
            else
            {
                bodySystems.Add(
                    CreateSingleBodySystem(
                        "stellar",
                        "star",
                        star,
                        starQualityProfile,
                        new DoubleVector3(),
                        new DoubleVector3(),
                        ownedRuntimeObjects));
            }

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

                if (!CelestialBodyRotationModel.TryEvaluatePlanet(
                        planetSeed,
                        formation,
                        systemEvolution.PresentOrbitAstronomicalUnits,
                        stellarProperties.CurrentMassSolar,
                        request.Environment.SystemAgeGigayears,
                        out var planetRotation,
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
                        planetaryEvolution,
                        planetRotation);
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

                if (!CelestialRingSystemModel.TryGenerate(
                        planetSeed,
                        formation,
                        moonSystem,
                        out var ringSystem,
                        out error))
                {
                    ReleaseOwnedRuntimeObjects(
                        ownedRuntimeObjects);
                    return false;
                }

                planet.ConfigureRuntimeRingSystemProperties(
                    ringSystem);

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

                    if (!CelestialMoonEvolutionModel.TryEvaluate(
                            moonSeed,
                            moonFormation,
                            formation,
                            stellarProperties,
                            request.Environment.SystemAgeGigayears,
                            systemEvolution.PresentOrbitAstronomicalUnits,
                            out var moonEvolution,
                            out error))
                    {
                        ReleaseOwnedRuntimeObjects(
                            ownedRuntimeObjects);
                        return false;
                    }

                    if (!CelestialBodyRotationModel.TryEvaluateMoon(
                            moonSeed,
                            moonFormation,
                            moonEvolution,
                            formation.TotalMassEarth,
                            out var moonRotation,
                            out error))
                    {
                        ReleaseOwnedRuntimeObjects(
                            ownedRuntimeObjects);
                        return false;
                    }

                    var moon =
                        CreateMoonDefinition(
                            moonDefinition,
                            $"generated-{instanceId}-moon-{generatedMoons.Count + 1}",
                            moonSeed,
                            moonFormation,
                            moonSystem,
                            moonEvolution,
                            moonRotation,
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
                    formationDisk,
                    referencePoints);
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
            string description,
            RoundMapMagicSurfaceDefinition stellarSurface)
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
                stellarSurface != null
                    ? stellarSurface
                    : prototype.RoundMapMagicSurface,
                prototype.OceanDefinition);
            definition.ConfigureRuntimeStellarProperties(
                properties);
            definition.ConfigureRuntimeGravitationalLensing(
                SelectStellarGravitationalLensingSettings(
                    properties));
            definition.ConfigureRuntimeDescription(
                description);
            return definition;
        }

        private static CelestialGravitationalLensingSettings
            SelectStellarGravitationalLensingSettings(
                CelestialStellarEvolutionResult properties)
        {
            if (properties.EvolutionState !=
                CelestialStellarEvolutionState.BlackHole)
            {
                return CelestialGravitationalLensingSettings.Default;
            }

            // A stylized v1 visual influence region. This is intentionally broader
            // than the physical event horizon so the effect is visible at system scale.
            return new CelestialGravitationalLensingSettings(
                true,
                1000000.0f,
                0.18f,
                1.5f,
                0.45f);
        }

        private RoundMapMagicSurfaceDefinition SelectStellarSurfaceDefinition(
            CelestialStellarEvolutionResult properties)
        {
            if (properties.EvolutionState !=
                    CelestialStellarEvolutionState.MainSequence)
            {
                return stellarCompactEmitterSurface;
            }

            if (properties.EffectiveTemperatureKelvin < 4500.0 ||
                properties.CurrentMassSolar < 0.7)
            {
                return stellarPhotosphereConvectiveSurface;
            }

            if (properties.EffectiveTemperatureKelvin > 9000.0 ||
                properties.CurrentMassSolar > 1.5)
            {
                return stellarPhotosphereAtmosphereDominatedSurface;
            }

            return stellarPhotosphereOrdinarySurface;
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
            CelestialPlanetaryEvolutionResult evolution,
            CelestialBodyRotationResult rotation)
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
            definition.ConfigureRuntimeRotationProperties(
                rotation);
            definition.ConfigureRuntimeDescription(
                CelestialObjectDescriptionGenerator.DescribePlanet(
                    formation,
                    evolution,
                    systemEvolution,
                    rotation,
                    generationSeed));
            return definition;
        }

        private static CelestialBodyDefinition CreateMoonDefinition(
            CelestialBodyDefinition prototype,
            string definitionId,
            int generationSeed,
            CelestialMoonFormationResult formation,
            CelestialMoonSystemFormationResult system,
            CelestialMoonEvolutionResult evolution,
            CelestialBodyRotationResult rotation,
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
            definition.ConfigureRuntimeMoonEvolutionProperties(
                evolution);
            definition.ConfigureRuntimeRotationProperties(
                rotation);
            definition.ConfigureRuntimeDescription(
                CelestialObjectDescriptionGenerator.DescribeMoon(
                    formation,
                    evolution,
                    rotation,
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
            ICollection<UnityEngine.Object> ownedRuntimeObjects,
            CelestialTrajectoryDefinition trajectory = null,
            string referencePointInstanceId = null)
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
                    CelestialBodySpawnMode.PrescribedTrajectory,
                    null,
                    null,
                    trajectory,
                    referencePointInstanceId);
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

        private static CelestialStellarPopulationSample CreatePopulationSample(
            double birthMassSolar,
            double systemAgeGigayears)
        {
            var lifetimeGigayears =
                10.0 *
                Math.Pow(
                    birthMassSolar,
                    -2.5);
            CelestialStellarEvolutionState state;

            if (systemAgeGigayears <= lifetimeGigayears)
            {
                state = CelestialStellarEvolutionState.MainSequence;
            }
            else if (birthMassSolar < 8.0)
            {
                state = CelestialStellarEvolutionState.WhiteDwarf;
            }
            else if (birthMassSolar < 25.0)
            {
                state = CelestialStellarEvolutionState.NeutronStar;
            }
            else
            {
                state = CelestialStellarEvolutionState.BlackHole;
            }

            return
                new CelestialStellarPopulationSample(
                    birthMassSolar,
                    lifetimeGigayears,
                    state);
        }

        public static bool IsBinarySceneCandidate(
            CelestialStellarEvolutionResult primary,
            CelestialStellarEvolutionResult companion,
            CelestialStellarCompanionFormation formation)
        {
            var combinedRadiusAstronomicalUnits =
                (primary.RadiusSolar + companion.RadiusSolar) *
                SolarRadiusMeters /
                AstronomicalUnitMeters;
            return
                formation.PeriapsisAstronomicalUnits >
                    combinedRadiusAstronomicalUnits * 3.0;
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
