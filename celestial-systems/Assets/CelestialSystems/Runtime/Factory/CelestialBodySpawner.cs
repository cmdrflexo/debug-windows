/*
 * Provides an Inspector-authored scene entry point for the same celestial-body factory used by procedural game code.
 */

using System.Collections;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialBodySpawner :
        MonoBehaviour
    {
        [Header("Factory")]
        [SerializeField]
        private CelestialBodyFactory factory;

        [Header("Body")]
        [SerializeField]
        private CelestialBodyDefinition definition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile qualityProfile;

        [SerializeField]
        private string instanceId =
            "body-instance";

        [Header("Initial Motion")]
        [SerializeField]
        private DoubleVector3 positionMetersFromFrameOrigin;

        [SerializeField]
        private DoubleVector3 velocityMetersPerSecond;

        [SerializeField]
        private Vector3 rotationEulerDegrees;

        [Header("Placement")]
        [SerializeField]
        private Transform parentOverride;

        [SerializeField]
        private bool spawnOnStart = true;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForFactory;

        [SerializeField]
        private bool spawnAttempted;

        [SerializeField]
        private bool spawnSucceeded;

        [SerializeField]
        private CelestialBodyRuntimeContext spawnedBody;

        [SerializeField]
        private string lastError;

        public CelestialBodyFactory Factory =>
            factory;

        public CelestialBodyDefinition Definition =>
            definition;

        public string InstanceId =>
            instanceId;

        public bool WaitingForFactory =>
            waitingForFactory;

        public bool SpawnAttempted =>
            spawnAttempted;

        public bool SpawnSucceeded =>
            spawnSucceeded;

        public CelestialBodyRuntimeContext SpawnedBody =>
            spawnedBody;

        public string LastError =>
            lastError;

        private IEnumerator Start()
        {
            if (!spawnOnStart)
            {
                yield break;
            }

            if (factory == null)
            {
                SetError(
                    "A celestial body spawner requires a factory.");
                yield break;
            }

            waitingForFactory = true;

            while (factory != null &&
                !factory.CanSpawnBodies)
            {
                yield return null;
            }

            waitingForFactory = false;

            if (factory == null)
            {
                SetError(
                    "The assigned celestial body factory was destroyed before it became ready.");
                yield break;
            }

            TrySpawn();
        }

        public bool TrySpawn()
        {
            spawnAttempted = true;
            spawnSucceeded = false;
            lastError = string.Empty;

            if (!Application.isPlaying)
            {
                return SetError(
                    "Celestial bodies can only be spawned while the application is playing.");
            }

            if (factory == null)
            {
                return SetError(
                    "A celestial body spawner requires a factory.");
            }

            if (definition == null)
            {
                return SetError(
                    "A celestial body spawner requires a body definition.");
            }

            var request =
                new CelestialBodySpawnRequest(
                    instanceId,
                    definition,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    Quaternion.Euler(
                        rotationEulerDegrees),
                    qualityProfile,
                    parentOverride);

            spawnSucceeded =
                factory.TrySpawnBody(
                    request,
                    out spawnedBody);

            if (!spawnSucceeded)
            {
                lastError =
                    factory.LastError;
            }

            return spawnSucceeded;
        }

        public bool TryDespawn()
        {
            if (factory == null ||
                spawnedBody == null)
            {
                return false;
            }

            var despawnSucceeded =
                factory.TryDespawnBody(
                    spawnedBody.InstanceId);

            if (despawnSucceeded)
            {
                spawnedBody = null;
                spawnSucceeded = false;
            }

            return despawnSucceeded;
        }

        private bool SetError(
            string error)
        {
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }
    }
}
