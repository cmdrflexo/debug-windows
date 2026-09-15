/*
 * Drives the existing SGT universe anchor as a body-relative astronomical observation camera.
 */

using System;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class UniverseObservationAnchorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SgtUniverseOriginBridge anchorBridge;

        [SerializeField]
        private DebugWindowManager debugWindowManager;

        [SerializeField]
        [Tooltip("Shared owner of the currently selected observation target.")]
        private UniverseObservationSelectionController selectionController;

        [SerializeField]
        private UniverseObservationGridRenderer observationGrid;

        [SerializeField]
        [Min(1.0f)]
        private double initialOriginDistanceMeters = 1.0e11;

        [Header("Session View")]
        [SerializeField] private bool rememberViewOnStop = true;
        [SerializeField] private CelestialUniverseRuntimeController generationController;

        [Serializable]
        private sealed class SavedView
        {
            public int version = 1;
            public int seed;
            public UniversePosition pivot;
            public double distance;
            public float yaw;
            public float pitch;
            public Vector3 planeNormal;
            public Vector3 planeForward;
            public bool hasCameraRotation;
            public Quaternion cameraRotation;
            public bool hasCameraPosition;
            public UniversePosition cameraPosition;
            public bool hasUniversalTime;
            public double universalTimeSeconds;
            public bool hasNavigationMode;
            public int navigationMode;
            public bool hasFreeFeatureLock;
            public string freeFeatureLockInstanceId;
        }

        private SavedView lastRenderedView;
        private bool checkedSavedView;
        private bool hasRenderedPivot;
        private UniversePosition renderedPivot;
        private bool navigationActive = true;
        private UniversePosition suspendedPivot;
        private bool hasSuspendedPivot;
        private Quaternion handoffRotation = Quaternion.identity;
        private bool settleHandoffRotation;
        private bool retainHandoffRotation;
        private Quaternion retainedRotation;
        private bool holdTransferredPose;
        private UniverseMotionState transferredPose;
        private bool hasPendingRestoredNavigationMode;
        private UniverseCameraModeController.NavigationMode pendingRestoredNavigationMode;
        private string pendingRestoredFreeLockInstanceId;
        public bool NavigationActive => navigationActive;
        private string ViewPreferenceKey =>
            "CelestialSystems.Observation.LastView.v1." + gameObject.scene.path;

        private bool originLocked = true;
        public bool IsOriginLocked => originLocked;

        [Header("Target")]
        [SerializeField]
        private CelestialBodyRuntimeContext target;

        [SerializeField]
        [Tooltip("Runtime Instance ID to select after a factory spawns the body.")]
        private string targetInstanceId;

        [SerializeField]
        private bool automaticallySelectFirstTarget = true;

        [Header("Reference Plane")]
        [SerializeField]
        [Tooltip("Universe-space normal of the observation plate.")]
        private Vector3 referencePlaneNormal = Vector3.up;

        [SerializeField]
        [Tooltip("Universe-space forward direction on the observation plate.")]
        private Vector3 referencePlaneForward = Vector3.forward;

        [Header("View")]
        [SerializeField]
        [Range(-80.0f, 80.0f)]
        private float pitchDegrees = 35.0f;

        [SerializeField]
        private float yawDegrees;

        [SerializeField]
        [Min(1.01f)]
        private float initialDistanceInTargetRadii = 3.0f;

        [SerializeField]
        [Min(1.001f)]
        private float minimumDistanceInTargetRadii = 1.05f;

        [SerializeField]
        [Min(1.0f)]
        private double maximumDistanceMeters = 1.0e18;

        [Header("Controls")]
        [SerializeField]
        private InputActionReference pointerDeltaAction;

        [SerializeField]
        private InputActionReference orbitAction;

        [SerializeField]
        private InputActionReference orbitLeftStepAction;

        [SerializeField]
        private InputActionReference orbitRightStepAction;

        [SerializeField]
        private InputActionReference panAction;

        [SerializeField]
        private InputActionReference panDirectionAction;

        [SerializeField]
        private InputActionReference zoomAction;

        [SerializeField]
        private InputActionReference zoomInAction;

        [SerializeField]
        private InputActionReference zoomOutAction;

        [SerializeField]
        [Tooltip("Modifier that changes scroll wheel input from zoom to plane elevation.")]
        private InputActionReference planeElevationModifierAction;

        [SerializeField]
        private InputActionReference recenterAction;

        [SerializeField]
        private bool invertPitch;

        [SerializeField]
        private InputActionReference lockToOriginAction;

        private bool enabledLockToOriginAction;

        [SerializeField]
        [Tooltip("Degrees of orbit rotation per mouse pixel.")]
        [Min(0.0f)]
        private float orbitDegreesPerPixel = 0.2f;

        [SerializeField]
        [Tooltip("How quickly pointer movement becomes orbit velocity.")]
        [Min(0.0f)]
        private float orbitVelocityResponse = 25.0f;

        [SerializeField]
        [Tooltip("How quickly orbit motion stops after pointer input ends.")]
        [Min(0.0f)]
        private float orbitDamping = 20.0f;

        [SerializeField]
        [Tooltip("Horizontal orbit angle applied by each step action.")]
        [Min(0.0f)]
        private float orbitStepDegrees = 45.0f;

        [SerializeField]
        [Tooltip("Plate movement per mouse pixel, as a fraction of camera distance.")]
        [Min(0.0f)]
        private float panDistanceFractionPerPixel = 0.0015f;

        [SerializeField]
        [Tooltip("How quickly drag movement becomes fling velocity.")]
        [Min(0.0f)]
        private float panVelocityResponse = 20.0f;

        [SerializeField]
        [Tooltip("How quickly a released pan fling slows down.")]
        [Min(0.0f)]
        private float panDamping = 4.0f;

        [SerializeField]
        [Tooltip("Keyboard pan speed as a fraction of camera distance per second.")]
        [Min(0.0f)]
        private float keyboardPanDistanceFractionPerSecond = 0.75f;

        [SerializeField]
        [Tooltip("Base-2 logarithmic zoom applied per mouse-wheel unit.")]
        [Min(0.0f)]
        private float zoomExponentPerWheelUnit = 0.002f;

        [SerializeField]
        [Tooltip("How quickly the camera settles on the selected zoom distance.")]
        [Min(0.0f)]
        private float zoomResponse = 12.0f;

        [SerializeField]
        [Tooltip("Distance multiplier applied by each zoom-key press.")]
        [Min(1.01f)]
        private float keyboardZoomStepMultiplier = 2.0f;

        [SerializeField]
        [Tooltip("Plane-normal movement per scroll-wheel unit, as a fraction of camera distance.")]
        [Min(0.0f)]
        private float planeElevationFractionPerWheelUnit = 0.002f;

        [SerializeField]
        [Tooltip("How quickly the plane settles on its selected elevation.")]
        [Min(0.0f)]
        private float planeElevationResponse = 12.0f;

        [SerializeField]
        [Tooltip("How quickly the observation pivot settles onto a newly selected target.")]
        [Min(0.0f)]
        private float targetTransitionResponse = 4.0f;

        [SerializeField]
        private bool listen = true;

        [Header("Runtime")]
        [SerializeField]
        private double distanceMeters;

        [SerializeField]
        private double targetDistanceMeters;

        [SerializeField]
        private double planeElevationMeters;

        [SerializeField]
        private double targetPlaneElevationMeters;

        [SerializeField]
        private double plateOffsetRightMeters;

        [SerializeField]
        private double plateOffsetForwardMeters;

        [SerializeField]
        private double targetTransitionOffsetXMeters;

        [SerializeField]
        private double targetTransitionOffsetYMeters;

        [SerializeField]
        private double targetTransitionOffsetZMeters;

        [SerializeField]
        private double panVelocityRightMetersPerSecond;

        [SerializeField]
        private double panVelocityForwardMetersPerSecond;

        [SerializeField]
        private bool hasTargetMotion;

        [SerializeField]
        private string lastError;

        private DebugWindowManager subscribedDebugWindowManager;
        private bool debugMenuVisible;
        private bool viewInitialized;
        private bool enabledPointerDeltaAction;
        private bool enabledOrbitAction;
        private bool enabledOrbitLeftStepAction;
        private bool enabledOrbitRightStepAction;
        private bool enabledPanAction;
        private bool enabledPanDirectionAction;
        private bool enabledZoomAction;
        private bool enabledZoomInAction;
        private bool enabledZoomOutAction;
        private bool enabledPlaneElevationModifierAction;
        private bool enabledRecenterAction;
        private bool recenterZoomArmed;
        private double yawVelocityDegreesPerSecond;
        private double pitchVelocityDegreesPerSecond;

        public CelestialBodyRuntimeContext Target => target;

        public double DistanceMeters => distanceMeters;

        public double TargetDistanceMeters => targetDistanceMeters;

        public double PlaneElevationMeters => planeElevationMeters;

        public double TargetPlaneElevationMeters => targetPlaneElevationMeters;

        public InputActionReference PlaneElevationModifierAction =>
            planeElevationModifierAction;

        public bool PlaneElevationModifierPressed =>
            IsPressed(planeElevationModifierAction);

        public double PlateOffsetRightMeters => plateOffsetRightMeters;

        public double PlateOffsetForwardMeters => plateOffsetForwardMeters;

        public bool HasTargetMotion => hasTargetMotion;

        public string LastError => lastError;

        public bool TryGetObservationPlane(
            out UniversePosition pivotPosition,
            out Vector3 planeRight,
            out Vector3 planeForward,
            out Vector3 planeUp)
        {
            if (!navigationActive && hasSuspendedPivot)
            {
                pivotPosition = suspendedPivot;
                GetReferencePlaneAxes(out planeRight, out planeForward, out planeUp);
                return true;
            }
            if (!TryGetPivotMotion(out var targetMotion))
            {
                pivotPosition = default;
                planeRight = default;
                planeForward = default;
                planeUp = default;
                return false;
            }

            GetReferencePlaneAxes(
                out planeRight,
                out planeForward,
                out planeUp);

            var pivotOffset =
                ToDoubleVector(planeRight) * plateOffsetRightMeters +
                ToDoubleVector(planeForward) * plateOffsetForwardMeters +
                ToDoubleVector(planeUp) * planeElevationMeters +
                GetTargetTransitionOffset();
            pivotPosition = targetMotion.Position;
            pivotPosition.AddLocalMeters(
                pivotOffset.x,
                pivotOffset.y,
                pivotOffset.z);
            return true;
        }

        private void Awake()
        {
            ResolveReferences();
            NormalizeReferencePlane();
        }

        private void OnEnable()
        {
            ResolveReferences();
            enabledPointerDeltaAction = EnableAction(pointerDeltaAction);
            enabledOrbitAction = EnableAction(orbitAction);
            enabledOrbitLeftStepAction = EnableAction(orbitLeftStepAction);
            enabledOrbitRightStepAction = EnableAction(orbitRightStepAction);
            enabledPanAction = EnableAction(panAction);
            enabledPanDirectionAction = EnableAction(panDirectionAction);
            enabledZoomAction = EnableAction(zoomAction);
            enabledZoomInAction = EnableAction(zoomInAction);
            enabledZoomOutAction = EnableAction(zoomOutAction);
            enabledPlaneElevationModifierAction =
                EnableAction(planeElevationModifierAction);
            enabledRecenterAction = EnableAction(recenterAction);
            enabledLockToOriginAction = EnableAction(lockToOriginAction);
            SubscribeToDebugMenu();
        }

        private void Start()
        {
            SubscribeToDebugMenu();
        }

        private void OnDisable()
        {
            SaveSessionView();
            UnsubscribeFromDebugMenu();
            DisableAction(pointerDeltaAction, enabledPointerDeltaAction);
            DisableAction(orbitAction, enabledOrbitAction);
            DisableAction(orbitLeftStepAction, enabledOrbitLeftStepAction);
            DisableAction(orbitRightStepAction, enabledOrbitRightStepAction);
            DisableAction(panAction, enabledPanAction);
            DisableAction(panDirectionAction, enabledPanDirectionAction);
            DisableAction(zoomAction, enabledZoomAction);
            DisableAction(zoomInAction, enabledZoomInAction);
            DisableAction(zoomOutAction, enabledZoomOutAction);
            DisableAction(
                planeElevationModifierAction,
                enabledPlaneElevationModifierAction);
            DisableAction(recenterAction, enabledRecenterAction);
            DisableAction(lockToOriginAction, enabledLockToOriginAction);
            enabledLockToOriginAction = false;
            enabledPointerDeltaAction = false;
            enabledOrbitAction = false;
            enabledOrbitLeftStepAction = false;
            enabledOrbitRightStepAction = false;
            enabledPanAction = false;
            enabledPanDirectionAction = false;
            enabledZoomAction = false;
            enabledZoomInAction = false;
            enabledZoomOutAction = false;
            enabledPlaneElevationModifierAction = false;
            enabledRecenterAction = false;
            ClearPanVelocity();
            ClearOrbitVelocity();
        }

        private void OnValidate()
        {
            pitchDegrees = Mathf.Clamp(pitchDegrees, -80.0f, 80.0f);
            initialDistanceInTargetRadii = Mathf.Max(1.01f, initialDistanceInTargetRadii);
            minimumDistanceInTargetRadii = Mathf.Max(1.001f, minimumDistanceInTargetRadii);
            maximumDistanceMeters = Math.Max(1.0, maximumDistanceMeters);
            initialOriginDistanceMeters = Math.Max(1.0, initialOriginDistanceMeters);
            orbitDegreesPerPixel = Mathf.Max(0.0f, orbitDegreesPerPixel);
            orbitVelocityResponse = Mathf.Max(0.0f, orbitVelocityResponse);
            orbitDamping = Mathf.Max(0.0f, orbitDamping);
            orbitStepDegrees = Mathf.Max(0.0f, orbitStepDegrees);
            panDistanceFractionPerPixel = Mathf.Max(0.0f, panDistanceFractionPerPixel);
            panVelocityResponse = Mathf.Max(0.0f, panVelocityResponse);
            panDamping = Mathf.Max(0.0f, panDamping);
            keyboardPanDistanceFractionPerSecond = Mathf.Max(
                0.0f,
                keyboardPanDistanceFractionPerSecond);
            zoomExponentPerWheelUnit = Mathf.Max(0.0f, zoomExponentPerWheelUnit);
            zoomResponse = Mathf.Max(0.0f, zoomResponse);
            keyboardZoomStepMultiplier = Mathf.Max(
                1.01f,
                keyboardZoomStepMultiplier);
            planeElevationFractionPerWheelUnit = Mathf.Max(
                0.0f,
                planeElevationFractionPerWheelUnit);
            planeElevationResponse = Mathf.Max(
                0.0f,
                planeElevationResponse);
            targetTransitionResponse = Mathf.Max(
                0.0f,
                targetTransitionResponse);
            NormalizeReferencePlane();
        }

        private void Update()
        {
            if (!navigationActive) return;
            ResolveSelectionTarget();

            if (selectionController == null && !originLocked)
            {
                ResolveTarget();
            }

            if (!listen || debugMenuVisible || (!originLocked && target == null))
            {
                return;
            }

            ReadControls(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (!navigationActive) return;
            TryRestoreSessionView();
            if ((!originLocked && target == null) || anchorBridge == null)
            {
                hasTargetMotion = false;
                return;
            }

            if (!TryGetPivotMotion(out var targetMotion))
            {
                hasTargetMotion = false;
                lastError = "The selected target has no available motion state.";
                return;
            }

            hasTargetMotion = true;

            if (!viewInitialized)
            {
                InitializeView();
            }

            var radiusMeters = GetTargetRadiusMeters();
            var minimumDistanceMeters = originLocked ? 1.0 : radiusMeters * minimumDistanceInTargetRadii;

            targetDistanceMeters = Math.Max(
                minimumDistanceMeters,
                Math.Min(maximumDistanceMeters, targetDistanceMeters));
            var zoomBlend = zoomResponse <= Mathf.Epsilon
                ? 1.0
                : 1.0 - Math.Exp(
                    -zoomResponse * Time.unscaledDeltaTime);
            distanceMeters = Lerp(
                distanceMeters,
                targetDistanceMeters,
                zoomBlend);

            if (Math.Abs(distanceMeters - targetDistanceMeters) <=
                Math.Max(0.001, targetDistanceMeters * 1.0e-9))
            {
                distanceMeters = targetDistanceMeters;
            }

            distanceMeters = Math.Max(
                minimumDistanceMeters,
                Math.Min(maximumDistanceMeters, distanceMeters));

            var elevationBlend = planeElevationResponse <= Mathf.Epsilon
                ? 1.0
                : 1.0 - Math.Exp(
                    -planeElevationResponse * Time.unscaledDeltaTime);
            planeElevationMeters = Lerp(
                planeElevationMeters,
                targetPlaneElevationMeters,
                elevationBlend);

            if (Math.Abs(
                    planeElevationMeters -
                    targetPlaneElevationMeters) <=
                Math.Max(
                    0.001,
                    Math.Abs(targetPlaneElevationMeters) * 1.0e-9))
            {
                planeElevationMeters = targetPlaneElevationMeters;
            }

            UpdateTargetTransition(Time.unscaledDeltaTime);

            GetReferencePlaneAxes(
                out var planeRight,
                out var planeForward,
                out var planeUp);

            var pivotOffset =
                ToDoubleVector(planeRight) * plateOffsetRightMeters +
                ToDoubleVector(planeForward) * plateOffsetForwardMeters +
                ToDoubleVector(planeUp) * planeElevationMeters +
                GetTargetTransitionOffset();
            var pivotPosition = targetMotion.Position;
            pivotPosition.AddLocalMeters(
                pivotOffset.x,
                pivotOffset.y,
                pivotOffset.z);

            if (settleHandoffRotation)
            {
                retainHandoffRotation = false;
                handoffRotation = Quaternion.Slerp(
                    handoffRotation, Quaternion.identity,
                    1.0f - Mathf.Exp(-8.0f * Time.unscaledDeltaTime));
                if (Quaternion.Angle(handoffRotation, Quaternion.identity) < 0.001f)
                {
                    handoffRotation = Quaternion.identity;
                    settleHandoffRotation = false;
                }
            }
            var cameraRotation = retainHandoffRotation
                ? retainedRotation : handoffRotation * GetOrbitRotation();
            var pivotToCameraDirection = -(cameraRotation * Vector3.forward);
            var cameraOffset =
                ToDoubleVector(pivotToCameraDirection) *
                distanceMeters;
            var cameraPosition = pivotPosition;
            cameraPosition.AddLocalMeters(
                cameraOffset.x,
                cameraOffset.y,
                cameraOffset.z);

            // Avoid multiplying a tiny float quaternion reconstruction error by an
            // astronomical pivot distance during an otherwise motionless handoff.
            if (holdTransferredPose)
            {
                cameraPosition = transferredPose.Position;
                cameraRotation = transferredPose.Rotation;
            }

            if (!anchorBridge.TrySetUniversePose(
                    new UniverseMotionState(
                        cameraPosition,
                        cameraRotation)))
            {
                lastError =
                    "The observation controller could not set the SGT anchor pose. Ensure its bridge is the active universe anchor source.";
                return;
            }

            lastError = string.Empty;
            renderedPivot = pivotPosition;
            hasRenderedPivot = true;
            if (rememberViewOnStop && generationController != null &&
                generationController.GenerationSucceeded)
            {
                lastRenderedView ??= new SavedView();
                lastRenderedView.seed = generationController.ResolvedSeed;
                lastRenderedView.pivot = pivotPosition;
                lastRenderedView.distance = distanceMeters;
                lastRenderedView.yaw = yawDegrees;
                lastRenderedView.pitch = pitchDegrees;
                lastRenderedView.planeNormal = referencePlaneNormal;
                lastRenderedView.planeForward = referencePlaneForward;
                lastRenderedView.hasCameraRotation = true;
                lastRenderedView.cameraRotation = cameraRotation;
                lastRenderedView.hasCameraPosition = true;
                lastRenderedView.cameraPosition = cameraPosition;
                CaptureUniversalTime(lastRenderedView);
            CaptureNavigationMode(lastRenderedView);
            ApplyPendingRestoredNavigationMode();
            }
        }

        public void SelectTarget(CelestialBodyRuntimeContext newTarget)
        {
            if (target == newTarget && !originLocked)
            {
                return;
            }
            holdTransferredPose = false;

            var preserveDistance =
                newTarget != null &&
                viewInitialized;
            var currentPivotPosition = default(UniversePosition);
            var planeUp = referencePlaneNormal.normalized;
            var newTargetMotion = default(UniverseMotionState);
            var hasCurrentPivot =
                preserveDistance &&
                TryGetObservationPlane(
                    out currentPivotPosition,
                    out _,
                    out _,
                    out planeUp);
            var hasNewTargetMotion =
                newTarget != null &&
                newTarget.TryGetMotionState(out newTargetMotion);
            var preservedPlaneElevationMeters = 0.0;

            if (hasCurrentPivot &&
                hasNewTargetMotion &&
                currentPivotPosition.TryGetOffsetMetersFrom(
                    newTargetMotion.Position,
                    out var pivotOffsetFromNewTarget))
            {
                preservedPlaneElevationMeters = Dot(
                    pivotOffsetFromNewTarget,
                    planeUp);
            }

            originLocked = newTarget == null;
            target = newTarget;
            targetInstanceId =
                newTarget != null
                    ? newTarget.InstanceId
                    : string.Empty;
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            planeElevationMeters = preservedPlaneElevationMeters;
            targetPlaneElevationMeters = preservedPlaneElevationMeters;
            ClearTargetTransition();

            if (hasCurrentPivot && hasNewTargetMotion)
            {
                var destinationPivot = newTargetMotion.Position;
                var elevationOffset =
                    ToDoubleVector(planeUp) *
                    preservedPlaneElevationMeters;
                destinationPivot.AddLocalMeters(
                    elevationOffset.x,
                    elevationOffset.y,
                    elevationOffset.z);

                if (currentPivotPosition.TryGetOffsetMetersFrom(
                        destinationPivot,
                        out var transitionOffset))
                {
                    targetTransitionOffsetXMeters = transitionOffset.x;
                    targetTransitionOffsetYMeters = transitionOffset.y;
                    targetTransitionOffsetZMeters = transitionOffset.z;
                }
            }

            ClearPanVelocity();
            viewInitialized = preserveDistance;
            hasTargetMotion = false;
            recenterZoomArmed = preserveDistance;
        }

        public bool SelectTargetByInstanceId(string instanceId)
        {
            targetInstanceId = instanceId;

            foreach (var context in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (context != null &&
                    string.Equals(
                        context.InstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                {
                    SelectTarget(context);
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("Select First Active Body")]
        public void SelectFirstActiveBody()
        {
            foreach (var context in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (context != null)
                {
                    SelectTarget(context);
                    return;
                }
            }
        }

        [ContextMenu("Recenter On Target")]
        public void RecenterOnTarget()
        {
            holdTransferredPose = false;
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
            ClearTargetTransition();
            ClearPanVelocity();
        }

        [ContextMenu("Reset Target View")]
        public void ResetTargetView()
        {
            holdTransferredPose = false;
            retainHandoffRotation = false;
            handoffRotation = Quaternion.identity;
            settleHandoffRotation = false;
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            ClearPanVelocity();
            pitchDegrees = 35.0f;
            yawDegrees = 0.0f;
            ClearOrbitVelocity();
            targetDistanceMeters = 0.0;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
            ClearTargetTransition();
            viewInitialized = false;
            recenterZoomArmed = false;
        }

        private void ResolveTarget()
        {
            if (target != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(targetInstanceId) &&
                SelectTargetByInstanceId(targetInstanceId))
            {
                return;
            }

            if (automaticallySelectFirstTarget)
            {
                SelectFirstActiveBody();
            }
        }

        private void ResolveSelectionTarget()
        {
            if (selectionController == null)
            {
                return;
            }

            if (selectionController.IsOriginLocked)
            {
                if (!originLocked)
                {
                    LockToOrigin();
                }
                return;
            }

            var selectedTarget = selectionController.SelectedTarget;

            if (originLocked || target != selectedTarget)
            {
                SelectTarget(selectedTarget);
            }
        }

        private void InitializeView()
        {
            distanceMeters = Math.Max(
                (originLocked ? initialOriginDistanceMeters : GetTargetRadiusMeters() * initialDistanceInTargetRadii),
                1.0);
            targetDistanceMeters = distanceMeters;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            ClearTargetTransition();
            ClearPanVelocity();
            recenterZoomArmed = false;
            viewInitialized = true;
        }

        private void ReadControls(float deltaTime)
        {
            if (WasPressedThisFrame(lockToOriginAction))
            {
                LockToOrigin();
                return;
            }

            var pointerDelta = ReadVector2(pointerDeltaAction);

            if (((IsPressed(orbitAction) || IsPressed(panAction)) &&
                    pointerDelta.sqrMagnitude > Mathf.Epsilon) ||
                ReadVector2(panDirectionAction).sqrMagnitude > Mathf.Epsilon ||
                !Mathf.Approximately(ReadFloat(zoomAction), 0.0f) ||
                WasPressedThisFrame(zoomInAction) || WasPressedThisFrame(zoomOutAction) ||
                WasPressedThisFrame(orbitLeftStepAction) || WasPressedThisFrame(orbitRightStepAction) ||
                WasPressedThisFrame(recenterAction))
            {
                holdTransferredPose = false;
            }

            ApplyOrbitInput(pointerDelta, deltaTime);
            ApplyOrbitStepInput();

            var panDirection = ReadVector2(panDirectionAction);

            if (IsPressed(panAction))
            {
                if (pointerDelta.sqrMagnitude > Mathf.Epsilon)
                {
                    recenterZoomArmed = false;
                }

                ApplyPanDrag(pointerDelta, deltaTime);
            }
            else if (panDirection.sqrMagnitude > Mathf.Epsilon)
            {
                recenterZoomArmed = false;
                ApplyPanDirection(panDirection, deltaTime);
            }
            else
            {
                ApplyPanFling(deltaTime);
            }

            var zoomInput = ReadFloat(zoomAction);

            if (!Mathf.Approximately(zoomInput, 0.0f))
            {
                recenterZoomArmed = false;

                if (IsPressed(planeElevationModifierAction))
                {
                    targetPlaneElevationMeters +=
                        zoomInput *
                        distanceMeters *
                        planeElevationFractionPerWheelUnit;
                }
                else
                {
                    targetDistanceMeters = Math.Max(
                        1.0,
                        targetDistanceMeters > 0.0
                            ? targetDistanceMeters
                            : distanceMeters);
                    targetDistanceMeters *= Math.Pow(
                        2.0,
                        -zoomInput * zoomExponentPerWheelUnit);
                }
            }

            if (WasPressedThisFrame(zoomInAction))
            {
                ApplyKeyboardZoomStep(1.0 / keyboardZoomStepMultiplier);
            }

            if (WasPressedThisFrame(zoomOutAction))
            {
                ApplyKeyboardZoomStep(keyboardZoomStepMultiplier);
            }

            if (WasPressedThisFrame(recenterAction))
            {
                HandleRecenterPressed();
            }
        }

        private void HandleRecenterPressed()
        {
            if (recenterZoomArmed)
            {
                targetDistanceMeters = Math.Max(
                    (originLocked ? initialOriginDistanceMeters : GetTargetRadiusMeters() * initialDistanceInTargetRadii),
                    1.0);
                recenterZoomArmed = false;
                return;
            }

            RecenterOnTarget();
            recenterZoomArmed = true;
        }

        private void ApplyOrbitInput(Vector2 pointerDelta, float deltaTime)
        {
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            if (IsPressed(orbitAction))
            {
                if (pointerDelta.sqrMagnitude > Mathf.Epsilon)
                {
                    settleHandoffRotation = true;
                    recenterZoomArmed = false;
                }

                var pitchDirection = invertPitch ? 1.0f : -1.0f;
                var instantYawVelocity =
                    pointerDelta.x * orbitDegreesPerPixel / deltaTime;
                var instantPitchVelocity =
                    pointerDelta.y * orbitDegreesPerPixel *
                    pitchDirection / deltaTime;
                var response =
                    1.0 - Math.Exp(-orbitVelocityResponse * deltaTime);

                yawVelocityDegreesPerSecond = Lerp(
                    yawVelocityDegreesPerSecond,
                    instantYawVelocity,
                    response);
                pitchVelocityDegreesPerSecond = Lerp(
                    pitchVelocityDegreesPerSecond,
                    instantPitchVelocity,
                    response);
            }
            else
            {
                var dampingFactor = Math.Exp(-orbitDamping * deltaTime);
                yawVelocityDegreesPerSecond *= dampingFactor;
                pitchVelocityDegreesPerSecond *= dampingFactor;
            }

            yawDegrees +=
                (float)(yawVelocityDegreesPerSecond * deltaTime);

            var nextPitch =
                pitchDegrees +
                (float)(pitchVelocityDegreesPerSecond * deltaTime);
            pitchDegrees = Mathf.Clamp(nextPitch, -80.0f, 80.0f);

            if (!Mathf.Approximately(nextPitch, pitchDegrees))
            {
                pitchVelocityDegreesPerSecond = 0.0;
            }

            if (Math.Abs(yawVelocityDegreesPerSecond) < 1.0e-6)
            {
                yawVelocityDegreesPerSecond = 0.0;
            }

            if (Math.Abs(pitchVelocityDegreesPerSecond) < 1.0e-6)
            {
                pitchVelocityDegreesPerSecond = 0.0;
            }
        }

        private void ApplyOrbitStepInput()
        {
            var step = 0.0f;

            if (WasPressedThisFrame(orbitLeftStepAction))
            {
                step -= orbitStepDegrees;
            }

            if (WasPressedThisFrame(orbitRightStepAction))
            {
                step += orbitStepDegrees;
            }

            if (Mathf.Approximately(step, 0.0f))
            {
                return;
            }

            ClearOrbitVelocity();
            settleHandoffRotation = true;
            yawDegrees += step;
            recenterZoomArmed = false;
        }

        private void ApplyKeyboardZoomStep(double multiplier)
        {
            targetDistanceMeters = Math.Max(
                1.0,
                targetDistanceMeters > 0.0
                    ? targetDistanceMeters
                    : distanceMeters);
            targetDistanceMeters *= multiplier;
            recenterZoomArmed = false;
        }

        private void ApplyPanDirection(Vector2 direction, float deltaTime)
        {
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            if (direction.sqrMagnitude > 1.0f)
            {
                direction.Normalize();
            }

            GetReferencePlaneAxes(
                out var planeRight,
                out var planeForward,
                out var planeUp);

            var yawRotation = Quaternion.AngleAxis(yawDegrees, planeUp);
            var cameraRightOnPlane = yawRotation * planeRight;
            var cameraForwardOnPlane = yawRotation * planeForward;
            var metersPerSecond =
                distanceMeters * keyboardPanDistanceFractionPerSecond;
            var desiredMovement =
                ToDoubleVector(cameraRightOnPlane) *
                    (direction.x * metersPerSecond) +
                ToDoubleVector(cameraForwardOnPlane) *
                    (direction.y * metersPerSecond);
            var desiredRightVelocity = Dot(desiredMovement, planeRight);
            var desiredForwardVelocity = Dot(desiredMovement, planeForward);
            var response = 1.0 - Math.Exp(-panVelocityResponse * deltaTime);

            panVelocityRightMetersPerSecond = Lerp(
                panVelocityRightMetersPerSecond,
                desiredRightVelocity,
                response);
            panVelocityForwardMetersPerSecond = Lerp(
                panVelocityForwardMetersPerSecond,
                desiredForwardVelocity,
                response);
            plateOffsetRightMeters +=
                panVelocityRightMetersPerSecond * deltaTime;
            plateOffsetForwardMeters +=
                panVelocityForwardMetersPerSecond * deltaTime;
        }

        private void ApplyPanDrag(Vector2 pointerDelta, float deltaTime)
        {
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            GetReferencePlaneAxes(
                out var planeRight,
                out var planeForward,
                out var planeUp);

            var yawRotation = Quaternion.AngleAxis(yawDegrees, planeUp);
            var cameraRightOnPlane = yawRotation * planeRight;
            var cameraForwardOnPlane = yawRotation * planeForward;
            var metersPerPixel = distanceMeters * panDistanceFractionPerPixel;
            var movement =
                ToDoubleVector(cameraRightOnPlane) *
                    (-pointerDelta.x * metersPerPixel) +
                ToDoubleVector(cameraForwardOnPlane) *
                    (-pointerDelta.y * metersPerPixel);
            var rightMovement = Dot(movement, planeRight);
            var forwardMovement = Dot(movement, planeForward);

            plateOffsetRightMeters += rightMovement;
            plateOffsetForwardMeters += forwardMovement;

            var instantRightVelocity = rightMovement / deltaTime;
            var instantForwardVelocity = forwardMovement / deltaTime;
            var response = 1.0 - Math.Exp(-panVelocityResponse * deltaTime);

            panVelocityRightMetersPerSecond = Lerp(
                panVelocityRightMetersPerSecond,
                instantRightVelocity,
                response);
            panVelocityForwardMetersPerSecond = Lerp(
                panVelocityForwardMetersPerSecond,
                instantForwardVelocity,
                response);
        }

        private void ApplyPanFling(float deltaTime)
        {
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            plateOffsetRightMeters +=
                panVelocityRightMetersPerSecond * deltaTime;
            plateOffsetForwardMeters +=
                panVelocityForwardMetersPerSecond * deltaTime;

            var dampingFactor = Math.Exp(-panDamping * deltaTime);
            panVelocityRightMetersPerSecond *= dampingFactor;
            panVelocityForwardMetersPerSecond *= dampingFactor;

            if (Math.Abs(panVelocityRightMetersPerSecond) < 1.0e-9)
            {
                panVelocityRightMetersPerSecond = 0.0;
            }

            if (Math.Abs(panVelocityForwardMetersPerSecond) < 1.0e-9)
            {
                panVelocityForwardMetersPerSecond = 0.0;
            }
        }

        private void UpdateTargetTransition(float deltaTime)
        {
            if (deltaTime <= Mathf.Epsilon)
            {
                return;
            }

            if (targetTransitionResponse <= Mathf.Epsilon)
            {
                ClearTargetTransition();
                return;
            }

            var dampingFactor = Math.Exp(
                -targetTransitionResponse * deltaTime);
            targetTransitionOffsetXMeters *= dampingFactor;
            targetTransitionOffsetYMeters *= dampingFactor;
            targetTransitionOffsetZMeters *= dampingFactor;
            var snapDistance = Math.Max(
                0.001,
                distanceMeters * 1.0e-9);

            if (Math.Abs(targetTransitionOffsetXMeters) <= snapDistance &&
                Math.Abs(targetTransitionOffsetYMeters) <= snapDistance &&
                Math.Abs(targetTransitionOffsetZMeters) <= snapDistance)
            {
                ClearTargetTransition();
            }
        }

        private DoubleVector3 GetTargetTransitionOffset()
        {
            return new DoubleVector3(
                targetTransitionOffsetXMeters,
                targetTransitionOffsetYMeters,
                targetTransitionOffsetZMeters);
        }

        private void ClearTargetTransition()
        {
            targetTransitionOffsetXMeters = 0.0;
            targetTransitionOffsetYMeters = 0.0;
            targetTransitionOffsetZMeters = 0.0;
        }

        private void ClearPanVelocity()
        {
            panVelocityRightMetersPerSecond = 0.0;
            panVelocityForwardMetersPerSecond = 0.0;
        }

        private void ClearOrbitVelocity()
        {
            yawVelocityDegreesPerSecond = 0.0;
            pitchVelocityDegreesPerSecond = 0.0;
        }

        private void GetReferencePlaneAxes(
            out Vector3 planeRight,
            out Vector3 planeForward,
            out Vector3 planeUp)
        {
            planeUp = referencePlaneNormal.normalized;
            planeForward = Vector3.ProjectOnPlane(
                referencePlaneForward,
                planeUp).normalized;
            planeRight = Vector3.Cross(
                planeUp,
                planeForward).normalized;
        }

        private void NormalizeReferencePlane()
        {
            if (referencePlaneNormal.sqrMagnitude <= Mathf.Epsilon)
            {
                referencePlaneNormal = Vector3.up;
            }

            referencePlaneNormal.Normalize();
            referencePlaneForward = Vector3.ProjectOnPlane(
                referencePlaneForward,
                referencePlaneNormal);

            if (referencePlaneForward.sqrMagnitude <= Mathf.Epsilon)
            {
                referencePlaneForward = Vector3.ProjectOnPlane(
                    Vector3.forward,
                    referencePlaneNormal);
            }

            if (referencePlaneForward.sqrMagnitude <= Mathf.Epsilon)
            {
                referencePlaneForward = Vector3.ProjectOnPlane(
                    Vector3.right,
                    referencePlaneNormal);
            }

            referencePlaneForward.Normalize();
        }

        private double GetTargetRadiusMeters()
        {
            if (target == null ||
                double.IsNaN(target.ConfiguredReferenceRadiusMeters) ||
                double.IsInfinity(target.ConfiguredReferenceRadiusMeters))
            {
                return 1.0;
            }

            return Math.Max(1.0, target.ConfiguredReferenceRadiusMeters);
        }

        private bool TryGetPivotMotion(out UniverseMotionState motion)
        {
            if (originLocked)
            {
                var origin = observationGrid != null && observationGrid.HasGridOrigin
                    ? observationGrid.GridOrigin
                    : default(UniversePosition);
                motion = new UniverseMotionState(origin, Quaternion.identity);
                return true;
            }

            motion = default;
            return target != null && target.TryGetMotionState(out motion);
        }

        [ContextMenu("Lock To Origin")]
        public void LockToOrigin()
        {
            var hasPivot = TryGetObservationPlane(
                out var previousPivot, out _, out _, out _);
            // Use the last displayed pivot, not the tracked body's next time step.
            if (hasRenderedPivot)
            {
                previousPivot = renderedPivot;
                hasPivot = true;
            }
            originLocked = true;
            selectionController?.LockToOrigin();
            RecenterOnTarget();
            ClearOrbitVelocity();
            targetDistanceMeters = distanceMeters;
            if (hasPivot)
            {
                SetOriginPivot(previousPivot);
            }
            recenterZoomArmed = false;
        }

        private void SetOriginPivot(UniversePosition position)
        {
            if (!TryGetPivotMotion(out var originMotion) ||
                !position.TryGetOffsetMetersFrom(originMotion.Position, out var offset))
            {
                return;
            }

            GetReferencePlaneAxes(out var right, out var forward, out var up);
            plateOffsetRightMeters = Dot(offset, right);
            plateOffsetForwardMeters = Dot(offset, forward);
            planeElevationMeters = Dot(offset, up);
            targetPlaneElevationMeters = planeElevationMeters;
            ClearTargetTransition();
        }

        private void OnApplicationQuit()
        {
            SaveSessionView();
        }

        private void SaveSessionView()
        {
            // Cache after rendering so destruction order cannot erase the seed or pose.
            if (!rememberViewOnStop || lastRenderedView == null)
            {
                return;
            }
            PlayerPrefs.SetString(ViewPreferenceKey, JsonUtility.ToJson(lastRenderedView));
            PlayerPrefs.Save();
        }

        private void TryRestoreSessionView()
        {
            if (!rememberViewOnStop || checkedSavedView)
            {
                return;
            }
            if (generationController == null)
            {
                generationController = FindFirstObjectByType<CelestialUniverseRuntimeController>();
            }
            if (generationController == null || !generationController.GenerationSucceeded)
            {
                return;
            }

            checkedSavedView = true;
            var json = PlayerPrefs.GetString(ViewPreferenceKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }

            SavedView saved;
            try
            {
                saved = JsonUtility.FromJson<SavedView>(json);
            }
            catch (ArgumentException)
            {
                return;
            }
            if (saved == null || saved.version != 1 ||
                saved.seed != generationController.ResolvedSeed ||
                !IsFiniteViewValue(saved.distance) || saved.distance < 1.0 ||
                !IsFiniteViewValue(saved.yaw) || !IsFiniteViewValue(saved.pitch) ||
                !IsFiniteViewVector(saved.planeNormal) ||
                !IsFiniteViewVector(saved.planeForward) ||
                !saved.pivot.TryGetOffsetMetersFrom(default(UniversePosition), out var offset) ||
                !IsFiniteViewValue(offset.x) || !IsFiniteViewValue(offset.y) ||
                !IsFiniteViewValue(offset.z))
            {
                return;
            }

            // Restore time before querying target motion or applying the saved
            // camera pose, so trajectory bodies occupy the same positions.
            RestoreUniversalTime(saved);
            if (saved.hasNavigationMode &&
                saved.navigationMode >= (int)UniverseCameraModeController.NavigationMode.Observer &&
                saved.navigationMode <= (int)UniverseCameraModeController.NavigationMode.Free)
            {
                pendingRestoredNavigationMode =
                    (UniverseCameraModeController.NavigationMode)saved.navigationMode;
                hasPendingRestoredNavigationMode = true;
                pendingRestoredFreeLockInstanceId =
                    pendingRestoredNavigationMode ==
                        UniverseCameraModeController.NavigationMode.Free &&
                    saved.hasFreeFeatureLock
                    ? saved.freeFeatureLockInstanceId ?? string.Empty
                    : string.Empty;
            }
            originLocked = true;
            selectionController?.LockToOrigin();
            referencePlaneNormal = saved.planeNormal;
            referencePlaneForward = saved.planeForward;
            NormalizeReferencePlane();
            yawDegrees = saved.yaw;
            pitchDegrees = Mathf.Clamp(saved.pitch, -80.0f, 80.0f);
            distanceMeters = Math.Min(saved.distance, maximumDistanceMeters);
            targetDistanceMeters = distanceMeters;
            SetOriginPivot(saved.pivot);
            handoffRotation = saved.hasCameraRotation && IsFiniteViewRotation(saved.cameraRotation)
                ? saved.cameraRotation.normalized * Quaternion.Inverse(GetOrbitRotation())
                : Quaternion.identity;
            retainHandoffRotation = saved.hasCameraRotation && IsFiniteViewRotation(saved.cameraRotation);
            retainedRotation = retainHandoffRotation ? saved.cameraRotation.normalized : Quaternion.identity;
            holdTransferredPose = retainHandoffRotation && saved.hasCameraPosition &&
                saved.cameraPosition.TryGetOffsetMetersFrom(default(UniversePosition), out _);
            if (holdTransferredPose)
                transferredPose = new UniverseMotionState(saved.cameraPosition, retainedRotation);
            settleHandoffRotation = false;
            ClearPanVelocity();
            ClearOrbitVelocity();
            viewInitialized = true;
            recenterZoomArmed = false;
        }

        private void ApplyPendingRestoredNavigationMode()
        {
            if (!hasPendingRestoredNavigationMode)
            {
                return;
            }

            var modeController =
                FindFirstObjectByType<UniverseCameraModeController>();
            if (modeController == null ||
                !modeController.SetMode(pendingRestoredNavigationMode))
            {
                return;
            }

            if (pendingRestoredNavigationMode ==
                    UniverseCameraModeController.NavigationMode.Free &&
                !string.IsNullOrEmpty(pendingRestoredFreeLockInstanceId))
            {
                var freeFlight =
                    FindFirstObjectByType<FreeUniverseAnchorController>();
                freeFlight?.RequestFeatureLockRestore(
                    pendingRestoredFreeLockInstanceId);
            }

            pendingRestoredFreeLockInstanceId = string.Empty;
            hasPendingRestoredNavigationMode = false;
        }

        private static void CaptureNavigationMode(SavedView view)
        {
            view.hasFreeFeatureLock = false;
            view.freeFeatureLockInstanceId = string.Empty;

            var modeController =
                FindFirstObjectByType<UniverseCameraModeController>();
            if (modeController == null)
            {
                view.hasNavigationMode = false;
                return;
            }

            view.hasNavigationMode = true;
            view.navigationMode = (int)modeController.Mode;

            if (modeController.Mode !=
                UniverseCameraModeController.NavigationMode.Free)
            {
                return;
            }

            var freeFlight =
                FindFirstObjectByType<FreeUniverseAnchorController>();
            var lockedBody = freeFlight != null
                ? freeFlight.LockedFeatureBody
                : null;
            if (lockedBody == null ||
                string.IsNullOrEmpty(lockedBody.InstanceId))
            {
                return;
            }

            view.hasFreeFeatureLock = true;
            view.freeFeatureLockInstanceId = lockedBody.InstanceId;
        }

        private static void CaptureUniversalTime(SavedView view)
        {
            var timeController = CelestialTimeController.Instance ??
                FindFirstObjectByType<CelestialTimeController>();
            if (timeController == null ||
                !IsFiniteViewValue(timeController.UniversalTimeSeconds))
            {
                view.hasUniversalTime = false;
                return;
            }

            view.hasUniversalTime = true;
            view.universalTimeSeconds =
                timeController.UniversalTimeSeconds;
        }

        private static void RestoreUniversalTime(SavedView view)
        {
            if (!view.hasUniversalTime ||
                !IsFiniteViewValue(view.universalTimeSeconds))
            {
                return;
            }

            var timeController = CelestialTimeController.Instance ??
                FindFirstObjectByType<CelestialTimeController>();
            timeController?.SetUniversalTimeSeconds(
                view.universalTimeSeconds);
        }

        private static bool IsFiniteViewValue(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsFiniteViewVector(Vector3 value)
        {
            return IsFiniteViewValue(value.x) && IsFiniteViewValue(value.y) &&
                IsFiniteViewValue(value.z) && value.sqrMagnitude > Mathf.Epsilon;
        }

        private static bool IsFiniteViewRotation(Quaternion value)
        {
            return IsFiniteViewValue(value.x) && IsFiniteViewValue(value.y) &&
                IsFiniteViewValue(value.z) && IsFiniteViewValue(value.w) &&
                Quaternion.Dot(value, value) > Mathf.Epsilon;
        }

        private Quaternion GetOrbitRotation()
        {
            GetReferencePlaneAxes(out var right, out var forward, out var up);
            var yaw = Quaternion.AngleAxis(yawDegrees, up);
            var direction = Quaternion.AngleAxis(-pitchDegrees, yaw * right) * (yaw * forward);
            return Quaternion.LookRotation(direction, up);
        }

        public void SetNavigationActive(bool active)
        {
            if (navigationActive == active) return;
            if (!active)
            {
                hasSuspendedPivot = TryGetObservationPlane(
                    out suspendedPivot, out _, out _, out _);
                if (hasRenderedPivot)
                {
                    suspendedPivot = renderedPivot;
                    hasSuspendedPivot = true;
                }
            }
            navigationActive = active;
            ClearPanVelocity();
            ClearOrbitVelocity();
        }

        // Adopt the displayed pose without forcing free-flight roll/pitch into orbit limits.
        // Orbit input gradually removes the residual rotation; switching itself never does.
        public void AdoptUniversePose(UniverseMotionState pose)
        {
            ResolveReferences();
            NormalizeReferencePlane();
            originLocked = true;
            selectionController?.LockToOrigin();
            GetReferencePlaneAxes(out var right, out var forward, out var up);
            var direction = pose.Rotation * Vector3.forward;
            var flat = Vector3.ProjectOnPlane(direction, up);
            if (flat.sqrMagnitude > Mathf.Epsilon)
            {
                yawDegrees = Mathf.Atan2(Vector3.Dot(flat, right),
                    Vector3.Dot(flat, forward)) * Mathf.Rad2Deg;
            }
            pitchDegrees = Mathf.Clamp(
                Mathf.Asin(Mathf.Clamp(Vector3.Dot(direction, up), -1.0f, 1.0f)) *
                Mathf.Rad2Deg, -80.0f, 80.0f);
            distanceMeters = Math.Max(1.0, viewInitialized ? distanceMeters : initialOriginDistanceMeters);
            targetDistanceMeters = distanceMeters;
            var pivot = pose.Position;
            pivot.AddLocalMeters(direction.x * distanceMeters,
                direction.y * distanceMeters, direction.z * distanceMeters);
            SetOriginPivot(pivot);
            handoffRotation = pose.Rotation * Quaternion.Inverse(GetOrbitRotation());
            retainHandoffRotation = true;
            retainedRotation = pose.Rotation;
            holdTransferredPose = true;
            transferredPose = pose;
            settleHandoffRotation = false;
            ClearPanVelocity();
            ClearOrbitVelocity();
            renderedPivot = pivot;
            hasRenderedPivot = true;
            viewInitialized = true;
            recenterZoomArmed = false;
            // A deliberate mode switch takes precedence over a delayed startup restore.
            checkedSavedView = true;
        }

        public void RecordExternalPose(UniverseMotionState pose)
        {
            if (!rememberViewOnStop) return;
            if (generationController == null)
                generationController = FindFirstObjectByType<CelestialUniverseRuntimeController>();
            if (generationController == null || !generationController.GenerationSucceeded) return;
            var distance = Math.Max(1.0, viewInitialized ? distanceMeters : initialOriginDistanceMeters);
            var direction = pose.Rotation * Vector3.forward;
            var pivot = pose.Position;
            pivot.AddLocalMeters(direction.x * distance, direction.y * distance, direction.z * distance);
            lastRenderedView ??= new SavedView();
            lastRenderedView.seed = generationController.ResolvedSeed;
            lastRenderedView.pivot = pivot;
            lastRenderedView.distance = distance;
            lastRenderedView.yaw = yawDegrees;
            lastRenderedView.pitch = pitchDegrees;
            lastRenderedView.planeNormal = referencePlaneNormal;
            lastRenderedView.planeForward = referencePlaneForward;
            lastRenderedView.hasCameraRotation = true;
            lastRenderedView.cameraRotation = pose.Rotation;
            lastRenderedView.hasCameraPosition = true;
            lastRenderedView.cameraPosition = pose.Position;
            CaptureUniversalTime(lastRenderedView);
            CaptureNavigationMode(lastRenderedView);
        }

        private void ResolveReferences()
        {
            if (observationGrid == null)
            {
                observationGrid = FindFirstObjectByType<UniverseObservationGridRenderer>();
            }

            if (anchorBridge == null)
            {
                anchorBridge = GetComponentInParent<SgtUniverseOriginBridge>();
            }

            if (anchorBridge == null)
            {
                anchorBridge = FindFirstObjectByType<SgtUniverseOriginBridge>();
            }

            if (selectionController == null)
            {
                selectionController =
                    GetComponentInParent<UniverseObservationSelectionController>();
            }

            if (selectionController == null)
            {
                selectionController =
                    FindFirstObjectByType<UniverseObservationSelectionController>();
            }
        }

        private void SubscribeToDebugMenu()
        {
            var manager = debugWindowManager != null
                ? debugWindowManager
                : DebugWindowManager.Instance;

            if (manager == subscribedDebugWindowManager)
            {
                return;
            }

            UnsubscribeFromDebugMenu();
            subscribedDebugWindowManager = manager;

            if (subscribedDebugWindowManager == null)
            {
                debugMenuVisible = false;
                return;
            }

            debugWindowManager = subscribedDebugWindowManager;
            debugMenuVisible = subscribedDebugWindowManager.MenuVisible;
            subscribedDebugWindowManager.MenuVisibilityChanged +=
                OnDebugMenuVisibilityChanged;
        }

        private void UnsubscribeFromDebugMenu()
        {
            if (subscribedDebugWindowManager != null)
            {
                subscribedDebugWindowManager.MenuVisibilityChanged -=
                    OnDebugMenuVisibilityChanged;
            }

            subscribedDebugWindowManager = null;
        }

        private void OnDebugMenuVisibilityChanged(bool visible)
        {
            debugMenuVisible = visible;

            if (visible)
            {
                ClearPanVelocity();
                ClearOrbitVelocity();
                recenterZoomArmed = false;
            }
        }

        private static bool EnableAction(InputActionReference actionReference)
        {
            var action = actionReference != null
                ? actionReference.action
                : null;

            if (action == null || action.enabled)
            {
                return false;
            }

            action.Enable();
            return true;
        }

        private static void DisableAction(
            InputActionReference actionReference,
            bool enabledByThisComponent)
        {
            if (!enabledByThisComponent ||
                actionReference == null ||
                actionReference.action == null)
            {
                return;
            }

            actionReference.action.Disable();
        }

        private static Vector2 ReadVector2(InputActionReference actionReference)
        {
            return actionReference != null && actionReference.action != null
                ? actionReference.action.ReadValue<Vector2>()
                : Vector2.zero;
        }

        private static float ReadFloat(InputActionReference actionReference)
        {
            return actionReference != null && actionReference.action != null
                ? actionReference.action.ReadValue<float>()
                : 0.0f;
        }

        private static bool IsPressed(InputActionReference actionReference)
        {
            return actionReference != null &&
                actionReference.action != null &&
                actionReference.action.IsPressed();
        }

        private static bool WasPressedThisFrame(
            InputActionReference actionReference)
        {
            return actionReference != null &&
                actionReference.action != null &&
                actionReference.action.WasPressedThisFrame();
        }

        private static double Dot(DoubleVector3 value, Vector3 axis)
        {
            return
                value.x * axis.x +
                value.y * axis.y +
                value.z * axis.z;
        }

        private static double Lerp(double from, double to, double amount)
        {
            return from + (to - from) * Math.Max(0.0, Math.Min(1.0, amount));
        }

        private static DoubleVector3 ToDoubleVector(Vector3 value)
        {
            return new DoubleVector3(value.x, value.y, value.z);
        }
    }
}
