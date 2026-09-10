/*
 * Provides the scene-facing entry point for the universe-to-body runtime factory chain.
 */

using System.Collections;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialUniverseRuntimeController :
        MonoBehaviour
    {
        [Header("Factory")]
        [SerializeField]
        private CelestialBodyFactory bodyFactory;

        [Header("Prototype Generation")]
        [SerializeField]
        private CelestialSystemGenerationGuide starSystemGuide;

        [SerializeField]
        private CelestialGalacticEnvironmentDefinition galacticEnvironment;

        [SerializeField]
        private int seed = 1;

        [SerializeField]
        private string universeInstanceId =
            "universe-runtime";

        [SerializeField]
        private string galaxyInstanceId =
            "prototype-galaxy";

        [SerializeField]
        private string starSystemInstanceId =
            "prototype-star-system";

        [Header("Initial Motion")]
        [SerializeField]
        private DoubleVector3 positionMetersFromFrameOrigin;

        [SerializeField]
        private DoubleVector3 velocityMetersPerSecond;

        [SerializeField]
        private Vector3 rotationEulerDegrees;

        [Header("Lifecycle")]
        [SerializeField]
        private Transform generatedParentOverride;

        [SerializeField]
        private bool generateOnStart = true;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForFactory;

        [SerializeField]
        private bool generationAttempted;

        [SerializeField]
        private bool generationSucceeded;

        [SerializeField]
        private Transform generatedUniverseRoot;

        [SerializeField]
        private int generatedGalaxyCount;

        [SerializeField]
        private int generatedStarSystemCount;

        [SerializeField]
        private int generatedBodySystemCount;

        [SerializeField]
        private int generatedBodyCount;

        [SerializeField]
        private string lastError;

        private CelestialUniverseFactory universeFactory;

        private CelestialUniverseFactory.GeneratedUniverse generatedUniverse;

        public CelestialBodyFactory BodyFactory =>
            bodyFactory;

        public CelestialSystemGenerationGuide StarSystemGuide =>
            starSystemGuide;

        public CelestialGalacticEnvironmentDefinition GalacticEnvironment =>
            galacticEnvironment;

        public int Seed =>
            seed;

        public bool WaitingForFactory =>
            waitingForFactory;

        public bool GenerationAttempted =>
            generationAttempted;

        public bool GenerationSucceeded =>
            generationSucceeded;

        public Transform GeneratedUniverseRoot =>
            generatedUniverseRoot;

        public int GeneratedGalaxyCount =>
            generatedGalaxyCount;

        public int GeneratedStarSystemCount =>
            generatedStarSystemCount;

        public int GeneratedBodySystemCount =>
            generatedBodySystemCount;

        public int GeneratedBodyCount =>
            generatedBodyCount;

        public string LastError =>
            lastError;

        private IEnumerator Start()
        {
            if (!generateOnStart)
            {
                yield break;
            }

            if (bodyFactory == null)
            {
                SetError(
                    "The universe runtime controller requires a celestial body factory.");
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

            TryGenerate();
        }

        public bool TryGenerate()
        {
            if (generatedUniverse != null)
            {
                return SetError(
                    "This universe runtime controller already owns a generated universe.");
            }

            ResetRuntimeSummary();
            generationAttempted = true;

            if (!Application.isPlaying)
            {
                return SetError(
                    "A universe can only be generated while the application is playing.");
            }

            if (bodyFactory == null)
            {
                return SetError(
                    "The universe runtime controller requires a celestial body factory.");
            }

            if (starSystemGuide == null)
            {
                return SetError(
                    "The universe runtime controller requires a star-system generation guide.");
            }

            if (galacticEnvironment == null)
            {
                return SetError(
                    "The universe runtime controller requires a galactic environment.");
            }

            universeFactory ??=
                new CelestialUniverseFactory(                    bodyFactory);

            var parent =
                generatedParentOverride != null
                    ? generatedParentOverride
                    : transform;

            generationSucceeded =
                universeFactory.TryGenerate(
                    universeInstanceId,
                    galaxyInstanceId,
                    starSystemInstanceId,
                    seed,
                    starSystemGuide,
                    galacticEnvironment,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    Quaternion.Euler(
                        rotationEulerDegrees),
                    parent,
                    out generatedUniverse);

            if (!generationSucceeded)
            {
                lastError =
                    universeFactory.LastError;
                return false;
            }

            generatedUniverseRoot =
                generatedUniverse.Root;
            generatedGalaxyCount = 1;
            generatedStarSystemCount = 1;

            var starSystem =
                generatedUniverse.Galaxy?.StarSystem;

            if (starSystem != null)
            {
                generatedBodySystemCount =
                    starSystem.BodySystems.Count;

                foreach (var bodySystem in
                    starSystem.BodySystems.Values)
                {
                    if (bodySystem != null)
                    {
                        generatedBodyCount +=
                            bodySystem.Bodies.Count;
                    }
                }
            }

            return true;
        }

        public bool TryDespawn()
        {
            if (universeFactory == null ||
                generatedUniverse == null)
            {
                return false;
            }

            if (!universeFactory.TryDespawn(
                    generatedUniverse))
            {
                lastError =
                    universeFactory.LastError;
                return false;
            }

            generatedUniverse = null;
            ResetRuntimeSummary();
            return true;
        }

        private void ResetRuntimeSummary()
        {
            generationSucceeded = false;
            generatedUniverseRoot = null;
            generatedGalaxyCount = 0;
            generatedStarSystemCount = 0;
            generatedBodySystemCount = 0;
            generatedBodyCount = 0;
            lastError = string.Empty;
        }

        private bool SetError(
            string error)
        {
            generationSucceeded = false;
            lastError = error;
            Debug.LogError(
                error,
                this);
            return false;
        }
    }
}
