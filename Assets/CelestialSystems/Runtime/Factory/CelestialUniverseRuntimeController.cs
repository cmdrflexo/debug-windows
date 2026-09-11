/*
 * Provides the scene-facing entry point for the universe-to-body runtime factory chain.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Serialization;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialUniverseRuntimeController :
        MonoBehaviour
    {
        private const double SolarMassKilograms =
            1.98847e30;

        private const double SolarRadiusMeters =
            6.957e8;

        private const double EarthMassKilograms =
            5.9722e24;

        private const double EarthRadiusMeters =
            6371000.0;

        [Header("Factory")]
        [SerializeField]
        private CelestialBodyFactory bodyFactory;

        [Header("Prototype Generation")]
        [SerializeField]
        private CelestialSystemGenerationGuide starSystemGuide;

        [SerializeField]
        private CelestialGalacticEnvironmentDefinition galacticEnvironment;

        [SerializeField]
        [Tooltip("Deterministic generation seed. Set to zero to choose a new random nonzero seed for each generation.")]
        private int seed = 1;

        [SerializeField]
        private string universeInstanceId =
            "universe-runtime";

        [SerializeField]
        private string galaxyInstanceId =
            "prototype-galaxy";

        [SerializeField]
        private string starSystemInstanceId =
            "prototype-star-system";

        [Header("Initial Motion")]
        [SerializeField]
        private DoubleVector3 positionMetersFromFrameOrigin;

        [SerializeField]
        private DoubleVector3 velocityMetersPerSecond;

        [SerializeField]
        private Vector3 rotationEulerDegrees;

        [Header("Observation")]
        [SerializeField]
        [Tooltip("Grid whose cell alignment origin is the generated system barycenter.")]
        private UniverseObservationGridRenderer observationGrid;

        [SerializeField]
        [Tooltip("Optional grid pivot marker that will point toward the generated star system's barycenter.")]
        private UniverseObservationPivotMarker observationPivotMarker;

        [SerializeField]
        [FormerlySerializedAs("planetMarkerTemplate")]
        [Tooltip("Optional scene marker whose appearance is used for every generated body marker.")]
        private UniverseObservationBodyMarkerDecorator bodyMarkerTemplate;

        [SerializeField]
        [FormerlySerializedAs("generatePlanetMarkers")]
        private bool generateBodyMarkers = true;

        [SerializeField]
        [Tooltip("Optional scene orbit decorator whose appearance is used for generated orbit paths.")]
        private UniverseObservationOrbitDecorator orbitDecoratorTemplate;

        [SerializeField]
        private bool generateOrbitPaths = true;

        [SerializeField]
        [Tooltip("Optional scene trail decorator whose appearance is used for generated body trails.")]
        private UniverseObservationTrailDecorator trailDecoratorTemplate;

        [SerializeField]
        private bool generateBodyTrails = true;

        [Header("Lifecycle")]
        [SerializeField]
        private Transform generatedParentOverride;

        [SerializeField]
        private bool generateOnStart = true;

        [Header("Development")]
        [SerializeField]
        [Tooltip("Print a compact star-and-planet summary after generation.")]
        private bool logGeneratedStarSystemSummary = true;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForFactory;

        [SerializeField]
        private bool generationAttempted;

        [SerializeField]
        private bool generationSucceeded;

        [SerializeField]
        private int resolvedSeed;

        [SerializeField]
        private Transform generatedUniverseRoot;

        [SerializeField]
        private int generatedGalaxyCount;

        [SerializeField]
        private int generatedStarSystemCount;

        [SerializeField]
        private int generatedBodySystemCount;

        [SerializeField]
        private int generatedBodyCount;

        [SerializeField]
        private bool hasGeneratedStarSystemBarycenter;

        [SerializeField]
        private UniversePosition generatedStarSystemBarycenter;

        [SerializeField]
        private string lastError;

        private CelestialUniverseFactory universeFactory;

        private CelestialUniverseFactory.GeneratedUniverse generatedUniverse;

        private readonly List<UniverseObservationBodyMarkerDecorator>
            generatedBodyMarkers =
                new List<UniverseObservationBodyMarkerDecorator>();

        private readonly List<UniverseObservationOrbitDecorator>
            generatedOrbitDecorators =
                new List<UniverseObservationOrbitDecorator>();

        private readonly List<UniverseObservationTrailDecorator>
            generatedTrailDecorators =
                new List<UniverseObservationTrailDecorator>();

        public CelestialBodyFactory BodyFactory =>
            bodyFactory;

        public CelestialSystemGenerationGuide StarSystemGuide =>
            starSystemGuide;

        public CelestialGalacticEnvironmentDefinition GalacticEnvironment =>
            galacticEnvironment;

        public int Seed =>
            seed;

        public bool WaitingForFactory =>
            waitingForFactory;

        public bool GenerationAttempted =>
            generationAttempted;

        public bool GenerationSucceeded =>
            generationSucceeded;

        public int ResolvedSeed =>
            resolvedSeed;

        public Transform GeneratedUniverseRoot =>
            generatedUniverseRoot;

        public int GeneratedGalaxyCount =>
            generatedGalaxyCount;

        public int GeneratedStarSystemCount =>
            generatedStarSystemCount;

        public int GeneratedBodySystemCount =>
            generatedBodySystemCount;

        public int GeneratedBodyCount =>
            generatedBodyCount;

        public bool HasGeneratedStarSystemBarycenter =>
            hasGeneratedStarSystemBarycenter;

        public UniversePosition GeneratedStarSystemBarycenter =>
            generatedStarSystemBarycenter;

        public string LastError =>
            lastError;

        private IEnumerator Start()
        {
            if (!generateOnStart)
            {
                yield break;
            }

            if (bodyFactory == null)
            {
                SetError(
                    "The universe runtime controller requires a celestial body factory.");
                yield break;
            }

            waitingForFactory = true;

            while (bodyFactory != null &&
                !bodyFactory.CanSpawnBodies)
            {
                yield return null;
            }

            waitingForFactory = false;

            if (bodyFactory == null)
            {
                SetError(
                    "The assigned celestial body factory was destroyed before it became ready.");
                yield break;
            }

            TryGenerate();
        }

        public bool TryGenerate()
        {
            if (generatedUniverse != null)
            {
                return SetError(
                    "This universe runtime controller already owns a generated universe.");
            }

            ResetRuntimeSummary();
            generationAttempted = true;

            if (!Application.isPlaying)
            {
                return SetError(
                    "A universe can only be generated while the application is playing.");
            }

            if (bodyFactory == null)
            {
                return SetError(
                    "The universe runtime controller requires a celestial body factory.");
            }

            if (starSystemGuide == null)
            {
                return SetError(
                    "The universe runtime controller requires a star-system generation guide.");
            }

            if (galacticEnvironment == null)
            {
                return SetError(
                    "The universe runtime controller requires a galactic environment.");
            }

            resolvedSeed =
                ResolveGenerationSeed(
                    seed);

            universeFactory ??=
                new CelestialUniverseFactory(                    bodyFactory);

            var parent =
                generatedParentOverride != null
                    ? generatedParentOverride
                    : transform;

            generationSucceeded =
                universeFactory.TryGenerate(
                    universeInstanceId,
                    galaxyInstanceId,
                    starSystemInstanceId,
                    resolvedSeed,
                    starSystemGuide,
                    galacticEnvironment,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    Quaternion.Euler(
                        rotationEulerDegrees),
                    parent,
                    out generatedUniverse);

            if (!generationSucceeded)
            {
                lastError =
                    universeFactory.LastError;
                return false;
            }

            generatedUniverseRoot =
                generatedUniverse.Root;
            generatedGalaxyCount = 1;
            generatedStarSystemCount = 1;

            var starSystem =
                generatedUniverse.Galaxy?.StarSystem;

            if (starSystem != null)
            {
                generatedBodySystemCount =
                    starSystem.BodySystems.Count;

                foreach (var bodySystem in
                    starSystem.BodySystems.Values)
                {
                    if (bodySystem != null)
                    {
                        generatedBodyCount +=
                            bodySystem.Bodies.Count;
                    }
                }

                SetObservationMarkerToBarycenter(
                    starSystem);
                ConfigureObservationBodyMarkers(
                    starSystem);
                ConfigureObservationOrbitDecorators(
                    starSystem);
                ConfigureObservationTrailDecorators(
                    starSystem);

                if (logGeneratedStarSystemSummary)
                {
                    LogStarSystemSummary(
                        starSystem);
                }
            }

            return true;
        }

        public bool TryDespawn()
        {
            if (universeFactory == null ||
                generatedUniverse == null)
            {
                return false;
            }

            if (!universeFactory.TryDespawn(
                    generatedUniverse))
            {
                lastError =
                    universeFactory.LastError;
                return false;
            }

            generatedUniverse = null;
            ResetRuntimeSummary();
            return true;
        }

        private void ConfigureObservationOrbitDecorators(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            ClearObservationOrbitDecorators();

            if (!generateOrbitPaths)
            {
                return;
            }

            orbitDecoratorTemplate ??=
                FindFirstObjectByType<UniverseObservationOrbitDecorator>();

            if (orbitDecoratorTemplate == null)
            {
                return;
            }

            orbitDecoratorTemplate.ClearTarget();

            var orbitingBodies =
                new List<CelestialBodyRuntimeContext>();

            foreach (var bodySystem in
                starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in
                    bodySystem.Bodies.Values)
                {
                    if (body != null &&
                        body.MotionProviderSource is
                            TrajectoryCelestialBodyMotionProvider)
                    {
                        orbitingBodies.Add(
                            body);
                    }
                }
            }

            orbitingBodies.Sort(
                CompareBodyMarkerTargets);

            for (var index = 0;
                index < orbitingBodies.Count;
                index++)
            {
                if (index == 0)
                {
                    orbitDecoratorTemplate.SetTarget(
                        orbitingBodies[index]);
                    continue;
                }

                var generatedDecorator =
                    orbitDecoratorTemplate.CreateRuntimeSibling(
                        orbitingBodies[index]);

                if (generatedDecorator != null)
                {
                    generatedOrbitDecorators.Add(
                        generatedDecorator);
                }
            }
        }

        private void ClearObservationOrbitDecorators()
        {
            if (orbitDecoratorTemplate != null)
            {
                orbitDecoratorTemplate.ClearTarget();
            }

            for (var index = 0;
                index < generatedOrbitDecorators.Count;
                index++)
            {
                if (generatedOrbitDecorators[index] != null)
                {
                    Destroy(
                        generatedOrbitDecorators[index]);
                }
            }

            generatedOrbitDecorators.Clear();
        }

        private void ConfigureObservationBodyMarkers(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            ClearObservationBodyMarkers();

            if (!generateBodyMarkers)
            {
                return;
            }

            bodyMarkerTemplate ??=
                FindFirstObjectByType<UniverseObservationBodyMarkerDecorator>();

            if (bodyMarkerTemplate == null)
            {
                return;
            }

            bodyMarkerTemplate.ClearTarget();

            var bodies =
                new List<CelestialBodyRuntimeContext>();

            foreach (var bodySystem in
                starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in
                    bodySystem.Bodies.Values)
                {
                    if (body != null &&
                        body.Definition != null)
                    {
                        bodies.Add(
                            body);
                    }
                }
            }

            bodies.Sort(
                CompareBodyMarkerTargets);

            for (var index = 0;
                index < bodies.Count;
                index++)
            {
                if (index == 0)
                {
                    bodyMarkerTemplate.SetTarget(
                        bodies[index]);
                    continue;
                }

                var generatedMarker =
                    bodyMarkerTemplate.CreateRuntimeSibling(
                        bodies[index]);

                if (generatedMarker != null)
                {
                    generatedBodyMarkers.Add(
                        generatedMarker);
                }
            }
        }

        private void ConfigureObservationTrailDecorators(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            ClearObservationTrailDecorators();

            if (!generateBodyTrails)
            {
                return;
            }

            trailDecoratorTemplate ??=
                FindFirstObjectByType<UniverseObservationTrailDecorator>();

            if (trailDecoratorTemplate == null)
            {
                return;
            }

            trailDecoratorTemplate.ClearTarget();
            var bodies = new List<CelestialBodyRuntimeContext>();

            foreach (var bodySystem in starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in bodySystem.Bodies.Values)
                {
                    if (body != null && body.Definition != null)
                    {
                        bodies.Add(body);
                    }
                }
            }

            bodies.Sort(CompareBodyMarkerTargets);

            for (var index = 0; index < bodies.Count; index++)
            {
                if (index == 0)
                {
                    trailDecoratorTemplate.SetTarget(bodies[index]);
                    continue;
                }

                var generated =
                    trailDecoratorTemplate.CreateRuntimeSibling(bodies[index]);

                if (generated != null)
                {
                    generatedTrailDecorators.Add(generated);
                }
            }
        }

        private void ClearObservationTrailDecorators()
        {
            if (trailDecoratorTemplate != null)
            {
                trailDecoratorTemplate.ClearTarget();
            }

            for (var index = 0;
                index < generatedTrailDecorators.Count;
                index++)
            {
                if (generatedTrailDecorators[index] != null)
                {
                    Destroy(generatedTrailDecorators[index]);
                }
            }

            generatedTrailDecorators.Clear();
        }

        private static int CompareBodyMarkerTargets(
            CelestialBodyRuntimeContext left,
            CelestialBodyRuntimeContext right)
        {
            var leftRank =
                ResolveBodyMarkerRank(
                    left?.Definition);
            var rightRank =
                ResolveBodyMarkerRank(
                    right?.Definition);
            var rankComparison =
                leftRank.CompareTo(
                    rightRank);

            if (rankComparison != 0)
            {
                return rankComparison;
            }

            if (leftRank == 1)
            {
                var orbitComparison =
                    ResolvePresentOrbit(
                        left.Definition).CompareTo(
                            ResolvePresentOrbit(
                                right.Definition));

                if (orbitComparison != 0)
                {
                    return orbitComparison;
                }
            }

            return
                string.Compare(
                    left?.InstanceId,
                    right?.InstanceId,
                    StringComparison.Ordinal);
        }

        private static int ResolveBodyMarkerRank(
            CelestialBodyDefinition definition)
        {
            if (definition == null)
            {
                return 3;
            }

            if (definition.HasStellarProperties)
            {
                return 0;
            }

            if (definition.HasPlanetFormationProperties)
            {
                return 1;
            }

            return 2;
        }

        private void ClearObservationBodyMarkers()
        {
            if (bodyMarkerTemplate != null)
            {
                bodyMarkerTemplate.ClearTarget();
            }

            for (var index = 0;
                index < generatedBodyMarkers.Count;
                index++)
            {
                if (generatedBodyMarkers[index] != null)
                {
                    Destroy(
                        generatedBodyMarkers[index]);
                }
            }

            generatedBodyMarkers.Clear();
        }

        private void LogStarSystemSummary(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            CelestialBodyDefinition star = null;
            var planets =
                new List<CelestialBodyDefinition>();
            var moons =
                new List<CelestialBodyDefinition>();

            foreach (var bodySystem in
                starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in
                    bodySystem.Bodies.Values)
                {
                    var definition =
                        body != null
                            ? body.Definition
                            : null;

                    if (definition == null)
                    {
                        continue;
                    }

                    if (definition.HasStellarProperties)
                    {
                        star ??= definition;
                    }
                    else if (definition.HasPlanetFormationProperties)
                    {
                        planets.Add(
                            definition);
                    }
                    else if (definition.HasMoonFormationProperties)
                    {
                        moons.Add(
                            definition);
                    }
                }
            }

            planets.Sort(
                (left, right) =>
                    ResolvePresentOrbit(left).CompareTo(
                        ResolvePresentOrbit(right)));
            moons.Sort(
                (left, right) =>
                    left.MoonOrbitalRadiusMeters.CompareTo(
                        right.MoonOrbitalRadiusMeters));

            var builder =
                new StringBuilder();
            builder.Append(
                "Generated star system '");
            builder.Append(
                starSystem.InstanceId);
            builder.Append(
                "' (seed ");
            builder.Append(
                starSystem.Seed);
            builder.AppendLine(
                "):");

            if (star != null)
            {
                AppendSummaryLine(
                    builder,
                    "star",
                    DescribeStarClassification(
                        star),
                    star.MassKilograms /
                        SolarMassKilograms,
                    "Solar masses",
                    star.ReferenceRadiusMeters /
                        SolarRadiusMeters,
                    "Solar radii");
            }

            for (var index = 0;
                index < planets.Count;
                index++)
            {
                var planet =
                    planets[index];

                AppendSummaryLine(
                    builder,
                    $"planet {index + 1}",
                    DescribePlanetClassification(
                        planet.PlanetFormationClass),
                    planet.MassKilograms /
                        EarthMassKilograms,
                    "Earth masses",
                    planet.ReferenceRadiusMeters /
                        EarthRadiusMeters,
                    "Earth radii");
            }

            for (var index = 0;
                index < moons.Count;
                index++)
            {
                var moon =
                    moons[index];

                AppendSummaryLine(
                    builder,
                    $"moon {index + 1}",
                    DescribeMoonClassification(
                        moon.MoonFormationOrigin),
                    moon.MassKilograms /
                        EarthMassKilograms,
                    "Earth masses",
                    moon.ReferenceRadiusMeters /
                        EarthRadiusMeters,
                    "Earth radii");
            }

            Debug.Log(
                builder.ToString().TrimEnd(),
                this);
        }

        private static void AppendSummaryLine(
            StringBuilder builder,
            string label,
            string classification,
            double mass,
            string massUnit,
            double radius,
            string radiusUnit)
        {
            builder.Append(
                label.PadRight(
                    9));
            builder.Append(
                "- ");
            builder.Append(
                classification.PadRight(
                    31));
            builder.Append(
                " [");
            builder.Append(
                FormatSummaryValue(
                    mass).PadLeft(
                        10));
            builder.Append(
                " ");
            builder.Append(
                massUnit.PadRight(
                    12));
            builder.Append(
                "] [");
            builder.Append(
                FormatSummaryValue(
                    radius).PadLeft(
                        7));
            builder.Append(
                " ");
            builder.Append(
                radiusUnit);
            builder.AppendLine(
                "]");
        }

        private static string DescribeStarClassification(
            CelestialBodyDefinition star)
        {
            switch (star.StellarEvolutionState)
            {
                case CelestialStellarEvolutionState.WhiteDwarf:
                    return "white dwarf";
                case CelestialStellarEvolutionState.NeutronStar:
                    return "neutron star";
                case CelestialStellarEvolutionState.BlackHole:
                    return "stellar-mass black hole";
                case CelestialStellarEvolutionState.MainSequence:
                    var massSolar =
                        star.MassKilograms /
                        SolarMassKilograms;

                    if (massSolar < 0.5)
                    {
                        return "low-mass main-sequence star";
                    }

                    if (massSolar < 1.5)
                    {
                        return "solar-type main-sequence star";
                    }

                    return "intermediate-mass main-sequence star";
                default:
                    return "unclassified star";
            }
        }

        private static string DescribePlanetClassification(
            CelestialPlanetFormationClass formationClass)
        {
            switch (formationClass)
            {
                case CelestialPlanetFormationClass.Rocky:
                    return "rocky planet";
                case CelestialPlanetFormationClass.VolatileRich:
                    return "volatile-rich planet";
                case CelestialPlanetFormationClass.GasRich:
                    return "gas-rich planet";
                case CelestialPlanetFormationClass.Giant:
                    return "giant planet";
                default:
                    return "unclassified planet";
            }
        }

        private static string DescribeMoonClassification(
            CelestialMoonFormationOrigin origin)
        {
            switch (origin)
            {
                case CelestialMoonFormationOrigin.RegularDisk:
                    return "regular moon";
                case CelestialMoonFormationOrigin.GiantImpact:
                    return "impact-formed moon";
                case CelestialMoonFormationOrigin.Captured:
                    return "captured moon";
                default:
                    return "unclassified moon";
            }
        }

        private static double ResolvePresentOrbit(
            CelestialBodyDefinition planet)
        {
            return
                planet.HasPostMainSequenceProperties
                    ? planet.PlanetPresentOrbitAstronomicalUnits
                    : planet.PlanetFinalOrbitAstronomicalUnits;
        }

        private static int ResolveGenerationSeed(
            int configuredSeed)
        {
            if (configuredSeed != 0)
            {
                return configuredSeed;
            }

            var randomSeed =
                Guid.NewGuid().GetHashCode();

            return
                randomSeed != 0
                    ? randomSeed
                    : 1;
        }

        private static string FormatSummaryValue(
            double value)
        {
            return
                value.ToString(
                    "0.###",
                    CultureInfo.InvariantCulture);
        }

        private void SetObservationMarkerToBarycenter(
            CelestialStarSystemFactory.GeneratedSystem starSystem)
        {
            observationPivotMarker ??=
                FindFirstObjectByType<UniverseObservationPivotMarker>();

            observationGrid ??=
                FindFirstObjectByType<UniverseObservationGridRenderer>();

            if (!TryCalculateBarycenter(
                    starSystem,
                    out generatedStarSystemBarycenter))
            {
                hasGeneratedStarSystemBarycenter = false;
                return;
            }

            hasGeneratedStarSystemBarycenter = true;
            if (observationPivotMarker != null)
            {
                observationPivotMarker.SetDirectionReference(
                    generatedStarSystemBarycenter);
            }

            if (observationGrid != null)
            {
                observationGrid.SetGridOrigin(
                    generatedStarSystemBarycenter);
            }
        }

        private static bool TryCalculateBarycenter(
            CelestialStarSystemFactory.GeneratedSystem starSystem,
            out UniversePosition barycenter)
        {
            barycenter = default;

            if (starSystem == null)
            {
                return false;
            }

            var hasOrigin = false;
            var origin = default(UniversePosition);
            var weightedX = 0.0;
            var weightedY = 0.0;
            var weightedZ = 0.0;
            var totalMass = 0.0;

            foreach (var bodySystem in
                starSystem.BodySystems.Values)
            {
                if (bodySystem == null)
                {
                    continue;
                }

                foreach (var body in
                    bodySystem.Bodies.Values)
                {
                    if (body == null ||
                        body.Definition == null ||
                        !IsFinitePositive(
                            body.Definition.MassKilograms) ||
                        !body.TryGetMotionState(
                            out var motionState))
                    {
                        continue;
                    }

                    if (!hasOrigin)
                    {
                        origin = motionState.Position;
                        hasOrigin = true;
                    }

                    if (!motionState.Position.TryGetOffsetMetersFrom(
                            origin,
                            out var offset))
                    {
                        continue;
                    }

                    var mass =
                        body.Definition.MassKilograms;
                    weightedX +=
                        offset.x *
                        mass;
                    weightedY +=
                        offset.y *
                        mass;
                    weightedZ +=
                        offset.z *
                        mass;
                    totalMass +=
                        mass;
                }
            }

            if (!hasOrigin ||
                !IsFinitePositive(
                    totalMass))
            {
                return false;
            }

            var offsetX =
                weightedX /
                totalMass;
            var offsetY =
                weightedY /
                totalMass;
            var offsetZ =
                weightedZ /
                totalMass;

            if (!IsFinite(
                    offsetX) ||
                !IsFinite(
                    offsetY) ||
                !IsFinite(
                    offsetZ))
            {
                return false;
            }

            barycenter = origin;
            barycenter.AddLocalMeters(
                offsetX,
                offsetY,
                offsetZ);
            return true;
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

        private void ResetRuntimeSummary()
        {
            generationSucceeded = false;
            resolvedSeed = 0;
            generatedUniverseRoot = null;
            generatedGalaxyCount = 0;
            generatedStarSystemCount = 0;
            generatedBodySystemCount = 0;
            generatedBodyCount = 0;

            if (hasGeneratedStarSystemBarycenter &&
                observationPivotMarker != null)
            {
                observationPivotMarker.ClearDirectionReference();
            }

            if (hasGeneratedStarSystemBarycenter &&
                observationGrid != null)
            {
                observationGrid.ClearGridOrigin();
            }

            hasGeneratedStarSystemBarycenter = false;
            generatedStarSystemBarycenter = default;
            ClearObservationBodyMarkers();
            ClearObservationOrbitDecorators();
            ClearObservationTrailDecorators();
            lastError = string.Empty;
        }

        private bool SetError(
            string error)
        {
            generationSucceeded = false;
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }
    }
}
