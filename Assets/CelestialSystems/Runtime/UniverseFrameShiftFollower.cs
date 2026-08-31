/*
 * Moves an ordinary scene-space root by each project-owned universe origin-shift delta.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class UniverseFrameShiftFollower : MonoBehaviour
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        private void OnEnable()
        {
            if (universeFrame != null)
            {
                universeFrame.OriginShifted += HandleOriginShifted;
            }
        }

        private void Start()
        {
            if (universeFrame == null)
            {
                Debug.LogError(
                    "The universe frame shift follower requires a universe frame controller.",
                    this);
            }
        }

        private void OnDisable()
        {
            if (universeFrame != null)
            {
                universeFrame.OriginShifted -= HandleOriginShifted;
            }
        }

        private void HandleOriginShifted(
            UniversePosition frameOrigin,
            Vector3 sceneDelta)
        {
            if (!Application.isPlaying)
            {
                return;
            }

            transform.position += sceneDelta;
        }
    }
}
