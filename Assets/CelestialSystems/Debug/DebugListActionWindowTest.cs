/*
 * Registers temporary sample data for visually testing the generic list-action debug window.
 */

using System.Collections.Generic;
using jcan.DebugWindows;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class DebugListActionWindowTest : MonoBehaviour
    {
        private const string WindowId = "jcan.celestialsystems.list-action-test";

        private DebugListActionWindow window;
        private bool applicationIsQuitting;

        private void OnEnable()
        {
            applicationIsQuitting = false;
            window = new DebugListActionWindow(
                WindowId,
                "List Action Test",
                new[]
                {
                    new DebugListAction("load", "Load"),
                    new DebugListAction("delete", "Delete")
                },
                new DebugListAction("save", "Save"),
                new Vector2(360.0f, 260.0f),
                DebugWindowDisplayState.Open,
                new Vector2(450.0f, -12.0f));

            var items = new List<DebugListItem>();
            for (var i = 1; i <= 30; i++)
            {
                items.Add(new DebugListItem(
                    $"sample-{i}",
                    $"Sample Pose {i}"));
            }

            window.SetItems(items);
            window.ActionInvoked += HandleAction;
            DebugWindowRegistry.Register(window.CreateRegistration());
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDisable()
        {
            if (window != null)
                window.ActionInvoked -= HandleAction;

            if (!applicationIsQuitting)
                DebugWindowRegistry.Unregister(WindowId);

            window = null;
        }

        private void HandleAction(DebugListActionInvocation invocation)
        {
            Debug.Log(
                $"List action '{invocation.ActionId}', selected " +
                $"'{invocation.SelectedItem?.DisplayName ?? "--"}', text " +
                $"'{invocation.InputText}', items {invocation.Items.Count}.",
                this);
        }
    }
}
