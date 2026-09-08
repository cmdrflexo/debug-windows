/*
 * Routes the project's existing debug input action into the portable debug-window menu.
 */

using jcan.DebugWindows;
using UnityEngine;

public sealed class DebugWindowToggleInput : BasicActionInput
{
    [SerializeField]
    private DebugWindowManager windowManager;

    protected override void OnToggle()
    {
        if (windowManager == null)
        {
            Debug.LogWarning("DebugWindowToggleInput requires a DebugWindowManager.", this);
            return;
        }

        windowManager.ToggleMenuVisibility();
    }
}
