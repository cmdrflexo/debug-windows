/*
 * Prototype scene-light controller that follows the generated system's
 * primary stellar body through the SGT floating-origin coordinate system.
 *
 * The generated definition provides physical luminosity and effective
 * temperature. This component maps those values into renderer-specific light
 * settings, keeping visual calibration outside generation.
 */

using System;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(450)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SgtFloatingObject))]
    [RequireComponent(typeof(Light))]
    public sealed class SgtPrimaryStarLightPrototype :
        MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private CelestialUniverseRuntimeController generationController;

        [Header("Generated Appearance")]
        [SerializeField]
        [Tooltip("Maps generated stellar temperature and luminosity onto this Unity light.")]
        private bool applyGeneratedAppearance = true;

        [SerializeField, Min(100.0f)]
        [Tooltip("Temperature represented by the left edge of the spectrum gradient.")]
        private float spectrumMinimumTemperatureKelvin = 1800.0f;

        [SerializeField, Min(100.0f)]
        [Tooltip("Temperature represented by the right edge of the spectrum gradient.")]
        private float spectrumMaximumTemperatureKelvin = 40000.0f;

        [SerializeField]
        [Tooltip("Maps normalized effective temperature from cool to hot into light color.")]
        private Gradient temperatureSpectrum =
            CreateDefaultTemperatureSpectrum();

        [SerializeField]
        [Tooltip("Maps log10(generated luminosity in solar units) into Unity Light intensity.")]
        private AnimationCurve logLuminosityToIntensity =
            CreateDefaultLuminosityIntensityCurve();

        [SerializeField, Min(0.0f)]
        [Tooltip("Final multiplier applied after the generated luminosity curve.")]
        private float intensityMultiplier = 1.0f;

        [SerializeField]
        [Tooltip("Disables the light when its generated stellar luminosity maps to zero.")]
        private bool disableWhenNoEmittedLight = true;

        [Header("Runtime")]
        [SerializeField]
        private CelestialBodyRuntimeContext trackedStar;

        [SerializeField]
        private bool isTrackingPrimaryStar;

        [SerializeField]
        private double resolvedEffectiveTemperatureKelvin;

        [SerializeField]
        private double resolvedLuminositySolar;

        [SerializeField]
        private Color resolvedLightColor = Color.white;

        [SerializeField]
        private float resolvedLightIntensity;

        private SgtFloatingObject floatingObject;
        private Light sceneLight;

        /// <summary>
        /// The most massive generated stellar body currently used as the
        /// source position for this light.
        /// </summary>
        public CelestialBodyRuntimeContext TrackedStar =>
            trackedStar;

        public CelestialBodyDefinition TrackedStarDefinition =>
            trackedStar != null
                ? trackedStar.Definition
                : null;

        public bool IsTrackingPrimaryStar =>
            isTrackingPrimaryStar;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            spectrumMinimumTemperatureKelvin =
                Mathf.Max(
                    100.0f,
                    spectrumMinimumTemperatureKelvin);
            spectrumMaximumTemperatureKelvin =
                Mathf.Max(
                    spectrumMinimumTemperatureKelvin + 1.0f,
                    spectrumMaximumTemperatureKelvin);
            intensityMultiplier =
                Mathf.Max(
                    0.0f,
                    intensityMultiplier);
        }

        private void LateUpdate()
        {
            ResolveReferences();

            if (generationController == null ||
                !generationController.TryGetPrimaryStellarBody(
                    out trackedStar) ||
                !trackedStar.TryGetMotionState(
                    out var starMotion) ||
                floatingObject == null ||
                !SgtUniversePositionConverter.TryToSgtPosition(
                    starMotion.Position,
                    0.0,
                    0.0,
                    0.0,
                    out var starPosition))
            {
                trackedStar = null;
                isTrackingPrimaryStar = false;
                return;
            }

            floatingObject.SetPosition(
                starPosition);
            floatingObject.ApplyPosition();

            if (applyGeneratedAppearance)
            {
                ApplyGeneratedAppearance(
                    trackedStar.Definition);
            }

            isTrackingPrimaryStar = true;
        }

        private void ApplyGeneratedAppearance(
            CelestialBodyDefinition definition)
        {
            if (sceneLight == null ||
                definition == null ||
                !definition.HasStellarProperties)
            {
                return;
            }

            resolvedEffectiveTemperatureKelvin =
                definition.StellarEffectiveTemperatureKelvin;
            resolvedLuminositySolar =
                definition.StellarLuminositySolar;

            var normalizedTemperature =
                Mathf.InverseLerp(
                    spectrumMinimumTemperatureKelvin,
                    spectrumMaximumTemperatureKelvin,
                    (float)resolvedEffectiveTemperatureKelvin);
            resolvedLightColor =
                temperatureSpectrum.Evaluate(
                    normalizedTemperature);

            // Prototype visual calibration: physical luminosity cannot be
            // applied directly to Unity's arbitrary Light intensity units.
            // The curve is deliberately editable while render-scale lighting
            // is still being designed.
            var logLuminosity =
                Math.Log10(
                    Math.Max(
                        1.0e-12,
                        resolvedLuminositySolar));
            resolvedLightIntensity =
                Mathf.Max(
                    0.0f,
                    logLuminosityToIntensity.Evaluate(
                        (float)logLuminosity) *
                    intensityMultiplier);

            sceneLight.color =
                resolvedLightColor;
            sceneLight.intensity =
                resolvedLightIntensity;

            if (disableWhenNoEmittedLight)
            {
                sceneLight.enabled =
                    resolvedLightIntensity > 0.0f;
            }
        }

        private void ResolveReferences()
        {
            if (generationController == null)
            {
                generationController =
                    FindFirstObjectByType<CelestialUniverseRuntimeController>();
            }

            floatingObject ??=
                GetComponent<SgtFloatingObject>();
            sceneLight ??=
                GetComponent<Light>();
        }

        private static Gradient CreateDefaultTemperatureSpectrum()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(
                        new Color(1.0f, 0.16f, 0.03f),
                        0.0f),
                    new GradientColorKey(
                        new Color(1.0f, 0.48f, 0.14f),
                        0.12f),
                    new GradientColorKey(
                        new Color(1.0f, 0.84f, 0.62f),
                        0.28f),
                    new GradientColorKey(
                        new Color(1.0f, 0.96f, 0.88f),
                        0.42f),
                    new GradientColorKey(
                        new Color(0.88f, 0.94f, 1.0f),
                        0.62f),
                    new GradientColorKey(
                        new Color(0.56f, 0.72f, 1.0f),
                        1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 1.0f)
                });
            return gradient;
        }

        private static AnimationCurve CreateDefaultLuminosityIntensityCurve()
        {
            // Prototype visual calibration. Horizontal values are
            // log10(luminosity / solar luminosity), not raw luminosity.
            return new AnimationCurve(
                new Keyframe(-12.0f, 0.0f),
                new Keyframe(-6.0f, 0.02f),
                new Keyframe(-3.0f, 0.08f),
                new Keyframe(-1.0f, 0.35f),
                new Keyframe(0.0f, 1.0f),
                new Keyframe(2.0f, 3.0f),
                new Keyframe(5.0f, 8.0f));
        }
    }
}
