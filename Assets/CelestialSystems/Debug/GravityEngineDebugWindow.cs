/*
 * Presents live Gravity Engine state and runtime simulation-rate controls through the generic tabbed debug-window framework.
 */

using System;
using jcan.DebugWindows;
using UnityEngine;
using UnityEngine.Serialization;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class GravityEngineDebugWindow :
        MonoBehaviour
    {
        private const string WindowId =
            "jcan.celestialsystems.gravity-engine";

        [SerializeField]
        private Vector2 preferredContentSize =
            new Vector2(
                420.0f,
                300.0f);

        [SerializeField]
        private Vector2 defaultPosition =
            new Vector2(
                540.0f,
                -12.0f);

        [SerializeField]
        [FormerlySerializedAs(
            "maximumTimeStepMultiplier")]
        [Min(1.0f)]
        private double maximumTimeZoom =
            1000000.0;

        private GravityEngine gravityEngine;
        private DebugTabbedWindow window;
        private bool applicationIsQuitting;
        private string lastAction =
            "Ready.";

        private void OnEnable()
        {
            applicationIsQuitting =
                false;
            ResolveGravityEngine();

            window =
                new DebugTabbedWindow(
                    WindowId,
                    "Gravity Engine",
                    new[]
                    {
                        new DebugTabbedPage(
                            "simulation",
                            "Simulation",
                            BuildSimulationPage)
                    },
                    BuildFooter,
                    preferredContentSize,
                    DebugWindowDisplayState.Closed,
                    defaultPosition);

            if (!DebugWindowRegistry.Register(
                    window.CreateRegistration()))
            {
                window =
                    null;
            }
        }

        private void Update()
        {
            ResolveGravityEngine();
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting =
                true;
        }

        private void OnDisable()
        {
            if (!applicationIsQuitting)
            {
                DebugWindowRegistry.Unregister(
                    WindowId);
            }

            window =
                null;
        }

        private void BuildSimulationPage(
            DebugWindowFormContent content)
        {
            content.AddReadOnly(
                "Engine Ready",
                () => IsEngineReady()
                    ? "Yes"
                    : "No");
            content.AddReadOnly(
                "Evolution",
                () => IsEngineReady()
                    ? gravityEngine.GetEvolve()
                        ? "Running"
                        : "Paused"
                    : "--");
            content.AddReadOnly(
                "Simulation Time",
                GetSimulationTimeText);
            content.AddReadOnly(
                "Active Bodies",
                () => CelestialBodyRuntimeContext
                    .ActiveContexts.Count.ToString());
            content.AddReadOnly(
                "Algorithm",
                () => IsEngineReady()
                    ? GravityEngine.GetAlgorithmName(
                        gravityEngine.algorithm)
                    : "--");
            content.AddReadOnly(
                "Update Mode",
                () => gravityEngine != null
                    ? gravityEngine.updateMode.ToString()
                    : "--");
            content.AddReadOnly(
                "Units",
                () => gravityEngine != null
                    ? gravityEngine.units.ToString()
                    : "--");
            content.AddReadOnly(
                "Steps / Frame",
                () => gravityEngine != null
                    ? gravityEngine.stepsPerFrame.ToString()
                    : "--");
            content.AddReadOnly(
                "Engine dt",
                () => gravityEngine != null
                    ? gravityEngine.engineDt.ToString(
                        "0.###############")
                    : "--");
            content.AddReadOnly(
                "World Step / Tick",
                GetWorldStepText);
            content.AddReadOnly(
                "Current Rate",
                () => GetCurrentTimeZoom()
                    .ToString("0.####") + "×");
            content.AddNumberField(
                "Time Zoom",
                GetCurrentTimeZoom(),
                SetTimeZoom,
                "×");
            content.AddReadOnly(
                "Last Action",
                () => lastAction);
            content.AddReadOnly(
                "Accuracy",
                () => GetCurrentTimeZoom() > 1.0
                    ? "High zoom increases GE work per frame."
                    : "Normal.");
        }

        private void BuildFooter(
            DebugWindowFormContent content)
        {
            content.AddButton(
                "pause-resume",
                "Pause / Resume",
                ToggleEvolution);
            content.AddButton(
                "normal-rate",
                "1×",
                () => SetTimeZoom(
                    1.0));
        }

        private void ResolveGravityEngine()
        {
            if (gravityEngine == null)
            {
                gravityEngine =
                    GravityEngine.Instance();
            }

        }

        private bool IsEngineReady()
        {
            return
                gravityEngine != null &&
                gravityEngine.IsSetup();
        }

        private void SetTimeZoom(
            double requestedTimeZoom)
        {
            ResolveGravityEngine();

            if (!IsEngineReady())
            {
                lastAction =
                    "Gravity Engine is not ready.";
                return;
            }

            if (!IsFinitePositive(
                    requestedTimeZoom))
            {
                lastAction =
                    "Time zoom must be greater than zero.";
                return;
            }

            var timeZoom =
                Math.Min(
                    requestedTimeZoom,
                    Math.Max(
                        1.0,
                        maximumTimeZoom));
            gravityEngine.SetTimeZoom(
                (float)timeZoom);
            lastAction =
                $"Set GE time zoom to {timeZoom:0.####}×.";
        }

        private void ToggleEvolution()
        {
            ResolveGravityEngine();

            if (!IsEngineReady())
            {
                lastAction =
                    "Gravity Engine is not ready.";
                return;
            }

            var evolve =
                !gravityEngine.GetEvolve();
            gravityEngine.SetEvolve(
                evolve);
            lastAction =
                evolve
                    ? "Resumed GE evolution."
                    : "Paused GE evolution.";
        }

        private double GetCurrentTimeZoom()
        {
            ResolveGravityEngine();

            if (!IsEngineReady())
            {
                return 1.0;
            }

            var timeZoom =
                gravityEngine.GetTimeZoom();
            return IsFinitePositive(
                    timeZoom)
                ? timeZoom
                : 1.0;
        }

        private string GetSimulationTimeText()
        {
            if (!IsEngineReady())
            {
                return "--";
            }

            var worldState =
                gravityEngine.GetWorldState();
            if (worldState == null)
            {
                return "--";
            }

            var seconds =
                GravityScaler.GetWorldTimeSeconds(
                    worldState.GetPhysicsTime());
            return FormatDuration(
                seconds);
        }

        private string GetWorldStepText()
        {
            if (gravityEngine == null ||
                !IsFinitePositive(
                    gravityEngine.engineDt))
            {
                return "--";
            }

            var seconds =
                GravityScaler.GetWorldTimeSeconds(
                    gravityEngine.engineDt *
                    Math.Max(
                        1,
                        gravityEngine.stepsPerFrame));
            return
                seconds.ToString(
                    "0.###############") +
                " s";
        }

        private static string FormatDuration(
            double seconds)
        {
            if (double.IsNaN(seconds) ||
                double.IsInfinity(seconds))
            {
                return "--";
            }

            var sign =
                seconds < 0.0
                    ? "-"
                    : string.Empty;
            var absolute =
                Math.Abs(
                    seconds);
            var days =
                Math.Floor(
                    absolute /
                    86400.0);
            var remainder =
                absolute -
                days *
                86400.0;
            var hours =
                Math.Floor(
                    remainder /
                    3600.0);
            remainder -=
                hours *
                3600.0;
            var minutes =
                Math.Floor(
                    remainder /
                    60.0);
            var remainingSeconds =
                remainder -
                minutes *
                60.0;

            return
                $"{sign}{days:0}d {hours:00}:{minutes:00}:{remainingSeconds:00.000}";
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }
}
