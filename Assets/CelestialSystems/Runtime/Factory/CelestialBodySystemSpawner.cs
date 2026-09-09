/*
 * Provides an Inspector-authored scene entry point for the reusable celestial-body-system factory.
 */

using System.Collections;
using UnityEngine;


namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialBodySystemSpawner :
        MonoBehaviour
    {
        [Header("Factory")]
        [SerializeField]
        private CelestialBodyFactory bodyFactory;

        [Header("Body System")]
        [SerializeField]
        private CelestialBodySystemDefinition definition;

        [SerializeField]
        private string instanceId =
            "body-system-instance";

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
        private Transform generatedSystemRoot;

        [SerializeField]
        private int generatedBodyCount;

        [SerializeField]
        private string lastError;

        private CelestialBodySystemFactory systemFactory;

        private CelestialBodySystemFactory.GeneratedSystem generatedSystem;

        public CelestialBodyFactory BodyFactory =>
            bodyFactory;

        public CelestialBodySystemDefinition Definition =>
            definition;

        public string InstanceId =>
            instanceId;

        public bool WaitingForFactory =>
            waitingForFactory;

        public bool SpawnAttempted =>
            spawnAttempted;

        public bool SpawnSucceeded =>
            spawnSucceeded;

        public Transform GeneratedSystemRoot =>
            generatedSystemRoot;

        public int GeneratedBodyCount =>
            generatedBodyCount;

        public string LastError =>
            lastError;

        private IEnumerator Start()
        {
            if (!spawnOnStart)
            {
                yield break;
            }

            if (bodyFactory == null)
            {
                SetError(
                    "A celestial body system spawner requires a body factory.");
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

            TrySpawn();
        }

        public bool TrySpawn()
        {
            spawnAttempted = true;
            spawnSucceeded = false;
            generatedSystemRoot = null;
            generatedBodyCount = 0;
            lastError = string.Empty;

            if (!Application.isPlaying)
            {
                return SetError(
                    "Celestial body systems can only be spawned while the application is playing.");
            }

            if (bodyFactory == null)
            {
                return SetError(
                    "A celestial body system spawner requires a body factory.");
            }

            if (definition == null)
            {
                return SetError(
                    "A celestial body system spawner requires a system definition.");
            }

            systemFactory ??=
                new CelestialBodySystemFactory(
                    bodyFactory);
            spawnSucceeded =
                systemFactory.TryGenerate(
                    instanceId,
                    definition,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    Quaternion.Euler(
                        rotationEulerDegrees),
                    parentOverride,
                    out generatedSystem);

            if (!spawnSucceeded)
            {
                lastError =
                    systemFactory.LastError;
                return false;
            }

            generatedSystemRoot =
                generatedSystem.Root;
            generatedBodyCount =
                generatedSystem.Bodies.Count;
            return true;
        }

        public bool TryDespawn()
        {
            if (systemFactory == null ||
                generatedSystem == null)
            {
                return false;
            }

            var despawnSucceeded =
                systemFactory.TryDespawn(
                    generatedSystem);

            if (!despawnSucceeded)
            {
                lastError =
                    systemFactory.LastError;
                return false;
            }

            generatedSystem = null;
            generatedSystemRoot = null;
            generatedBodyCount = 0;
            spawnSucceeded = false;
            return true;
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
