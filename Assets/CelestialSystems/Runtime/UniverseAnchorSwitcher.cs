/*
 * Selects between configured universe anchor sources at runtime without changing the current universe position.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class UniverseAnchorSwitcher : MonoBehaviour
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private UniverseAnchorSource[] sources;

        [ContextMenu("Select Next Anchor")]
        public void SelectNextAnchor()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "Universe anchors can only be switched during Play mode.",
                    this);
                return;
            }

            if (universeFrame == null ||
                sources == null ||
                sources.Length == 0)
            {
                Debug.LogError(
                    "The universe anchor switcher requires a frame controller and at least one source.",
                    this);
                return;
            }

            var currentIndex = -1;

            for (var index = 0; index < sources.Length; index++)
            {
                if (sources[index] == universeFrame.ActiveAnchorSource)
                {
                    currentIndex = index;
                    break;
                }
            }

            var nextIndex = (currentIndex + 1) % sources.Length;
            SelectAnchor(nextIndex);
        }

        public bool SelectAnchor(int index)
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            if (universeFrame == null ||
                sources == null ||
                index < 0 ||
                index >= sources.Length)
            {
                return false;
            }

            var source = sources[index];

            if (source == null)
            {
                Debug.LogError(
                    $"Universe anchor source {index} is not assigned.",
                    this);
                return false;
            }

            return universeFrame.SetActiveAnchorSource(source);
        }
    }
}
