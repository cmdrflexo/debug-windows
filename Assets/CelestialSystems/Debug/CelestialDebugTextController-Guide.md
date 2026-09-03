# Celestial Debug Text Controller

`CelestialDebugTextController` builds a live TextMeshPro debug display from a pasted text recipe. A recipe can combine ordinary text, TextMeshPro rich-text tags, and values read from explicitly assigned Unity objects.

The controller is intended for runtime diagnostics while developing Celestial Systems. It does not execute code or call methods.

## Setup

1. Add `CelestialDebugTextController` to the GameObject used as the Debug Controller.
2. Assign the TextMeshPro component that should receive the output to **Target Text**.
3. Set **Refresh Interval Seconds**. The default `0.1` updates the display ten times per second.
4. Expand **Sources** and add every component or asset the recipe should be allowed to read.
5. Give each source a short alias and assign its Unity object.
6. Paste a recipe into **Display Code**.

Example source list:

| Alias | Source object |
| --- | --- |
| `surface` | `GePlanetSurfaceFrame` component |
| `tiles` | `CubeSphereTerrainAddressTracker` component |
| `session` | `RoundMapMagicSurfaceSession` component |
| `frame` | `UniverseFrameController` component |
| `body` | `CelestialBodyRuntimeContext` component |
| `definition` | `CelestialBodyDefinition` asset |

Aliases are case-insensitive. Member names are case-sensitive and must match their C# names.

Only one script should write to a particular TMP text component. Disable `GePlanetSurfaceAltitudeDebugText` if it targets the same text.

## Recipe syntax

A value token has this form:

```text
{alias.Member.Submember:format|fallback}
```

Only the source alias and at least one member are required:

```text
Altitude: {surface.AnchorAltitudeMeters}
```

### Property paths

Start with a configured source alias and follow it with one or more public fields or properties separated by periods.

```text
Body: {body.InstanceId}
Face: {surface.AnchorAddress.Face}
Tile U: {tiles.PrimaryTileAddress.TileU}
Frame cell X: {frame.FrameOrigin.CellX}
```

Nested value types such as `CubeSphereAddress`, `CubeSphereTileAddress`, `CubeSphereFaceProximity`, and `UniversePosition` can be traversed normally.

The controller can read:

- Public instance properties with getters
- Public instance fields
- Nested public fields and properties
- Components, GameObjects, ScriptableObjects, and other `UnityEngine.Object` sources assigned in the Inspector

The controller cannot read private or protected members, access collections by index, call methods, assign values, or execute arbitrary code.

### Numeric formats

Put a standard or custom C# format after a colon:

```text
Altitude: {surface.AnchorAltitudeMeters:N1} m
Face U: {surface.AnchorAddress.FaceU:N5}
Radius: {surface.PlanetRadiusMeters:N0} m
```

Common formats:

| Format | Example result | Use |
| --- | --- | --- |
| `N0` | `6,371,000` | Whole number with separators |
| `N1` | `1,234.5` | One decimal place with separators |
| `N3` | `1,234.568` | Three decimal places with separators |
| `F2` | `1234.57` | Fixed two decimal places |
| `E3` | `1.235E+006` | Scientific notation |
| `0.000` | `12.346` | Custom fixed precision |

Formatting uses the invariant culture, so decimal points and grouping remain consistent between machines.

Values that do not support the requested format fall back to their normal `ToString()` representation when possible. If formatting throws an error, the token prints its fallback.

### Fallback text

Put fallback text after a vertical bar:

```text
Altitude: {surface.AnchorAltitudeMeters:N1|--} m
Body: {session.ActiveBodyContext.InstanceId|No active body}
```

The fallback is printed when:

- The source is unassigned or has been destroyed
- A value in the property path is `null`
- The alias or member path is invalid
- A property getter throws an exception
- The requested value cannot be formatted successfully

If no fallback is supplied, the default fallback is `--`.

A Boolean value such as `False` is a valid value, not a missing value, so it prints normally rather than using the fallback.

### Decoration and rich text

Everything outside braces is copied directly into the output. This includes labels, units, blank lines, punctuation, and supported TextMeshPro rich-text tags.

```text
<b><color=#7FDBFF>CELESTIAL SURFACE</color></b>
Altitude: {surface.AnchorAltitudeMeters:N1|--} m
Session: <color=#FFD166>{session.HasActiveSession}</color>
```

Whether a rich-text tag is displayed or interpreted depends on the target TMP component's rich-text settings.

### Conditional colors

A conditional color token chooses a TextMeshPro color from a live value and emits an opening `<color>` tag:

```text
{color:condition|true color|false color|unavailable color}
```

Close the colored section with a normal TextMeshPro `</color>` tag:

```text
{color:surface.AnchorAltitudeMeters < 0|#FF6060|#60E880|#AAAAAA}
Altitude: {surface.AnchorAltitudeMeters:N1|--} m
</color>
```

This displays the entire altitude line in red below zero altitude, green at or above zero, and gray when the altitude cannot be read.

The unavailable color is optional and defaults to `#AAAAAA`:

```text
{color:surface.AnchorAltitudeMeters < 0|red|green}Altitude: {surface.AnchorAltitudeMeters:N1|--} m</color>
```

A Boolean property can be used directly:

```text
{color:session.HasActiveSession|#60E880|#FF6060|#AAAAAA}
Session active: {session.HasActiveSession|--}
</color>
```

Supported comparison operators are:

| Operator | Meaning |
| --- | --- |
| `<` | Less than |
| `<=` | Less than or equal |
| `>` | Greater than |
| `>=` | Greater than or equal |
| `==` | Equal |
| `!=` | Not equal |

Ordered comparisons use numeric values. Equality comparisons also work with Booleans, strings, and enums, ignoring letter case:

```text
{color:surface.AnchorAddress.Face == PositiveY|yellow|white|gray}
Face: {surface.AnchorAddress.Face|--}
</color>
```

The selected color can cover one value, a whole line, or multiple lines. Conditional color tokens do not automatically insert `</color>`, allowing the recipe to choose the extent of the colored section.

If the condition path is invalid, its source is unavailable, a direct condition is not Boolean, or an ordered comparison is not numeric, the unavailable color is used. **Last Configuration Error** reports syntax and path errors; runtime type or value failures simply select the unavailable color.

### Literal braces

Double braces print a literal brace instead of starting or ending a token:

```text
{{debug}} becomes {debug}
```

Use `{{` for `{` and `}}` for `}`.

## Complete Celestial recipe

This recipe expects the aliases `surface`, `tiles`, `session`, `frame`, and `body` from the setup table.

```text
<b><color=#7FDBFF>CELESTIAL SURFACE</color></b>
Body: {body.InstanceId|--}
Session: {session.HasActiveSession}
Frame ready: {frame.FrameOriginInitialized}

<b>Surface Address</b>
Altitude: {surface.AnchorAltitudeMeters:N1|--} m
Face: {surface.AnchorAddress.Face|--}
Face UV: ({surface.AnchorAddress.FaceU:N5|--}, {surface.AnchorAddress.FaceV:N5|--})
Inside face: {surface.AnchorAddress.IsInsideFace|--}

<b>Terrain Tile</b>
Tile: ({tiles.PrimaryTileAddress.TileU|--}, {tiles.PrimaryTileAddress.TileV|--})
Local UV: ({tiles.PrimaryTileAddress.LocalUMeters:N1|--}, {tiles.PrimaryTileAddress.LocalVMeters:N1|--}) m
Tile size: {tiles.TileSizeMeters:N0|--} m

<b>Nearest Face Edges</b>
U: {surface.AnchorFaceProximity.ClosestUEdge|--} — {surface.AnchorFaceProximity.ClosestUEdgeMeters:N1|--} m
V: {surface.AnchorFaceProximity.ClosestVEdge|--} — {surface.AnchorFaceProximity.ClosestVEdgeMeters:N1|--} m
Overall: {surface.AnchorFaceProximity.ClosestEdge|--} — {surface.AnchorFaceProximity.ClosestEdgeMeters:N1|--} m

Adjacent U loaded: {tiles.HasUAdjacentTileAddress}
Adjacent V loaded: {tiles.HasVAdjacentTileAddress}
```

## Additional recipe examples

### Body definition

Assign a `CelestialBodyDefinition` asset with the alias `definition`:

```text
<b>{definition.name}</b>
Radius: {definition.ReferenceRadiusMeters:N0} m
Mass: {definition.MassKilograms:E3} kg
Surface: {definition.ResolvedSurfaceSystem}
Valid physics: {definition.HasValidPhysicalSettings}
Valid surface: {definition.HasValidResolvedSurfaceSettings}
```

`name` is inherited from `UnityEngine.Object` and is therefore available as a public property.

### Universe frame origin

```text
<b>Universe Frame</b>
Initialized: {frame.FrameOriginInitialized}
Cell: ({frame.FrameOrigin.CellX}, {frame.FrameOrigin.CellY}, {frame.FrameOrigin.CellZ})
Local: ({frame.FrameOrigin.LocalXMeters:N1}, {frame.FrameOrigin.LocalYMeters:N1}, {frame.FrameOrigin.LocalZMeters:N1}) m
Anchor: {frame.ActiveAnchorSource.name|None}
```

### Compact surface display

```text
{body.InstanceId|--} | {surface.AnchorAddress.Face|--}
Alt {surface.AnchorAltitudeMeters:N1|--} m
Tile {tiles.PrimaryTileAddress.TileU|--}, {tiles.PrimaryTileAddress.TileV|--}
```

## Runtime behavior and diagnostics

The recipe is parsed when the component starts and whenever its text or source assignments change. Member paths are resolved and cached at that time. Values are then read at the configured refresh interval.

**Last Configuration Error** shows the first problem found while compiling the current recipe. It should remain empty for a valid configuration. Invalid tokens use their fallback, allowing the rest of the display to continue updating.

Changing the recipe through another script is also supported:

```csharp
debugTextController.SetDisplayCode(
    "Altitude: {surface.AnchorAltitudeMeters:N1|--} m");
```

## Current limitations

- Comparisons are currently available only for conditional colors; recipes cannot perform general calculations.
- Recipes do not currently contain conditional text sections or custom true/false labels.
- Collection indexing and method calls are intentionally unsupported.
- Values are refreshed by polling rather than events.
- A source must be assigned explicitly before its data is available.
- Reflection is used to read public members. Paths are cached, but very large displays or a zero-second refresh interval should still be avoided.

These constraints keep the tool predictable and prevent pasted recipes from modifying game state.
