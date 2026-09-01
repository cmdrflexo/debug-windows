/*
 * Positions and orients a round body's local tangent frame and tracks the active anchor's canonical cube-sphere address.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class GePlanetSurfaceFrame : MonoBehaviour
    {
        private const float MinimumAxisSquared = 0.000001f;

        [Header("Configuration")]
        [SerializeField]
        private CelestialBodyRuntimeContext bodyContext;

        [Header("Resolved Body Configuration")]
        [SerializeField]
        private double planetRadiusMeters;

        [SerializeField]
        private Vector3 planetNorthAxis =
            Vector3.up;

        [SerializeField]
        private Vector3 poleReferenceAxis =
            Vector3.forward;

        [Header("Runtime")]
        [SerializeField]
        private double anchorAltitudeMeters;

        [SerializeField]
        private bool hasAnchorAddress;

        [SerializeField]
        private CubeSphereAddress anchorAddress;

        [SerializeField]
        private CubeSphereFaceProximity anchorFaceProximity;

        [Header("Gizmos")]
        [SerializeField]
        private GizmoDrawMode gizmoDrawMode;

        [SerializeField]
        private float gizmoSize = 100000.0f;

        private GravityEngine gravityEngine;
        private Vector3 previousTangentForward;
        private bool previousTangentForwardSet;

        public CelestialBodyRuntimeContext BodyContext =>
            bodyContext;

        public double PlanetRadiusMeters =>
            planetRadiusMeters;

        public double AnchorAltitudeMeters =>
            anchorAltitudeMeters;

        public bool HasAnchorAddress =>
            hasAnchorAddress;

        public CubeSphereAddress AnchorAddress =>
            anchorAddress;

        public CubeSphereFaceProximity AnchorFaceProximity =>
            anchorFaceProximity;

        private void Start()
        {
            gravityEngine =
                GravityEngine.Instance();

            if (bodyContext == null)
            {
                Debug.LogError(
                    "The round-body surface frame requires a celestial body runtime context.",
                    this);
                return;
            }

            if (bodyContext.Definition == null)
            {
                Debug.LogError(
                    "The round-body surface frame requires a body context with a definition.",
                    this);
                return;
            }

            if (bodyContext.ResolvedSurfaceSystem !=
                CelestialSurfaceSystem.RoundMapMagic)
            {
                Debug.LogError(
                    "The round-body surface frame requires a body definition resolved to Round MapMagic.",
                    this);
            }

            ResolveBodyConfiguration(
                out _,
                out _);
        }

        private void LateUpdate()
        {
            hasAnchorAddress = false;
            anchorFaceProximity = default;

            if (!ResolveBodyConfiguration(
                    out var universeFrame,
                    out var gravityBody) ||
                !universeFrame.TryGetActiveAnchorOffsetMeters(
                    out var anchorOffsetMeters))
            {
                return;
            }

            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup())
            {
                return;
            }

            var physicalScale =
                gravityEngine.GetPhysicalScale();

            if (Mathf.Approximately(
                    physicalScale,
                    0.0f))
            {
                return;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(
                    gravityBody);
            var planetCenterX =
                physicsPosition.x *
                physicalScale;
            var planetCenterY =
                physicsPosition.y *
                physicalScale;
            var planetCenterZ =
                physicsPosition.z *
                physicalScale;
            var radialX =
                anchorOffsetMeters.x -
                planetCenterX;
            var radialY =
                anchorOffsetMeters.y -
                planetCenterY;
            var radialZ =
                anchorOffsetMeters.z -
                planetCenterZ;
            var radialDistanceSquared =
                radialX *
                radialX +
                radialY *
                radialY +
                radialZ *
                radialZ;

            if (radialDistanceSquared <=
                double.Epsilon)
            {
                return;
            }

            var radialDistance =
                Math.Sqrt(
                    radialDistanceSquared);
            var upX =
                radialX /
                radialDistance;
            var upY =
                radialY /
                radialDistance;
            var upZ =
                radialZ /
                radialDistance;
            var surfacePosition =
                new Vector3(
                    (float)(
                        planetCenterX +
                        upX *
                        planetRadiusMeters),
                    (float)(
                        planetCenterY +
                        upY *
                        planetRadiusMeters),
                    (float)(
                        planetCenterZ +
                        upZ *
                        planetRadiusMeters));
            var surfaceUp =
                new Vector3(
                    (float)upX,
                    (float)upY,
                    (float)upZ).normalized;
            var tangentForward =
                ResolveTangentForward(
                    surfaceUp);

            anchorAltitudeMeters =
                radialDistance -
                planetRadiusMeters;
            hasAnchorAddress =
                CubeSphereMapping.TryDirectionToAddress(
                    new DoubleVector3(
                        radialX,
                        radialY,
                        radialZ),
                    anchorAltitudeMeters,
                    out anchorAddress);

            if (hasAnchorAddress)
            {
                anchorFaceProximity =
                    CubeSphereMapping.GetFaceProximity(
                        anchorAddress,
                        planetRadiusMeters);
            }

            transform.SetPositionAndRotation(
                surfacePosition,
                Quaternion.LookRotation(
                    tangentForward,
                    surfaceUp));

            previousTangentForward =
                tangentForward;
            previousTangentForwardSet =
                true;
        }

        public bool TryGetPlanetCenterScenePosition(
            out Vector3 planetCenter)
        {
            if (!hasAnchorAddress ||
                !IsFinite(
                    planetRadiusMeters) ||
                planetRadiusMeters <= 0.0 ||
                planetRadiusMeters >
                    float.MaxValue)
            {
                planetCenter = default;
                return false;
            }

            planetCenter =
                transform.position -
                transform.up *
                    (float)planetRadiusMeters;
            return true;
        }

        private bool ResolveBodyConfiguration(
            out UniverseFrameController universeFrame,
            out NBody gravityBody)
        {
            universeFrame = null;
            gravityBody = null;

            if (bodyContext == null ||
                bodyContext.Definition == null ||
                bodyContext.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic)
            {
                return false;
            }

            var definition =
                bodyContext.Definition;

            planetRadiusMeters =
                definition.ReferenceRadiusMeters;
            planetNorthAxis =
                definition.NorthAxis;
            poleReferenceAxis =
                definition.PoleReferenceAxis;
            universeFrame =
                bodyContext.UniverseFrame;
            gravityBody =
                bodyContext.GravityBody;

            return
                definition.HasValidPhysicalSettings &&
                IsFinite(
                    planetRadiusMeters) &&
                planetRadiusMeters > 0.0 &&
                universeFrame != null &&
                gravityBody != null;
        }

        private Vector3 ResolveTangentForward(
            Vector3 surfaceUp)
        {
            var tangentForward =
                ProjectOntoTangent(
                    planetNorthAxis,
                    surfaceUp);

            if (tangentForward.sqrMagnitude <
                MinimumAxisSquared &&
                previousTangentForwardSet)
            {
                tangentForward =
                    ProjectOntoTangent(
                        previousTangentForward,
                        surfaceUp);
            }

            if (tangentForward.sqrMagnitude <
                MinimumAxisSquared)
            {
                tangentForward =
                    ProjectOntoTangent(
                        poleReferenceAxis,
                        surfaceUp);
            }

            if (tangentForward.sqrMagnitude <
                MinimumAxisSquared)
            {
                var fallbackAxis =
                    Mathf.Abs(
                        Vector3.Dot(
                            surfaceUp,
                            Vector3.forward)) <
                        0.99f
                        ? Vector3.forward
                        : Vector3.right;

                tangentForward =
                    ProjectOntoTangent(
                        fallbackAxis,
                        surfaceUp);
            }

            return tangentForward.normalized;
        }

        private static Vector3 ProjectOntoTangent(
            Vector3 direction,
            Vector3 surfaceUp)
        {
            return Vector3.ProjectOnPlane(
                direction,
                surfaceUp);
        }

        private void OnDrawGizmos()
        {
            if (gizmoDrawMode ==
                GizmoDrawMode.Always)
            {
                DrawGizmos();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (gizmoDrawMode ==
                GizmoDrawMode.Selected)
            {
                DrawGizmos();
            }
        }

        private void DrawGizmos()
        {
            if (gizmoSize <= 0.0f)
            {
                return;
            }

            Gizmos.color =
                Color.red;
            Gizmos.DrawRay(
                transform.position,
                transform.right *
                    gizmoSize);

            Gizmos.color =
                Color.green;
            Gizmos.DrawRay(
                transform.position,
                transform.up *
                    gizmoSize);

            Gizmos.color =
                Color.blue;
            Gizmos.DrawRay(
                transform.position,
                transform.forward *
                    gizmoSize);
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
