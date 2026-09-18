/*
 * Stores a bounded cube-sphere terrain location as a face, long tile coordinates, and local meters inside the tile.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CubeSphereTileAddress
    {
        [SerializeField]
        private CubeSphereFace face;

        [SerializeField]
        private long tileU;

        [SerializeField]
        private long tileV;

        [SerializeField]
        private double localUMeters;

        [SerializeField]
        private double localVMeters;

        [SerializeField]
        private double altitudeMeters;

        public CubeSphereFace Face =>
            face;

        public long TileU =>
            tileU;

        public long TileV =>
            tileV;

        public double LocalUMeters =>
            localUMeters;

        public double LocalVMeters =>
            localVMeters;

        public double AltitudeMeters =>
            altitudeMeters;

        public CubeSphereTileAddress(
            CubeSphereFace face,
            long tileU,
            long tileV,
            double localUMeters,
            double localVMeters,
            double altitudeMeters)
        {
            this.face = face;
            this.tileU = tileU;
            this.tileV = tileV;
            this.localUMeters = localUMeters;
            this.localVMeters = localVMeters;
            this.altitudeMeters = altitudeMeters;
        }

        public double GetFaceUMeters(
            double tileSizeMeters)
        {
            return
                tileU * tileSizeMeters +
                localUMeters;
        }

        public double GetFaceVMeters(
            double tileSizeMeters)
        {
            return
                tileV * tileSizeMeters +
                localVMeters;
        }

        public bool IsLocalPositionInsideTile(
            double tileSizeMeters)
        {
            return
                tileSizeMeters > 0.0 &&
                localUMeters >= 0.0 &&
                localUMeters < tileSizeMeters &&
                localVMeters >= 0.0 &&
                localVMeters < tileSizeMeters;
        }

        public override string ToString()
        {
            return
                $"{face}, Tile ({tileU}, {tileV}), Local ({localUMeters}, {localVMeters}) m, Altitude {altitudeMeters} m";
        }
    }
}
