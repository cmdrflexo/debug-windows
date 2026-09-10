/*
 * Drives the existing SGT universe anchor as a body-relative astronomical observation camera.
 */

using System;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(410)]
    [DisallowMultipleComponent]
    public sealed class UniverseObservationAnchorController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SgtGravityOriginBridge anchorBridge;

        [SerializeField]
        private DebugWindowManager debugWindowManager;

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
        [Tooltip("Degrees of orbit rotation per mouse pixel.")]
        [Min(0.0f)]
        private float orbitDegreesPerPixel = 0.2f;

        [SerializeField]
        [Tooltip("Plate movement per mouse pixel, as a fraction of camera distance.")]
        [Min(0.0f)]
        private float panDistanceFractionPerPixel = 0.0015f;

        [SerializeField]
        [Tooltip("Base-2 logarithmic zoom applied per mouse-wheel unit.")]
        [Min(0.0f)]
        private float zoomExponentPerWheelUnit = 0.002f;

        [SerializeField]
        private bool listen = true;

        [Header("Runtime")]
        [SerializeField]
        private double distanceMeters;

        [SerializeField]
        private double plateOffsetRightMeters;

        [SerializeField]
        private double plateOffsetForwardMeters;

        [SerializeField]
        private bool hasTargetMotion;

        [SerializeField]
        private string lastError;

        private DebugWindowManager subscribedDebugWindowManager;
        private bool debugMenuVisible;
        private bool viewInitialized;

        public CelestialBodyRuntimeContext Target => target;

        public double DistanceMeters => distanceMeters;

        public double PlateOffsetRightMeters => plateOffsetRightMeters;

        public double PlateOffsetForwardMeters => plateOffsetForwardMeters;

        public bool HasTargetMotion => hasTargetMotion;

        public string LastError => lastError;

        private void Awake()
        {
            ResolveReferences();
            NormalizeReferencePlane();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeToDebugMenu();
        }

        private void Start()
        {
            SubscribeToDebugMenu();
        }

        private void OnDisable()
        {
            UnsubscribeFromDebugMenu();
        }

        private void OnValidate()
        {
            pitchDegrees = Mathf.Clamp(pitchDegrees, -80.0f, 80.0f);
            initialDistanceInTargetRadii = Mathf.Max(1.01f, initialDistanceInTargetRadii);
            minimumDistanceInTargetRadii = Mathf.Max(1.001f, minimumDistanceInTargetRadii);
            maximumDistanceMeters = Math.Max(1.0, maximumDistanceMeters);
            orbitDegreesPerPixel = Mathf.Max(0.0f, orbitDegreesPerPixel);
            panDistanceFractionPerPixel = Mathf.Max(0.0f, panDistanceFractionPerPixel);
            zoomExponentPerWheelUnit = Mathf.Max(0.0f, zoomExponentPerWheelUnit);
            NormalizeReferencePlane();
        }

        private void Update()
        {
            ResolveTarget();

            if (!listen || debugMenuVisible || target == null)
            {
                return;
            }

            ReadControls();
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

            distanceMeters = Math.Max(
                minimumDistanceMeters,
                Math.Min(maximumDistanceMeters, distanceMeters));

            GetReferencePlaneAxes(
                out var planeRight,
                out var planeForward,
                out var planeUp);

            var pivotOffset =
                ToDoubleVector(planeRight) * plateOffsetRightMeters +
                ToDoubleVector(planeForward) * plateOffsetForwardMeters;
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
            target = newTarget;
            targetInstanceId =
                newTarget != null
                    ? newTarget.InstanceId
                    : string.Empty;
            viewInitialized = false;
            hasTargetMotion = false;
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
        }

        [ContextMenu("Reset Target View")]
        public void ResetTargetView()
        {
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            pitchDegrees = 35.0f;
            yawDegrees = 0.0f;
            viewInitialized = false;
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

        private void InitializeView()
        {
            distanceMeters = Math.Max(
                GetTargetRadiusMeters() * initialDistanceInTargetRadii,
                1.0);
            plateOffsetRightMeters = 0.0;
            plateOffsetForwardMeters = 0.0;
            viewInitialized = true;
        }

        private void ReadControls()
        {
            var mouse = Mouse.current;

            if (mouse != null)
            {
                var pointerDelta = mouse.delta.ReadValue();

                if (mouse.rightButton.isPressed)
                {
                    yawDegrees += pointerDelta.x * orbitDegreesPerPixel;
                    pitchDegrees = Mathf.Clamp(
                        pitchDegrees - pointerDelta.y * orbitDegreesPerPixel,
                        -80.0f,
                        80.0f);
                }

                if (mouse.middleButton.isPressed)
                {
                    var metersPerPixel =
                        distanceMeters * panDistanceFractionPerPixel;

                    plateOffsetRightMeters +=
                        -pointerDelta.x * metersPerPixel;
                    plateOffsetForwardMeters +=
                        -pointerDelta.y * metersPerPixel;
                }

                var wheel = mouse.scroll.ReadValue().y;

                if (!Mathf.Approximately(wheel, 0.0f))
                {
                    distanceMeters *= Math.Pow(
                        2.0,
                        -wheel * zoomExponentPerWheelUnit);
                }
            }

            var keyboard = Keyboard.current;

            if (keyboard != null && keyboard.homeKey.wasPressedThisFrame)
            {
                RecenterOnTarget();
            }
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
                anchorBridge = GetComponentInParent<SgtGravityOriginBridge>();
            }

            if (anchorBridge == null)
            {
                anchorBridge = FindFirstObjectByType<SgtGravityOriginBridge>();
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
        }

        private static DoubleVector3 ToDoubleVector(Vector3 value)
        {
            return new DoubleVector3(value.x, value.y, value.z);
        }
    }
}
