/*
 * Evaluates normalized latitude directly from a body-space direction without cube-face seams.
 */

using System;

namespace jcan.CelestialSystems
{
    public enum SphericalLatitudeAxis { X, Y, Z }
    public enum SphericalLatitudeOutput { SignedLatitude, AbsoluteLatitude, EquatorProximity }

    public static class RoundMapMagicSphericalLatitude
    {
        public static double EvaluateNormalized(
            DoubleVector3 direction, SphericalLatitudeAxis axis, SphericalLatitudeOutput output)
        {
            var magnitude = Math.Sqrt(direction.x*direction.x + direction.y*direction.y + direction.z*direction.z);
            if (double.IsNaN(magnitude) || double.IsInfinity(magnitude) || magnitude <= 0) return 0;
            double component;
            switch (axis)
            {
                case SphericalLatitudeAxis.X: component = direction.x / magnitude; break;
                case SphericalLatitudeAxis.Z: component = direction.z / magnitude; break;
                default: component = direction.y / magnitude; break;
            }
            component = Math.Max(-1, Math.Min(1, component));
            var signed = Math.Asin(component) / Math.PI + 0.5;
            var absolute = Math.Abs(signed - 0.5) * 2;
            switch (output)
            {
                case SphericalLatitudeOutput.AbsoluteLatitude: return absolute;
                case SphericalLatitudeOutput.EquatorProximity: return 1 - absolute;
                default: return signed;
            }
        }
    }
}
