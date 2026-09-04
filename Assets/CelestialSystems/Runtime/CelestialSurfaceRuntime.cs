/*
 * Owns the permanent adaptive-surface foundation for one packaged body while renderers and caches are added incrementally.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-80)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceRuntime :
        MonoBehaviour
    {
        private static readonly CubeSphereFace[] RootFaces =
        {
            CubeSphereFace.PositiveX,
            CubeSphereFace.NegativeX,
            CubeSphereFace.PositiveY,
            CubeSphereFace.NegativeY,
            CubeSphereFace.PositiveZ,
            CubeSphereFace.NegativeZ
        };

        [Header("Runtime Ownership")]
        [SerializeField]
        private CelestialBodyRuntimeContext body;

        [SerializeField]
        private CelestialBodyDefinition bodyDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceDefinition surfaceDefinition;

        [SerializeField]
        private RoundMapMagicSurfaceQualityProfile qualityProfile;

        [Header("Foundation")]
        [SerializeField]
        private bool foundationReady;

        [SerializeField]
        private CelestialSurfaceCacheKey cacheKey;

        [SerializeField]
        private CelestialSurfaceLodPolicy lodPolicy;

        [SerializeField]
        private int rootPatchCount;

        [SerializeField]
        private string lastError;

        public CelestialBodyRuntimeContext Body =>
            body;

        public CelestialBodyDefinition BodyDefinition =>
            bodyDefinition;

        public RoundMapMagicSurfaceDefinition SurfaceDefinition =>
            surfaceDefinition;

        public RoundMapMagicSurfaceQualityProfile QualityProfile =>
            qualityProfile;

        public bool FoundationReady =>
            foundationReady;

        public CelestialSurfaceCacheKey CacheKey =>
            cacheKey;

        public string GraphSettingsHash =>
            cacheKey.GraphSettingsHash;

        public CelestialSurfaceLodPolicy LodPolicy =>
            lodPolicy;

        public int PatchResolution =>
            lodPolicy.PatchResolution;

        public int MinimumLevel =>
            lodPolicy.MinimumLevel;

        public int MaximumLevel =>
            lodPolicy.MaximumLevel;

        public int RootPatchCount =>
            rootPatchCount;

        public string LastError =>
            lastError;

        public bool Initialize(
            CelestialBodyRuntimeContext newBody)
        {
            foundationReady = false;
            lastError = string.Empty;
            body = newBody;
            bodyDefinition =
                body != null
                    ? body.Definition
                    : null;
            qualityProfile =
                body != null
                    ? body.QualityProfile
                    : null;
            surfaceDefinition =
                bodyDefinition != null
                    ? bodyDefinition.RoundMapMagicSurface
                    : null;

            if (body == null ||
                bodyDefinition == null)
            {
                return Fail(
                    "The surface runtime requires an initialized body package.");
            }

            if (bodyDefinition.ResolvedSurfaceSystem !=
                    CelestialSurfaceSystem.RoundMapMagic ||
                surfaceDefinition == null ||
                !surfaceDefinition.HasValidSettings)
            {
                return Fail(
                    "The surface runtime currently requires a valid Round MapMagic surface definition.");
            }

            lodPolicy =
                qualityProfile != null
                    ? qualityProfile.AdaptiveLodPolicy
                    : CelestialSurfaceLodPolicy.CreateDefault();

            if (!lodPolicy.IsValid)
            {
                return Fail(
                    "The adaptive surface LOD policy is invalid.");
            }

            if (!CelestialSurfaceCacheKey.TryCreate(
                    bodyDefinition,
                    out cacheKey,
                    out var cacheKeyError))
            {
                return Fail(
                    cacheKeyError);
            }

            rootPatchCount =
                RootFaces.Length;
            foundationReady = true;
            body.AttachSurfaceRuntime(
                this);
            return true;
        }

        public bool TryGetRootPatch(
            int index,
            out CubeSpherePatchAddress address)
        {
            if (!foundationReady ||
                index < 0 ||
                index >= RootFaces.Length)
            {
                address = default;
                return false;
            }

            address =
                CubeSpherePatchAddress.Root(
                    RootFaces[index]);
            return true;
        }

        public bool TryCreateMapMagicRequest(
            CubeSpherePatchAddress address,
            out RoundMapMagicSurfacePatchRequest request,
            out string error)
        {
            if (!foundationReady)
            {
                request = null;
                error =
                    "The surface foundation is not ready.";
                return false;
            }

            return
                RoundMapMagicSurfacePatchRequest.TryCreate(
                    bodyDefinition,
                    address,
                    lodPolicy.PatchResolution,
                    out request,
                    out error);
        }

        private bool Fail(
            string error)
        {
            foundationReady = false;
            rootPatchCount = 0;
            lastError = error;
            return false;
        }
    }
}
