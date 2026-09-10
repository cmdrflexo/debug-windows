/*
 * Owns the active universe frame and notifies scene-space listeners when its origin shifts.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class UniverseFrameController : MonoBehaviour
    {
        [SerializeField]
        private UniverseAnchorSource activeAnchorSource;

        [SerializeField]
        private bool logOriginShifts;

        [SerializeField]
        private UniversePosition frameOrigin;

        private bool frameOriginInitialized;

        public UniverseAnchorSource ActiveAnchorSource =>
            activeAnchorSource;

        public UniversePosition FrameOrigin => frameOrigin;

        public bool FrameOriginInitialized => frameOriginInitialized;

        public event Action<UniversePosition, Vector3> OriginShifted;

        public event Action<
            UniverseAnchorSource,
            UniverseAnchorSource> ActiveAnchorSourceChanged;

         private void Start()
        {
            activeAnchorSource?.SetSourceActive(true);
        }

        public bool SetActiveAnchorSource(UniverseAnchorSource source)
        {
            if (source == null)
            {
                Debug.LogError(
                    "Cannot activate a null universe anchor source.",
                    this);
                return false;
            }

            if (source.UniverseFrame != this)
            {
                Debug.LogError(
                    $"Cannot activate {source.name} because it belongs to a different universe frame.",
                    source);
                return false;
            }

            if (source == activeAnchorSource)
            {
                return true;
            }

            var previousSource = activeAnchorSource;

            activeAnchorSource = source;
            previousSource?.SetSourceActive(false);
            activeAnchorSource.SetSourceActive(true);

            ActiveAnchorSourceChanged?.Invoke(
                previousSource,
                activeAnchorSource);
            return true;
        }

        public bool TryGetActiveAnchorOffsetMeters(
            out Vector3d offsetMeters)
        {
            if (activeAnchorSource == null)
            {
                offsetMeters = default;
                return false;
            }

            return activeAnchorSource.TryGetFrameOffsetMeters(
                out offsetMeters);
        }

        public bool InitializeFrameOrigin(
            UniverseAnchorSource source,
            UniversePosition initialFrameOrigin)
        {
            if (source == null || source != activeAnchorSource)
            {
                return false;
            }

            if (!frameOriginInitialized)
            {
                frameOrigin = initialFrameOrigin;
                frameOriginInitialized = true;
            }

            return true;
        }

        public bool ShiftOrigin(
            UniverseAnchorSource source,
            Vector3d originAdvanceMeters,
            Vector3 sceneDelta)
        {
            if (source == null || source != activeAnchorSource)
            {
                return false;
            }

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
