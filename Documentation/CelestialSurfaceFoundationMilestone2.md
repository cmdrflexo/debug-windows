# Unified Celestial Surface Foundation — Milestone 2

Milestone 2 defines the common address, sampling, data, and policy model that future Far, Mid, and Local replacement renderers will share. It does not disable or replace the current `Planet Surface Frame` components.

## MapMagic remains the authoring system

Body surface features continue to be designed in MapMagic graphs referenced by `RoundMapMagicSurfaceDefinition`. The new surface foundation does not introduce a second procedural terrain language. It gives all LOD levels one stable way to request rectangular portions of that graph on the spherical body.

`RoundMapMagicSurfacePatchRequest` converts a quadtree patch into the same map-world coordinate convention already used by `RoundMapMagicSphericalFaceMapCache` and `RoundMapMagicVirtualHeightSampler`:

- MapMagic X increases with cube-face U.
- MapMagic Z increases opposite cube-face V.
- The same face orientation and equiangular projection remain in use.
- The body radius, surface seed, and elevation datum come from the existing body and surface definitions.

## Canonical patch address

`CubeSpherePatchAddress` identifies every patch with:

- cube face
- quadtree level
- X coordinate at that level
- Y coordinate at that level

Level 0 contains six root patches, one per face. Every valid parent can resolve four children and every non-root child can resolve its parent. Address equality and hashing are stable, so the type can serve as a cache and scheduler key.

## Nested sample grid

`CubeSpherePatchGrid` accepts resolutions with a power-of-two interval count, such as 17, 33, 65, 129, or 257. A parent sample maps exactly onto a child sample. Shared patch edges therefore request the same planetary direction instead of independently approximating it.

The grid supplies:

- normalized face coordinates
- planet-relative unit directions
- planet-relative positions at a requested elevation
- persistent `UniversePosition` values using a body center and rotation
- approximate patch arc size
- parent-to-child shared-sample mapping

## Sample data is not a mesh

`CelestialSurfacePatchData` is immutable sampled data. It owns:

- cache key and patch address
- nested resolution
- elevation samples in meters relative to the body datum
- optional surface-layer control weights
- minimum and maximum elevation
- geometric error measured against the next coarser grid

It contains no `Mesh`, `MeshFilter`, `MeshRenderer`, or collider. Later render and collision builders can consume the same cached data independently.

## Cache identity

`CelestialSurfaceCacheKey` includes:

- cache format version
- body definition ID and generation seed
- reference radius
- MapMagic surface seed
- elevation offset
- a hash of the serialized graph and settings

Changing the graph, datum, seed, radius, or cache format produces a different key. Milestone 4 will use this identity for memory and optional persistent caches.

## Observer and LOD policy

`CelestialSurfaceObserverState` is the common request description for cameras and gameplay/physics observers. Render and collision requests can therefore refine independently while addressing the same patches.

`CelestialSurfaceLodPolicy` stores the nested patch resolution, permitted quadtree levels, screen-error threshold, hysteresis, collision limit, and the one-level neighbor rule. The existing `RoundMapMagicSurfaceQualityProfile` now owns these adaptive settings while retaining all current Local, Mid, and transition fields for the legacy system.

If no quality profile is supplied, the packaged runtime uses a 33×33 grid and levels 0–20.

## Packaged runtime integration

The factory adds one `CelestialSurfaceRuntime` to the generated `Visuals/Surface` child. It resolves the body definition, MapMagic surface definition, cache key, LOD policy, and six roots. This sets `SurfaceFoundation` readiness without claiming any generated terrain is ready.

The `Development` child receives `CelestialSurfaceFoundationDiagnostics`. It runs a read-only startup validation covering:

- root and level-3 parent/child shared samples
- all face edges and corners
- MapMagic versus canonical patch coordinates
- deterministic spherical-noise values at shared positions
- universe-position datum conversion
- patch range and geometric-error metadata

The diagnostic does not modify the MapMagic graph or the legacy surface stack.

## Same-scene checkpoint

1. Keep the original GE planet and `Planet Surface Frame` enabled.
2. Pull the milestone branch and allow Unity to compile.
3. Enter Play Mode and let the factory body spawn.
4. Use the updated **Body runtime package** Debug Controller recipe.
5. Confirm `Surface foundation` and `Foundation test` are true.
6. Confirm `Root patches` is 6 and `MapMagic coordinates` is true.
7. Confirm `Foundation error` is empty and the Console has no new errors.
8. Confirm the existing surface and ocean still render as before.

Coarse, visible, collision, and ocean readiness are expected to remain false. Milestone 3 will begin producing visible adaptive patches while the old renderer stays available for comparison.
