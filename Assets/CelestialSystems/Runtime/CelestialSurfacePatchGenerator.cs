/*
 * Represents one body's request handle into the global MapMagic surface cache and generation scheduler.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(285)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfacePatchGenerator :
        MonoBehaviour
    {
        [Header("Runtime Ownership")]
        [SerializeField]
        private CelestialSurfaceRuntime surfaceRuntime;

        [SerializeField]
        private CelestialSurfaceCacheManager cacheManager;

        [Header("Generation Request")]
        [SerializeField]
        [Range(0, 16)]
        private int margins = 2;

        [SerializeField]
        private bool requestBackgroundRoots = true;

        [Header("Runtime")]
        [SerializeField]
        private bool initialized;

        [SerializeField]
        private int clientId;

        [SerializeField]
        private bool sourceReady;

        [SerializeField]
        private bool isGenerating;

        [SerializeField]
        private CubeSpherePatchAddress activeAddress;

        [SerializeField]
        private int queuedPatchCount;

        [SerializeField]
        private int cachedPatchCount;

        [SerializeField]
        private int failedPatchCount;

        [SerializeField]
        private int completedPatchCount;

        [SerializeField]
        private int terrainLayerCount;

        [SerializeField]
        private int cacheVersion;

        [SerializeField]
        private double lastGenerationMilliseconds;

        [SerializeField]
        private string generationSourceName;

        [SerializeField]
        private string lastError;

        private bool requestFrameOpen;

        public CelestialSurfaceRuntime SurfaceRuntime =>
            surfaceRuntime;

        public CelestialSurfaceCacheManager CacheManager =>
            cacheManager;

        public bool Initialized =>
            initialized;

        public int ClientId =>
            clientId;

        public bool SourceReady =>
            sourceReady;

        public bool IsGenerating =>
            isGenerating;

        public CubeSpherePatchAddress ActiveAddress =>
            activeAddress;

        public string ActiveRequest =>
            isGenerating &&
            activeAddress.IsValid
                ? activeAddress.ToString()
                : "None";

        public int QueuedPatchCount =>
            queuedPatchCount;

        public int CachedPatchCount =>
            cachedPatchCount;

        public int FailedPatchCount =>
            failedPatchCount;

        public int CompletedPatchCount =>
            completedPatchCount;

        public int TerrainLayerCount =>
            terrainLayerCount;

        public int CacheVersion =>
            cacheVersion;

        public double LastGenerationMilliseconds =>
            lastGenerationMilliseconds;

        public string GenerationSourceName =>
            generationSourceName;

        public string CacheManagerName =>
            cacheManager != null
                ? cacheManager.name
                : "None";

        public string LastError =>
            lastError;

        public bool Initialize(
            CelestialSurfaceRuntime newSurfaceRuntime,
            int generationMargins)
        {
            return Initialize(
                newSurfaceRuntime,
                CelestialSurfaceCacheManager
                    .ResolveOrCreate(),
                generationMargins,
                true);
        }

        public bool Initialize(
            CelestialSurfaceRuntime newSurfaceRuntime,
            CelestialSurfaceCacheManager newCacheManager,
            int generationMargins,
            bool prewarmCoarseRoots)
        {
            Unregister();
            surfaceRuntime =
                newSurfaceRuntime;
            cacheManager =
                newCacheManager;
            margins =
                Mathf.Clamp(
                    generationMargins,
                    0,
                    16);
            requestBackgroundRoots =
                prewarmCoarseRoots;

            if (surfaceRuntime == null ||
                !surfaceRuntime.FoundationReady)
            {
                return Fail(
                    "The patch-cache client requires an initialized celestial surface runtime.");
            }

            if (cacheManager == null)
            {
                return Fail(
                    "The patch-cache client requires a global celestial surface cache manager.");
            }

            if (!cacheManager.RegisterClient(
                    this,
                    surfaceRuntime,
                    margins,
                    requestBackgroundRoots,
                    out clientId,
                    out var registrationError))
            {
                return Fail(
                    registrationError);
            }

            initialized = true;
            lastError = string.Empty;
            RefreshDiagnostics();
            return true;
        }

        public bool BeginRequestFrame()
        {
            if (!initialized ||
                cacheManager == null)
            {
                return false;
            }

            requestFrameOpen =
                cacheManager.BeginRequestFrame(
                    clientId);
            return requestFrameOpen;
        }

        public void EndRequestFrame()
        {
            if (!requestFrameOpen ||
                cacheManager == null)
            {
                return;
            }

            cacheManager.EndRequestFrame(
                clientId);
            requestFrameOpen = false;
            RefreshDiagnostics();
        }

        public bool RequestPatch(
            CubeSpherePatchAddress address,
            double priority)
        {
            return RequestPatch(
                address,
                priority,
                address.IsRoot
                    ? CelestialSurfacePatchRequestClass
                        .Coverage
                    : CelestialSurfacePatchRequestClass
                        .Visible);
        }

        public bool RequestPatch(
            CubeSpherePatchAddress address,
            double priority,
            CelestialSurfacePatchRequestClass requestClass)
        {
            return
                initialized &&
                cacheManager != null &&
                cacheManager.RequestPatch(
                    clientId,
                    address,
                    priority,
                    requestClass);
        }

        public CelestialSurfacePatchGenerationState GetPatchState(
            CubeSpherePatchAddress address)
        {
            return
                initialized &&
                cacheManager != null
                    ? cacheManager.GetPatchState(
                        clientId,
                        address)
                    : CelestialSurfacePatchGenerationState
                        .None;
        }

        public bool TryGetPatchData(
            CubeSpherePatchAddress address,
            out CelestialSurfacePatchData patchData)
        {
            if (!initialized ||
                cacheManager == null)
            {
                patchData = null;
                return false;
            }

            return cacheManager.TryGetPatchData(
                clientId,
                address,
                out patchData);
        }

        public bool AreRootsReady()
        {
            return
                initialized &&
                cacheManager != null &&
                cacheManager.AreRootsReady(
                    clientId);
        }

        public TerrainLayer GetTerrainLayer(
            int index)
        {
            if (!initialized ||
                cacheManager == null)
            {
                throw new InvalidOperationException(
                    "The patch-cache client is not initialized.");
            }

            return cacheManager.GetTerrainLayer(
                clientId,
                index);
        }

        [ContextMenu("Invalidate This Body's Shared Surface Cache")]
        private void InvalidateSharedCache()
        {
            if (surfaceRuntime == null ||
                cacheManager == null)
            {
                return;
            }

            cacheManager.InvalidateSurface(
                surfaceRuntime.CacheKey);
        }

        private void Update()
        {
            RefreshDiagnostics();
        }

        private void RefreshDiagnostics()
        {
            if (!initialized ||
                cacheManager == null)
            {
                return;
            }

            sourceReady =
                cacheManager.GetSourceReady(
                    clientId);
            isGenerating =
                cacheManager.GetIsGenerating(
                    clientId);
            activeAddress =
                cacheManager.GetActiveAddress(
                    clientId);
            queuedPatchCount =
                cacheManager.GetQueuedPatchCount(
                    clientId);
            cachedPatchCount =
                cacheManager.GetCachedPatchCount(
                    clientId);
            failedPatchCount =
                cacheManager.GetFailedPatchCount(
                    clientId);
            completedPatchCount =
                cacheManager.GetCompletedPatchCount(
                    clientId);
            terrainLayerCount =
                cacheManager.GetTerrainLayerCount(
                    clientId);
            cacheVersion =
                cacheManager.GetSurfaceVersion(
                    clientId);
            lastGenerationMilliseconds =
                cacheManager.GetLastGenerationMilliseconds(
                    clientId);
            generationSourceName =
                cacheManager.GetGenerationSourceName(
                    clientId);
            lastError =
                cacheManager.GetClientLastError(
                    clientId);
        }

        private bool Fail(
            string error)
        {
            initialized = false;
            clientId = 0;
            lastError = error;
            return false;
        }

        private void Unregister()
        {
            if (requestFrameOpen &&
                cacheManager != null)
            {
                cacheManager.EndRequestFrame(
                    clientId);
            }

            requestFrameOpen = false;

            if (clientId != 0 &&
                cacheManager != null)
            {
                cacheManager.UnregisterClient(
                    clientId);
            }

            initialized = false;
            clientId = 0;
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnEnable()
        {
            if (!initialized &&
                surfaceRuntime != null &&
                surfaceRuntime.FoundationReady &&
                cacheManager != null)
            {
                Initialize(
                    surfaceRuntime,
                    cacheManager,
                    margins,
                    requestBackgroundRoots);
            }
        }

        private void OnDestroy()
        {
            Unregister();
        }
    }
}
