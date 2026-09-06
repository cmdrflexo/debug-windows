/*
 * Registers temporary pages for testing the generic tabbed debug-window framework.
 */

using System;
using jcan.DebugWindows;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class DebugTabbedWindowTest : MonoBehaviour
    {
        private const string WindowId = "jcan.celestialsystems.tabbed-window-test";

        private DebugTabbedWindow window;
        private bool applicationIsQuitting;
        private string displayName = "Example Body";
        private double radiusMeters = 6371000.0;
        private double massKilograms = 5.972e24;
        private bool oceansEnabled = true;

        private void OnEnable()
        {
            applicationIsQuitting = false;
            window = new DebugTabbedWindow(
                WindowId,
                "Tabbed Window Test",
                new[]
                {
                    new DebugTabbedPage("definition", "Definition", BuildDefinition),
                    new DebugTabbedPage("physical", "Physical", BuildPhysical),
                    new DebugTabbedPage("surface", "Surface", BuildPlaceholder),
                    new DebugTabbedPage("ocean", "Ocean", BuildOcean),
                    new DebugTabbedPage("motion", "Motion / Orbit", BuildPlaceholder),
                    new DebugTabbedPage("spawn", "Spawn", BuildPlaceholder)
                },
                BuildFooter,
                new Vector2(440.0f, 320.0f),
                DebugWindowDisplayState.Open,
                new Vector2(12.0f, -220.0f));

            DebugWindowRegistry.Register(window.CreateRegistration());
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

        private void BuildDefinition(DebugWindowFormContent content)
        {
            content.AddTextField(
                "Display Name",
                displayName,
                value => displayName = value);
            content.AddReadOnly(
                "Definition ID",
                () => "example-body");
        }

        private void BuildPhysical(DebugWindowFormContent content)
        {
            content.AddNumberField(
                "Radius",
                radiusMeters,
                value => radiusMeters = value,
                "m");
            content.AddNumberField(
                "Mass",
                massKilograms,
                value => massKilograms = value,
                "kg");
            content.AddReadOnly(
                "Surface Gravity",
                () => radiusMeters > 0.0
                    ? (6.67430e-11 * massKilograms /
                        (radiusMeters * radiusMeters)).ToString("N3") + " m/s²"
                    : "--");
        }

        private void BuildOcean(DebugWindowFormContent content)
        {
            content.AddToggle(
                "Ocean Enabled",
                oceansEnabled,
                value => oceansEnabled = value);
        }

        private void BuildPlaceholder(DebugWindowFormContent content)
        {
            for (var i = 1; i <= 16; i++)
            {
                var row = i;
                content.AddReadOnly(
                    $"Example Field {row}",
                    () => $"Value {row}");
            }
        }

        private void BuildFooter(DebugWindowFormContent content)
        {
            content.AddButton(
                "save",
                "Save",
                () => Debug.Log(
                    $"Tabbed test saved '{displayName}'.",
                    this));
        }
    }
}
