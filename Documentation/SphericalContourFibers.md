# Spherical Streamline Fibers

The existing Spherical Contour Fibers graph node now uses line-integral
convolution (LIC). Its type and serialized field names remain unchanged so saved
graph connections survive the upgrade. Spherical Flow Noise remains untouched.

## Method

A seeded, smooth three-component noise field is projected onto the tangent plane
at every sphere direction. The evaluator follows that direction both forward and
backward, repeatedly recomputing the tangent field, and averages fine carrier
noise along the resulting curved streamline with a cosine kernel.

This removes the periodic cosine responsible for the nested topographic rings.
Because a continuous tangent field cannot be nonzero everywhere on a sphere,
isolated swirl/singularity centers remain. The procedural vector components
distribute them instead of creating one repeated pattern or a fixed pole pair.

## Starting settings for a Sol-radius body

| Parameter | Start | Meaning |
|---|---:|---|
| Seed Offset | 6203 | Added to the surface seed |
| Flow Scale (m) | 180000000 | Scale of broad turns and activity regions |
| Streamline Samples | 24 | More makes longer/smoother fibers but costs more |
| Fiber Width (m) | 8000000 | Carrier-noise scale and integration step |
| Flow Complexity | 0.35 | Mix between circulating and direct tangent flow |
| Sharpness | 1.2 | Output contrast shaping |
| Output | Fibers | Field displays a diagnostic vector component |
| Intensity / Offset | 1 / 0 | Final normalized adjustment |

Existing nodes retain their old saved numeric values. Set the values above for
the first test after pulling; changing the labels does not rewrite graph assets.

## Performance and validation

Cost scales roughly linearly with Streamline Samples. At 24, each output pixel
evaluates 12 steps in both directions. Start there before trying 36 or 48.

The independent Python translation passes range, determinism, direction
normalization, seed variation, physical-scale invariance, and all 12 cube-edge
approach tests. Run **Tools > Celestial Systems > Validate Contour Fibers** in
Unity, then inspect rendered face seams and LOD transitions. Unity compilation
and rendered integration still require testing in the authoring project.

Reference: Cabral and Leedom, *Imaging Vector Fields Using Line Integral
Convolution*: https://cs.brown.edu/courses/csci2370/2000/1999/cabral.pdf
