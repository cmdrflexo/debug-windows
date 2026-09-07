/*
 * Validates spherical-latitude landmarks, normalization, and cube-edge continuity.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class SphericalLatitudeValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Spherical Latitude")]
        public static void Validate()
        {
            Require(Near(Sample(new DoubleVector3(0, 1, 0)), 1), "North pole");
            Require(Near(Sample(new DoubleVector3(0, 0, 1)), 0), "Equator");
            Require(Near(Sample(new DoubleVector3(0, -1, 0)), 1), "South pole");
            var maximumGap = 0.0;
            for (var i=0; i<3; i++) for (var j=i+1; j<3; j++)
            for (var si=-1; si<=1; si+=2) for (var sj=-1; sj<=1; sj+=2)
            for (var t=0; t<=256; t++)
            {
                var v = new double[3]; v[i]=si; v[j]=sj; v[3-i-j]=t/128.0-1;
                var left=(double[])v.Clone(); var right=(double[])v.Clone();
                left[j]*=1-1e-10; right[i]*=1-1e-10;
                maximumGap=Math.Max(maximumGap, Math.Abs(
                    Sample(new DoubleVector3(left[0],left[1],left[2]))-
                    Sample(new DoubleVector3(right[0],right[1],right[2]))));
            }
            Require(maximumGap < 1e-8, "Cube-edge continuity");
            Debug.Log("Spherical Latitude PASS. Max edge gap: " + maximumGap.ToString("G6"));
        }

        private static double Sample(DoubleVector3 direction)
        {
            return RoundMapMagicSphericalLatitude.EvaluateNormalized(direction,
                SphericalLatitudeAxis.Y, SphericalLatitudeOutput.AbsoluteLatitude);
        }
        private static bool Near(double a, double b) { return Math.Abs(a-b) < 1e-12; }
        private static void Require(bool passed, string name)
        {
            if (!passed) throw new InvalidOperationException("Spherical Latitude failed: " + name);
        }
    }
}
