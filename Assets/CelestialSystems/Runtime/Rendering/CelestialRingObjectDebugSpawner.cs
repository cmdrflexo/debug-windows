/*
 * Scene-owned manual rock spawner. It shares the ring object-set contract
 * without placing debug input on every generated ring body.
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingObjectDebugSpawner : MonoBehaviour
    {
        [SerializeField]
        private CelestialRingObjectSet objectSet;

        [SerializeField]
        private Camera observerCamera;

        [SerializeField]
        [Tooltip("Optional explicit frame. If empty, the active scene frame is used.")]
        private UniverseFrameController universeFrame;

        [SerializeField]
        [Tooltip("Optional nearest body/ring context. If empty, the camera's context is used.")]
        private UniverseLocalEnvironmentContext velocityReference;

        [SerializeField]
        [Min(0.1f)]
        private float spawnDistanceMeters = 20.0f;

        [SerializeField]
        private InputAction spawnAction =
            new InputAction(
                "Spawn Ring Object",
                InputActionType.Button,
                "<Keyboard>/h");

        [SerializeField]
        private CelestialRingObjectFamilyKind familyKind =
            CelestialRingObjectFamilyKind.IceChunk;

        [SerializeField]
        private bool castShadows;

        [SerializeField]
        private bool drawGizmos = true;

        private readonly List<GameObject> spawnedObjects =
            new List<GameObject>();

        private void OnEnable()
        {
            spawnAction?.Enable();
        }

        private void OnDisable()
        {
            spawnAction?.Disable();
            ClearSpawnedObjects();
        }

        private void OnValidate()
        {
            spawnDistanceMeters =
                Mathf.Max(0.1f, spawnDistanceMeters);
        }

        private void Update()
        {
            if (spawnAction != null &&
                spawnAction.WasPressedThisFrame())
            {
                SpawnRingObject();
            }
        }

        public void SpawnRingObject()
        {
            var camera = observerCamera != null
                ? observerCamera
                : Camera.main;
            var family = GetSpawnFamily();

            if (camera == null || family == null)
            {
                return;
            }

            var instance = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            instance.name = "Debug Ring Object";
            var collider = instance.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            instance.transform.position =
                camera.transform.position +
                camera.transform.forward *
                spawnDistanceMeters;
            instance.transform.rotation = Random.rotation;

            var hasInitialUniversePosition =
                UniverseTrackedObject
                    .TryGetUniversePositionFromScenePosition(
                        GetUniverseFrame(),
                        instance.transform.position,
                        out var initialUniversePosition);

            var seed = unchecked(
                (uint)(Time.frameCount * 747796405) +
                (uint)spawnedObjects.Count * 2891336453u);
            var diameter = Mathf.Lerp(
                family.MinimumDiameterMeters,
                family.MaximumDiameterMeters,
                0.55f);
            var request = new CelestialRingBodyRequest
            {
                Seed = seed,
                FamilyKind = family.Kind,
                NominalDiameterMeters = diameter
            };
            var resolved = default(CelestialRingResolvedBody);
            var hasResolvedVisual =
                family.BodySource != null &&
                family.BodySource.TryResolve(
                    request,
                    out resolved) &&
                resolved.HasVisual;
            var filter = instance.GetComponent<MeshFilter>();
            var renderer = instance.GetComponent<MeshRenderer>();

            filter.sharedMesh =
                hasResolvedVisual && resolved.Mesh != null
                    ? resolved.Mesh
                    : family.MeshVariants.Count > 0
                        ? family.MeshVariants[0]
                        : CelestialRingProceduralMeshes.GetMesh(
                            family.Kind,
                            seed);

            if (renderer != null)
            {
                renderer.shadowCastingMode =
                    castShadows
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off;
                renderer.receiveShadows = castShadows;
                renderer.sharedMaterial =
                    hasResolvedVisual && resolved.Material != null
                        ? resolved.Material
                        : family.MaterialVariants.Count > 0
                            ? family.MaterialVariants[0]
                            : renderer.sharedMaterial;
            }

            if (hasResolvedVisual)
            {
                diameter *= resolved.SafeDiameterMultiplier;
            }

            instance.transform.localScale =
                Vector3.one * diameter;

            if (hasInitialUniversePosition)
            {
                var velocityMotion =
                    instance.AddComponent<UniverseVelocityMotion>();
                var velocity = DoubleVector3.zero;

                if (TryGetVelocityReference(
                        camera,
                        out var referenceMotion))
                {
                    velocity =
                        referenceMotion
                            .LinearVelocityMetersPerSecond;
                }

                velocityMotion.Initialize(
                    new UniverseMotionState(
                        initialUniversePosition,
                        instance.transform.rotation,
                        velocity,
                        DoubleVector3.zero));
            }

            var gizmo = instance.AddComponent<
                CelestialRingObjectDebugGizmo>();
            gizmo.Configure(
                drawGizmos,
                GetGizmoColor(family.Kind));
            spawnedObjects.Add(instance);
        }

        [ContextMenu("Clear Spawned Ring Objects")]
        public void ClearSpawnedObjects()
        {
            foreach (var instance in spawnedObjects)
            {
                if (instance != null)
                {
                    Destroy(instance);
                }
            }

            spawnedObjects.Clear();
        }

        private UniverseFrameController GetUniverseFrame()
        {
            return universeFrame != null
                ? universeFrame
                : FindFirstObjectByType<UniverseFrameController>();
        }

        private bool TryGetVelocityReference(
            Camera camera,
            out UniverseMotionState motion)
        {
            var context = velocityReference != null
                ? velocityReference
                : camera.GetComponentInParent<
                    UniverseLocalEnvironmentContext>();

            context ??= FindFirstObjectByType<
                UniverseLocalEnvironmentContext>();
            return context != null &&
                context.TryGetReferenceMotion(out motion);
        }

        private CelestialRingObjectFamily GetSpawnFamily()
        {
            if (objectSet == null)
            {
                return null;
            }

            CelestialRingObjectFamily fallback = null;
            foreach (var family in objectSet.Families)
            {
                if (family == null) continue;
                fallback ??= family;

                if (family.Kind == familyKind)
                {
                    return family;
                }
            }

            return fallback;
        }

        private static Color GetGizmoColor(
            CelestialRingObjectFamilyKind kind)
        {
            return kind switch
            {
                CelestialRingObjectFamilyKind.FineIce =>
                    new Color(0.35f, 0.9f, 1.0f, 1.0f),
                CelestialRingObjectFamilyKind.IceChunk =>
                    new Color(0.2f, 0.55f, 1.0f, 1.0f),
                CelestialRingObjectFamilyKind.DarkRubble =>
                    new Color(0.8f, 0.55f, 0.25f, 1.0f),
                CelestialRingObjectFamilyKind.DustCluster =>
                    new Color(0.9f, 0.75f, 0.25f, 1.0f),
                CelestialRingObjectFamilyKind.LargeClump =>
                    new Color(1.0f, 0.35f, 0.7f, 1.0f),
                _ => Color.cyan
            };
        }
    }
}
