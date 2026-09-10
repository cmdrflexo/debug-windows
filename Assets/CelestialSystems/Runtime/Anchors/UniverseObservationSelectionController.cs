/*
 * Owns the selected celestial observation target and deterministic body-target cycling.
 */

using System;
using System.Collections.Generic;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class UniverseObservationSelectionController : MonoBehaviour
    {
        [Header("Scope")]
        [SerializeField]
        [Tooltip("When assigned, only bodies belonging to this universe frame are selectable.")]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private DebugWindowManager debugWindowManager;

        [Header("Initial Selection")]
        [SerializeField]
        private CelestialBodyRuntimeContext selectedTarget;

        [SerializeField]
        private string initialTargetInstanceId;

        [SerializeField]
        private bool automaticallySelectFirstTarget = true;

        [Header("Input Actions")]
        [SerializeField]
        private InputActionReference nextTargetAction;

        [SerializeField]
        private InputActionReference previousTargetAction;

        [Header("Runtime")]
        [SerializeField]
        private int selectedTargetIndex = -1;

        [SerializeField]
        private int selectableTargetCount;

        private readonly List<CelestialBodyRuntimeContext> orderedTargets =
            new List<CelestialBodyRuntimeContext>();
        private readonly HashSet<CelestialBodyRuntimeContext> targetSet =
            new HashSet<CelestialBodyRuntimeContext>();
        private readonly HashSet<CelestialBodyRuntimeContext> appendedTargets =
            new HashSet<CelestialBodyRuntimeContext>();
        private bool enabledNextTargetAction;
        private bool enabledPreviousTargetAction;
        private DebugWindowManager subscribedDebugWindowManager;
        private bool debugMenuVisible;

        public CelestialBodyRuntimeContext SelectedTarget => selectedTarget;

        public int SelectedTargetIndex => selectedTargetIndex;

        public int SelectableTargetCount => selectableTargetCount;

        public event Action<
            CelestialBodyRuntimeContext,
            CelestialBodyRuntimeContext> TargetChanged;

        private void OnEnable()
        {
            enabledNextTargetAction = EnableAction(nextTargetAction);
            enabledPreviousTargetAction = EnableAction(previousTargetAction);

            if (nextTargetAction != null && nextTargetAction.action != null)
            {
                nextTargetAction.action.performed += OnNextTargetPerformed;
            }

            if (previousTargetAction != null && previousTargetAction.action != null)
            {
                previousTargetAction.action.performed += OnPreviousTargetPerformed;
            }

            SubscribeToDebugMenu();
            RefreshTargets();
        }

        private void Start()
        {
            SubscribeToDebugMenu();
        }

        private void Update()
        {
            if (selectedTarget == null && automaticallySelectFirstTarget)
            {
                RefreshTargets();

                if (!string.IsNullOrWhiteSpace(initialTargetInstanceId) &&
                    SelectTargetByInstanceId(initialTargetInstanceId))
                {
                    return;
                }

                if (orderedTargets.Count > 0)
                {
                    SelectTarget(orderedTargets[0]);
                }
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromDebugMenu();

            if (nextTargetAction != null && nextTargetAction.action != null)
            {
                nextTargetAction.action.performed -= OnNextTargetPerformed;
            }

            if (previousTargetAction != null && previousTargetAction.action != null)
            {
                previousTargetAction.action.performed -= OnPreviousTargetPerformed;
            }

            DisableAction(nextTargetAction, enabledNextTargetAction);
            DisableAction(previousTargetAction, enabledPreviousTargetAction);
            enabledNextTargetAction = false;
            enabledPreviousTargetAction = false;
        }

        public bool SelectTarget(CelestialBodyRuntimeContext newTarget)
        {
            if (newTarget != null && !IsInScope(newTarget))
            {
                return false;
            }

            RefreshTargets();

            if (newTarget != null && !targetSet.Contains(newTarget))
            {
                return false;
            }

            var previousTarget = selectedTarget;

            if (previousTarget == newTarget)
            {
                UpdateSelectedIndex();
                return true;
            }

            selectedTarget = newTarget;
            initialTargetInstanceId =
                newTarget != null
                    ? newTarget.InstanceId
                    : initialTargetInstanceId;
            UpdateSelectedIndex();
            TargetChanged?.Invoke(previousTarget, selectedTarget);
            return true;
        }

        public bool SelectTargetByInstanceId(string instanceId)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return false;
            }

            RefreshTargets();

            for (var index = 0; index < orderedTargets.Count; index++)
            {
                var candidate = orderedTargets[index];

                if (string.Equals(
                        candidate.InstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                {
                    return SelectTarget(candidate);
                }
            }

            return false;
        }

        [ContextMenu("Select Next Target")]
        public void SelectNextTarget()
        {
            SelectRelativeTarget(1);
        }

        [ContextMenu("Select Previous Target")]
        public void SelectPreviousTarget()
        {
            SelectRelativeTarget(-1);
        }

        [ContextMenu("Refresh Targets")]
        public void RefreshTargets()
        {
            orderedTargets.Clear();
            targetSet.Clear();
            appendedTargets.Clear();

            foreach (var context in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (IsInScope(context))
                {
                    targetSet.Add(context);
                }
            }

            var roots = new List<CelestialBodyRuntimeContext>();

            foreach (var context in targetSet)
            {
                var parent = GetReferenceBody(context);

                if (parent == null || !targetSet.Contains(parent))
                {
                    roots.Add(context);
                }
            }

            roots.Sort(CompareSiblings);

            for (var index = 0; index < roots.Count; index++)
            {
                AppendTargetTree(roots[index]);
            }

            if (orderedTargets.Count < targetSet.Count)
            {
                var remaining = new List<CelestialBodyRuntimeContext>();

                foreach (var context in targetSet)
                {
                    if (!appendedTargets.Contains(context))
                    {
                        remaining.Add(context);
                    }
                }

                remaining.Sort(CompareByInstanceId);

                for (var index = 0; index < remaining.Count; index++)
                {
                    AppendTargetTree(remaining[index]);
                }
            }

            selectableTargetCount = orderedTargets.Count;
            UpdateSelectedIndex();
        }

        private void SelectRelativeTarget(int direction)
        {
            RefreshTargets();

            if (orderedTargets.Count == 0)
            {
                SelectTarget(null);
                return;
            }

            var currentIndex = orderedTargets.IndexOf(selectedTarget);
            var nextIndex = currentIndex < 0
                ? direction >= 0
                    ? 0
                    : orderedTargets.Count - 1
                : (currentIndex + direction + orderedTargets.Count) %
                    orderedTargets.Count;

            SelectTarget(orderedTargets[nextIndex]);
        }

        private void AppendTargetTree(CelestialBodyRuntimeContext context)
        {
            if (context == null || !appendedTargets.Add(context))
            {
                return;
            }

            orderedTargets.Add(context);

            var children = new List<CelestialBodyRuntimeContext>();

            foreach (var candidate in targetSet)
            {
                if (GetReferenceBody(candidate) == context)
                {
                    children.Add(candidate);
                }
            }

            children.Sort(CompareSiblings);

            for (var index = 0; index < children.Count; index++)
            {
                AppendTargetTree(children[index]);
            }
        }

        private void UpdateSelectedIndex()
        {
            selectedTargetIndex = orderedTargets.IndexOf(selectedTarget);
        }

        private bool IsInScope(CelestialBodyRuntimeContext context)
        {
            return context != null &&
                (universeFrame == null || context.UniverseFrame == universeFrame);
        }

        private static CelestialBodyRuntimeContext GetReferenceBody(
            CelestialBodyRuntimeContext context)
        {
            return context != null &&
                context.MotionProviderSource is TrajectoryCelestialBodyMotionProvider provider
                    ? provider.ReferenceBody
                    : null;
        }

        private static int CompareSiblings(
            CelestialBodyRuntimeContext left,
            CelestialBodyRuntimeContext right)
        {
            var orbitComparison = GetOrbitSizeMeters(left).CompareTo(
                GetOrbitSizeMeters(right));

            return orbitComparison != 0
                ? orbitComparison
                : CompareByInstanceId(left, right);
        }

        private static int CompareByInstanceId(
            CelestialBodyRuntimeContext left,
            CelestialBodyRuntimeContext right)
        {
            return string.CompareOrdinal(
                left != null ? left.InstanceId : string.Empty,
                right != null ? right.InstanceId : string.Empty);
        }

        private static double GetOrbitSizeMeters(
            CelestialBodyRuntimeContext context)
        {
            if (context == null ||
                !(context.MotionProviderSource is TrajectoryCelestialBodyMotionProvider provider) ||
                provider.Trajectory == null)
            {
                return 0.0;
            }

            var trajectory = provider.Trajectory;

            if (trajectory.Kind == CelestialTrajectoryKind.KeplerianConic &&
                trajectory.Conic != null)
            {
                return Math.Abs(trajectory.Conic.SemiMajorAxisMeters);
            }

            return Math.Abs(trajectory.OrbitalRadiusMeters);
        }

        private void OnNextTargetPerformed(InputAction.CallbackContext context)
        {
            if (!debugMenuVisible)
            {
                SelectNextTarget();
            }
        }

        private void OnPreviousTargetPerformed(InputAction.CallbackContext context)
        {
            if (!debugMenuVisible)
            {
                SelectPreviousTarget();
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
    }
}
