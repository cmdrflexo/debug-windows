# JCan Debug Windows

A portable runtime uGUI framework for movable debug windows. It depends only on Unity UI and TextMeshPro; input handling and project-specific diagnostics stay outside the package.

## Scene setup

1. Create or choose a screen-space Canvas with a `GraphicRaycaster`.
2. Add a full-stretch `RectTransform` for generated windows.
3. Add `DebugWindowManager` to another object under the Canvas and assign the windows root.
4. Keep the script that enables and disables the Canvas outside the Canvas hierarchy.
5. Ensure the scene has an EventSystem suitable for the project's input system.

The manager creates the mandatory **Debug Windows** window and the example **FPS** window. The core window can be open or collapsed, but cannot be closed.

## Persistence

The manager writes `jcan-debug-windows.json` beneath `Application.persistentDataPath`. The Inspector shows the resolved **Layout File Path** at runtime. Each registered window saves:

- stable unique ID;
- open, collapsed, or closed state;
- Canvas-relative position.

The layout is saved after a drag or state change, when the debug Canvas is disabled, and when the application quits. Loading ignores unknown IDs. The first subsequent save rewrites only currently registered windows, removing stale records. Restored positions are clamped into the current Canvas bounds.

The layout is local to that Unity project/application identity and is intentionally not a project asset or source-controlled file.

## Registering another window

Register through the static registry so the window is available even if the debug Canvas is disabled when the provider starts:

```csharp
/*
 * Registers an example project-specific debug window with the portable debug-window package.
 */

using jcan.DebugWindows;
using UnityEngine;

public sealed class ExampleDebugWindowProvider : MonoBehaviour
{
    private const string WindowId = "my.project.example";

    private void OnEnable()
    {
        DebugWindowRegistry.Register(new DebugWindowRegistration(
            WindowId,
            "Example",
            content =>
            {
                content.AddText("Static text");
                content.AddUpdatingText(
                    () => $"Time: {Time.unscaledTime:N2}",
                    0.1f);
            }));
    }

    private void OnDisable()
    {
        DebugWindowRegistry.Unregister(WindowId);
    }
}
```

IDs are case-sensitive and must remain stable between code revisions to retain saved state. A duplicate ID is rejected. `Unregister` removes the live window and its saved state on the same save.

`DebugWindowContent.Root` is available when a window needs custom uGUI controls beyond `AddText` and `AddUpdatingText`. Use the manager's configured `TextSize` for consistent typography.

## Milestone 1 check

1. Enter Play Mode and show the debug Canvas.
2. Move the core and FPS windows.
3. Collapse the core window; close the FPS window from its title bar.
4. Reopen FPS using the core checkbox and move it again.
5. Exit and re-enter Play Mode. Confirm positions and states return.
6. Close Unity, reopen the project, and enter Play Mode. Confirm they return again.
7. Resize the Game view and confirm restored windows remain reachable.
8. Check the Console and the manager's **Last Persistence Error**. Both should be clear.

To verify stale cleanup, stop Play Mode, temporarily change the `FpsWindowId` constant, then run once. The old ID should disappear from the JSON after startup. Restore the constant afterward; do not commit that temporary edit.
