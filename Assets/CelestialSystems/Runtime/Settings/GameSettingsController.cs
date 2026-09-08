/*
 * Owns persistent player-facing display and quality settings independently of any settings user interface.
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
        }

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
        public string LastPersistenceError => lastPersistenceError;

        private void Awake()
        {
            LoadOrCaptureSettings();
            ApplyAll();
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

        private void ValidateSettings()
        {
            settings.resolutionWidth = Mathf.Max(
                320,
                settings.resolutionWidth);
            settings.resolutionHeight = Mathf.Max(
                200,
                settings.resolutionHeight);

            if (!Enum.IsDefined(
                    typeof(FullScreenMode),
                    settings.fullScreenMode))
            {
                settings.fullScreenMode =
                    (int)Screen.fullScreenMode;
            }

            settings.vSyncCount = Mathf.Clamp(
                settings.vSyncCount,
                0,
                4);
            settings.targetFrameRate =
                settings.targetFrameRate < 0
                    ? -1
                    : Mathf.Clamp(
                        settings.targetFrameRate,
                        15,
                        1000);
            settings.antiAliasing =
                NormalizeAntiAliasing(
                    settings.antiAliasing);
            settings.globalTextureMipmapLimit =
                Mathf.Clamp(
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

            settings.lodBias = Mathf.Clamp(
                settings.lodBias,
                0.25f,
                4.0f);
        }

        private void ApplyAll()
        {
            Screen.SetResolution(
                settings.resolutionWidth,
                settings.resolutionHeight,
                (FullScreenMode)settings.fullScreenMode);
            QualitySettings.vSyncCount =
                settings.vSyncCount;
            Application.targetFrameRate =
                settings.targetFrameRate;
            QualitySettings.antiAliasing =
                settings.antiAliasing;
            QualitySettings.globalTextureMipmapLimit =
                settings.globalTextureMipmapLimit;
            QualitySettings.anisotropicFiltering =
                (AnisotropicFiltering)settings.anisotropicFiltering;
            QualitySettings.lodBias =
                settings.lodBias;
        }

        private void MarkDirty()
        {
            savePending = true;
            saveAtTime =
                Time.unscaledTimeAsDouble +
                Mathf.Max(
                    0.0f,
                    saveDelaySeconds);
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
