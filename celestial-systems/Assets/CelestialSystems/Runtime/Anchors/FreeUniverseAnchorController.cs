/*
 * Moves a nonphysical universe anchor using camera-relative free-flight controls, with optional scene collision and planet-relative automatic speed.
 */

using System;
using System.Collections.Generic;
using CW.Common;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UniverseLocalEnvironmentContext))]
    public sealed class FreeUniverseAnchorController : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField]
        private Transform movementReference;

        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        [Tooltip("Optional debug-window manager whose full menu blocks flight input.")]
        private DebugWindowManager debugWindowManager;

        [Header("Movement")]
        [SerializeField]
        private bool listen = true;

        [SerializeField]
        private float damping = 10.0f;

        [SerializeField]
        [Tooltip("Positive floor used only when touching a feature or no navigation feature is available.")]
        [Min(0.0001f)]
        private float minimumAutomaticSpeedMetersPerSecond =
            0.1f;

        [Header("Collision")]
        [SerializeField]
        private bool collideWithScene;

        [SerializeField]
        [Min(0.01f)]
        private float collisionRadiusMeters = 1.0f;

        [SerializeField]
        [Min(0.0f)]
        private float collisionSkinMeters = 0.05f;

        [SerializeField]
        private LayerMask collisionLayers = ~0;

        [Header("Input Actions")]
        [SerializeField]
        [Tooltip("Vector2 action where X is horizontal movement and Y is forward/back movement.")]
        private InputActionReference moveAction;

        [SerializeField]
        [Tooltip("Axis action for vertical movement.")]
        private InputActionReference verticalAction;

        [SerializeField]
        [Tooltip("Axis action whose positive/negative performed values increase/decrease the manual speed multiplier.")]
        private InputActionReference speedMultiplierAction;

        [SerializeField]
        [Tooltip("Button action that temporarily ramps the boost multiplier while held.")]
        private InputActionReference boostAction;

        [Header("Camera Look")]
        [SerializeField]
        [Tooltip("Pointer-delta action used for always-on free-camera rotation.")]
        private InputActionReference lookDeltaAction;

        [SerializeField]
        [Tooltip("Signed axis action used to roll the free camera.")]
        private InputActionReference rollAction;

        [SerializeField]
        [Tooltip("Button action that toggles tracking of the nearest body or ring owner.")]
        private InputActionReference lockNearestFeatureAction;

        [Header("Camera Zoom")]
        [SerializeField]
        [Tooltip("Held action that narrows both the Main Camera and its child far camera.")]
        private InputActionReference zoomAction;

        [SerializeField]
        [Tooltip("Optical magnification while the zoom action is held.")]
        [Min(1.0f)]
        private float zoomMagnification = 10.0f;

        [SerializeField]
        [Tooltip("How quickly camera field of view settles on the zoomed value.")]
        [Min(0.0f)]
        private float zoomResponse = 20.0f;

        [SerializeField, Min(0.0f)]
        private float lookDegreesPerPixel = 0.2f;

        [SerializeField, Min(0.0f)]
        private float rollDegreesPerSecond = 45.0f;

        [SerializeField]
        private bool invertPitch;

        [Header("Manual Speed Multiplier")]
        [SerializeField]
        [Min(1.0001f)]
        private float speedMultiplierStep = 2.0f;

        [SerializeField]
        [Min(0.000001f)]
        private float moveSpeedMultiplier = 1.0f;

        [SerializeField]
        [Tooltip("Seconds used for both the initial Free-mode cruise speed and automatic approach braking.")]
        [Min(0.01f)]
        private float initialFeatureTravelSeconds = 10.0f;

        [Header("Boost")]
        [SerializeField]
        [Min(1.0f)]
        private float maximumBoostMultiplier = 8.0f;

        [SerializeField]
        [Tooltip("Seconds of continuous boost input required to reach the maximum multiplier.")]
        [Min(0.01f)]
        private float boostRampSeconds = 3.0f;

        [SerializeField]
        [Min(1.0f)]
        private float currentBoostMultiplier = 1.0f;

        [Header("Fallback Controls")]
        [SerializeField]
        private CwInputManager.Axis horizontalControls =
            new CwInputManager.Axis(
                2,
                false,
                CwInputManager.AxisGesture.HorizontalDrag,
                1.0f,
                KeyCode.A,
                KeyCode.D,
                KeyCode.LeftArrow,
                KeyCode.RightArrow,
                100.0f);

        [SerializeField]
        private CwInputManager.Axis depthControls =
            new CwInputManager.Axis(
                2,
                false,
                CwInputManager.AxisGesture.HorizontalDrag,
                1.0f,
                KeyCode.S,
                KeyCode.W,
                KeyCode.DownArrow,
                KeyCode.UpArrow,
                100.0f);

        [SerializeField]
        private CwInputManager.Axis verticalControls =
            new CwInputManager.Axis(
                3,
                false,
                CwInputManager.AxisGesture.HorizontalDrag,
                1.0f,
                KeyCode.F,
                KeyCode.R,
                KeyCode.None,
                KeyCode.None,
                100.0f);

        [Header("Runtime Speed")]
        [SerializeField]
        private bool hasNearestNavigationFeature;

        [SerializeField]
        private CelestialBodyRuntimeContext nearestBody;

        [SerializeField]
        private CelestialRingMeshPresentation nearestRing;

        [SerializeField]
        private string nearestNavigationFeatureName;

        [SerializeField]
        private double nearestNavigationFeatureDistanceMeters;

        [SerializeField]
        private double nearestNavigationFeatureScaleMeters;

        [SerializeField]
        private float resolvedSpeed;

        [SerializeField]
        private bool hasAutomaticCruiseSpeed;

        [SerializeField]
        private float automaticCruiseSpeedMetersPerSecond;

        [Header("Runtime Feature Lock")]
        [SerializeField]
        private CelestialBodyRuntimeContext lockedFeatureBody;

        [SerializeField]
        private bool hasLockedFeatureMotion;

        [SerializeField]
        private UniverseMotionState lockedFeatureMotion;

        private string pendingFeatureLockInstanceId;

        [Header("Runtime Collision")]
        [SerializeField]
        private bool hasCollision;

        [SerializeField]
        private string lastCollisionObject;

        [SerializeField]
        private float lastCollisionDistanceMeters;

        [SerializeField]
        private Vector3 lastCollisionNormal;

        private GravityEngine gravityEngine;
        private Vector3 remainingDelta;
        private SgtUniverseOriginBridge poseBridge;
        private UniverseLocalEnvironmentContext localEnvironmentContext;
        private int activationFrame;

        // Configure while disabled; the mode owner owns any runtime input references.
        public void ConfigureSharedRig(
            SgtUniverseOriginBridge bridge, Transform cameraTransform,
            InputActionReference move, InputActionReference vertical,
            InputActionReference speed, InputActionReference boost,
            InputActionReference lookDelta, InputActionReference roll,
            InputActionReference lockNearestFeature,
            InputActionReference zoom)
        {
            poseBridge = bridge;
            universeFrame = bridge.UniverseFrame;
            movementReference = cameraTransform;
            if (moveAction == null) moveAction = move;
            if (verticalAction == null) verticalAction = vertical;
            if (speedMultiplierAction == null) speedMultiplierAction = speed;
            if (boostAction == null) boostAction = boost;
            if (lookDeltaAction == null) lookDeltaAction = lookDelta;
            if (rollAction == null) rollAction = roll;
            if (lockNearestFeatureAction == null)
                lockNearestFeatureAction = lockNearestFeature;
            if (zoomAction == null) zoomAction = zoom;
            CaptureZoomCameras();
            ClearActiveInput();
        }
        private bool enabledMoveAction;
        private bool enabledVerticalAction;
        private bool enabledSpeedMultiplierAction;
        private bool enabledBoostAction;
        private bool enabledLookDeltaAction;
        private bool enabledRollAction;
        private bool enabledLockNearestFeatureAction;
        private bool enabledZoomAction;
        private readonly List<CameraZoomState> zoomCameras =
            new List<CameraZoomState>();
        private DebugWindowManager subscribedDebugWindowManager;
        private bool debugMenuVisible;

        public bool HasNearestNavigationFeature =>
            hasNearestNavigationFeature;

        public CelestialBodyRuntimeContext NearestBody =>
            nearestBody;

        public CelestialRingMeshPresentation NearestRing =>
            nearestRing;

        public double NearestNavigationFeatureDistanceMeters =>
            nearestNavigationFeatureDistanceMeters;

        public string NearestNavigationFeatureName =>
            nearestNavigationFeatureName;

        public double NearestNavigationFeatureScaleMeters =>
            nearestNavigationFeatureScaleMeters;

        public CelestialBodyRuntimeContext LockedFeatureBody =>
            lockedFeatureBody;

        public bool IsFeatureLocked =>
            lockedFeatureBody != null;

        public float MoveSpeedMultiplier =>
            moveSpeedMultiplier;

        public float AutomaticMoveSpeedMetersPerSecond =>
            resolvedSpeed;

        public bool InputBlockedByDebugMenu =>
            debugMenuVisible;

        public bool IsBoosting =>
            !debugMenuVisible &&
            boostAction != null &&
            boostAction.action != null &&
            boostAction.action.IsPressed();

        public float CurrentBoostMultiplier =>
            currentBoostMultiplier;

        public float CurrentMoveSpeedMetersPerSecond =>
            resolvedSpeed *
            moveSpeedMultiplier *
            currentBoostMultiplier;

        public bool IsZooming =>
            !debugMenuVisible && zoomAction != null &&
            zoomAction.action != null && zoomAction.action.IsPressed();

        // Capture one cruise ceiling per transition into Free mode. Nearest
        // feature changes can brake the camera, but never recalculate this
        // ceiling until Free mode is entered again.
        public void SetInitialSpeedFromNearestFeature()
        {
            hasAutomaticCruiseSpeed = false;
            automaticCruiseSpeedMetersPerSecond = 0.0f;
            moveSpeedMultiplier = 1.0f;
            UpdateSpeedState();

            if (!hasNearestNavigationFeature)
            {
                automaticCruiseSpeedMetersPerSecond =
                    minimumAutomaticSpeedMetersPerSecond;
                hasAutomaticCruiseSpeed = true;
                resolvedSpeed = automaticCruiseSpeedMetersPerSecond;
                return;
            }

            automaticCruiseSpeedMetersPerSecond =
                CalculateDistanceLimitedSpeed(
                    nearestNavigationFeatureDistanceMeters);
            hasAutomaticCruiseSpeed = true;
            resolvedSpeed = automaticCruiseSpeedMetersPerSecond;
        }

        private void Awake()
        {
            ResolveUniverseFrame();
            ResolveLocalEnvironmentContext();
        }

        private void OnEnable()
        {
            activationFrame = Time.frameCount;
            ResolveLocalEnvironmentContext();
            PublishLockedEnvironment();
            ClearActiveInput();
            CwInputManager.EnsureThisComponentExists();

            enabledMoveAction =
                EnableAction(
                    moveAction);
            enabledVerticalAction =
                EnableAction(
                    verticalAction);
            enabledSpeedMultiplierAction =
                EnableAction(
                    speedMultiplierAction);
            enabledBoostAction =
                EnableAction(
                    boostAction);
            enabledLookDeltaAction =
                EnableAction(
                    lookDeltaAction);
            enabledRollAction =
                EnableAction(
                    rollAction);
            enabledLockNearestFeatureAction =
                EnableAction(
                    lockNearestFeatureAction);
            enabledZoomAction =
                EnableAction(
                    zoomAction);
            CaptureZoomCameras();

            if (speedMultiplierAction != null &&
                speedMultiplierAction.action != null)
            {
                speedMultiplierAction.action.performed +=
                    OnSpeedMultiplierPerformed;
            }

            SubscribeToDebugMenu();
        }

        private void Start()
        {
            SubscribeToDebugMenu();
        }

        private void OnDisable()
        {
            ResolveLocalEnvironmentContext();
            localEnvironmentContext?.ClearIfSource(
                UniverseLocalEnvironmentContext.EnvironmentSource.FreeFlightLock);
            ClearActiveInput();
            UnsubscribeFromDebugMenu();

            if (speedMultiplierAction != null &&
                speedMultiplierAction.action != null)
            {
                speedMultiplierAction.action.performed -=
                    OnSpeedMultiplierPerformed;
            }

            DisableAction(
                moveAction,
                enabledMoveAction);
            DisableAction(
                verticalAction,
                enabledVerticalAction);
            DisableAction(
                speedMultiplierAction,
                enabledSpeedMultiplierAction);
            DisableAction(
                boostAction,
                enabledBoostAction);
            DisableAction(
                lookDeltaAction,
                enabledLookDeltaAction);
            DisableAction(
                rollAction,
                enabledRollAction);
            DisableAction(
                lockNearestFeatureAction,
                enabledLockNearestFeatureAction);
            DisableAction(
                zoomAction,
                enabledZoomAction);
            RestoreCameraZoom();

            enabledMoveAction = false;
            enabledVerticalAction = false;
            enabledSpeedMultiplierAction = false;
            enabledBoostAction = false;
            enabledLookDeltaAction = false;
            enabledRollAction = false;
            enabledLockNearestFeatureAction = false;
            enabledZoomAction = false;
            currentBoostMultiplier = 1.0f;
            hasAutomaticCruiseSpeed = false;
            automaticCruiseSpeedMetersPerSecond = 0.0f;
        }

        private void OnValidate()
        {
            speedMultiplierStep =
                Mathf.Max(
                    1.0001f,
                    speedMultiplierStep);
            moveSpeedMultiplier =
                Mathf.Max(
                    0.000001f,
                    moveSpeedMultiplier);
            initialFeatureTravelSeconds =
                Mathf.Max(
                    0.01f,
                    initialFeatureTravelSeconds);
            zoomMagnification = Mathf.Max(1.0f, zoomMagnification);
            zoomResponse = Mathf.Max(0.0f, zoomResponse);
            maximumBoostMultiplier =
                Mathf.Max(
                    1.0f,
                    maximumBoostMultiplier);
            boostRampSeconds =
                Mathf.Max(
                    0.01f,
                    boostRampSeconds);
            currentBoostMultiplier =
                Mathf.Clamp(
                    currentBoostMultiplier,
                    1.0f,
                    maximumBoostMultiplier);
        }

        private void Update()
        {
            UpdateCameraLook();

            if (!debugMenuVisible &&
                lockNearestFeatureAction != null &&
                lockNearestFeatureAction.action != null &&
                lockNearestFeatureAction.action.WasPressedThisFrame())
            {
                ToggleNearestFeatureLock();
            }

            if (poseBridge != null) return;
            UpdateBoostMultiplier(
                Time.deltaTime);
            UpdateSpeedState();

            if (listen &&
                !debugMenuVisible)
            {
                AddToDelta(
                    GetDelta(
                        Time.deltaTime));
            }
        }

        // Free mode owns camera rotation. Pointer look deliberately has no hold
        // binding: any configured pointer delta rotates while this controller is enabled.
        private void UpdateCameraLook()
        {
            if (!listen || debugMenuVisible || movementReference == null)
            {
                return;
            }

            var rotation = movementReference.rotation;
            // Match optical magnification with inverse turn rate: a 10x
            // zoom uses one-tenth mouse-look and roll sensitivity.
            var zoomRotationMultiplier = IsZooming
                ? 1.0f / Mathf.Max(1.0f, zoomMagnification)
                : 1.0f;
            if (lookDeltaAction != null && lookDeltaAction.action != null)
            {
                var delta = lookDeltaAction.action.ReadValue<Vector2>() *
                    lookDegreesPerPixel * zoomRotationMultiplier;
                rotation *= Quaternion.Euler(
                    invertPitch ? delta.y : -delta.y,
                    delta.x,
                    0.0f);
            }

            if (rollAction != null && rollAction.action != null)
            {
                rotation *= Quaternion.AngleAxis(
                    -rollAction.action.ReadValue<float>() *
                    rollDegreesPerSecond *
                    zoomRotationMultiplier *
                    Time.unscaledDeltaTime,
                    Vector3.forward);
            }

            movementReference.rotation = rotation;
        }

        private void LateUpdate()
        {
            if (poseBridge != null)
            {
                if (Time.frameCount == activationFrame) return;
                TryRestorePendingFeatureLock();
                FollowLockedFeature();
                UpdateBoostMultiplier(Time.deltaTime);
                UpdateSpeedState();
                if (listen && !debugMenuVisible)
                    AddToDelta(GetDelta(Time.deltaTime));
            }
            if (listen &&
                !debugMenuVisible)
            {
                DampenDelta();
            }

            UpdateCameraZoom(Time.unscaledDeltaTime);
        }

        public void ToggleNearestFeatureLock()
        {
            if (lockedFeatureBody != null)
            {
                ClearFeatureLock();
                return;
            }

            UpdateSpeedState();
            if (nearestBody == null ||
                !nearestBody.TryGetMotionState(out var motion))
            {
                return;
            }

            SetFeatureLock(nearestBody, motion);
        }

        public void RequestFeatureLockRestore(string instanceId)
        {
            pendingFeatureLockInstanceId = instanceId ?? string.Empty;
        }

        private void TryRestorePendingFeatureLock()
        {
            if (string.IsNullOrEmpty(pendingFeatureLockInstanceId))
            {
                return;
            }

            foreach (var candidate in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (candidate == null ||
                    !string.Equals(
                        candidate.InstanceId,
                        pendingFeatureLockInstanceId,
                        StringComparison.Ordinal) ||
                    !candidate.TryGetMotionState(out var motion))
                {
                    continue;
                }

                pendingFeatureLockInstanceId = string.Empty;
                SetFeatureLock(candidate, motion);
                return;
            }
        }

        private void SetFeatureLock(
            CelestialBodyRuntimeContext body,
            UniverseMotionState motion)
        {
            if (body == null)
            {
                return;
            }

            lockedFeatureBody = body;
            lockedFeatureMotion = motion;
            hasLockedFeatureMotion = true;
            ResolveLocalEnvironmentContext();
            localEnvironmentContext?.SetBody(
                lockedFeatureBody,
                UniverseLocalEnvironmentContext.EnvironmentSource.FreeFlightLock);
        }

        private void FollowLockedFeature()
        {
            if (lockedFeatureBody == null ||
                !lockedFeatureBody.TryGetMotionState(out var currentMotion))
            {
                ClearFeatureLock();
                return;
            }

            if (!hasLockedFeatureMotion)
            {
                lockedFeatureMotion = currentMotion;
                hasLockedFeatureMotion = true;
                return;
            }

            var rotationDelta =
                currentMotion.Rotation *
                Quaternion.Inverse(
                    lockedFeatureMotion.Rotation);

            if (poseBridge != null &&
                poseBridge.TryGetUniversePose(out var cameraPose) &&
                cameraPose.Position.TryGetOffsetMetersFrom(
                    lockedFeatureMotion.Position,
                    out var cameraOffsetFromPreviousFeature))
            {
                // Preserve the complete camera transform in the body's local
                // frame: rotate its offset about the moving center and apply
                // the same rotation delta to the camera's orientation.
                var rotatedOffset = Rotate(
                    rotationDelta,
                    cameraOffsetFromPreviousFeature);
                var position = currentMotion.Position;
                position.AddLocalMeters(
                    rotatedOffset.x,
                    rotatedOffset.y,
                    rotatedOffset.z);
                poseBridge.TrySetUniversePose(
                    new UniverseMotionState(
                        position,
                        rotationDelta * cameraPose.Rotation));
            }
            else if (currentMotion.Position.TryGetOffsetMetersFrom(
                lockedFeatureMotion.Position,
                out var featureMotionDelta))
            {
                transform.position += new Vector3(
                    (float)featureMotionDelta.x,
                    (float)featureMotionDelta.y,
                    (float)featureMotionDelta.z);
                transform.rotation =
                    rotationDelta * transform.rotation;
            }

            lockedFeatureMotion = currentMotion;
        }

        private void ClearFeatureLock()
        {
            lockedFeatureBody = null;
            pendingFeatureLockInstanceId = string.Empty;
            hasLockedFeatureMotion = false;
            lockedFeatureMotion = default;
            ResolveLocalEnvironmentContext();
            localEnvironmentContext?.ClearIfSource(
                UniverseLocalEnvironmentContext.EnvironmentSource.FreeFlightLock);
        }

        private void ResolveLocalEnvironmentContext()
        {
            if (localEnvironmentContext != null)
            {
                return;
            }

            localEnvironmentContext =
                GetComponent<UniverseLocalEnvironmentContext>();
            if (localEnvironmentContext == null)
            {
                // Existing scenes may predate the required component. Add it
                // once at runtime rather than leaving consumers with a null
                // context until the scene is manually resaved.
                localEnvironmentContext =
                    gameObject.AddComponent<UniverseLocalEnvironmentContext>();
            }
        }

        private void PublishLockedEnvironment()
        {
            if (lockedFeatureBody == null)
            {
                localEnvironmentContext?.ClearIfSource(
                    UniverseLocalEnvironmentContext.EnvironmentSource.FreeFlightLock);
                return;
            }

            localEnvironmentContext?.SetBody(
                lockedFeatureBody,
                UniverseLocalEnvironmentContext.EnvironmentSource.FreeFlightLock);
        }

        private static DoubleVector3 Rotate(
            Quaternion rotation,
            DoubleVector3 vector)
        {
            // Quaternion-vector rotation performed in doubles so a distant
            // camera retains its orbit offset while following a rotating body.
            var tx = 2.0 * (
                rotation.y * vector.z -
                rotation.z * vector.y);
            var ty = 2.0 * (
                rotation.z * vector.x -
                rotation.x * vector.z);
            var tz = 2.0 * (
                rotation.x * vector.y -
                rotation.y * vector.x);

            return new DoubleVector3(
                vector.x + rotation.w * tx +
                    rotation.y * tz - rotation.z * ty,
                vector.y + rotation.w * ty +
                    rotation.z * tx - rotation.x * tz,
                vector.z + rotation.w * tz +
                    rotation.x * ty - rotation.y * tx);
        }

        private Vector3 GetDelta(float deltaTime)
        {
            var planarInput =
                moveAction != null &&
                moveAction.action != null
                    ? moveAction.action.ReadValue<
                        Vector2>()
                    : new Vector2(
                        horizontalControls.GetValue(
                            deltaTime),
                        depthControls.GetValue(
                            deltaTime));
            var verticalInput =
                verticalAction != null &&
                verticalAction.action != null
                    ? verticalAction.action.ReadValue<
                        float>()
                    : verticalControls.GetValue(
                        deltaTime);

            return new Vector3(
                planarInput.x,
                verticalInput,
                planarInput.y);
        }

        private void UpdateBoostMultiplier(
            float deltaTime)
        {
            if (!IsBoosting)
            {
                currentBoostMultiplier = 1.0f;
                return;
            }

            var rampRate =
                (maximumBoostMultiplier -
                    1.0f) /
                Mathf.Max(
                    0.01f,
                    boostRampSeconds);

            currentBoostMultiplier =
                Mathf.MoveTowards(
                    currentBoostMultiplier,
                    maximumBoostMultiplier,
                    rampRate *
                        deltaTime);
        }

        private void OnSpeedMultiplierPerformed(
            InputAction.CallbackContext context)
        {
            if (debugMenuVisible)
            {
                return;
            }

            var input =
                context.ReadValue<float>();

            if (Mathf.Approximately(
                    input,
                    0.0f))
            {
                return;
            }

            var stepCount =
                Mathf.Max(
                    1,
                    Mathf.RoundToInt(
                        Mathf.Abs(
                            input)));
            var multiplierChange =
                Mathf.Pow(
                    speedMultiplierStep,
                    stepCount);

            if (input < 0.0f)
            {
                multiplierChange =
                    1.0f /
                    multiplierChange;
            }

            moveSpeedMultiplier =
                Mathf.Max(
                    0.000001f,
                    moveSpeedMultiplier *
                        multiplierChange);
        }

        private void CaptureZoomCameras()
        {
            zoomCameras.Clear();
            var root = movementReference != null
                ? movementReference
                : Camera.main != null
                    ? Camera.main.transform
                    : null;
            if (root == null)
            {
                return;
            }

            var cameras = root.GetComponentsInChildren<Camera>(true);
            foreach (var camera in cameras)
            {
                if (camera == null || camera.orthographic)
                {
                    continue;
                }

                zoomCameras.Add(new CameraZoomState
                {
                    Camera = camera,
                    BaseFieldOfView = camera.fieldOfView
                });
            }
        }

        private void UpdateCameraZoom(float deltaTime)
        {
            if (zoomCameras.Count == 0)
            {
                CaptureZoomCameras();
            }

            var zooming = IsZooming;
            var blend = zoomResponse <= Mathf.Epsilon
                ? 1.0f
                : 1.0f - Mathf.Exp(-zoomResponse * deltaTime);

            for (var index = zoomCameras.Count - 1; index >= 0; index--)
            {
                var state = zoomCameras[index];
                if (state.Camera == null)
                {
                    zoomCameras.RemoveAt(index);
                    continue;
                }

                var targetFov = zooming
                    ? GetMagnifiedFieldOfView(
                        state.BaseFieldOfView,
                        zoomMagnification)
                    : state.BaseFieldOfView;
                state.Camera.fieldOfView = Mathf.Lerp(
                    state.Camera.fieldOfView,
                    targetFov,
                    blend);
            }
        }

        private void RestoreCameraZoom()
        {
            foreach (var state in zoomCameras)
            {
                if (state.Camera != null)
                {
                    state.Camera.fieldOfView = state.BaseFieldOfView;
                }
            }

            zoomCameras.Clear();
        }

        private static float GetMagnifiedFieldOfView(
            float baseFieldOfView,
            float magnification)
        {
            var halfAngleRadians =
                Mathf.Clamp(baseFieldOfView, 0.01f, 179.0f) *
                Mathf.Deg2Rad * 0.5f;
            return 2.0f * Mathf.Atan(
                Mathf.Tan(halfAngleRadians) /
                Mathf.Max(1.0f, magnification)) *
                Mathf.Rad2Deg;
        }

        private struct CameraZoomState
        {
            public Camera Camera;
            public float BaseFieldOfView;
        }

        private static bool EnableAction(
            InputActionReference actionReference)
        {
            var action =
                actionReference != null
                    ? actionReference.action
                    : null;

            if (action == null ||
                action.enabled)
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

        private void AddToDelta(Vector3 localDelta)
        {
            var reference =
                movementReference;

            if (reference == null)
            {
                var mainCamera =
                    Camera.main;

                reference =
                    mainCamera != null
                        ? mainCamera.transform
                        : transform;
            }

            remainingDelta +=
                reference.TransformDirection(
                    localDelta) *
                GetSpeedMultiplier();
        }

        private void DampenDelta()
        {
            var factor =
                CwHelper.DampenFactor(
                    damping,
                    Time.deltaTime);
            var newDelta =
                Vector3.Lerp(
                    remainingDelta,
                    Vector3.zero,
                    factor);
            var requestedMovement =
                remainingDelta -
                newDelta;
            var appliedMovement =
                ResolveCollisionMovement(
                    requestedMovement);

            if (poseBridge != null)
            {
                // Set and snap the authoritative pose before body placement (order 100).
                // Keep the unmanaged legacy path available for existing free-anchor scenes.
                if (!poseBridge.TryGetUniversePose(out var pose))
                {
                    ClearActiveInput();
                    return;
                }
                var position = pose.Position;
                position.AddLocalMeters(appliedMovement.x, appliedMovement.y, appliedMovement.z);
                if (!poseBridge.TrySetUniversePose(new UniverseMotionState(
                        position, movementReference.rotation)))
                {
                    ClearActiveInput();
                    return;
                }
            }
            else
            {
                transform.position += appliedMovement;
            }

            if (hasCollision)
            {
                newDelta =
                    Vector3.ProjectOnPlane(
                        newDelta,
                        lastCollisionNormal);
            }

            remainingDelta =
                newDelta;
        }

        private Vector3 ResolveCollisionMovement(
            Vector3 requestedMovement)
        {
            ClearCollisionRuntime();

            if (!collideWithScene ||
                collisionRadiusMeters <= 0.0f ||
                requestedMovement.sqrMagnitude <=
                    Mathf.Epsilon)
            {
                return requestedMovement;
            }

            Physics.SyncTransforms();

            if (!TrySweep(
                    transform.position,
                    requestedMovement,
                    out var firstMovement,
                    out var firstHit))
            {
                return requestedMovement;
            }

            RecordCollision(
                firstHit);

            var remainingMovement =
                requestedMovement -
                firstMovement;
            var slideMovement =
                Vector3.ProjectOnPlane(
                    remainingMovement,
                    firstHit.normal);

            if (slideMovement.sqrMagnitude <=
                Mathf.Epsilon)
            {
                return firstMovement;
            }

            var slideOrigin =
                transform.position +
                firstMovement;

            if (TrySweep(
                    slideOrigin,
                    slideMovement,
                    out var allowedSlideMovement,
                    out var slideHit))
            {
                RecordCollision(
                    slideHit);
                slideMovement =
                    allowedSlideMovement;
            }

            return
                firstMovement +
                slideMovement;
        }

        private bool TrySweep(
            Vector3 origin,
            Vector3 requestedMovement,
            out Vector3 allowedMovement,
            out RaycastHit hit)
        {
            var requestedDistance =
                requestedMovement.magnitude;

            if (requestedDistance <=
                Mathf.Epsilon)
            {
                allowedMovement =
                    Vector3.zero;
                hit = default;
                return false;
            }

            var direction =
                requestedMovement /
                requestedDistance;
            var radius =
                Mathf.Max(
                    0.01f,
                    collisionRadiusMeters);
            var skin =
                Mathf.Max(
                    0.0f,
                    collisionSkinMeters);

            if (!Physics.SphereCast(
                    origin,
                    radius,
                    direction,
                    out hit,
                    requestedDistance +
                        skin,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                allowedMovement =
                    requestedMovement;
                return false;
            }

            var allowedDistance =
                Mathf.Clamp(
                    hit.distance -
                        skin,
                    0.0f,
                    requestedDistance);

            allowedMovement =
                direction *
                allowedDistance;
            return true;
        }

        private void RecordCollision(
            RaycastHit hit)
        {
            hasCollision = true;
            lastCollisionObject =
                hit.collider != null
                    ? hit.collider.name
                    : string.Empty;
            lastCollisionDistanceMeters =
                hit.distance;
            lastCollisionNormal =
                hit.normal;
        }

        private void ClearCollisionRuntime()
        {
            hasCollision = false;
            lastCollisionObject =
                string.Empty;
            lastCollisionDistanceMeters =
                0.0f;
            lastCollisionNormal =
                Vector3.zero;
        }

        private void UpdateSpeedState()
        {
            if (!TryFindNearestNavigationFeature(
                    out var body,
                    out var ring,
                    out var surfaceDistanceMeters,
                    out var characteristicScaleMeters))
            {
                hasNearestNavigationFeature = false;
                nearestBody = null;
                nearestRing = null;
                nearestNavigationFeatureName = string.Empty;
                nearestNavigationFeatureDistanceMeters = default;
                nearestNavigationFeatureScaleMeters = default;
                resolvedSpeed = hasAutomaticCruiseSpeed
                    ? automaticCruiseSpeedMetersPerSecond
                    : minimumAutomaticSpeedMetersPerSecond;
                return;
            }

            hasNearestNavigationFeature = true;
            nearestBody = body;
            nearestRing = ring;
            nearestNavigationFeatureName = ring != null
                ? $"{ring.Body.InstanceId} ring"
                : body.InstanceId;
            nearestNavigationFeatureDistanceMeters = surfaceDistanceMeters;
            nearestNavigationFeatureScaleMeters = characteristicScaleMeters;

            var distanceLimitedSpeed =
                CalculateDistanceLimitedSpeed(surfaceDistanceMeters);
            resolvedSpeed = hasAutomaticCruiseSpeed
                ? Mathf.Min(
                    automaticCruiseSpeedMetersPerSecond,
                    distanceLimitedSpeed)
                : distanceLimitedSpeed;
        }

        private float CalculateDistanceLimitedSpeed(
            double surfaceDistanceMeters)
        {
            var speed = Math.Max(
                minimumAutomaticSpeedMetersPerSecond,
                Math.Max(0.0, surfaceDistanceMeters) /
                    Math.Max(0.01f, initialFeatureTravelSeconds));

            if (!IsFinite(speed))
            {
                return minimumAutomaticSpeedMetersPerSecond;
            }

            return Mathf.Max(
                minimumAutomaticSpeedMetersPerSecond,
                (float)Math.Min(speed, float.MaxValue));
        }

        private bool TryFindNearestNavigationFeature(
            out CelestialBodyRuntimeContext selectedBody,
            out CelestialRingMeshPresentation selectedRing,
            out double surfaceDistanceMeters,
            out double characteristicScaleMeters)
        {
            selectedBody = null;
            selectedRing = null;
            surfaceDistanceMeters = default;
            characteristicScaleMeters = default;
            ResolveUniverseFrame();

            if (!TryGetCameraUniversePosition(out var cameraPosition))
            {
                return false;
            }

            var sceneCameraPosition = movementReference != null
                ? movementReference.position
                : transform.position;
            var nearestSurfaceDistanceMeters = double.PositiveInfinity;

            // Use the same global motion contract as the observer and orbit
            // systems. Bodies generated on trajectories deliberately have no
            // Gravity Engine NBody, so GE positions cannot be this selector's
            // eligibility boundary.
            foreach (var bodyContext in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (!IsEligibleBody(bodyContext) ||
                    !bodyContext.TryGetMotionState(out var bodyMotion) ||
                    !cameraPosition.TryGetOffsetMetersFrom(
                        bodyMotion.Position,
                        out var offsetMeters))
                {
                    continue;
                }

                var radialDistanceMeters = Math.Sqrt(
                    offsetMeters.x * offsetMeters.x +
                    offsetMeters.y * offsetMeters.y +
                    offsetMeters.z * offsetMeters.z);
                var candidateDistance = Math.Abs(
                    radialDistanceMeters -
                    bodyContext.ConfiguredReferenceRadiusMeters);
                if (!IsFinite(candidateDistance) ||
                    candidateDistance >= nearestSurfaceDistanceMeters)
                {
                    continue;
                }

                selectedBody = bodyContext;
                selectedRing = null;
                surfaceDistanceMeters = candidateDistance;
                characteristicScaleMeters =
                    bodyContext.ConfiguredReferenceRadiusMeters;
                nearestSurfaceDistanceMeters = candidateDistance;
            }

            foreach (var ringPresentation in
                CelestialRingMeshPresentation.ActivePresentations)
            {
                if (ringPresentation == null || ringPresentation.Body == null ||
                    !IsEligibleBody(ringPresentation.Body) ||
                    !ringPresentation.TryGetNearestSurfaceDistance(
                        sceneCameraPosition,
                        out var candidateDistance,
                        out var candidateScale) ||
                    candidateDistance >= nearestSurfaceDistanceMeters)
                {
                    continue;
                }

                selectedBody = ringPresentation.Body;
                selectedRing = ringPresentation;
                surfaceDistanceMeters = candidateDistance;
                characteristicScaleMeters = candidateScale;
                nearestSurfaceDistanceMeters = candidateDistance;
            }

            return selectedBody != null;
        }

        private bool TryGetCameraUniversePosition(
            out UniversePosition cameraPosition)
        {
            if (poseBridge != null &&
                poseBridge.TryGetUniversePose(out var cameraPose))
            {
                cameraPosition = cameraPose.Position;
                return true;
            }

            if (universeFrame == null ||
                !universeFrame.FrameOriginInitialized)
            {
                cameraPosition = default;
                return false;
            }

            var scenePosition = movementReference != null
                ? movementReference.position
                : transform.position;
            cameraPosition = universeFrame.FrameOrigin;
            cameraPosition.AddLocalMeters(
                scenePosition.x,
                scenePosition.y,
                scenePosition.z);
            return true;
        }

        private bool IsEligibleBody(
            CelestialBodyRuntimeContext bodyContext)
        {
            return bodyContext != null &&
                bodyContext.HasValidConfiguration &&
                IsFinite(bodyContext.ConfiguredReferenceRadiusMeters) &&
                bodyContext.ConfiguredReferenceRadiusMeters > 0.0 &&
                (universeFrame == null ||
                    bodyContext.UniverseFrame == universeFrame);
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

            if (debugMenuVisible)
            {
                ClearActiveInput();
            }
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

            if (debugMenuVisible)
            {
                ClearActiveInput();
            }
        }

        private void ClearActiveInput()
        {
            remainingDelta = Vector3.zero;
            currentBoostMultiplier = 1.0f;
        }

        private void ResolveUniverseFrame()
        {
            if (universeFrame != null)
            {
                return;
            }

            var anchorSources =
                GetComponents<
                    UniverseAnchorSource>();

            for (var index = 0;
                index < anchorSources.Length;
                index++)
            {
                var anchorSource =
                    anchorSources[index];

                if (anchorSource.UniverseFrame ==
                    null)
                {
                    continue;
                }

                universeFrame =
                    anchorSource.UniverseFrame;
                return;
            }
        }

        private float GetSpeedMultiplier()
        {
            return
                CurrentMoveSpeedMetersPerSecond;
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(
                    value) &&
                !float.IsInfinity(
                    value);
        }
    }
}
