/*
 * Registers the existing Celestial debug recipe output as a portable runtime debug window.
 */

using jcan.DebugWindows;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialDebugWindow : MonoBehaviour
    {
        [SerializeField]
        private CelestialDebugTextController source;

        [SerializeField]
        private string uniqueId = "jcan.celestialsystems.debug-text";

        [SerializeField]
        private string windowTitle = "Celestial Debug";

        [SerializeField]
        private DebugWindowDisplayState defaultState = DebugWindowDisplayState.Open;

        [SerializeField]
        private Vector2 defaultPosition = new Vector2(210.0f, -12.0f);

        [SerializeField]
        [Min(0.0f)]
        private float refreshIntervalSeconds = 0.1f;

        private string registeredId;
        private bool applicationIsQuitting;

        private void OnEnable()
        {
            applicationIsQuitting = false;

            if (source == null)
            {
                Debug.LogWarning("CelestialDebugWindow requires a CelestialDebugTextController.", this);
                return;
            }

            var candidateId = string.IsNullOrWhiteSpace(uniqueId)
                ? "jcan.celestialsystems.debug-text"
                : uniqueId.Trim();

            if (DebugWindowRegistry.Register(new DebugWindowRegistration(
                    candidateId,
                    string.IsNullOrWhiteSpace(windowTitle) ? "Celestial Debug" : windowTitle,
                    BuildContent,
                    defaultState,
                    defaultPosition)))
            {
                registeredId = candidateId;
            }
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDisable()
        {
            if (applicationIsQuitting)
            {
                return;
            }

            if (!string.IsNullOrEmpty(registeredId))
            {
                DebugWindowRegistry.Unregister(registeredId);
                registeredId = null;
            }
        }

        private void BuildContent(DebugWindowContent content)
        {
            content.AddUpdatingText(
                GetRenderedText,
                refreshIntervalSeconds);
        }

        private string GetRenderedText()
        {
            return source != null
                ? source.RenderedText
                : "Debug text source unavailable.";
        }
    }
}
