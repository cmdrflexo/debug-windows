/*
 * Owns the active universe frame, applies origin shifts to Gravity Engine, and notifies scene-space listeners.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class UniverseFrameController : MonoBehaviour
    {
        [SerializeField]
        private bool logOriginShifts;

        [SerializeField]
        private UniversePosition frameOrigin;

        private GravityEngine gravityEngine;
        private bool frameOriginInitialized;

        public UniversePosition FrameOrigin => frameOrigin;

        public bool FrameOriginInitialized => frameOriginInitialized;

        public event Action<UniversePosition, Vector3> OriginShifted;

        private void Awake()
        {
            gravityEngine = GravityEngine.Instance();
        }

        public void InitializeFrameOrigin(UniversePosition initialFrameOrigin)
        {
            if (frameOriginInitialized)
            {
                return;
            }

            frameOrigin = initialFrameOrigin;
            frameOriginInitialized = true;
        }

        public bool ShiftOrigin(Vector3d originAdvanceMeters, Vector3 sceneDelta)
        {
            gravityEngine ??= GravityEngine.Instance();

            if (gravityEngine == null)
            {
                Debug.LogError(
                    "Cannot shift the universe frame because no Gravity Engine exists in the scene.",
                    this);
                return false;
            }

            var physicalScale = gravityEngine.GetPhysicalScale();

            if (Mathf.Approximately(physicalScale, 0.0f))
            {
                Debug.LogError(
                    "Cannot shift the universe frame because Gravity Engine's physical scale is zero.",
                    this);
                return false;
            }

            var physicsDelta = new Vector3d(
                -originAdvanceMeters.x / physicalScale,
                -originAdvanceMeters.y / physicalScale,
                -originAdvanceMeters.z / physicalScale);

            gravityEngine.MoveAll(physicsDelta);

            frameOrigin.AddLocalMeters(
                originAdvanceMeters.x,
                originAdvanceMeters.y,
                originAdvanceMeters.z);
            frameOriginInitialized = true;

            OriginShifted?.Invoke(frameOrigin, sceneDelta);

            if (logOriginShifts)
            {
                Debug.Log(
                    $"Origin shifted to {frameOrigin}. Scene delta: {sceneDelta}.",
                    this);
            }

            return true;
        }
    }
}
