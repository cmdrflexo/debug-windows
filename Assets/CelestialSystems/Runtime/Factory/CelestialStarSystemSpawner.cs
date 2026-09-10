/*
 * Provides an Inspector-authored scene entry point for seeded, guide-driven celestial star-system generation.
 */

using System.Collections;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialStarSystemSpawner :
        MonoBehaviour
    {
        [Header("Factory")]
        [SerializeField]
        private CelestialBodyFactory bodyFactory;

        [Header("Generation")]
        [SerializeField]
        private CelestialSystemGenerationGuide generationGuide;

        [SerializeField]
        [Tooltip("Optional galactic conditions supplied to environment-aware generation guides.")]
        private CelestialGalacticEnvironmentDefinition galacticEnvironment;

        [SerializeField]
        private int seed = 1;

        [SerializeField]
        private string instanceId =
            "star-system-instance";

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
        private int generatedBodySystemCount;

        [SerializeField]
        private int generatedBodyCount;

        [SerializeField]
        private string lastError;

        private CelestialStarSystemFactory starSystemFactory;

        private CelestialStarSystemFactory.GeneratedSystem generatedSystem;

        public CelestialBodyFactory BodyFactory =>
            bodyFactory;

        public CelestialSystemGenerationGuide GenerationGuide =>
            generationGuide;

        public CelestialGalacticEnvironmentDefinition GalacticEnvironment =>
            galacticEnvironment;

        public int Seed =>
            seed;

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

        public int GeneratedBodySystemCount =>
            generatedBodySystemCount;

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
                    "A celestial star system spawner requires a body factory.");
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
            generatedBodySystemCount = 0;
            generatedBodyCount = 0;
            lastError = string.Empty;

            if (!Application.isPlaying)
            {
                return SetError(
                    "Celestial star systems can only be spawned while the application is playing.");
            }

            if (bodyFactory == null)
            {
                return SetError(
                    "A celestial star system spawner requires a body factory.");
            }

            if (generationGuide == null)
            {
                return SetError(
                    "A celestial star system spawner requires a system generation guide.");
            }

            if (generatedSystem != null)
            {
                return SetError(
                    "This celestial star system spawner already has a generated system.");
            }

            starSystemFactory ??=
                new CelestialStarSystemFactory(
                    bodyFactory);
            spawnSucceeded =
                starSystemFactory.TryGenerate(
                    instanceId,
                    seed,
                    generationGuide,
                    galacticEnvironment,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    Quaternion.Euler(
                        rotationEulerDegrees),
                    parentOverride,
                    out generatedSystem);

            if (!spawnSucceeded)
            {
                lastError =
                    starSystemFactory.LastError;
                return false;
            }

            generatedSystemRoot =
                generatedSystem.Root;
            generatedBodySystemCount =
                generatedSystem.BodySystems.Count;

            foreach (var bodySystem in
                generatedSystem.BodySystems.Values)
            {
                if (bodySystem != null)
                {
                    generatedBodyCount +=
                        bodySystem.Bodies.Count;
                }
            }

            return true;
        }

        public bool TryDespawn()
        {
            if (starSystemFactory == null ||
                generatedSystem == null)
            {
                return false;
            }

            var despawnSucceeded =
                starSystemFactory.TryDespawn(
                    generatedSystem);

            if (!despawnSucceeded)
            {
                lastError =
                    starSystemFactory.LastError;
                return false;
            }

            generatedSystem = null;
            generatedSystemRoot = null;
            generatedBodySystemCount = 0;
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
