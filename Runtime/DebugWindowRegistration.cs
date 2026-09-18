/*
 * Describes one debug window without coupling its content builder to a project-specific system.
 */

using System;
using UnityEngine;

namespace jcan.DebugWindows
{
    public sealed class DebugWindowRegistration
    {
        public string UniqueId { get; }
        public string Title { get; }
        public DebugWindowDisplayState DefaultState { get; }
        public Vector2 DefaultPosition { get; }
        public bool CanClose { get; }
        public Action<DebugWindowContent> BuildContent { get; }
        public Func<string> CaptureCustomState { get; }
        public Action<string> RestoreCustomState { get; }

        public DebugWindowRegistration(
            string uniqueId,
            string title,
            Action<DebugWindowContent> buildContent,
            DebugWindowDisplayState defaultState = DebugWindowDisplayState.Closed,
            Vector2? defaultPosition = null,
            bool canClose = true,
            Func<string> captureCustomState = null,
            Action<string> restoreCustomState = null)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                throw new ArgumentException("A debug window requires a stable unique ID.", nameof(uniqueId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("A debug window requires a title.", nameof(title));

            UniqueId = uniqueId.Trim();
            Title = title.Trim();
            BuildContent = buildContent;
            DefaultState = canClose || defaultState != DebugWindowDisplayState.Closed
                ? defaultState
                : DebugWindowDisplayState.Collapsed;
            DefaultPosition = defaultPosition ?? Vector2.zero;
            CanClose = canClose;
            CaptureCustomState = captureCustomState;
            RestoreCustomState = restoreCustomState;
        }
    }
}
