/*
 * Metadata carried by a pooled celestial small-body GameObject. Requesting
 * systems may add their own motion, collider, and gameplay components.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialSmallBodyInstance :
        MonoBehaviour
    {
        [SerializeField]
        private string sourceToolId;

        [SerializeField]
        private uint seed;

        [SerializeField]
        private CelestialSmallBodyLod generatedLod;

        [SerializeField]
        private bool isPoolManaged;

        public string SourceToolId =>
            sourceToolId;

        public uint Seed =>
            seed;

        public CelestialSmallBodyLod GeneratedLod =>
            generatedLod;

        public bool IsPoolManaged =>
            isPoolManaged;

        internal void ConfigurePoolMetadata(
            CelestialSmallBodyRequest request)
        {
            sourceToolId =
                request.ToolId;
            seed =
                request.Seed;
            generatedLod =
                request.DesiredLod;
            isPoolManaged = true;
        }

        internal void ClearPoolMetadata()
        {
            isPoolManaged = false;
        }
    }
}
