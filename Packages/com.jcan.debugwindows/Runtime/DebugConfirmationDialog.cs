/*
 * Creates a centered modal confirmation dialog with confirm, cancel, and backdrop-cancel behavior.
 */

using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    internal sealed class DebugConfirmationDialog : MonoBehaviour
    {
        private static DebugConfirmationDialog current;

        private Action confirmed;
        private Action cancelled;
        private bool closing;

        public static void Show(
            DebugWindowManager manager,
            string message,
            Action confirmed,
            Action cancelled,
            string confirmText,
            string cancelText,
            Vector2 minimumSize)
        {
            if (manager == null || manager.WindowsRoot == null)
                return;

            if (current != null)
                current.Close(false);

            var overlay = DebugWindowUi.CreateRect(
                "Confirmation Dialog",
                manager.WindowsRoot);
            DebugWindowUi.Stretch(overlay);
            overlay.SetAsLastSibling();

            var overlayImage = DebugWindowUi.AddImage(
                overlay.gameObject,
                new Color(0.0f, 0.0f, 0.0f, 0.55f));
            var backdropButton = overlay.gameObject.AddComponent<Button>();
            backdropButton.targetGraphic = overlayImage;
            backdropButton.transition = Selectable.Transition.None;

            var dialog = overlay.gameObject.AddComponent<DebugConfirmationDialog>();
            dialog.confirmed = confirmed;
            dialog.cancelled = cancelled;
            backdropButton.onClick.AddListener(() => dialog.Close(false));

            dialog.Build(
                manager,
                overlay,
                message ?? string.Empty,
                string.IsNullOrWhiteSpace(confirmText) ? "Confirm" : confirmText.Trim(),
                string.IsNullOrWhiteSpace(cancelText) ? "Cancel" : cancelText.Trim(),
                minimumSize);
            current = dialog;
        }

        private void Build(
            DebugWindowManager manager,
            RectTransform overlay,
            string message,
            string confirmText,
            string cancelText,
            Vector2 minimumSize)
        {
            var panel = DebugWindowUi.CreateRect("Panel", overlay);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;

            var panelImage = DebugWindowUi.AddImage(
                panel.gameObject,
                manager.WindowColor);
            if (manager.WindowBackgroundSprite != null)
            {
                panelImage.sprite = manager.WindowBackgroundSprite;
                panelImage.type = Image.Type.Sliced;
            }

            // Consume panel clicks so only the backdrop outside it cancels.
            var panelButton = panel.gameObject.AddComponent<Button>();
            panelButton.targetGraphic = panelImage;
            panelButton.transition = Selectable.Transition.None;
            panelButton.onClick.AddListener(() => { });

            var padding = Mathf.Max(12.0f, manager.Padding * 2.0f);
            var maximumMessageWidth = Mathf.Max(
                220.0f,
                Mathf.Min(520.0f, manager.WindowsRoot.rect.width - padding * 4.0f));

            var messageLabel = DebugWindowUi.CreateText(
                "Message",
                panel,
                message,
                manager.TextSize,
                manager.TextColor,
                TextAlignmentOptions.Center);
            messageLabel.enableWordWrapping = true;
            messageLabel.overflowMode = TextOverflowModes.Overflow;

            var naturalSize = messageLabel.GetPreferredValues(message);
            var messageWidth = Mathf.Clamp(
                naturalSize.x,
                Mathf.Min(220.0f, maximumMessageWidth),
                maximumMessageWidth);
            var wrappedSize = messageLabel.GetPreferredValues(
                message,
                messageWidth,
                0.0f);

            var buttonHeight =
                manager.TextSize + manager.ControlVerticalPadding * 2.0f;
            var buttonWidth = Mathf.Max(
                88.0f,
                manager.MinimumWindowWidth * 0.45f);
            var panelWidth = Mathf.Max(
                minimumSize.x,
                Mathf.Max(
                    messageWidth + padding * 2.0f,
                    buttonWidth * 2.0f + manager.Spacing + padding * 2.0f));
            var panelHeight = Mathf.Max(
                minimumSize.y,
                wrappedSize.y + buttonHeight + manager.Spacing + padding * 3.0f);
            panel.sizeDelta = new Vector2(panelWidth, panelHeight);

            messageLabel.rectTransform.anchorMin = new Vector2(0.0f, 0.0f);
            messageLabel.rectTransform.anchorMax = new Vector2(1.0f, 1.0f);
            messageLabel.rectTransform.offsetMin = new Vector2(
                padding,
                padding * 2.0f + buttonHeight + manager.Spacing);
            messageLabel.rectTransform.offsetMax = new Vector2(
                -padding,
                -padding);

            var buttons = DebugWindowUi.CreateRect("Buttons", panel);
            buttons.anchorMin = new Vector2(0.5f, 0.0f);
            buttons.anchorMax = new Vector2(0.5f, 0.0f);
            buttons.pivot = new Vector2(0.5f, 0.0f);
            buttons.anchoredPosition = new Vector2(0.0f, padding);
            buttons.sizeDelta = new Vector2(
                buttonWidth * 2.0f + manager.Spacing,
                buttonHeight);

            var buttonLayout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = manager.Spacing;
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = true;

            DebugWindowUi.CreateButton(
                "Cancel",
                buttons,
                cancelText,
                manager.TextSize,
                manager.ButtonColor,
                manager.TextColor,
                () => Close(false),
                buttonWidth,
                manager.ControlHorizontalPadding,
                manager.ControlVerticalPadding);
            DebugWindowUi.CreateButton(
                "Confirm",
                buttons,
                confirmText,
                manager.TextSize,
                manager.AccentColor,
                manager.TextColor,
                () => Close(true),
                buttonWidth,
                manager.ControlHorizontalPadding,
                manager.ControlVerticalPadding);
        }

        private void Close(bool wasConfirmed)
        {
            if (closing)
                return;

            closing = true;
            if (current == this)
                current = null;

            var callback = wasConfirmed ? confirmed : cancelled;
            Destroy(gameObject);
            callback?.Invoke();
        }
    }
}
