/*
 * Owns galaxy-level runtime generation and delegates individual systems to the star-system factory.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class CelestialGalaxyFactory
    {
        public sealed class GeneratedGalaxy
        {
            internal GeneratedGalaxy(
                string instanceId,
                Transform root,
                CelestialStarSystemFactory.GeneratedSystem starSystem)
            {
                InstanceId = instanceId;
                Root = root;
                StarSystem = starSystem;
            }

            public string InstanceId { get; }

            public Transform Root { get; }

            public CelestialStarSystemFactory.GeneratedSystem StarSystem { get; }
        }

        private readonly CelestialStarSystemFactory starSystemFactory;

        public CelestialGalaxyFactory(
            CelestialBodyFactory bodyFactory)
        {
            starSystemFactory =
                new CelestialStarSystemFactory(
                    bodyFactory);
        }

        public string LastError { get; private set; }

        public bool TryGenerate(
            string galaxyInstanceId,
            string starSystemInstanceId,
            int seed,
            CelestialSystemGenerationGuide guide,
            CelestialGalacticEnvironmentDefinition environment,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion rotation,
            Transform parent,
            out GeneratedGalaxy generatedGalaxy)
        {
            generatedGalaxy = null;
            LastError = string.Empty;

            if (string.IsNullOrWhiteSpace(
                    galaxyInstanceId))
            {
                return SetError(
                    "A generated galaxy requires an instance ID.");
            }

            var rootObject =
                new GameObject(
                    $"Galaxy ({galaxyInstanceId})");
            var root =
                rootObject.transform;
            root.SetParent(
                parent,
                false);

            if (!starSystemFactory.TryGenerate(
                    starSystemInstanceId,
                    seed,
                    guide,
                    environment,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    rotation,
                    root,
                    out var generatedStarSystem))
            {
                var starSystemError =
                    starSystemFactory.LastError;
                Object.Destroy(
                    rootObject);
                return SetError(
                    $"Galaxy '{galaxyInstanceId}' failed to generate star system '{starSystemInstanceId}': {starSystemError}");
            }

            generatedGalaxy =
                new GeneratedGalaxy(
                    galaxyInstanceId,
                    root,
                    generatedStarSystem);
            return true;
        }

        public bool TryDespawn(
            GeneratedGalaxy generatedGalaxy)
        {
            LastError = string.Empty;

            if (generatedGalaxy == null)
            {
                return SetError(
                    "A generated galaxy is required for despawning.");
            }

            if (generatedGalaxy.StarSystem != null &&
                !starSystemFactory.TryDespawn(
                    generatedGalaxy.StarSystem))
            {
                return SetError(
                    $"Galaxy '{generatedGalaxy.InstanceId}' could not despawn its star system: {starSystemFactory.LastError}");
            }

            if (generatedGalaxy.Root != null)
            {
                Object.Destroy(
                    generatedGalaxy.Root.gameObject);
            }

            return true;
        }

        private bool SetError(
            string error)
        {
            LastError = error;
            Debug.LogError(
                error);
            return false;
        }
    }
}
