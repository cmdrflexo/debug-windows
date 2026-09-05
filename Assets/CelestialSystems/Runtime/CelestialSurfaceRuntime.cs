/*
 * Owns the permanent adaptive-surface foundation for one packaged body while renderers and caches are added incrementally.
 */

using System;
using SpaceGraphicsToolkit;
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
        private CelestialSurfacePatchGenerator patchGenerator;

        [SerializeField]
        private CelestialSurfaceQuadtreeRenderer adaptiveRenderer;

        [SerializeField]
        private CelestialSurfaceCollisionRuntime collisionRuntime;

        private Camera projectionCamera;
        private SgtFloatingCamera floatingCamera;

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

        public CelestialSurfacePatchGenerator PatchGenerator =>
            patchGenerator;

        public CelestialSurfaceQuadtreeRenderer AdaptiveRenderer =>
            adaptiveRenderer;

        public CelestialSurfaceCollisionRuntime CollisionRuntime => collisionRuntime;

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
            patchGenerator = null;
            adaptiveRenderer = null;
            collisionRuntime = null;
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

        internal void AttachAdaptivePipeline(
            CelestialSurfacePatchGenerator newPatchGenerator,
            CelestialSurfaceQuadtreeRenderer newAdaptiveRenderer)
        {
            patchGenerator =
                newPatchGenerator;
            adaptiveRenderer =
                newAdaptiveRenderer;
        }

        internal void AttachCollisionRuntime(CelestialSurfaceCollisionRuntime value)
        {
            collisionRuntime = value;
        }

        public bool TrySampleBodyLocal(DoubleVector3 positionMeters, int requestedLevel,
            out CelestialSurfaceSample sample, bool allowCoarser = true)
        {
            sample = default;
            if (!foundationReady || patchGenerator == null || !patchGenerator.Initialized ||
                requestedLevel < MinimumLevel || requestedLevel > MaximumLevel ||
                !body.TryGetMotionState(out var motion) ||
                !CubeSphereMapping.TryDirectionToAddress(positionMeters, 0.0, out var address))
                return false;
            for (var level = requestedLevel; level >= 0; level--)
            {
                if (CubeSpherePatchAddress.TryFromAddress(address, level, out var patch) &&
                    patchGenerator.TryGetPatchData(patch, out var data) &&
                    CelestialSurfaceGeometry.TrySample(data, bodyDefinition.ReferenceRadiusMeters,
                        positionMeters, out var point, out var normal))
                {
                    var version = patchGenerator.CacheManager.GetSurfaceVersion(patchGenerator.ClientId);
                    sample = new CelestialSurfaceSample(data, requestedLevel, version,
                        bodyDefinition.ReferenceRadiusMeters, positionMeters, point, normal, motion);
                    return true;
                }
                if (!allowCoarser) break;
            }
            return false;
        }

        public bool TrySampleUniverse(UniversePosition position, int requestedLevel,
            out CelestialSurfaceSample sample, bool allowCoarser = true)
        {
            sample = default;
            return TryUniverseToBodyLocal(position, out var local) &&
                TrySampleBodyLocal(local, requestedLevel, out sample, allowCoarser);
        }

        public bool TrySampleScene(Vector3 position, int requestedLevel,
            out CelestialSurfaceSample sample, bool allowCoarser = true)
        {
            sample = default;
            return TrySceneToBodyLocal(position, out var local) &&
                TrySampleBodyLocal(local, requestedLevel, out sample, allowCoarser);
        }

        // Query reads never block or silently enqueue work. Renew explicit requests while needed.
        public bool RequestSurfaceSample(DoubleVector3 bodyPosition, int level)
        {
            return collisionRuntime != null && collisionRuntime.RequestSample(bodyPosition, level);
        }

        public bool HasCollisionSurface(Vector3 scenePosition)
        {
            return collisionRuntime != null && collisionRuntime.HasCoverageAt(scenePosition);
        }

        public bool TryUniverseToBodyLocal(UniversePosition position, out DoubleVector3 local)
        {
            local = default;
            if (body == null || !body.TryGetMotionState(out var motion)) return false;
            local = CelestialSurfaceGeometry.Rotate(CelestialSurfaceGeometry.Difference(position, motion.Position),
                Quaternion.Inverse(motion.Rotation));
            return CelestialSurfaceGeometry.IsFinite(local.Magnitude);
        }

        public bool TrySceneToBodyLocal(Vector3 scenePosition, out DoubleVector3 local)
        {
            local = default;
            return TryGetSceneOrigin(out var origin) &&
                TryUniverseToBodyLocal(CelestialSurfaceGeometry.Add(origin,
                    CelestialSurfaceGeometry.ToDouble(scenePosition)), out local);
        }

        public bool TryBodyLocalToScene(DoubleVector3 local, out Vector3 scenePosition)
        {
            scenePosition = default;
            if (!TryGetScenePose(out var center, out var rotation)) return false;
            scenePosition = CelestialSurfaceGeometry.ToVector3(center +
                CelestialSurfaceGeometry.Rotate(local, rotation));
            return true;
        }

        internal bool TryGetScenePose(out DoubleVector3 center, out Quaternion rotation)
        {
            center = default;
            rotation = Quaternion.identity;
            if (body == null || !body.TryGetMotionState(out var motion) ||
                !TryGetSceneOrigin(out var origin)) return false;
            center = CelestialSurfaceGeometry.Difference(motion.Position, origin);
            rotation = motion.Rotation;
            return CelestialSurfaceGeometry.IsFinite(center.Magnitude);
        }

        private bool TryGetSceneOrigin(out UniversePosition origin)
        {
            origin = default;
            var camera = adaptiveRenderer != null ? adaptiveRenderer.ObserverCamera : null;
            if (camera == null) camera = Camera.main;
            if (camera != projectionCamera || floatingCamera == null)
            {
                projectionCamera = camera;
                floatingCamera = camera != null ? camera.GetComponentInParent<SgtFloatingCamera>() : null;
            }
            if (floatingCamera != null)
            {
                origin = SgtUniversePositionConverter.ToUniversePosition(floatingCamera.Position);
                origin = CelestialSurfaceGeometry.Add(origin,
                    CelestialSurfaceGeometry.ToDouble(floatingCamera.transform.position) * -1.0);
                return true;
            }
            // Static/custom scene views can supply their projection through the existing surface transform.
            if (body == null || !body.TryGetMotionState(out var motion)) return false;
            origin = CelestialSurfaceGeometry.Add(motion.Position,
                CelestialSurfaceGeometry.ToDouble(transform.position) * -1.0);
            return true;
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
