/*
 * Provides the common project-owned contract used by sources that can move the active universe frame.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public abstract class UniverseAnchorSource : MonoBehaviour
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private Behaviour[] activeBehaviours;

        public UniverseFrameController UniverseFrame => universeFrame;

        public bool IsActiveSource =>
            universeFrame != null &&
            universeFrame.ActiveAnchorSource == this;

        internal void SetSourceActive(bool active)
        {
            if (activeBehaviours != null)
            {
                foreach (var activeBehaviour in activeBehaviours)
                {
                    if (activeBehaviour != null)
                    {
                        activeBehaviour.enabled = active;
                    }
                }
            }

            OnSourceActivationChanged(active);
        }

        protected virtual void OnSourceActivationChanged(bool active)
        {
        }

        protected bool TryInitializeFrameOrigin(
            UniversePosition initialFrameOrigin)
        {
            return universeFrame != null &&
                universeFrame.InitializeFrameOrigin(
                    this,
                    initialFrameOrigin);
        }

        protected bool TryShiftOrigin(
            Vector3d originAdvanceMeters,
            Vector3 sceneDelta)
        {
            return universeFrame != null &&
                universeFrame.ShiftOrigin(
                    this,
                    originAdvanceMeters,
                    sceneDelta);
        }
    }
}
