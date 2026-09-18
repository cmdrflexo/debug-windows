/*
 * Orchestrates the celestial-body factory to generate complete, definition-driven body systems.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class CelestialBodySystemFactory
    {
        public sealed class GeneratedSystem
        {
            private readonly Dictionary<
                string,
                CelestialBodyRuntimeContext> bodies;

            internal GeneratedSystem(
                string instanceId,
                CelestialBodySystemDefinition definition,
                Transform root,
                Dictionary<string, CelestialBodyRuntimeContext> bodies)
            {
                InstanceId = instanceId;
                Definition = definition;
                Root = root;
                this.bodies = bodies;
            }

            public string InstanceId { get; }

            public CelestialBodySystemDefinition Definition { get; }

            public Transform Root { get; }

            public IReadOnlyDictionary<
                string,
                CelestialBodyRuntimeContext> Bodies =>
                    bodies;

            public bool TryGetBody(
                string entryInstanceId,
                out CelestialBodyRuntimeContext body)
            {
                return bodies.TryGetValue(
                    entryInstanceId,
                    out body) &&
                    body != null;
            }
        }

        private readonly CelestialBodyFactory bodyFactory;

        public CelestialBodySystemFactory(
            CelestialBodyFactory bodyFactory)
        {
            this.bodyFactory = bodyFactory;
        }

        public string LastError { get; private set; }

        public bool TryGenerate(
            string systemInstanceId,
            CelestialBodySystemDefinition definition,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion rotation,
            Transform parent,
            out GeneratedSystem generatedSystem)
        {
            return TryGenerate(
                systemInstanceId,
                definition,
                positionMetersFromFrameOrigin,
                velocityMetersPerSecond,
                rotation,
                parent,
                null,
                null,
                null,
                out generatedSystem);
        }

        public bool TryGenerate(
            string systemInstanceId,
            CelestialBodySystemDefinition definition,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion rotation,
            Transform parent,
            CelestialBodySpawnMode? rootMotionModeOverride,
            ICelestialMotionStateSource externalRootOrbitCenter,
            CelestialTrajectoryDefinition externalRootTrajectory,
            out GeneratedSystem generatedSystem)
        {
            generatedSystem = null;
            LastError = string.Empty;

            if (bodyFactory == null)
            {
                return SetError(
                    "A celestial body system factory requires a celestial body factory.");
            }

            if (string.IsNullOrWhiteSpace(
                    systemInstanceId))
            {
                return SetError(
                    "A generated celestial body system requires an instance ID.");
            }

            if (definition == null)
            {
                return SetError(
                    "A celestial body system factory requires a system definition.");
            }

            if (!definition.TryValidate(
                    out var validationError))
            {
                return SetError(
                    validationError);
            }

            if (!IsFinite(
                    positionMetersFromFrameOrigin) ||
                !IsFinite(
                    velocityMetersPerSecond) ||
                !IsFinite(
                    rotation))
            {
                return SetError(
                    "A celestial body system generation request contains non-finite motion values.");
            }

            var rootObject =
                new GameObject(
                    $"{definition.DefinitionId} ({systemInstanceId})");
            var root = rootObject.transform;
            root.SetParent(
                parent,
                false);

            var generatedBodies =
                new Dictionary<
                    string,
                    CelestialBodyRuntimeContext>(
                        StringComparer.Ordinal);
            var pendingBodies =
                new List<CelestialBodySystemDefinition.BodyEntry>(
                    definition.Bodies);

            while (pendingBodies.Count > 0)
            {
                var generatedThisPass = false;

                for (var index =
                    pendingBodies.Count - 1;
                    index >= 0;
                    index--)
                {
                    var entry =
                        pendingBodies[index];
                    CelestialBodyRuntimeContext orbitCenter = null;

                    if (!string.IsNullOrWhiteSpace(
                            entry.ParentInstanceId) &&
                        !generatedBodies.TryGetValue(
                            entry.ParentInstanceId,
                            out orbitCenter))
                    {
                        continue;
                    }

                    var isRootEntry =
                        string.IsNullOrWhiteSpace(
                            entry.ParentInstanceId);
                    var effectiveMotionMode =
                        isRootEntry &&
                        rootMotionModeOverride.HasValue
                            ? rootMotionModeOverride.Value
                            : entry.MotionMode;
                    var effectiveOrbitCenter =
                        isRootEntry
                            ? externalRootOrbitCenter
                            : orbitCenter;
                    var effectiveTrajectory =
                        isRootEntry &&
                        externalRootTrajectory != null
                            ? externalRootTrajectory
                            : entry.Trajectory;
                    var bodyInstanceId =
                        $"{systemInstanceId}/{entry.InstanceId}";
                    var request =
                        new CelestialBodySpawnRequest(
                            bodyInstanceId,
                            entry.Definition,
                            positionMetersFromFrameOrigin +
                                Rotate(
                                    rotation,
                                    entry.PositionMetersFromSystemOrigin),
                            velocityMetersPerSecond +
                                Rotate(
                                    rotation,
                                    entry.VelocityMetersPerSecond),
                            rotation *
                                entry.Rotation,
                            entry.QualityProfile,
                            root,
                            effectiveMotionMode,
                            effectiveOrbitCenter,
                            Rotate(
                                rotation,
                                entry.AngularVelocityRadiansPerSecond),
                            effectiveTrajectory);

                    if (!bodyFactory.TrySpawnBody(
                            request,
                            out var generatedBody))
                    {
                        var bodyError =
                            bodyFactory.LastError;
                        RollBack(
                            generatedBodies,
                            rootObject);
                        return SetError(
                            $"Failed to generate body-system entry '{entry.InstanceId}': {bodyError}");
                    }

                    generatedBodies.Add(
                        entry.InstanceId,
                        generatedBody);
                    pendingBodies.RemoveAt(
                        index);
                    generatedThisPass = true;
                }

                if (generatedThisPass)
                {
                    continue;
                }

                RollBack(
                    generatedBodies,
                    rootObject);
                return SetError(
                    "The celestial body system could not resolve its parent generation order.");
            }

            generatedSystem =
                new GeneratedSystem(
                    systemInstanceId,
                    definition,
                    root,
                    generatedBodies);
            return true;
        }

        public bool TryDespawn(
            GeneratedSystem generatedSystem)
        {
            LastError = string.Empty;

            if (bodyFactory == null)
            {
                return SetError(
                    "A celestial body system factory requires a celestial body factory.");
            }

            if (generatedSystem == null)
            {
                return SetError(
                    "A celestial body system is required for despawning.");
            }

            var allBodiesDespawned = true;

            foreach (var body in
                generatedSystem.Bodies.Values)
            {
                if (body != null &&
                    !bodyFactory.TryDespawnBody(
                        body.InstanceId))
                {
                    allBodiesDespawned = false;
                }
            }

            if (generatedSystem.Root != null)
            {
                UnityEngine.Object.Destroy(
                    generatedSystem.Root.gameObject);
            }

            if (!allBodiesDespawned)
            {
                return SetError(
                    $"One or more bodies in generated system '{generatedSystem.InstanceId}' could not be despawned.");
            }

            return true;
        }

        private void RollBack(
            Dictionary<string, CelestialBodyRuntimeContext> generatedBodies,
            GameObject rootObject)
        {
            foreach (var body in
                generatedBodies.Values)
            {
                if (body != null)
                {
                    bodyFactory.TryDespawnBody(
                        body.InstanceId);
                }
            }

            if (rootObject != null)
            {
                UnityEngine.Object.Destroy(
                    rootObject);
            }
        }

        private bool SetError(
            string error)
        {
            LastError = error;
            Debug.LogError(
                error);
            return false;
        }

        private static DoubleVector3 Rotate(
            Quaternion rotation,
            DoubleVector3 vector)
        {
            var axis =
                new DoubleVector3(
                    rotation.x,
                    rotation.y,
                    rotation.z);
            var scalar =
                (double)rotation.w;
            var axisDotVector =
                axis.x * vector.x +
                axis.y * vector.y +
                axis.z * vector.z;
            var axisSquared =
                axis.x * axis.x +
                axis.y * axis.y +
                axis.z * axis.z;
            var axisCrossVector =
                new DoubleVector3(
                    axis.y * vector.z -
                        axis.z * vector.y,
                    axis.z * vector.x -
                        axis.x * vector.z,
                    axis.x * vector.y -
                        axis.y * vector.x);

            return
                axis *
                    (2.0 * axisDotVector) +
                vector *
                    (scalar * scalar - axisSquared) +
                axisCrossVector *
                    (2.0 * scalar);
        }

        private static bool IsFinite(
            DoubleVector3 value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z);
        }

        private static bool IsFinite(
            Quaternion value)
        {
            return
                IsFinite(value.x) &&
                IsFinite(value.y) &&
                IsFinite(value.z) &&
                IsFinite(value.w);
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
