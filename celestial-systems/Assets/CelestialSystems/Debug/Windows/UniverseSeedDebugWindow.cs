/*
 * Presents a named universe-seed library through the reusable multi-input list-action debug window.
 */

using System.Collections.Generic;
using System.Globalization;
using jcan.DebugWindows;
using TMPro;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UniverseSeedLibrary))]
    public sealed class UniverseSeedDebugWindow : MonoBehaviour
    {
        private const string WindowId =
            "jcan.celestialsystems.universe-seeds";
        private const string SeedInputId = "seed";
        private const string NameInputId = "name";

        [SerializeField]
        private UniverseSeedLibrary seedLibrary;

        [SerializeField]
        private Vector2 preferredContentSize =
            new Vector2(420.0f, 280.0f);

        [SerializeField]
        private Vector2 defaultPosition =
            new Vector2(450.0f, -300.0f);

        private DebugListActionWindow window;
        private bool applicationIsQuitting;

        private void OnEnable()
        {
            applicationIsQuitting = false;
            if (seedLibrary == null)
                seedLibrary = GetComponent<UniverseSeedLibrary>();

            if (seedLibrary == null)
            {
                Debug.LogWarning(
                    "UniverseSeedDebugWindow requires a UniverseSeedLibrary.",
                    this);
                return;
            }

            window = new DebugListActionWindow(
                WindowId,
                "Universe Seeds",
                new[]
                {
                    new DebugListAction("recall", "Recall"),
                    new DebugListAction(
                        "delete",
                        "Delete",
                        "DELETED",
                        3.0f,
                        false)
                },
                new[]
                {
                    new DebugListInput(
                        SeedInputId,
                        "Seed",
                        TMP_InputField.ContentType.IntegerNumber,
                        1.0f),
                    new DebugListInput(
                        NameInputId,
                        "Name",
                        TMP_InputField.ContentType.Standard,
                        1.0f)
                },
                new DebugListAction(
                    "save",
                    "Save",
                    "SAVED",
                    3.0f,
                    false),
                preferredContentSize,
                DebugWindowDisplayState.Closed,
                defaultPosition);

            window.ActionInvoked += HandleAction;
            seedLibrary.EntriesChanged += RefreshItems;
            RefreshItems();

            if (!DebugWindowRegistry.Register(window.CreateRegistration()))
            {
                window.ActionInvoked -= HandleAction;
                seedLibrary.EntriesChanged -= RefreshItems;
                window = null;
            }
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDisable()
        {
            if (window != null)
                window.ActionInvoked -= HandleAction;
            if (seedLibrary != null)
                seedLibrary.EntriesChanged -= RefreshItems;

            if (!applicationIsQuitting)
                DebugWindowRegistry.Unregister(WindowId);

            window = null;
        }

        private void HandleAction(DebugListActionInvocation invocation)
        {
            switch (invocation.ActionId)
            {
                case "save":
                    Save(invocation);
                    break;

                case "recall":
                    Recall(invocation.SelectedItem);
                    break;

                case "delete":
                    ConfirmDelete(invocation.SelectedItem);
                    break;
            }
        }

        private void Save(DebugListActionInvocation invocation)
        {
            var selectedId = invocation.SelectedItem?.UniqueId;
            if (!seedLibrary.TrySave(
                    selectedId,
                    invocation.GetInputText(SeedInputId),
                    invocation.GetInputText(NameInputId),
                    out var saved))
            {
                Debug.LogWarning(seedLibrary.LastError, this);
                return;
            }

            window.SetInputText(
                SeedInputId,
                saved.seed.ToString(CultureInfo.InvariantCulture));
            window.SetInputText(
                NameInputId,
                saved.displayName);
            window.ShowActionFeedback("save");
        }

        private void Recall(DebugListItem selectedItem)
        {
            var saved = seedLibrary.Find(selectedItem?.UniqueId);
            if (saved == null)
                return;

            window.SetInputText(
                SeedInputId,
                saved.seed.ToString(CultureInfo.InvariantCulture));
            window.SetInputText(
                NameInputId,
                saved.displayName);
        }

        private void ConfirmDelete(DebugListItem selectedItem)
        {
            var saved = seedLibrary.Find(selectedItem?.UniqueId);
            var manager = DebugWindowManager.Instance;
            if (saved == null || manager == null)
                return;

            var uniqueId = saved.uniqueId;
            manager.ShowConfirmation(
                $"Delete saved universe seed '{FormatDisplayName(saved)}'?",
                () =>
                {
                    if (seedLibrary.Delete(uniqueId))
                    {
                        window?.SetInputText(SeedInputId, string.Empty);
                        window?.SetInputText(NameInputId, string.Empty);
                        window?.ShowActionFeedback("delete");
                    }
                });
        }

        private void RefreshItems()
        {
            if (window == null || seedLibrary == null)
                return;

            var items = new List<DebugListItem>(
                seedLibrary.Entries.Count);
            for (var i = 0; i < seedLibrary.Entries.Count; i++)
            {
                var entry = seedLibrary.Entries[i];
                if (entry != null)
                {
                    items.Add(
                        new DebugListItem(
                            entry.uniqueId,
                            FormatDisplayName(entry)));
                }
            }

            window.SetItems(items);
        }

        private static string FormatDisplayName(SavedUniverseSeed entry)
        {
            var seed = entry.seed.ToString(
                CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(entry.displayName)
                ? seed
                : entry.displayName + " — " + seed;
        }
    }
}
