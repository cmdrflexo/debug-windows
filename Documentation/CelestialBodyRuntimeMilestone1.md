# Celestial Body Runtime Package — Milestone 1

Milestone 1 establishes the public body package used by future adaptive surfaces without replacing the current `Planet Surface Frame` implementation.

## Runtime ownership

`CelestialBodyFactory` creates one body root from a `CelestialBodyDefinition` and a `CelestialBodySpawnRequest`. The existing `CelestialBodyRuntimeContext` is retained as the public facade so current sessions and selectors continue to work.

The generated runtime hierarchy is:

```text
definition-id (instance-id)
├─ Motion
├─ Visuals
│  ├─ Surface
│  └─ Ocean
└─ Development
```

The root retains the `NBody` and `CelestialBodyRuntimeContext` components from the existing prefab. The `Motion` child receives a `GravityEngineCelestialBodyMotionProvider` that translates the Gravity Engine body's current position into the project's persistent `UniversePosition` format.

The empty `Surface` and `Ocean` children are ownership points for later milestones. The current shared Surface Frame continues rendering exactly as before.

## Compatibility

The existing factory fields and `TrySpawnBody(string, definition, position, velocity, out body)` method remain available. They now build a `CelestialBodySpawnRequest` internally.

`CelestialBodyRuntimeContext` retains these existing members:

- `InstanceId`
- `Definition`
- `UniverseFrame`
- `GravityBody`
- `VisualRoot`
- `HasValidConfiguration`
- resolved body configuration properties
- `ActiveContexts`

This lets `RoundMapMagicSurfaceSessionSelector`, `GePlanetSurfaceFrame`, and the rest of the current Surface Frame bind factory-created bodies without modification.

## Scene-authored spawning

`CelestialBodySpawner` is the preferred scene-facing authoring component. Assign:

1. The scene's `CelestialBodyFactory`.
2. A `CelestialBodyDefinition`.
3. A unique instance ID.
4. Initial frame-relative position, velocity, and rotation.
5. An optional surface quality profile and parent override.

When **Spawn On Start** is enabled, the spawner waits for Gravity Engine setup before submitting the request. Procedural code can construct `CelestialBodySpawnRequest` and call the same factory method directly.

The startup fields already present on `CelestialBodyFactory` remain supported for gradual scene migration. Do not enable both startup paths for the same instance ID.

## Motion boundary

`ICelestialBodyMotionProvider` is the boundary between a body package and its motion authority. Runtime consumers should prefer `CelestialBodyRuntimeContext.TryGetMotionState` rather than reading Gravity Engine directly.

`GravityEngineCelestialBodyMotionProvider` is the first implementation. The runtime context still exposes `GravityBody` as a compatibility bridge for the current surface system. That direct dependency can be retired only after the legacy Surface Frame no longer needs it.

## Readiness

`CelestialBodyRuntimeContext.Readiness` is a flag set. Milestone 1 supplies:

- `Definition`
- `Registered`
- `RuntimeHierarchy`
- `Motion`

Future subsystems report these independently:

- `CoarseSurface`
- `VisibleSurface`
- `CollisionSurface`
- `Ocean`

`IsReady` currently means definition, registration, runtime hierarchy, and authoritative motion are all available. It does not claim that terrain or ocean data has finished generating.

## Same-scene transition

Keep `Planet Surface Frame` in the current scene. Factory-created bodies continue appearing in `CelestialBodyRuntimeContext.ActiveContexts`, so the existing session selector can choose them.

At each later milestone, the new subsystem will be added under the generated package, compared with the current equivalent, and only then used to replace that responsibility. Legacy components should be disabled before they are removed.

## Initial integration check

1. Pull the branch and allow Unity to recompile.
2. Enter Play Mode with the existing factory startup body enabled.
3. Find the spawned `definition-id (instance-id)` object.
4. Confirm it contains the generated hierarchy shown above.
5. Confirm the `Motion` child has `GravityEngineCelestialBodyMotionProvider`.
6. Confirm the root context reaches `Lifecycle State: Active` and `Is Ready: true` after the universe frame initializes.
7. Confirm the existing Surface Frame continues selecting and rendering bodies normally.
8. Exit Play Mode and confirm there are no errors.

