/*
 * Persistent, position-only universe tracking for lightweight scene objects.
 * It deliberately owns no velocity or physics state.
 */

using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    public sealed class UniverseTrackedObject : MonoBehaviour
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private SgtFloatingObject floatingObject;

        [SerializeField]
        private UniversePosition universePosition;

        [SerializeField]
        private bool initialized;

        public UniversePosition Position => universePosition;

        public bool IsInitialized => initialized;

        public bool InitializeFromScenePosition(
            UniverseFrameController frame,
            Vector3 scenePositionMeters)
        {
            UnsubscribeFromFrame();
            universeFrame = frame != null
                ? frame
                : universeFrame;
            SubscribeToFrame();

            if (universeFrame == null ||
                !universeFrame.FrameOriginInitialized)
            {
                initialized = false;
                return false;
            }

            universePosition = universeFrame.FrameOrigin;
            universePosition.AddLocalMeters(
                scenePositionMeters.x,
                scenePositionMeters.y,
                scenePositionMeters.z);
            initialized = ApplyUniversePosition();
            return initialized;
        }

        public bool SetUniversePosition(
            UniversePosition position)
        {
            universePosition = position;
            initialized = ApplyUniversePosition();
            return initialized;
        }

        private void Awake()
        {
            floatingObject ??=
                GetComponent<SgtFloatingObject>();
        }

        private void OnEnable()
        {
            SubscribeToFrame();
        }

        private void OnDisable()
        {
            UnsubscribeFromFrame();
        }

        private void OnDestroy()
        {
            UnsubscribeFromFrame();
        }

        private bool ApplyUniversePosition()
        {
            floatingObject ??=
                GetComponent<SgtFloatingObject>();

            if (floatingObject == null ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    universePosition,
                    0.0,
                    0.0,
                    0.0,
                    out var floatingPosition))
            {
                return false;
            }

            floatingObject.SetPosition(floatingPosition);
            floatingObject.ApplyPosition();
            return true;
        }

        private void SubscribeToFrame()
        {
            if (universeFrame != null)
            {
                universeFrame.OriginShifted -= HandleOriginShifted;
                universeFrame.OriginShifted += HandleOriginShifted;
            }
        }

        private void UnsubscribeFromFrame()
        {
            if (universeFrame != null)
            {
                universeFrame.OriginShifted -= HandleOriginShifted;
            }
        }

        private void HandleOriginShifted(
            UniversePosition newFrameOrigin,
            Vector3 sceneDelta)
        {
            if (initialized)
            {
                ApplyUniversePosition();
            }
        }
    }
}
