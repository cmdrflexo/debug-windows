/*
 * Provides small content helpers so project code can populate a debug window without building uGUI plumbing.
 */

using System;
using TMPro;
using UnityEngine;

namespace jcan.DebugWindows
{
    public sealed class DebugWindowContent
    {
        private readonly DebugWindowManager manager;

        internal DebugWindowContent(DebugWindowManager manager, RectTransform root)
        {
            this.manager = manager;
            Root = root;
        }

        public RectTransform Root { get; }
        public float TextSize => manager.TextSize;
        internal DebugWindowManager Manager => manager;

        public TMP_Text AddText(string text)
        {
            var label = DebugWindowUi.CreateText(
                "Text",
                Root,
                text ?? string.Empty,
                manager.TextSize,
                manager.TextColor,
                TextAlignmentOptions.TopLeft);
            label.enableWordWrapping = false;
            return label;
        }

        public TMP_Text AddUpdatingText(
            Func<string> valueProvider,
            float refreshIntervalSeconds = 0.1f)
        {
            var label = AddText(string.Empty);
            var updater = label.gameObject.AddComponent<DebugWindowTextUpdater>();
            updater.Initialize(label, valueProvider, refreshIntervalSeconds);
            return label;
        }
    }
}
