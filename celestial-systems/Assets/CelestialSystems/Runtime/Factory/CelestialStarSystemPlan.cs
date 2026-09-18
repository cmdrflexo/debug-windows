/*
 * Describes the body systems a generation guide wants a celestial star-system factory to spawn.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class CelestialStarSystemPlan
    {
        public sealed class ReferencePointPlan
        {
            public ReferencePointPlan(
                string instanceId,
                double massKilograms,
                DoubleVector3 positionMetersFromStarSystemOrigin,
                DoubleVector3 velocityMetersPerSecond)
            {
                InstanceId = instanceId;
                MassKilograms = massKilograms;
                PositionMetersFromStarSystemOrigin =
                    positionMetersFromStarSystemOrigin;
                VelocityMetersPerSecond =
                    velocityMetersPerSecond;
            }

            public string InstanceId { get; }
            public double MassKilograms { get; }
            public DoubleVector3 PositionMetersFromStarSystemOrigin { get; }
            public DoubleVector3 VelocityMetersPerSecond { get; }
        }

        public sealed class BodySystemPlan
        {
            public BodySystemPlan(
                string instanceId,
                CelestialBodySystemDefinition definition,
                DoubleVector3 positionMetersFromStarSystemOrigin,
                DoubleVector3 velocityMetersPerSecond,
                Quaternion rotation,
                CelestialBodySpawnMode? rootMotionModeOverride = null,
                string referenceBodySystemInstanceId = null,
                string referenceBodyInstanceId = null,
                CelestialTrajectoryDefinition trajectory = null,
                string referencePointInstanceId = null)
            {
                InstanceId = instanceId;
                Definition = definition;
                PositionMetersFromStarSystemOrigin =
                    positionMetersFromStarSystemOrigin;
                VelocityMetersPerSecond =
                    velocityMetersPerSecond;
                Rotation = rotation;
                RootMotionModeOverride = rootMotionModeOverride;
                ReferenceBodySystemInstanceId =
                    referenceBodySystemInstanceId;
                ReferenceBodyInstanceId =
                    referenceBodyInstanceId;
                Trajectory = trajectory;
                ReferencePointInstanceId =
                    referencePointInstanceId;
            }

            public string InstanceId { get; }

            public CelestialBodySystemDefinition Definition { get; }

            public DoubleVector3 PositionMetersFromStarSystemOrigin { get; }

            public DoubleVector3 VelocityMetersPerSecond { get; }

            public Quaternion Rotation { get; }

            public CelestialBodySpawnMode? RootMotionModeOverride { get; }

            public string ReferenceBodySystemInstanceId { get; }

            public string ReferenceBodyInstanceId { get; }

            public CelestialTrajectoryDefinition Trajectory { get; }

            public string ReferencePointInstanceId { get; }
        }

        private readonly BodySystemPlan[] bodySystems;

        private readonly ReferencePointPlan[] referencePoints;

        private readonly UnityEngine.Object[] ownedRuntimeObjects;

        public CelestialStarSystemPlan(
            string definitionId,
            IReadOnlyList<BodySystemPlan> bodySystems,
            IReadOnlyList<UnityEngine.Object> ownedRuntimeObjects = null,
            CelestialProtoplanetaryDiskResult? formationDisk = null,
            IReadOnlyList<ReferencePointPlan> referencePoints = null)
        {
            DefinitionId = definitionId;
            FormationDisk = formationDisk;
            this.bodySystems =
                bodySystems == null
                    ? Array.Empty<BodySystemPlan>()
                    : Copy(bodySystems);
            this.referencePoints =
                referencePoints == null
                    ? Array.Empty<ReferencePointPlan>()
                    : Copy(referencePoints);
            this.ownedRuntimeObjects =
                ownedRuntimeObjects == null
                    ? Array.Empty<UnityEngine.Object>()
                    : Copy(ownedRuntimeObjects);
        }

        public string DefinitionId { get; }

        public CelestialProtoplanetaryDiskResult? FormationDisk { get; }

        public IReadOnlyList<BodySystemPlan> BodySystems =>
            bodySystems;

        public IReadOnlyList<ReferencePointPlan> ReferencePoints =>
            referencePoints;

        internal void ReleaseOwnedRuntimeObjects()
        {
            for (var index = 0;
                index < ownedRuntimeObjects.Length;
                index++)
            {
                if (ownedRuntimeObjects[index] == null)
                {
                    continue;
                }

                UnityEngine.Object.Destroy(
                    ownedRuntimeObjects[index]);
                ownedRuntimeObjects[index] = null;
            }
        }

        private static ReferencePointPlan[] Copy(
            IReadOnlyList<ReferencePointPlan> source)
        {
            var result =
                new ReferencePointPlan[source.Count];

            for (var index = 0;
                index < source.Count;
                index++)
            {
                result[index] =
                    source[index];
            }

            return result;
        }

        private static BodySystemPlan[] Copy(
            IReadOnlyList<BodySystemPlan> source)
        {
            var result =
                new BodySystemPlan[source.Count];

            for (var index = 0;
                index < source.Count;
                index++)
            {
                result[index] =
                    source[index];
            }

            return result;
        }

        private static UnityEngine.Object[] Copy(
            IReadOnlyList<UnityEngine.Object> source)
        {
            var result =
                new UnityEngine.Object[source.Count];

            for (var index = 0;
                index < source.Count;
                index++)
            {
                result[index] =
                    source[index];
            }

            return result;
        }
    }
}
