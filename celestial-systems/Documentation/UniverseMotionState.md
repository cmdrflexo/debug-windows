# Universe motion state

`UniverseMotionState` replaces `CelestialBodyMotionState` as the shared value type for an object's global pose and motion. It has no dependency on a celestial body, parent, sector, gravity source, or motion backend.

| Member | Meaning |
| --- | --- |
| `Position` | Persistent `UniversePosition`: integer cells and local double-precision meters. |
| `Rotation` | Unity `Quaternion` mapping object-local axes into universe axes. |
| `LinearVelocityMetersPerSecond` | `DoubleVector3` velocity of `Position`, in universe axes and meters per simulated SI second. |
| `AngularVelocityRadiansPerSecond` | `DoubleVector3` rotation axis in universe axes, multiplied by radians per simulated SI second. Uses Unity's quaternion axis/angle convention, not Euler angle rates. |

The four-argument constructor supplies a complete state. The two-argument constructor explicitly creates a stationary pose with both velocities zero. As with the old struct, use a constructor with `Quaternion.identity` for an identity pose; `default(UniverseMotionState)` contains a zero quaternion and is not a usable orientation.

Changing the floating origin only changes the scene representation. It does not rotate these axes or change the global velocities. A gravity-influence reference can be added to the object that owns the state later, independently of the coordinate representation.

## Gravity Engine adapter

`ICelestialBodyMotionProvider.TryGetMotionState` and `CelestialBodyRuntimeContext.TryGetMotionState` now return the generic type. The interface remains body-specific in this milestone. Callers must check the returned boolean before using the output; cached Inspector values can describe the last successful sample when motion is unavailable.

The GE provider:

- Preserves the existing position and rotation sources: frame origin plus double-precision GE position, and `SourceBody.transform.rotation`.
- Reads double-precision translation velocity from `GetWorldState().GetVelocity3d(SourceBody)`. It applies `GravityScaler.VelocityScaletoSIUnits()`, the inverse of the velocity conversion used when spawning a body.
- Requires SI units, matching the factory's existing restriction. It rejects invalid scales and non-finite converted values.
- Estimates angular velocity from successive source rotations against `GravityScaler.GetWorldTimeSeconds(worldState.GetPhysicsTime())`. GE provides translation; this code does not add a spin simulation.

Position and velocity conversions have different scale factors. Neither scene units nor Unity's frame duration belong in the physical velocity conversion. Pausing evolution holds the current physical velocity; time acceleration does not directly multiply it. Motion rates describe simulated time, rather than wall-clock playback speed.

The vendor documents the world-state read boundary in its [GE guide](https://nbodyphysics.com/blog/gravity-engine-doc-1-3-2-2-2/gravity-engine-10v0-2-2-2/), velocity and time methods in [GravityState](https://nbodyphysics.com/gravityengine/html/class_gravity_state.html), and conversions in [GravityScaler](https://nbodyphysics.com/gravityengine/html/class_gravity_scaler.html). The public reference is GE 12; the project's installed GE 13.1 package remains the compilation check.

## Angular sampling

`UniverseAngularVelocitySampler` keeps the last two distinct simulation timestamps. Repeated reads preserve the estimate. A rotation updated later at the same timestamp recalculates against the previous distinct sample, so polling cannot create a zero-duration division or consume the interval.

The first valid sample establishes a baseline and reports zero with `HasAngularVelocityEstimate: false` on the provider. A second distinct timestamp makes the estimate available, including a valid zero estimate for a stationary body. Disabling or reinitializing the provider, changing its body/world-state source, receiving invalid input, or rewinding time clears the history.

Angular velocity is an average over that interval, not an instantaneous authoritative spin rate. Rotations must be authored against the same simulation clock. A paused sample with an unchanged rotation preserves the last rate. For a manual rotation teleport or a discontinuous clock/rotation change, call **Reset Angular Velocity Sampling** on the provider after making the change; the next sample becomes a fresh baseline. Sampling uses the shortest quaternion arc, so rotation must remain below 180 degrees between distinct samples. A future spin authority can provide its angular velocity directly, avoiding this sampling limitation.

## Migration and scope

The renamed script retains its `.meta` GUID and declares Unity's `MovedFrom` serialization metadata. Existing serialized `position` and `rotation` field names are retained; missing velocity fields default to zero. Source code that explicitly names the old type must use `UniverseMotionState`. No compatibility alias remains.

All tracked consumers are updated. Surface queries, collision motion, and drop-test frame compensation retain their existing algorithms. In particular, collision/drop-test velocity estimation still uses its existing Unity physics timestep; it has not been switched to the new SI velocity fields. This milestone does not claim to fix carrier compensation under time acceleration. A later milestone can deliberately convert the clock and frame conventions before using `v + omega × offset`.

## Validation in Unity

1. Pull and allow Unity to compile. Run **Tools > Celestial Systems > Validate Universe Motion State** outside Play Mode. Expect a passing Console message. This checks actual Unity serialization, legacy pose data, global cell precision, and angular sampling, including small angles, quaternion signs, universe axes, repeated reads, reset, and rewind.
2. Play the existing factory scene. Confirm the body and its generated `Motion` provider are ready, with no error. Terrain, ocean, surface queries, and the existing drop test should behave as before.
3. Use a body with a known nonzero initial velocity. At spawn, compare the state with the spawner's SI velocity; as it evolves, gravity may change the vector. A stationary body should report zero angular velocity. On the provider, `Has Angular Velocity Estimate` should become true after simulation time advances.
4. Move far enough to trigger an origin shift. Check that the reported velocity has no shift-sized spike and the terrain/ocean remain aligned. Repeating this with GE evolution paused gives an exact check: global pose and stored velocity should remain unchanged across an origin shift.
5. Pause and resume GE, then try a modest time-acceleration setting. Motion values should remain finite; paused physical velocities are retained. Angular sampling uses GE simulation seconds. Test collision at the existing normal playback speed for this milestone.
6. The editor validation supplies known rotations and known simulated intervals for the angular checks. Once a runtime spin authority is added, compare its known axis and rate with this sampled value; a manual Inspector rotation is a teleport, so reset sampling afterward.

Use the compact **Universe motion state** display in `Assets/CelestialSystems/Debug/CelestialDebugTextController-Guide.md`. It uses the existing `factory` source. Replace only temporary Display Code for this test; the source list can stay intact.
