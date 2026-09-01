/*
 * Creates definition-driven celestial-body instances and registers their NBody components with Gravity Engine at runtime.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class CelestialBodyFactory :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private CelestialBodyRuntimeContext bodyPrefab;

        [SerializeField]
        private Transform spawnedBodyParent;

        [Header("Optional Startup Body")]
        [SerializeField]
        private bool spawnOnStart;

        [SerializeField]
        private CelestialBodyDefinition startupDefinition;

        [SerializeField]
        private string startupInstanceId =
            "spawned-body-01";

        [SerializeField]
        private DoubleVector3 startupPositionMetersFromFrameOrigin;

        [SerializeField]
        private DoubleVector3 startupVelocityMetersPerSecond;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForGravityEngine;

        [SerializeField]
        private bool lastSpawnSucceeded;

        [SerializeField]
        private int activeBodyCount;

        [SerializeField]
        private CelestialBodyRuntimeContext lastSpawnedBody;

        private readonly Dictionary<
            string,
            CelestialBodyRuntimeContext> spawnedBodies =
                new Dictionary<
                    string,
                    CelestialBodyRuntimeContext>(
                        StringComparer.Ordinal);

        private GravityEngine gravityEngine;

        public UniverseFrameController UniverseFrame =>
            universeFrame;

        public CelestialBodyRuntimeContext BodyPrefab =>
            bodyPrefab;

        public int ActiveBodyCount =>
            activeBodyCount;

        public CelestialBodyRuntimeContext LastSpawnedBody =>
            lastSpawnedBody;

        public event Action<CelestialBodyRuntimeContext> BodySpawned;

        private void Start()
        {
            if (!spawnOnStart)
            {
                return;
            }

            gravityEngine =
                GravityEngine.Instance();

            if (gravityEngine == null)
            {
                Debug.LogError(
                    "Cannot spawn the configured startup body because no Gravity Engine exists in the scene.",
                    this);
                return;
            }

            waitingForGravityEngine = true;
            gravityEngine.AddGEStartCallback(
                SpawnConfiguredStartupBody);
        }

        public bool TrySpawnBody(
            string newInstanceId,
            CelestialBodyDefinition newDefinition,
            DoubleVector3 initialPositionMetersFromFrameOrigin,
            DoubleVector3 initialVelocityMetersPerSecond,
            out CelestialBodyRuntimeContext spawnedBody)
        {
            spawnedBody = null;
            lastSpawnSucceeded = false;

            if (!Application.isPlaying)
            {
                Debug.LogError(
                    "Celestial bodies can only be spawned while the application is playing.",
                    this);
                return false;
            }

            if (!TryValidateSpawn(
                    newInstanceId,
                    newDefinition,
                    initialPositionMetersFromFrameOrigin,
                    initialVelocityMetersPerSecond,
                    out var positionMetersPerPhysicsUnit,
                    out var velocityMetersPerSecondPerPhysicsUnit))
            {
                return false;
            }

            var instance =
                Instantiate(
                    bodyPrefab,
                    spawnedBodyParent);

            if (instance == null)
            {
                Debug.LogError(
                    "The celestial body factory could not instantiate its body prefab.",
                    this);
                return false;
            }

            var gravityBody =
                instance.GravityBody;

            if (gravityBody == null)
            {
                gravityBody =
                    instance.GetComponent<NBody>();
            }

            if (gravityBody == null)
            {
                Debug.LogError(
                    "The celestial body prefab requires an NBody assigned to its runtime context or attached to the same GameObject.",
                    instance);
                Destroy(
                    instance.gameObject);
                return false;
            }

            var massKilograms =
                newDefinition.MassKilograms;

            if (massKilograms >
                float.MaxValue)
            {
                Debug.LogError(
                    "The celestial body definition's mass exceeds Gravity Engine's NBody mass range.",
                    newDefinition);
                Destroy(
                    instance.gameObject);
                return false;
            }

            gravityBody.mass =
                (float)massKilograms;

            gravityBody.SetPosVel3d(
                new Vector3d(
                    initialPositionMetersFromFrameOrigin.x /
                        positionMetersPerPhysicsUnit,
                    initialPositionMetersFromFrameOrigin.y /
                        positionMetersPerPhysicsUnit,
                    initialPositionMetersFromFrameOrigin.z /
                        positionMetersPerPhysicsUnit),
                new Vector3d(
                    initialVelocityMetersPerSecond.x /
                        velocityMetersPerSecondPerPhysicsUnit,
                    initialVelocityMetersPerSecond.y /
                        velocityMetersPerSecondPerPhysicsUnit,
                    initialVelocityMetersPerSecond.z /
                        velocityMetersPerSecondPerPhysicsUnit));

            instance.name =
                $"{newDefinition.DefinitionId} ({newInstanceId})";

            instance.Initialize(
                newInstanceId,
                newDefinition,
                universeFrame,
                gravityBody,
                instance.VisualRoot);

            gravityEngine.AddBody(
                instance.gameObject);

            spawnedBodies.Add(
                newInstanceId,
                instance);
            activeBodyCount =
                spawnedBodies.Count;
            lastSpawnedBody =
                instance;
            lastSpawnSucceeded = true;
            spawnedBody =
                instance;

            BodySpawned?.Invoke(
                instance);
            return true;
        }

        private void SpawnConfiguredStartupBody()
        {
            waitingForGravityEngine = false;

            if (this == null ||
                !isActiveAndEnabled)
            {
                return;
            }

            lastSpawnSucceeded =
                TrySpawnBody(
                    startupInstanceId,
                    startupDefinition,
                    startupPositionMetersFromFrameOrigin,
                    startupVelocityMetersPerSecond,
                    out _);
        }

        private bool TryValidateSpawn(
            string newInstanceId,
            CelestialBodyDefinition newDefinition,
            DoubleVector3 initialPositionMetersFromFrameOrigin,
            DoubleVector3 initialVelocityMetersPerSecond,
            out double positionMetersPerPhysicsUnit,
            out double velocityMetersPerSecondPerPhysicsUnit)
        {
            positionMetersPerPhysicsUnit = default;
            velocityMetersPerSecondPerPhysicsUnit = default;

            if (universeFrame == null)
            {
                Debug.LogError(
                    "The celestial body factory requires a universe frame controller.",
                    this);
                return false;
            }

            if (bodyPrefab == null)
            {
                Debug.LogError(
                    "The celestial body factory requires a body prefab.",
                    this);
                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    newInstanceId))
            {
                Debug.LogError(
                    "A spawned celestial body requires a unique instance ID.",
                    this);
                return false;
            }

            if (HasExistingInstanceId(
                    newInstanceId))
            {
                Debug.LogError(
                    $"A celestial body with instance ID '{newInstanceId}' already exists.",
                    this);
                return false;
            }

            if (newDefinition == null)
            {
                Debug.LogError(
                    "A spawned celestial body requires a body definition.",
                    this);
                return false;
            }

            if (!newDefinition.HasValidPhysicalSettings)
            {
                Debug.LogError(
                    "The body definition has invalid physical settings.",
                    newDefinition);
                return false;
            }

            if (!newDefinition.HasValidResolvedSurfaceSettings)
            {
                Debug.LogError(
                    "The body definition does not have valid settings for its resolved surface system.",
                    newDefinition);
                return false;
            }

            if (!IsFinite(
                    initialPositionMetersFromFrameOrigin) ||
                !IsFinite(
                    initialVelocityMetersPerSecond))
            {
                Debug.LogError(
                    "The spawned body's starting position and velocity must contain finite values.",
                    this);
                return false;
            }

            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine == null)
            {
                Debug.LogError(
                    "Cannot spawn a celestial body because no Gravity Engine exists in the scene.",
                    this);
                return false;
            }

            if (!gravityEngine.IsSetup())
            {
                Debug.LogError(
                    "Cannot spawn a celestial body before Gravity Engine has completed setup.",
                    this);
                return false;
            }

            if (gravityEngine.units !=
                GravityScaler.Units.SI)
            {
                Debug.LogError(
                    "Definition-driven body spawning currently requires Gravity Engine to use SI units.",
                    this);
                return false;
            }

            positionMetersPerPhysicsUnit =
                gravityEngine.GetPhysicalScale();
            velocityMetersPerSecondPerPhysicsUnit =
                GravityScaler.VelocityScaletoSIUnits();

            if (!IsFinite(
                    positionMetersPerPhysicsUnit) ||
                positionMetersPerPhysicsUnit <= 0.0 ||
                !IsFinite(
                    velocityMetersPerSecondPerPhysicsUnit) ||
                velocityMetersPerSecondPerPhysicsUnit <= 0.0)
            {
                Debug.LogError(
                    "Gravity Engine returned invalid SI position or velocity scaling.",
                    this);
                return false;
            }

            return true;
        }

        private bool HasExistingInstanceId(
            string candidateInstanceId)
        {
            if (spawnedBodies.ContainsKey(
                    candidateInstanceId))
            {
                return true;
            }

            var existingContexts =
                FindObjectsByType<
                    CelestialBodyRuntimeContext>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

            foreach (var existingContext in
                existingContexts)
            {
                if (existingContext != null &&
                    string.Equals(
                        existingContext.InstanceId,
                        candidateInstanceId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
