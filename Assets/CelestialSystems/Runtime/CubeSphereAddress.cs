/*
 * Stores a canonical location on a cube-sphere face using normalized equiangular coordinates and altitude.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CubeSphereAddress
    {
        [SerializeField]
        private CubeSphereFace face;

        [SerializeField]
        private double faceU;

        [SerializeField]
        private double faceV;

        [SerializeField]
        private double altitudeMeters;

        public CubeSphereFace Face => face;

        public double FaceU => faceU;

        public double FaceV => faceV;

        public double AltitudeMeters => altitudeMeters;

        public bool IsInsideFace =>
            faceU >= -1.0 &&
            faceU <= 1.0 &&
            faceV >= -1.0 &&
            faceV <= 1.0;

        public CubeSphereAddress(
            CubeSphereFace face,
            double faceU,
            double faceV,
            double altitudeMeters)
        {
            this.face = face;
            this.faceU = faceU;
            this.faceV = faceV;
            this.altitudeMeters = altitudeMeters;
        }

        public override string ToString()
        {
            return
                $"{face}, U {faceU}, V {faceV}, Altitude {altitudeMeters} m";
        }
    }
}
