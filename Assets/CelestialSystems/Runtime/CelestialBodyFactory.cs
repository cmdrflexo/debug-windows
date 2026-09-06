/*
 * Creates packaged, definition-driven celestial bodies and connects their motion to Gravity Engine through an adapter.
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
        private readonly struct RuntimeHierarchy
        {
            public Transform MotionRoot { get; }

            public Transform VisualRoot { get; }

            public Transform SurfaceRoot { get; }

            public Transform OceanRoot { get; }

            public Transform DevelopmentRoot { get; }

            public RuntimeHierarchy(
                Transform motionRoot,
                Transform visualRoot,
                Transform surfaceRoot,
                Transform oceanRoot,
                Transform developmentRoot)
            {
                MotionRoot = motionRoot;
                VisualRoot = visualRoot;
                SurfaceRoot = surfaceRoot;
                OceanRoot = oceanRoot;
                DevelopmentRoot = developmentRoot;
            }
        }

        [Header("Configuration")]
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private CelestialBodyRuntimeContext bodyPrefab;

        [SerializeField]
        private Transform spawnedBodyParent;

        [SerializeField]
        [Tooltip("Optional world-level shared surface cache. One is created automatically when this is empty.")]
        private CelestialSurfaceCacheManager surfaceCacheManager;

        [Header("Adaptive Surface Transition")]
        [SerializeField]
        [Tooltip("Hidden leaves the working Far/Mid/Local renderer untouched. Select Surface or LOD Debug to inspect the adaptive renderer.")]
        private CelestialAdaptiveSurfaceRenderMode adaptiveSurfaceRenderMode;

        [SerializeField]
        [Tooltip("Optional camera used by generated adaptive surfaces. Camera.main is used when this is empty.")]
        private Camera adaptiveSurfaceObserver;

        [Header("Optional Startup Body")]
        [SerializeField]
        private bool spawnOnStart;

        [SerializeField]
        private CelestialBodyDefinition startupDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile startupQualityProfile;

        [SerializeField]
        private string startupInstanceId =
            "spawned-body-01";

        [SerializeField]
        private DoubleVector3 startupPositionMetersFromFrameOrigin;

        [SerializeField]
        private DoubleVector3 startupVelocityMetersPerSecond;

        [SerializeField]
        private Vector3 startupRotationEulerDegrees;

        [Header("Runtime")]
        [SerializeField]
        private bool waitingForGravityEngine;

        [SerializeField]
        private bool motionBackendReady;

        [SerializeField]
        private bool lastSpawnSucceeded;

        [SerializeField]
        private int activeBodyCount;

        [SerializeField]
        private CelestialBodyRuntimeContext lastSpawnedBody;

        [SerializeField]
        private string lastError;

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

        public Transform SpawnedBodyParent =>
            spawnedBodyParent;

        public CelestialSurfaceCacheManager SurfaceCacheManager =>
            surfaceCacheManager;

        public CelestialAdaptiveSurfaceRenderMode AdaptiveSurfaceRenderMode =>
            adaptiveSurfaceRenderMode;

        public Camera AdaptiveSurfaceObserver =>
            adaptiveSurfaceObserver;

        public bool CanSpawnBodies
        {
            get
            {
                RefreshMotionBackendState();
                return
                    Application.isPlaying &&
                    motionBackendReady;
            }
        }

        public bool WaitingForGravityEngine =>
            waitingForGravityEngine;

        public bool MotionBackendReady =>
            motionBackendReady;

        public bool LastSpawnSucceeded =>
            lastSpawnSucceeded;

        public int ActiveBodyCount =>
            activeBodyCount;

        public CelestialBodyRuntimeContext LastSpawnedBody =>
            lastSpawnedBody;

        public string LastError =>
            lastError;

        public event Action<CelestialBodyRuntimeContext> BodySpawned;

        public event Action<CelestialBodyRuntimeContext> BodyDespawned;

        public event Action<string> BodySpawnFailed;

        private void Start()
        {
            gravityEngine =
                GravityEngine.Instance();
            RefreshMotionBackendState();

            if (!spawnOnStart)
            {
                return;
            }

            if (gravityEngine == null)
            {
                RecordSpawnFailure(
                    "Cannot spawn the configured startup body because no Gravity Engine exists in the scene.");
                return;
            }

            waitingForGravityEngine = true;
            gravityEngine.AddGEStartCallback(
                SpawnConfiguredStartupBody);
        }

        private void Update()
        {
            RefreshMotionBackendState();
        }

        private void OnDestroy()
        {
            foreach (var body in
                spawnedBodies.Values)
            {
                if (body != null)
                {
                    body.Destroying -=
                        HandleSpawnedBodyDestroying;
                }
            }

            spawnedBodies.Clear();
            activeBodyCount = 0;
        }

        public bool TrySpawnBody(
            string newInstanceId,
            CelestialBodyDefinition newDefinition,
            DoubleVector3 initialPositionMetersFromFrameOrigin,
            DoubleVector3 initialVelocityMetersPerSecond,
            out CelestialBodyRuntimeContext spawnedBody)
        {
            var request =
                new CelestialBodySpawnRequest(
                    newInstanceId,
                    newDefinition,
                    initialPositionMetersFromFrameOrigin,
                    initialVelocityMetersPerSecond,
                    Quaternion.identity);

            return TrySpawnBody(
                request,
                out spawnedBody);
        }

        public bool TrySpawnBody(
            CelestialBodySpawnRequest request,
            out CelestialBodyRuntimeContext spawnedBody)
        {
            spawnedBody = null;
            lastSpawnSucceeded = false;
            lastError = string.Empty;

            if (!Application.isPlaying)
            {
                return RecordSpawnFailure(
                    "Celestial bodies can only be spawned while the application is playing.");
            }

            if (!TryValidateSpawn(
                    request,
                    out var positionMetersPerPhysicsUnit,
                    out var velocityMetersPerSecondPerPhysicsUnit))
            {
                return false;
            }

            var parent =
                request.ParentOverride != null
                    ? request.ParentOverride
                    : spawnedBodyParent;
            var instance =
                Instantiate(
                    bodyPrefab,
                    parent);

            if (instance == null)
            {
                return RecordSpawnFailure(
                    "The celestial body factory could not instantiate its body prefab.");
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
                Destroy(
                    instance.gameObject);
                return RecordSpawnFailure(
                    "The celestial body prefab requires an NBody assigned to its runtime context or attached to the same GameObject.");
            }

            var massKilograms =
                request.Definition.MassKilograms;

            if (massKilograms >
                float.MaxValue)
            {
                Destroy(
                    instance.gameObject);
                return RecordSpawnFailure(
                    "The celestial body definition's mass exceeds Gravity Engine's NBody mass range.");
            }

            var hierarchy =
                EnsureRuntimeHierarchy(
                    instance);
            var motionProvider =
                hierarchy.MotionRoot.GetComponent<
                    GravityEngineCelestialBodyMotionProvider>();

            if (motionProvider == null)
            {
                motionProvider =
                    hierarchy.MotionRoot.gameObject.AddComponent<
                        GravityEngineCelestialBodyMotionProvider>();
            }

            gravityBody.mass =
                (float)massKilograms;

            gravityBody.SetPosVel3d(
                new Vector3d(
                    request.InitialPositionMetersFromFrameOrigin.x /
                        positionMetersPerPhysicsUnit,
                    request.InitialPositionMetersFromFrameOrigin.y /
                        positionMetersPerPhysicsUnit,
                    request.InitialPositionMetersFromFrameOrigin.z /
                        positionMetersPerPhysicsUnit),
                new Vector3d(
                    request.InitialVelocityMetersPerSecond.x /
                        velocityMetersPerSecondPerPhysicsUnit,
                    request.InitialVelocityMetersPerSecond.y /
                        velocityMetersPerSecondPerPhysicsUnit,
                    request.InitialVelocityMetersPerSecond.z /
                        velocityMetersPerSecondPerPhysicsUnit));

            instance.transform.rotation =
                request.InitialRotation;
            instance.name =
                $"{request.Definition.DefinitionId} ({request.InstanceId})";

            try
            {
                gravityEngine.AddBody(
                    instance.gameObject);

                if (request.MotionMode ==
                    CelestialBodySpawnMode.OnRails)
                {
                    var orbit =
                        instance.GetComponent<OrbitUniversal>();
                    if (orbit == null)
                    {
                        orbit =
                            instance.gameObject.AddComponent<OrbitUniversal>();
                    }

                    orbit.InitFromActiveNBody(
                        gravityBody,
                        request.OrbitCenter.GravityBody,
                        OrbitUniversal.EvolveMode.KEPLERS_EQN);
                }
            }
            catch (Exception exception)
            {
                gravityEngine.RemoveBody(
                    instance.gameObject);
                Destroy(
                    instance.gameObject);
                return RecordSpawnFailure(
                    $"Gravity Engine rejected the spawned celestial body: {exception.Message}");
            }

            motionProvider.Initialize(
                universeFrame,
                gravityBody);

            if (!instance.InitializePackage(
                    request,
                    universeFrame,
                    gravityBody,
                    motionProvider,
                    hierarchy.MotionRoot,
                    hierarchy.VisualRoot,
                    hierarchy.SurfaceRoot,
                    hierarchy.OceanRoot,
                    hierarchy.DevelopmentRoot))
            {
                var initializationError =
                    instance.LastError;

                gravityEngine.RemoveBody(
                    instance.gameObject);
                Destroy(
                    instance.gameObject);
                return RecordSpawnFailure(
                    string.IsNullOrWhiteSpace(
                        initializationError)
                        ? "The celestial body runtime package failed to initialize."
                        : initializationError);
            }

            var surfaceRuntime =
                hierarchy.SurfaceRoot.GetComponent<
                    CelestialSurfaceRuntime>();

            if (surfaceRuntime == null)
            {
                surfaceRuntime =
                    hierarchy.SurfaceRoot.gameObject.AddComponent<
                        CelestialSurfaceRuntime>();
            }

            if (!surfaceRuntime.Initialize(
                    instance))
            {
                var surfaceError =
                    surfaceRuntime.LastError;

                gravityEngine.RemoveBody(
                    instance.gameObject);
                Destroy(
                    instance.gameObject);
                return RecordSpawnFailure(
                    string.IsNullOrWhiteSpace(
                        surfaceError)
                        ? "The celestial surface foundation failed to initialize."
                        : surfaceError);
            }

            var foundationDiagnostics =
                hierarchy.DevelopmentRoot.GetComponent<
                    CelestialSurfaceFoundationDiagnostics>();

            if (foundationDiagnostics == null)
            {
                foundationDiagnostics =
                    hierarchy.DevelopmentRoot.gameObject.AddComponent<
                        CelestialSurfaceFoundationDiagnostics>();
            }

            instance.AttachSurfaceFoundationDiagnostics(
                foundationDiagnostics);
            foundationDiagnostics.Initialize(
                surfaceRuntime);

            var patchGenerator =
                hierarchy.SurfaceRoot.GetComponent<
                    CelestialSurfacePatchGenerator>();

            if (patchGenerator == null)
            {
                patchGenerator =
                    hierarchy.SurfaceRoot.gameObject.AddComponent<
                        CelestialSurfacePatchGenerator>();
            }

            var generationMargins =
                surfaceRuntime.QualityProfile != null
                    ? surfaceRuntime.QualityProfile
                        .AdaptiveGenerationMargins
                    : 2;
            var prewarmCoarseSurface =
                surfaceRuntime.QualityProfile == null ||
                surfaceRuntime.QualityProfile
                    .AdaptivePrewarmCoarseSurface;
            surfaceCacheManager =
                CelestialSurfaceCacheManager
                    .ResolveOrCreate(
                        surfaceCacheManager);
            var generatorReady =
                patchGenerator.Initialize(
                    surfaceRuntime,
                    surfaceCacheManager,
                    generationMargins,
                    prewarmCoarseSurface);
            var adaptiveRenderer =
                hierarchy.SurfaceRoot.GetComponent<
                    CelestialSurfaceQuadtreeRenderer>();

            if (adaptiveRenderer == null)
            {
                adaptiveRenderer =
                    hierarchy.SurfaceRoot.gameObject.AddComponent<
                        CelestialSurfaceQuadtreeRenderer>();
            }

            if (generatorReady)
            {
                adaptiveRenderer.Initialize(
                    surfaceRuntime,
                    patchGenerator,
                    adaptiveSurfaceRenderMode,
                    adaptiveSurfaceObserver);
            }

            surfaceRuntime.AttachAdaptivePipeline(
                patchGenerator,
                adaptiveRenderer);

            var collisionRuntime = hierarchy.SurfaceRoot.GetComponent<CelestialSurfaceCollisionRuntime>();
            if (collisionRuntime == null)
                collisionRuntime = hierarchy.SurfaceRoot.gameObject.AddComponent<CelestialSurfaceCollisionRuntime>();
            if (generatorReady)
                collisionRuntime.Initialize(surfaceRuntime);

            spawnedBodies.Add(
                request.InstanceId,
                instance);
            instance.Destroying +=
                HandleSpawnedBodyDestroying;
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

        public bool TryGetSpawnedBody(
            string instanceId,
            out CelestialBodyRuntimeContext body)
        {
            if (string.IsNullOrWhiteSpace(
                    instanceId))
            {
                body = null;
                return false;
            }

            return spawnedBodies.TryGetValue(
                instanceId,
                out body) &&
                body != null;
        }

        public bool TryDespawnBody(
            string instanceId)
        {
            if (!TryGetSpawnedBody(
                    instanceId,
                    out var body))
            {
                return false;
            }

            body.BeginDespawn();

            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine != null &&
                gravityEngine.IsSetup() &&
                body.GravityBody != null)
            {
                gravityEngine.RemoveBody(
                    body.gameObject);
            }

            UnregisterSpawnedBody(
                body,
                true);
            Destroy(
                body.gameObject);
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

            var request =
                new CelestialBodySpawnRequest(
                    startupInstanceId,
                    startupDefinition,
                    startupPositionMetersFromFrameOrigin,
                    startupVelocityMetersPerSecond,
                    Quaternion.Euler(
                        startupRotationEulerDegrees),
                    startupQualityProfile);

            lastSpawnSucceeded =
                TrySpawnBody(
                    request,
                    out _);
        }

        private bool TryValidateSpawn(
            CelestialBodySpawnRequest request,
            out double positionMetersPerPhysicsUnit,
            out double velocityMetersPerSecondPerPhysicsUnit)
        {
            positionMetersPerPhysicsUnit = default;
            velocityMetersPerSecondPerPhysicsUnit = default;

            if (universeFrame == null)
            {
                return RecordSpawnFailure(
                    "The celestial body factory requires a universe frame controller.");
            }

            if (bodyPrefab == null)
            {
                return RecordSpawnFailure(
                    "The celestial body factory requires a body prefab.");
            }

            if (request == null)
            {
                return RecordSpawnFailure(
                    "The celestial body factory requires a spawn request.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.InstanceId))
            {
                return RecordSpawnFailure(
                    "A spawned celestial body requires a unique instance ID.");
            }

            if (HasExistingInstanceId(
                    request.InstanceId))
            {
                return RecordSpawnFailure(
                    $"A celestial body with instance ID '{request.InstanceId}' already exists.");
            }

            if (request.Definition == null)
            {
                return RecordSpawnFailure(
                    "A spawned celestial body requires a body definition.");
            }

            if (!request.Definition.HasValidPhysicalSettings)
            {
                return RecordSpawnFailure(
                    "The body definition has invalid physical settings.");
            }

            if (!request.Definition.HasValidResolvedSurfaceSettings)
            {
                return RecordSpawnFailure(
                    "The body definition does not have valid settings for its resolved surface system.");
            }

            if (request.QualityProfile != null &&
                !request.QualityProfile.HasValidSettings)
            {
                return RecordSpawnFailure(
                    "The requested surface quality profile has invalid settings.");
            }

            if (!IsFinite(
                    request.InitialPositionMetersFromFrameOrigin) ||
                !IsFinite(
                    request.InitialVelocityMetersPerSecond) ||
                !IsFinite(
                    request.InitialAngularVelocityRadiansPerSecond) ||
                !IsFinite(
                    request.InitialRotation))
            {
                return RecordSpawnFailure(
                    "The spawned body's starting position, velocity, and rotation must contain finite values.");
            }

            if (request.MotionMode ==
                CelestialBodySpawnMode.OnRails &&
                (request.OrbitCenter == null ||
                    request.OrbitCenter.GravityBody == null ||
                    request.OrbitCenter.Definition == null))
            {
                return RecordSpawnFailure(
                    "An on-rails celestial body requires an active orbit-center body.");
            }

            RefreshMotionBackendState();

            if (gravityEngine == null)
            {
                return RecordSpawnFailure(
                    "Cannot spawn a celestial body because no Gravity Engine exists in the scene.");
            }

            if (!gravityEngine.IsSetup())
            {
                return RecordSpawnFailure(
                    "Cannot spawn a celestial body before Gravity Engine has completed setup.");
            }

            if (gravityEngine.units !=
                GravityScaler.Units.SI)
            {
                return RecordSpawnFailure(
                    "Definition-driven body spawning currently requires Gravity Engine to use SI units.");
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
                return RecordSpawnFailure(
                    "Gravity Engine returned invalid SI position or velocity scaling.");
            }

            return true;
        }

        private RuntimeHierarchy EnsureRuntimeHierarchy(
            CelestialBodyRuntimeContext body)
        {
            var bodyRoot =
                body.transform;
            var motionRoot =
                GetOrCreateDirectChild(
                    bodyRoot,
                    "Motion");
            var visualRoot =
                body.VisualRoot != null
                    ? body.VisualRoot
                    : GetOrCreateDirectChild(
                        bodyRoot,
                        "Visuals");
            var surfaceRoot =
                GetOrCreateDirectChild(
                    visualRoot,
                    "Surface");
            var oceanRoot =
                GetOrCreateDirectChild(
                    visualRoot,
                    "Ocean");
            var developmentRoot =
                GetOrCreateDirectChild(
                    bodyRoot,
                    "Development");

            return new RuntimeHierarchy(
                motionRoot,
                visualRoot,
                surfaceRoot,
                oceanRoot,
                developmentRoot);
        }

        private static Transform GetOrCreateDirectChild(
            Transform parent,
            string childName)
        {
            for (var index = 0;
                index < parent.childCount;
                index++)
            {
                var child =
                    parent.GetChild(
                        index);

                if (string.Equals(
                        child.name,
                        childName,
                        StringComparison.Ordinal))
                {
                    return child;
                }
            }

            var childObject =
                new GameObject(
                    childName);
            var childTransform =
                childObject.transform;

            childTransform.SetParent(
                parent,
                false);
            return childTransform;
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

        private void HandleSpawnedBodyDestroying(
            CelestialBodyRuntimeContext body)
        {
            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine != null &&
                gravityEngine.IsSetup() &&
                body != null &&
                body.GravityBody != null)
            {
                gravityEngine.RemoveBody(
                    body.gameObject);
            }

            UnregisterSpawnedBody(
                body,
                true);
        }

        private void UnregisterSpawnedBody(
            CelestialBodyRuntimeContext body,
            bool notify)
        {
            if (body == null ||
                string.IsNullOrWhiteSpace(
                    body.InstanceId) ||
                !spawnedBodies.Remove(
                    body.InstanceId))
            {
                return;
            }

            body.Destroying -=
                HandleSpawnedBodyDestroying;
            activeBodyCount =
                spawnedBodies.Count;

            if (lastSpawnedBody ==
                body)
            {
                lastSpawnedBody = null;
            }

            if (notify)
            {
                BodyDespawned?.Invoke(
                    body);
            }
        }

        private void RefreshMotionBackendState()
        {
            gravityEngine ??=
                GravityEngine.Instance();
            motionBackendReady =
                gravityEngine != null &&
                gravityEngine.IsSetup();
        }

        private bool RecordSpawnFailure(
            string error)
        {
            lastSpawnSucceeded = false;
            lastError = error;

            Debug.LogError(
                error,
                this);
            BodySpawnFailed?.Invoke(
                error);
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
