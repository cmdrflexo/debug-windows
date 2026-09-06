/*
 * Runs the optional Milestone 5 sphere drop test with radial gravity and floating-origin rebasing.
 * This development probe does not supply a gameplay character or vehicle movement model.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(310)]
    public sealed class CelestialSurfaceDropTestBody : MonoBehaviour
    {
        private CelestialSurfaceRuntime surface;
        private Rigidbody physicsBody;
        private UniverseFrameController frame;
        private UniverseMotionState previousMotion;
        private Vector3 previousCarrierVelocity;
        private bool hasPreviousMotion;
        private bool hasPreviousCarrierVelocity;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        public bool TouchingTerrain
        {
            get
            {
                contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
                return contacts.Count > 0;
            }
        }

        public void Initialize(CelestialSurfaceRuntime value)
        {
            surface = value;
            physicsBody = GetComponent<Rigidbody>();
            hasPreviousMotion = surface.Body.TryGetMotionState(out previousMotion);
            previousCarrierVelocity = physicsBody.linearVelocity;
            hasPreviousCarrierVelocity = hasPreviousMotion;
            frame = surface.Body.UniverseFrame;
            if (frame != null) frame.OriginShifted += ShiftOrigin;
            surface.Body.Destroying += BodyDestroying;
            Destroy(gameObject, 180.0f);
        }

        private void FixedUpdate()
        {
            if (surface == null || !surface.isActiveAndEnabled)
            {
                Destroy(gameObject);
                return;
            }
            CompensateForBodyFrameMotion();
            if (surface.TryGetScenePose(out var center, out _))
            {
                var towardsCenter = center - CelestialSurfaceGeometry.ToDouble(physicsBody.position);
                if (towardsCenter.Magnitude > 0.0)
                    physicsBody.AddForce(CelestialSurfaceGeometry.ToVector3(towardsCenter / towardsCenter.Magnitude) * 9.81f,
                        ForceMode.Acceleration);
            }
        }

        private void CompensateForBodyFrameMotion()
        {
            if (!surface.Body.TryGetMotionState(out var currentMotion)) return;
            if (hasPreviousMotion && Time.fixedDeltaTime > 0.0f &&
                surface.TrySceneToBodyLocal(physicsBody.position, out var local))
            {
                var previousPoint = CelestialSurfaceGeometry.Add(previousMotion.Position,
                    CelestialSurfaceGeometry.Rotate(local, previousMotion.Rotation));
                var currentPoint = CelestialSurfaceGeometry.Add(currentMotion.Position,
                    CelestialSurfaceGeometry.Rotate(local, currentMotion.Rotation));
                var carrierVelocity = CelestialSurfaceGeometry.ToVector3(
                    CelestialSurfaceGeometry.Difference(currentPoint, previousPoint) / Time.fixedDeltaTime);
                if (hasPreviousCarrierVelocity)
                {
                    var frameRotation = currentMotion.Rotation * Quaternion.Inverse(previousMotion.Rotation);
                    var relativeVelocity = physicsBody.linearVelocity - previousCarrierVelocity;
                    physicsBody.linearVelocity = carrierVelocity + frameRotation * relativeVelocity;
                }
                previousCarrierVelocity = carrierVelocity;
                hasPreviousCarrierVelocity = true;
            }
            previousMotion = currentMotion;
            hasPreviousMotion = true;
        }

        private void ShiftOrigin(UniversePosition origin, Vector3 delta) { physicsBody.position += delta; }
        private void BodyDestroying(CelestialBodyRuntimeContext _) { Destroy(gameObject); }
        private void OnCollisionEnter(Collision collision) { if (IsTerrain(collision)) contacts.Add(collision.collider); }
        private void OnCollisionExit(Collision collision) { contacts.Remove(collision.collider); }
        private bool IsTerrain(Collision collision) => surface != null && surface.CollisionRuntime != null &&
            surface.CollisionRuntime.OwnsCollider(collision.collider);
        private void OnDestroy()
        {
            if (frame != null) frame.OriginShifted -= ShiftOrigin;
            if (surface != null && surface.Body != null) surface.Body.Destroying -= BodyDestroying;
        }
    }
}
