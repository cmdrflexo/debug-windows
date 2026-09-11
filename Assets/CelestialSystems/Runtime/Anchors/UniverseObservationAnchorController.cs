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
            if (target == null ||
                !target.TryGetMotionState(out var targetMotion))
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
                ToDoubleVector(planeUp) * planeElevationMeters;
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
            SubscribeToDebugMenu();
        }

        private void Start()
        {
            SubscribeToDebugMenu();
        }

        private void OnDisable()
        {
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
            NormalizeReferencePlane();
        }

        private void Update()
        {
            ResolveSelectionTarget();

            if (selectionController == null)
            {
                ResolveTarget();
            }

            if (!listen || debugMenuVisible || target == null)
            {
                return;
            }

            ReadControls(Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
            if (target == null || anchorBridge == null)
            {
                hasTargetMotion = false;
                return;
            }

            if (!target.TryGetMotionState(out var targetMotion))
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
            var minimumDistanceMeters = radiusMeters * minimumDistanceInTargetRadii;

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

            GetReferencePlaneAxes(
                out var planeRight,
                out var planeForward,
                out var planeUp);

            var pivotOffset =
                ToDoubleVector(planeRight) * plateOffsetRightMeters +
                ToDoubleVector(planeForward) * plateOffsetForwardMeters +
                ToDoubleVector(planeUp) * planeElevationMeters;
            var pivotPosition = targetMotion.Position;
            pivotPosition.AddLocalMeters(
                pivotOffset.x,
                pivotOffset.y,
                pivotOffset.z);

            var yawRotation = Quaternion.AngleAxis(yawDegrees, planeUp);
            var yawForward = yawRotation * planeForward;
            var yawRight = yawRotation * planeRight;
            var pitchRotation = Quaternion.AngleAxis(-pitchDegrees, yawRight);
            var pivotToCameraDirection =
                pitchRotation * -yawForward;
            var cameraOffset =
                ToDoubleVector(pivotToCameraDirection.normalized) *
                distanceMeters;
            var cameraPosition = pivotPosition;
            cameraPosition.AddLocalMeters(
                cameraOffset.x,
                cameraOffset.y,
                cameraOffset.z);

            var cameraRotation = Quaternion.LookRotation(
                -pivotToCameraDirection.normalized,
                planeUp);

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
        }

        public void SelectTarget(CelestialBodyRuntimeContext newTarget)
        {
            if (target == newTarget)
            {
                return;
            }

            var preserveDistance =
                target != null &&
                newTarget != null &&
                viewInitialized;

            target = newTarget;
            targetInstanceId =
                newTarget != null
                    ? newTarget.InstanceId
                    : string.Empty;
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
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
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
            ClearPanVelocity();
        }

        [ContextMenu("Reset Target View")]
        public void ResetTargetView()
        {
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            ClearPanVelocity();
            pitchDegrees = 35.0f;
            yawDegrees = 0.0f;
            ClearOrbitVelocity();
            targetDistanceMeters = 0.0;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
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

            var selectedTarget = selectionController.SelectedTarget;

            if (target != selectedTarget)
            {
                SelectTarget(selectedTarget);
            }
        }

        private void InitializeView()
        {
            distanceMeters = Math.Max(
                GetTargetRadiusMeters() * initialDistanceInTargetRadii,
                1.0);
            targetDistanceMeters = distanceMeters;
            planeElevationMeters = 0.0;
            targetPlaneElevationMeters = 0.0;
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            ClearPanVelocity();
            recenterZoomArmed = false;
            viewInitialized = true;
        }

        private void ReadControls(float deltaTime)
        {
            var pointerDelta = ReadVector2(pointerDeltaAction);

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
                    GetTargetRadiusMeters() * initialDistanceInTargetRadii,
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

        private void ResolveReferences()
        {
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
