/*
 * Refreshes one TextMeshPro label from a read-only value provider on unscaled time.
 */

using System;
using TMPro;
using UnityEngine;

namespace jcan.DebugWindows
{
    [DisallowMultipleComponent]
    internal sealed class DebugWindowTextUpdater : MonoBehaviour
    {
        private TMP_Text target;
        private Func<string> valueProvider;
        private float refreshIntervalSeconds;
        private double nextRefreshTime;

        public void Initialize(
            TMP_Text newTarget,
            Func<string> newValueProvider,
            float newRefreshIntervalSeconds)
        {
            target = newTarget;
            valueProvider = newValueProvider;
            refreshIntervalSeconds = Mathf.Max(0.0f, newRefreshIntervalSeconds);
            Refresh();
        }

        private void OnEnable()
        {
            nextRefreshTime = 0.0;
        }

        private void Update()
        {
            if (Time.unscaledTimeAsDouble < nextRefreshTime)
                return;

            Refresh();
        }

        private void Refresh()
        {
            nextRefreshTime = Time.unscaledTimeAsDouble + refreshIntervalSeconds;

            if (target == null || valueProvider == null)
                return;

            try
            {
                target.text = valueProvider() ?? string.Empty;
            }
            catch (Exception exception)
            {
                target.text = "Unavailable";
                Debug.LogException(exception, this);
                enabled = false;
            }
        }
    }
}
