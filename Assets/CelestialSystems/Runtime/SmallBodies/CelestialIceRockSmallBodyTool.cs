/*
 * Queued small-body tool that adapts an existing Ring Object Set and its
 * procedural ring asteroid source. LOD 0 is a camera-facing billboard;
 * LODs 1-4 resolve the source at matching mesh detail.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialIceRockSmallBodyTool :
        MonoBehaviour,
        ICelestialSmallBodyGenerationTool
    {
        private sealed class PendingGeneration
        {
            public CelestialSmallBodyRequest Request;
            public Action<CelestialSmallBodyGenerationResult> Completed;
        }

        [Header("Identity")]
        [SerializeField]
        private string toolId =
            "ice-rock";

        [SerializeField]
        [Range(1, 5)]
        [Tooltip("Includes LOD 0, which is the billboard representation.")]
        private int lodCount = 5;

        [Header("Ring Source")]
        [SerializeField]
        private CelestialRingObjectSet ringObjectSet;

        [SerializeField]
        private CelestialRingObjectFamilyKind familyKind =
            CelestialRingObjectFamilyKind.IceChunk;

        [SerializeField]
        [Tooltip("Optional direct source. When empty, the selected family source is used.")]
        private CelestialRingAsteroidBodySource asteroidBodySource;

        [SerializeField]
        [Tooltip("Optional material for LOD 0. A family material is used when empty.")]
        private Material billboardMaterial;

        [Header("Generation Scheduling")]
        [SerializeField]
        [Min(1)]
        [Tooltip("Limits completed generation jobs per frame. Jobs are queued by the pool manager and never generated during the request call.")]
        private int maximumCompletionsPerFrame = 1;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int pendingGenerationCount;

        [SerializeField]
        private string lastError;

        private readonly Queue<PendingGeneration> pendingGenerations =
            new Queue<PendingGeneration>();
        private Mesh billboardMesh;

        public string ToolId =>
            toolId;

        public int LodCount =>
            lodCount;

        public int PendingGenerationCount =>
            pendingGenerationCount;

        public string LastError =>
            lastError;

        private void OnValidate()
        {
            toolId =
                toolId?.Trim() ??
                string.Empty;
            lodCount =
                Mathf.Clamp(
                    lodCount,
                    1,
                    5);
            maximumCompletionsPerFrame =
                Mathf.Max(
                    1,
                    maximumCompletionsPerFrame);
        }

        private void Update()
        {
            var completedThisFrame = 0;

            while (completedThisFrame <
                    maximumCompletionsPerFrame &&
                pendingGenerations.Count > 0)
            {
                var pending =
                    pendingGenerations.Dequeue();
                pendingGenerationCount =
                    pendingGenerations.Count;
                CompleteGeneration(
                    pending);
                completedThisFrame++;
            }
        }

        private void OnDisable()
        {
            while (pendingGenerations.Count > 0)
            {
                var pending =
                    pendingGenerations.Dequeue();
                pending.Completed?.Invoke(
                    new CelestialSmallBodyGenerationResult(
                        pending.Request,
                        null,
                        "The ice-rock small-body tool was disabled before generation completed."));
            }

            pendingGenerationCount = 0;
        }

        private void OnDestroy()
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

        private void CompleteGeneration(
            PendingGeneration pending)
        {
            try
            {
                if (!TryGetFamily(
                        out var family))
                {
                    CompleteFailure(
                        pending,
                        "The ice-rock tool could not find its configured ring-object family.");
                    return;
                }

                var body =
                    CreateSmallBody(
                        pending.Request,
                        family);

                if (body == null)
                {
                    CompleteFailure(
                        pending,
                        lastError);
                    return;
                }

                lastError = string.Empty;
                pending.Completed?.Invoke(
                    new CelestialSmallBodyGenerationResult(
                        pending.Request,
                        body));
            }
            catch (Exception exception)
            {
                CompleteFailure(
                    pending,
                    exception.Message);
            }
        }

        private GameObject CreateSmallBody(
            CelestialSmallBodyRequest request,
            CelestialRingObjectFamily family)
        {
            var diameter =
                Mathf.Lerp(
                    family.MinimumDiameterMeters,
                    family.MaximumDiameterMeters,
                    HashToUnitFloat(
                        request.Seed));
            var body =
                new GameObject(
                    $"Ice Rock {request.Seed} LOD {(int)request.DesiredLod}");
            var filter =
                body.AddComponent<MeshFilter>();
            var renderer =
                body.AddComponent<MeshRenderer>();

            if (request.DesiredLod ==
                CelestialSmallBodyLod.Billboard)
            {
                filter.sharedMesh =
                    GetBillboardMesh();
                renderer.sharedMaterial =
                    ResolveBillboardMaterial(
                        family);
                body.AddComponent<
                    CelestialSmallBodyBillboard>();
                body.transform.localScale =
                    Vector3.one * diameter;
                return body;
            }

            var sourceRequest =
                new CelestialRingBodyRequest
                {
                    Seed = request.Seed,
                    FamilyKind = family.Kind,
                    NominalDiameterMeters = diameter
                };
            var resolved = default(
                CelestialRingResolvedBody);
            var source =
                asteroidBodySource != null
                    ? asteroidBodySource
                    : family.BodySource as
                        CelestialRingAsteroidBodySource;
            var resolvedBySource =
                source != null
                    ? source.TryResolve(
                        sourceRequest,
                        (int)request.DesiredLod,
                        out resolved)
                    : family.BodySource != null &&
                        family.BodySource.TryResolve(
                            sourceRequest,
                            out resolved);

            if (!resolvedBySource ||
                resolved.Mesh == null)
            {
                Destroy(
                    body);
                lastError =
                    "The configured ice-rock source did not resolve a mesh.";
                return null;
            }

            filter.sharedMesh =
                resolved.Mesh;
            renderer.sharedMaterial =
                resolved.Material != null
                    ? resolved.Material
                    : ResolveFamilyMaterial(
                        family);
            body.transform.localScale =
                Vector3.one *
                diameter *
                resolved.SafeDiameterMultiplier;
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

        private Material ResolveBillboardMaterial(
            CelestialRingObjectFamily family)
        {
            return billboardMaterial != null
                ? billboardMaterial
                : ResolveFamilyMaterial(
                    family);
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
                    name = "Celestial Small Body Billboard"
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
                    ? "Ice-rock generation failed."
                    : error;
            pending.Completed?.Invoke(
                new CelestialSmallBodyGenerationResult(
                    pending.Request,
                    null,
                    lastError));
        }
    }
}
