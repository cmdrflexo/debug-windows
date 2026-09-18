/*
 * Combines base image settings with named effector contributions and applies the result to a runtime Global Volume profile.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct ImageEffectBaseSettings
    {
        public bool postProcessingEnabled;
        public bool automaticExposureEnabled;
        public float exposureCompensation;
        public float minimumExposure;
        public float maximumExposure;
        public float middleGray;
        public float brightenSpeed;
        public float darkenSpeed;
        public bool bloomEnabled;
        public float bloomIntensity;
        public bool lensDistortionEnabled;
        public float lensDistortionIntensity;
        public bool motionBlurEnabled;
        public float motionBlurIntensity;
        public float contrast;
        public float saturation;
    }

    [Serializable]
    public struct ImageEffectContribution
    {
        public float bloomIntensity;
        public float exposureCompensation;
        public float lensDistortionIntensity;
    }

    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class ImageEffectorReceiver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Optional source profile. When empty, the active Global Volume profile is used.")]
        private VolumeProfile sourceProfile;

        [SerializeField]
        private string lastError;

        private readonly Dictionary<string, ImageEffectContribution> contributions =
            new Dictionary<string, ImageEffectContribution>(StringComparer.Ordinal);

        private VolumeProfile originalProfile;
        private VolumeProfile runtimeProfile;
        private AutomaticExposure automaticExposure;
        private Bloom bloom;
        private LensDistortion lensDistortion;
        private MotionBlur motionBlur;
        private ColorAdjustments colorAdjustments;
        private ImageEffectBaseSettings baseSettings;
        private bool initialized;

        public bool IsReady => initialized;
        public string LastError => lastError;
        public ImageEffectBaseSettings BaseSettings => baseSettings;
        public float EffectiveBloomIntensity { get; private set; }
        public float EffectiveExposureCompensation { get; private set; }
        public float EffectiveLensDistortionIntensity { get; private set; }
        public int ActiveContributionCount => contributions.Count;

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (runtimeProfile == null)
                return;

            var manager = VolumeManager.instance;
            if (manager != null && manager.globalDefaultProfile == runtimeProfile)
                manager.SetGlobalDefaultProfile(originalProfile);

            Destroy(runtimeProfile);
            runtimeProfile = null;
        }

        public bool EnsureInitialized()
        {
            if (initialized)
                return true;

            var manager = VolumeManager.instance;
            originalProfile = sourceProfile != null
                ? sourceProfile
                : manager?.globalDefaultProfile;
            if (manager == null || originalProfile == null)
            {
                lastError = "No Global Volume profile is available.";
                return false;
            }

            runtimeProfile = Instantiate(originalProfile);
            runtimeProfile.name = originalProfile.name + " [Runtime]";
            automaticExposure = GetOrAdd<AutomaticExposure>(runtimeProfile);
            bloom = GetOrAdd<Bloom>(runtimeProfile);
            lensDistortion = GetOrAdd<LensDistortion>(runtimeProfile);
            motionBlur = GetOrAdd<MotionBlur>(runtimeProfile);
            colorAdjustments = GetOrAdd<ColorAdjustments>(runtimeProfile);

            baseSettings = CaptureBaseSettings();
            manager.SetGlobalDefaultProfile(runtimeProfile);
            lastError = string.Empty;
            initialized = true;
            Recalculate();
            return true;
        }

        public void SetBaseSettings(ImageEffectBaseSettings value)
        {
            if (!EnsureInitialized())
                return;

            baseSettings = Sanitize(value);
            Recalculate();
        }

        public void SetContribution(string sourceId, ImageEffectContribution value)
        {
            if (string.IsNullOrWhiteSpace(sourceId) || !EnsureInitialized())
                return;

            contributions[sourceId.Trim()] = value;
            Recalculate();
        }

        public void ClearContribution(string sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId))
                return;

            if (contributions.Remove(sourceId.Trim()))
                Recalculate();
        }

        public void ClearAllContributions()
        {
            if (contributions.Count == 0)
                return;

            contributions.Clear();
            Recalculate();
        }

        private ImageEffectBaseSettings CaptureBaseSettings()
        {
            return Sanitize(new ImageEffectBaseSettings
            {
                postProcessingEnabled =
                    automaticExposure.active ||
                    bloom.active ||
                    lensDistortion.active ||
                    motionBlur.active ||
                    colorAdjustments.active,
                automaticExposureEnabled = automaticExposure.active,
                exposureCompensation = automaticExposure.compensation.value,
                minimumExposure = automaticExposure.minimumExposure.value,
                maximumExposure = automaticExposure.maximumExposure.value,
                middleGray = automaticExposure.middleGray.value,
                brightenSpeed = automaticExposure.brightenSpeed.value,
                darkenSpeed = automaticExposure.darkenSpeed.value,
                bloomEnabled = bloom.active,
                bloomIntensity = bloom.intensity.value,
                lensDistortionEnabled = lensDistortion.active,
                lensDistortionIntensity = lensDistortion.intensity.value,
                motionBlurEnabled = motionBlur.active,
                motionBlurIntensity = motionBlur.intensity.value,
                contrast = colorAdjustments.contrast.value,
                saturation = colorAdjustments.saturation.value
            });
        }

        private void Recalculate()
        {
            if (!initialized)
                return;

            var bloomContribution = 0.0f;
            var exposureContribution = 0.0f;
            var distortionContribution = 0.0f;
            foreach (var value in contributions.Values)
            {
                bloomContribution += value.bloomIntensity;
                exposureContribution += value.exposureCompensation;
                distortionContribution += value.lensDistortionIntensity;
            }

            EffectiveBloomIntensity = Mathf.Max(
                0.0f,
                baseSettings.bloomIntensity + bloomContribution);
            EffectiveExposureCompensation = Mathf.Clamp(
                baseSettings.exposureCompensation + exposureContribution,
                -10.0f,
                10.0f);
            EffectiveLensDistortionIntensity = Mathf.Clamp(
                baseSettings.lensDistortionIntensity + distortionContribution,
                -1.0f,
                1.0f);

            var enabled = baseSettings.postProcessingEnabled;
            automaticExposure.active =
                enabled && baseSettings.automaticExposureEnabled;
            automaticExposure.compensation.value =
                EffectiveExposureCompensation;
            automaticExposure.minimumExposure.value =
                baseSettings.minimumExposure;
            automaticExposure.maximumExposure.value =
                baseSettings.maximumExposure;
            automaticExposure.middleGray.value = baseSettings.middleGray;
            automaticExposure.brightenSpeed.value = baseSettings.brightenSpeed;
            automaticExposure.darkenSpeed.value = baseSettings.darkenSpeed;

            bloom.active =
                enabled &&
                (baseSettings.bloomEnabled ||
                    EffectiveBloomIntensity > Mathf.Epsilon);
            bloom.intensity.value = EffectiveBloomIntensity;

            lensDistortion.active =
                enabled &&
                (baseSettings.lensDistortionEnabled ||
                    Mathf.Abs(EffectiveLensDistortionIntensity) > Mathf.Epsilon);
            lensDistortion.intensity.value =
                EffectiveLensDistortionIntensity;

            motionBlur.active =
                enabled && baseSettings.motionBlurEnabled;
            motionBlur.intensity.value = baseSettings.motionBlurIntensity;

            colorAdjustments.active = enabled;
            colorAdjustments.postExposure.value =
                automaticExposure.active
                    ? 0.0f
                    : EffectiveExposureCompensation;
            colorAdjustments.contrast.value = baseSettings.contrast;
            colorAdjustments.saturation.value = baseSettings.saturation;
        }

        private static ImageEffectBaseSettings Sanitize(
            ImageEffectBaseSettings value)
        {
            value.exposureCompensation = Mathf.Clamp(
                value.exposureCompensation,
                -10.0f,
                10.0f);
            value.minimumExposure = Mathf.Clamp(
                value.minimumExposure,
                -16.0f,
                16.0f);
            value.maximumExposure = Mathf.Clamp(
                value.maximumExposure,
                value.minimumExposure,
                16.0f);
            value.middleGray = Mathf.Clamp(value.middleGray, 0.01f, 1.0f);
            value.brightenSpeed = Mathf.Clamp(value.brightenSpeed, 0.01f, 20.0f);
            value.darkenSpeed = Mathf.Clamp(value.darkenSpeed, 0.01f, 20.0f);
            value.bloomIntensity = Mathf.Clamp(value.bloomIntensity, 0.0f, 20.0f);
            value.lensDistortionIntensity = Mathf.Clamp(
                value.lensDistortionIntensity,
                -1.0f,
                1.0f);
            value.motionBlurIntensity = Mathf.Clamp(
                value.motionBlurIntensity,
                0.0f,
                1.0f);
            value.contrast = Mathf.Clamp(value.contrast, -100.0f, 100.0f);
            value.saturation = Mathf.Clamp(value.saturation, -100.0f, 100.0f);
            return value;
        }

        private static T GetOrAdd<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            return profile.TryGet<T>(out var component)
                ? component
                : profile.Add<T>(true);
        }
    }
}
