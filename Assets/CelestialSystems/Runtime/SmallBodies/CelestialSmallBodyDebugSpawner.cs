/*
 * Scene-owned manual requester for pooled celestial small bodies. It is useful
 * for testing a tool and pool without enabling a ring/object streaming system.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using SpaceGraphicsToolkit;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyDebugSpawner :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private CelestialSmallBodyPoolManager poolManager;

        [SerializeField]
        private Camera observerCamera;

        [SerializeField]
        [Tooltip("Optional explicit frame. The active scene frame is used when empty.")]
        private UniverseFrameController universeFrame;

        [Header("Request")]
        [SerializeField]
        private string toolId =
            "ice-rock";

        [SerializeField]
        private CelestialSmallBodyLod desiredLod =
            CelestialSmallBodyLod.Lod2;

        [SerializeField]
        private bool useExplicitSeed;

        [SerializeField]
        private uint seed = 12u;

        [Header("Spawn")]
        [SerializeField]
        [Min(0.1f)]
        private float spawnDistanceMeters = 20.0f;

        [SerializeField]
        [Tooltip("Initialize the rock with and continuously match the nearest body's velocity. Disable to leave it fixed in universe space.")]
        private bool initializeWithReferenceVelocity = true;

        [SerializeField]
        private InputAction spawnAction =
            new InputAction(
                "Spawn Small Body",
                InputActionType.Button,
                "<Keyboard>/j");

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int activeSpawnCount;

        [SerializeField]
        private string lastStatus;

        private readonly List<GameObject> spawnedBodies =
            new List<GameObject>();

        private void OnEnable()
        {
            spawnAction?.Enable();
        }

        private void OnDisable()
        {
            spawnAction?.Disable();
        }

        private void OnValidate()
        {
            toolId =
                toolId?.Trim() ??
                string.Empty;
            spawnDistanceMeters =
                Mathf.Max(
                    0.1f,
                    spawnDistanceMeters);
        }

        private void Update()
        {
            if (spawnAction != null &&
                spawnAction.WasPressedThisFrame())
            {
                SpawnSmallBody();
            }
        }

        [ContextMenu("Spawn Small Body")]
        public void SpawnSmallBody()
        {
            if (!Application.isPlaying)
            {
                lastStatus =
                    "Small-body pool requests require play mode.";
                return;
            }

            poolManager ??=
                FindFirstObjectByType<
                    CelestialSmallBodyPoolManager>();

            var camera =
                observerCamera != null
                    ? observerCamera
                    : Camera.main;

            if (poolManager == null ||
                camera == null ||
                string.IsNullOrWhiteSpace(
                    toolId))
            {
                lastStatus =
                    "Assign a pool manager, camera, and tool ID.";
                return;
            }

            var request =
                new CelestialSmallBodyRequest(
                    toolId,
                    useExplicitSeed
                        ? seed
                        : NextSeed(),
                    useExplicitSeed,
                    desiredLod);

            poolManager.RequestSmallBody(
                request,
                body => HandleSmallBodyReady(
                    body,
                    camera));
            lastStatus =
                $"Requested {toolId} LOD {(int)desiredLod}.";
        }

        [ContextMenu("Clear Spawned Small Bodies")]
        public void ClearSpawnedSmallBodies()
        {
            poolManager ??=
                FindFirstObjectByType<
                    CelestialSmallBodyPoolManager>();

            foreach (var body in spawnedBodies)
            {
                if (body == null)
                {
                    continue;
                }

                if (poolManager != null)
                {
                    poolManager.ReturnToPool(
                        body);
                }
                else
                {
                    Destroy(
                        body);
                }
            }

            spawnedBodies.Clear();
            activeSpawnCount = 0;
        }

        private void HandleSmallBodyReady(
            GameObject body,
            Camera camera)
        {
            if (body == null ||
                camera == null)
            {
                lastStatus =
                    "The requested small body was not generated.";
                return;
            }

            body.name =
                $"Debug Small Body {toolId} LOD {(int)desiredLod}";
            body.transform.position =
                camera.transform.position +
                camera.transform.forward *
                spawnDistanceMeters;
            body.transform.rotation =
                Random.rotation;

            if (UniverseTrackedObject
                .TryGetUniversePositionFromScenePosition(
                    GetUniverseFrame(),
                    body.transform.position,
                    out var universePosition))
            {
                InitializeUniverseMotion(
                    body,
                    camera,
                    universePosition);
            }

            spawnedBodies.Add(
                body);
            activeSpawnCount =
                spawnedBodies.Count;
            lastStatus =
                $"Spawned {body.name}.";
        }

        private void InitializeUniverseMotion(
            GameObject body,
            Camera camera,
            UniversePosition universePosition)
        {
            if (body.GetComponent<SgtFloatingObject>() ==
                null)
            {
                body.AddComponent<SgtFloatingObject>();
            }

            var motion =
                body.GetComponent<
                    UniverseVelocityMotion>();

            if (motion == null)
            {
                motion =
                    body.AddComponent<
                        UniverseVelocityMotion>();
            }

            var velocity =
                DoubleVector3.zero;
            CelestialBodyRuntimeContext referenceBody =
                null;

            if (initializeWithReferenceVelocity &&
                TryFindNearestBody(
                    camera,
                    out var referenceMotion,
                    out referenceBody))
            {
                velocity =
                    referenceMotion
                        .LinearVelocityMetersPerSecond;
            }

            motion.Initialize(
                new UniverseMotionState(
                    universePosition,
                    body.transform.rotation,
                    velocity,
                    DoubleVector3.zero));
            motion.SetVelocityReference(
                referenceBody,
                initializeWithReferenceVelocity &&
                referenceBody != null);
        }

        private bool TryFindNearestBody(
            Camera camera,
            out UniverseMotionState motion,
            out CelestialBodyRuntimeContext body)
        {
            motion = default;
            body = null;

            if (camera == null ||
                !UniverseTrackedObject
                    .TryGetUniversePositionFromScenePosition(
                        GetUniverseFrame(),
                        camera.transform.position,
                        out var cameraPosition))
            {
                return false;
            }

            var closestDistanceSquared =
                double.PositiveInfinity;

            foreach (var candidate in
                CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (candidate == null ||
                    !candidate.TryGetMotionState(
                        out var candidateMotion) ||
                    !cameraPosition.TryGetOffsetMetersFrom(
                        candidateMotion.Position,
                        out var offsetMeters))
                {
                    continue;
                }

                var distanceSquared =
                    offsetMeters.x * offsetMeters.x +
                    offsetMeters.y * offsetMeters.y +
                    offsetMeters.z * offsetMeters.z;

                if (double.IsNaN(
                        distanceSquared) ||
                    double.IsInfinity(
                        distanceSquared) ||
                    distanceSquared >=
                        closestDistanceSquared)
                {
                    continue;
                }

                closestDistanceSquared =
                    distanceSquared;
                motion =
                    candidateMotion;
                body =
                    candidate;
            }

            return body != null;
        }

        private UniverseFrameController GetUniverseFrame()
        {
            return universeFrame != null
                ? universeFrame
                : FindFirstObjectByType<
                    UniverseFrameController>();
        }

        private uint NextSeed()
        {
            seed =
                unchecked(
                    seed *
                    747796405u +
                    2891336453u);
            return seed;
        }
    }
}
