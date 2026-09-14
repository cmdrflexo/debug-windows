/*
 * Prototype scene-light controller that follows the generated system's
 * primary stellar body through the SGT floating-origin coordinate system.
 *
 * The tracked star is exposed for a later generated spectral appearance
 * component to drive this light's color and intensity.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(450)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    [RequireComponent(typeof(Light))]
    public sealed class SgtPrimaryStarLightPrototype :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private CelestialUniverseRuntimeController generationController;

        [Header("Runtime")]
        [SerializeField]
        private CelestialBodyRuntimeContext trackedStar;

        [SerializeField]
        private bool isTrackingPrimaryStar;

        private SgtFloatingObject floatingObject;

        /// <summary>
        /// The most massive generated stellar body currently used as the
        /// source position for this light.
        /// </summary>
        public CelestialBodyRuntimeContext TrackedStar =>
            trackedStar;

        /// <summary>
        /// Lets a later spectral-lighting component read the generated stellar
        /// temperature, luminosity, and evolutionary state from the same target.
        /// </summary>
        public CelestialBodyDefinition TrackedStarDefinition =>
            trackedStar != null
                ? trackedStar.Definition
                : null;

        public bool IsTrackingPrimaryStar =>
            isTrackingPrimaryStar;

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
                !generationController.TryGetPrimaryStellarBody(
                    out trackedStar) ||
                !trackedStar.TryGetMotionState(
                    out var starMotion) ||
                floatingObject == null ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    starMotion.Position,
                    0.0,
                    0.0,
                    0.0,
                    out var starPosition))
            {
                trackedStar = null;
                isTrackingPrimaryStar = false;
                return;
            }

            floatingObject.SetPosition(
                starPosition);
            floatingObject.ApplyPosition();
            isTrackingPrimaryStar = true;
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
    }
}
