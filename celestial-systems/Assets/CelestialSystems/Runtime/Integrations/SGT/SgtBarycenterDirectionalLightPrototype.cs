/*
 * Prototype directional-light controller that anchors a scene light at the
 * generated star-system barycenter and aims it toward the observation camera.
 *
 * This intentionally approximates a future local point-light solution; it is
 * not a physically correct distant-light model.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(450)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    [RequireComponent(typeof(Light))]
    public sealed class SgtBarycenterDirectionalLightPrototype :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private CelestialUniverseRuntimeController generationController;

        [SerializeField]
        private Camera targetCamera;

        [Header("Runtime")]
        [SerializeField]
        private bool isTrackingBarycenter;

        private SgtFloatingObject floatingObject;

        public bool IsTrackingBarycenter =>
            isTrackingBarycenter;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            ResolveReferences();

            if (generationController == null ||
                !generationController.GenerationSucceeded ||
                !generationController.HasGeneratedStarSystemBarycenter ||
                floatingObject == null)
            {
                isTrackingBarycenter = false;
                return;
            }

            if (!SgtUniversePositionConverter.TryToSgtPosition(
                    generationController.GeneratedStarSystemBarycenter,
                    0.0,
                    0.0,
                    0.0,
                    out var barycenterPosition))
            {
                isTrackingBarycenter = false;
                return;
            }

            floatingObject.SetPosition(
                barycenterPosition);
            floatingObject.ApplyPosition();

            var camera = ResolveTargetCamera();
            if (camera != null)
            {
                var direction =
                    camera.transform.position -
                    transform.position;

                if (direction.sqrMagnitude > Mathf.Epsilon)
                {
                    // Directional lights illuminate in transform.forward.
                    transform.rotation =
                        Quaternion.LookRotation(
                            direction.normalized,
                            camera.transform.up);
                }
            }

            isTrackingBarycenter = true;
        }

        private void ResolveReferences()
        {
            if (generationController == null)
            {
                generationController =
                    FindFirstObjectByType<CelestialUniverseRuntimeController>();
            }

            floatingObject ??=
                GetComponent<SgtFloatingObject>();
        }

        private Camera ResolveTargetCamera()
        {
            if (targetCamera != null &&
                targetCamera.isActiveAndEnabled)
            {
                return targetCamera;
            }

            return Camera.main;
        }
    }
}
