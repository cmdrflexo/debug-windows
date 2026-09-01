/*
 * Binds one spawned celestial-body instance to its reusable definition, Gravity Engine body, universe frame, and optional visual.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class CelestialBodyRuntimeContext :
        MonoBehaviour
    {
        private static readonly HashSet<
            CelestialBodyRuntimeContext> activeContexts =
                new HashSet<
                    CelestialBodyRuntimeContext>();

        [Header("Identity")]
        [SerializeField]
        private string instanceId =
            "body-instance";

        [SerializeField]
        private CelestialBodyDefinition definition;

        [Header("Runtime References")]
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private NBody gravityBody;

        [SerializeField]
        private Transform visualRoot;

        [Header("Resolved Runtime State")]
        [SerializeField]
        private bool hasValidConfiguration;

        [SerializeField]
        private CelestialSurfaceSystem resolvedSurfaceSystem;

        [SerializeField]
        private double configuredMassKilograms;

        [SerializeField]
        private double configuredReferenceRadiusMeters;

        public static IReadOnlyCollection<
            CelestialBodyRuntimeContext> ActiveContexts =>
                activeContexts;

        public string InstanceId =>
            instanceId;

        public CelestialBodyDefinition Definition =>
            definition;

        public UniverseFrameController UniverseFrame =>
            universeFrame;

        public NBody GravityBody =>
            gravityBody;

        public Transform VisualRoot =>
            visualRoot;

        public bool HasValidConfiguration =>
            hasValidConfiguration;

        public CelestialSurfaceSystem ResolvedSurfaceSystem =>
            resolvedSurfaceSystem;

        public double ConfiguredMassKilograms =>
            configuredMassKilograms;

        public double ConfiguredReferenceRadiusMeters =>
            configuredReferenceRadiusMeters;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveContexts()
        {
            activeContexts.Clear();
        }

        private void OnEnable()
        {
            activeContexts.Add(
                this);
        }

        private void OnDisable()
        {
            activeContexts.Remove(
                this);
        }

        private void Reset()
        {
            gravityBody =
                GetComponent<NBody>();
        }

        private void Start()
        {
            activeContexts.Add(
                this);
            RefreshRuntimeState();

            if (string.IsNullOrWhiteSpace(
                    instanceId))
            {
                Debug.LogError(
                    "A celestial body runtime context requires a unique instance ID.",
                    this);
            }

            if (definition == null)
            {
                Debug.LogError(
                    "A celestial body runtime context requires a body definition.",
                    this);
            }
            else
            {
                if (!definition.HasValidPhysicalSettings)
                {
                    Debug.LogError(
                        "The assigned celestial body definition has invalid physical settings.",
                        this);
                }

                if (!definition.HasValidResolvedSurfaceSettings)
                {
                    Debug.LogError(
                        "The assigned celestial body definition does not have valid settings for its resolved surface system.",
                        this);
                }
            }

            if (universeFrame == null)
            {
                Debug.LogError(
                    "A celestial body runtime context requires a universe frame controller.",
                    this);
            }

            if (gravityBody == null)
            {
                Debug.LogError(
                    "A celestial body runtime context requires an NBody.",
                    this);
            }
        }

        private void OnValidate()
        {
            RefreshRuntimeState();
        }

        public void Initialize(
            string newInstanceId,
            CelestialBodyDefinition newDefinition,
            UniverseFrameController newUniverseFrame,
            NBody newGravityBody,
            Transform newVisualRoot)
        {
            instanceId =
                newInstanceId;
            definition =
                newDefinition;
            universeFrame =
                newUniverseFrame;
            gravityBody =
                newGravityBody;
            visualRoot =
                newVisualRoot;

            RefreshRuntimeState();
        }

        private void RefreshRuntimeState()
        {
            if (definition == null)
            {
                hasValidConfiguration = false;
                resolvedSurfaceSystem = default;
                configuredMassKilograms = default;
                configuredReferenceRadiusMeters = default;
                return;
            }

            resolvedSurfaceSystem =
                definition.ResolvedSurfaceSystem;
            configuredMassKilograms =
                definition.MassKilograms;
            configuredReferenceRadiusMeters =
                definition.ReferenceRadiusMeters;
            hasValidConfiguration =
                !string.IsNullOrWhiteSpace(
                    instanceId) &&
                definition.HasValidPhysicalSettings &&
                definition.HasValidResolvedSurfaceSettings &&
                universeFrame != null &&
                gravityBody != null;
        }
    }
}
