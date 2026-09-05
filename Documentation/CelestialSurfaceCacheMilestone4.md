# Shared Celestial Surface Cache and LOD Transitions — Milestone 4

Milestone 4 moves adaptive MapMagic generation out of each body renderer and into one world-level service. It also replaces abrupt parent/child geometry changes with reversible geometric morphs. The established planet, legacy Far/Mid/Local stack, ocean, MapMagic graph assets, and shader integration remain available in the same scene throughout the transition.

## MapMagic remains the authoring system

Body surface features still come from the `Graph` on `RoundMapMagicSurfaceDefinition`. The shared service evaluates that exact graph for canonical cube-sphere patch coordinates and stores the resulting elevation samples and texture-control weights. It does not bake a second procedural model or reinterpret the graph at different LODs.

During the incremental migration, the cache manager still borrows MapMagic globals from a compatible generation source exposed by the existing `CubeSphereMapMagicRootPool`. It owns the new queue, async budget, results, and cache; it does not ask the legacy pool to create or retire terrain tiles.

## World-level cache and scheduler

`CelestialSurfaceCacheManager` is created automatically by the first `CelestialBodyFactory` that needs it, or a scene-authored manager can be assigned to the factory. Every generated `CelestialSurfacePatchGenerator` is now a lightweight per-body client of this manager.

Patch identity includes:

- the definition, generation seed, radius, surface seed, elevation datum, and serialized graph fingerprint
- cache format version
- nested sample resolution and generation margins
- cube face, quadtree level, and patch coordinates

Bodies using the same complete identity share immutable `CelestialSurfacePatchData`; Unity meshes and control textures remain renderer-owned and are never placed in the data cache.

The scheduler provides:

- one shared async generation limit across all registered bodies
- a separate per-frame main-thread `Graph.Prepare` limit
- request classes for background, prefetch, visible, coverage, and future collision work
- aggregation when multiple clients request the same patch
- cancellation of requests abandoned by a moving observer
- preemption of lower-class background work by coverage or visible work
- serialization of evaluations that use the same MapMagic graph
- LRU eviction by patch count and estimated sample-memory budget
- explicit cache invalidation and cache-format versioning

The default concurrency remains one. This is conservative for MapMagic and makes workload behavior predictable; a scene manager can raise it when multiple independent graphs have been profiled safely.

## Coarse background preparation

The quality profile's **Adaptive Prewarm Coarse Surface** option requests all six root patches as soon as a body registers. These requests use the lowest priority, so a visible body or coverage repair goes first. A hidden adaptive renderer can therefore report `HasCoarseSurface` without rendering or refining a quadtree.

“Coarse ready” continues to mean that every viewing direction has a cached root fallback. It does not mean the complete body has been sampled at ground resolution.

## Exact parent-to-child morphs

The renderer still waits until all four direct child meshes exist before replacing a parent. Each child is initially positioned on the exact triangles of the parent mesh—not merely at parent-interpolated radial elevations—then smoothly moves to its own MapMagic samples. The transition reverses from its current weight if the observer retreats midway through it.

This makes subdivision behave as follows:

1. The parent covers the patch while child data and meshes are incomplete.
2. All four children appear as a perfect retessellation of the parent.
3. Child vertices morph to their higher-detail positions over the configured duration.
4. Deeper refinement begins only after that direct transition completes.
5. On collapse, the children morph back to the parent triangles before being pooled.

One-level neighbor balancing and inward skirts remain active. Morphing removes the parent/child pop; skirts continue to hide the temporary T-junction where a fully refined patch borders a coarser neighbor.

## Quality and diagnostics

`RoundMapMagicSurfaceQualityProfile` now adds:

- **Adaptive Prewarm Coarse Surface** (`true` by default)
- **Adaptive LOD Morph Duration Seconds** (`0.35 s` by default; `0` disables morphing)

The Debug Controller's **Body runtime package** recipe includes global client/surface counts, shared queue and active work, cache memory, completion/cancellation/eviction totals, manager errors, cache version, and active morphing branches.

## Same-scene checkpoint

1. Pull and compile while leaving the original planet and all existing Surface Frame components enabled.
2. Keep `CelestialBodyFactory` in `LodDebug` for the first pass and paste the updated Debug Controller recipe.
3. Enter Play Mode. Confirm one `Celestial Surface Cache Manager` appears at the scene root and reports one registered client/surface for the test body.
4. Confirm the six background roots generate even before every root is visible. `Coarse surface` should become true, `failed` should remain zero, and cache memory should rise gradually.
5. Approach the body rapidly, retreat, and approach again. Visible or coverage work should replace stale background work; `cancelled` may increase, but the surface must never disappear.
6. Move repeatedly across an LOD threshold in `LodDebug`. `morphing` should briefly rise above zero, color boundaries should move without holes, and `Coverage invariant` must stay true.
7. Fly from orbit to the ground and back out. Watch for radial popping, cracks, missing quadrants, or jobs continuing indefinitely for terrain left behind.
8. Cross a cube-face boundary at low altitude. `Neighbor balance` must remain true with maximum delta no greater than one.
9. Switch to `Surface` and confirm the MapMagic terrain controls remain oriented correctly and the established coastline/features match Milestone 3.
10. Optionally spawn a second instance using the same definition. Registered clients should increase while registered surfaces and duplicate generation remain shared.

Expected at this milestone:

- render patches share sampled data globally, but each body still owns its own meshes and textures
- the cache is memory-resident only; persistent disk caching remains optional future work
- generation still needs a compatible existing MapMagic source while the legacy scene remains our reference
- collision readiness remains false until Milestone 5
- terrain-control textures switch with the child patch while geometry morphs; shader-level texture crossfading can be added later if close inspection warrants it
