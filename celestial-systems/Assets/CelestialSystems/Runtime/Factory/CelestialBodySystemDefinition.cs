/*
 * Stores reusable body membership, relative motion, and parent relationships for a celestial body system.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Celestial Body System",
        menuName = "Celestial Systems/Celestial Body System")]
    public sealed class CelestialBodySystemDefinition :
        ScriptableObject
    {
        [Serializable]
        public sealed class BodyEntry
        {
            [SerializeField]
            private string instanceId =
                "body";

            [SerializeField]
            private CelestialBodyDefinition definition;

            [SerializeField]
            private RoundMapMagicSurfaceQualityProfile qualityProfile;

            [SerializeField]
            [Tooltip("Optional instance ID of another entry in this system. Leave empty for a system root body.")]
            private string parentInstanceId;

            [SerializeField]
            private CelestialBodySpawnMode motionMode =
                CelestialBodySpawnMode.FreeSimulation;

            [SerializeField]
            [Tooltip("Position relative to the body-system origin, expressed in system axes.")]
            private DoubleVector3 positionMetersFromSystemOrigin;

            [SerializeField]
            [Tooltip("Velocity relative to the body-system velocity, expressed in system axes.")]
            private DoubleVector3 velocityMetersPerSecond;

            [SerializeField]
            [Tooltip("Optional prescribed trajectory relative to the parent entry.")]
            private CelestialTrajectoryDefinition trajectory;

            [SerializeField]
            private Vector3 rotationEulerDegrees;

            [SerializeField]
            private DoubleVector3 angularVelocityRadiansPerSecond;

            public BodyEntry(
                string instanceId,
                CelestialBodyDefinition definition,
                RoundMapMagicSurfaceQualityProfile qualityProfile,
                string parentInstanceId,
                CelestialBodySpawnMode motionMode,
                DoubleVector3 positionMetersFromSystemOrigin,
                DoubleVector3 velocityMetersPerSecond,
                Vector3 rotationEulerDegrees,
                DoubleVector3 angularVelocityRadiansPerSecond,
                CelestialTrajectoryDefinition trajectory = null)
            {
                this.instanceId = instanceId;
                this.definition = definition;
                this.qualityProfile = qualityProfile;
                this.parentInstanceId = parentInstanceId;
                this.motionMode = motionMode;
                this.positionMetersFromSystemOrigin =
                    positionMetersFromSystemOrigin;
                this.velocityMetersPerSecond =
                    velocityMetersPerSecond;
                this.trajectory =
                    trajectory;
                this.rotationEulerDegrees =
                    rotationEulerDegrees;
                this.angularVelocityRadiansPerSecond =
                    angularVelocityRadiansPerSecond;
            }

            public string InstanceId =>
                instanceId;

            public CelestialBodyDefinition Definition =>
                definition;

            public RoundMapMagicSurfaceQualityProfile QualityProfile =>
                qualityProfile;

            public string ParentInstanceId =>
                parentInstanceId;

            public CelestialBodySpawnMode MotionMode =>
                motionMode;

            public DoubleVector3 PositionMetersFromSystemOrigin =>
                positionMetersFromSystemOrigin;

            public DoubleVector3 VelocityMetersPerSecond =>
                velocityMetersPerSecond;

            public CelestialTrajectoryDefinition Trajectory =>
                trajectory;

            public Quaternion Rotation =>
                Quaternion.Euler(
                    rotationEulerDegrees);

            public DoubleVector3 AngularVelocityRadiansPerSecond =>
                angularVelocityRadiansPerSecond;
        }

        [SerializeField]
        private string definitionId =
            "body-system";

        [SerializeField]
        private BodyEntry[] bodies =
            Array.Empty<BodyEntry>();

        public string DefinitionId =>
            definitionId;

        public IReadOnlyList<BodyEntry> Bodies =>
            bodies;

        public static CelestialBodySystemDefinition CreateRuntime(
            string definitionId,
            IReadOnlyList<BodyEntry> bodies)
        {
            var runtimeDefinition =
                CreateInstance<CelestialBodySystemDefinition>();
            runtimeDefinition.name =
                definitionId;
            runtimeDefinition.hideFlags =
                HideFlags.DontSave;
            runtimeDefinition.definitionId =
                definitionId;

            if (bodies == null)
            {
                runtimeDefinition.bodies =
                    Array.Empty<BodyEntry>();
                return runtimeDefinition;
            }

            runtimeDefinition.bodies =
                new BodyEntry[bodies.Count];

            for (var index = 0;
                index < bodies.Count;
                index++)
            {
                runtimeDefinition.bodies[index] =
                    bodies[index];
            }

            return runtimeDefinition;
        }

        public bool HasValidSettings =>
            TryValidate(
                out _);

        public bool TryValidate(
            out string error)
        {
            if (string.IsNullOrWhiteSpace(
                    definitionId))
            {
                error =
                    "A celestial body system definition requires a definition ID.";
                return false;
            }

            if (bodies == null ||
                bodies.Length == 0)
            {
                error =
                    "A celestial body system definition requires at least one body.";
                return false;
            }

            var entriesById =
                new Dictionary<string, BodyEntry>(
                    StringComparer.Ordinal);

            foreach (var body in bodies)
            {
                if (body == null)
                {
                    error =
                        "A celestial body system definition contains a missing body entry.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        body.InstanceId))
                {
                    error =
                        "Every body-system entry requires an instance ID.";
                    return false;
                }

                if (!entriesById.TryAdd(
                        body.InstanceId,
                        body))
                {
                    error =
                        $"The body-system instance ID '{body.InstanceId}' is used more than once.";
                    return false;
                }

                if (body.Definition == null)
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' requires a body definition.";
                    return false;
                }

                if (!body.Definition.HasValidPhysicalSettings)
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' has invalid physical settings.";
                    return false;
                }

                if (body.Trajectory != null &&
                    !body.Trajectory.TryValidate(
                        out var trajectoryError))
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' has an invalid trajectory: {trajectoryError}";
                    return false;
                }

                if (!IsFinite(
                        body.PositionMetersFromSystemOrigin) ||
                    !IsFinite(
                        body.VelocityMetersPerSecond) ||
                    !IsFinite(
                        body.AngularVelocityRadiansPerSecond))
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' contains non-finite motion values.";
                    return false;
                }
            }

            foreach (var body in bodies)
            {
                if (string.IsNullOrWhiteSpace(
                        body.ParentInstanceId))
                {
                    continue;
                }

                if (string.Equals(
                        body.InstanceId,
                        body.ParentInstanceId,
                        StringComparison.Ordinal))
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' cannot parent itself.";
                    return false;
                }

                if (!entriesById.ContainsKey(
                        body.ParentInstanceId))
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' references missing parent '{body.ParentInstanceId}'.";
                    return false;
                }

                if (body.Trajectory != null &&
                    !string.Equals(
                        body.Trajectory.ReferenceInstanceId,
                        body.ParentInstanceId,
                        StringComparison.Ordinal))
                {
                    error =
                        $"The body-system entry '{body.InstanceId}' trajectory reference must match parent '{body.ParentInstanceId}'.";
                    return false;
                }

                var visitedIds =
                    new HashSet<string>(
                        StringComparer.Ordinal);
                var current = body;

                while (!string.IsNullOrWhiteSpace(
                    current.ParentInstanceId))
                {
                    if (!visitedIds.Add(
                            current.InstanceId))
                    {
                        error =
                            $"The body-system parent relationships contain a cycle involving '{body.InstanceId}'.";
                        return false;
                    }

                    current =
                        entriesById[current.ParentInstanceId];
                }
            }

            error = string.Empty;
            return true;
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
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
