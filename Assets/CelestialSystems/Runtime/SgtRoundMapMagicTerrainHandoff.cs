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

        [SerializeField]
        private RoundMapMagicVirtualHeightSampler localHeightSampler;

        [SerializeField]
        private RoundMapMagicVirtualHeightTileRenderer midTileRenderer;

        [SerializeField]
        private RoundMapMagicVirtualHeightTileRenderer localTileRenderer;

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
        private bool customSurfaceHandoffAllowed;

        [SerializeField]
        private double resolvedHandoffAltitudeMeters;

        [SerializeField]
        private double resolvedReleaseAltitudeMeters;

        [SerializeField]
        private bool nearSurfaceMode;

        [SerializeField]
        private int controlledSimplexLayerCount;

        [SerializeField]
        private bool hasAppliedMode;

        private bool configurationIsValid;

        private void Start()
        {
            ResolveHeightSamplers();
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

            ResolveHeightSamplers();
            var hasTrackedLocalSurface =
                localHeightSampler != null &&
                localHeightSampler.SampleStream ==
                    RoundMapMagicVirtualSampleStream.Local &&
                localHeightSampler.StreamingActive &&
                localHeightSampler.TrackedBodyContext ==
                    bodyContext;
            localSurfaceReady =
                hasTrackedLocalSurface &&
                localHeightSampler.HasCompleteCoverage &&
                localHeightSampler.HasSample &&
                (localTileRenderer == null ||
                    localTileRenderer.HasCompleteVisibleCoverage);
            var hasTrackedMidSurface =
                midHeightSampler != null &&
                midHeightSampler.MidStreamingActive &&
                midHeightSampler.TrackedBodyContext ==
                    bodyContext;
            midSurfaceReady =
                hasTrackedMidSurface &&
                midHeightSampler.HasCompleteCoverage &&
                midHeightSampler.HasSample &&
                (midTileRenderer == null ||
                    midTileRenderer.HasCompleteVisibleCoverage);
            UpdateHandoffAltitudeState();
            var hasUsableMidSurface =
                nearSurfaceMode
                    ? hasTrackedMidSurface &&
                        midHeightSampler.HasSample
                    : midSurfaceReady;
            var hasUsableLocalSurface =
                localHeightSampler == null
                    ? hasUsableMidSurface
                    : nearSurfaceMode
                        ? hasTrackedLocalSurface &&
                            localHeightSampler.HasSample
                        : localSurfaceReady;
            var shouldUseNearSurfaceMode =
                customSurfaceHandoffAllowed &&
                hasUsableMidSurface &&
                hasUsableLocalSurface;

            if (!hasAppliedMode ||
                nearSurfaceMode !=
                    shouldUseNearSurfaceMode)
            {
                ApplyMode(
                    shouldUseNearSurfaceMode);
            }
        }

        private void UpdateHandoffAltitudeState()
        {
            var qualityProfile =
                surfaceSession.ConfiguredQualityProfile;

            if (qualityProfile == null ||
                !qualityProfile.HasValidSettings ||
                midHeightSampler == null ||
                midHeightSampler.TrackedBodyContext !=
                    bodyContext)
            {
                customSurfaceHandoffAllowed = false;
                resolvedHandoffAltitudeMeters = default;
                resolvedReleaseAltitudeMeters = default;
                return;
            }

            resolvedHandoffAltitudeMeters =
                qualityProfile.CustomSurfaceHandoffAltitudeMeters;
            resolvedReleaseAltitudeMeters =
                qualityProfile.CustomSurfaceReleaseAltitudeMeters;
            var altitudeMeters =
                midHeightSampler.AnchorAltitudeMeters;

            if (customSurfaceHandoffAllowed)
            {
                if (altitudeMeters >
                    resolvedReleaseAltitudeMeters)
                {
                    customSurfaceHandoffAllowed = false;
                }

                return;
            }

            if (altitudeMeters <=
                resolvedHandoffAltitudeMeters)
            {
                customSurfaceHandoffAllowed = true;
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

        private void ResolveHeightSamplers()
        {
            if (surfaceSession == null)
            {
                return;
            }

            if (midHeightSampler != null &&
                midHeightSampler.SampleStream !=
                    RoundMapMagicVirtualSampleStream.Mid)
            {
                midHeightSampler =
                    null;
            }

            if (localHeightSampler != null &&
                localHeightSampler.SampleStream !=
                    RoundMapMagicVirtualSampleStream.Local)
            {
                localHeightSampler =
                    null;
            }

            if (midTileRenderer != null &&
                midTileRenderer.SampleStream !=
                    RoundMapMagicVirtualSampleStream.Mid)
            {
                midTileRenderer =
                    null;
            }

            if (localTileRenderer != null &&
                localTileRenderer.SampleStream !=
                    RoundMapMagicVirtualSampleStream.Local)
            {
                localTileRenderer =
                    null;
            }

            if (midHeightSampler != null &&
                localHeightSampler != null &&
                midTileRenderer != null &&
                localTileRenderer != null)
            {
                return;
            }

            var samplers =
                surfaceSession.GetComponents<RoundMapMagicVirtualHeightSampler>();

            for (var index = 0;
                index < samplers.Length;
                index++)
            {
                var sampler =
                    samplers[index];

                if (sampler.SampleStream ==
                    RoundMapMagicVirtualSampleStream.Mid)
                {
                    midHeightSampler =
                        sampler;
                }
                else if (sampler.SampleStream ==
                    RoundMapMagicVirtualSampleStream.Local)
                {
                    localHeightSampler =
                        sampler;
                }
            }

            if (midTileRenderer != null &&
                localTileRenderer != null)
            {
                return;
            }

            var renderers =
                surfaceSession.GetComponents<RoundMapMagicVirtualHeightTileRenderer>();

            for (var index = 0;
                index < renderers.Length;
                index++)
            {
                var renderer =
                    renderers[index];

                if (renderer.SampleStream ==
                    RoundMapMagicVirtualSampleStream.Mid)
                {
                    midTileRenderer =
                        renderer;
                }
                else if (renderer.SampleStream ==
                    RoundMapMagicVirtualSampleStream.Local)
                {
                    localTileRenderer =
                        renderer;
                }
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
