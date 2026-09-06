/*
 * Holds detached body-editor values, validates free and on-rails motion, and builds runtime spawn requests.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum CelestialBodySpawnMode
    {
        FreeSimulation,
        OnRails
    }

    [Serializable]
    public sealed class CelestialOrbitParameters
    {
        public double semiMajorAxisMeters = 10000000.0;
        [Range(0.0f, 0.999999f)] public double eccentricity;
        public double inclinationDegrees;
        public double longitudeAscendingNodeDegrees;
        public double argumentOfPeriapsisDegrees;
        public double trueAnomalyDegrees;

        public bool HasValidSettings =>
            IsFinite(semiMajorAxisMeters) && semiMajorAxisMeters > 0.0 &&
            IsFinite(eccentricity) && eccentricity >= 0.0 && eccentricity < 1.0 &&
            IsFinite(inclinationDegrees) && IsFinite(longitudeAscendingNodeDegrees) &&
            IsFinite(argumentOfPeriapsisDegrees) && IsFinite(trueAnomalyDegrees);

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }

    [Serializable]
    public sealed class CelestialBodyEditorModel
    {
        private const double GravitationalConstant = 6.67430e-11;

        public string instanceId = "spawned-body";
        public string definitionId = "body";
        public double massKilograms = 1.0;
        public double referenceRadiusMeters = 500000.0;
        public int generationSeed;
        public Vector3 northAxis = Vector3.up;
        public Vector3 poleReferenceAxis = Vector3.forward;
        public CelestialSurfaceSystem surfaceSystem = CelestialSurfaceSystem.AutomaticByDiameter;
        public RoundMapMagicSurfaceDefinition surfaceDefinition;
        public OceanDefinition oceanDefinition;
        public RoundMapMagicSurfaceQualityProfile qualityProfile;
        public CelestialBodySpawnMode spawnMode;
        public DoubleVector3 freePositionMetersFromFrameOrigin;
        public DoubleVector3 freeVelocityMetersPerSecond;
        public Vector3 rotationEulerDegrees;
        public DoubleVector3 angularVelocityRadiansPerSecond;
        public CelestialBodyRuntimeContext orbitParent;
        public CelestialOrbitParameters orbit = new CelestialOrbitParameters();

        public void Load(CelestialBodyDefinition source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            definitionId = source.DefinitionId;
            massKilograms = source.MassKilograms;
            referenceRadiusMeters = source.ReferenceRadiusMeters;
            generationSeed = source.GenerationSeed;
            northAxis = source.NorthAxis;
            poleReferenceAxis = source.PoleReferenceAxis;
            surfaceSystem = source.SurfaceSystem;
            surfaceDefinition = source.RoundMapMagicSurface;
            oceanDefinition = source.OceanDefinition;
        }

        public bool TryBuildSpawnRequest(
            UniverseFrameController universeFrame,
            out CelestialBodySpawnRequest request,
            out string error)
        {
            request = null;
            if (!TryCreateRuntimeDefinition(out var definition, out error))
                return false;

            var position = freePositionMetersFromFrameOrigin;
            var velocity = freeVelocityMetersPerSecond;
            if (spawnMode == CelestialBodySpawnMode.OnRails &&
                !TryCalculateOnRailsState(universeFrame, out position, out velocity, out error))
            {
                UnityEngine.Object.Destroy(definition);
                return false;
            }

            request = new CelestialBodySpawnRequest(
                instanceId,
                definition,
                position,
                velocity,
                Quaternion.Euler(rotationEulerDegrees),
                qualityProfile,
                null,
                spawnMode,
                orbitParent,
                angularVelocityRadiansPerSecond);
            error = string.Empty;
            return true;
        }

        public bool TryCreateRuntimeDefinition(out CelestialBodyDefinition definition, out string error)
        {
            definition = ScriptableObject.CreateInstance<CelestialBodyDefinition>();
            definition.hideFlags = HideFlags.DontSave;
            definition.name = string.IsNullOrWhiteSpace(definitionId) ? "Runtime Body" : definitionId.Trim();
            definition.ConfigureRuntime(
                definitionId, massKilograms, referenceRadiusMeters, generationSeed,
                northAxis, poleReferenceAxis, surfaceSystem, surfaceDefinition, oceanDefinition);

            if (!definition.HasValidPhysicalSettings)
            {
                UnityEngine.Object.Destroy(definition);
                definition = null;
                error = "The edited body has invalid physical settings.";
                return false;
            }

            if (!definition.HasValidResolvedSurfaceSettings)
            {
                UnityEngine.Object.Destroy(definition);
                definition = null;
                error = "The edited body has invalid settings for its resolved surface system.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                UnityEngine.Object.Destroy(definition);
                definition = null;
                error = "A body instance ID is required.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool TryCalculateOnRailsState(
            UniverseFrameController universeFrame,
            out DoubleVector3 position,
            out DoubleVector3 velocity,
            out string error)
        {
            position = default;
            velocity = default;
            if (universeFrame == null || orbitParent == null ||
                orbitParent.Definition == null ||
                !orbitParent.TryGetMotionState(out var parentState))
            {
                error = "On-rails spawning requires an active parent body and universe frame.";
                return false;
            }

            if (orbit == null || !orbit.HasValidSettings)
            {
                error = "The orbital parameters are invalid.";
                return false;
            }

            var mu = GravitationalConstant * (orbitParent.Definition.MassKilograms + massKilograms);
            var p = orbit.semiMajorAxisMeters * (1.0 - orbit.eccentricity * orbit.eccentricity);
            if (!IsFinite(mu) || mu <= 0.0 || !IsFinite(p) || p <= 0.0)
            {
                error = "The orbit does not produce a valid gravitational parameter.";
                return false;
            }

            var anomaly = DegreesToRadians(orbit.trueAnomalyDegrees);
            var radius = p / (1.0 + orbit.eccentricity * Math.Cos(anomaly));
            var perifocalPosition = new DoubleVector3(radius * Math.Cos(anomaly), radius * Math.Sin(anomaly), 0.0);
            var speedScale = Math.Sqrt(mu / p);
            var perifocalVelocity = new DoubleVector3(
                -speedScale * Math.Sin(anomaly),
                speedScale * (orbit.eccentricity + Math.Cos(anomaly)),
                0.0);

            var relativePosition = RotatePerifocal(perifocalPosition);
            var relativeVelocity = RotatePerifocal(perifocalVelocity);
            var parentPosition = Difference(parentState.Position, universeFrame.FrameOrigin);
            position = Add(parentPosition, relativePosition);
            velocity = Add(parentState.LinearVelocityMetersPerSecond, relativeVelocity);
            error = string.Empty;
            return true;
        }

        private DoubleVector3 RotatePerifocal(DoubleVector3 value)
        {
            var node = DegreesToRadians(orbit.longitudeAscendingNodeDegrees);
            var inclination = DegreesToRadians(orbit.inclinationDegrees);
            var periapsis = DegreesToRadians(orbit.argumentOfPeriapsisDegrees);
            var cosNode = Math.Cos(node); var sinNode = Math.Sin(node);
            var cosInc = Math.Cos(inclination); var sinInc = Math.Sin(inclination);
            var cosPeri = Math.Cos(periapsis); var sinPeri = Math.Sin(periapsis);
            return new DoubleVector3(
                (cosNode * cosPeri - sinNode * sinPeri * cosInc) * value.x +
                (-cosNode * sinPeri - sinNode * cosPeri * cosInc) * value.y,
                (sinNode * cosPeri + cosNode * sinPeri * cosInc) * value.x +
                (-sinNode * sinPeri + cosNode * cosPeri * cosInc) * value.y,
                sinPeri * sinInc * value.x + cosPeri * sinInc * value.y);
        }

        private static DoubleVector3 Difference(UniversePosition first, UniversePosition second) =>
            new DoubleVector3(
                ((double)first.CellX - second.CellX) * UniversePosition.CellSizeMeters + first.LocalXMeters - second.LocalXMeters,
                ((double)first.CellY - second.CellY) * UniversePosition.CellSizeMeters + first.LocalYMeters - second.LocalYMeters,
                ((double)first.CellZ - second.CellZ) * UniversePosition.CellSizeMeters + first.LocalZMeters - second.LocalZMeters);

        private static DoubleVector3 Add(DoubleVector3 a, DoubleVector3 b) =>
            new DoubleVector3(a.x + b.x, a.y + b.y, a.z + b.z);
        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
