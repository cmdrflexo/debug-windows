/*
 * Stores reusable material and global surface-elevation settings for a celestial body's ocean.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [CreateAssetMenu(
        fileName = "Ocean",
        menuName = "Celestial Systems/Ocean")]
    public sealed class OceanDefinition :
        ScriptableObject
    {
        [SerializeField]
        private Material material;

        [SerializeField]
        [Tooltip("Raises or lowers the global spherical ocean relative to the body's reference-radius datum.")]
        private double globalSurfaceElevationMeters;

        public Material Material =>
            material;

        public double GlobalSurfaceElevationMeters =>
            globalSurfaceElevationMeters;

        public bool HasValidSettings =>
            material != null &&
            IsFinite(
                globalSurfaceElevationMeters);

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(
                    value) &&
                !double.IsInfinity(
                    value);
        }
    }
}
