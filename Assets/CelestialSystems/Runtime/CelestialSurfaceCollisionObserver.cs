/*
 * Requests local collision coverage for a gameplay actor independently of the rendering camera.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceCollisionObserver : MonoBehaviour
    {
        private static readonly HashSet<CelestialSurfaceCollisionObserver> observers =
            new HashSet<CelestialSurfaceCollisionObserver>();
        [SerializeField] private CelestialBodyRuntimeContext body;
        [SerializeField] private UniverseFrameController universeFrame;
        [SerializeField, Min(0.0f), Tooltip("Zero uses the body's collision coverage radius.")]
        private float coverageRadiusMeters;
        [SerializeField, Min(0.0f)] private float priority = 1.0f;

        public static IEnumerable<CelestialSurfaceCollisionObserver> ActiveObservers => observers;
        public CelestialBodyRuntimeContext Body => body;
        public UniverseFrameController UniverseFrame => universeFrame;
        public float CoverageRadiusMeters => coverageRadiusMeters;
        public float Priority => priority;
        public void SetBody(CelestialBodyRuntimeContext value) { body = value; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetObservers() { observers.Clear(); }
        private void OnEnable() { observers.Add(this); }
        private void OnDisable() { observers.Remove(this); }
        private void OnDestroy() { observers.Remove(this); }
    }
}
