# Blender-style asteroid generator for Unity

A separate comparison implementation in `jcan.CelestialSystems`, reconstructed from the uploaded `rockgen.py`, `randomize_texture.py`, `utils.py`, and the Asteroid screenshot. It uses new class names and can coexist with `CelestialRockMeshGenerator`.

## Install and compare

1. Copy this entire `AsteroidGenerator` folder into `Assets/CelestialSystems/` (keep its `Editor` subfolder).
2. Create an empty GameObject. Add **Blender Asteroid Preview** and assign your rock material.
3. The starting settings reproduce the screenshot controls: XYZ ranges 1–5, zero skew, texture scaling off, deformation 7.74, roughness 1.56, smoothing disabled, viewport 3, render 4, seed 12.
4. Click **Regenerate**, then Previous/Next Seed to compare shapes. Use viewport detail 2 while experimenting if generation is slow; try 3 for the final comparison.
5. To reuse an existing preview object, disable its old `ProceduralRockPreview` before adding the new component. Two preview components must not write the same MeshFilter.
6. **Create Batch** creates the requested number of separate editable previews on a grid, using consecutive seeds. **Save Mesh Asset** generates the render-detail mesh and saves an asset; assign that asset to a regular MeshFilter for a permanent scene object.
7. Run **Tools → Celestial Systems → Validate Blender Asteroid Generator** to exercise the generator inside Unity. The command throws with a specific failure or logs PASS.

Auto Update starts disabled. Enable it for live adjustments. A preview also rebuilds when enabled; transient meshes are released when disabled. OnValidate only queues work for the main-thread Update, so it never destroys a mesh inside validation. Generated meshes do not modify imported assets.

## Why this differs from the current generator

The uploaded current generator subdivides an icosphere, then changes its radius using directional lobes, spherical cell weights and sine bands. This reconstruction uses the original 12 mixed triangle/quad cage recipes, independently randomized corner coordinates, Catmull–Clark subdivision, then sequential displacement along recalculated mesh normals.

Displacement order and exposed units follow the Python script:

- Weak one-octave Musgrave shape layer: deformation / 1000, zero midlevel.
- Dominant Cartesian Voronoi F1 layer: deformation / 10, zero midlevel. Euclidean or squared-distance metric; scale approximately 0.625, brightness 0.7, contrast 0.5.
- Medium texture: roughness / 50, midlevel 0.5.
- Fine texture: roughness / 100, midlevel 0.5, texture scale 0.15.

The strengths and texture parameters have the reference's randomized distributions. Medium/fine texture families are selected using the reference's Weibull-style weighting. Fixed-seed randomization is local and does not change UnityEngine.Random state.

## Controls

| Control | Meaning |
|---|---|
| Scale X/Y/Z | Per-corner coordinate ranges; not guaranteed finished bounds. Equal endpoints disable random variation on that axis. Reversed endpoints are sorted. |
| Skew | Bias in each coordinate distribution, -1 to +1. Zero is centered. This is not geometric shear. |
| Scale Textures / Texture Scale | Normalize cage coordinates before subdivision/displacement, then restore their axis scales afterward, as the script does. |
| Deformation / Roughness | Original user units, including their internal /10 and /100 conversions. |
| Smooth Factor / Iterations | Final neighbor smoothing. Either zero disables it. Factor values above one extrapolate, as allowed by the source. |
| Viewport / Render Detail | Each applies to two consecutive subdivision stages. Unity preview uses Viewport; asset export uses Render. |
| Base Shape | -1 selects randomly; 0–11 locks an original cage recipe for comparisons. |
| Meters Per Unit | Final uniform unit conversion; does not change texture frequency or the seed pattern. |
| Smooth Normals | Smooth shared normals or flat triangle shading. |
| Generate UVs | One continuous longitude/latitude chart in UV0, with a single seam on the rear longitude; tangents generated. |
| Random Seed | Explicit Regenerate chooses a seed and displays it. Previous/Next and automatic updates remain reproducible. |
| Number Of Rocks / Batch Spacing | Editor batch creation count and spacing in Unity units. |

Scale/skew/texture axes use Blender's X/Y/Z convention. Final coordinates are converted to Unity Y-up (Blender Z becomes Unity Y). UVs do not drive mesh displacement. The UVs are suitable for tiled detail. They have pole stretching and are not a unique unwrap for baking; a triplanar material also works.

Runtime use:

```csharp
var settings = new BlenderAsteroidSettings
{
    Seed = 12352,
    ViewportDetail = 2,
    Deformation = 7.74f,
    Roughness = 1.56f
};
Mesh mesh = BlenderAsteroidGenerator.Create(settings);
// Assign to a MeshFilter. The caller owns this mesh and must destroy it when done.
```

Generation is synchronous and allocates managed geometry. Pre-generate/cache meshes for ring spawning instead of generating a high-detail mesh on every spawn. Runtime calls that create a Unity Mesh must run on Unity's main thread.

## Fidelity limits and verification

This is a reconstruction, not a bit-for-bit Blender port. Same-number seeds will not produce the same object across the two applications.

- All 12 supplied cage coordinate/face recipes are translated, including unusual branches in the original. Cage winding is made consistent before subdivision.
- The deterministic PRNG and noise hashes differ from Blender/Python/NumPy.
- Blender Original and Original Perlin basis choices share an improved gradient-noise approximation here. Stucci is an approximation. Musgrave and distorted noise reproduce the layer structure but not Blender's exact legacy kernels/default internals.
- Crease weights use a fractional semisharp subdivision approximation. Edge indexing is first-face encounter order rather than Blender's implicit edge ordering; crease positions and sharpness can differ. These approximations are explicitly marked in code.
- Smooth normals are area weighted. Blender's weighting and modifier details may differ.
- Detail is capped at 4, batches at 20, and coordinate ranges have a small positive floor. The reference permits much higher values. At detail 4, a cube cage produces about 786,432 triangles; the triangular cages can produce about 1,572,864. Use lower detail for runtime objects.
- Only the screenshot's Asteroid defaults are provided. `settings.py` loads presets from `add_mesh_rocks.xml` / `factory.xml`; neither XML was supplied.

Local verification: all 12 source cages have closed two-face edge incidence, Euler characteristic 2, and consistent orientability. Their subdivision face counts were checked. Source delimiter and translated-recipe structure checks were performed. No Unity editor or C# compiler is installed in the execution environment, so Unity compilation, the included validation command, and the final visual match have **not** been run here. Use the included validation command and then compare the preview in your scene.

The cage implementation is derived from the supplied Blender add-on source; it does not carry the old Unity generator's claim of being an independent implementation. Source attribution is retained in code and this document.
