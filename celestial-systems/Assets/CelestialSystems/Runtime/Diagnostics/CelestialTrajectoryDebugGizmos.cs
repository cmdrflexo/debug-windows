/*
 * Draws optional trajectory diagnostics for a generated celestial body.
 */

using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialTrajectoryDebugGizmos :
        MonoBehaviour
    {
        public enum GizmoVisibility
        {
            Always = 0,
            Selected = 1
        }

        public static bool GlobalVisibility { get; set; } = true;

        [Header("Visibility")]
        [SerializeField]
        private bool showGizmos = true;

        [SerializeField]
        private GizmoVisibility visibility =
            GizmoVisibility.Always;

        [SerializeField]
        private bool showOrbitPath = true;

        [SerializeField]
        private bool showReferenceLine = true;

        [SerializeField]
        private bool showApsides = true;

        [SerializeField]
        private bool showLabels = true;

        [Header("Appearance")]
        [SerializeField]
        [Range(16, 256)]
        private int pathSegments = 96;

        [SerializeField]
        [Min(0.0f)]
        private float markerRadius = 10000000.0f;

        [SerializeField]
        private Color orbitColor =
            new Color(0.15f, 0.8f, 1.0f, 0.85f);

        [SerializeField]
        private Color periapsisColor =
            new Color(0.2f, 1.0f, 0.35f, 1.0f);

        [SerializeField]
        private Color apoapsisColor =
            new Color(1.0f, 0.45f, 0.15f, 1.0f);

        private TrajectoryCelestialBodyMotionProvider provider;

        public bool ShowGizmos
        {
            get => showGizmos;
            set => showGizmos = value;
        }

        private void OnDrawGizmos()
        {
            if (visibility == GizmoVisibility.Always)
            {
                Draw();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (visibility == GizmoVisibility.Selected)
            {
                Draw();
            }
        }

        private void Draw()
        {
            if (!GlobalVisibility ||
                !showGizmos ||
                !TryResolveProvider() ||
                !provider.UsesTrajectory ||
                provider.ReferenceBody == null)
            {
                return;
            }

            var referencePosition =
                provider.ReferenceBody.transform.position;

            if (showReferenceLine)
            {
                Gizmos.color = orbitColor;
                Gizmos.DrawLine(
                    referencePosition,
                    transform.position);
            }

            if (showOrbitPath &&
                provider.TryGetClosedOrbitPeriodSeconds(
                    out var periodSeconds))
            {
                DrawClosedPath(
                    referencePosition,
                    periodSeconds);
            }

            if (showApsides &&
                provider.TryGetConicApsisStates(
                    out var periapsis,
                    out var apoapsis,
                    out var hasApoapsis))
            {
                DrawMarker(
                    referencePosition,
                    periapsis,
                    periapsisColor,
                    "Periapsis");

                if (hasApoapsis)
                {
                    DrawMarker(
                        referencePosition,
                        apoapsis,
                        apoapsisColor,
                        "Apoapsis");
                }
            }
        }

        private void DrawClosedPath(
            Vector3 referencePosition,
            double periodSeconds)
        {
            var startTime =
                provider.Trajectory.EpochUniversalTimeSeconds;
            var segmentCount =
                Mathf.Clamp(
                    pathSegments,
                    16,
                    256);
            var hasPrevious = false;
            var previous = default(Vector3);

            Gizmos.color = orbitColor;

            for (var index = 0;
                index <= segmentCount;
                index++)
            {
                var time =
                    startTime +
                    periodSeconds *
                    index /
                    segmentCount;

                if (!provider.TryEvaluateRelativeState(
                        time,
                        out var relativePosition,
                        out _) ||
                    !TryToScenePosition(
                        referencePosition,
                        relativePosition,
                        out var point))
                {
                    hasPrevious = false;
                    continue;
                }

                if (hasPrevious)
                {
                    Gizmos.DrawLine(
                        previous,
                        point);
                }

                previous = point;
                hasPrevious = true;
            }
        }

        private void DrawMarker(
            Vector3 referencePosition,
            DoubleVector3 relativePosition,
            Color color,
            string label)
        {
            if (!TryToScenePosition(
                    referencePosition,
                    relativePosition,
                    out var position))
            {
                return;
            }

            Gizmos.color = color;
            Gizmos.DrawWireSphere(
                position,
                markerRadius);

#if UNITY_EDITOR
            if (showLabels)
            {
                Handles.color = color;
                Handles.Label(
                    position,
                    label);
            }
#endif
        }

        private bool TryResolveProvider()
        {
            if (provider == null)
            {
                provider =
                    GetComponentInChildren<
                        TrajectoryCelestialBodyMotionProvider>(
                            true);
            }

            return provider != null;
        }

        private static bool TryToScenePosition(
            Vector3 referencePosition,
            DoubleVector3 relativePosition,
            out Vector3 scenePosition)
        {
            scenePosition = default;

            if (!IsFloatRepresentable(
                    relativePosition.x) ||
                !IsFloatRepresentable(
                    relativePosition.y) ||
                !IsFloatRepresentable(
                    relativePosition.z))
            {
                return false;
            }

            scenePosition =
                referencePosition +
                new Vector3(
                    (float)relativePosition.x,
                    (float)relativePosition.y,
                    (float)relativePosition.z);
            return
                IsFinite(scenePosition.x) &&
                IsFinite(scenePosition.y) &&
                IsFinite(scenePosition.z);
        }

        private static bool IsFloatRepresentable(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                Math.Abs(value) <= float.MaxValue;
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
