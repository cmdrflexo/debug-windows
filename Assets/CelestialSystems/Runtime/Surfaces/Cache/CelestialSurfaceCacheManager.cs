/*
 * Shares prioritized MapMagic patch generation and immutable sampled data across every registered celestial body instance.
 */

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Den.Tools;
using Den.Tools.Matrices;
using MapMagic.Core;
using MapMagic.Nodes.MatrixGenerators;
using MapMagic.Products;
using MapMagic.Terrains;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(270)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceCacheManager :
        MonoBehaviour
    {
        private const int MaximumSurfaceLayerCount =
            4;
        private const double BackgroundRootPriority =
            1000000.0;

        private readonly struct SurfaceVariantKey :
            IEquatable<SurfaceVariantKey>
        {
            public CelestialSurfaceCacheKey CacheKey { get; }

            public int Resolution { get; }

            public int Margins { get; }

            public SurfaceVariantKey(
                CelestialSurfaceCacheKey cacheKey,
                int resolution,
                int margins)
            {
                CacheKey = cacheKey;
                Resolution = resolution;
                Margins = margins;
            }

            public bool Equals(
                SurfaceVariantKey other)
            {
                return
                    CacheKey.Equals(
                        other.CacheKey) &&
                    Resolution == other.Resolution &&
                    Margins == other.Margins;
            }

            public override bool Equals(
                object value)
            {
                return
                    value is SurfaceVariantKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = CacheKey.GetHashCode();
                    hash =
                        (hash * 397) ^
                        Resolution;
                    hash =
                        (hash * 397) ^
                        Margins;
                    return hash;
                }
            }

            public override string ToString()
            {
                return
                    $"{CacheKey} {Resolution}x{Resolution} m{Margins}";
            }
        }

        private readonly struct PatchKey :
            IEquatable<PatchKey>
        {
            public SurfaceVariantKey Surface { get; }

            public CubeSpherePatchAddress Address { get; }

            public PatchKey(
                SurfaceVariantKey surface,
                CubeSpherePatchAddress address)
            {
                Surface = surface;
                Address = address;
            }

            public bool Equals(
                PatchKey other)
            {
                return
                    Surface.Equals(
                        other.Surface) &&
                    Address.Equals(
                        other.Address);
            }

            public override bool Equals(
                object value)
            {
                return
                    value is PatchKey other &&
                    Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return
                        (Surface.GetHashCode() * 397) ^
                        Address.GetHashCode();
                }
            }
        }

        private sealed class ClientRecord
        {
            public int Id;
            public MonoBehaviour Owner;
            public CelestialSurfaceRuntime Runtime;
            public SurfaceVariantKey Surface;
            public bool RequestFrameOpen;
            public HashSet<PatchKey> PreviousRequests =
                new HashSet<PatchKey>();
            public HashSet<PatchKey> CurrentRequests =
                new HashSet<PatchKey>();
        }

        private sealed class ClientInterest
        {
            public bool Background;
            public bool Transient;
            public double Priority;
            public CelestialSurfacePatchRequestClass RequestClass;
        }

        private sealed class RequestRecord
        {
            public PatchKey Key;
            public long Sequence;
            public Dictionary<int, ClientInterest> Interests =
                new Dictionary<int, ClientInterest>();
            public double AggregatePriority;
            public CelestialSurfacePatchRequestClass AggregateClass;
        }

        private sealed class SurfaceRecord
        {
            public SurfaceVariantKey Key;
            public CelestialBodyDefinition Definition;
            public MapMagicObject GenerationSource;
            public string GenerationSourceName;
            public TerrainLayer[] TerrainLayers =
                Array.Empty<TerrainLayer>();
            public bool HasResolvedTextureLayout;
            public int Version;
            public int CompletedPatchCount;
            public double LastGenerationMilliseconds;
            public float NextSourceSearchTime;
            public string LastError;
            public HashSet<int> Clients =
                new HashSet<int>();

            public MapMagic.Nodes.Graph Graph =>
                Definition != null &&
                Definition.RoundMapMagicSurface != null
                    ? Definition.RoundMapMagicSurface.Graph
                    : null;
        }

        private sealed class CacheEntry
        {
            public CelestialSurfacePatchData Data;
            public long LastAccess;
        }

        private sealed class GenerationResult
        {
            public int SurfaceVersion;
            public PatchKey Key;
            public float[] ElevationsMeters;
            public int SurfaceLayerCount;
            public float[] SurfaceControlWeights;
            public TerrainLayer[] TerrainLayers;
            public double Milliseconds;
            public string Error;
        }

        private sealed class GenerationJob
        {
            public PatchKey Key;
            public RequestRecord Request;
            public SurfaceRecord Surface;
            public StopToken Stop;
            public Task<GenerationResult> Task;
            public bool CancellationRequested;
        }

        [Header("Shared Generation Budget")]
        [SerializeField]
        [Range(1, 8)]
        private int maximumConcurrentGenerations = 1;

        [SerializeField]
        [Range(1, 8)]
        private int maximumMainThreadPreparationsPerFrame = 1;

        [Header("Shared Memory Budget")]
        [SerializeField]
        [Min(64)]
        private int maximumCachedPatchCount =
            16384;

        [SerializeField]
        [Min(32)]
        private int memoryBudgetMegabytes =
            512;

        [Header("Background Preparation")]
        [SerializeField]
        private bool prepareCoarseRoots = true;

        [Header("Runtime")]
        [SerializeField]
        private int registeredClientCount;

        [SerializeField]
        private int registeredSurfaceCount;

        [SerializeField]
        private int queuedPatchCount;

        [SerializeField]
        private int activeGenerationCount;

        [SerializeField]
        private int cachedPatchCount;

        [SerializeField]
        private int failedPatchCount;

        [SerializeField]
        private int completedGenerationCount;

        [SerializeField]
        private int cancelledGenerationCount;

        [SerializeField]
        private int evictedPatchCount;

        [SerializeField]
        private double estimatedCacheMegabytes;

        [SerializeField]
        private string lastError;

        private readonly Dictionary<int, ClientRecord> clients =
            new Dictionary<int, ClientRecord>();
        private readonly Dictionary<
            MonoBehaviour,
            int> clientIdsByOwner =
                new Dictionary<
                    MonoBehaviour,
                    int>();
        private readonly Dictionary<
            SurfaceVariantKey,
            SurfaceRecord> surfaces =
                new Dictionary<
                    SurfaceVariantKey,
                    SurfaceRecord>();
        private readonly Dictionary<
            PatchKey,
            RequestRecord> requests =
                new Dictionary<
                    PatchKey,
                    RequestRecord>();
        private readonly Dictionary<
            PatchKey,
            CacheEntry> cache =
                new Dictionary<
                    PatchKey,
                    CacheEntry>();
        private readonly Dictionary<
            PatchKey,
            string> failures =
                new Dictionary<
                    PatchKey,
                    string>();
        private readonly List<GenerationJob> activeJobs =
            new List<GenerationJob>();
        private readonly Dictionary<
            PatchKey,
            GenerationJob> activeJobsByKey =
                new Dictionary<
                    PatchKey,
                    GenerationJob>();
        private readonly Dictionary<
            MapMagic.Nodes.Graph,
            MapMagicObject> ownedGenerationSources =
                new Dictionary<
                    MapMagic.Nodes.Graph,
                    MapMagicObject>();

        private int nextClientId = 1;
        private long requestSequence;
        private long accessSequence;
        private long estimatedCacheBytes;

        public int RegisteredClientCount =>
            registeredClientCount;

        public int RegisteredSurfaceCount =>
            registeredSurfaceCount;

        public int QueuedPatchCount =>
            queuedPatchCount;

        public int ActiveGenerationCount =>
            activeGenerationCount;

        public int CachedPatchCount =>
            cachedPatchCount;

        public int FailedPatchCount =>
            failedPatchCount;

        public int CompletedGenerationCount =>
            completedGenerationCount;

        public int CancelledGenerationCount =>
            cancelledGenerationCount;

        public int EvictedPatchCount =>
            evictedPatchCount;

        public double EstimatedCacheMegabytes =>
            estimatedCacheMegabytes;

        public int MaximumConcurrentGenerations =>
            maximumConcurrentGenerations;

        public int MaximumMainThreadPreparationsPerFrame =>
            maximumMainThreadPreparationsPerFrame;

        public int MaximumCachedPatchCount =>
            maximumCachedPatchCount;

        public int MemoryBudgetMegabytes =>
            memoryBudgetMegabytes;

        public bool PrepareCoarseRoots =>
            prepareCoarseRoots;

        public string LastError =>
            lastError;

        public static CelestialSurfaceCacheManager ResolveOrCreate(
            CelestialSurfaceCacheManager preferred = null)
        {
            if (preferred != null)
            {
                return preferred;
            }

            var managers =
                FindObjectsByType<
                    CelestialSurfaceCacheManager>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None);

            for (var index = 0;
                index < managers.Length;
                index++)
            {
                if (managers[index] != null &&
                    managers[index].isActiveAndEnabled)
                {
                    return managers[index];
                }
            }

            var managerObject =
                new GameObject(
                    "Celestial Surface Cache Manager");
            return
                managerObject.AddComponent<
                    CelestialSurfaceCacheManager>();
        }

        public bool RegisterClient(
            MonoBehaviour owner,
            CelestialSurfaceRuntime surfaceRuntime,
            int generationMargins,
            bool requestBackgroundRoots,
            out int clientId,
            out string error)
        {
            clientId = 0;

            if (owner == null ||
                surfaceRuntime == null ||
                !surfaceRuntime.FoundationReady)
            {
                error =
                    "The shared surface cache requires an initialized client and surface runtime.";
                return false;
            }

            if (clientIdsByOwner.TryGetValue(
                    owner,
                    out var existingClientId))
            {
                UnregisterClient(
                    existingClientId);
            }

            var resolvedMargins =
                Mathf.Clamp(
                    generationMargins,
                    0,
                    16);
            var surfaceKey =
                new SurfaceVariantKey(
                    surfaceRuntime.CacheKey,
                    surfaceRuntime.PatchResolution,
                    resolvedMargins);

            if (!surfaces.TryGetValue(
                    surfaceKey,
                    out var surface))
            {
                surface =
                    new SurfaceRecord
                    {
                        Key = surfaceKey,
                        Definition =
                            surfaceRuntime.BodyDefinition
                    };
                surfaces.Add(
                    surfaceKey,
                    surface);
            }

            clientId = nextClientId++;
            var client =
                new ClientRecord
                {
                    Id = clientId,
                    Owner = owner,
                    Runtime = surfaceRuntime,
                    Surface = surfaceKey
                };
            clients.Add(
                clientId,
                client);
            clientIdsByOwner.Add(
                owner,
                clientId);
            surface.Clients.Add(
                clientId);

            if (prepareCoarseRoots &&
                requestBackgroundRoots)
            {
                AddBackgroundRootRequests(
                    client);
            }

            RefreshDiagnostics();
            error = string.Empty;
            return true;
        }

        public void UnregisterClient(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return;
            }

            var affectedKeys =
                new List<PatchKey>();

            foreach (var pair in requests)
            {
                if (pair.Value.Interests.Remove(
                        clientId))
                {
                    affectedKeys.Add(
                        pair.Key);
                }
            }

            for (var index = 0;
                index < affectedKeys.Count;
                index++)
            {
                RefreshRequestAfterInterestChange(
                    affectedKeys[index]);
            }

            if (surfaces.TryGetValue(
                    client.Surface,
                    out var surface))
            {
                surface.Clients.Remove(
                    clientId);
            }

            clientIdsByOwner.Remove(
                client.Owner);

            clients.Remove(
                clientId);
            RefreshDiagnostics();
        }

        public bool BeginRequestFrame(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return false;
            }

            if (client.RequestFrameOpen)
            {
                EndRequestFrame(
                    clientId);
            }

            client.CurrentRequests.Clear();
            client.RequestFrameOpen = true;
            return true;
        }

        public bool RequestPatch(
            int clientId,
            CubeSpherePatchAddress address,
            double priority,
            CelestialSurfacePatchRequestClass requestClass)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client) ||
                !client.RequestFrameOpen ||
                !address.IsValid ||
                address.Level <
                    client.Runtime.MinimumLevel ||
                address.Level >
                    client.Runtime.MaximumLevel ||
                !IsFinite(priority))
            {
                return false;
            }

            var key =
                new PatchKey(
                    client.Surface,
                    address);
            var request =
                GetOrCreateRequest(
                    key);

            if (!request.Interests.TryGetValue(
                    clientId,
                    out var interest))
            {
                interest =
                    new ClientInterest();
                request.Interests.Add(
                    clientId,
                    interest);
            }

            var firstRequestThisFrame =
                client.CurrentRequests.Add(
                    key);
            interest.Transient = true;

            if (firstRequestThisFrame ||
                requestClass >
                    interest.RequestClass ||
                requestClass ==
                    interest.RequestClass &&
                priority > interest.Priority)
            {
                interest.Priority = priority;
                interest.RequestClass =
                    requestClass;
            }

            RecomputeAggregate(
                request);

            if (cache.TryGetValue(
                    key,
                    out var entry))
            {
                Touch(
                    entry);
            }

            return
                !failures.ContainsKey(
                    key);
        }

        public void EndRequestFrame(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client) ||
                !client.RequestFrameOpen)
            {
                return;
            }

            foreach (var previousKey in
                client.PreviousRequests)
            {
                if (client.CurrentRequests.Contains(
                        previousKey))
                {
                    continue;
                }

                RemoveTransientInterest(
                    clientId,
                    previousKey);
            }

            client.PreviousRequests.Clear();

            foreach (var currentKey in
                client.CurrentRequests)
            {
                client.PreviousRequests.Add(
                    currentKey);
            }

            client.RequestFrameOpen = false;
            CancelUnrequestedJobs();
            RefreshDiagnostics();
        }

        public CelestialSurfacePatchGenerationState GetPatchState(
            int clientId,
            CubeSpherePatchAddress address)
        {
            if (!TryGetClientPatchKey(
                    clientId,
                    address,
                    out var key))
            {
                return
                    CelestialSurfacePatchGenerationState.None;
            }

            if (cache.ContainsKey(
                    key))
            {
                return
                    CelestialSurfacePatchGenerationState.Ready;
            }

            if (failures.ContainsKey(
                    key))
            {
                return
                    CelestialSurfacePatchGenerationState.Failed;
            }

            if (activeJobsByKey.ContainsKey(
                    key))
            {
                return
                    CelestialSurfacePatchGenerationState.Generating;
            }

            return
                requests.ContainsKey(
                    key)
                    ? CelestialSurfacePatchGenerationState.Queued
                    : CelestialSurfacePatchGenerationState.None;
        }

        public bool TryGetPatchData(
            int clientId,
            CubeSpherePatchAddress address,
            out CelestialSurfacePatchData patchData)
        {
            patchData = null;

            if (!TryGetClientPatchKey(
                    clientId,
                    address,
                    out var key) ||
                !cache.TryGetValue(
                    key,
                    out var entry))
            {
                return false;
            }

            Touch(
                entry);
            patchData = entry.Data;
            return true;
        }

        public bool AreRootsReady(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return false;
            }

            for (var index = 0;
                index < client.Runtime.RootPatchCount;
                index++)
            {
                if (!client.Runtime.TryGetRootPatch(
                        index,
                        out var address) ||
                    !cache.ContainsKey(
                        new PatchKey(
                            client.Surface,
                            address)))
                {
                    return false;
                }
            }

            return true;
        }

        public TerrainLayer GetTerrainLayer(
            int clientId,
            int index)
        {
            var surface =
                GetClientSurface(
                    clientId);

            if (surface == null ||
                index < 0 ||
                index >= surface.TerrainLayers.Length)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(index));
            }

            return surface.TerrainLayers[index];
        }

        public int GetTerrainLayerCount(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null
                    ? surface.TerrainLayers.Length
                    : 0;
        }

        public bool GetSourceReady(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null &&
                SourceMatchesSurface(
                    surface,
                    surface.GenerationSource);
        }

        public string GetGenerationSourceName(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null
                    ? surface.GenerationSourceName
                    : string.Empty;
        }

        public bool GetIsGenerating(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return false;
            }

            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                if (activeJobs[index].Key.Surface.Equals(
                        client.Surface))
                {
                    return true;
                }
            }

            return false;
        }

        public CubeSpherePatchAddress GetActiveAddress(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return default;
            }

            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                if (activeJobs[index].Key.Surface.Equals(
                        client.Surface))
                {
                    return activeJobs[index].Key.Address;
                }
            }

            return default;
        }

        public int GetQueuedPatchCount(
            int clientId)
        {
            var count = 0;

            foreach (var pair in requests)
            {
                if (!cache.ContainsKey(
                        pair.Key) &&
                    !failures.ContainsKey(
                        pair.Key) &&
                    !activeJobsByKey.ContainsKey(
                        pair.Key) &&
                    HasActiveInterest(
                        pair.Value,
                        clientId))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetCachedPatchCount(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return 0;
            }

            var count = 0;

            foreach (var pair in cache)
            {
                if (pair.Key.Surface.Equals(
                        client.Surface))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetFailedPatchCount(
            int clientId)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client))
            {
                return 0;
            }

            var count = 0;

            foreach (var pair in failures)
            {
                if (pair.Key.Surface.Equals(
                        client.Surface))
                {
                    count++;
                }
            }

            return count;
        }

        public int GetCompletedPatchCount(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null
                    ? surface.CompletedPatchCount
                    : 0;
        }

        public double GetLastGenerationMilliseconds(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null
                    ? surface.LastGenerationMilliseconds
                    : 0.0;
        }

        public int GetSurfaceVersion(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null
                    ? surface.Version
                    : -1;
        }

        public string GetClientLastError(
            int clientId)
        {
            var surface =
                GetClientSurface(
                    clientId);
            return
                surface != null
                    ? surface.LastError
                    : "The surface-cache client is not registered.";
        }

        public int InvalidateSurface(
            CelestialSurfaceCacheKey cacheKey)
        {
            var variants =
                new List<SurfaceVariantKey>();

            foreach (var pair in surfaces)
            {
                if (pair.Key.CacheKey.Equals(
                        cacheKey))
                {
                    variants.Add(
                        pair.Key);
                    pair.Value.Version++;
                    pair.Value.HasResolvedTextureLayout =
                        false;
                    pair.Value.TerrainLayers =
                        Array.Empty<TerrainLayer>();
                    pair.Value.LastError =
                        string.Empty;
                }
            }

            var removed = 0;
            var cacheKeys =
                new List<PatchKey>(
                    cache.Keys);

            for (var index = 0;
                index < cacheKeys.Count;
                index++)
            {
                if (variants.Contains(
                        cacheKeys[index].Surface))
                {
                    RemoveCacheEntry(
                        cacheKeys[index]);
                    removed++;
                }
            }

            var failureKeys =
                new List<PatchKey>(
                    failures.Keys);

            for (var index = 0;
                index < failureKeys.Count;
                index++)
            {
                if (variants.Contains(
                        failureKeys[index].Surface))
                {
                    failures.Remove(
                        failureKeys[index]);
                }
            }

            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                if (variants.Contains(
                        activeJobs[index]
                            .Key.Surface))
                {
                    RequestCancellation(
                        activeJobs[index]);
                }
            }

            RefreshDiagnostics();
            return removed;
        }

        [ContextMenu("Evict Unrequested Surface Patches")]
        private void EvictUnrequestedSurfacePatches()
        {
            var keys =
                new List<PatchKey>(
                    cache.Keys);

            for (var index = 0;
                index < keys.Count;
                index++)
            {
                if (!IsPatchPinned(
                        keys[index]))
                {
                    RemoveCacheEntry(
                        keys[index]);
                    evictedPatchCount++;
                }
            }

            RefreshDiagnostics();
        }

        private void Update()
        {
            PruneDestroyedClients();
            CompleteFinishedJobs();
            CancelUnrequestedJobs();
            PreemptBackgroundWork();
            StartGenerationWork();
            EvictToBudget();
            RefreshDiagnostics();
        }

        private void AddBackgroundRootRequests(
            ClientRecord client)
        {
            for (var index = 0;
                index < client.Runtime.RootPatchCount;
                index++)
            {
                if (!client.Runtime.TryGetRootPatch(
                        index,
                        out var address))
                {
                    continue;
                }

                var key =
                    new PatchKey(
                        client.Surface,
                        address);
                var request =
                    GetOrCreateRequest(
                        key);

                if (!request.Interests.TryGetValue(
                        client.Id,
                        out var interest))
                {
                    interest =
                        new ClientInterest();
                    request.Interests.Add(
                        client.Id,
                        interest);
                }

                interest.Background = true;
                RecomputeAggregate(
                    request);
            }
        }

        private RequestRecord GetOrCreateRequest(
            PatchKey key)
        {
            if (requests.TryGetValue(
                    key,
                    out var request))
            {
                return request;
            }

            request =
                new RequestRecord
                {
                    Key = key,
                    Sequence = requestSequence++
                };
            requests.Add(
                key,
                request);
            return request;
        }

        private void RemoveTransientInterest(
            int clientId,
            PatchKey key)
        {
            if (!requests.TryGetValue(
                    key,
                    out var request) ||
                !request.Interests.TryGetValue(
                    clientId,
                    out var interest))
            {
                return;
            }

            interest.Transient = false;

            if (!interest.Background)
            {
                request.Interests.Remove(
                    clientId);
            }

            RefreshRequestAfterInterestChange(
                key);
        }

        private void RefreshRequestAfterInterestChange(
            PatchKey key)
        {
            if (!requests.TryGetValue(
                    key,
                    out var request))
            {
                return;
            }

            if (request.Interests.Count == 0)
            {
                requests.Remove(
                    key);

                if (activeJobsByKey.TryGetValue(
                        key,
                        out var job))
                {
                    RequestCancellation(
                        job);
                }

                return;
            }

            RecomputeAggregate(
                request);
        }

        private static void RecomputeAggregate(
            RequestRecord request)
        {
            var hasAggregate = false;
            var bestClass =
                CelestialSurfacePatchRequestClass.Background;
            var bestPriority =
                double.NegativeInfinity;

            foreach (var pair in request.Interests)
            {
                var interest = pair.Value;

                if (interest.Background)
                {
                    ConsiderPriority(
                        CelestialSurfacePatchRequestClass.Background,
                        BackgroundRootPriority -
                            (int)request.Key.Address.Face,
                        ref hasAggregate,
                        ref bestClass,
                        ref bestPriority);
                }

                if (interest.Transient)
                {
                    ConsiderPriority(
                        interest.RequestClass,
                        interest.Priority,
                        ref hasAggregate,
                        ref bestClass,
                        ref bestPriority);
                }
            }

            request.AggregateClass =
                bestClass;
            request.AggregatePriority =
                hasAggregate
                    ? bestPriority
                    : 0.0;
        }

        private static void ConsiderPriority(
            CelestialSurfacePatchRequestClass requestClass,
            double priority,
            ref bool hasAggregate,
            ref CelestialSurfacePatchRequestClass bestClass,
            ref double bestPriority)
        {
            if (!hasAggregate ||
                requestClass > bestClass ||
                requestClass == bestClass &&
                priority > bestPriority)
            {
                hasAggregate = true;
                bestClass = requestClass;
                bestPriority = priority;
            }
        }

        private void StartGenerationWork()
        {
            var preparationsRemaining =
                Mathf.Max(
                    1,
                    maximumMainThreadPreparationsPerFrame);
            var concurrency =
                Mathf.Max(
                    1,
                    maximumConcurrentGenerations);

            while (activeJobs.Count < concurrency &&
                preparationsRemaining > 0 &&
                TrySelectBestRunnableRequest(
                    out var request,
                    out var surface))
            {
                preparationsRemaining--;
                StartGeneration(
                    request,
                    surface);
            }
        }

        private bool TrySelectBestRunnableRequest(
            out RequestRecord selected,
            out SurfaceRecord selectedSurface)
        {
            selected = null;
            selectedSurface = null;

            foreach (var pair in requests)
            {
                var request = pair.Value;

                if (request.Interests.Count == 0 ||
                    cache.ContainsKey(
                        request.Key) ||
                    failures.ContainsKey(
                        request.Key) ||
                    activeJobsByKey.ContainsKey(
                        request.Key) ||
                    !surfaces.TryGetValue(
                        request.Key.Surface,
                        out var surface) ||
                    !ResolveGenerationSource(
                        surface,
                        false) ||
                    IsGraphGenerating(
                        surface.Graph))
                {
                    continue;
                }

                if (selected == null ||
                    IsHigherPriority(
                        request,
                        selected))
                {
                    selected = request;
                    selectedSurface = surface;
                }
            }

            return selected != null;
        }

        private static bool IsHigherPriority(
            RequestRecord candidate,
            RequestRecord current)
        {
            return
                candidate.AggregateClass >
                    current.AggregateClass ||
                candidate.AggregateClass ==
                    current.AggregateClass &&
                (candidate.AggregatePriority >
                    current.AggregatePriority ||
                candidate.AggregatePriority.Equals(
                    current.AggregatePriority) &&
                candidate.Sequence <
                    current.Sequence);
        }

        private bool IsGraphGenerating(
            MapMagic.Nodes.Graph graph)
        {
            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                if (activeJobs[index]
                        .Surface.Graph == graph)
                {
                    return true;
                }
            }

            return false;
        }

        private void StartGeneration(
            RequestRecord request,
            SurfaceRecord surface)
        {
            if (!RoundMapMagicSurfacePatchRequest.TryCreate(
                    surface.Definition,
                    request.Key.Address,
                    surface.Key.Resolution,
                    out var patchRequest,
                    out var requestError))
            {
                FailRequest(
                    request.Key,
                    surface,
                    requestError);
                return;
            }

            var graph = surface.Graph;
            var area =
                new Area(
                    new Vector2D(
                        (float)patchRequest
                            .MapWorldOriginXMeters,
                        (float)patchRequest
                            .MapWorldOriginZMeters),
                    new Vector2D(
                        (float)patchRequest
                            .MapWorldSizeXMeters,
                        (float)patchRequest
                            .MapWorldSizeZMeters),
                    patchRequest.Resolution,
                    surface.Key.Margins);
            var data =
                new RoundMapMagicSphericalTileData
                {
                    Face = request.Key.Address.Face,
                    PlanetRadiusMeters =
                        patchRequest.PlanetRadiusMeters,
                    SurfaceSeed =
                        patchRequest.SurfaceSeed,
                    MapWorldOriginXMeters =
                        patchRequest.MapWorldOriginXMeters,
                    MapWorldOriginZMeters =
                        patchRequest.MapWorldOriginZMeters,
                    MapWorldSizeXMeters =
                        patchRequest.MapWorldSizeXMeters,
                    MapWorldSizeZMeters =
                        patchRequest.MapWorldSizeZMeters,
                    area = area,
                    globals =
                        surface.GenerationSource.globals,
                    random = graph.random,
                    isPreview = false,
                    isDraft = true
                };

            try
            {
                graph.Prepare(
                    data,
                    null);
            }
            catch (Exception exception)
            {
                data.Clear(
                    clearApply: true,
                    inSubs: true);
                FailRequest(
                    request.Key,
                    surface,
                    exception.GetBaseException().Message);
                return;
            }

            var stop =
                new StopToken();
            var job =
                new GenerationJob
                {
                    Key = request.Key,
                    Request = request,
                    Surface = surface,
                    Stop = stop
                };
            var surfaceVersion =
                surface.Version;
            job.Task =
                Task.Run(
                    () => GeneratePatch(
                        graph,
                        data,
                        stop,
                        patchRequest,
                        request.Key,
                        surfaceVersion));
            activeJobs.Add(
                job);
            activeJobsByKey.Add(
                request.Key,
                job);
        }

        private static GenerationResult GeneratePatch(
            MapMagic.Nodes.Graph graph,
            RoundMapMagicSphericalTileData data,
            StopToken stop,
            RoundMapMagicSurfacePatchRequest request,
            PatchKey key,
            int surfaceVersion)
        {
            var result =
                new GenerationResult
                {
                    SurfaceVersion = surfaceVersion,
                    Key = key
                };
            var stopwatch =
                System.Diagnostics.Stopwatch.StartNew();

            try
            {
                try
                {
                    graph.Generate(
                        data,
                        stop);
                    graph.Finalize(
                        data,
                        stop);

                    if (stop.stop)
                    {
                        throw new InvalidOperationException(
                            "MapMagic stopped the shared patch request.");
                    }

                    if (data.heights == null)
                    {
                        throw new InvalidOperationException(
                            "The MapMagic graph did not produce a finalized Height output for the shared patch request.");
                    }

                    result.ElevationsMeters =
                        CopyElevations(
                            data,
                            request);
                    CaptureSurfaceControls(
                        result,
                        data.ApplyOfType<
                            TexturesOutput200.ApplyData>(),
                        request);
                }
                finally
                {
                    data.Clear(
                        clearApply: true,
                        inSubs: true);
                }
            }
            catch (Exception exception)
            {
                result.Error =
                    exception.GetBaseException().Message;
            }
            finally
            {
                stopwatch.Stop();
                result.Milliseconds =
                    stopwatch.Elapsed.TotalMilliseconds;
            }

            return result;
        }

        private static float[] CopyElevations(
            RoundMapMagicSphericalTileData data,
            RoundMapMagicSurfacePatchRequest request)
        {
            var resolution = request.Resolution;
            var elevations =
                new float[
                    resolution *
                    resolution];
            var heightScaleMeters =
                data.heights.worldSize.y;

            for (var sampleY = 0;
                sampleY < resolution;
                sampleY++)
            {
                var mapPixelZ =
                    request.PatchSampleYToMapPixelZ(
                        sampleY);
                var pixelZ =
                    data.area.active.rect.offset.z +
                    mapPixelZ;

                for (var sampleX = 0;
                    sampleX < resolution;
                    sampleX++)
                {
                    var pixelX =
                        data.area.active.rect.offset.x +
                        sampleX;
                    var normalizedHeight =
                        data.heights[
                            pixelX,
                            pixelZ];
                    elevations[
                        sampleY * resolution +
                        sampleX] =
                            normalizedHeight *
                                heightScaleMeters +
                            (float)request
                                .ElevationOffsetMeters;
                }
            }

            return elevations;
        }

        private static void CaptureSurfaceControls(
            GenerationResult result,
            TexturesOutput200.ApplyData textureData,
            RoundMapMagicSurfacePatchRequest request)
        {
            if (textureData == null ||
                textureData.splats == null ||
                textureData.prototypes == null ||
                textureData.prototypes.Length == 0)
            {
                return;
            }

            var resolution = request.Resolution;
            var layerCount =
                textureData.prototypes.Length;

            if (layerCount >
                MaximumSurfaceLayerCount)
            {
                throw new InvalidOperationException(
                    $"The shared surface cache supports up to {MaximumSurfaceLayerCount} terrain layers, but the graph produced {layerCount}.");
            }

            if (textureData.splats.GetLength(0) !=
                    resolution ||
                textureData.splats.GetLength(1) !=
                    resolution ||
                textureData.splats.GetLength(2) !=
                    layerCount)
            {
                throw new InvalidOperationException(
                    "The MapMagic texture-control dimensions do not match the shared patch request.");
            }

            var weights =
                new float[
                    resolution *
                    resolution *
                    layerCount];

            for (var sampleY = 0;
                sampleY < resolution;
                sampleY++)
            {
                var mapPixelZ =
                    request.PatchSampleYToMapPixelZ(
                        sampleY);

                for (var sampleX = 0;
                    sampleX < resolution;
                    sampleX++)
                {
                    for (var layer = 0;
                        layer < layerCount;
                        layer++)
                    {
                        weights[
                            (sampleY * resolution +
                                sampleX) *
                                layerCount +
                            layer] =
                                textureData.splats[
                                    mapPixelZ,
                                    sampleX,
                                    layer];
                    }
                }
            }

            result.SurfaceLayerCount =
                layerCount;
            result.SurfaceControlWeights =
                weights;
            result.TerrainLayers =
                textureData.prototypes;
        }

        private void CompleteFinishedJobs()
        {
            for (var index = activeJobs.Count - 1;
                index >= 0;
                index--)
            {
                var job = activeJobs[index];

                if (!job.Task.IsCompleted)
                {
                    continue;
                }

                activeJobs.RemoveAt(
                    index);
                activeJobsByKey.Remove(
                    job.Key);
                GenerationResult result;

                try
                {
                    result =
                        job.Task.GetAwaiter().GetResult();
                }
                catch (Exception exception)
                {
                    result =
                        new GenerationResult
                        {
                            SurfaceVersion =
                                job.Surface.Version,
                            Key = job.Key,
                            Error = exception
                                .GetBaseException()
                                .Message
                        };
                }

                if (job.CancellationRequested ||
                    result.SurfaceVersion !=
                        job.Surface.Version ||
                    !requests.ContainsKey(
                        job.Key))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(
                        result.Error))
                {
                    FailRequest(
                        job.Key,
                        job.Surface,
                        result.Error);
                    continue;
                }

                if (!TryAcceptTextureLayout(
                        job.Surface,
                        result,
                        out var textureError))
                {
                    FailRequest(
                        job.Key,
                        job.Surface,
                        textureError);
                    continue;
                }

                try
                {
                    var data =
                        new CelestialSurfacePatchData(
                            job.Surface.Key.CacheKey,
                            job.Key.Address,
                            job.Surface.Key.Resolution,
                            result.ElevationsMeters,
                            result.SurfaceLayerCount,
                            result.SurfaceControlWeights);
                    StoreCacheEntry(
                        job.Key,
                        data);
                    job.Surface.CompletedPatchCount++;
                    job.Surface.LastGenerationMilliseconds =
                        result.Milliseconds;
                    job.Surface.LastError =
                        string.Empty;
                    completedGenerationCount++;
                }
                catch (Exception exception)
                {
                    FailRequest(
                        job.Key,
                        job.Surface,
                        exception.GetBaseException().Message);
                }
            }
        }

        private static bool TryAcceptTextureLayout(
            SurfaceRecord surface,
            GenerationResult result,
            out string error)
        {
            var hasTextureData =
                result.SurfaceLayerCount > 0 &&
                result.SurfaceControlWeights != null &&
                result.TerrainLayers != null &&
                result.TerrainLayers.Length ==
                    result.SurfaceLayerCount;

            if (!surface.HasResolvedTextureLayout)
            {
                surface.HasResolvedTextureLayout =
                    true;
                surface.TerrainLayers =
                    hasTextureData
                        ? (TerrainLayer[])result
                            .TerrainLayers.Clone()
                        : Array.Empty<TerrainLayer>();
                error = string.Empty;
                return true;
            }

            if (hasTextureData !=
                (surface.TerrainLayers.Length > 0))
            {
                error =
                    "The MapMagic graph produced inconsistent texture outputs between shared patches.";
                return false;
            }

            if (hasTextureData)
            {
                if (result.TerrainLayers.Length !=
                    surface.TerrainLayers.Length)
                {
                    error =
                        "The MapMagic graph produced inconsistent terrain-layer counts between shared patches.";
                    return false;
                }

                for (var index = 0;
                    index < surface.TerrainLayers.Length;
                    index++)
                {
                    if (surface.TerrainLayers[index] !=
                        result.TerrainLayers[index])
                    {
                        error =
                            "The MapMagic graph produced different terrain-layer prototypes between shared patches.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private void StoreCacheEntry(
            PatchKey key,
            CelestialSurfacePatchData data)
        {
            if (cache.TryGetValue(
                    key,
                    out var existing))
            {
                estimatedCacheBytes -=
                    existing.Data.EstimatedMemoryBytes;
            }

            var entry =
                new CacheEntry
                {
                    Data = data
                };
            Touch(
                entry);
            cache[key] = entry;
            estimatedCacheBytes +=
                data.EstimatedMemoryBytes;
        }

        private void Touch(
            CacheEntry entry)
        {
            entry.LastAccess =
                ++accessSequence;
        }

        private void EvictToBudget()
        {
            var byteBudget =
                (long)Mathf.Max(
                    32,
                    memoryBudgetMegabytes) *
                1024L *
                1024L;
            var countBudget =
                Mathf.Max(
                    64,
                    maximumCachedPatchCount);

            while (cache.Count > countBudget ||
                estimatedCacheBytes > byteBudget)
            {
                var found = false;
                var oldestKey = default(PatchKey);
                var oldestAccess = long.MaxValue;

                foreach (var pair in cache)
                {
                    if (IsPatchPinned(
                            pair.Key) ||
                        pair.Value.LastAccess >=
                            oldestAccess)
                    {
                        continue;
                    }

                    found = true;
                    oldestKey = pair.Key;
                    oldestAccess =
                        pair.Value.LastAccess;
                }

                if (!found)
                {
                    lastError =
                        "The shared surface cache exceeded its budget because every cached patch is currently requested.";
                    break;
                }

                RemoveCacheEntry(
                    oldestKey);
                evictedPatchCount++;
            }
        }

        private bool IsPatchPinned(
            PatchKey key)
        {
            return
                activeJobsByKey.ContainsKey(
                    key) ||
                requests.TryGetValue(
                    key,
                    out var request) &&
                request.Interests.Count > 0;
        }

        private void RemoveCacheEntry(
            PatchKey key)
        {
            if (!cache.TryGetValue(
                    key,
                    out var entry))
            {
                return;
            }

            estimatedCacheBytes -=
                entry.Data.EstimatedMemoryBytes;
            estimatedCacheBytes =
                Math.Max(
                    0L,
                    estimatedCacheBytes);
            cache.Remove(
                key);
        }

        private void CancelUnrequestedJobs()
        {
            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                var job = activeJobs[index];

                if (!requests.TryGetValue(
                        job.Key,
                        out var request) ||
                    request.Interests.Count == 0)
                {
                    RequestCancellation(
                        job);
                }
            }
        }

        private void PreemptBackgroundWork()
        {
            if (activeJobs.Count <
                Mathf.Max(
                    1,
                    maximumConcurrentGenerations))
            {
                return;
            }

            RequestRecord bestWaiting = null;

            foreach (var pair in requests)
            {
                var request = pair.Value;

                if (cache.ContainsKey(
                        request.Key) ||
                    failures.ContainsKey(
                        request.Key) ||
                    activeJobsByKey.ContainsKey(
                        request.Key) ||
                    request.Interests.Count == 0 ||
                    !surfaces.TryGetValue(
                        request.Key.Surface,
                        out var surface) ||
                    !ResolveGenerationSource(
                        surface,
                        false) ||
                    (bestWaiting != null &&
                    !IsHigherPriority(
                        request,
                        bestWaiting)))
                {
                    continue;
                }

                bestWaiting = request;
            }

            if (bestWaiting == null)
            {
                return;
            }

            GenerationJob lowestJob = null;

            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                var job = activeJobs[index];

                if (job.CancellationRequested ||
                    job.Request.AggregateClass >=
                        bestWaiting.AggregateClass)
                {
                    continue;
                }

                if (lowestJob == null ||
                    IsHigherPriority(
                        lowestJob.Request,
                        job.Request))
                {
                    lowestJob = job;
                }
            }

            if (lowestJob != null)
            {
                RequestCancellation(
                    lowestJob);
            }
        }

        private void RequestCancellation(
            GenerationJob job)
        {
            if (job == null ||
                job.CancellationRequested)
            {
                return;
            }

            job.CancellationRequested = true;

            if (job.Stop != null)
            {
                job.Stop.stop = true;
            }

            cancelledGenerationCount++;
        }

        private bool ResolveGenerationSource(
            SurfaceRecord surface,
            bool force)
        {
            if (SourceMatchesSurface(
                    surface,
                    surface.GenerationSource))
            {
                surface.GenerationSourceName =
                    surface.GenerationSource.name;
                return true;
            }

            surface.GenerationSource = null;
            surface.GenerationSourceName =
                string.Empty;

            if (!force &&
                Time.unscaledTime <
                    surface.NextSourceSearchTime)
            {
                return false;
            }

            surface.NextSourceSearchTime =
                Time.unscaledTime +
                1.0f;
            var rootPools =
                FindObjectsByType<
                    CubeSphereMapMagicRootPool>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);

            for (var index = 0;
                index < rootPools.Length;
                index++)
            {
                var candidate =
                    rootPools[index] != null
                        ? rootPools[index]
                            .GenerationSource
                        : null;

                if (SourceMatchesSurface(
                        surface,
                        candidate))
                {
                    surface.GenerationSource =
                        candidate;
                    surface.GenerationSourceName =
                        candidate.name;
                    surface.LastError =
                        string.Empty;
                    return true;
                }
            }

            if (TryResolveOrCreateOwnedGenerationSource(
                    surface,
                    out var ownedSource))
            {
                surface.GenerationSource =
                    ownedSource;
                surface.GenerationSourceName =
                    ownedSource.name;
                surface.LastError =
                    string.Empty;
                return true;
            }

            surface.LastError =
                "Waiting for a MapMagic generation source using this surface graph.";
            return false;
        }

        private bool TryResolveOrCreateOwnedGenerationSource(
            SurfaceRecord surface,
            out MapMagicObject source)
        {
            source = null;
            var graph =
                surface != null
                    ? surface.Graph
                    : null;

            if (graph == null)
            {
                return false;
            }

            if (ownedGenerationSources.TryGetValue(
                    graph,
                    out source) &&
                source != null)
            {
                return true;
            }

            ownedGenerationSources.Remove(
                graph);

            var sourceObject =
                new GameObject(
                    $"MapMagic Generation Source [{graph.name}]");
            sourceObject.hideFlags =
                HideFlags.DontSave;
            sourceObject.transform.SetParent(
                transform,
                false);
            sourceObject.SetActive(
                false);

            source =
                sourceObject.AddComponent<
                    MapMagicObject>();
            source.graph =
                graph;
            ownedGenerationSources.Add(
                graph,
                source);
            return true;
        }

        private static bool SourceMatchesSurface(
            SurfaceRecord surface,
            MapMagicObject source)
        {
            return
                surface != null &&
                surface.Graph != null &&
                source != null &&
                source.graph == surface.Graph;
        }

        private void FailRequest(
            PatchKey key,
            SurfaceRecord surface,
            string error)
        {
            failures[key] = error;
            surface.LastError =
                $"{key.Address}: {error}";
            lastError =
                surface.LastError;
        }

        private void PruneDestroyedClients()
        {
            List<int> destroyed = null;

            foreach (var pair in clients)
            {
                if (pair.Value.Owner != null &&
                    pair.Value.Runtime != null)
                {
                    continue;
                }

                if (destroyed == null)
                {
                    destroyed =
                        new List<int>();
                }

                destroyed.Add(
                    pair.Key);
            }

            if (destroyed == null)
            {
                return;
            }

            for (var index = 0;
                index < destroyed.Count;
                index++)
            {
                UnregisterClient(
                    destroyed[index]);
            }
        }

        private SurfaceRecord GetClientSurface(
            int clientId)
        {
            return
                clients.TryGetValue(
                    clientId,
                    out var client) &&
                surfaces.TryGetValue(
                    client.Surface,
                    out var surface)
                    ? surface
                    : null;
        }

        private bool TryGetClientPatchKey(
            int clientId,
            CubeSpherePatchAddress address,
            out PatchKey key)
        {
            if (!clients.TryGetValue(
                    clientId,
                    out var client) ||
                !address.IsValid)
            {
                key = default;
                return false;
            }

            key =
                new PatchKey(
                    client.Surface,
                    address);
            return true;
        }

        private static bool HasActiveInterest(
            RequestRecord request,
            int clientId)
        {
            return
                request.Interests.TryGetValue(
                    clientId,
                    out var interest) &&
                (interest.Background ||
                    interest.Transient);
        }

        private void RefreshDiagnostics()
        {
            registeredClientCount =
                clients.Count;
            registeredSurfaceCount =
                surfaces.Count;
            activeGenerationCount =
                activeJobs.Count;
            cachedPatchCount =
                cache.Count;
            failedPatchCount =
                failures.Count;
            queuedPatchCount = 0;

            foreach (var pair in requests)
            {
                if (pair.Value.Interests.Count > 0 &&
                    !cache.ContainsKey(
                        pair.Key) &&
                    !failures.ContainsKey(
                        pair.Key) &&
                    !activeJobsByKey.ContainsKey(
                        pair.Key))
                {
                    queuedPatchCount++;
                }
            }

            estimatedCacheMegabytes =
                estimatedCacheBytes /
                (1024.0 * 1024.0);

            if (failedPatchCount == 0 &&
                estimatedCacheBytes <=
                    (long)Mathf.Max(
                        32,
                        memoryBudgetMegabytes) *
                    1024L *
                    1024L)
            {
                lastError = string.Empty;
            }
        }

        private void OnDisable()
        {
            for (var index = 0;
                index < activeJobs.Count;
                index++)
            {
                RequestCancellation(
                    activeJobs[index]);
            }
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
