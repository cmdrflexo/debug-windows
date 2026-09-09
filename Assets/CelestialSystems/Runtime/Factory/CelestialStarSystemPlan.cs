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
        public sealed class BodySystemPlan
        {
            public BodySystemPlan(
                string instanceId,
                CelestialBodySystemDefinition definition,
                DoubleVector3 positionMetersFromStarSystemOrigin,
                DoubleVector3 velocityMetersPerSecond,
                Quaternion rotation)
            {
                InstanceId = instanceId;
                Definition = definition;
                PositionMetersFromStarSystemOrigin =
                    positionMetersFromStarSystemOrigin;
                VelocityMetersPerSecond =
                    velocityMetersPerSecond;
                Rotation = rotation;
            }

            public string InstanceId { get; }

            public CelestialBodySystemDefinition Definition { get; }

            public DoubleVector3 PositionMetersFromStarSystemOrigin { get; }

            public DoubleVector3 VelocityMetersPerSecond { get; }

            public Quaternion Rotation { get; }
        }

        private readonly BodySystemPlan[] bodySystems;

        private readonly UnityEngine.Object[] ownedRuntimeObjects;

        public CelestialStarSystemPlan(
            string definitionId,
            IReadOnlyList<BodySystemPlan> bodySystems,
            IReadOnlyList<UnityEngine.Object> ownedRuntimeObjects = null)
        {
            DefinitionId = definitionId;
            this.bodySystems =
                bodySystems == null
                    ? Array.Empty<BodySystemPlan>()
                    : Copy(bodySystems);
            this.ownedRuntimeObjects =
                ownedRuntimeObjects == null
                    ? Array.Empty<UnityEngine.Object>()
                    : Copy(ownedRuntimeObjects);
        }

        public string DefinitionId { get; }

        public IReadOnlyList<BodySystemPlan> BodySystems =>
            bodySystems;

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
