# Spherical Contour Fibers: first visual prototype

New MapMagic source under Map/Initial. Existing Spherical Flow Noise and graph
assets are untouched. This is a static visual field, not a magnetic simulation.

## Method and research

- Cabral and Leedom, *Imaging Vector Fields Using Line Integral Convolution*:
  https://cs.brown.edu/courses/csci2370/2000/1999/cabral.pdf
  LIC filters a texture along streamlines. The prior five-sample ridge average
  is too sparse for its chosen length/feature scale; its failure is not evidence
  against LIC. A dense implementation would need substantially more samples.
- *Physically Based Rendering*, Noise / Marble:
  https://pbr-book.org/3ed-2018/Texture/Noise
  Noise perturbs a phase that feeds a periodic band function. This node adapts
  that idea to a spherical scalar field rather than a world-Y coordinate.
  No external implementation is copied.

Two independently seeded and rotated broad value-noise samples define a scalar
field. A cosine creates multiple level bands through it. Smaller noise adds a
bounded phase disturbance; a power controls band width. No longitude, latitude,
fixed global strand axis, or sparse shifted image copies are used.

## First test

Connect Spherical Contour Fibers directly to the bright Textures layer. Keep
the required Height branch and the saved granulation branch unchanged.

| Parameter | Start | Meaning |
|---|---:|---|
| Seed Offset | 6203 | Added to the surface definition's seed |
| Region Size (m) | 300000000 | Scale of broad bends and closed contour regions |
| Bands | 12 | Cycles across the scalar field's full 0-1 range, not bands per region |
| Distortion Size (m) | 30000000 | Scale of small bends |
| Distortion (cycles) | 0.15 | Phase disturbance amplitude, independent of Bands |
| Sharpness | 2 | Larger values narrow bright stripes |
| Output | Fibers | Field shows the broad source without contour modulation |
| Intensity / Offset | 1 / 0 | Normalized output multiplier and offset |

The preview radius is only a fallback; runtime spherical context supplies the
actual radius and seed. Default fallback radius: 696340000 m.

## Verification and limits

The Python numerical prototype passed 10,000 finite/range, repeatability,
direction normalization, seed variation, and physical scale checks. Approaches
from either side of all 12 cube-edge ray families differed by at most 7.33e-9.
This is an independent translation, not a compiled C# or Unity integration test.

Run Tools > Celestial Systems > Validate Contour Fibers in Unity for checks
against the C# evaluator. Inspect all rendered faces and LOD transitions too.
Unity compilation and this menu command have not been run in the authoring
environment. No Unity/MapMagic compiler is installed there.

Spacing varies with the scalar gradient; extrema produce closed rings or broad
patches. This first version deliberately does not promise constant meter-wide
fibers, physical activity centers, temporal animation, or anti-aliasing.
The underlying value lattice may still impart directional bias. Increasing
Bands or Sharpness can alias in coarse graph maps; use a conservative preview
first, then address sample-footprint filtering before dense distant rendering.

The prototype render is a mathematical sanity check, not a screenshot from Unity.
The noise implementation and node mappings from main were reused unchanged.
