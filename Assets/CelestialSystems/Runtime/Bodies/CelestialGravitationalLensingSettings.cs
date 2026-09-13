/*
 * Defines optional screen-space gravitational lensing controls for a celestial body.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialGravitationalLensingSettings
    {
        [SerializeField]
        private bool enabled;

        [SerializeField]
        [Min(1.0f)]
        [Tooltip("Multiplies the reference radius to define the world-space radius affected by lensing.")]
        private float influenceRadiusMultiplier;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Controls the maximum screen-space distortion as a fraction of the lens influence radius.")]
        private float strength;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("Controls how tightly distortion concentrates around the lens.")]
        private float falloff;

        [SerializeField]
        [Range(0.01f, 1.0f)]
        [Tooltip("Caps the lens influence radius in viewport-height units to prevent a nearby lens from consuming the whole frame.")]
        private float maximumScreenRadius;

        public bool Enabled => enabled;
        public float InfluenceRadiusMultiplier => influenceRadiusMultiplier;
        public float Strength => strength;
        public float Falloff => falloff;
        public float MaximumScreenRadius => maximumScreenRadius;

        public bool HasValidSettings =>
            enabled &&
            IsFinite(influenceRadiusMultiplier) &&
            influenceRadiusMultiplier >= 1.0f &&
            IsFinite(strength) &&
            strength > 0.0f &&
            IsFinite(falloff) &&
            falloff > 0.0f &&
            IsFinite(maximumScreenRadius) &&
            maximumScreenRadius > 0.0f;

        public static CelestialGravitationalLensingSettings Default =>
            new CelestialGravitationalLensingSettings(
                false,
                3.0f,
                0.18f,
                1.5f,
                0.75f);

        public CelestialGravitationalLensingSettings(
            bool newEnabled,
            float newInfluenceRadiusMultiplier,
            float newStrength,
            float newFalloff,
            float newMaximumScreenRadius)
        {
            enabled = newEnabled;
            influenceRadiusMultiplier =
                newInfluenceRadiusMultiplier;
            strength = newStrength;
            falloff = newFalloff;
            maximumScreenRadius =
                newMaximumScreenRadius;
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }
    }
}
