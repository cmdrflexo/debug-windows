/*
 * Owns bounded local collider patches and gameplay query requests over the body's shared MapMagic cache.
 * Collider meshes are immutable between cache revisions; GE motion is applied through kinematic bodies.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(295)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceCollisionRuntime : MonoBehaviour
    {
        private sealed class Patch
        {
            public CubeSpherePatchAddress Address;
            public DoubleVector3 Reference;
            public GameObject Object;
            public Mesh Mesh;
            public MeshCollider Collider;
            public Rigidbody Rigidbody;
            public float LastRequiredTime;
            public float MinimumElevationMeters;
            public float MaximumElevationMeters;
        }

        private struct Interest
        {
            public DoubleVector3 Position;
            public double Radius;
            public double Priority;
        }

        [SerializeField] private CelestialSurfaceRuntime surfaceRuntime;
        [Header("Local Collision")]
        [SerializeField] private bool collisionEnabled = true;
        [SerializeField] private bool followObserverCamera = true;
        [SerializeField, Min(1.0f)] private float coverageRadiusMeters = 256.0f;
        [SerializeField, Min(0.0f)] private float prefetchMarginMeters = 128.0f;
        [SerializeField, Min(0.1f)] private float targetSampleSpacingMeters = 8.0f;
        [SerializeField, Min(0.0f)] private float activationAltitudeMeters = 2500.0f;
        [SerializeField, Range(4, 512)] private int maximumPatchCount = 128;
        [SerializeField, Range(1, 8)] private int maximumMeshBuildsPerFrame = 2;
        [SerializeField, Min(0.0f)] private float retirementDelaySeconds = 1.0f;
        [SerializeField, Range(0, 31)] private int collisionLayer;
        [Header("Runtime")]
        [SerializeField] private bool initialized;
        [SerializeField] private bool coverageReady;
        [SerializeField] private bool budgetExceeded;
        [SerializeField] private int observerCount;
        [SerializeField] private int requiredPatchCount;
        [SerializeField] private int activePatchCount;
        [SerializeField] private int pendingPatchCount;
        [SerializeField] private int builtPatchCount;
        [SerializeField] private int collisionLevel;
        [SerializeField] private int originShiftCount;
        [SerializeField] private bool queryAvailable;
        [SerializeField] private bool colliderProbeHit;
        [SerializeField] private double colliderQueryErrorMeters;
        [SerializeField] private string lastError;

        private CelestialSurfaceCacheManager cache;
        private UniverseFrameController frame;
        private int clientId;
        private int cacheVersion;
        private GameObject physicsRoot;
        private readonly Dictionary<CubeSpherePatchAddress, Patch> patches = new Dictionary<CubeSpherePatchAddress, Patch>();
        private readonly Stack<Patch> pool = new Stack<Patch>();
        private readonly List<Interest> interests = new List<Interest>();
        private readonly HashSet<CubeSpherePatchAddress> required = new HashSet<CubeSpherePatchAddress>();
        private readonly HashSet<CubeSpherePatchAddress> desired = new HashSet<CubeSpherePatchAddress>();
        private readonly HashSet<CubeSpherePatchAddress> visualAncestors = new HashSet<CubeSpherePatchAddress>();
        private readonly HashSet<CubeSpherePatchAddress> observerFootprint = new HashSet<CubeSpherePatchAddress>();
        private readonly List<CubeSpherePatchAddress> ordered = new List<CubeSpherePatchAddress>();
        private readonly List<CubeSpherePatchAddress> removals = new List<CubeSpherePatchAddress>();
        private readonly Dictionary<CubeSpherePatchAddress, float> queryRequests = new Dictionary<CubeSpherePatchAddress, float>();
        private CelestialSurfaceSample lastSample;
        private CelestialBodyMotionState previousMotion;
        private CelestialBodyMotionState physicsMotion;
        private bool hasPhysicsMotion;
        private bool hasPreviousMotion;
        private CelestialSurfaceDropTestBody dropTestBody;
        private readonly CelestialSurfaceCollisionDiagnostics geometryDiagnostics = new CelestialSurfaceCollisionDiagnostics();

        public CelestialSurfaceRuntime SurfaceRuntime => surfaceRuntime;
        public bool Initialized => initialized;
        public bool CollisionEnabled => collisionEnabled;
        public bool CoverageReady => coverageReady;
        public bool BudgetExceeded => budgetExceeded;
        public int ObserverCount => observerCount;
        public int RequiredPatchCount => requiredPatchCount;
        public int ActivePatchCount => activePatchCount;
        public int PendingPatchCount => pendingPatchCount;
        public int PooledPatchCount => pool.Count;
        public int BuiltPatchCount => builtPatchCount;
        public int CollisionLevel => collisionLevel;
        public int OriginShiftCount => originShiftCount;
        public int QueryRequestCount => queryRequests.Count;
        public bool QueryAvailable => queryAvailable;
        public CelestialSurfaceSample LastSample => lastSample;
        public bool ColliderProbeHit => colliderProbeHit;
        public double ColliderQueryErrorMeters => colliderQueryErrorMeters;
        public string LastError => lastError;
        public bool DropTestActive => dropTestBody != null;
        public bool DropTestTouchingTerrain => dropTestBody != null && dropTestBody.TouchingTerrain;
        public CelestialSurfaceCollisionDiagnostics GeometryDiagnostics => geometryDiagnostics;

        public bool Initialize(CelestialSurfaceRuntime runtime)
        {
            Unregister();
            surfaceRuntime = runtime;
            if (surfaceRuntime == null) { lastError = "A surface runtime is required."; return false; }
            ApplyQuality();
            surfaceRuntime.AttachCollisionRuntime(this);
            ValidateCollisionGeometry();
            return Register();
        }

        [ContextMenu("Validate Collision Geometry")]
        private void ValidateCollisionGeometry()
        {
            if (surfaceRuntime != null && surfaceRuntime.FoundationReady) geometryDiagnostics.Run(surfaceRuntime);
        }

        public void SetCollisionEnabled(bool value)
        {
            collisionEnabled = value;
            if (!value) ClearCollision();
        }

        public bool RequestSample(DoubleVector3 position, int level)
        {
            if (!initialized || level < surfaceRuntime.MinimumLevel || level > surfaceRuntime.MaximumLevel ||
                !CubeSphereMapping.TryDirectionToAddress(position, 0.0, out var address) ||
                !CubeSpherePatchAddress.TryFromAddress(address, level, out var patch)) return false;
            if (!queryRequests.ContainsKey(patch) && queryRequests.Count >= 256) return false;
            queryRequests[patch] = Time.unscaledTime + 1.0f;
            return true;
        }

        public bool HasCoverageAt(Vector3 scenePosition)
        {
            return initialized && surfaceRuntime.TrySceneToBodyLocal(scenePosition, out var local) &&
                HasCoverageAtLocal(local);
        }

        public bool HasCoverageFor(CelestialSurfaceCollisionObserver observer)
        {
            if (!initialized || observer == null || !observer.isActiveAndEnabled || !Accepts(observer) ||
                !surfaceRuntime.TrySceneToBodyLocal(observer.transform.position, out var local)) return false;
            observerFootprint.Clear();
            var radius = observer.CoverageRadiusMeters > 0.0f ? observer.CoverageRadiusMeters : coverageRadiusMeters;
            return CelestialSurfaceCollisionCoverage.Collect(local, Radius, radius, collisionLevel,
                observerFootprint, maximumPatchCount) && IsFootprintReady(observerFootprint);
        }

        internal bool RequiresVisualRefinement(CubeSpherePatchAddress address) =>
            initialized && collisionEnabled && visualAncestors.Contains(address);

        internal bool OwnsCollider(Collider collider)
        {
            foreach (var patch in patches.Values)
                if (patch.Collider == collider) return true;
            return false;
        }

        private double Radius => surfaceRuntime.BodyDefinition.ReferenceRadiusMeters;

        private bool Register()
        {
            if (surfaceRuntime == null || !surfaceRuntime.FoundationReady ||
                surfaceRuntime.PatchGenerator == null || !surfaceRuntime.PatchGenerator.Initialized) return false;
            cache = surfaceRuntime.PatchGenerator.CacheManager;
            var quality = surfaceRuntime.QualityProfile;
            if (quality != null && !quality.HasValidAdaptiveCollisionSettings)
            {
                lastError = "The adaptive collision quality settings are invalid.";
                return false;
            }
            initialized = cache != null && cache.RegisterClient(this, surfaceRuntime,
                quality != null ? quality.AdaptiveGenerationMargins : 2, false, out clientId, out lastError);
            if (!initialized) return false;
            cacheVersion = cache.GetSurfaceVersion(clientId);
            collisionLevel = CelestialSurfaceCollisionCoverage.ResolveLevel(Radius, surfaceRuntime.PatchResolution,
                targetSampleSpacingMeters, surfaceRuntime.MinimumLevel, surfaceRuntime.LodPolicy.CollisionMaximumLevel);
            frame = surfaceRuntime.Body.UniverseFrame;
            if (frame != null) frame.OriginShifted += HandleOriginShifted;
            return true;
        }

        private void LateUpdate()
        {
            if ((!initialized && !Register()) || cache == null) return;
            var requestedLevel = CelestialSurfaceCollisionCoverage.ResolveLevel(Radius, surfaceRuntime.PatchResolution,
                targetSampleSpacingMeters, surfaceRuntime.MinimumLevel, surfaceRuntime.LodPolicy.CollisionMaximumLevel);
            if (cacheVersion != cache.GetSurfaceVersion(clientId) || requestedLevel != collisionLevel)
            {
                ClearCollision();
                cacheVersion = cache.GetSurfaceVersion(clientId);
                collisionLevel = requestedLevel;
            }
            if (!cache.BeginRequestFrame(clientId)) return;
            try
            {
                CollectInterests();
                BuildFootprint();
                SubmitQueryRequests();
                ordered.Clear();
                ordered.AddRange(desired);
                ordered.Sort(ComparePriority);
                var builds = 0;
                foreach (var address in ordered)
                {
                    cache.RequestPatch(clientId, address, Priority(address),
                        required.Contains(address) ? CelestialSurfacePatchRequestClass.Collision : CelestialSurfacePatchRequestClass.Prefetch);
                    if (patches.TryGetValue(address, out var patch))
                        patch.LastRequiredTime = Time.unscaledTime;
                    else if (builds < maximumMeshBuildsPerFrame && patches.Count < maximumPatchCount * 2 &&
                        cache.TryGetPatchData(clientId, address, out var data))
                    {
                        BuildPatch(data);
                        builds++;
                    }
                }
                // Pin retained data as long as it backs a live collider.
                foreach (var patch in patches.Values)
                    cache.RequestPatch(clientId, patch.Address, -1.0, CelestialSurfacePatchRequestClass.Prefetch);
                requiredPatchCount = required.Count;
                pendingPatchCount = 0;
                foreach (var address in required)
                    if (!patches.TryGetValue(address, out var p) || !p.Collider.enabled) pendingPatchCount++;
                SetReadiness(!budgetExceeded && required.Count > 0 && pendingPatchCount == 0);
            }
            catch (Exception exception)
            {
                lastError = exception.GetBaseException().Message;
                SetReadiness(false);
            }
            finally { cache.EndRequestFrame(clientId); }
        }

        private void CollectInterests()
        {
            interests.Clear();
            queryAvailable = false;
            lastSample = default;
            budgetExceeded = false;
            lastError = string.Empty;
            var renderer = surfaceRuntime.AdaptiveRenderer;
            var camera = renderer != null ? renderer.ObserverCamera : null;
            if (camera == null) camera = Camera.main;
            if (collisionEnabled && followObserverCamera && camera != null && camera.isActiveAndEnabled &&
                renderer != null && renderer.isActiveAndEnabled && renderer.RenderMode != CelestialAdaptiveSurfaceRenderMode.Hidden)
                AddInterest(camera.transform.position, coverageRadiusMeters, 1.0);
            if (collisionEnabled)
                foreach (var observer in CelestialSurfaceCollisionObserver.ActiveObservers)
                    if (observer != null && Accepts(observer))
                        AddInterest(observer.transform.position,
                            observer.CoverageRadiusMeters > 0.0f ? observer.CoverageRadiusMeters : coverageRadiusMeters, observer.Priority);
            observerCount = interests.Count;
        }

        private bool Accepts(CelestialSurfaceCollisionObserver observer) =>
            (observer.Body == null || observer.Body == surfaceRuntime.Body) &&
            (observer.UniverseFrame == null || observer.UniverseFrame == frame) &&
            observer.gameObject.scene == gameObject.scene;

        private void AddInterest(Vector3 scenePosition, double radius, double priority)
        {
            if (!CelestialSurfaceGeometry.IsFinite(radius) || radius <= 0.0 ||
                !CelestialSurfaceGeometry.IsFinite(priority) || priority < 0.0)
            {
                budgetExceeded = true;
                return;
            }
            if (!surfaceRuntime.TrySceneToBodyLocal(scenePosition, out var local) || local.Magnitude <= 0.0) return;
            var altitude = local.Magnitude - Radius;
            if (surfaceRuntime.TrySampleBodyLocal(local, collisionLevel, out var sample))
            {
                altitude = sample.AltitudeMeters;
                if (!queryAvailable) { queryAvailable = true; lastSample = sample; }
            }
            if (Math.Abs(altitude) > activationAltitudeMeters) return;
            if (interests.Count >= 16) { budgetExceeded = true; return; }
            interests.Add(new Interest { Position = local, Radius = radius, Priority = priority });
        }

        private void BuildFootprint()
        {
            required.Clear();
            desired.Clear();
            visualAncestors.Clear();
            foreach (var interest in interests)
                if (!CelestialSurfaceCollisionCoverage.Collect(interest.Position, Radius, interest.Radius,
                        collisionLevel, required, maximumPatchCount))
                    budgetExceeded = true;
            desired.UnionWith(required);
            foreach (var interest in interests)
                CelestialSurfaceCollisionCoverage.Collect(interest.Position, Radius,
                    interest.Radius + prefetchMarginMeters, collisionLevel, desired, maximumPatchCount);
            foreach (var patch in required)
            {
                var ancestor = patch;
                while (ancestor.TryGetParent(out var parent))
                {
                    visualAncestors.Add(parent);
                    ancestor = parent;
                }
            }
            if (budgetExceeded) lastError = "Collision footprint exceeds its budget; increase the patch budget or reduce coverage/detail.";
        }

        private void SubmitQueryRequests()
        {
            removals.Clear();
            foreach (var pair in queryRequests)
                if (pair.Value < Time.unscaledTime) removals.Add(pair.Key);
                else cache.RequestPatch(clientId, pair.Key, 0.0, CelestialSurfacePatchRequestClass.Prefetch);
            foreach (var address in removals) queryRequests.Remove(address);
        }

        private double Priority(CubeSpherePatchAddress address)
        {
            var center = CelestialSurfaceGeometry.ReferencePosition(address, Radius);
            var best = double.NegativeInfinity;
            foreach (var interest in interests)
                best = Math.Max(best, interest.Priority * 1000000.0 - (interest.Position - center).Magnitude);
            return CelestialSurfaceGeometry.IsFinite(best) ? best : -1.0;
        }

        private int ComparePriority(CubeSpherePatchAddress a, CubeSpherePatchAddress b)
        {
            var requiredOrder = required.Contains(b).CompareTo(required.Contains(a));
            if (requiredOrder != 0) return requiredOrder;
            var order = Priority(b).CompareTo(Priority(a));
            if (order != 0) return order;
            order = ((int)a.Face).CompareTo((int)b.Face);
            if (order != 0) return order;
            order = a.Y.CompareTo(b.Y);
            return order != 0 ? order : a.X.CompareTo(b.X);
        }

        private void BuildPatch(CelestialSurfacePatchData data)
        {
            var patch = pool.Count > 0 ? pool.Pop() : CreatePatch();
            try
            {
                patch.Address = data.Address;
                patch.Reference = CelestialSurfaceGeometry.ReferencePosition(data.Address, Radius);
                patch.LastRequiredTime = Time.unscaledTime;
                patch.Collider.enabled = false;
                patch.Collider.sharedMesh = null;
                patch.Mesh.Clear();
                patch.Mesh.indexFormat = data.SampleCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                var vertices = CelestialSurfaceGeometry.BuildVertices(data, Radius);
                patch.Mesh.vertices = vertices;
                patch.Mesh.triangles = CelestialSurfaceGeometry.BuildOutwardTriangles(
                    data.Resolution, vertices, patch.Reference);
                patch.Mesh.RecalculateBounds();
                // Cooking is limited by maximumMeshBuildsPerFrame; meshes are never cooked during a morph.
                Physics.BakeMesh(patch.Mesh.GetInstanceID(), false, patch.Collider.cookingOptions);
                patch.Collider.sharedMesh = patch.Mesh;
                patch.Object.name = $"Collision {data.Address}";
                patch.Object.layer = collisionLayer;
                patch.MinimumElevationMeters = data.MinimumElevationMeters;
                patch.MaximumElevationMeters = data.MaximumElevationMeters;
                patches.Add(data.Address, patch);
                builtPatchCount++;
            }
            catch
            {
                Recycle(patch);
                throw;
            }
        }

        private Patch CreatePatch()
        {
            if (physicsRoot == null)
            {
                physicsRoot = new GameObject($"Celestial Collision ({surfaceRuntime.Body.InstanceId})");
                SceneManager.MoveGameObjectToScene(physicsRoot, gameObject.scene);
            }
            var go = new GameObject("Collision Patch");
            go.transform.SetParent(physicsRoot.transform, false);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var collider = go.AddComponent<MeshCollider>();
            collider.enabled = false;
            collider.convex = false;
            go.SetActive(false);
            return new Patch { Object = go, Rigidbody = rb, Collider = collider,
                Mesh = new Mesh { name = "Celestial Collision Mesh", hideFlags = HideFlags.HideAndDontSave } };
        }

        private void FixedUpdate()
        {
            if (!initialized) return;
            if (cache == null || cacheVersion != cache.GetSurfaceVersion(clientId))
            {
                ClearCollision();
                return;
            }
            if (surfaceRuntime.Body.TryGetMotionState(out var motion))
            {
                previousMotion = physicsMotion;
                hasPreviousMotion = hasPhysicsMotion;
                physicsMotion = motion;
                hasPhysicsMotion = true;
            }
            if (!surfaceRuntime.TryGetScenePose(out var center, out var rotation))
            {
                foreach (var patch in patches.Values) patch.Collider.enabled = false;
                SetReadiness(false);
                return;
            }
            // Probe the settled physics pose before scheduling this tick's kinematic movement.
            // A MovePosition target is not necessarily the pose currently used by PhysX queries.
            UpdateProbe();
            removals.Clear();
            activePatchCount = 0;
            foreach (var pair in patches)
            {
                var patch = pair.Value;
                if (!desired.Contains(pair.Key) && Time.unscaledTime - patch.LastRequiredTime >= retirementDelaySeconds)
                {
                    removals.Add(pair.Key);
                    continue;
                }
                var position = CelestialSurfaceGeometry.ToVector3(center +
                    CelestialSurfaceGeometry.Rotate(patch.Reference, rotation));
                if (!patch.Collider.enabled)
                {
                    patch.Rigidbody.position = position;
                    patch.Rigidbody.rotation = rotation;
                    patch.Object.SetActive(true);
                    patch.Collider.enabled = true;
                }
                else
                {
                    patch.Rigidbody.MovePosition(position);
                    patch.Rigidbody.MoveRotation(rotation);
                }
                activePatchCount++;
            }
            foreach (var address in removals)
            {
                Recycle(patches[address]);
                patches.Remove(address);
            }
            SetReadiness(!budgetExceeded && required.Count > 0 && IsFootprintReady(required));
        }

        [ContextMenu("Drop Test Sphere")]
        private void DropTestSphere()
        {
            if (!Application.isPlaying || !coverageReady || !queryAvailable || !lastSample.IsRequestedDetail ||
                !hasPreviousMotion)
            {
                lastError = "Approach the surface and wait for collision coverage before dropping the test sphere.";
                return;
            }
            var sample = lastSample;
            var camera = surfaceRuntime.AdaptiveRenderer.ObserverCamera;
            if (camera != null)
            {
                var ahead = camera.transform.position + camera.transform.forward * 15.0f;
                if (surfaceRuntime.TrySampleScene(ahead, collisionLevel, out var forwardSample, false) &&
                    HasCoverageAt(ahead)) sample = forwardSample;
            }
            var up = sample.BodyPositionMeters / sample.BodyPositionMeters.Magnitude;
            var local = sample.BodyPositionMeters + up * 5.0;
            if (!surfaceRuntime.TryBodyLocalToScene(local, out var point)) return;
            var previousPoint = CelestialSurfaceGeometry.Add(previousMotion.Position,
                CelestialSurfaceGeometry.Rotate(local, previousMotion.Rotation));
            var currentPoint = CelestialSurfaceGeometry.Add(physicsMotion.Position,
                CelestialSurfaceGeometry.Rotate(local, physicsMotion.Rotation));
            var velocity = CelestialSurfaceGeometry.ToVector3(
                CelestialSurfaceGeometry.Difference(currentPoint, previousPoint) / Time.fixedDeltaTime);
            if (dropTestBody != null) Destroy(dropTestBody.gameObject);
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Celestial Surface Drop Test";
            SceneManager.MoveGameObjectToScene(go, gameObject.scene);
            go.transform.position = point;
            go.transform.localScale = Vector3.one * 2.0f;
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            var observer = go.AddComponent<CelestialSurfaceCollisionObserver>();
            observer.SetBody(surfaceRuntime.Body);
            dropTestBody = go.AddComponent<CelestialSurfaceDropTestBody>();
            dropTestBody.Initialize(surfaceRuntime);
        }

        private bool IsFootprintReady(HashSet<CubeSpherePatchAddress> footprint)
        {
            if (!initialized || !collisionEnabled || footprint.Count == 0 || cache == null ||
                cacheVersion != cache.GetSurfaceVersion(clientId)) return false;
            foreach (var address in footprint)
                if (!patches.TryGetValue(address, out var patch) || !patch.Collider.enabled) return false;
            return true;
        }

        private bool HasCoverageAtLocal(DoubleVector3 position)
        {
            return collisionEnabled && cache != null && cacheVersion == cache.GetSurfaceVersion(clientId) &&
                CubeSphereMapping.TryDirectionToAddress(position, 0.0, out var address) &&
                CubeSpherePatchAddress.TryFromAddress(address, collisionLevel, out var key) &&
                patches.TryGetValue(key, out var patch) && patch.Collider.enabled;
        }

        private void UpdateProbe()
        {
            colliderProbeHit = false;
            colliderQueryErrorMeters = 0.0;
            if (!queryAvailable || lastSample.ResolvedLevel != collisionLevel ||
                !patches.TryGetValue(lastSample.Patch, out var patch) || !patch.Collider.enabled) return;
            // Compare against this collider's current physics pose, independent of render-frame GE movement.
            var direction = patch.Rigidbody.rotation * CelestialSurfaceGeometry.ToVector3(
                lastSample.BodyPositionMeters / lastSample.BodyPositionMeters.Magnitude);
            var expected = patch.Rigidbody.position + patch.Rigidbody.rotation *
                CelestialSurfaceGeometry.ToVector3(lastSample.BodyPositionMeters - patch.Reference);
            var relief = Math.Max(0.0,
                patch.MaximumElevationMeters - patch.MinimumElevationMeters);
            var clearance = (float)Math.Max(100.0, relief + 100.0);
            var ray = new Ray(expected + direction * clearance, -direction);
            var previousBackfaceSetting = Physics.queriesHitBackfaces;
            RaycastHit hit;
            try
            {
                // Diagnostic queries should not depend on triangle facing under unusually steep relief.
                Physics.queriesHitBackfaces = true;
                if (!patch.Collider.Raycast(ray, out hit, clearance * 2.0f)) return;
            }
            finally { Physics.queriesHitBackfaces = previousBackfaceSetting; }
            colliderProbeHit = true;
            colliderQueryErrorMeters = Vector3.Distance(hit.point, expected);
        }

        private void HandleOriginShifted(UniversePosition origin, Vector3 delta)
        {
            foreach (var patch in patches.Values)
                patch.Rigidbody.position += delta;
            originShiftCount++;
            // Teleport physics poses on rebases. MovePosition would turn the shift into a large velocity.
            Physics.SyncTransforms();
        }

        private void SetReadiness(bool ready)
        {
            coverageReady = ready;
            if (surfaceRuntime == null || surfaceRuntime.Body == null) return;
            var renderer = surfaceRuntime.AdaptiveRenderer;
            surfaceRuntime.Body.ReportSurfaceReadiness(
                surfaceRuntime.PatchGenerator != null && surfaceRuntime.PatchGenerator.AreRootsReady(),
                renderer != null && renderer.VisibleSurfaceReady, ready);
        }

        private void Recycle(Patch patch)
        {
            patch.Collider.enabled = false;
            patch.Collider.sharedMesh = null;
            patch.Object.SetActive(false);
            patch.Mesh.Clear();
            if (pool.Count < maximumPatchCount) pool.Push(patch);
            else { Destroy(patch.Mesh); Destroy(patch.Object); }
        }

        private void ClearCollision()
        {
            foreach (var patch in patches.Values) Recycle(patch);
            patches.Clear();
            required.Clear();
            desired.Clear();
            visualAncestors.Clear();
            requiredPatchCount = activePatchCount = pendingPatchCount = 0;
            colliderProbeHit = queryAvailable = false;
            SetReadiness(false);
        }

        private void ApplyQuality()
        {
            var q = surfaceRuntime != null ? surfaceRuntime.QualityProfile : null;
            if (q == null) return;
            collisionEnabled = q.AdaptiveCollisionEnabled;
            followObserverCamera = q.AdaptiveCollisionFollowsCamera;
            coverageRadiusMeters = q.AdaptiveCollisionCoverageRadiusMeters;
            prefetchMarginMeters = q.AdaptiveCollisionPrefetchMarginMeters;
            targetSampleSpacingMeters = q.AdaptiveCollisionSampleSpacingMeters;
            activationAltitudeMeters = q.AdaptiveCollisionActivationAltitudeMeters;
            maximumPatchCount = q.AdaptiveCollisionMaximumPatchCount;
            maximumMeshBuildsPerFrame = q.AdaptiveCollisionMeshBuildsPerFrame;
            retirementDelaySeconds = q.AdaptiveCollisionRetirementDelaySeconds;
            collisionLayer = q.AdaptiveCollisionLayer;
        }

        private void Unregister()
        {
            if (frame != null) frame.OriginShifted -= HandleOriginShifted;
            if (cache != null && clientId != 0) cache.UnregisterClient(clientId);
            initialized = false;
            clientId = 0;
            ClearCollision();
            queryRequests.Clear();
            hasPhysicsMotion = hasPreviousMotion = false;
            if (dropTestBody != null) Destroy(dropTestBody.gameObject);
        }

        private void OnEnable() { if (surfaceRuntime != null && !initialized) Register(); }
        private void OnDisable() { Unregister(); }
        private void OnDestroy()
        {
            Unregister();
            foreach (var patch in pool) { Destroy(patch.Mesh); Destroy(patch.Object); }
            pool.Clear();
            if (physicsRoot != null) Destroy(physicsRoot);
        }

        private void OnValidate()
        {
            coverageRadiusMeters = Mathf.Max(1.0f, coverageRadiusMeters);
            prefetchMarginMeters = Mathf.Max(0.0f, prefetchMarginMeters);
            targetSampleSpacingMeters = Mathf.Max(0.1f, targetSampleSpacingMeters);
            activationAltitudeMeters = Mathf.Max(0.0f, activationAltitudeMeters);
            maximumPatchCount = Mathf.Clamp(maximumPatchCount, 4, 512);
            maximumMeshBuildsPerFrame = Mathf.Clamp(maximumMeshBuildsPerFrame, 1, 8);
            retirementDelaySeconds = Mathf.Max(0.0f, retirementDelaySeconds);
            collisionLayer = Mathf.Clamp(collisionLayer, 0, 31);
        }
    }
}
