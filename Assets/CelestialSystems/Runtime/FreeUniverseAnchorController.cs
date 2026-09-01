/*
 * Moves a nonphysical universe anchor using camera-relative free-flight controls, with optional scene collision, without giving the camera origin authority.
 */

using CW.Common;
using SpaceGraphicsToolkit;
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

        [Header("Movement")]
        [SerializeField]
        private bool listen = true;

        [SerializeField]
        private float damping = 10.0f;

        [SerializeField]
        private float speedMin = 1.0f;

        [SerializeField]
        private float speedMax = 10.0f;

        [SerializeField]
        private float speedRange = 100.0f;

        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float speedWheel = 0.1f;

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

        [Header("Runtime Collision")]
        [SerializeField]
        private bool hasCollision;

        [SerializeField]
        private string lastCollisionObject;

        [SerializeField]
        private float lastCollisionDistanceMeters;

        [SerializeField]
        private Vector3 lastCollisionNormal;

        private Vector3 remainingDelta;

        private void OnEnable()
        {
            CwInputManager.EnsureThisComponentExists();
        }

        private void Update()
        {
            if (listen)
            {
                AddToDelta(GetDelta(Time.deltaTime));
            }

            if (CwInput.GetMouseExists())
            {
                speedRange *=
                    1.0f -
                    Mathf.Clamp(CwInput.GetMouseWheelDelta(), -1.0f, 1.0f) *
                    speedWheel;
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
                horizontalControls.GetValue(deltaTime),
                verticalControls.GetValue(deltaTime),
                depthControls.GetValue(deltaTime));
        }

        private void AddToDelta(Vector3 localDelta)
        {
            var reference = movementReference;

            if (reference == null)
            {
                var mainCamera = Camera.main;
                reference = mainCamera != null ? mainCamera.transform : transform;
            }

            remainingDelta +=
                reference.TransformDirection(localDelta) *
                GetSpeedMultiplier();
        }

        private void DampenDelta()
        {
            var factor = CwHelper.DampenFactor(damping, Time.deltaTime);
            var newDelta = Vector3.Lerp(remainingDelta, Vector3.zero, factor);
            var requestedMovement = remainingDelta - newDelta;
            var appliedMovement = ResolveCollisionMovement(requestedMovement);

            transform.position += appliedMovement;

            if (hasCollision)
            {
                newDelta = Vector3.ProjectOnPlane(newDelta, lastCollisionNormal);
            }

            remainingDelta = newDelta;
        }

        private Vector3 ResolveCollisionMovement(Vector3 requestedMovement)
        {
            ClearCollisionRuntime();

            if (!collideWithScene ||
                collisionRadiusMeters <= 0.0f ||
                requestedMovement.sqrMagnitude <= Mathf.Epsilon)
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

            RecordCollision(firstHit);

            var remainingMovement = requestedMovement - firstMovement;
            var slideMovement =
                Vector3.ProjectOnPlane(remainingMovement, firstHit.normal);

            if (slideMovement.sqrMagnitude <= Mathf.Epsilon)
            {
                return firstMovement;
            }

            var slideOrigin = transform.position + firstMovement;

            if (TrySweep(
                    slideOrigin,
                    slideMovement,
                    out var allowedSlideMovement,
                    out var slideHit))
            {
                RecordCollision(slideHit);
                slideMovement = allowedSlideMovement;
            }

            return firstMovement + slideMovement;
        }

        private bool TrySweep(
            Vector3 origin,
            Vector3 requestedMovement,
            out Vector3 allowedMovement,
            out RaycastHit hit)
        {
            var requestedDistance = requestedMovement.magnitude;

            if (requestedDistance <= Mathf.Epsilon)
            {
                allowedMovement = Vector3.zero;
                hit = default;
                return false;
            }

            var direction = requestedMovement / requestedDistance;
            var radius = Mathf.Max(0.01f, collisionRadiusMeters);
            var skin = Mathf.Max(0.0f, collisionSkinMeters);

            if (!Physics.SphereCast(
                    origin,
                    radius,
                    direction,
                    out hit,
                    requestedDistance + skin,
                    collisionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                allowedMovement = requestedMovement;
                return false;
            }

            var allowedDistance =
                Mathf.Clamp(hit.distance - skin, 0.0f, requestedDistance);

            allowedMovement = direction * allowedDistance;
            return true;
        }

        private void RecordCollision(RaycastHit hit)
        {
            hasCollision = true;
            lastCollisionObject =
                hit.collider != null ? hit.collider.name : string.Empty;
            lastCollisionDistanceMeters = hit.distance;
            lastCollisionNormal = hit.normal;
        }

        private void ClearCollisionRuntime()
        {
            hasCollision = false;
            lastCollisionObject = string.Empty;
            lastCollisionDistanceMeters = 0.0f;
            lastCollisionNormal = Vector3.zero;
        }

        private float GetSpeedMultiplier()
        {
            if (speedMax <= 0.0f)
            {
                return 0.0f;
            }

            var distance = float.PositiveInfinity;

            SgtCommon.InvokeCalculateDistance(transform.position, ref distance);

            var distance01 = Mathf.InverseLerp(
                speedMin * speedRange,
                speedMax * speedRange,
                distance);

            return Mathf.Lerp(speedMin, speedMax, distance01);
        }
    }
}
