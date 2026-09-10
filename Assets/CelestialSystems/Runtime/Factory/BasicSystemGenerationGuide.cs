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

        [Header("Body Variation")]
        [SerializeField]
        [Min(0.01f)]
        private float minimumPlanetRadiusScale =
            0.5f;

        [SerializeField]
        [Min(0.01f)]
        private float maximumPlanetRadiusScale =
            1.5f;

        [SerializeField]
        [Min(0.01f)]
        private float minimumMoonRadiusScale =
            0.5f;

        [SerializeField]
        [Min(0.01f)]
        private float maximumMoonRadiusScale =
            1.5f;

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

        [Header("Prototype Moons")]
        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float moonChance;

        [SerializeField]
        [Min(1.01f)]
        private float minimumMoonOrbitPlanetRadii =
            3.0f;

        [SerializeField]
        [Min(1.01f)]
        private float maximumMoonOrbitPlanetRadii =
            30.0f;

        [SerializeField]
        [Range(0.0f, 90.0f)]
        private float maximumMoonInclinationDegrees =
            15.0f;

        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float maximumMoonEccentricity =
            0.08f;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float moonRetrogradeChance;

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

            Array.Sort(
                planetFormation,
                (left, right) =>
                    left.FinalOrbitAstronomicalUnits.CompareTo(
                        right.FinalOrbitAstronomicalUnits));

            var safePlanetEccentricityLimit =
                CalculateNonCrossingPlanetEccentricityLimit(
                    planetFormation);
            var bodySystems =
                new List<CelestialStarSystemPlan.BodySystemPlan>(
                    planetCount + 1);
            var ownedRuntimeObjects =
                new List<UnityEngine.Object>(
                    (planetCount + 1) *
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
                index < planetCount;
                index++)
            {
                var formation =
                    planetFormation[index];
                var orbitRadius =
                    formation.FinalOrbitAstronomicalUnits *
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
                var planet =
                    CreatePlanetDefinition(
                        planetDefinition,
                        $"generated-{instanceId}",
                        random.NextInt(),
                        formation);
                ownedRuntimeObjects.Add(
                    planet);

                CelestialBodyDefinition moon = null;
                var moonOrbitRadius =
                    0.0;
                var moonPhaseRadians =
                    0.0;
                var moonInclinationRadians =
                    0.0;
                var moonDirection =
                    1.0;
                var moonEccentricity =
                    0.0;
                var moonArgumentOfPeriapsisRadians =
                    0.0;

                if (moonChance > 0.0f &&
                    random.Next01() < moonChance)
                {
                    var minimumMoonOrbitRadius =
                        planet.ReferenceRadiusMeters *
                        minimumMoonOrbitPlanetRadii;
                    var configuredMaximumMoonOrbitRadius =
                        planet.ReferenceRadiusMeters *
                        maximumMoonOrbitPlanetRadii;
                    var hillRadius =
                        orbitRadius *
                        Math.Pow(
                            planet.MassKilograms /
                            (3.0 *
                                star.MassKilograms),
                            1.0 /
                            3.0);
                    var stableMaximumMoonOrbitRadius =
                        hillRadius *
                        0.35;
                    moonEccentricity =
                        random.NextRange(
                            0.0,
                            maximumMoonEccentricity);
                    moonArgumentOfPeriapsisRadians =
                        random.Next01() *
                        Math.PI *
                        2.0;
                    var maximumStablePeriapsis =
                        stableMaximumMoonOrbitRadius *
                        (1.0 - moonEccentricity) /
                        (1.0 + moonEccentricity);
                    var maximumMoonOrbitRadius =
                        Math.Min(
                            configuredMaximumMoonOrbitRadius,
                            maximumStablePeriapsis);

                    if (maximumMoonOrbitRadius >=
                        minimumMoonOrbitRadius)
                    {
                        moon =
                            CreateVariedDefinition(
                                moonDefinition,
                                $"generated-{instanceId}-moon-1",
                                random.NextInt(),
                                random.NextRange(
                                    minimumMoonRadiusScale,
                                    maximumMoonRadiusScale));
                        ownedRuntimeObjects.Add(
                            moon);
                        moonOrbitRadius =
                            random.NextRange(
                                minimumMoonOrbitRadius,
                                maximumMoonOrbitRadius);
                        moonPhaseRadians =
                            random.Next01() *
                            Math.PI *
                            2.0;
                        moonInclinationRadians =
                            (random.Next01() * 2.0 - 1.0) *
                            maximumMoonInclinationDegrees *
                            Math.PI /
                            180.0;
                        moonDirection =
                            random.Next01() <
                                moonRetrogradeChance
                                    ? -1.0
                                    : 1.0;
                    }
                }

                bodySystems.Add(
                    CreatePlanetaryBodySystem(
                        instanceId,
                        planet,
                        planetQualityProfile,
                        moon,
                        moonQualityProfile,
                        position,
                        velocity,
                        orbitRadius,
                        phaseRadians,
                        inclinationRadians,
                        direction,
                        eccentricity,
                        argumentOfPeriapsisRadians,
                        moonOrbitRadius,
                        moonPhaseRadians,
                        moonInclinationRadians,
                        moonDirection,
                        moonEccentricity,
                        moonArgumentOfPeriapsisRadians,
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

            if (moonChance > 0.0f &&
                !IsUsablePrototype(
                    moonDefinition))
            {
                error =
                    "The basic system generation guide requires a valid, spawnable moon body definition when moon chance is greater than zero.";
                return false;
            }

            if (!IsValidScaleRange(
                    minimumPlanetRadiusScale,
                    maximumPlanetRadiusScale) ||
                !IsValidScaleRange(
                    minimumMoonRadiusScale,
                    maximumMoonRadiusScale))
            {
                error =
                    "The basic system generation guide has an invalid body radius-scale range.";
                return false;
            }

            if (minimumMoonOrbitPlanetRadii <= 1.0f ||
                maximumMoonOrbitPlanetRadii <
                    minimumMoonOrbitPlanetRadii)
            {
                error =
                    "The basic system generation guide has an invalid moon orbital-radius range.";
                return false;
            }

            if (!IsFinite(
                    maximumPlanetEccentricity) ||
                maximumPlanetEccentricity < 0.0 ||
                maximumPlanetEccentricity >= 1.0 ||
                !IsFinite(
                    maximumMoonEccentricity) ||
                maximumMoonEccentricity < 0.0 ||
                maximumMoonEccentricity >= 1.0)
            {
                error =
                    "The basic system generation guide requires eccentricity limits from zero up to, but not including, one.";
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

        private static CelestialBodyDefinition CreatePlanetDefinition(
            CelestialBodyDefinition prototype,
            string definitionId,
            int generationSeed,
            CelestialPlanetFormationResult formation)
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
            definition.ConfigureRuntimeDescription(
                CelestialObjectDescriptionGenerator.DescribePlanet(
                    formation));
            return definition;
        }

        private static CelestialBodyDefinition CreateVariedDefinition(
            CelestialBodyDefinition prototype,
            string definitionId,
            int generationSeed,
            double radiusScale)
        {
            var definition =
                CreateInstance<CelestialBodyDefinition>();
            definition.name =
                definitionId;
            definition.hideFlags =
                HideFlags.DontSave;
            definition.ConfigureRuntime(
                definitionId,
                prototype.MassKilograms *
                    radiusScale *
                    radiusScale *
                    radiusScale,
                prototype.ReferenceRadiusMeters *
                    radiusScale,
                generationSeed,
                prototype.NorthAxis,
                prototype.PoleReferenceAxis,
                prototype.SurfaceSystem,
                prototype.RoundMapMagicSurface,
                prototype.OceanDefinition);
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
            CelestialBodyDefinition moon,
            RoundMapMagicSurfaceQualityProfile moonQualityProfile,
            DoubleVector3 position,
            DoubleVector3 velocity,
            double planetOrbitRadius,
            double planetPhaseRadians,
            double planetInclinationRadians,
            double planetDirection,
            double planetEccentricity,
            double planetArgumentOfPeriapsisRadians,
            double moonOrbitRadius,
            double moonPhaseRadians,
            double moonInclinationRadians,
            double moonDirection,
            double moonEccentricity,
            double moonArgumentOfPeriapsisRadians,
            ICollection<UnityEngine.Object> ownedRuntimeObjects)
        {
            var planetPosition =
                new DoubleVector3();
            var planetVelocity =
                new DoubleVector3();
            var entries =
                new List<CelestialBodySystemDefinition.BodyEntry>(
                    moon == null
                        ? 1
                        : 2);

            if (moon != null)
            {
                var totalMass =
                    planet.MassKilograms +
                    moon.MassKilograms;
                var relativeSpeed =
                    Math.Sqrt(
                        GravitationalConstant *
                        totalMass /
                        moonOrbitRadius);
                var cosinePhase =
                    Math.Cos(
                        moonPhaseRadians);
                var sinePhase =
                    Math.Sin(
                        moonPhaseRadians);
                var cosineInclination =
                    Math.Cos(
                        moonInclinationRadians);
                var sineInclination =
                    Math.Sin(
                        moonInclinationRadians);
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
                var planetDistance =
                    moonOrbitRadius *
                    moon.MassKilograms /
                    totalMass;
                var moonDistance =
                    moonOrbitRadius *
                    planet.MassKilograms /
                    totalMass;
                var planetSpeed =
                    relativeSpeed *
                    moon.MassKilograms /
                    totalMass;
                var moonSpeed =
                    relativeSpeed *
                    planet.MassKilograms /
                    totalMass;

                planetPosition =
                    radialDirection *
                    -planetDistance;
                planetVelocity =
                    tangentDirection *
                    (-planetSpeed *
                        moonDirection);

                entries.Add(
                    new CelestialBodySystemDefinition.BodyEntry(
                        "moon-1",
                        moon,
                        moonQualityProfile,
                        "planet",
                        CelestialBodySpawnMode.PrescribedTrajectory,
                        radialDirection *
                            moonDistance,
                        tangentDirection *
                            (moonSpeed *
                                moonDirection),
                        Vector3.zero,
                        new DoubleVector3(),
                        CreateConicTrajectory(
                            "planet",
                            moonOrbitRadius,
                            moonEccentricity,
                            moonPhaseRadians,
                            moonInclinationRadians,
                            moonArgumentOfPeriapsisRadians,
                            moonDirection)));
            }

            entries.Insert(
                0,
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
            IReadOnlyList<CelestialPlanetFormationResult> planets)
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
                        .FinalOrbitAstronomicalUnits;
                var outerOrbit =
                    planets[index]
                        .FinalOrbitAstronomicalUnits;

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
