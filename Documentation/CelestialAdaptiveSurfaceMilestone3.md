# Adaptive Celestial Surface Renderer — Milestone 3

Milestone 3 adds the first visible renderer built on the unified patch foundation. It is installed beside the working Far/Mid/Local stack and is **hidden by default**. No legacy scene component, MapMagic graph, ocean, renderer, or handoff is removed or disabled.

## MapMagic remains the surface authoring system

Every adaptive patch evaluates the `Graph` referenced by the body's existing `RoundMapMagicSurfaceDefinition`. Height and texture outputs use the same spherical coordinate convention and elevation datum established by the legacy samplers and Milestone 2 patch request.

During this transition, `CelestialSurfacePatchGenerator` locates a compatible `MapMagicObject` on the existing `CubeSphereMapMagicRootPool` and borrows its globals as the generation context. It does not ask that pool to create or release terrain tiles. Milestone 4 will replace this temporary source lookup with the global cache and generation service.

## Generated runtime components

The factory now adds two internal components to the packaged body's existing `Visuals/Surface` object:

- `CelestialSurfacePatchGenerator` owns a priority queue, one bounded background MapMagic evaluation, immutable `CelestialSurfacePatchData`, and the temporary per-body memory cache.
- `CelestialSurfaceQuadtreeRenderer` owns six patch roots, adaptive selection, horizon/frustum culling, one-level neighbor balancing, mesh/control-texture creation, pooling, and readiness reporting.

The public body facade remains `CelestialBodyRuntimeContext`; other game systems do not need to find these generated components.

## Patch selection and continuity

Each patch uses the quality profile's nested resolution. Projected error combines the sampled height error with a conservative cell-size estimate, so a low-resolution whole-face evaluation cannot incorrectly claim that fine terrain needs no refinement.

The renderer guarantees the following transition behavior:

- A parent remains visible until all four child meshes exist.
- All four children replace their parent in one update.
- Desired neighboring leaves differ by no more than one level, subject to the configured total patch cap.
- Shared edges use the canonical nested sample positions.
- Every patch includes a short inward skirt to cover temporary one-level T-junctions. Milestone 4 will add geometric morphing and the final transition treatment.
- Render meshes are pooled when a branch collapses.

Horizon and frustum tests prevent non-visible branches from refining. The six root maps are still requested first so `HasCoarseSurface` has a precise meaning: every viewing direction has a cached fallback.

## Transitional render modes

`CelestialBodyFactory` exposes **Adaptive Surface Render Mode**:

| Mode | Behavior |
|---|---|
| `Hidden` | Default. The new renderer does no patch work and the current surface remains unchanged. |
| `Surface` | Renders MapMagic terrain layers with the surface definition's material or the custom celestial terrain shader. |
| `LodDebug` | Colors patches by face and quadtree level so subdivision and coverage can be inspected. |

The generated renderer also has context-menu commands to change these modes during Play Mode. Changing a runtime mode is diagnostic only and is not saved back to the factory.

## Quality settings

The existing `RoundMapMagicSurfaceQualityProfile` now also owns:

- maximum desired patch count (`768` by default)
- maximum mesh builds per frame (`4`)
- MapMagic generation margins (`2`)
- skirt depth as a fraction of one patch cell (`0.02`)
- minimum skirt depth (`1 m`)

The established adaptive patch resolution, level range, screen-error threshold, hysteresis, and one-level neighbor rule remain unchanged.

## Same-scene checkpoint

1. Leave the original planet, `Planet Surface Frame`, Far/Mid/Local components, ocean, and MapMagic roots enabled.
2. On `CelestialBodyFactory`, set **Adaptive Surface Render Mode** to `Lod Debug`.
3. Enter Play Mode and allow the six roots to generate. The existing surface remains underneath as a safety fallback.
4. Use the updated **Body runtime package** Debug Controller display.
5. Confirm `MapMagic source`, `Neighbor balance`, and `Coverage invariant` are green/true; `failed` remains `0`.
6. Confirm `Coarse surface` becomes true after all six roots are cached and `Visible surface` becomes true when the first patch mesh appears.
7. Move from distant orbit toward the surface. `Active LOD` should rise, parents may briefly appear in `held parents`, and terrain must not disappear while their children generate.
8. Orbit across at least one cube-face boundary. Look for missing quadrants, open cracks, or a fixed face seam.
9. Switch the generated `CelestialSurfaceQuadtreeRenderer` to **Show Adaptive Surface** from its component context menu. Compare the MapMagic shoreline and regional features with the current renderer.
10. Return the factory mode to `Hidden` after the comparison if continued work should use only the established surface.

Expected during this milestone:

- patch generation is deliberately one-at-a-time and locally cached
- child changes do not morph yet, though the parent prevents holes and skirts cover T-junctions
- collision readiness remains false
- the existing renderer may z-fight with the adaptive renderer in `Surface` mode because both are intentionally present; hide one renderer only for the visual comparison, then restore it

Milestone 4 will globalize generation/cache budgets across bodies, add cancellation/reprioritization, data eviction, parent-child morphing, and final crack treatment.
