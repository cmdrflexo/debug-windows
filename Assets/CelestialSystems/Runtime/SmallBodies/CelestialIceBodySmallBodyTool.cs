/*
 * Asynchronously prepares generic icy small-body meshes for the generic
 * pool. Ring Object Set families currently provide the material and scale
 * data; mesh generation is fully project-owned.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public class CelestialIceBodySmallBodyTool :
        MonoBehaviour,
        ICelestialSmallBodyGenerationTool
    {
        private sealed class PendingGeneration
        {
            public CelestialSmallBodyRequest Request;
            public Action<CelestialSmallBodyGenerationResult> Completed;
        }

        private sealed class ActiveGeneration
        {
            public PendingGeneration Pending;
            public float NominalDiameterMeters;
            public Material Material;
            public CancellationTokenSource Cancellation;
            public Task<CelestialIceBodyMeshData> Task;
        }

        [Header("Identity")]
        [SerializeField]
        private string toolId =
            "ice-rock";

        [SerializeField]
        [Range(1, 5)]
        [Tooltip("Includes LOD 0, which is the billboard representation.")]
        private int lodCount = 5;

        [Header("Ring Family Adapter")]
        [SerializeField]
        private CelestialRingObjectSet ringObjectSet;

        [SerializeField]
        private CelestialRingObjectFamilyKind familyKind =
            CelestialRingObjectFamilyKind.IceChunk;

        [SerializeField]
        [Tooltip("Optional material for LOD 0. A family material is used when empty.")]
        private Material billboardMaterial;

        [Header("Ice-Body Mesh Generation")]
        [SerializeField]
        private CelestialIceBodyGenerationSettings iceBodySettings =
            new CelestialIceBodyGenerationSettings();

        [Header("Background Scheduling")]
        [SerializeField]
        [Min(1)]
        private int maximumConcurrentJobs = 1;

        [SerializeField]
        [Min(1)]
        [Tooltip("Limits Unity Mesh uploads per frame after worker jobs finish.")]
        private int maximumMeshUploadsPerFrame = 1;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int pendingGenerationCount;

        [SerializeField]
        private int activeWorkerCount;

        [SerializeField]
        private string lastError;

        private readonly Queue<PendingGeneration> pendingGenerations =
            new Queue<PendingGeneration>();
        private readonly List<ActiveGeneration> activeGenerations =
            new List<ActiveGeneration>();
        private Mesh billboardMesh;

        public string ToolId =>
            toolId;

        public int LodCount =>
            lodCount;

        public int PendingGenerationCount =>
            pendingGenerationCount;

        public int ActiveWorkerCount =>
            activeWorkerCount;

        public string LastError =>
            lastError;

        protected virtual void OnValidate()
        {
            toolId =
                toolId?.Trim() ??
                string.Empty;
            lodCount =
                Mathf.Clamp(
                    lodCount,
                    1,
                    5);
            maximumConcurrentJobs =
                Mathf.Max(
                    1,
                    maximumConcurrentJobs);
            maximumMeshUploadsPerFrame =
                Mathf.Max(
                    1,
                    maximumMeshUploadsPerFrame);
            iceBodySettings ??=
                new CelestialIceBodyGenerationSettings();
        }

        protected virtual void Update()
        {
            StartPendingWorkerJobs();
            CompleteFinishedWorkerJobs();
            pendingGenerationCount =
                pendingGenerations.Count;
            activeWorkerCount =
                activeGenerations.Count;
        }

        protected virtual void OnDisable()
        {
            while (pendingGenerations.Count > 0)
            {
                var pending =
                    pendingGenerations.Dequeue();
                CompleteFailure(
                    pending,
                    "The ice-rock small-body tool was disabled before generation started.");
            }

            foreach (var active in activeGenerations)
            {
                active.Cancellation.Cancel();
                CompleteFailure(
                    active.Pending,
                    "The ice-rock small-body tool was disabled before generation completed.");
                active.Cancellation.Dispose();
            }

            activeGenerations.Clear();
            pendingGenerationCount = 0;
            activeWorkerCount = 0;
        }

        protected virtual void OnDestroy()
        {
            if (billboardMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(
                    billboardMesh);
            }
            else
            {
                DestroyImmediate(
                    billboardMesh);
            }
        }

        public bool CanGenerate(
            CelestialSmallBodyRequest request)
        {
            return
                string.Equals(
                    request.ToolId,
                    ToolId,
                    StringComparison.Ordinal) &&
                (int)request.DesiredLod >= 0 &&
                (int)request.DesiredLod < lodCount &&
                TryGetFamily(
                    out _);
        }

        public bool TryBeginGeneration(
            CelestialSmallBodyRequest request,
            Action<CelestialSmallBodyGenerationResult> completed)
        {
            if (!CanGenerate(
                    request))
            {
                return false;
            }

            pendingGenerations.Enqueue(
                new PendingGeneration
                {
                    Request = request,
                    Completed = completed
                });
            pendingGenerationCount =
                pendingGenerations.Count;
            return true;
        }

        private void StartPendingWorkerJobs()
        {
            while (activeGenerations.Count <
                    maximumConcurrentJobs &&
                pendingGenerations.Count > 0)
            {
                var pending =
                    pendingGenerations.Dequeue();

                if (!TryGetFamily(
                        out var family))
                {
                    CompleteFailure(
                        pending,
                        "The ice-rock tool could not find its configured ring-object family.");
                    continue;
                }

                var diameter =
                    Mathf.Lerp(
                        family.MinimumDiameterMeters,
                        family.MaximumDiameterMeters,
                        HashToUnitFloat(
                            pending.Request.Seed));
                var material =
                    pending.Request.DesiredLod ==
                        CelestialSmallBodyLod.Billboard &&
                    billboardMaterial != null
                        ? billboardMaterial
                        : ResolveFamilyMaterial(
                            family);

                if (pending.Request.DesiredLod ==
                    CelestialSmallBodyLod.Billboard)
                {
                    CompleteBillboard(
                        pending,
                        diameter,
                        material);
                    continue;
                }

                var cancellation =
                    new CancellationTokenSource();
                var settings =
                    iceBodySettings
                        .CloneForDetail(
                            (int)pending.Request
                                .DesiredLod);

                activeGenerations.Add(
                    new ActiveGeneration
                    {
                        Pending = pending,
                        NominalDiameterMeters = diameter,
                        Material = material,
                        Cancellation = cancellation,
                        Task = CelestialIceBodyMeshGenerator
                            .GenerateAsync(
                                pending.Request.Seed,
                                settings,
                                cancellation.Token)
                    });
            }
        }

        private void CompleteFinishedWorkerJobs()
        {
            var uploadsThisFrame = 0;

            for (var index =
                    activeGenerations.Count - 1;
                index >= 0 &&
                    uploadsThisFrame <
                        maximumMeshUploadsPerFrame;
                index--)
            {
                var active =
                    activeGenerations[index];

                if (!active.Task.IsCompleted)
                {
                    continue;
                }

                activeGenerations.RemoveAt(
                    index);

                try
                {
                    var data =
                        active.Task
                            .GetAwaiter()
                            .GetResult();
                    var mesh =
                        CelestialIceBodyMeshGenerator
                            .CreateMesh(
                                data,
                                $"Ice Body {active.Pending.Request.Seed} LOD {(int)active.Pending.Request.DesiredLod}");
                    var body =
                        CreateMeshBody(
                            active.Pending.Request,
                            mesh,
                            active.Material,
                            active.NominalDiameterMeters);
                    lastError = string.Empty;
                    active.Pending.Completed?.Invoke(
                        new CelestialSmallBodyGenerationResult(
                            active.Pending.Request,
                            body));
                }
                catch (OperationCanceledException)
                {
                    CompleteFailure(
                        active.Pending,
                        "Ice-body mesh generation was cancelled.");
                }
                catch (Exception exception)
                {
                    CompleteFailure(
                        active.Pending,
                        exception.Message);
                }
                finally
                {
                    active.Cancellation.Dispose();
                }

                uploadsThisFrame++;
            }
        }

        private void CompleteBillboard(
            PendingGeneration pending,
            float diameter,
            Material material)
        {
            var body =
                new GameObject(
                    $"Ice Body {pending.Request.Seed} Billboard");
            var filter =
                body.AddComponent<MeshFilter>();
            var renderer =
                body.AddComponent<MeshRenderer>();

            filter.sharedMesh =
                GetBillboardMesh();
            renderer.sharedMaterial =
                material;
            body.AddComponent<
                CelestialSmallBodyBillboard>();
            body.transform.localScale =
                Vector3.one * diameter;
            lastError = string.Empty;
            pending.Completed?.Invoke(
                new CelestialSmallBodyGenerationResult(
                    pending.Request,
                    body));
        }

        private static GameObject CreateMeshBody(
            CelestialSmallBodyRequest request,
            Mesh mesh,
            Material material,
            float nominalDiameterMeters)
        {
            var body =
                new GameObject(
                    $"Ice Body {request.Seed} LOD {(int)request.DesiredLod}");
            var filter =
                body.AddComponent<MeshFilter>();
            var renderer =
                body.AddComponent<MeshRenderer>();

            filter.sharedMesh =
                mesh;
            renderer.sharedMaterial =
                material;
            body.AddComponent<
                CelestialIceBodyGeneratedMesh>();

            var maximumExtent =
                Mathf.Max(
                    mesh.bounds.size.x,
                    Mathf.Max(
                        mesh.bounds.size.y,
                        mesh.bounds.size.z));
            body.transform.localScale =
                Vector3.one *
                nominalDiameterMeters /
                Mathf.Max(
                    0.0001f,
                    maximumExtent);
            return body;
        }

        private bool TryGetFamily(
            out CelestialRingObjectFamily family)
        {
            family = null;

            if (ringObjectSet == null)
            {
                return false;
            }

            foreach (var candidate in
                ringObjectSet.Families)
            {
                if (candidate != null &&
                    candidate.Kind == familyKind)
                {
                    family =
                        candidate;
                    return true;
                }
            }

            return false;
        }

        private static Material ResolveFamilyMaterial(
            CelestialRingObjectFamily family)
        {
            return family.MaterialVariants.Count > 0
                ? family.MaterialVariants[0]
                : null;
        }

        private Mesh GetBillboardMesh()
        {
            if (billboardMesh != null)
            {
                return billboardMesh;
            }

            billboardMesh =
                new Mesh
                {
                    name = "Celestial Ice Body Billboard"
                };
            billboardMesh.vertices =
                new[]
                {
                    new Vector3(-0.5f, -0.5f, 0.0f),
                    new Vector3(0.5f, -0.5f, 0.0f),
                    new Vector3(0.5f, 0.5f, 0.0f),
                    new Vector3(-0.5f, 0.5f, 0.0f)
                };
            billboardMesh.uv =
                new[]
                {
                    new Vector2(0.0f, 0.0f),
                    new Vector2(1.0f, 0.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(0.0f, 1.0f)
                };
            billboardMesh.triangles =
                new[]
                {
                    0, 1, 2,
                    0, 2, 3
                };
            billboardMesh.RecalculateBounds();
            return billboardMesh;
        }

        private static float HashToUnitFloat(
            uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return
                (value & 0x00FFFFFFu) /
                16777215.0f;
        }

        private void CompleteFailure(
            PendingGeneration pending,
            string error)
        {
            lastError =
                string.IsNullOrWhiteSpace(
                    error)
                    ? "Ice-body generation failed."
                    : error;
            pending.Completed?.Invoke(
                new CelestialSmallBodyGenerationResult(
                    pending.Request,
                    null,
                    lastError));
        }
    }
}
