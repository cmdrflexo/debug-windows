/*
 * Selects the nearest compatible round body and requests one reusable MapMagic surface session with activation hysteresis.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-70)]
    [DisallowMultipleComponent]
    public sealed class RoundMapMagicSurfaceSessionSelector :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private UniverseFrameController universeFrame;

        [Header("Selection Distances")]
        [SerializeField]
        private double activationDistanceMeters =
            500000.0;

        [SerializeField]
        private double releaseDistanceMeters =
            750000.0;

        [SerializeField]
        [Min(0.0f)]
        private float evaluationIntervalSeconds =
            0.25f;

        [Header("Runtime")]
        [SerializeField]
        private int eligibleBodyCount;

        [SerializeField]
        private bool hasSelectedBody;

        [SerializeField]
        private CelestialBodyRuntimeContext selectedBodyContext;

        [SerializeField]
        private double selectedSurfaceDistanceMeters;

        private GravityEngine gravityEngine;
        private double nextEvaluationTime;

        public int EligibleBodyCount =>
            eligibleBodyCount;

        public bool HasSelectedBody =>
            hasSelectedBody;

        public CelestialBodyRuntimeContext SelectedBodyContext =>
            selectedBodyContext;

        public double SelectedSurfaceDistanceMeters =>
            selectedSurfaceDistanceMeters;

        private void Reset()
        {
            surfaceSession =
                GetComponent<RoundMapMagicSurfaceSession>();

            if (surfaceSession != null &&
                surfaceSession.RequestedBodyContext != null)
            {
                universeFrame =
                    surfaceSession.RequestedBodyContext.UniverseFrame;
            }
        }

        private void Start()
        {
            gravityEngine =
                GravityEngine.Instance();

            if (surfaceSession == null)
            {
                Debug.LogError(
                    "The automatic round-surface selector requires a Round MapMagic surface session.",
                    this);
            }

            if (universeFrame == null)
            {
                Debug.LogError(
                    "The automatic round-surface selector requires a universe frame controller.",
                    this);
            }

            if (!SelectionDistancesAreValid())
            {
                Debug.LogError(
                    "The automatic round-surface selector requires finite non-negative distances with release distance greater than or equal to activation distance.",
                    this);
            }

            if (surfaceSession != null &&
                surfaceSession.HasActiveSession)
            {
                selectedBodyContext =
                    surfaceSession.ActiveBodyContext;
                hasSelectedBody =
                    selectedBodyContext != null;
            }
        }

        private void LateUpdate()
        {
            var currentTime =
                Time.unscaledTimeAsDouble;

            if (hasSelectedBody &&
                currentTime <
                    nextEvaluationTime)
            {
                return;
            }

            nextEvaluationTime =
                currentTime +
                evaluationIntervalSeconds;
            EvaluateSelection();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            ClearSelection();
        }

        private void EvaluateSelection()
        {
            eligibleBodyCount = 0;

            if (surfaceSession == null ||
                universeFrame == null ||
                surfaceSession.ConfiguredSurfaceDefinition ==
                    null ||
                !SelectionDistancesAreValid())
            {
                ClearSelection();
                return;
            }

            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup() ||
                !universeFrame.TryGetActiveAnchorOffsetMeters(
                    out var anchorOffsetMeters))
            {
                return;
            }

            var positionMetersPerPhysicsUnit =
                gravityEngine.GetPhysicalScale();

            if (!IsFinite(
                    positionMetersPerPhysicsUnit) ||
                positionMetersPerPhysicsUnit <=
                    0.0)
            {
                return;
            }

            CelestialBodyRuntimeContext nearestBody =
                null;
            var nearestSurfaceDistanceMeters =
                double.PositiveInfinity;
            var selectedBodyIsEligible =
                false;
            var currentSelectedDistanceMeters =
                double.PositiveInfinity;

            foreach (var bodyContext in
                CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (!IsEligibleBody(
                        bodyContext))
                {
                    continue;
                }

                if (!TryGetSurfaceDistanceMeters(
                        bodyContext,
                        anchorOffsetMeters,
                        positionMetersPerPhysicsUnit,
                        out var surfaceDistanceMeters))
                {
                    continue;
                }

                eligibleBodyCount++;

                if (bodyContext ==
                    selectedBodyContext)
                {
                    selectedBodyIsEligible = true;
                    currentSelectedDistanceMeters =
                        surfaceDistanceMeters;
                }

                if (surfaceDistanceMeters <
                    nearestSurfaceDistanceMeters)
                {
                    nearestBody =
                        bodyContext;
                    nearestSurfaceDistanceMeters =
                        surfaceDistanceMeters;
                }
            }

            if (hasSelectedBody &&
                selectedBodyIsEligible &&
                currentSelectedDistanceMeters <=
                    releaseDistanceMeters)
            {
                selectedSurfaceDistanceMeters =
                    currentSelectedDistanceMeters;

                if ((!surfaceSession.HasActiveSession ||
                    surfaceSession.ActiveBodyContext !=
                        selectedBodyContext) &&
                    !surfaceSession.RequestSession(
                        selectedBodyContext))
                {
                    ClearSelection();
                }

                return;
            }

            if (nearestBody != null &&
                nearestSurfaceDistanceMeters <=
                    activationDistanceMeters)
            {
                SelectBody(
                    nearestBody,
                    nearestSurfaceDistanceMeters);
                return;
            }

            ClearSelection();
        }

        private bool IsEligibleBody(
            CelestialBodyRuntimeContext bodyContext)
        {
            if (bodyContext == null ||
                !bodyContext.HasValidConfiguration ||
                bodyContext.UniverseFrame !=
                    universeFrame)
            {
                return false;
            }

            var definition =
                bodyContext.Definition;

            return
                definition != null &&
                definition.ResolvedSurfaceSystem ==
                    CelestialSurfaceSystem.RoundMapMagic &&
                definition.HasValidPhysicalSettings &&
                definition.HasValidResolvedSurfaceSettings &&
                definition.RoundMapMagicSurface ==
                    surfaceSession.ConfiguredSurfaceDefinition;
        }

        private bool TryGetSurfaceDistanceMeters(
            CelestialBodyRuntimeContext bodyContext,
            Vector3d anchorOffsetMeters,
            double positionMetersPerPhysicsUnit,
            out double surfaceDistanceMeters)
        {
            var gravityBody =
                bodyContext.GravityBody;

            if (gravityBody == null)
            {
                surfaceDistanceMeters = default;
                return false;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(
                    gravityBody);
            var centerXMeters =
                physicsPosition.x *
                positionMetersPerPhysicsUnit;
            var centerYMeters =
                physicsPosition.y *
                positionMetersPerPhysicsUnit;
            var centerZMeters =
                physicsPosition.z *
                positionMetersPerPhysicsUnit;
            var deltaX =
                anchorOffsetMeters.x -
                centerXMeters;
            var deltaY =
                anchorOffsetMeters.y -
                centerYMeters;
            var deltaZ =
                anchorOffsetMeters.z -
                centerZMeters;
            var radialDistanceMeters =
                Math.Sqrt(
                    deltaX *
                    deltaX +
                    deltaY *
                    deltaY +
                    deltaZ *
                    deltaZ);

            surfaceDistanceMeters =
                Math.Abs(
                    radialDistanceMeters -
                    bodyContext.ConfiguredReferenceRadiusMeters);
            return
                IsFinite(
                    surfaceDistanceMeters);
        }

        private void SelectBody(
            CelestialBodyRuntimeContext bodyContext,
            double surfaceDistanceMeters)
        {
            if (!surfaceSession.RequestSession(
                    bodyContext))
            {
                ClearSelection();
                return;
            }

            selectedBodyContext =
                bodyContext;
            selectedSurfaceDistanceMeters =
                surfaceDistanceMeters;
            hasSelectedBody = true;
        }

        private void ClearSelection()
        {
            if (surfaceSession != null &&
                surfaceSession.HasActiveSession)
            {
                surfaceSession.ReleaseSession();
            }

            hasSelectedBody = false;
            selectedBodyContext = null;
            selectedSurfaceDistanceMeters = default;
        }

        private bool SelectionDistancesAreValid()
        {
            return
                IsFinite(
                    activationDistanceMeters) &&
                activationDistanceMeters >=
                    0.0 &&
                IsFinite(
                    releaseDistanceMeters) &&
                releaseDistanceMeters >=
                    activationDistanceMeters;
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
