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
        [Min(0.0f)]
        private float speedMin = 1.0f;

        [SerializeField]
        [Min(0.0f)]
        private float speedMax = 10.0f;

        [SerializeField]
        [Tooltip("Height above the nearest planet, measured as a fraction of its radius, where maximum speed is reached.")]
        [Min(0.0001f)]
        private float maxSpeedAltitudeRadiusFraction =
            0.2f;

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
        [Min(0.0001f)]
        private float minimumSpeedMultiplier = 0.0625f;

        [SerializeField]
        [Min(0.0001f)]
        private float maximumSpeedMultiplier = 64.0f;

        [SerializeField]
        [Min(0.0001f)]
        private float moveSpeedMultiplier = 1.0f;

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
        private bool hasNearestPlanet;

        [SerializeField]
        private CelestialBodyRuntimeContext nearestPlanet;

        [SerializeField]
        private double nearestPlanetAltitudeMeters;

        [SerializeField]
        private double nearestPlanetRadiusMeters;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float resolvedSpeedBlend = 1.0f;

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

        public bool HasNearestPlanet =>
            hasNearestPlanet;

        public double NearestPlanetAltitudeMeters =>
            nearestPlanetAltitudeMeters;

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
            minimumSpeedMultiplier =
                Mathf.Max(
                    0.0001f,
                    minimumSpeedMultiplier);
            maximumSpeedMultiplier =
                Mathf.Max(
                    minimumSpeedMultiplier,
                    maximumSpeedMultiplier);
            moveSpeedMultiplier =
                Mathf.Clamp(
                    moveSpeedMultiplier,
                    minimumSpeedMultiplier,
                    maximumSpeedMultiplier);
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
                Mathf.Clamp(
                    moveSpeedMultiplier *
                        multiplierChange,
                    minimumSpeedMultiplier,
                    maximumSpeedMultiplier);
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
            var minimumSpeed =
                Mathf.Max(
                    0.0f,
                    speedMin);
            var maximumSpeed =
                Mathf.Max(
                    minimumSpeed,
                    speedMax);

            if (!TryFindNearestPlanet(
                    out var selectedPlanet,
                    out var altitudeMeters,
                    out var radiusMeters))
            {
                hasNearestPlanet = false;
                nearestPlanet = null;
                nearestPlanetAltitudeMeters =
                    default;
                nearestPlanetRadiusMeters =
                    default;
                resolvedSpeedBlend = 1.0f;
                resolvedSpeed =
                    maximumSpeed;
                return;
            }

            hasNearestPlanet = true;
            nearestPlanet =
                selectedPlanet;
            nearestPlanetAltitudeMeters =
                altitudeMeters;
            nearestPlanetRadiusMeters =
                radiusMeters;

            var maximumSpeedAltitudeMeters =
                radiusMeters *
                Math.Max(
                    0.0001,
                    maxSpeedAltitudeRadiusFraction);
            var speedBlend =
                maximumSpeedAltitudeMeters >
                    double.Epsilon
                    ? altitudeMeters /
                        maximumSpeedAltitudeMeters
                    : 1.0;

            resolvedSpeedBlend =
                Mathf.Clamp01(
                    (float)speedBlend);
            resolvedSpeed =
                Mathf.Lerp(
                    minimumSpeed,
                    maximumSpeed,
                    resolvedSpeedBlend);
        }

        private bool TryFindNearestPlanet(
            out CelestialBodyRuntimeContext selectedPlanet,
            out double altitudeMeters,
            out double radiusMeters)
        {
            selectedPlanet = null;
            altitudeMeters = default;
            radiusMeters = default;

            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup())
            {
                return false;
            }

            var physicalScale =
                gravityEngine.GetPhysicalScale();

            if (!IsFinite(
                    physicalScale) ||
                physicalScale <= 0.0f)
            {
                return false;
            }

            ResolveUniverseFrame();

            var anchorPosition =
                transform.position;
            var nearestSurfaceDistanceMeters =
                double.PositiveInfinity;

            foreach (var bodyContext in
                CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (!IsEligiblePlanet(
                        bodyContext))
                {
                    continue;
                }

                var gravityBody =
                    bodyContext.GravityBody;
                var planetRadiusMeters =
                    bodyContext.ConfiguredReferenceRadiusMeters;
                var physicsPosition =
                    gravityEngine.GetPositionDoubleV3(
                        gravityBody);
                var deltaX =
                    anchorPosition.x -
                    physicsPosition.x *
                        physicalScale;
                var deltaY =
                    anchorPosition.y -
                    physicsPosition.y *
                        physicalScale;
                var deltaZ =
                    anchorPosition.z -
                    physicsPosition.z *
                        physicalScale;
                var radialDistanceMeters =
                    Math.Sqrt(
                        deltaX *
                            deltaX +
                        deltaY *
                            deltaY +
                        deltaZ *
                            deltaZ);
                var candidateAltitudeMeters =
                    radialDistanceMeters -
                    planetRadiusMeters;
                var surfaceDistanceMeters =
                    Math.Abs(
                        candidateAltitudeMeters);

                if (!IsFinite(
                        surfaceDistanceMeters) ||
                    surfaceDistanceMeters >=
                        nearestSurfaceDistanceMeters)
                {
                    continue;
                }

                selectedPlanet =
                    bodyContext;
                altitudeMeters =
                    candidateAltitudeMeters;
                radiusMeters =
                    planetRadiusMeters;
                nearestSurfaceDistanceMeters =
                    surfaceDistanceMeters;
            }

            return
                selectedPlanet != null;
        }

        private bool IsEligiblePlanet(
            CelestialBodyRuntimeContext bodyContext)
        {
            if (bodyContext == null ||
                !bodyContext.HasValidConfiguration ||
                bodyContext.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic ||
                bodyContext.GravityBody == null ||
                !IsFinite(
                    bodyContext.ConfiguredReferenceRadiusMeters) ||
                bodyContext.ConfiguredReferenceRadiusMeters <=
                    0.0)
            {
                return false;
            }

            return
                universeFrame == null ||
                bodyContext.UniverseFrame ==
                    universeFrame;
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
