/*
 * Provides a code-defined slider field with value display, optional units, and stepped values.
 */

using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace jcan.DebugWindows
{
    public sealed class DebugSliderField : MonoBehaviour
    {
        private Slider slider;
        private TMP_Text valueLabel;
        private Action<float> changed;
        private float step;
        private float currentValue;
        private string units;

        internal static DebugSliderField Create(
            DebugWindowManager manager,
            RectTransform parent,
            float value,
            float minimum,
            float maximum,
            Action<float> changed,
            string units,
            float step)
        {
            var root = DebugWindowUi.CreateRect("Slider Field", parent);
            var rootSize = root.gameObject.AddComponent<LayoutElement>();
            rootSize.preferredWidth = manager.PreferredFieldWidth;
            rootSize.preferredHeight =
                manager.TextSize +
                manager.ControlVerticalPadding * 2.0f;
            rootSize.flexibleWidth = 1.0f;

            var rootLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            rootLayout.spacing = manager.Spacing;
            rootLayout.childAlignment = TextAnchor.MiddleLeft;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = false;
            rootLayout.childForceExpandHeight = false;

            var sliderRoot = DebugWindowUi.CreateRect("Slider", root);
            var sliderSize = sliderRoot.gameObject.AddComponent<LayoutElement>();
            sliderSize.minWidth = 72.0f;
            sliderSize.preferredWidth = Mathf.Max(
                96.0f,
                manager.PreferredFieldWidth -
                manager.TextSize * 4.5f -
                manager.Spacing);
            sliderSize.preferredHeight = manager.TextSize;
            sliderSize.flexibleWidth = 1.0f;

            var track = DebugWindowUi.CreateRect("Track", sliderRoot);
            track.anchorMin = new Vector2(0.0f, 0.5f);
            track.anchorMax = new Vector2(1.0f, 0.5f);
            track.offsetMin = new Vector2(4.0f, -3.0f);
            track.offsetMax = new Vector2(-4.0f, 3.0f);
            DebugWindowUi.AddImage(track.gameObject, manager.ButtonColor);

            var fillArea = DebugWindowUi.CreateRect("Fill Area", sliderRoot);
            DebugWindowUi.Stretch(fillArea);
            fillArea.offsetMin = new Vector2(4.0f, 0.0f);
            fillArea.offsetMax = new Vector2(-4.0f, 0.0f);

            var fill = DebugWindowUi.CreateRect("Fill", fillArea);
            DebugWindowUi.Stretch(fill);
            DebugWindowUi.AddImage(fill.gameObject, manager.AccentColor);

            var handleArea = DebugWindowUi.CreateRect("Handle Slide Area", sliderRoot);
            DebugWindowUi.Stretch(handleArea);
            handleArea.offsetMin = new Vector2(7.0f, 0.0f);
            handleArea.offsetMax = new Vector2(-7.0f, 0.0f);

            var handle = DebugWindowUi.CreateRect("Handle", handleArea);
            handle.anchorMin = new Vector2(0.0f, 0.5f);
            handle.anchorMax = new Vector2(0.0f, 0.5f);
            handle.sizeDelta = new Vector2(14.0f, 14.0f);
            var handleImage = DebugWindowUi.AddImage(
                handle.gameObject,
                manager.AccentColor);

            var field = root.gameObject.AddComponent<DebugSliderField>();
            field.changed = changed;
            field.step = Mathf.Max(0.0f, step);
            field.units = units?.Trim() ?? string.Empty;

            field.slider = sliderRoot.gameObject.AddComponent<Slider>();
            field.slider.fillRect = fill;
            field.slider.handleRect = handle;
            field.slider.targetGraphic = handleImage;
            field.slider.direction = Slider.Direction.LeftToRight;
            field.slider.minValue = Mathf.Min(minimum, maximum);
            field.slider.maxValue = Mathf.Max(minimum, maximum);
            field.slider.wholeNumbers = false;

            field.valueLabel = DebugWindowUi.CreateText(
                "Value",
                root,
                string.Empty,
                manager.TextSize,
                manager.TextColor,
                TextAlignmentOptions.MidlineRight);
            var valueSize = field.valueLabel.gameObject.AddComponent<LayoutElement>();
            valueSize.preferredWidth = manager.TextSize * 4.5f;
            valueSize.flexibleWidth = 0.0f;

            field.slider.onValueChanged.AddListener(field.HandleSliderChanged);
            field.SetValue(value, false);
            return field;
        }

        public float Value => slider != null ? slider.value : 0.0f;

        public void SetValue(float value, bool notify = false)
        {
            if (slider == null)
                return;

            var adjusted = ClampAndSnap(value);
            var valueChanged = !Mathf.Approximately(currentValue, adjusted);
            currentValue = adjusted;
            slider.SetValueWithoutNotify(adjusted);
            RefreshValueLabel(adjusted);

            if (notify && valueChanged)
                changed?.Invoke(adjusted);
        }

        private void HandleSliderChanged(float value)
        {
            var adjusted = ClampAndSnap(value);
            var valueChanged = !Mathf.Approximately(currentValue, adjusted);
            currentValue = adjusted;

            if (!Mathf.Approximately(slider.value, adjusted))
                slider.SetValueWithoutNotify(adjusted);

            RefreshValueLabel(adjusted);
            if (valueChanged)
                changed?.Invoke(adjusted);
        }

        private float ClampAndSnap(float value)
        {
            var clamped = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            if (step <= Mathf.Epsilon)
                return clamped;

            var steps = Mathf.Round((clamped - slider.minValue) / step);
            return Mathf.Clamp(
                slider.minValue + steps * step,
                slider.minValue,
                slider.maxValue);
        }

        private void RefreshValueLabel(float value)
        {
            if (valueLabel == null)
                return;

            var formatted = value.ToString(
                "0.###",
                CultureInfo.InvariantCulture);
            valueLabel.text = string.IsNullOrEmpty(units)
                ? formatted
                : formatted + " " + units;
        }
    }
}
