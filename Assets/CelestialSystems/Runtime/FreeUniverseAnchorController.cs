/*
 * Moves a nonphysical universe anchor using camera-relative free-flight controls, with optional scene collision and planet-relative automatic speed.
 */

using System;
using CW.Common;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(400)]
    [DisallowMultipleComponent]
    public sealed class FreeUniverseAnchorController : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField]
        private Transform movementReference;

        [SerializeField]
        private UniverseFrameController universeFrame;

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

        [Header("Controls")]
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

        private void Awake()
        {
            ResolveUniverseFrame();
        }

        private void OnEnable()
        {
            CwInputManager.EnsureThisComponentExists();
        }

        private void Update()
        {
            UpdateSpeedState();

            if (listen)
            {
                AddToDelta(
                    GetDelta(
                        Time.deltaTime));
            }
        }

        private void LateUpdate()
        {
            if (listen)
            {
                DampenDelta();
            }
        }

        private Vector3 GetDelta(float deltaTime)
        {
            return new Vector3(
                horizontalControls.GetValue(
                    deltaTime),
                verticalControls.GetValue(
                    deltaTime),
                depthControls.GetValue(
                    deltaTime));
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

            transform.position +=
                appliedMovement;

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
                resolvedSpeed;
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
