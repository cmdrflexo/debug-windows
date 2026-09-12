/*
 * Defines one generic relative Keplerian orbit and partitions it into two
 * mass-weighted component states around a shared barycenter.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum CelestialTwoBodyComponent
    {
        BodyA = 0,
        BodyB = 1
    }

    [Serializable]
    public sealed class CelestialTwoBodyBarycentricOrbit
    {
        [SerializeField]
        private string pairId;

        [SerializeField]
        private string barycenterReferenceInstanceId;

        [SerializeField]
        private double bodyAMassKilograms;

        [SerializeField]
        private double bodyBMassKilograms;

        [SerializeField]
        private KeplerianConicTrajectory relativeTrajectory;

        public CelestialTwoBodyBarycentricOrbit(
            string pairId,
            string barycenterReferenceInstanceId,
            double bodyAMassKilograms,
            double bodyBMassKilograms,
            KeplerianConicTrajectory relativeTrajectory)
        {
            this.pairId = pairId;
            this.barycenterReferenceInstanceId =
                barycenterReferenceInstanceId;
            this.bodyAMassKilograms =
                bodyAMassKilograms;
            this.bodyBMassKilograms =
                bodyBMassKilograms;
            this.relativeTrajectory =
                relativeTrajectory;
        }

        public string PairId => pairId;

        public string BarycenterReferenceInstanceId =>
            barycenterReferenceInstanceId;

        public double BodyAMassKilograms =>
            bodyAMassKilograms;

        public double BodyBMassKilograms =>
            bodyBMassKilograms;

        public double TotalMassKilograms =>
            bodyAMassKilograms +
            bodyBMassKilograms;

        public KeplerianConicTrajectory RelativeTrajectory =>
            relativeTrajectory;

        public bool TryValidate(
            out string error)
        {
            if (string.IsNullOrWhiteSpace(
                    pairId) ||
                string.IsNullOrWhiteSpace(
                    barycenterReferenceInstanceId))
            {
                error =
                    "A two-body barycentric orbit requires pair and barycenter reference IDs.";
                return false;
            }

            if (!IsFinitePositive(
                    bodyAMassKilograms) ||
                !IsFinitePositive(
                    bodyBMassKilograms))
            {
                error =
                    "A two-body barycentric orbit requires two finite positive masses.";
                return false;
            }

            if (relativeTrajectory == null ||
                !relativeTrajectory.TryValidate(
                    out error))
            {
                if (relativeTrajectory == null)
                {
                    error =
                        "A two-body barycentric orbit requires a relative trajectory.";
                }

                return false;
            }

            if (!string.Equals(
                    relativeTrajectory.ReferenceInstanceId,
                    barycenterReferenceInstanceId,
                    StringComparison.Ordinal))
            {
                error =
                    "The relative trajectory and pair must name the same barycenter reference.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public double GetPositionScale(
            CelestialTwoBodyComponent component)
        {
            return
                component == CelestialTwoBodyComponent.BodyA
                    ? -bodyBMassKilograms /
                        TotalMassKilograms
                    : bodyAMassKilograms /
                        TotalMassKilograms;
        }

        private static bool IsFinitePositive(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value > 0.0;
        }
    }
}
