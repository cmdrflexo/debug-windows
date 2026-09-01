/*
 * Stores exact spherical-surface distances from an address to all four edges of its current cube-sphere face.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CubeSphereFaceProximity
    {
        [SerializeField]
        private double negativeUEdgeMeters;

        [SerializeField]
        private double positiveUEdgeMeters;

        [SerializeField]
        private double negativeVEdgeMeters;

        [SerializeField]
        private double positiveVEdgeMeters;

        public double NegativeUEdgeMeters =>
            negativeUEdgeMeters;

        public double PositiveUEdgeMeters =>
            positiveUEdgeMeters;

        public double NegativeVEdgeMeters =>
            negativeVEdgeMeters;

        public double PositiveVEdgeMeters =>
            positiveVEdgeMeters;

        public double ClosestUEdgeMeters =>
            Math.Min(
                negativeUEdgeMeters,
                positiveUEdgeMeters);

        public double ClosestVEdgeMeters =>
            Math.Min(
                negativeVEdgeMeters,
                positiveVEdgeMeters);

        public double ClosestEdgeMeters =>
            Math.Min(
                ClosestUEdgeMeters,
                ClosestVEdgeMeters);

        public CubeSphereEdge ClosestEdge
        {
            get
            {
                var edge = CubeSphereEdge.NegativeU;
                var distanceMeters =
                    negativeUEdgeMeters;

                if (positiveUEdgeMeters <
                    distanceMeters)
                {
                    edge = CubeSphereEdge.PositiveU;
                    distanceMeters =
                        positiveUEdgeMeters;
                }

                if (negativeVEdgeMeters <
                    distanceMeters)
                {
                    edge = CubeSphereEdge.NegativeV;
                    distanceMeters =
                        negativeVEdgeMeters;
                }

                if (positiveVEdgeMeters <
                    distanceMeters)
                {
                    edge = CubeSphereEdge.PositiveV;
                }

                return edge;
            }
        }

        public CubeSphereFaceProximity(
            double negativeUEdgeMeters,
            double positiveUEdgeMeters,
            double negativeVEdgeMeters,
            double positiveVEdgeMeters)
        {
            this.negativeUEdgeMeters =
                negativeUEdgeMeters;
            this.positiveUEdgeMeters =
                positiveUEdgeMeters;
            this.negativeVEdgeMeters =
                negativeVEdgeMeters;
            this.positiveVEdgeMeters =
                positiveVEdgeMeters;
        }

        public bool IsNearEdge(double thresholdMeters)
        {
            ValidateThreshold(thresholdMeters);

            return ClosestEdgeMeters <=
                thresholdMeters;
        }

        public bool IsNearCorner(double thresholdMeters)
        {
            ValidateThreshold(thresholdMeters);

            return
                ClosestUEdgeMeters <=
                    thresholdMeters &&
                ClosestVEdgeMeters <=
                    thresholdMeters;
        }

        public override string ToString()
        {
            return
                $"-U {negativeUEdgeMeters} m, +U {positiveUEdgeMeters} m, -V {negativeVEdgeMeters} m, +V {positiveVEdgeMeters} m";
        }

        private static void ValidateThreshold(
            double thresholdMeters)
        {
            if (double.IsNaN(thresholdMeters) ||
                thresholdMeters < 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(thresholdMeters),
                    "An edge threshold must be non-negative.");
            }
        }
    }
}
