/*
 * Owns universe-level runtime generation and delegates its current prototype galaxy to the galaxy factory.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public sealed class CelestialUniverseFactory
    {
        public sealed class GeneratedUniverse
        {
            internal GeneratedUniverse(
                string instanceId,
                Transform root,
                CelestialGalaxyFactory.GeneratedGalaxy galaxy)
            {
                InstanceId = instanceId;
                Root = root;
                Galaxy = galaxy;
            }

            public string InstanceId { get; }

            public Transform Root { get; }

            public CelestialGalaxyFactory.GeneratedGalaxy Galaxy { get; }
        }

        private readonly CelestialGalaxyFactory galaxyFactory;

        public CelestialUniverseFactory(
            CelestialBodyFactory bodyFactory)
        {
            galaxyFactory =
                new CelestialGalaxyFactory(
                    bodyFactory);
        }

        public string LastError { get; private set; }

        public bool TryGenerate(
            string universeInstanceId,
            string galaxyInstanceId,
            string starSystemInstanceId,
            int seed,
            CelestialSystemGenerationGuide guide,
            CelestialGalacticEnvironmentDefinition environment,
            DoubleVector3 positionMetersFromFrameOrigin,
            DoubleVector3 velocityMetersPerSecond,
            Quaternion rotation,
            Transform parent,
            out GeneratedUniverse generatedUniverse)
        {
            generatedUniverse = null;
            LastError = string.Empty;

            if (string.IsNullOrWhiteSpace(
                    universeInstanceId))
            {
                return SetError(
                    "A generated universe requires an instance ID.");
            }

            var rootObject =
                new GameObject(
                    $"Universe ({universeInstanceId})");
            var root =
                rootObject.transform;
            root.SetParent(
                parent,
                false);

            if (!galaxyFactory.TryGenerate(
                    galaxyInstanceId,
                    starSystemInstanceId,
                    seed,
                    guide,
                    environment,
                    positionMetersFromFrameOrigin,
                    velocityMetersPerSecond,
                    rotation,
                    root,
                    out var generatedGalaxy))
            {
                var galaxyError =
                    galaxyFactory.LastError;
                Object.Destroy(
                    rootObject);
                return SetError(
                    $"Universe '{universeInstanceId}' failed to generate galaxy '{galaxyInstanceId}': {galaxyError}");
            }

            generatedUniverse =
                new GeneratedUniverse(
                    universeInstanceId,
                    root,
                    generatedGalaxy);
            return true;
        }

        public bool TryDespawn(
            GeneratedUniverse generatedUniverse)
        {
            LastError = string.Empty;

            if (generatedUniverse == null)
            {
                return SetError(
                    "A generated universe is required for despawning.");
            }

            if (generatedUniverse.Galaxy != null &&
                !galaxyFactory.TryDespawn(
                    generatedUniverse.Galaxy))
            {
                return SetError(
                    $"Universe '{generatedUniverse.InstanceId}' could not despawn its galaxy: {galaxyFactory.LastError}");
            }

            if (generatedUniverse.Root != null)
            {
                Object.Destroy(
                    generatedUniverse.Root.gameObject);
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
