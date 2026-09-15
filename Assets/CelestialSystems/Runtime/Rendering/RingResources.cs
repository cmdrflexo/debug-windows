/*
 * Holds scene-authored resources shared by runtime-generated ring bodies.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class RingResources :
        MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Object families available to generated close-range ring streaming.")]
        private CelestialRingObjectSet objectSet;

        public CelestialRingObjectSet ObjectSet =>
            objectSet;
    }
}
