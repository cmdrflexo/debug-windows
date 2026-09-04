/*
 * Carries the authoritative universe pose supplied by a celestial-body motion provider.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public struct CelestialBodyMotionState
    {
        [SerializeField]
        private UniversePosition position;

        [SerializeField]
        private Quaternion rotation;

        public UniversePosition Position =>
            position;

        public Quaternion Rotation =>
            rotation;

        public CelestialBodyMotionState(
            UniversePosition position,
            Quaternion rotation)
        {
            this.position = position;
            this.rotation = rotation;
        }
    }
}
