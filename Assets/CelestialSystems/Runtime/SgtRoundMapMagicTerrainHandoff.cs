/*
 * Switches one body's SGT terrain-height modifiers between distant and near-surface settings when local or mid-detail custom terrain is ready.
 */

using System;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(350)]
    [DisallowMultipleComponent]
    public sealed class SgtRoundMapMagicTerrainHandoff :
        MonoBehaviour
    {
        [Serializable]
        private sealed class SimplexLayerSettings
        {
            [SerializeField]
            private SgtTerrainSimplex layer;

            [SerializeField]
            private double farAmplitude = 100.0;

            [SerializeField]
            private double nearAmplitude = 0.1;

            public SgtTerrainSimplex Layer =>
                layer;

            public double FarAmplitude =>
                farAmplitude;

            public double NearAmplitude =>
                nearAmplitude;

            public bool HasValidSettings =>
                layer != null &&
                IsFinite(
                    farAmplitude) &&
                IsFinite(
                    nearAmplitude);

            private static bool IsFinite(
                double value)
            {
                return
                    !double.IsNaN(value) &&
                    !double.IsInfinity(value);
            }
        }

        [Header("Session")]
        [SerializeField]
        private CelestialBodyRuntimeContext bodyContext;

        [SerializeField]
        private RoundMapMagicSurfaceSession surfaceSession;

        [SerializeField]
        private RoundMapMagicVirtualHeightSampler midHeightSampler;

        [Header("SGT Heightmap")]
        [SerializeField]
        private SgtTerrainHeightmap heightmap;

        [SerializeField]
        private double farHeightmapDisplacementMeters =
            20000.0;

        [SerializeField]
        private double nearHeightmapDisplacementMeters;

        [Header("SGT Simplex Layers")]
        [SerializeField]
        private SimplexLayerSettings[] simplexLayers =
            new SimplexLayerSettings[0];

        [Header("Runtime")]
        [SerializeField]
        private bool localSurfaceReady;

        [SerializeField]
        private bool midSurfaceReady;

        [SerializeField]
        private bool nearSurfaceMode;

        [SerializeField]
        private int controlledSimplexLayerCount;

        [SerializeField]
        private bool hasAppliedMode;

        private bool configurationIsValid;

        private void Start()
        {
            ResolveMidHeightSampler();
            configurationIsValid =
                ValidateConfiguration(
                    true);
            hasAppliedMode = false;
            ApplyRequestedMode();
        }

        private void LateUpdate()
        {
            if (!configurationIsValid)
            {
                return;
            }

            ApplyRequestedMode();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying ||
                !configurationIsValid)
            {
                return;
            }

            ApplyMode(
                false);
            hasAppliedMode = false;
        }

        private void ApplyRequestedMode()
        {
            if (!configurationIsValid)
            {
                return;
            }

            var shouldUseNearSurfaceMode =
                surfaceSession.HasActiveSession &&
                surfaceSession.ActiveBodyContext ==
                    bodyContext;
            localSurfaceReady =
                shouldUseNearSurfaceMode;
            midSurfaceReady =
                midHeightSampler != null &&
                midHeightSampler.MidStreamingActive &&
                midHeightSampler.HasCompleteCoverage &&
                midHeightSampler.TrackedBodyContext ==
                    bodyContext;
            shouldUseNearSurfaceMode =
                localSurfaceReady ||
                midSurfaceReady;

            if (!hasAppliedMode ||
                nearSurfaceMode !=
                    shouldUseNearSurfaceMode)
            {
                ApplyMode(
                    shouldUseNearSurfaceMode);
            }
        }

        private void ApplyMode(
            bool useNearSurfaceMode)
        {
            if (heightmap != null)
            {
                heightmap.Displacement =
                    useNearSurfaceMode
                        ? nearHeightmapDisplacementMeters
                        : farHeightmapDisplacementMeters;
            }

            controlledSimplexLayerCount = 0;

            if (simplexLayers != null)
            {
                for (var index = 0;
                    index < simplexLayers.Length;
                    index++)
                {
                    var settings =
                        simplexLayers[index];

                    if (settings == null ||
                        !settings.HasValidSettings)
                    {
                        continue;
                    }

                    settings.Layer.Amplitude =
                        useNearSurfaceMode
                            ? settings.NearAmplitude
                            : settings.FarAmplitude;
                    controlledSimplexLayerCount++;
                }
            }

            nearSurfaceMode =
                useNearSurfaceMode;
            hasAppliedMode = true;
        }

        private bool ValidateConfiguration(
            bool logErrors)
        {
            var valid = true;

            if (bodyContext == null)
            {
                valid = false;

                if (logErrors)
                {
                    Debug.LogError(
                        "The SGT terrain handoff requires the celestial-body context represented by this visual.",
                        this);
                }
            }

            if (surfaceSession == null)
            {
                valid = false;

                if (logErrors)
                {
                    Debug.LogError(
                        "The SGT terrain handoff requires the shared Round MapMagic surface session.",
                        this);
                }
            }

            if (midHeightSampler == null)
            {
                valid = false;

                if (logErrors)
                {
                    Debug.LogError(
                        "The SGT terrain handoff requires the shared virtual mid-height sampler.",
                        this);
                }
            }

            if (!IsFinite(
                    farHeightmapDisplacementMeters) ||
                !IsFinite(
                    nearHeightmapDisplacementMeters))
            {
                valid = false;

                if (logErrors)
                {
                    Debug.LogError(
                        "The SGT terrain handoff requires finite heightmap displacements.",
                        this);
                }
            }

            var validSimplexLayerCount = 0;

            if (simplexLayers != null)
            {
                for (var index = 0;
                    index < simplexLayers.Length;
                    index++)
                {
                    var settings =
                        simplexLayers[index];

                    if (settings != null &&
                        settings.HasValidSettings)
                    {
                        validSimplexLayerCount++;
                    }
                }
            }

            if (heightmap == null &&
                validSimplexLayerCount == 0)
            {
                valid = false;

                if (logErrors)
                {
                    Debug.LogError(
                        "The SGT terrain handoff requires at least one heightmap or Simplex layer.",
                        this);
                }
            }

            return valid;
        }

        private void ResolveMidHeightSampler()
        {
            if (midHeightSampler == null &&
                surfaceSession != null)
            {
                midHeightSampler =
                    surfaceSession.GetComponent<RoundMapMagicVirtualHeightSampler>();
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
