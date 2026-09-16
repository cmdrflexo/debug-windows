/*
 * Maintains ready-to-use generic celestial small bodies for any attached
 * generation tool. Tools own their generation implementation; this manager
 * owns requests, ready pools, demand callbacks, and prewarm/refill policy.
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
            [Tooltip("How many ready, unused bodies this tool should keep available.")]
            private int targetReadyCount = 8;

            [SerializeField]
            [Tooltip("Lowest LOD included in background prewarming. LOD 0 is normally the billboard representation.")]
            private CelestialSmallBodyLod minimumPrewarmLod =
                CelestialSmallBodyLod.Billboard;

            [SerializeField]
            [Tooltip("Highest LOD included in background prewarming.")]
            private CelestialSmallBodyLod maximumPrewarmLod =
                CelestialSmallBodyLod.Detail2;

            [SerializeField]
            [Tooltip("Checked-out objects are treated as consumed and replaced instead of returned to the ready pool.")]
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

        private sealed class RuntimePool
        {
            public ToolPoolConfiguration Configuration;
            public ICelestialSmallBodyGenerationTool Tool;
            public readonly Queue<GameObject> ReadyObjects =
                new Queue<GameObject>();
            public readonly List<WaitingRequest> WaitingRequests =
                new List<WaitingRequest>();
            public int PendingGenerationCount;
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
        [Tooltip("Optional parent used only while a body is waiting in a ready pool.")]
        private Transform poolRoot;

        [SerializeField]
        [Min(1)]
        [Tooltip("Upper limit on new background generation requests started each frame.")]
        private int maximumGenerationStartsPerFrame = 1;

        [SerializeField]
        [Tooltip("Seed used for non-deterministic prewarmed bodies.")]
        private uint prewarmSeed = 1u;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int readyObjectCount;

        [SerializeField]
        private int pendingGenerationCount;

        [SerializeField]
        private int waitingRequestCount;

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
                foreach (var body in pool.ReadyObjects)
                {
                    if (body != null)
                    {
                        Destroy(body);
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

            if (!TryTakeMatchingReadyObject(
                    pool,
                    request,
                    out smallBody))
            {
                return false;
            }

            PrepareForCheckout(
                pool,
                smallBody);
            return true;
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

            if (!TryStartGeneration(
                    pool,
                    request))
            {
                var waiting =
                    pool.WaitingRequests[
                        pool.WaitingRequests.Count - 1];
                pool.WaitingRequests.RemoveAt(
                    pool.WaitingRequests.Count - 1);
                waiting.Completed?.Invoke(
                    null);
            }
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

            if (pool.Configuration.RegenerateAfterCheckout)
            {
                instance.ClearPoolMetadata();
                Destroy(
                    smallBody);
                return;
            }

            smallBody.SetActive(
                false);
            smallBody.transform.SetParent(
                EnsurePoolRoot(),
                false);
            pool.ReadyObjects.Enqueue(
                smallBody);
        }

        [ContextMenu("Clear Ready Small Bodies")]
        public void ClearReadySmallBodies()
        {
            foreach (var pool in poolsByToolId.Values)
            {
                while (pool.ReadyObjects.Count > 0)
                {
                    var body =
                        pool.ReadyObjects.Dequeue();

                    if (body != null)
                    {
                        Destroy(
                            body);
                    }
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
            RefreshDiagnostics();
        }

        private void MaintainPools()
        {
            var startsRemaining =
                maximumGenerationStartsPerFrame;

            foreach (var pool in poolsByToolId.Values)
            {
                while (startsRemaining > 0 &&
                    pool.ReadyObjects.Count +
                        pool.PendingGenerationCount <
                    pool.Configuration.TargetReadyCount)
                {
                    var request =
                        new CelestialSmallBodyRequest(
                            pool.Tool.ToolId,
                            NextPrewarmSeed(),
                            false,
                            SelectPrewarmLod(
                                pool.Configuration));

                    if (!TryStartGeneration(
                            pool,
                            request))
                    {
                        break;
                    }

                    startsRemaining--;
                }
            }
        }

        private bool TryStartGeneration(
            RuntimePool pool,
            CelestialSmallBodyRequest request)
        {
            if (pool.Tool == null ||
                !pool.Tool.CanGenerate(
                    request))
            {
                return false;
            }

            pool.PendingGenerationCount++;

            try
            {
                if (pool.Tool.TryBeginGeneration(
                        request,
                        result => HandleGenerationCompleted(
                            pool,
                            result)))
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                RecordError(
                    $"Small-body tool '{request.ToolId}' threw while starting generation: {exception.Message}");
            }

            pool.PendingGenerationCount =
                Mathf.Max(
                    0,
                    pool.PendingGenerationCount - 1);
            return false;
        }

        private void HandleGenerationCompleted(
            RuntimePool pool,
            CelestialSmallBodyGenerationResult result)
        {
            pool.PendingGenerationCount =
                Mathf.Max(
                    0,
                    pool.PendingGenerationCount - 1);

            if (!result.Succeeded)
            {
                RecordError(
                    string.IsNullOrWhiteSpace(
                        result.Error)
                        ? $"Small-body tool '{pool.Tool.ToolId}' did not return an instance."
                        : result.Error);
                FulfillFailedWaitingRequest(
                    pool,
                    result.Request);
                RefreshDiagnostics();
                return;
            }

            var body =
                result.Instance;
            var instance =
                body.GetComponent<
                    CelestialSmallBodyInstance>();

            if (instance == null)
            {
                instance =
                    body.AddComponent<
                        CelestialSmallBodyInstance>();
            }

            instance.ConfigurePoolMetadata(
                result.Request);

            if (TryFulfillWaitingRequest(
                    pool,
                    result.Request,
                    body))
            {
                RefreshDiagnostics();
                return;
            }

            body.SetActive(
                false);
            body.transform.SetParent(
                EnsurePoolRoot(),
                false);
            pool.ReadyObjects.Enqueue(
                body);
            RefreshDiagnostics();
        }

        private bool TryFulfillWaitingRequest(
            RuntimePool pool,
            CelestialSmallBodyRequest generatedRequest,
            GameObject body)
        {
            for (var index = 0;
                index < pool.WaitingRequests.Count;
                index++)
            {
                var waiting =
                    pool.WaitingRequests[index];

                if (!CanSatisfy(
                        generatedRequest,
                        waiting.Request))
                {
                    continue;
                }

                pool.WaitingRequests.RemoveAt(
                    index);
                PrepareForCheckout(
                    pool,
                    body);
                waiting.Completed?.Invoke(
                    body);
                return true;
            }

            return false;
        }

        private void FulfillFailedWaitingRequest(
            RuntimePool pool,
            CelestialSmallBodyRequest failedRequest)
        {
            for (var index = 0;
                index < pool.WaitingRequests.Count;
                index++)
            {
                var waiting =
                    pool.WaitingRequests[index];

                if (!CanSatisfy(
                        failedRequest,
                        waiting.Request))
                {
                    continue;
                }

                pool.WaitingRequests.RemoveAt(
                    index);
                waiting.Completed?.Invoke(
                    null);
                return;
            }
        }

        private bool TryTakeMatchingReadyObject(
            RuntimePool pool,
            CelestialSmallBodyRequest request,
            out GameObject body)
        {
            body = null;
            var count =
                pool.ReadyObjects.Count;

            for (var index = 0;
                index < count;
                index++)
            {
                var candidate =
                    pool.ReadyObjects.Dequeue();

                if (candidate == null)
                {
                    continue;
                }

                var instance =
                    candidate.GetComponent<
                        CelestialSmallBodyInstance>();

                if (body == null &&
                    instance != null &&
                    CanSatisfy(
                        new CelestialSmallBodyRequest(
                            instance.SourceToolId,
                            instance.Seed,
                            true,
                            instance.GeneratedLod),
                        request))
                {
                    body = candidate;
                    continue;
                }

                pool.ReadyObjects.Enqueue(
                    candidate);
            }

            return body != null;
        }

        private static bool CanSatisfy(
            CelestialSmallBodyRequest available,
            CelestialSmallBodyRequest requested)
        {
            return
                string.Equals(
                    available.ToolId,
                    requested.ToolId,
                    StringComparison.Ordinal) &&
                (!requested.HasExplicitSeed ||
                    available.Seed == requested.Seed) &&
                (int)available.DesiredLod >=
                    (int)requested.DesiredLod;
        }

        private void PrepareForCheckout(
            RuntimePool pool,
            GameObject body)
        {
            body.transform.SetParent(
                null,
                true);
            body.SetActive(
                true);

            // This setting means the caller owns the instance for the rest
            // of its life. The manager immediately refills the missing slot.
            if (pool.Configuration.RegenerateAfterCheckout)
            {
                MaintainPools();
            }
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

        private CelestialSmallBodyLod SelectPrewarmLod(
            ToolPoolConfiguration configuration)
        {
            var minimum =
                (int)configuration.MinimumPrewarmLod;
            var maximum =
                (int)configuration.MaximumPrewarmLod;
            var span =
                Mathf.Max(
                    1,
                    maximum - minimum + 1);
            var selected =
                minimum +
                (int)(NextPrewarmSeed() % (uint)span);
            return
                (CelestialSmallBodyLod)selected;
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

            foreach (var pool in poolsByToolId.Values)
            {
                readyObjectCount +=
                    pool.ReadyObjects.Count;
                pendingGenerationCount +=
                    pool.PendingGenerationCount;
                waitingRequestCount +=
                    pool.WaitingRequests.Count;
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
