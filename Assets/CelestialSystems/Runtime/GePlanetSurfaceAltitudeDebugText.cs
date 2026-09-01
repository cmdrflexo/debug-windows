/*
 * Displays a planet surface frame's current anchor altitude in a TextMeshPro text component for runtime debugging.
 */

using TMPro;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class GePlanetSurfaceAltitudeDebugText :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private TMP_Text targetText;

        [SerializeField]
        private string label =
            "Altitude";

        [SerializeField]
        [Range(0, 3)]
        private int decimalPlaces = 1;

        [SerializeField]
        [Min(0.0f)]
        private float refreshIntervalSeconds = 0.1f;

        [SerializeField]
        private string unavailableText =
            "Altitude: --";

        [Header("Runtime")]
        [SerializeField]
        private bool hasAltitude;

        [SerializeField]
        private double altitudeMeters;

        private double nextRefreshTime;

        private void Reset()
        {
            targetText =
                GetComponent<TMP_Text>();
        }

        private void Start()
        {
            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "The surface altitude debug text requires a planet surface frame.",
                    this);
            }

            if (targetText == null)
            {
                Debug.LogError(
                    "The surface altitude debug text requires a TextMeshPro text target.",
                    this);
            }

            RefreshText();
        }

        private void Update()
        {
            if (Time.unscaledTimeAsDouble <
                nextRefreshTime)
            {
                return;
            }

            nextRefreshTime =
                Time.unscaledTimeAsDouble +
                refreshIntervalSeconds;
            RefreshText();
        }

        private void RefreshText()
        {
            hasAltitude =
                surfaceFrame != null &&
                surfaceFrame.HasAnchorAddress;

            if (!hasAltitude)
            {
                altitudeMeters = default;

                if (targetText != null)
                {
                    targetText.text =
                        unavailableText;
                }

                return;
            }

            altitudeMeters =
                surfaceFrame.AnchorAltitudeMeters;

            if (targetText == null)
            {
                return;
            }

            var format =
                "N" +
                decimalPlaces;

            targetText.text =
                $"{label}: {altitudeMeters.ToString(format)} m";
        }
    }
}
