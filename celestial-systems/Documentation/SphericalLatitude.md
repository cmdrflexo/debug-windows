# Spherical Latitude

Seam-free MapMagic source under **Map/Initial > Spherical Latitude**.

| Output | Range |
|---|---|
| Signed Latitude | South pole 0, equator 0.5, north pole 1 |
| Absolute Latitude | Equator 0, either pole 1 |
| Equator Proximity | Equator 1, either pole 0 |

Latitude is linear in angular degrees rather than the raw axis component. Axis
defaults to Y and may be changed for bodies authored around another local pole.
Intensity and Offset are applied after evaluation.

## Solar activity belts

Use **Absolute Latitude** into a Unity Curve. For an initial solar mask, set the
curve to 0 at 0 degrees, rise to 1 near 15 degrees, hold through roughly 30
degrees, and return to 0 by 45 degrees. Multiply the curve output into both the
penumbra and umbra masks.

Run **Tools > Celestial Systems > Validate Spherical Latitude** after compiling.
The validation checks pole/equator landmarks and all 12 cube-edge families.
