/*
 * Owns navigation modes on one SGT rig. Mode changes preserve the displayed
 * universe pose; only one movement/look controller may drive the rig at a time.
 */
using System.Collections.Generic;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class UniverseCameraModeController : MonoBehaviour
    {
        private const string GeneratedDecorationsName =
            "Generated Observation Decorations";
        private const string GeneratedPivotMarkerName =
            "Generated Observation Pivot Marker";

        public enum NavigationMode { Observer, Free }

        [Header("Shared Rig")]
        [SerializeField] private SgtUniverseOriginBridge anchorBridge;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private UniverseObservationAnchorController observer;
        [SerializeField] private FreeUniverseAnchorController freeFlight;
        [SerializeField] private UniverseObservationSelectionController selection;
        [Tooltip("Root containing the grid, markers, orbits, and other observer-only decorations.")]
        [SerializeField] private GameObject observerInterfaceRoot;
        [Tooltip("Other look components that must not compete with the navigation modes.")]
        [SerializeField] private Behaviour[] legacyLookBehaviours;
        [SerializeField] private NavigationMode mode = NavigationMode.Observer;

        [Header("Mode Actions (defaults: F1, F2, V)")]
        [SerializeField] private InputActionReference observerModeAction;
        [SerializeField] private InputActionReference freeModeAction;
        [SerializeField] private InputActionReference toggleModeAction;

        [Header("Free Look (defaults: pointer delta; always active in Free mode)")]
        [Tooltip("Pointer delta for FreeUniverseAnchorController. No hold action is used.")]
        [SerializeField] private InputActionReference lookDeltaAction;
        [Tooltip("Signed axis for FreeUniverseAnchorController roll (default: Q/E).")]
        [SerializeField] private InputActionReference rollAction;
        [Tooltip("Free-mode nearest feature lock (default: L).")]
        [SerializeField] private InputActionReference lockNearestFeatureAction;
        [Tooltip("Held Free-mode optical zoom (default: right mouse button).")]
        [SerializeField] private InputActionReference freeZoomAction;

        [Header("Free Movement Defaults")]
        [Tooltip("Existing input references on FreeUniverseAnchorController take precedence.")]
        [SerializeField] private InputActionReference moveAction;
        [SerializeField] private InputActionReference verticalAction;
        [SerializeField] private InputActionReference speedAction;
        [SerializeField] private InputActionReference boostAction;

        [Header("Free Cursor")]
        [SerializeField] private bool hideCursorInFreeMode = true;
        [SerializeField] private CursorLockMode freeCursorLockMode =
            CursorLockMode.Locked;

        private readonly List<InputActionReference> ownedReferences = new List<InputActionReference>();
        private readonly List<InputAction> enabledActions = new List<InputAction>();
        private InputActionAsset runtimeInputAsset;
        private InputActionMap runtimeInputMap;
        private bool initialized;
        private bool cursorStateCaptured;
        private bool capturedCursorVisible;
        private CursorLockMode capturedCursorLockMode;
        public NavigationMode Mode => mode;

        private void Awake()
        {
            observer = observer != null ? observer : GetComponent<UniverseObservationAnchorController>();
            freeFlight = freeFlight != null ? freeFlight : GetComponent<FreeUniverseAnchorController>();
            selection = selection != null ? selection : GetComponent<UniverseObservationSelectionController>();
            if (observerInterfaceRoot == null)
            {
                var grid = FindFirstObjectByType<UniverseObservationGridRenderer>(
                    FindObjectsInactive.Include);
                observerInterfaceRoot = grid != null ? grid.gameObject : null;
            }
            viewCamera = viewCamera != null ? viewCamera : Camera.main;
            anchorBridge = anchorBridge != null ? anchorBridge : FindFirstObjectByType<SgtUniverseOriginBridge>();
            if (observer == null || freeFlight == null || viewCamera == null ||
                anchorBridge == null || anchorBridge.UniverseFrame == null ||
                viewCamera.transform.parent != transform)
            {
                Debug.LogError("Camera modes require both controllers on the shared anchor, its bridge, and a direct child camera.", this);
                enabled = false;
                return;
            }

            freeFlight.enabled = false;
            if (legacyLookBehaviours != null)
                foreach (var behaviour in legacyLookBehaviours)
                    if (behaviour != null) behaviour.enabled = false;

            observerModeAction = Resolve(observerModeAction, "Observer Mode", InputActionType.Button, "<Keyboard>/f1");
            freeModeAction = Resolve(freeModeAction, "Free Mode", InputActionType.Button, "<Keyboard>/f2");
            toggleModeAction = Resolve(toggleModeAction, "Toggle Camera Mode", InputActionType.Button, "<Keyboard>/v");
            lookDeltaAction = Resolve(lookDeltaAction, "Free Look Delta", InputActionType.Value, "<Pointer>/delta");
            rollAction = Resolve(rollAction, "Free Roll", InputActionType.Value);
            lockNearestFeatureAction = Resolve(
                lockNearestFeatureAction,
                "Lock Nearest Feature",
                InputActionType.Button,
                "<Keyboard>/l");
            freeZoomAction = Resolve(
                freeZoomAction,
                "Free Zoom",
                InputActionType.Button,
                "<Mouse>/rightButton");
            if (ownedReferences.Contains(rollAction))
                rollAction.action.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/q").With("Positive", "<Keyboard>/e");
            moveAction = Resolve(moveAction, "Free Move", InputActionType.Value);
            if (ownedReferences.Contains(moveAction))
                moveAction.action.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                    .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            verticalAction = Resolve(verticalAction, "Free Vertical", InputActionType.Value);
            if (ownedReferences.Contains(verticalAction))
                verticalAction.action.AddCompositeBinding("1DAxis")
                    .With("Negative", "<Keyboard>/f").With("Positive", "<Keyboard>/r");
            speedAction = Resolve(speedAction, "Free Speed", InputActionType.Value, "<Mouse>/scroll/y");
            boostAction = Resolve(boostAction, "Free Boost", InputActionType.Button, "<Keyboard>/leftShift");
            freeFlight.ConfigureSharedRig(anchorBridge, viewCamera.transform,
                moveAction, verticalAction, speedAction, boostAction,
                lookDeltaAction, rollAction, lockNearestFeatureAction,
                freeZoomAction);
            initialized = true;
        }

        private InputActionReference Resolve(InputActionReference supplied, string name,
            InputActionType type, string binding = null)
        {
            if (supplied != null && supplied.action != null) return supplied;
            // InputActionReference requires an action belonging to an asset.
            if (runtimeInputAsset == null)
            {
                runtimeInputAsset = ScriptableObject.CreateInstance<InputActionAsset>();
                runtimeInputAsset.name = "Camera Mode Defaults (Runtime)";
                runtimeInputMap = new InputActionMap("Camera Modes");
                runtimeInputAsset.AddActionMap(runtimeInputMap);
            }
            var reference = InputActionReference.Create(runtimeInputMap.AddAction(name, type, binding));
            ownedReferences.Add(reference);
            return reference;
        }

        private void OnEnable()
        {
            if (!initialized) return;
            Enable(observerModeAction);
            Enable(freeModeAction);
            Enable(toggleModeAction);
            ApplyMode();
        }

        private void Enable(InputActionReference reference)
        {
            if (reference.action.enabled) return;
            reference.action.Enable();
            enabledActions.Add(reference.action);
        }

        private void OnDisable()
        {
            if (!initialized) return;
            freeFlight.enabled = false;
            observer.SetNavigationActive(false);
            if (selection != null) selection.NavigationEnabled = false;
            RestoreCursorState();
            foreach (var action in enabledActions) action.Disable();
            enabledActions.Clear();
        }

        private void OnDestroy()
        {
            foreach (var reference in ownedReferences)
            {
                Destroy(reference);
            }
            ownedReferences.Clear();
            if (runtimeInputAsset != null)
            {
                runtimeInputAsset.Disable();
                runtimeInputMap.Dispose();
                Destroy(runtimeInputAsset);
            }
        }

        private void ApplyMode()
        {
            var observerActive = mode == NavigationMode.Observer;
            observer.SetNavigationActive(observerActive);
            freeFlight.enabled = mode == NavigationMode.Free;
            if (selection != null) selection.NavigationEnabled = observerActive;

            // Set the shared flags before disabling the authored grid object.
            // Its decorators generate render objects outside that hierarchy, so
            // those generated roots must also be toggled directly.
            UniverseObservationBodyMarkerDecorator.SetNavigationVisible(observerActive);
            UniverseObservationPivotMarker.SetNavigationVisible(observerActive);
            UniverseObservationOrbitDecorator.SetNavigationVisible(observerActive);
            UniverseObservationTrailDecorator.SetNavigationVisible(observerActive);

            if (observerInterfaceRoot != null &&
                observerInterfaceRoot.activeSelf != observerActive)
            {
                observerInterfaceRoot.SetActive(observerActive);
            }

            SetGeneratedObservationObjectsActive(observerActive);
            SetFreeCursorActive(!observerActive);
        }

        private static void SetGeneratedObservationObjectsActive(bool active)
        {
            var transforms = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (var index = 0; index < transforms.Length; index++)
            {
                var candidate = transforms[index];
                if (candidate == null ||
                    !candidate.gameObject.scene.IsValid() ||
                    (candidate.name != GeneratedDecorationsName &&
                        candidate.name != GeneratedPivotMarkerName))
                {
                    continue;
                }

                if (candidate.gameObject.activeSelf != active)
                {
                    candidate.gameObject.SetActive(active);
                }
            }
        }

        private void SetFreeCursorActive(bool active)
        {
            if (!active)
            {
                RestoreCursorState();
                return;
            }

            if (!cursorStateCaptured)
            {
                capturedCursorVisible = Cursor.visible;
                capturedCursorLockMode = Cursor.lockState;
                cursorStateCaptured = true;
            }

            Cursor.lockState = freeCursorLockMode;
            Cursor.visible = !hideCursorInFreeMode;
        }

        private void RestoreCursorState()
        {
            if (!cursorStateCaptured)
            {
                return;
            }

            Cursor.lockState = capturedCursorLockMode;
            Cursor.visible = capturedCursorVisible;
            cursorStateCaptured = false;
        }

        public bool SetMode(NavigationMode requested)
        {
            if (!initialized || !isActiveAndEnabled || !anchorBridge.IsActiveSource) return false;
            if (requested == mode) return true;
            if (!TryGetDisplayedPose(out var pose)) return false;
            freeFlight.enabled = false;
            if (requested == NavigationMode.Observer)
                observer.AdoptUniversePose(pose);
            else
                observer.RecordExternalPose(pose);
            mode = requested;
            ApplyMode();
            if (mode == NavigationMode.Free)
            {
                freeFlight.SetInitialSpeedFromNearestFeature();
            }

            return true;
        }

        private bool TryGetDisplayedPose(out UniverseMotionState pose)
        {
            // Read scene position against the settled origin rather than an SGT
            // Position cache that may not yet reflect a transform movement.
            var position = anchorBridge.UniverseFrame.FrameOrigin;
            var scenePosition = viewCamera.transform.position;
            position.AddLocalMeters(scenePosition.x, scenePosition.y, scenePosition.z);
            pose = new UniverseMotionState(position, viewCamera.transform.rotation);
            return anchorBridge.UniverseFrame.FrameOriginInitialized;
        }

        private void Update()
        {
            if (!initialized || !anchorBridge.IsActiveSource) return;
            var debug = DebugWindowManager.Instance;
            var debugMenuVisible = debug != null && debug.MenuVisible;
            SetFreeCursorActive(
                mode == NavigationMode.Free &&
                !debugMenuVisible);
            if (debugMenuVisible) return;
            if (observerModeAction.action.WasPressedThisFrame())
            {
                SetMode(NavigationMode.Observer);
                return;
            }
            if (freeModeAction.action.WasPressedThisFrame())
            {
                SetMode(NavigationMode.Free);
                return;
            }
            if (toggleModeAction.action.WasPressedThisFrame())
            {
                SetMode(mode == NavigationMode.Observer ? NavigationMode.Free : NavigationMode.Observer);
            }
        }

        private void LateUpdate()
        {
            if (initialized && mode == NavigationMode.Free &&
                anchorBridge.IsActiveSource && TryGetDisplayedPose(out var pose))
                observer.RecordExternalPose(pose);
        }
    }
}
