/*
 * Presents one packaged celestial-body instance through a stable facade while preserving the existing runtime-context API.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class CelestialBodyRuntimeContext :
        MonoBehaviour
    {
        private const CelestialBodyReadiness RequiredPackageReadiness =
            CelestialBodyReadiness.Definition |
            CelestialBodyReadiness.Registered |
            CelestialBodyReadiness.RuntimeHierarchy |
            CelestialBodyReadiness.Motion;

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

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile qualityProfile;

        [SerializeField]
        private MonoBehaviour motionProviderSource;

        [Header("Generated Runtime Hierarchy")]
        [SerializeField]
        private Transform motionRoot;

        [SerializeField]
        private Transform surfaceRoot;

        [SerializeField]
        private Transform oceanRoot;

        [SerializeField]
        private Transform developmentRoot;

        [SerializeField]
        private CelestialSurfaceRuntime surfaceRuntime;

        [SerializeField]
        private CelestialSurfaceFoundationDiagnostics surfaceFoundationDiagnostics;

        [Header("Resolved Runtime State")]
        [SerializeField]
        private CelestialBodyLifecycleState lifecycleState;

        [SerializeField]
        private CelestialBodyReadiness readiness;

        [SerializeField]
        private bool hasValidConfiguration;

        [SerializeField]
        private CelestialSurfaceSystem resolvedSurfaceSystem;

        [SerializeField]
        private double configuredMassKilograms;

        [SerializeField]
        private double configuredReferenceRadiusMeters;

        [SerializeField]
        private UniverseMotionState currentMotionState;

        [SerializeField]
        private string lastError;

        private ICelestialBodyMotionProvider motionProvider;
        private bool coarseSurfaceReady;
        private bool visibleSurfaceReady;
        private bool collisionSurfaceReady;
        private bool oceanReady;

        public static IReadOnlyCollection<
            CelestialBodyRuntimeContext> ActiveContexts =>
                activeContexts;

        public string InstanceId =>
            instanceId;

        public CelestialBodyDefinition Definition =>
            definition;

        public string Description =>
            definition != null
                ? definition.Description
                : string.Empty;

        public UniverseFrameController UniverseFrame =>
            universeFrame;

        public NBody GravityBody =>
            gravityBody;

        public Transform MotionRoot =>
            motionRoot;

        public Transform VisualRoot =>
            visualRoot;

        public Transform SurfaceRoot =>
            surfaceRoot;

        public Transform OceanRoot =>
            oceanRoot;

        public Transform DevelopmentRoot =>
            developmentRoot;

        public CelestialSurfaceRuntime SurfaceRuntime =>
            surfaceRuntime;

        public CelestialSurfaceFoundationDiagnostics SurfaceFoundationDiagnostics =>
            surfaceFoundationDiagnostics;

        public RoundMapMagicSurfaceQualityProfile QualityProfile =>
            qualityProfile;

        public MonoBehaviour MotionProviderSource =>
            motionProviderSource;

        public string MotionProviderName =>
            motionProvider != null
                ? motionProvider.ProviderName
                : string.Empty;

        public CelestialBodyLifecycleState LifecycleState =>
            lifecycleState;

        public CelestialBodyReadiness Readiness =>
            readiness;

        public bool IsReady =>
            (readiness & RequiredPackageReadiness) ==
                RequiredPackageReadiness;

        public bool IsRegistered =>
            HasReadiness(
                CelestialBodyReadiness.Registered);

        public bool IsMotionReady =>
            HasReadiness(
                CelestialBodyReadiness.Motion);

        public bool HasRuntimeHierarchy =>
            HasReadiness(
                CelestialBodyReadiness.RuntimeHierarchy);

        public bool HasSurfaceFoundation =>
            HasReadiness(
                CelestialBodyReadiness.SurfaceFoundation);

        public bool HasCoarseSurface =>
            HasReadiness(
                CelestialBodyReadiness.CoarseSurface);

        public bool HasVisibleSurface =>
            HasReadiness(
                CelestialBodyReadiness.VisibleSurface);

        public bool HasCollisionSurface =>
            HasReadiness(
                CelestialBodyReadiness.CollisionSurface);

        public bool HasOcean =>
            HasReadiness(
                CelestialBodyReadiness.Ocean);

        public bool HasValidConfiguration =>
            hasValidConfiguration;

        public CelestialSurfaceSystem ResolvedSurfaceSystem =>
            resolvedSurfaceSystem;

        public double ConfiguredMassKilograms =>
            configuredMassKilograms;

        public double ConfiguredReferenceRadiusMeters =>
            configuredReferenceRadiusMeters;

        public UniverseMotionState CurrentMotionState =>
            currentMotionState;

        public string LastError =>
            lastError;

        public event Action<CelestialBodyRuntimeContext> Initialized;

        public event Action<
            CelestialBodyRuntimeContext,
            CelestialBodyLifecycleState> LifecycleStateChanged;

        public event Action<
            CelestialBodyRuntimeContext,
            CelestialBodyReadiness> ReadinessChanged;

        public event Action<CelestialBodyRuntimeContext> Destroying;

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
            RefreshReadiness();
        }

        private void OnDisable()
        {
            activeContexts.Remove(
                this);
            RefreshReadiness();
        }

        private void OnDestroy()
        {
            SetLifecycleState(
                CelestialBodyLifecycleState.Destroying);
            Destroying?.Invoke(
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
            ResolveMotionProvider();
            RefreshRuntimeState();
            ValidateConfiguration();

            if (hasValidConfiguration)
            {
                SetLifecycleState(
                    CelestialBodyLifecycleState.Active);
            }

            RefreshReadiness();
        }

        private void LateUpdate()
        {
            RefreshReadiness();
        }

        private void OnValidate()
        {
            ResolveMotionProvider();
            RefreshRuntimeState();
            RefreshReadiness();
        }

        public void Initialize(
            string newInstanceId,
            CelestialBodyDefinition newDefinition,
            UniverseFrameController newUniverseFrame,
            NBody newGravityBody,
            Transform newVisualRoot)
        {
            SetLifecycleState(
                CelestialBodyLifecycleState.Initializing);

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

            ResolveMotionProvider();
            FinishInitialization();
        }

        public bool InitializePackage(
            CelestialBodySpawnRequest request,
            UniverseFrameController newUniverseFrame,
            NBody newGravityBody,
            MonoBehaviour newMotionProviderSource,
            Transform newMotionRoot,
            Transform newVisualRoot,
            Transform newSurfaceRoot,
            Transform newOceanRoot,
            Transform newDevelopmentRoot)
        {
            SetLifecycleState(
                CelestialBodyLifecycleState.Initializing);

            if (request == null)
            {
                FailInitialization(
                    "A celestial body runtime package requires a spawn request.");
                return false;
            }

            instanceId =
                request.InstanceId;
            definition =
                request.Definition;
            universeFrame =
                newUniverseFrame;
            gravityBody =
                newGravityBody;
            qualityProfile =
                request.QualityProfile;
            motionProviderSource =
                newMotionProviderSource;
            motionRoot =
                newMotionRoot;
            visualRoot =
                newVisualRoot;
            surfaceRoot =
                newSurfaceRoot;
            oceanRoot =
                newOceanRoot;
            developmentRoot =
                newDevelopmentRoot;

            ResolveMotionProvider();

            if (motionProviderSource == null ||
                motionProvider == null)
            {
                FailInitialization(
                    "The runtime package requires a component that implements ICelestialBodyMotionProvider.");
                return false;
            }

            return FinishInitialization();
        }

        public bool TryGetMotionState(
            out UniverseMotionState motionState)
        {
            if (motionProvider == null ||
                !motionProvider.TryGetMotionState(
                    out motionState))
            {
                motionState = default;
                return false;
            }

            currentMotionState =
                motionState;
            return true;
        }

        public void ReportSurfaceReadiness(
            bool hasCoarseSurface,
            bool hasVisibleSurface,
            bool hasCollisionSurface)
        {
            coarseSurfaceReady =
                hasCoarseSurface;
            visibleSurfaceReady =
                hasVisibleSurface;
            collisionSurfaceReady =
                hasCollisionSurface;

            RefreshReadiness();
        }

        public void ReportOceanReadiness(
            bool hasReadyOcean)
        {
            oceanReady =
                hasReadyOcean;
            RefreshReadiness();
        }

        internal void BeginDespawn()
        {
            SetLifecycleState(
                CelestialBodyLifecycleState.Destroying);
        }

        internal void AttachSurfaceRuntime(
            CelestialSurfaceRuntime newSurfaceRuntime)
        {
            surfaceRuntime =
                newSurfaceRuntime;
            RefreshReadiness();
        }

        internal void AttachSurfaceFoundationDiagnostics(
            CelestialSurfaceFoundationDiagnostics diagnostics)
        {
            surfaceFoundationDiagnostics =
                diagnostics;
        }

        private bool FinishInitialization()
        {
            RefreshRuntimeState();

            if (!hasValidConfiguration)
            {
                FailInitialization(
                    "The celestial body runtime package has invalid definition, frame, or motion-provider configuration.");
                return false;
            }

            lastError = string.Empty;
            SetLifecycleState(
                CelestialBodyLifecycleState.Active);
            RefreshReadiness();
            Initialized?.Invoke(
                this);
            return true;
        }

        private void FailInitialization(
            string error)
        {
            lastError = error;
            hasValidConfiguration = false;
            SetLifecycleState(
                CelestialBodyLifecycleState.Failed);
            RefreshReadiness();
        }

        private void ValidateConfiguration()
        {
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

        }

        private void ResolveMotionProvider()
        {
            motionProvider =
                motionProviderSource as
                    ICelestialBodyMotionProvider;
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
                universeFrame != null;
        }

        private void RefreshReadiness()
        {
            var updatedReadiness =
                CelestialBodyReadiness.None;

            if (definition != null &&
                definition.HasValidPhysicalSettings &&
                definition.HasValidResolvedSurfaceSettings)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.Definition;
            }

            if (activeContexts.Contains(
                    this))
            {
                updatedReadiness |=
                    CelestialBodyReadiness.Registered;
            }

            if (motionRoot != null &&
                visualRoot != null &&
                surfaceRoot != null &&
                oceanRoot != null &&
                developmentRoot != null)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.RuntimeHierarchy;
            }

            if (TryGetMotionState(
                    out currentMotionState))
            {
                updatedReadiness |=
                    CelestialBodyReadiness.Motion;
            }

            if (surfaceRuntime != null &&
                surfaceRuntime.FoundationReady)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.SurfaceFoundation;
            }

            if (coarseSurfaceReady)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.CoarseSurface;
            }

            if (visibleSurfaceReady)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.VisibleSurface;
            }

            if (collisionSurfaceReady)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.CollisionSurface;
            }

            if (oceanReady)
            {
                updatedReadiness |=
                    CelestialBodyReadiness.Ocean;
            }

            if (updatedReadiness ==
                readiness)
            {
                return;
            }

            readiness =
                updatedReadiness;
            ReadinessChanged?.Invoke(
                this,
                readiness);
        }

        private bool HasReadiness(
            CelestialBodyReadiness requiredReadiness)
        {
            return
                (readiness & requiredReadiness) ==
                    requiredReadiness;
        }

        private void SetLifecycleState(
            CelestialBodyLifecycleState newState)
        {
            if (lifecycleState ==
                newState)
            {
                return;
            }

            lifecycleState =
                newState;
            LifecycleStateChanged?.Invoke(
                this,
                lifecycleState);
        }
    }
}
