/*
 * Owns double-precision universal time for trajectories, save loading, and direct timestamp jumps.
 */

using System;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("Input")]
        [SerializeField]
        [Tooltip("Axis action where positive input increases time scale and negative input decreases it.")]
        private InputActionReference timeScaleStepAction;

        [SerializeField]
        [Min(1.0001f)]
        private double timeScaleStepMultiplier = 10.0;

        [SerializeField]
        [Min(0.000001f)]
        private double minimumTimeScaleMagnitude = 0.001;

        [SerializeField]
        [Min(0.000001f)]
        private double maximumTimeScaleMagnitude = 1000000000000.0;

        [Header("Runtime")]
        [SerializeField]
        private double universalTimeSeconds;

        [SerializeField]
        private bool isRunning;

        private bool enabledTimeScaleStepAction;

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

        private void OnEnable()
        {
            var action =
                timeScaleStepAction != null
                    ? timeScaleStepAction.action
                    : null;

            if (action == null)
            {
                return;
            }

            enabledTimeScaleStepAction =
                !action.enabled;

            if (enabledTimeScaleStepAction)
            {
                action.Enable();
            }

            action.performed +=
                OnTimeScaleStepPerformed;
        }

        private void OnDisable()
        {
            var action =
                timeScaleStepAction != null
                    ? timeScaleStepAction.action
                    : null;

            if (action != null)
            {
                action.performed -=
                    OnTimeScaleStepPerformed;

                if (enabledTimeScaleStepAction)
                {
                    action.Disable();
                }
            }

            enabledTimeScaleStepAction = false;
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

        public bool IncreaseTimeScale(
            int stepCount = 1)
        {
            return StepTimeScale(
                Math.Max(
                    1,
                    stepCount));
        }

        public bool DecreaseTimeScale(
            int stepCount = 1)
        {
            return StepTimeScale(
                -Math.Max(
                    1,
                    stepCount));
        }

        private bool StepTimeScale(
            int signedStepCount)
        {
            if (signedStepCount == 0)
            {
                return true;
            }

            var stepFactor =
                Math.Pow(
                    timeScaleStepMultiplier,
                    Math.Abs(
                        signedStepCount));

            if (!IsFinite(
                    stepFactor) ||
                stepFactor <= 0.0)
            {
                return false;
            }

            var sign =
                timeScale < 0.0
                    ? -1.0
                    : 1.0;
            var magnitude =
                Math.Abs(
                    timeScale);

            if (magnitude == 0.0)
            {
                magnitude =
                    minimumTimeScaleMagnitude;
            }
            else if (signedStepCount > 0)
            {
                magnitude *=
                    stepFactor;
            }
            else
            {
                magnitude /=
                    stepFactor;
            }

            magnitude =
                Math.Max(
                    minimumTimeScaleMagnitude,
                    Math.Min(
                        maximumTimeScaleMagnitude,
                        magnitude));

            return SetTimeScale(
                sign *
                    magnitude);
        }

        private void OnTimeScaleStepPerformed(
            InputAction.CallbackContext context)
        {
            var input =
                context.ReadValue<float>();

            if (Mathf.Approximately(
                    input,
                    0.0f))
            {
                return;
            }

            var stepCount =
                Math.Max(
                    1,
                    Mathf.RoundToInt(
                        Mathf.Abs(
                            input)));

            StepTimeScale(
                input > 0.0f
                    ? stepCount
                    : -stepCount);
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

            if (!IsFinite(
                    timeScaleStepMultiplier) ||
                timeScaleStepMultiplier <= 1.0)
            {
                timeScaleStepMultiplier =
                    10.0;
            }

            if (!IsFinite(
                    minimumTimeScaleMagnitude) ||
                minimumTimeScaleMagnitude <= 0.0)
            {
                minimumTimeScaleMagnitude =
                    0.001;
            }

            if (!IsFinite(
                    maximumTimeScaleMagnitude) ||
                maximumTimeScaleMagnitude <
                    minimumTimeScaleMagnitude)
            {
                maximumTimeScaleMagnitude =
                    minimumTimeScaleMagnitude;
            }

            if (timeScale != 0.0)
            {
                timeScale =
                    Math.Sign(
                        timeScale) *
                    Math.Max(
                        minimumTimeScaleMagnitude,
                        Math.Min(
                            maximumTimeScaleMagnitude,
                            Math.Abs(
                                timeScale)));
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
