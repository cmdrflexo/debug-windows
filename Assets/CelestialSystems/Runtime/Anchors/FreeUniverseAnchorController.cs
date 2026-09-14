/*
 * Moves a nonphysical universe anchor using camera-relative free-flight controls, with optional scene collision and planet-relative automatic speed.
 */

using System;
using CW.Common;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
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
        [Tooltip("Initial travel time to the nearest body or ring after entering Free mode.")]
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
        private int activationFrame;

        // Configure while disabled; the mode owner owns any runtime input references.
        public void ConfigureSharedRig(
            SgtUniverseOriginBridge bridge, Transform cameraTransform,
            InputActionReference move, InputActionReference vertical,
            InputActionReference speed, InputActionReference boost,
            InputActionReference lookDelta, InputActionReference roll)
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
            ClearActiveInput();
        }
        private bool enabledMoveAction;
        private bool enabledVerticalAction;
        private bool enabledSpeedMultiplierAction;
        private bool enabledBoostAction;
        private bool enabledLookDeltaAction;
        private bool enabledRollAction;
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

        public double NearestNavigationFeatureScaleMeters =>
            nearestNavigationFeatureScaleMeters;

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

        // Called by the shared mode owner after it has handed the displayed
        // camera pose to Free mode. It deliberately sets the multiplier, so
        // wheel adjustments continue from this practical starting speed.
        public void SetInitialSpeedFromNearestFeature()
        {
            UpdateSpeedState();

            if (!hasNearestNavigationFeature ||
                nearestNavigationFeatureDistanceMeters <= 0.0 ||
                resolvedSpeed <= 0.0f)
            {
                moveSpeedMultiplier = 1.0f;
                return;
            }

            var desiredSpeed = Math.Max(
                minimumAutomaticSpeedMetersPerSecond,
                nearestNavigationFeatureDistanceMeters /
                Math.Max(0.01f, initialFeatureTravelSeconds));
            var multiplier = desiredSpeed / resolvedSpeed;
            moveSpeedMultiplier = IsFinite(multiplier)
                ? Mathf.Max(0.000001f, (float)Math.Min(multiplier, float.MaxValue))
                : 1.0f;
        }

        private void Awake()
        {
            ResolveUniverseFrame();
        }

        private void OnEnable()
        {
            activationFrame = Time.frameCount;
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

            enabledMoveAction = false;
            enabledVerticalAction = false;
            enabledSpeedMultiplierAction = false;
            enabledBoostAction = false;
            enabledLookDeltaAction = false;
            enabledRollAction = false;
            currentBoostMultiplier = 1.0f;
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
            if (lookDeltaAction != null && lookDeltaAction.action != null)
            {
                var delta = lookDeltaAction.action.ReadValue<Vector2>() *
                    lookDegreesPerPixel;
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
                resolvedSpeed = minimumAutomaticSpeedMetersPerSecond;
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
            resolvedSpeed = CalculateAutomaticSpeed(
                surfaceDistanceMeters,
                characteristicScaleMeters);
        }

        // The speed response is derived from distance to the nearest physical
        // feature, scaled by that feature's size. There is deliberately no
        // manual maximum: astronomical separation produces astronomical travel
        // speed, while the positive floor prevents a stationary camera on a
        // surface or when no generated feature exists.
        private float CalculateAutomaticSpeed(
            double surfaceDistanceMeters,
            double characteristicScaleMeters)
        {
            var distance = Math.Max(0.0, surfaceDistanceMeters);
            var scaleFactor = Math.Max(
                1.0,
                characteristicScaleMeters / 1000000.0);
            var speed = Math.Sqrt(distance * scaleFactor);
            if (!IsFinite(speed))
            {
                speed = minimumAutomaticSpeedMetersPerSecond;
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
