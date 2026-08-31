/*
 * Stores a persistent universe-space position using integer cells and double-precision local meters.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct UniversePosition
    {
        public const double CellSizeMeters = 1000000000.0;

        [SerializeField]
        private long cellX;

        [SerializeField]
        private long cellY;

        [SerializeField]
        private long cellZ;

        [SerializeField]
        private double localXMeters;

        [SerializeField]
        private double localYMeters;

        [SerializeField]
        private double localZMeters;

        public long CellX => cellX;

        public long CellY => cellY;

        public long CellZ => cellZ;

        public double LocalXMeters => localXMeters;

        public double LocalYMeters => localYMeters;

        public double LocalZMeters => localZMeters;

        public UniversePosition(
            long cellX,
            long cellY,
            long cellZ,
            double localXMeters,
            double localYMeters,
            double localZMeters)
        {
            this.cellX = cellX;
            this.cellY = cellY;
            this.cellZ = cellZ;
            this.localXMeters = localXMeters;
            this.localYMeters = localYMeters;
            this.localZMeters = localZMeters;

            Normalize();
        }

        public void AddLocalMeters(double x, double y, double z)
        {
            localXMeters += x;
            localYMeters += y;
            localZMeters += z;

            Normalize();
        }

        public override string ToString()
        {
            return $"Cell ({cellX}, {cellY}, {cellZ}), Local m ({localXMeters}, {localYMeters}, {localZMeters})";
        }

        private void Normalize()
        {
            NormalizeAxis(ref cellX, ref localXMeters);
            NormalizeAxis(ref cellY, ref localYMeters);
            NormalizeAxis(ref cellZ, ref localZMeters);
        }

        private static void NormalizeAxis(ref long cell, ref double localMeters)
        {
            var cellShift = (long)(localMeters / CellSizeMeters);

            if (cellShift != 0)
            {
                cell += cellShift;
                localMeters -= cellShift * CellSizeMeters;
            }
        }
    }
}
