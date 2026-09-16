/*
 * Maintains seeded, progressive-LOD celestial small-body containers. Tools
 * provide one representation at a time; this manager assembles each container
 * from billboard through its configured mesh-detail ceiling.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyPoolManager :
        MonoBehaviour
    {
        [Serializable]
        private sealed class ToolPoolConfiguration
        {
            [SerializeField]
            [Tooltip("A component implementing ICelestialSmallBodyGenerationTool.")]
            private MonoBehaviour toolSource;

            [SerializeField]
            [Min(0)]
            [Tooltip("Target number of available small-body containers. This is not multiplied by the number of LODs.")]
            private int targetReadyCount = 8;

            [SerializeField]
            [Tooltip("Every container starts at billboard LOD 0, then is upgraded until this minimum is ready.")]
            private CelestialSmallBodyLod minimumPrewarmLod =
                CelestialSmallBodyLod.Billboard;

            [SerializeField]
            [Tooltip("Idle background upgrades stop at this LOD.")]
            private CelestialSmallBodyLod maximumPrewarmLod =
                CelestialSmallBodyLod.Detail2;

            [SerializeField]
            [Tooltip("Checked-out objects are consumed and immediately replaced by a new LOD 0 container.")]
            private bool regenerateAfterCheckout = true;

            public MonoBehaviour ToolSource =>
                toolSource;

            public int TargetReadyCount =>
                targetReadyCount;

            public CelestialSmallBodyLod MinimumPrewarmLod =>
                minimumPrewarmLod;

            public CelestialSmallBodyLod MaximumPrewarmLod =>
                maximumPrewarmLod;

            public bool RegenerateAfterCheckout =>
                regenerateAfterCheckout;

            public void Validate()
            {
                targetReadyCount =
                    Mathf.Max(
                        0,
                        targetReadyCount);
                minimumPrewarmLod =
                    ClampLod(
                        minimumPrewarmLod);
                maximumPrewarmLod =
                    ClampLod(
                        maximumPrewarmLod);

                if ((int)maximumPrewarmLod <
                    (int)minimumPrewarmLod)
                {
                    maximumPrewarmLod =
                        minimumPrewarmLod;
                }
            }

            public void ClampToToolLods(
                int lodCount)
            {
                var maximumLod =
                    Mathf.Max(
                        0,
                        lodCount - 1);
                minimumPrewarmLod =
                    (CelestialSmallBodyLod)Mathf.Min(
                        (int)minimumPrewarmLod,
                        maximumLod);
                maximumPrewarmLod =
                    (CelestialSmallBodyLod)Mathf.Min(
                        (int)maximumPrewarmLod,
                        maximumLod);

                if ((int)maximumPrewarmLod <
                    (int)minimumPrewarmLod)
                {
                    maximumPrewarmLod =
                        minimumPrewarmLod;
                }
            }

            private static CelestialSmallBodyLod ClampLod(
                CelestialSmallBodyLod lod)
            {
                return
                    (CelestialSmallBodyLod)Mathf.Clamp(
                        (int)lod,
                        (int)CelestialSmallBodyLod.Billboard,
                        (int)CelestialSmallBodyLod.Detail4);
            }
        }

        private sealed class WaitingRequest
        {
            public CelestialSmallBodyRequest Request;
            public Action<GameObject> Completed;
        }

        private sealed class RuntimeSlot
        {
            public uint Seed;
            public CelestialSmallBodyInstance Instance;
            public bool CheckedOut;
            public bool GenerationPending;
            public CelestialSmallBodyLod PendingLod;
        }

        private sealed class RuntimePool
        {
            public ToolPoolConfiguration Configuration;
            public ICelestialSmallBodyGenerationTool Tool;
            public readonly List<RuntimeSlot> Slots =
                new List<RuntimeSlot>();
            public readonly List<WaitingRequest> WaitingRequests =
                new List<WaitingRequest>();
        }

        [Header("Tool Pools")]
        [SerializeField]
        private List<ToolPoolConfiguration> toolPools =
            new List<ToolPoolConfiguration>();

        [Header("Registered Tool Sources")]
        [SerializeField]
        [Tooltip("Runtime list of the sources currently registered from Tool Pools.")]
        private List<MonoBehaviour> registeredToolSources =
            new List<MonoBehaviour>();

        [SerializeField]
        [Tooltip("Optional parent used only while a small-body container is available in its pool.")]
        private Transform poolRoot;

        [SerializeField]
        [Min(1)]
        [Tooltip("Upper limit on new representation jobs started each frame.")]
        private int maximumGenerationStartsPerFrame = 1;

        [SerializeField]
        [Tooltip("Seed used for non-deterministic prewarmed containers.")]
        private uint prewarmSeed = 1u;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        [Tooltip("Available LOD 0-or-higher containers across all tools.")]
        private int readyObjectCount;

        [SerializeField]
        [Tooltip("Representation jobs currently in flight across all containers.")]
        private int pendingGenerationCount;

        [SerializeField]
        private int waitingRequestCount;

        [SerializeField]
        [Tooltip("All currently tracked containers, including those upgrading.")]
        private int containerCount;

        [SerializeField]
        private string lastError;

        private readonly Dictionary<string, RuntimePool> poolsByToolId =
            new Dictionary<string, RuntimePool>(
                StringComparer.Ordinal);
        private bool poolsResolved;

        public int ReadyObjectCount =>
            readyObjectCount;

        public int PendingGenerationCount =>
            pendingGenerationCount;

        public int WaitingRequestCount =>
            waitingRequestCount;

        public int ContainerCount =>
            containerCount;

        public string LastError =>
            lastError;

        private void Awake()
        {
            ResolvePools();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                EnsurePoolRoot();
                ResolvePools();
            }
        }

        private void OnValidate()
        {
            maximumGenerationStartsPerFrame =
                Mathf.Max(
                    1,
                    maximumGenerationStartsPerFrame);
            registeredToolSources ??=
                new List<MonoBehaviour>();
            registeredToolSources.Clear();

            foreach (var configuration in toolPools)
            {
                configuration?.Validate();

                if (configuration?.ToolSource != null)
                {
                    registeredToolSources.Add(
                        configuration.ToolSource);
                }
            }

            poolsResolved = false;
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!poolsResolved)
            {
                ResolvePools();
            }

            MaintainPools();
            RefreshDiagnostics();
        }

        private void OnDestroy()
        {
            foreach (var pool in poolsByToolId.Values)
            {
                foreach (var slot in pool.Slots)
                {
                    if (slot.Instance != null)
                    {
                        Destroy(
                            slot.Instance.gameObject);
                    }
                }
            }
        }

        public bool TryAcquireReady(
            CelestialSmallBodyRequest request,
            out GameObject smallBody)
        {
            smallBody = null;

            if (!TryGetPool(
                    request.ToolId,
                    out var pool))
            {
                return false;
            }

            var slot =
                FindBestAvailableSlot(
                    pool,
                    request);

            if (slot == null)
            {
                return false;
            }

            CheckoutSlot(
                pool,
                slot,
                out smallBody);
            return smallBody != null;
        }

        public void RequestSmallBody(
            CelestialSmallBodyRequest request,
            Action<GameObject> completed)
        {
            if (TryAcquireReady(
                    request,
                    out var readyBody))
            {
                completed?.Invoke(
                    readyBody);
                return;
            }

            if (!TryGetPool(
                    request.ToolId,
                    out var pool))
            {
                completed?.Invoke(
                    null);
                return;
            }

            pool.WaitingRequests.Add(
                new WaitingRequest
                {
                    Request = request,
                    Completed = completed
                });
        }

        public void ReturnToPool(
            GameObject smallBody)
        {
            if (smallBody == null)
            {
                return;
            }

            var instance =
                smallBody.GetComponent<
                    CelestialSmallBodyInstance>();

            if (instance == null ||
                !instance.IsPoolManaged ||
                !TryGetPool(
                    instance.SourceToolId,
                    out var pool))
            {
                Destroy(
                    smallBody);
                return;
            }

            var slot =
                FindSlot(
                    pool,
                    instance);

            if (slot == null ||
                pool.Configuration.RegenerateAfterCheckout)
            {
                instance.ClearPoolMetadata();
                Destroy(
                    smallBody);
                return;
            }

            slot.CheckedOut = false;
            smallBody.SetActive(
                false);
            smallBody.transform.SetParent(
                EnsurePoolRoot(),
                false);
        }

        [ContextMenu("Clear Available Small Bodies")]
        public void ClearReadySmallBodies()
        {
            foreach (var pool in poolsByToolId.Values)
            {
                for (var index =
                        pool.Slots.Count - 1;
                    index >= 0;
                    index--)
                {
                    var slot =
                        pool.Slots[index];

                    if (slot.CheckedOut ||
                        slot.Instance == null)
                    {
                        continue;
                    }

                    Destroy(
                        slot.Instance.gameObject);
                    pool.Slots.RemoveAt(
                        index);
                }
            }

            RefreshDiagnostics();
        }

        private void ResolvePools()
        {
            poolsByToolId.Clear();
            registeredToolSources.Clear();
            lastError = string.Empty;

            foreach (var configuration in toolPools)
            {
                if (configuration == null)
                {
                    continue;
                }

                configuration.Validate();
                var tool =
                    configuration.ToolSource as
                        ICelestialSmallBodyGenerationTool;

                if (tool == null ||
                    string.IsNullOrWhiteSpace(
                        tool.ToolId) ||
                    tool.LodCount <= 0)
                {
                    continue;
                }

                configuration.ClampToToolLods(
                    tool.LodCount);
                registeredToolSources.Add(
                    configuration.ToolSource);

                if (poolsByToolId.ContainsKey(
                        tool.ToolId))
                {
                    RecordError(
                        $"More than one small-body tool uses ID '{tool.ToolId}'.");
                    continue;
                }

                poolsByToolId.Add(
                    tool.ToolId,
                    new RuntimePool
                    {
                        Configuration = configuration,
                        Tool = tool
                    });
            }

            poolsResolved = true;
        }

        private void MaintainPools()
        {
            var startsRemaining =
                maximumGenerationStartsPerFrame;

            foreach (var pool in poolsByToolId.Values)
            {
                startsRemaining =
                    CreateBaselineSlots(
                        pool,
                        startsRemaining);
            }

            foreach (var pool in poolsByToolId.Values)
            {
                startsRemaining =
                    UpgradeForWaitingRequests(
                        pool,
                        startsRemaining);
            }

            foreach (var pool in poolsByToolId.Values)
            {
                startsRemaining =
                    UpgradeToMinimumLod(
                        pool,
                        startsRemaining);
            }

            foreach (var pool in poolsByToolId.Values)
            {
                startsRemaining =
                    UpgradeInBackground(
                        pool,
                        startsRemaining);
            }
        }

        private int CreateBaselineSlots(
            RuntimePool pool,
            int startsRemaining)
        {
            while (startsRemaining > 0 &&
                pool.Slots.Count <
                    pool.Configuration.TargetReadyCount)
            {
                var slot =
                    new RuntimeSlot
                    {
                        Seed = NextPrewarmSeed()
                    };
                pool.Slots.Add(
                    slot);

                if (!TryStartLodGeneration(
                        pool,
                        slot,
                        CelestialSmallBodyLod.Billboard))
                {
                    pool.Slots.Remove(
                        slot);
                    break;
                }

                startsRemaining--;
            }

            return startsRemaining;
        }

        private int UpgradeForWaitingRequests(
            RuntimePool pool,
            int startsRemaining)
        {
            foreach (var waiting in
                pool.WaitingRequests)
            {
                if (startsRemaining <= 0)
                {
                    break;
                }

                var slot =
                    FindUpgradeableSlot(
                        pool,
                        waiting.Request);

                if (slot == null &&
                    waiting.Request.HasExplicitSeed)
                {
                    slot =
                        CreateExplicitSeedSlot(
                            pool,
                            waiting.Request.Seed,
                            ref startsRemaining);

                    // The new slot is still generating its billboard.
                    continue;
                }

                if (slot == null ||
                    !TryStartNextLodGeneration(
                        pool,
                        slot))
                {
                    continue;
                }

                startsRemaining--;
            }

            return startsRemaining;
        }

        private int UpgradeToMinimumLod(
            RuntimePool pool,
            int startsRemaining)
        {
            while (startsRemaining > 0)
            {
                var slot =
                    FindUpgradeableSlot(
                        pool,
                        pool.Configuration
                            .MinimumPrewarmLod);

                if (slot == null ||
                    !TryStartNextLodGeneration(
                        pool,
                        slot))
                {
                    break;
                }

                startsRemaining--;
            }

            return startsRemaining;
        }

        private int UpgradeInBackground(
            RuntimePool pool,
            int startsRemaining)
        {
            while (startsRemaining > 0)
            {
                var slot =
                    FindUpgradeableSlot(
                        pool,
                        pool.Configuration
                            .MaximumPrewarmLod);

                if (slot == null ||
                    !TryStartNextLodGeneration(
                        pool,
                        slot))
                {
                    break;
                }

                startsRemaining--;
            }

            return startsRemaining;
        }

        private bool TryStartNextLodGeneration(
            RuntimePool pool,
            RuntimeSlot slot)
        {
            if (slot == null ||
                slot.Instance == null ||
                slot.GenerationPending)
            {
                return false;
            }

            var nextLod =
                (CelestialSmallBodyLod)(
                    slot.Instance.HighestReadyLod + 1);
            return TryStartLodGeneration(
                pool,
                slot,
                nextLod);
        }

        private bool TryStartLodGeneration(
            RuntimePool pool,
            RuntimeSlot slot,
            CelestialSmallBodyLod lod)
        {
            if (slot == null ||
                slot.GenerationPending ||
                (int)lod >=
                    pool.Tool.LodCount ||
                !pool.Tool.CanGenerate(
                    new CelestialSmallBodyRequest(
                        pool.Tool.ToolId,
                        slot.Seed,
                        true,
                        lod)))
            {
                return false;
            }

            var request =
                new CelestialSmallBodyRequest(
                    pool.Tool.ToolId,
                    slot.Seed,
                    true,
                    lod);
            slot.GenerationPending = true;
            slot.PendingLod = lod;

            try
            {
                if (pool.Tool.TryBeginGeneration(
                        request,
                        result => HandleGenerationCompleted(
                            pool,
                            slot,
                            result)))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                RecordError(
                    $"Small-body tool '{pool.Tool.ToolId}' threw while starting generation: {exception.Message}");
            }

            slot.GenerationPending = false;
            return false;
        }

        private void HandleGenerationCompleted(
            RuntimePool pool,
            RuntimeSlot slot,
            CelestialSmallBodyGenerationResult result)
        {
            if (slot == null)
            {
                if (result.Instance != null)
                {
                    Destroy(
                        result.Instance);
                }

                return;
            }

            slot.GenerationPending = false;

            if (!result.Succeeded)
            {
                RecordError(
                    string.IsNullOrWhiteSpace(
                        result.Error)
                        ? $"Small-body tool '{pool.Tool.ToolId}' did not return an LOD representation."
                        : result.Error);
                RefreshDiagnostics();
                return;
            }

            if (slot.Instance == null)
            {
                var container =
                    new GameObject(
                        $"Pooled Small Body {pool.Tool.ToolId} {slot.Seed}");
                slot.Instance =
                    container.AddComponent<
                        CelestialSmallBodyInstance>();
                slot.Instance.ConfigurePoolMetadata(
                    pool.Tool.ToolId,
                    slot.Seed);
                container.SetActive(
                    false);
                container.transform.SetParent(
                    EnsurePoolRoot(),
                    false);
            }

            if (!slot.Instance.TryAddLodRepresentation(
                    result.Request.DesiredLod,
                    result.Instance))
            {
                Destroy(
                    result.Instance);
            }

            FulfillWaitingRequests(
                pool);
            RefreshDiagnostics();
        }

        private void FulfillWaitingRequests(
            RuntimePool pool)
        {
            for (var index = 0;
                index < pool.WaitingRequests.Count;)
            {
                var waiting =
                    pool.WaitingRequests[index];

                if (!TryAcquireReady(
                        waiting.Request,
                        out var body))
                {
                    index++;
                    continue;
                }

                pool.WaitingRequests.RemoveAt(
                    index);
                waiting.Completed?.Invoke(
                    body);
            }
        }

        private RuntimeSlot FindBestAvailableSlot(
            RuntimePool pool,
            CelestialSmallBodyRequest request)
        {
            RuntimeSlot best = null;
            var bestLod =
                int.MaxValue;

            foreach (var slot in
                pool.Slots)
            {
                if (slot.CheckedOut ||
                    slot.GenerationPending ||
                    slot.Instance == null ||
                    !slot.Instance.HasAtLeastLod(
                        request.DesiredLod) ||
                    (request.HasExplicitSeed &&
                     slot.Seed != request.Seed))
                {
                    continue;
                }

                var highestLod =
                    slot.Instance.HighestReadyLod;

                if (highestLod < bestLod)
                {
                    best = slot;
                    bestLod = highestLod;
                }
            }

            return best;
        }

        private RuntimeSlot FindUpgradeableSlot(
            RuntimePool pool,
            CelestialSmallBodyRequest request)
        {
            RuntimeSlot best = null;
            var bestLod =
                int.MaxValue;

            foreach (var slot in
                pool.Slots)
            {
                if (slot.CheckedOut ||
                    slot.GenerationPending ||
                    slot.Instance == null ||
                    slot.Instance.HighestReadyLod >=
                        (int)request.DesiredLod ||
                    (request.HasExplicitSeed &&
                     slot.Seed != request.Seed) ||
                    slot.Instance.HighestReadyLod <
                        (int)CelestialSmallBodyLod.Billboard)
                {
                    continue;
                }

                if (slot.Instance.HighestReadyLod <
                    bestLod)
                {
                    best = slot;
                    bestLod =
                        slot.Instance.HighestReadyLod;
                }
            }

            return best;
        }

        private RuntimeSlot CreateExplicitSeedSlot(
            RuntimePool pool,
            uint seed,
            ref int startsRemaining)
        {
            if (startsRemaining <= 0 ||
                (int)CelestialSmallBodyLod.Billboard >=
                    pool.Tool.LodCount)
            {
                return null;
            }

            foreach (var existing in pool.Slots)
            {
                if (existing.Seed == seed)
                {
                    return existing;
                }
            }

            var slot =
                new RuntimeSlot
                {
                    Seed = seed
                };
            pool.Slots.Add(
                slot);

            if (!TryStartLodGeneration(
                    pool,
                    slot,
                    CelestialSmallBodyLod.Billboard))
            {
                pool.Slots.Remove(
                    slot);
                return null;
            }

            startsRemaining--;
            return slot;
        }

        private void CheckoutSlot(
            RuntimePool pool,
            RuntimeSlot slot,
            out GameObject smallBody)
        {
            smallBody =
                slot.Instance != null
                    ? slot.Instance.gameObject
                    : null;

            if (smallBody == null)
            {
                return;
            }

            if (pool.Configuration.RegenerateAfterCheckout)
            {
                pool.Slots.Remove(
                    slot);
            }
            else
            {
                slot.CheckedOut = true;
            }

            smallBody.transform.SetParent(
                null,
                true);
            smallBody.SetActive(
                true);
        }

        private RuntimeSlot FindSlot(
            RuntimePool pool,
            CelestialSmallBodyInstance instance)
        {
            foreach (var slot in
                pool.Slots)
            {
                if (slot.Instance == instance)
                {
                    return slot;
                }
            }

            return null;
        }

        private bool TryGetPool(
            string toolId,
            out RuntimePool pool)
        {
            if (!poolsResolved)
            {
                ResolvePools();
            }

            if (string.IsNullOrWhiteSpace(
                    toolId) ||
                !poolsByToolId.TryGetValue(
                    toolId,
                    out pool))
            {
                pool = null;
                return false;
            }

            return true;
        }

        private Transform EnsurePoolRoot()
        {
            if (poolRoot != null)
            {
                return poolRoot;
            }

            var root =
                new GameObject(
                    "Pooled Celestial Small Bodies");
            root.transform.SetParent(
                transform,
                false);
            poolRoot =
                root.transform;
            return poolRoot;
        }

        private uint NextPrewarmSeed()
        {
            prewarmSeed =
                unchecked(
                    prewarmSeed *
                    747796405u +
                    2891336453u);
            return prewarmSeed;
        }

        private void RefreshDiagnostics()
        {
            readyObjectCount = 0;
            pendingGenerationCount = 0;
            waitingRequestCount = 0;
            containerCount = 0;

            foreach (var pool in poolsByToolId.Values)
            {
                containerCount +=
                    pool.Slots.Count;
                waitingRequestCount +=
                    pool.WaitingRequests.Count;

                foreach (var slot in
                    pool.Slots)
                {
                    if (slot.GenerationPending)
                    {
                        pendingGenerationCount++;
                    }

                    if (!slot.CheckedOut &&
                        slot.Instance != null &&
                        slot.Instance.HasAtLeastLod(
                            CelestialSmallBodyLod.Billboard))
                    {
                        readyObjectCount++;
                    }
                }
            }
        }

        private void RecordError(
            string error)
        {
            lastError = error;
            Debug.LogWarning(
                error,
                this);
        }
    }
}
