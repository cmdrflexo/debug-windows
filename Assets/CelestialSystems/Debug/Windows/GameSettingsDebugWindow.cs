/*
 * Supplies player-facing display and quality settings through the generic tabbed debug-window framework.
 */

using System;
using System.Collections.Generic;
using jcan.DebugWindows;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameSettingsController))]
    public sealed class GameSettingsDebugWindow : MonoBehaviour
    {
        private const string WindowId =
            "jcan.celestialsystems.game-settings";

        private sealed class ResolutionChoice
        {
            public ResolutionChoice(int width, int height)
            {
                Width = width;
                Height = height;
            }

            public int Width { get; }
            public int Height { get; }
            public string UniqueId => Width + "x" + Height;
        }

        [SerializeField]
        private GameSettingsController settings;

        [SerializeField]
        private Vector2 preferredContentSize =
            new Vector2(460.0f, 340.0f);

        [SerializeField]
        private Vector2 defaultPosition =
            new Vector2(12.0f, -560.0f);

        private DebugTabbedWindow window;
        private bool applicationIsQuitting;

        private void OnEnable()
        {
            applicationIsQuitting = false;

            if (settings == null)
                settings = GetComponent<GameSettingsController>();
            if (settings == null)
                settings = FindFirstObjectByType<GameSettingsController>();

            if (settings == null)
            {
                Debug.LogWarning(
                    "GameSettingsDebugWindow requires a GameSettingsController.",
                    this);
                return;
            }

            window = new DebugTabbedWindow(
                WindowId,
                "Game Settings",
                new[]
                {
                    new DebugTabbedPage(
                        "display",
                        "Display",
                        BuildDisplayPage),
                    new DebugTabbedPage(
                        "quality",
                        "Quality",
                        BuildQualityPage),
                    new DebugTabbedPage(
                        "post-processing",
                        "Post Processing",
                        BuildPostProcessingPage),
                    new DebugTabbedPage(
                        "debug-effectors",
                        "Debug Effectors",
                        BuildDebugEffectorsPage)
                },
                BuildFooter,
                preferredContentSize,
                DebugWindowDisplayState.Closed,
                defaultPosition);

            if (!DebugWindowRegistry.Register(
                    window.CreateRegistration()))
            {
                window = null;
            }
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDisable()
        {
            if (!applicationIsQuitting)
                DebugWindowRegistry.Unregister(WindowId);

            window = null;
        }

        private void BuildDisplayPage(
            DebugWindowFormContent content)
        {
            content.AddChoice(
                "Resolution",
                BuildResolutionOptions(),
                ResolutionId(
                    settings.ResolutionWidth,
                    settings.ResolutionHeight),
                option =>
                {
                    if (option?.Value is ResolutionChoice resolution)
                    {
                        settings.SetResolution(
                            resolution.Width,
                            resolution.Height);
                    }
                });

            content.AddChoice(
                "Window Mode",
                EnumOptions<FullScreenMode>(),
                settings.FullScreenMode.ToString(),
                option =>
                {
                    if (option?.Value is FullScreenMode mode)
                        settings.SetFullScreenMode(mode);
                });

            content.AddToggle(
                "VSync",
                settings.VSyncEnabled,
                settings.SetVSyncEnabled);

            content.AddChoice(
                "Frame Limit",
                BuildFrameLimitOptions(),
                FrameLimitId(settings.TargetFrameRate),
                option =>
                {
                    if (option?.Value is int value)
                        settings.SetTargetFrameRate(value);
                });

            content.AddReadOnly(
                "Persistence",
                () => string.IsNullOrEmpty(
                        settings.LastPersistenceError)
                    ? "Ready"
                    : settings.LastPersistenceError);
        }

        private void BuildQualityPage(
            DebugWindowFormContent content)
        {
            content.AddReadOnly(
                "Quality Level",
                CurrentQualityLevelName);

            content.AddChoice(
                "Anti-aliasing",
                new[]
                {
                    new DebugChoiceOption("0", "Disabled", 0),
                    new DebugChoiceOption("2", "2× MSAA", 2),
                    new DebugChoiceOption("4", "4× MSAA", 4),
                    new DebugChoiceOption("8", "8× MSAA", 8)
                },
                settings.AntiAliasing.ToString(),
                option =>
                {
                    if (option?.Value is int value)
                        settings.SetAntiAliasing(value);
                });

            content.AddChoice(
                "Texture Detail",
                new[]
                {
                    new DebugChoiceOption("0", "Full", 0),
                    new DebugChoiceOption("1", "Half", 1),
                    new DebugChoiceOption("2", "Quarter", 2),
                    new DebugChoiceOption("3", "Eighth", 3)
                },
                settings.GlobalTextureMipmapLimit.ToString(),
                option =>
                {
                    if (option?.Value is int value)
                    {
                        settings.SetGlobalTextureMipmapLimit(
                            value);
                    }
                });

            content.AddChoice(
                "Anisotropic",
                EnumOptions<AnisotropicFiltering>(),
                settings.AnisotropicFiltering.ToString(),
                option =>
                {
                    if (option?.Value is AnisotropicFiltering value)
                    {
                        settings.SetAnisotropicFiltering(
                            value);
                    }
                });

            content.AddSlider(
                "LOD Bias",
                settings.LodBias,
                0.25f,
                4.0f,
                settings.SetLodBias,
                "×",
                0.25f);
        }

        private static void BuildPostProcessingPage(
            DebugWindowFormContent content)
        {
            content.AddReadOnly(
                "Status",
                () => "Runtime Volume controls are the next milestone.");
        }

        private static void BuildDebugEffectorsPage(
            DebugWindowFormContent content)
        {
            content.AddReadOnly(
                "Status",
                () => "Waiting for the image-effector receiver.");
        }

        private void BuildFooter(
            DebugWindowFormContent content)
        {
            content.AddButton(
                "save",
                "Save Now",
                settings.SaveNow);
        }

        private List<DebugChoiceOption> BuildResolutionOptions()
        {
            var result = new List<DebugChoiceOption>();
            var added = new HashSet<string>(
                StringComparer.Ordinal);
            AddResolutionOption(
                result,
                added,
                settings.ResolutionWidth,
                settings.ResolutionHeight);

            var resolutions = Screen.resolutions;
            for (var i = 0; i < resolutions.Length; i++)
            {
                AddResolutionOption(
                    result,
                    added,
                    resolutions[i].width,
                    resolutions[i].height);
            }

            return result;
        }

        private static void AddResolutionOption(
            ICollection<DebugChoiceOption> options,
            ISet<string> added,
            int width,
            int height)
        {
            var resolution = new ResolutionChoice(
                width,
                height);
            if (!added.Add(resolution.UniqueId))
                return;

            options.Add(
                new DebugChoiceOption(
                    resolution.UniqueId,
                    width + " × " + height,
                    resolution));
        }

        private static List<DebugChoiceOption> BuildFrameLimitOptions()
        {
            return new List<DebugChoiceOption>
            {
                new DebugChoiceOption(
                    "unlimited",
                    "Unlimited",
                    -1),
                new DebugChoiceOption("30", "30 FPS", 30),
                new DebugChoiceOption("60", "60 FPS", 60),
                new DebugChoiceOption("120", "120 FPS", 120),
                new DebugChoiceOption("144", "144 FPS", 144),
                new DebugChoiceOption("240", "240 FPS", 240)
            };
        }

        private static List<DebugChoiceOption> EnumOptions<T>()
            where T : struct, Enum
        {
            var result = new List<DebugChoiceOption>();
            foreach (var value in Enum.GetValues(typeof(T)))
            {
                var text = value.ToString();
                result.Add(
                    new DebugChoiceOption(
                        text,
                        SplitName(text),
                        value));
            }

            return result;
        }

        private static string ResolutionId(
            int width,
            int height)
        {
            return width + "x" + height;
        }

        private static string FrameLimitId(int value)
        {
            switch (value)
            {
                case 30:
                case 60:
                case 120:
                case 144:
                case 240:
                    return value.ToString();
                default:
                    return "unlimited";
            }
        }

        private static string CurrentQualityLevelName()
        {
            var names = QualitySettings.names;
            var index = QualitySettings.GetQualityLevel();
            return index >= 0 && index < names.Length
                ? names[index]
                : index.ToString();
        }

        private static string SplitName(string value)
        {
            for (var i = 1; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]) &&
                    !char.IsUpper(value[i - 1]))
                {
                    value = value.Insert(i++, " ");
                }
            }

            return value;
        }
    }
}
