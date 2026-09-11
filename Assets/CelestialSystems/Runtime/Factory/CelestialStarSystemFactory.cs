/*
 * Converts guide-generated star-system plans into complete collections of spawned celestial body systems.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class CelestialStarSystemFactory
    {
        public sealed class GeneratedSystem
        {
            private readonly Dictionary<
                string,
                CelestialBodySystemFactory.GeneratedSystem> bodySystems;

            private readonly Dictionary<
                string,
                CelestialMotionReferencePoint> referencePoints;

            internal GeneratedSystem(
                string instanceId,
                int seed,
                CelestialSystemGenerationGuide guide,
                CelestialGalacticEnvironmentDefinition environment,
                CelestialStarSystemPlan plan,
                Transform root,
                Dictionary<string, CelestialBodySystemFactory.GeneratedSystem> bodySystems,
                Dictionary<string, CelestialMotionReferencePoint> referencePoints)
            {
                InstanceId = instanceId;
                Seed = seed;
                Guide = guide;
                Environment = environment;
                Plan = plan;
                Root = root;
                this.bodySystems = bodySystems;
                this.referencePoints = referencePoints;
            }

            public string InstanceId { get; }

            public int Seed { get; }

            public CelestialSystemGenerationGuide Guide { get; }

            public CelestialGalacticEnvironmentDefinition Environment { get; }

            public CelestialStarSystemPlan Plan { get; }

            public Transform Root { get; }

            public IReadOnlyDictionary<
                string,
                CelestialBodySystemFactory.GeneratedSystem> BodySystems =>
                    bodySystems;

            public IReadOnlyDictionary<
                string,
                CelestialMotionReferencePoint> ReferencePoints =>
                    referencePoints;
        }

        private readonly CelestialBodyFactory bodyFactory;

        private readonly CelestialBodySystemFactory bodySystemFactory;

        public CelestialStarSystemFactory(
            CelestialBodyFactory bodyFactory)
        {
            this.bodyFactory =
                bodyFactory;
            bodySystemFactory =
                new CelestialBodySystemFactory(
                    bodyFactory);
        }

        public string LastError { get; private set; }

        public bool TryGenerate(
            string starSystemInstanceId,
            int seed,
            CelestialSystemGenerationGuide guide,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion rotation,
            Transform parent,
            out GeneratedSystem generatedSystem)
        {
            return TryGenerate(
                starSystemInstanceId,
                seed,
                guide,
                null,
                positionMetersFromFrameOrigin,
                velocityMetersPerSecond,
                rotation,
                parent,
                out generatedSystem);
        }

        public bool TryGenerate(
            string starSystemInstanceId,
            int seed,
            CelestialSystemGenerationGuide guide,
            CelestialGalacticEnvironmentDefinition environment,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion rotation,
            Transform parent,
            out GeneratedSystem generatedSystem)
        {
            generatedSystem = null;
            LastError = string.Empty;

            if (string.IsNullOrWhiteSpace(
                    starSystemInstanceId))
            {
                return SetError(
                    "A generated celestial star system requires an instance ID.");
            }

            if (guide == null)
            {
                return SetError(
                    "A celestial star system factory requires a generation guide.");
            }

            if (environment != null &&
                !environment.TryValidate(
                    out var environmentError))
            {
                return SetError(
                    environmentError);
            }

            if (!IsFinite(
                    positionMetersFromFrameOrigin) ||
                !IsFinite(
                    velocityMetersPerSecond) ||
                !IsFinite(
                    rotation))
            {
                return SetError(
                    "A celestial star system generation request contains non-finite motion values.");
            }

            CelestialStarSystemPlan plan;

            try
            {
                if (!guide.TryGenerate(
                        new CelestialStarSystemGenerationRequest(
                            seed,
                            environment),
                        out plan,
                        out var guideError))
                {
                    return SetError(
                        string.IsNullOrWhiteSpace(
                            guideError)
                            ? "The celestial system generation guide failed without an error message."
                            : guideError);
                }
            }
            catch (Exception exception)
            {
                return SetError(
                    $"The celestial system generation guide threw an exception: {exception.Message}");
            }

            if (!TryValidatePlan(
                    plan,
                    out var validationError))
            {
                plan?.ReleaseOwnedRuntimeObjects();
                return SetError(
                    validationError);
            }

            var rootObject =
                new GameObject(
                    $"{plan.DefinitionId} ({starSystemInstanceId})");
            var root =
                rootObject.transform;
            root.SetParent(
                parent,
                false);

            var generatedReferencePoints =
                new Dictionary<
                    string,
                    CelestialMotionReferencePoint>(
                        StringComparer.Ordinal);

            foreach (var referencePointPlan in
                plan.ReferencePoints)
            {
                var referenceObject =
                    new GameObject(
                        referencePointPlan.InstanceId);
                referenceObject.transform.SetParent(
                    root,
                    false);
                var provider =
                    referenceObject.AddComponent<
                        TrajectoryCelestialBodyMotionProvider>();
                var referencePoint =
                    referenceObject.AddComponent<
                        CelestialMotionReferencePoint>();
                var referenceInstanceId =
                    $"{starSystemInstanceId}/{referencePointPlan.InstanceId}";

                if (!referencePoint.Initialize(
                        referenceInstanceId,
                        referencePointPlan.MassKilograms,
                        provider) ||
                    !provider.InitializeInertial(
                        bodyFactory.UniverseFrame,
                        bodyFactory.CelestialTime,
                        referenceObject.transform,
                        positionMetersFromFrameOrigin +
                            Rotate(
                                rotation,
                                referencePointPlan.PositionMetersFromStarSystemOrigin),
                        velocityMetersPerSecond +
                            Rotate(
                                rotation,
                                referencePointPlan.VelocityMetersPerSecond),
                        rotation,
                        new DoubleVector3()))
                {
                    UnityEngine.Object.Destroy(
                        rootObject);
                    plan.ReleaseOwnedRuntimeObjects();
                    return SetError(
                        $"Failed to create motion reference point '{referencePointPlan.InstanceId}'.");
                }

                generatedReferencePoints.Add(
                    referencePointPlan.InstanceId,
                    referencePoint);
            }

            var generatedBodySystems =
                new Dictionary<
                    string,
                    CelestialBodySystemFactory.GeneratedSystem>(
                        StringComparer.Ordinal);

            var pendingBodySystems =
                new List<CelestialStarSystemPlan.BodySystemPlan>(
                    plan.BodySystems);

            while (pendingBodySystems.Count > 0)
            {
                var generatedThisPass = false;

                for (var index =
                    pendingBodySystems.Count - 1;
                    index >= 0;
                    index--)
                {
                    var bodySystemPlan =
                        pendingBodySystems[index];
                    ICelestialMotionStateSource referenceSource = null;

                    if (!string.IsNullOrWhiteSpace(
                            bodySystemPlan.ReferencePointInstanceId))
                    {
                        if (!generatedReferencePoints.TryGetValue(
                                bodySystemPlan.ReferencePointInstanceId,
                                out var referencePoint))
                        {
                            RollBack(
                                generatedBodySystems,
                                rootObject,
                                plan);
                            return SetError(
                                $"Star-system entry '{bodySystemPlan.InstanceId}' could not resolve reference point '{bodySystemPlan.ReferencePointInstanceId}'.");
                        }

                        referenceSource =
                            referencePoint;
                    }
                    else if (!string.IsNullOrWhiteSpace(
                            bodySystemPlan.ReferenceBodySystemInstanceId))
                    {
                        if (!generatedBodySystems.TryGetValue(
                                bodySystemPlan.ReferenceBodySystemInstanceId,
                                out var referenceBodySystem))
                        {
                            continue;
                        }

                        if (!referenceBodySystem.TryGetBody(
                                bodySystemPlan.ReferenceBodyInstanceId,
                                out var referenceBody))
                        {
                            RollBack(
                                generatedBodySystems,
                                rootObject,
                                plan);
                            return SetError(
                                $"Star-system entry '{bodySystemPlan.InstanceId}' could not resolve reference body '{bodySystemPlan.ReferenceBodySystemInstanceId}/{bodySystemPlan.ReferenceBodyInstanceId}'.");
                        }

                        referenceSource =
                            referenceBody;
                    }

                    var bodySystemInstanceId =
                        $"{starSystemInstanceId}/{bodySystemPlan.InstanceId}";

                    if (!bodySystemFactory.TryGenerate(
                            bodySystemInstanceId,
                            bodySystemPlan.Definition,
                            positionMetersFromFrameOrigin +
                                Rotate(
                                    rotation,
                                    bodySystemPlan.PositionMetersFromStarSystemOrigin),
                            velocityMetersPerSecond +
                                Rotate(
                                    rotation,
                                    bodySystemPlan.VelocityMetersPerSecond),
                            rotation *
                                bodySystemPlan.Rotation,
                            root,
                            bodySystemPlan.RootMotionModeOverride,
                            referenceSource,
                            bodySystemPlan.Trajectory,
                            out var generatedBodySystem))
                    {
                        RollBack(
                            generatedBodySystems,
                            rootObject,
                            plan);
                        return SetError(
                            $"Failed to generate star-system entry '{bodySystemPlan.InstanceId}': {bodySystemFactory.LastError}");
                    }

                    generatedBodySystems.Add(
                        bodySystemPlan.InstanceId,
                        generatedBodySystem);
                    pendingBodySystems.RemoveAt(
                        index);
                    generatedThisPass = true;
                }

                if (generatedThisPass)
                {
                    continue;
                }

                RollBack(
                    generatedBodySystems,
                    rootObject,
                    plan);
                return SetError(
                    "The celestial star system could not resolve its body-system reference generation order.");
            }

            generatedSystem =
                new GeneratedSystem(
                    starSystemInstanceId,
                    seed,
                    guide,
                    environment,
                    plan,
                    root,
                    generatedBodySystems,
                    generatedReferencePoints);
            return true;
        }

        public bool TryDespawn(
            GeneratedSystem generatedSystem)
        {
            LastError = string.Empty;

            if (generatedSystem == null)
            {
                return SetError(
                    "A generated celestial star system is required for despawning.");
            }

            var allBodySystemsDespawned =
                true;

            foreach (var bodySystem in
                generatedSystem.BodySystems.Values)
            {
                if (bodySystem != null &&
                    !bodySystemFactory.TryDespawn(
                        bodySystem))
                {
                    allBodySystemsDespawned =
                        false;
                }
            }

            if (generatedSystem.Root != null)
            {
                UnityEngine.Object.Destroy(
                    generatedSystem.Root.gameObject);
            }

            generatedSystem.Plan.ReleaseOwnedRuntimeObjects();

            if (!allBodySystemsDespawned)
            {
                return SetError(
                    $"One or more body systems in generated star system '{generatedSystem.InstanceId}' could not be despawned.");
            }

            return true;
        }

        private static bool TryValidatePlan(
            CelestialStarSystemPlan plan,
            out string error)
        {
            if (plan == null)
            {
                error =
                    "The celestial system generation guide returned no plan.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    plan.DefinitionId))
            {
                error =
                    "A generated celestial star-system plan requires a definition ID.";
                return false;
            }

            if (plan.BodySystems == null ||
                plan.BodySystems.Count == 0)
            {
                error =
                    "A generated celestial star-system plan requires at least one body system.";
                return false;
            }

            var referencePointIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (var referencePoint in
                plan.ReferencePoints)
            {
                if (referencePoint == null ||
                    string.IsNullOrWhiteSpace(
                        referencePoint.InstanceId) ||
                    !referencePointIds.Add(
                        referencePoint.InstanceId) ||
                    !IsFinitePositive(
                        referencePoint.MassKilograms) ||
                    !IsFinite(
                        referencePoint.PositionMetersFromStarSystemOrigin) ||
                    !IsFinite(
                        referencePoint.VelocityMetersPerSecond))
                {
                    error =
                        "A generated star-system plan contains an invalid or duplicate motion reference point.";
                    return false;
                }
            }

            var instanceIds =
                new HashSet<string>(
                    StringComparer.Ordinal);

            foreach (var bodySystem in
                plan.BodySystems)
            {
                if (bodySystem == null)
                {
                    error =
                        "A generated celestial star-system plan contains a missing body-system entry.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        bodySystem.InstanceId))
                {
                    error =
                        "Every generated star-system body-system entry requires an instance ID.";
                    return false;
                }

                if (!instanceIds.Add(
                        bodySystem.InstanceId))
                {
                    error =
                        $"The star-system body-system instance ID '{bodySystem.InstanceId}' is used more than once.";
                    return false;
                }

                if (bodySystem.Definition == null)
                {
                    error =
                        $"The star-system entry '{bodySystem.InstanceId}' requires a body-system definition.";
                    return false;
                }

                if (bodySystem.Trajectory != null &&
                    !bodySystem.Trajectory.TryValidate(
                        out var trajectoryError))
                {
                    error =
                        $"The star-system entry '{bodySystem.InstanceId}' has an invalid trajectory: {trajectoryError}";
                    return false;
                }

                if (bodySystem.Trajectory != null)
                {
                    var hasReferencePoint =
                        !string.IsNullOrWhiteSpace(
                            bodySystem.ReferencePointInstanceId);
                    var hasCompleteBodyReference =
                        !string.IsNullOrWhiteSpace(
                            bodySystem.ReferenceBodySystemInstanceId) &&
                        !string.IsNullOrWhiteSpace(
                            bodySystem.ReferenceBodyInstanceId);

                    if (hasReferencePoint ==
                        hasCompleteBodyReference)
                    {
                        error =
                            $"The star-system entry '{bodySystem.InstanceId}' trajectory requires exactly one body or motion-reference-point source.";
                        return false;
                    }

                    if (hasReferencePoint &&
                        !referencePointIds.Contains(
                            bodySystem.ReferencePointInstanceId))
                    {
                        error =
                            $"The star-system entry '{bodySystem.InstanceId}' references missing motion reference point '{bodySystem.ReferencePointInstanceId}'.";
                        return false;
                    }
                }

                if (!IsFinite(
                        bodySystem.PositionMetersFromStarSystemOrigin) ||
                    !IsFinite(
                        bodySystem.VelocityMetersPerSecond) ||
                    !IsFinite(
                        bodySystem.Rotation))
                {
                    error =
                        $"The star-system entry '{bodySystem.InstanceId}' contains non-finite motion values.";
                    return false;
                }
            }

            foreach (var bodySystem in
                plan.BodySystems)
            {
                if (!string.IsNullOrWhiteSpace(
                        bodySystem.ReferencePointInstanceId) ||
                    string.IsNullOrWhiteSpace(
                        bodySystem.ReferenceBodySystemInstanceId))
                {
                    continue;
                }

                if (string.Equals(
                        bodySystem.InstanceId,
                        bodySystem.ReferenceBodySystemInstanceId,
                        StringComparison.Ordinal))
                {
                    error =
                        $"The star-system entry '{bodySystem.InstanceId}' cannot reference its own body system.";
                    return false;
                }

                if (!instanceIds.Contains(
                        bodySystem.ReferenceBodySystemInstanceId))
                {
                    error =
                        $"The star-system entry '{bodySystem.InstanceId}' references missing body system '{bodySystem.ReferenceBodySystemInstanceId}'.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private void RollBack(
            Dictionary<string, CelestialBodySystemFactory.GeneratedSystem> generatedBodySystems,
            GameObject rootObject,
            CelestialStarSystemPlan plan)
        {
            foreach (var bodySystem in
                generatedBodySystems.Values)
            {
                if (bodySystem != null)
                {
                    bodySystemFactory.TryDespawn(
                        bodySystem);
                }
            }

            if (rootObject != null)
            {
                UnityEngine.Object.Destroy(
                    rootObject);
            }

            plan?.ReleaseOwnedRuntimeObjects();
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

        private static bool IsFinitePositive(
            double value)
        {
            return
                IsFinite(value) &&
                value > 0.0;
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
