/*
 * Represents an invisible, motion-only massive reference such as a stellar-system barycenter.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialMotionReferencePoint :
        MonoBehaviour,
        ICelestialMotionStateSource
    {
        [SerializeField]
        private string instanceId;

        [SerializeField]
        private double configuredMassKilograms;

        [SerializeField]
        private TrajectoryCelestialBodyMotionProvider motionProvider;

        public string InstanceId =>
            instanceId ?? string.Empty;

        public double ConfiguredMassKilograms =>
            configuredMassKilograms;

        internal bool Initialize(
            string newInstanceId,
            double newMassKilograms,
            TrajectoryCelestialBodyMotionProvider newMotionProvider)
        {
            if (string.IsNullOrWhiteSpace(newInstanceId) ||
                double.IsNaN(newMassKilograms) ||
                double.IsInfinity(newMassKilograms) ||
                newMassKilograms <= 0.0 ||
                newMotionProvider == null)
            {
                return false;
            }

            instanceId =
                newInstanceId.Trim();
            configuredMassKilograms =
                newMassKilograms;
            motionProvider =
                newMotionProvider;
            return true;
        }

        public bool TryGetMotionState(
            out UniverseMotionState motionState)
        {
            if (motionProvider == null)
            {
                motionState = default;
                return false;
            }

            return
                motionProvider.TryGetMotionState(
                    out motionState);
        }
    }
}
