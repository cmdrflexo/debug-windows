/*
 * Defines the volume controls used by the URP histogram-based automatic exposure renderer feature.
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Automatic Exposure")]
    public sealed class AutomaticExposure :
        VolumeComponent,
        IPostProcessComponent
    {
        [Tooltip("Offsets the automatically calculated exposure in stops.")]
        public ClampedFloatParameter compensation =
            new ClampedFloatParameter(
                0.0f,
                -10.0f,
                10.0f);

        [Tooltip("Darkest exposure the system may select, in stops.")]
        public ClampedFloatParameter minimumExposure =
            new ClampedFloatParameter(
                -8.0f,
                -16.0f,
                16.0f);

        [Tooltip("Brightest exposure the system may select, in stops.")]
        public ClampedFloatParameter maximumExposure =
            new ClampedFloatParameter(
                8.0f,
                -16.0f,
                16.0f);

        [Tooltip("Percentage of the darkest metered pixels excluded from exposure calculation.")]
        public ClampedFloatParameter lowPercent =
            new ClampedFloatParameter(
                60.0f,
                0.0f,
                99.0f);

        [Tooltip("Percentage of metered pixels below the bright cutoff. Pixels above this percentile are excluded.")]
        public ClampedFloatParameter highPercent =
            new ClampedFloatParameter(
                98.0f,
                1.0f,
                100.0f);

        [Tooltip("Luminance that the metered scene is driven toward.")]
        public ClampedFloatParameter middleGray =
            new ClampedFloatParameter(
                0.18f,
                0.01f,
                1.0f);

        [Tooltip("Adaptation speed when exposure increases to reveal a darker scene.")]
        public ClampedFloatParameter brightenSpeed =
            new ClampedFloatParameter(
                1.0f,
                0.01f,
                20.0f);

        [Tooltip("Adaptation speed when exposure decreases for a brighter scene.")]
        public ClampedFloatParameter darkenSpeed =
            new ClampedFloatParameter(
                3.0f,
                0.01f,
                20.0f);

        public bool IsActive()
        {
            return active;
        }

        public bool IsTileCompatible()
        {
            return false;
        }
    }
}
