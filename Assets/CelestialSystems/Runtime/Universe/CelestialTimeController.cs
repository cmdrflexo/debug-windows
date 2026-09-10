/*
 * Owns double-precision universal time for trajectories, save loading, and direct timestamp jumps.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class CelestialTimeController :
        MonoBehaviour,
        ICelestialTimeSource
    {
        [SerializeField]
        private double initialUniversalTimeSeconds;

        [SerializeField]
        private double timeScale = 1.0;

        [SerializeField]
        private bool runOnStart = true;

        [Header("Runtime")]
        [SerializeField]
        private double universalTimeSeconds;

        [SerializeField]
        private bool isRunning;

        public static CelestialTimeController Instance { get; private set; }

        public double UniversalTimeSeconds =>
            universalTimeSeconds;

        public double TimeScale =>
            timeScale;

        public bool IsRunning =>
            isRunning;

        public event Action<double, double> UniversalTimeChanged;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance()
        {
            Instance = null;
        }

        private void Awake()
        {
            if (Instance != null &&
                Instance != this)
            {
                Debug.LogError(
                    "Only one Celestial Time Controller can be active in a scene.",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
            universalTimeSeconds =
                IsFinite(
                    initialUniversalTimeSeconds)
                    ? initialUniversalTimeSeconds
                    : 0.0;
            timeScale =
                IsFinite(
                    timeScale)
                    ? timeScale
                    : 1.0;
            isRunning =
                runOnStart;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!isRunning)
            {
                return;
            }

            AdvanceSeconds(
                Time.unscaledDeltaTime *
                timeScale);
        }

        public bool SetUniversalTimeSeconds(
            double newUniversalTimeSeconds)
        {
            if (!IsFinite(
                    newUniversalTimeSeconds))
            {
                return false;
            }

            var previousTime =
                universalTimeSeconds;
            universalTimeSeconds =
                newUniversalTimeSeconds;
            UniversalTimeChanged?.Invoke(
                previousTime,
                universalTimeSeconds);
            return true;
        }

        public bool AdvanceSeconds(
            double elapsedUniversalSeconds)
        {
            if (!IsFinite(
                    elapsedUniversalSeconds))
            {
                return false;
            }

            return SetUniversalTimeSeconds(
                universalTimeSeconds +
                elapsedUniversalSeconds);
        }

        public bool SetTimeScale(
            double newTimeScale)
        {
            if (!IsFinite(
                    newTimeScale))
            {
                return false;
            }

            timeScale =
                newTimeScale;
            return true;
        }

        public void SetRunning(
            bool shouldRun)
        {
            isRunning =
                shouldRun;
        }

        public void ResetToInitialTime()
        {
            SetUniversalTimeSeconds(
                initialUniversalTimeSeconds);
        }

        private void OnValidate()
        {
            if (!IsFinite(
                    initialUniversalTimeSeconds))
            {
                initialUniversalTimeSeconds =
                    0.0;
            }

            if (!IsFinite(
                    timeScale))
            {
                timeScale =
                    1.0;
            }
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
