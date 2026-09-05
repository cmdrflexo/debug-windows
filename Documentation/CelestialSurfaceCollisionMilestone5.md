# Milestone 5: local collision and surface queries

The factory now creates a `CelestialSurfaceCollisionRuntime` alongside its adaptive renderer. It requests local patches from the same MapMagic cache and builds stable physics meshes at a separately configured resolution. Existing scene wiring, GE motion, legacy surface components, graph assets, and surface shaders are retained.

## What is generated

- One collision cache client per body, sharing the renderer's surface variant and generation budget. For one factory body the cache normally reports **2 clients / 1 surface**.
- A bounded footprint around the observer camera, plus any `CelestialSurfaceCollisionObserver` components on gameplay actors.
- Uniform-level collider patches throughout that footprint, including cube-face edges and corners. They use the cached terrain triangles without render skirts or morphing.
- A scene-root `Celestial Collision (<instance ID>)` hierarchy owned and destroyed by the body's collision runtime. Its kinematic rigidbodies receive GE-derived positions and rotations in `FixedUpdate`; keeping this hierarchy separate prevents visual `LateUpdate` transforms from moving physics twice.
- Nonblocking surface queries with elevation, radial altitude, geometric normal, slope, resolved LOD, and cache revision.

Visible ancestors of required collision patches request refinement, subject to the existing renderer's visibility and patch budgets. Rendering may refine beyond collision resolution. Aggressive sub-sample height noise can therefore produce visual detail that the collider cannot represent; reduce collision sample spacing if gameplay needs those features. Query results describe the selected cached triangle surface, not the temporary morphed visual surface or an exact evaluation of an unsampled graph.

## Default quality settings

These fields are under **Unified Collision** in `RoundMapMagicSurfaceQualityProfile`.

| Setting | Default |
|---|---:|
| Enabled / follows camera | true / true |
| Required coverage radius | 256 m |
| Additional prefetch margin | 128 m |
| Target sample spacing | 8 m |
| Activation distance above or below sampled terrain | 2,500 m |
| Maximum requested patches | 128 |
| Maximum collider mesh builds per render frame | 2 |
| Retirement delay | 1 s |
| Unity collision layer | 0 (Default) |

The selected level is capped by the existing collision maximum LOD. Spacing is approximate because cube-sphere distortion varies across a face. Required coverage takes precedence over prefetch; overflow sets `BudgetExceeded` and keeps readiness false. Up to twice the requested patch count may remain resident during retirement, plus a pool capped at the requested count. Collider cooking happens on the main thread within the mesh-build budget; MapMagic evaluation remains asynchronous under the shared manager.

Existing quality assets acquire the field defaults without a scene migration. Runtime inspector adjustments affect the current generated body; edit the quality asset for subsequent spawns.

## One consolidated Play Mode test

1. Compile, keep the original GE test body enabled, and use the existing factory spawn setup in the same scene. The factory adds the collision component automatically.
2. Paste the **Milestone 5 collision display** from `Assets/CelestialSystems/Debug/CelestialDebugTextController-Guide.md` into the existing Debug Text Controller. It uses the existing `factory` source alias.
3. At orbit, confirm `Geometry test: True`, no collision error, and the shared cache reports 2 clients / 1 surface. Collision readiness can be false at orbit.
4. Approach within 2,500 m of the terrain. Wait for `Required` to become nonzero, `Pending` to reach zero, and `Collision ready` to become true. `Query exact` should be true once the collision-level sample at the camera is cached.
5. Near terrain, check `Collider probe: True`. The query/collider error should be small relative to the 8 m grid; investigate a sustained error above roughly 0.05 m near the floating origin. This diagnostic compares the query against the collider's current physics pose, avoiding misleading errors from GE motion between render frames.
6. Select the generated Surface object and use **Drop Test Sphere** in the `CelestialSurfaceCollisionRuntime` component's context menu. A 2 m sphere appears about 5 m above sampled terrain, preferably in front of the camera. It starts with the body's measured point velocity, uses radial test gravity, and requests its own collision coverage. Check that it lands and `Drop touching terrain` becomes true. On steep terrain it may roll.
7. Move across patch and cube-face boundaries, then trigger an ordinary floating-origin shift. Look for collision discontinuities, unexpected velocity kicks, and rising query/collider error. The sphere can retain its own coverage while the camera moves away.
8. Disable/re-enable the collision component, then destroy/recreate the factory body. Readiness should clear during shutdown and recover after generation. The owned collision hierarchy and test sphere should disappear with the body. A graph/cache invalidation must clear stale colliders before new coverage becomes ready.

The drop sphere replaces the previous test sphere and expires after three minutes. It is a development probe, not a player movement or gravity system. This milestone does not automatically make the free-flight camera land on or move with a planet. Use the existing camera collision option if appropriate, and use a physical actor for the landing test. A moving GE planet can still overtake an unconstrained camera.

## Gameplay observers

Add `CelestialSurfaceCollisionObserver` to an actor that needs coverage independently of the camera. Set `Body` to the factory's runtime context, or call `observer.SetBody(body)`. A zero coverage radius uses the body's quality setting. A null body can request nearby bodies in the same scene; an optional universe-frame reference restricts it further. Observers beyond the activation altitude do not request collision patches.

`CollisionRuntime.HasCoverageFor(observer)` checks the actor's entire requested footprint. `SurfaceRuntime.HasCollisionSurface(scenePosition)` checks the enabled collider patch under one direction; it does not imply coverage for an entire vehicle or movement path. Body-level `HasCollisionSurface` indicates all currently required footprints are ready. Callers should gate a landing or teleport on the appropriate query.

## Cached query API

```csharp
var surface = factory.LastSpawnedBody.SurfaceRuntime;
var level = surface.CollisionRuntime.CollisionLevel;

// Read immediately. No graph generation is started by a read.
if (surface.TrySampleScene(actor.transform.position, level, out var sample))
{
    // Inspect IsRequestedDetail before treating a coarser fallback as precise.
    var altitude = sample.AltitudeMeters;
    var slope = sample.SlopeDegrees;
    var bodyNormal = sample.BodyNormal;
    var universeSurfacePoint = sample.UniversePosition;
}

// Request a missing sample explicitly; renew while it is still wanted.
if (surface.TrySceneToBodyLocal(actor.transform.position, out var local))
    surface.RequestSurfaceSample(local, level);

// Require that level instead of accepting a cached ancestor.
bool exact = surface.TrySampleBodyLocal(local, level, out var precise, allowCoarser: false);
```

`TrySampleUniverse` accepts a persistent `UniversePosition`. `TrySampleBodyLocal` accepts meters relative to the body before rotation. A successful sample includes its actual `ResolvedLevel` and `CacheVersion`; a missing result returns false. Explicit requests expire after one second without renewal, are limited to 256 per body, and request data only, not colliders. APIs use Unity/main-thread state and are intended for the main thread.

The radial query intersects the same two triangles used for a collision grid cell. It does not bilinearly interpolate height, which would disagree with the mesh on steep detail. Body-relative double precision is retained until patch-local vertices and nearby physics positions are converted to floats. Universe cell differences are subtracted before conversion to double, preserving nearby positions beyond the exact integer range of doubles.

## Validation and limits

`python Tools/validate_surface_collision.py` checks an independent numerical model: 4,704 known triangle points across six faces at LOD 0, 8, 16, and 20 on two body sizes; corner footprint coverage and bounded overflow; and patch-local float precision with origin rebasing.

The generated collision component also runs `CelestialSurfaceCollisionDiagnostics` against the production C# geometry code at initialization. This tests triangle queries, all eight cube corners, and large universe-cell subtraction. **Validate Collision Geometry** reruns it from the component's context menu. It uses synthetic heights and does not regenerate the graph. Its result is separate from the live PhysX raycast probe.

These numeric checks do not replace Unity compilation and Play Mode validation. GE motion, real graph generation, collider cooking, contacts, and repeated origin shifts must be checked in the scene. The current implementation provides localized terrain collision and sample APIs; gameplay character movement, velocity inheritance after landing, and a general gravity model remain actor responsibilities.

Unity API references: [kinematic Rigidbody motion](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Rigidbody.MovePosition.html) and [mesh baking](https://docs.unity3d.com/6000.4/Documentation/ScriptReference/Physics.BakeMesh.html).
