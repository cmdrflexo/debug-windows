/*
 * Owns persistent player-facing display, quality, and base post-processing settings independently of any settings UI.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class GameSettingsController : MonoBehaviour
    {
        private const string PlayerPrefsKey =
            "jcan.celestialsystems.game-settings.v1";

        [Serializable]
        private sealed class SavedSettings
        {
            public int version = 1;
            public int resolutionWidth;
            public int resolutionHeight;
            public int fullScreenMode;
            public int vSyncCount;
            public int targetFrameRate;
            public int antiAliasing;
            public int globalTextureMipmapLimit;
            public int anisotropicFiltering;
            public float lodBias;

            public bool postProcessingInitialized;
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

        [SerializeField]
        private ImageEffectorReceiver imageEffectorReceiver;

        [SerializeField]
        [Min(0.0f)]
        private float saveDelaySeconds = 0.25f;

        [SerializeField]
        private string lastPersistenceError;

        private SavedSettings settings;
        private bool savePending;
        private double saveAtTime;
        private bool applicationIsQuitting;

        public int ResolutionWidth => settings?.resolutionWidth ?? Screen.width;
        public int ResolutionHeight => settings?.resolutionHeight ?? Screen.height;
        public FullScreenMode FullScreenMode =>
            settings != null
                ? (FullScreenMode)settings.fullScreenMode
                : Screen.fullScreenMode;
        public bool VSyncEnabled => settings != null
            ? settings.vSyncCount > 0
            : QualitySettings.vSyncCount > 0;
        public int TargetFrameRate => settings?.targetFrameRate ??
            Application.targetFrameRate;
        public int AntiAliasing => settings?.antiAliasing ??
            QualitySettings.antiAliasing;
        public int GlobalTextureMipmapLimit =>
            settings?.globalTextureMipmapLimit ??
            QualitySettings.globalTextureMipmapLimit;
        public AnisotropicFiltering AnisotropicFiltering =>
            settings != null
                ? (AnisotropicFiltering)settings.anisotropicFiltering
                : QualitySettings.anisotropicFiltering;
        public float LodBias => settings?.lodBias ?? QualitySettings.lodBias;

        public bool PostProcessingEnabled =>
            settings?.postProcessingEnabled ?? false;
        public bool AutomaticExposureEnabled =>
            settings?.automaticExposureEnabled ?? false;
        public float ExposureCompensation =>
            settings?.exposureCompensation ?? 0.0f;
        public float MinimumExposure => settings?.minimumExposure ?? -1.0f;
        public float MaximumExposure => settings?.maximumExposure ?? 2.0f;
        public float MiddleGray => settings?.middleGray ?? 0.36f;
        public float BrightenSpeed => settings?.brightenSpeed ?? 0.3f;
        public float DarkenSpeed => settings?.darkenSpeed ?? 2.0f;
        public bool BloomEnabled => settings?.bloomEnabled ?? false;
        public float BloomIntensity => settings?.bloomIntensity ?? 0.25f;
        public bool LensDistortionEnabled =>
            settings?.lensDistortionEnabled ?? false;
        public float LensDistortionIntensity =>
            settings?.lensDistortionIntensity ?? 0.0f;
        public bool MotionBlurEnabled => settings?.motionBlurEnabled ?? false;
        public float MotionBlurIntensity =>
            settings?.motionBlurIntensity ?? 1.0f;
        public float Contrast => settings?.contrast ?? 0.0f;
        public float Saturation => settings?.saturation ?? 0.0f;

        public ImageEffectorReceiver ImageEffectorReceiver =>
            imageEffectorReceiver;
        public string LastPersistenceError => lastPersistenceError;

        private void Awake()
        {
            LoadOrCaptureSettings();
            ApplyDisplayAndQuality();

            if (imageEffectorReceiver == null)
                imageEffectorReceiver = GetComponent<ImageEffectorReceiver>();
            if (imageEffectorReceiver != null)
                BindImageEffectorReceiver(imageEffectorReceiver);
        }

        private void Update()
        {
            if (savePending &&
                Time.unscaledTimeAsDouble >= saveAtTime)
            {
                SaveNow();
            }
        }

        private void OnDisable()
        {
            if (!applicationIsQuitting)
                SaveNow();
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
            SaveNow();
        }

        public void BindImageEffectorReceiver(ImageEffectorReceiver value)
        {
            imageEffectorReceiver = value;
            if (settings == null ||
                imageEffectorReceiver == null ||
                !imageEffectorReceiver.EnsureInitialized())
            {
                return;
            }

            if (!settings.postProcessingInitialized)
            {
                CapturePostProcessing(
                    imageEffectorReceiver.BaseSettings);
                settings.postProcessingInitialized = true;
                MarkDirty();
            }

            ApplyPostProcessing();
        }

        public void SetResolution(int width, int height)
        {
            if (settings == null || width <= 0 || height <= 0)
                return;

            settings.resolutionWidth = width;
            settings.resolutionHeight = height;
            Screen.SetResolution(
                width,
                height,
                (FullScreenMode)settings.fullScreenMode);
            MarkDirty();
        }

        public void SetFullScreenMode(FullScreenMode mode)
        {
            if (settings == null)
                return;

            settings.fullScreenMode = (int)mode;
            Screen.fullScreenMode = mode;
            MarkDirty();
        }

        public void SetVSyncEnabled(bool enabled)
        {
            if (settings == null)
                return;

            settings.vSyncCount = enabled ? 1 : 0;
            QualitySettings.vSyncCount = settings.vSyncCount;
            MarkDirty();
        }

        public void SetTargetFrameRate(int value)
        {
            if (settings == null)
                return;

            settings.targetFrameRate = value < 0
                ? -1
                : Mathf.Clamp(value, 15, 1000);
            Application.targetFrameRate = settings.targetFrameRate;
            MarkDirty();
        }

        public void SetAntiAliasing(int value)
        {
            if (settings == null)
                return;

            settings.antiAliasing = NormalizeAntiAliasing(value);
            QualitySettings.antiAliasing = settings.antiAliasing;
            MarkDirty();
        }

        public void SetGlobalTextureMipmapLimit(int value)
        {
            if (settings == null)
                return;

            settings.globalTextureMipmapLimit = Mathf.Clamp(value, 0, 3);
            QualitySettings.globalTextureMipmapLimit =
                settings.globalTextureMipmapLimit;
            MarkDirty();
        }

        public void SetAnisotropicFiltering(AnisotropicFiltering value)
        {
            if (settings == null)
                return;

            settings.anisotropicFiltering = (int)value;
            QualitySettings.anisotropicFiltering = value;
            MarkDirty();
        }

        public void SetLodBias(float value)
        {
            if (settings == null)
                return;

            settings.lodBias = Mathf.Clamp(value, 0.25f, 4.0f);
            QualitySettings.lodBias = settings.lodBias;
            MarkDirty();
        }

        public void SetPostProcessingEnabled(bool value)
        {
            settings.postProcessingEnabled = value;
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetAutomaticExposureEnabled(bool value)
        {
            settings.automaticExposureEnabled = value;
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetExposureCompensation(float value)
        {
            settings.exposureCompensation = Mathf.Clamp(value, -10.0f, 10.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetMinimumExposure(float value)
        {
            settings.minimumExposure = Mathf.Clamp(value, -16.0f, 16.0f);
            settings.maximumExposure = Mathf.Max(
                settings.maximumExposure,
                settings.minimumExposure);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetMaximumExposure(float value)
        {
            settings.maximumExposure = Mathf.Clamp(
                value,
                settings.minimumExposure,
                16.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetMiddleGray(float value)
        {
            settings.middleGray = Mathf.Clamp(value, 0.01f, 1.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetBrightenSpeed(float value)
        {
            settings.brightenSpeed = Mathf.Clamp(value, 0.01f, 20.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetDarkenSpeed(float value)
        {
            settings.darkenSpeed = Mathf.Clamp(value, 0.01f, 20.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetBloomEnabled(bool value)
        {
            settings.bloomEnabled = value;
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetBloomIntensity(float value)
        {
            settings.bloomIntensity = Mathf.Clamp(value, 0.0f, 20.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetLensDistortionEnabled(bool value)
        {
            settings.lensDistortionEnabled = value;
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetLensDistortionIntensity(float value)
        {
            settings.lensDistortionIntensity = Mathf.Clamp(value, -1.0f, 1.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetMotionBlurEnabled(bool value)
        {
            settings.motionBlurEnabled = value;
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetMotionBlurIntensity(float value)
        {
            settings.motionBlurIntensity = Mathf.Clamp(value, 0.0f, 1.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetContrast(float value)
        {
            settings.contrast = Mathf.Clamp(value, -100.0f, 100.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SetSaturation(float value)
        {
            settings.saturation = Mathf.Clamp(value, -100.0f, 100.0f);
            ApplyPostProcessingAndMarkDirty();
        }

        public void SaveNow()
        {
            if (settings == null || !savePending)
                return;

            try
            {
                PlayerPrefs.SetString(
                    PlayerPrefsKey,
                    JsonUtility.ToJson(settings));
                PlayerPrefs.Save();
                lastPersistenceError = string.Empty;
                savePending = false;
            }
            catch (Exception exception)
            {
                lastPersistenceError = exception.Message;
                Debug.LogException(exception, this);
            }
        }

        private void LoadOrCaptureSettings()
        {
            settings = null;

            if (PlayerPrefs.HasKey(PlayerPrefsKey))
            {
                try
                {
                    settings = JsonUtility.FromJson<SavedSettings>(
                        PlayerPrefs.GetString(PlayerPrefsKey));
                }
                catch (Exception exception)
                {
                    lastPersistenceError = exception.Message;
                    Debug.LogException(exception, this);
                }
            }

            if (settings == null || settings.version != 1)
                settings = CaptureCurrentSettings();

            ValidateSettings();
        }

        private static SavedSettings CaptureCurrentSettings()
        {
            return new SavedSettings
            {
                resolutionWidth = Screen.width,
                resolutionHeight = Screen.height,
                fullScreenMode = (int)Screen.fullScreenMode,
                vSyncCount = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                antiAliasing = QualitySettings.antiAliasing,
                globalTextureMipmapLimit =
                    QualitySettings.globalTextureMipmapLimit,
                anisotropicFiltering =
                    (int)QualitySettings.anisotropicFiltering,
                lodBias = QualitySettings.lodBias
            };
        }

        private void CapturePostProcessing(ImageEffectBaseSettings value)
        {
            settings.postProcessingEnabled = value.postProcessingEnabled;
            settings.automaticExposureEnabled =
                value.automaticExposureEnabled;
            settings.exposureCompensation = value.exposureCompensation;
            settings.minimumExposure = value.minimumExposure;
            settings.maximumExposure = value.maximumExposure;
            settings.middleGray = value.middleGray;
            settings.brightenSpeed = value.brightenSpeed;
            settings.darkenSpeed = value.darkenSpeed;
            settings.bloomEnabled = value.bloomEnabled;
            settings.bloomIntensity = value.bloomIntensity;
            settings.lensDistortionEnabled = value.lensDistortionEnabled;
            settings.lensDistortionIntensity =
                value.lensDistortionIntensity;
            settings.motionBlurEnabled = value.motionBlurEnabled;
            settings.motionBlurIntensity = value.motionBlurIntensity;
            settings.contrast = value.contrast;
            settings.saturation = value.saturation;
        }

        private void ValidateSettings()
        {
            settings.resolutionWidth = Mathf.Max(320, settings.resolutionWidth);
            settings.resolutionHeight = Mathf.Max(200, settings.resolutionHeight);

            if (!Enum.IsDefined(typeof(FullScreenMode), settings.fullScreenMode))
                settings.fullScreenMode = (int)Screen.fullScreenMode;

            settings.vSyncCount = Mathf.Clamp(settings.vSyncCount, 0, 4);
            settings.targetFrameRate = settings.targetFrameRate < 0
                ? -1
                : Mathf.Clamp(settings.targetFrameRate, 15, 1000);
            settings.antiAliasing = NormalizeAntiAliasing(settings.antiAliasing);
            settings.globalTextureMipmapLimit = Mathf.Clamp(
                settings.globalTextureMipmapLimit,
                0,
                3);

            if (!Enum.IsDefined(
                    typeof(AnisotropicFiltering),
                    settings.anisotropicFiltering))
            {
                settings.anisotropicFiltering =
                    (int)QualitySettings.anisotropicFiltering;
            }

            settings.lodBias = Mathf.Clamp(settings.lodBias, 0.25f, 4.0f);
            if (!settings.postProcessingInitialized)
                return;

            settings.exposureCompensation = Mathf.Clamp(
                settings.exposureCompensation,
                -10.0f,
                10.0f);
            settings.minimumExposure = Mathf.Clamp(
                settings.minimumExposure,
                -16.0f,
                16.0f);
            settings.maximumExposure = Mathf.Clamp(
                settings.maximumExposure,
                settings.minimumExposure,
                16.0f);
            settings.middleGray = Mathf.Clamp(settings.middleGray, 0.01f, 1.0f);
            settings.brightenSpeed = Mathf.Clamp(
                settings.brightenSpeed,
                0.01f,
                20.0f);
            settings.darkenSpeed = Mathf.Clamp(
                settings.darkenSpeed,
                0.01f,
                20.0f);
            settings.bloomIntensity = Mathf.Clamp(
                settings.bloomIntensity,
                0.0f,
                20.0f);
            settings.lensDistortionIntensity = Mathf.Clamp(
                settings.lensDistortionIntensity,
                -1.0f,
                1.0f);
            settings.motionBlurIntensity = Mathf.Clamp(
                settings.motionBlurIntensity,
                0.0f,
                1.0f);
            settings.contrast = Mathf.Clamp(settings.contrast, -100.0f, 100.0f);
            settings.saturation = Mathf.Clamp(
                settings.saturation,
                -100.0f,
                100.0f);
        }

        private void ApplyDisplayAndQuality()
        {
            Screen.SetResolution(
                settings.resolutionWidth,
                settings.resolutionHeight,
                (FullScreenMode)settings.fullScreenMode);
            QualitySettings.vSyncCount = settings.vSyncCount;
            Application.targetFrameRate = settings.targetFrameRate;
            QualitySettings.antiAliasing = settings.antiAliasing;
            QualitySettings.globalTextureMipmapLimit =
                settings.globalTextureMipmapLimit;
            QualitySettings.anisotropicFiltering =
                (AnisotropicFiltering)settings.anisotropicFiltering;
            QualitySettings.lodBias = settings.lodBias;
        }

        private void ApplyPostProcessing()
        {
            if (imageEffectorReceiver == null ||
                !settings.postProcessingInitialized)
            {
                return;
            }

            imageEffectorReceiver.SetBaseSettings(
                new ImageEffectBaseSettings
                {
                    postProcessingEnabled = settings.postProcessingEnabled,
                    automaticExposureEnabled =
                        settings.automaticExposureEnabled,
                    exposureCompensation = settings.exposureCompensation,
                    minimumExposure = settings.minimumExposure,
                    maximumExposure = settings.maximumExposure,
                    middleGray = settings.middleGray,
                    brightenSpeed = settings.brightenSpeed,
                    darkenSpeed = settings.darkenSpeed,
                    bloomEnabled = settings.bloomEnabled,
                    bloomIntensity = settings.bloomIntensity,
                    lensDistortionEnabled =
                        settings.lensDistortionEnabled,
                    lensDistortionIntensity =
                        settings.lensDistortionIntensity,
                    motionBlurEnabled = settings.motionBlurEnabled,
                    motionBlurIntensity = settings.motionBlurIntensity,
                    contrast = settings.contrast,
                    saturation = settings.saturation
                });
        }

        private void ApplyPostProcessingAndMarkDirty()
        {
            if (settings == null)
                return;

            ApplyPostProcessing();
            MarkDirty();
        }

        private void MarkDirty()
        {
            savePending = true;
            saveAtTime =
                Time.unscaledTimeAsDouble +
                Mathf.Max(0.0f, saveDelaySeconds);
        }

        private static int NormalizeAntiAliasing(int value)
        {
            if (value >= 8)
                return 8;
            if (value >= 4)
                return 4;
            if (value >= 2)
                return 2;
            return 0;
        }
    }
}
