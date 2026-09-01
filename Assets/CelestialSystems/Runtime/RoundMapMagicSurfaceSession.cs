/*
 * Lazily binds one reusable Round MapMagic surface rig to a requested celestial-body runtime context.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicSurfaceSession :
        MonoBehaviour
    {
        [Header("Rig Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private CubeSphereMapMagicRootPool rootPool;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition configuredSurfaceDefinition;

        [Header("Session Request")]
        [SerializeField]
        private bool requestedActive = true;

        [SerializeField]
        private CelestialBodyRuntimeContext requestedBodyContext;

        [Header("Runtime")]
        [SerializeField]
        private bool hasActiveSession;

        [SerializeField]
        private CelestialBodyRuntimeContext activeBodyContext;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition activeSurfaceDefinition;

        private bool started;

        public bool RequestedActive =>
            requestedActive;

        public CelestialBodyRuntimeContext RequestedBodyContext =>
            requestedBodyContext;

        public bool HasActiveSession =>
            hasActiveSession;

        public CelestialBodyRuntimeContext ActiveBodyContext =>
            activeBodyContext;

        public RoundMapMagicSurfaceDefinition ActiveSurfaceDefinition =>
            activeSurfaceDefinition;

        public RoundMapMagicSurfaceDefinition ConfiguredSurfaceDefinition =>
            configuredSurfaceDefinition;

        private void Reset()
        {
            surfaceFrame =
                GetComponent<GePlanetSurfaceFrame>();
            rootPool =
                GetComponent<CubeSphereMapMagicRootPool>();

            if (surfaceFrame == null)
            {
                return;
            }

            requestedBodyContext =
                surfaceFrame.BodyContext;

            if (requestedBodyContext != null &&
                requestedBodyContext.Definition != null)
            {
                configuredSurfaceDefinition =
                    requestedBodyContext.Definition.RoundMapMagicSurface;
            }
        }

        private void Start()
        {
            started = true;

            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "A Round MapMagic surface session requires a round-body surface frame.",
                    this);
            }

            if (rootPool == null)
            {
                Debug.LogError(
                    "A Round MapMagic surface session requires a MapMagic root pool.",
                    this);
            }

            if (configuredSurfaceDefinition == null)
            {
                Debug.LogError(
                    "A Round MapMagic surface session requires the surface definition currently configured on its reusable rig.",
                    this);
            }

            if (requestedBodyContext == null &&
                surfaceFrame != null)
            {
                requestedBodyContext =
                    surfaceFrame.BodyContext;
            }

            ApplyRequestedState();
        }

        private void LateUpdate()
        {
            if (!started)
            {
                return;
            }

            ApplyRequestedState();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            DeactivateSession();
        }

        public bool RequestSession(
            CelestialBodyRuntimeContext bodyContext)
        {
            requestedBodyContext =
                bodyContext;
            requestedActive =
                bodyContext != null;

            if (!Application.isPlaying ||
                !started)
            {
                return requestedActive;
            }

            return ApplyRequestedState();
        }

        public void ReleaseSession()
        {
            requestedActive = false;

            if (Application.isPlaying)
            {
                DeactivateSession();
            }
        }

        private bool ApplyRequestedState()
        {
            if (!requestedActive)
            {
                if (hasActiveSession ||
                    surfaceFrame != null &&
                    surfaceFrame.BodyContext != null)
                {
                    DeactivateSession();
                }

                return true;
            }

            if (requestedBodyContext == null)
            {
                Debug.LogError(
                    "An active Round MapMagic surface-session request requires a body context.",
                    this);
                requestedActive = false;
                DeactivateSession();
                return false;
            }

            if (hasActiveSession &&
                activeBodyContext ==
                    requestedBodyContext)
            {
                return true;
            }

            if (!TryActivateSession(
                    requestedBodyContext))
            {
                requestedActive = false;
                DeactivateSession();
                return false;
            }

            return true;
        }

        private bool TryActivateSession(
            CelestialBodyRuntimeContext bodyContext)
        {
            if (surfaceFrame == null ||
                rootPool == null ||
                configuredSurfaceDefinition == null)
            {
                Debug.LogError(
                    "The Round MapMagic surface session is missing required rig configuration.",
                    this);
                return false;
            }

            var definition =
                bodyContext.Definition;

            if (definition == null ||
                definition.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic ||
                !definition.HasValidPhysicalSettings ||
                !definition.HasValidResolvedSurfaceSettings)
            {
                Debug.LogError(
                    "The requested body does not have valid Round MapMagic definition settings.",
                    bodyContext);
                return false;
            }

            var requestedSurfaceDefinition =
                definition.RoundMapMagicSurface;

            if (requestedSurfaceDefinition !=
                configuredSurfaceDefinition)
            {
                Debug.LogError(
                    "The requested body's Round MapMagic surface definition does not match the reusable rig's configured surface definition.",
                    bodyContext);
                return false;
            }

            if (hasActiveSession &&
                activeBodyContext !=
                    bodyContext)
            {
                DeactivateSession();
            }

            if (!surfaceFrame.TrySetBodyContext(
                    bodyContext))
            {
                return false;
            }

            hasActiveSession = true;
            activeBodyContext =
                bodyContext;
            activeSurfaceDefinition =
                requestedSurfaceDefinition;
            return true;
        }

        private void DeactivateSession()
        {
            if (surfaceFrame != null)
            {
                surfaceFrame.ClearBodyContext();
            }

            if (rootPool != null)
            {
                rootPool.ReleaseAllRoots();
            }

            hasActiveSession = false;
            activeBodyContext = null;
            activeSurfaceDefinition = null;
        }
    }
}
