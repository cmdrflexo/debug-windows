/*
 * Renders one MapMagic-authored celestial surface as a pooled, culled, neighbor-balanced cube-sphere quadtree.
 */

using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(300)]
    [DisallowMultipleComponent]
    public sealed class CelestialSurfaceQuadtreeRenderer :
        MonoBehaviour
    {
        private const string DefaultTerrainShaderName =
            "jcan/Celestial Systems/Celestial Body Terrain";
        private const double DefaultHeightEnvelopeMeters =
            20000.0;
        private const double CurvatureErrorFraction =
            0.25;
        private const int MaximumAdaptedLayerCount =
            4;

        private static readonly ProfilerMarker RequestFrameMarker =
            new ProfilerMarker("Celestial Surface.Request Frame");
        private static readonly ProfilerMarker CacheResetMarker =
            new ProfilerMarker("Celestial Surface.Cache Reset");
        private static readonly ProfilerMarker ObserverMarker =
            new ProfilerMarker("Celestial Surface.Observer");
        private static readonly ProfilerMarker PrepareTreeMarker =
            new ProfilerMarker("Celestial Surface.Prepare Tree");
        private static readonly ProfilerMarker BalanceTreeMarker =
            new ProfilerMarker("Celestial Surface.Balance Tree");
        private static readonly ProfilerMarker EvaluateBalanceMarker =
            new ProfilerMarker("Celestial Surface.Evaluate Balance");
        private static readonly ProfilerMarker MaterialsMarker =
            new ProfilerMarker("Celestial Surface.Materials");
        private static readonly ProfilerMarker ApplyTreeMarker =
            new ProfilerMarker("Celestial Surface.Apply Tree");
        private static readonly ProfilerMarker StatisticsMarker =
            new ProfilerMarker("Celestial Surface.Statistics");
        private static readonly ProfilerMarker BuildVisualMarker =
            new ProfilerMarker("Celestial Surface.Build Visual");
        private static readonly ProfilerMarker BuildGeometryMarker =
            new ProfilerMarker("Celestial Surface.Build Geometry");
        private static readonly ProfilerMarker UploadMeshMarker =
            new ProfilerMarker("Celestial Surface.Upload Mesh");
        private static readonly ProfilerMarker RecalculateTangentsMarker =
            new ProfilerMarker("Celestial Surface.Recalculate Tangents");
        private static readonly ProfilerMarker ConfigureMaterialMarker =
            new ProfilerMarker("Celestial Surface.Configure Material");

        private static readonly CubeSphereEdge[] Edges =
        {
            CubeSphereEdge.NegativeU,
            CubeSphereEdge.PositiveU,
            CubeSphereEdge.NegativeV,
            CubeSphereEdge.PositiveV
        };

        private sealed class PatchNode
        {
            public CubeSpherePatchAddress Address;
            public PatchNode Parent;
            public PatchNode[] Children;
            public PatchVisual Visual;
            public CelestialSurfacePatchData Data;
            public DoubleVector3 CenterDirection;
            public double AngularRadiusRadians;
            public Vector3 LocalCenter;
            public double BoundingRadiusMeters;
            public double DistanceMeters;
            public double ProjectedErrorPixels;
            public bool PotentiallyVisible;
            public bool DesiredSplit;
            public bool ChildrenDisplayed;
            public float ChildMorphWeight;
            public float TransitionStartWeight;
            public float TransitionTargetWeight;
            public float TransitionStartTime;
            public bool TransitionActive;
        }

        private sealed class PatchVisual
        {
            public GameObject GameObject;
            public MeshFilter MeshFilter;
            public MeshRenderer MeshRenderer;
            public Mesh Mesh;
            public MaterialPropertyBlock PropertyBlock;
            public Texture2D ControlTexture;
            public PatchNode Owner;
            public Vector3[] ParentVertices;
            public Vector3[] DetailVertices;
            public Vector3[] WorkingVertices;
            public float AppliedMorphWeight;
        }

        [Header("Runtime Ownership")]
        [SerializeField]
        private CelestialSurfaceRuntime surfaceRuntime;

        [SerializeField]
        private CelestialSurfacePatchGenerator patchGenerator;

        [Header("Transition")]
        [SerializeField]
        [Tooltip("Hidden preserves the existing renderer. Surface and LOD Debug enable the new adaptive renderer for comparison.")]
        private CelestialAdaptiveSurfaceRenderMode renderMode;

        [SerializeField]
        private Camera observerCamera;

        [Header("Adaptive Limits")]
        [SerializeField]
        [Range(16, 4096)]
        private int maximumDesiredPatchCount =
            768;

        [SerializeField]
        [Range(1, 32)]
        private int maximumMeshBuildsPerFrame =
            4;

        [SerializeField]
        [Range(0.0f, 5.0f)]
        private float lodMorphDurationSeconds =
            0.35f;

        [SerializeField]
        [Range(0.001f, 0.25f)]
        private float skirtDepthCellFraction =
            0.02f;

        [SerializeField]
        [Min(0.01f)]
        private float minimumSkirtDepthMeters =
            1.0f;

        [Header("Rendering")]
        [SerializeField]
        private bool castShadows;

        [SerializeField]
        private bool receiveShadows =
            true;

        [Header("Runtime")]
        [SerializeField]
        private bool initialized;

        [SerializeField]
        private bool hasObserver;

        [SerializeField]
        private bool coarseSurfaceReady;

        [SerializeField]
        private bool visibleSurfaceReady;

        [SerializeField]
        private int desiredLeafCount;

        [SerializeField]
        private int activePatchCount;

        [SerializeField]
        private int pooledVisualCount;

        [SerializeField]
        private int createdVisualCount;

        [SerializeField]
        private int culledPatchCount;

        [SerializeField]
        private int heldParentCount;

        [SerializeField]
        private int transitioningBranchCount;

        [SerializeField]
        private int maximumActiveLevel;

        [SerializeField]
        private int maximumNeighborLevelDifference;

        [SerializeField]
        private bool neighborBalanceValid;

        [SerializeField]
        private double observerAltitudeMeters;

        [SerializeField]
        private string lastError;

        private readonly PatchNode[] roots =
            new PatchNode[6];
        private readonly Stack<PatchVisual> visualPool =
            new Stack<PatchVisual>();
        private readonly List<PatchNode> desiredLeaves =
            new List<PatchNode>();
        private readonly Dictionary<int, Material> debugMaterials =
            new Dictionary<int, Material>();

        private readonly CelestialSurfaceLayerDefinition[]
            configuredSurfaceLayers =
                new CelestialSurfaceLayerDefinition[
                    MaximumAdaptedLayerCount];

        private Material surfaceMaterial;
        private CelestialSurfaceAppearance
            configuredSurfaceAppearance;
        private int configuredTerrainLayerCount =
            -1;
        private Plane[] frustumPlanes;
        private CelestialSurfaceObserverState observerState;
        private DoubleVector3 observerLocalPosition;
        private int remainingMeshBuilds;
        private bool coverageInvariantValid;
        private int observedCacheVersion;

        public CelestialSurfaceRuntime SurfaceRuntime =>
            surfaceRuntime;

        public CelestialSurfacePatchGenerator PatchGenerator =>
            patchGenerator;

        public CelestialAdaptiveSurfaceRenderMode RenderMode =>
            renderMode;

        public Camera ObserverCamera =>
            observerCamera;

        public bool Initialized =>
            initialized;

        public bool HasObserver =>
            hasObserver;

        public bool CoarseSurfaceReady =>
            coarseSurfaceReady;

        public bool VisibleSurfaceReady =>
            visibleSurfaceReady;

        public int DesiredLeafCount =>
            desiredLeafCount;

        public int ActivePatchCount =>
            activePatchCount;

        public int PooledVisualCount =>
            pooledVisualCount;

        public int CreatedVisualCount =>
            createdVisualCount;

        public int CulledPatchCount =>
            culledPatchCount;

        public int HeldParentCount =>
            heldParentCount;

        public int TransitioningBranchCount =>
            transitioningBranchCount;

        public float LodMorphDurationSeconds =>
            lodMorphDurationSeconds;

        public bool CastShadows =>
            castShadows;

        public bool ReceiveShadows =>
            receiveShadows;

        public int MaximumActiveLevel =>
            maximumActiveLevel;

        public int MaximumNeighborLevelDifference =>
            maximumNeighborLevelDifference;

        public bool NeighborBalanceValid =>
            neighborBalanceValid;

        public bool CoverageInvariantValid =>
            coverageInvariantValid;

        public double ObserverAltitudeMeters =>
            observerAltitudeMeters;

        public string LastError =>
            lastError;

        public bool Initialize(
            CelestialSurfaceRuntime newSurfaceRuntime,
            CelestialSurfacePatchGenerator newPatchGenerator,
            CelestialAdaptiveSurfaceRenderMode initialRenderMode,
            Camera initialObserverCamera)
        {
            ReleaseAllNodes();

            surfaceRuntime =
                newSurfaceRuntime;
            patchGenerator =
                newPatchGenerator;
            renderMode =
                initialRenderMode;
            observerCamera =
                initialObserverCamera;
            initialized =
                surfaceRuntime != null &&
                surfaceRuntime.FoundationReady &&
                patchGenerator != null &&
                patchGenerator.Initialized;

            if (!initialized)
            {
                lastError =
                    "The adaptive renderer requires an initialized surface runtime and patch generator.";
                return false;
            }

            ApplyQualityProfile();
            BuildRoots();
            observedCacheVersion =
                patchGenerator.CacheVersion;
            lastError = string.Empty;
            return true;
        }

        public void SetRenderMode(
            CelestialAdaptiveSurfaceRenderMode newRenderMode)
        {
            if (renderMode ==
                newRenderMode)
            {
                return;
            }

            renderMode =
                newRenderMode;
            RefreshVisualMaterials();

            if (renderMode ==
                CelestialAdaptiveSurfaceRenderMode.Hidden)
            {
                SetAllNodesActive(
                    false);
                ReportReadiness(
                    false,
                    false);
            }
        }

        [ContextMenu("Show Adaptive Surface")]
        private void ShowAdaptiveSurface()
        {
            SetRenderMode(
                CelestialAdaptiveSurfaceRenderMode.Surface);
        }

        [ContextMenu("Show Adaptive LOD Debug")]
        private void ShowAdaptiveLodDebug()
        {
            SetRenderMode(
                CelestialAdaptiveSurfaceRenderMode.LodDebug);
        }

        [ContextMenu("Hide Adaptive Surface")]
        private void HideAdaptiveSurface()
        {
            SetRenderMode(
                CelestialAdaptiveSurfaceRenderMode.Hidden);
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (observedCacheVersion !=
                patchGenerator.CacheVersion)
            {
                using (CacheResetMarker.Auto())
                {
                    ReleaseAllNodes();
                    BuildRoots();
                    observedCacheVersion =
                        patchGenerator.CacheVersion;
                }
            }

            using var requestFrameScope =
                RequestFrameMarker.Auto();

            if (!patchGenerator.BeginRequestFrame())
            {
                lastError =
                    "The adaptive renderer could not begin a shared surface-cache request frame.";
                SetAllNodesActive(
                    false);
                ReportReadiness(
                    patchGenerator.AreRootsReady(),
                    false);
                return;
            }

            try
            {
                UpdateAdaptiveSurface();
            }
            finally
            {
                patchGenerator.EndRequestFrame();
            }
        }

        private void UpdateAdaptiveSurface()
        {

            if (renderMode ==
                CelestialAdaptiveSurfaceRenderMode.Hidden)
            {
                SetAllNodesActive(
                    false);
                ReleaseRootDescendants();
                ClearFrameStatistics();
                ReportReadiness(
                    patchGenerator.AreRootsReady(),
                    false);
                return;
            }

            using (ObserverMarker.Auto())
            {
                ResolveObserverCamera();

                if (observerCamera == null ||
                    !observerCamera.isActiveAndEnabled)
                {
                    hasObserver = false;
                    lastError =
                        "Waiting for an enabled adaptive-surface observer camera.";
                    SetAllNodesActive(
                        false);
                    ReleaseRootDescendants();
                    ClearFrameStatistics();
                    ReportReadiness(
                        patchGenerator.AreRootsReady(),
                        false);
                    return;
                }

                if (!TryBuildObserverState())
                {
                    hasObserver = false;
                    lastError =
                        "The adaptive-surface observer state is invalid.";
                    SetAllNodesActive(
                        false);
                    ReleaseRootDescendants();
                    ClearFrameStatistics();
                    ReportReadiness(
                        patchGenerator.AreRootsReady(),
                        false);
                    return;
                }

                hasObserver = true;
                lastError = string.Empty;
                frustumPlanes =
                    GeometryUtility.CalculateFrustumPlanes(
                        observerCamera);
            }
            remainingMeshBuilds =
                Mathf.Max(
                    1,
                    maximumMeshBuildsPerFrame);
            ClearFrameStatistics();
            desiredLeafCount =
                roots.Length;

            using (PrepareTreeMarker.Auto())
            {
                for (var index = 0;
                    index < roots.Length;
                    index++)
                {
                    PrepareDesiredTree(
                        roots[index]);
                }
            }

            using (BalanceTreeMarker.Auto())
            {
                BalanceDesiredTree();
            }

            using (EvaluateBalanceMarker.Auto())
            {
                EvaluateNeighborBalance();
            }

            try
            {
                using (MaterialsMarker.Auto())
                {
                    EnsureRenderMaterials();
                }
            }
            catch (Exception exception)
            {
                lastError =
                    exception.GetBaseException().Message;
                SetAllNodesActive(
                    false);
                ReportReadiness(
                    false,
                    false);
                return;
            }

            coverageInvariantValid = true;

            using (ApplyTreeMarker.Auto())
            {
                for (var index = 0;
                    index < roots.Length;
                    index++)
                {
                    ApplyDesiredTree(
                        roots[index]);
                }
            }

            using (StatisticsMarker.Auto())
            {
                RefreshActiveStatistics();
                pooledVisualCount =
                    visualPool.Count;
                coarseSurfaceReady =
                    AreAllRootsReady();
                visibleSurfaceReady =
                    activePatchCount > 0;
                ReportReadiness(
                    coarseSurfaceReady,
                    visibleSurfaceReady);
            }
        }

        private void ApplyQualityProfile()
        {
            var quality =
                surfaceRuntime.QualityProfile;

            if (quality == null)
            {
                return;
            }

            maximumDesiredPatchCount =
                Mathf.Max(
                    16,
                    quality.AdaptiveMaximumPatchCount);
            maximumMeshBuildsPerFrame =
                Mathf.Max(
                    1,
                    quality.AdaptiveMaximumMeshBuildsPerFrame);
            lodMorphDurationSeconds =
                Mathf.Clamp(
                    quality.AdaptiveLodMorphDurationSeconds,
                    0.0f,
                    5.0f);
            skirtDepthCellFraction =
                Mathf.Clamp(
                    quality.AdaptiveSkirtDepthCellFraction,
                    0.001f,
                    0.25f);
            minimumSkirtDepthMeters =
                Mathf.Max(
                    0.01f,
                    quality.AdaptiveMinimumSkirtDepthMeters);
            castShadows =
                quality.AdaptiveCastShadows;
            receiveShadows =
                quality.AdaptiveReceiveShadows;
        }

        private void BuildRoots()
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                if (!surfaceRuntime.TryGetRootPatch(
                        index,
                        out var address))
                {
                    continue;
                }

                roots[index] =
                    CreateNode(
                        address,
                        null);
            }
        }

        private PatchNode CreateNode(
            CubeSpherePatchAddress address,
            PatchNode parent)
        {
            var centerAddress =
                new CubeSphereAddress(
                    address.Face,
                    (address.MinimumU +
                        address.MaximumU) *
                        0.5,
                    (address.MinimumV +
                        address.MaximumV) *
                        0.5,
                    0.0);
            var centerDirection =
                CubeSphereMapping.AddressToDirection(
                    centerAddress);
            var angularRadius =
                CalculateAngularRadius(
                    address,
                    centerDirection);

            return new PatchNode
            {
                Address =
                    address,
                Parent =
                    parent,
                CenterDirection =
                    centerDirection,
                AngularRadiusRadians =
                    angularRadius,
                ChildMorphWeight =
                    0.0f,
                TransitionTargetWeight =
                    0.0f
            };
        }

        private void PrepareDesiredTree(
            PatchNode node)
        {
            if (node == null)
            {
                return;
            }

            RefreshPatchData(
                node);
            UpdateSpatialState(
                node);

            patchGenerator.RequestPatch(
                node.Address,
                ResolveGenerationPriority(
                    node),
                ResolveRequestClass(
                    node));

            var wasSplit =
                node.DesiredSplit;
            var collisionRequiresSplit =
                surfaceRuntime.CollisionRuntime != null &&
                surfaceRuntime.CollisionRuntime.RequiresVisualRefinement(node.Address);
            var shouldSplit =
                node.PotentiallyVisible &&
                (collisionRequiresSplit || ShouldSubdivide(
                    node,
                    wasSplit)) &&
                desiredLeafCount + 3 <=
                    Mathf.Max(
                        16,
                        maximumDesiredPatchCount);

            node.DesiredSplit =
                shouldSplit;

            if (!shouldSplit)
            {
                return;
            }

            EnsureChildren(
                node);
            desiredLeafCount += 3;

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                PrepareDesiredTree(
                    node.Children[index]);
            }
        }

        private void RefreshPatchData(
            PatchNode node)
        {
            if (node.Data == null &&
                patchGenerator.TryGetPatchData(
                    node.Address,
                    out var data))
            {
                node.Data = data;
            }
        }

        private void UpdateSpatialState(
            PatchNode node)
        {
            var radius =
                surfaceRuntime.BodyDefinition
                    .ReferenceRadiusMeters;
            var minimumElevation =
                node.Data != null
                    ? node.Data.MinimumElevationMeters
                    : -DefaultHeightEnvelopeMeters;
            var maximumElevation =
                node.Data != null
                    ? node.Data.MaximumElevationMeters
                    : DefaultHeightEnvelopeMeters;
            var centerElevation =
                (minimumElevation +
                    maximumElevation) *
                0.5;
            var centerRadius =
                radius +
                centerElevation;
            var surfaceChordRadius =
                2.0 *
                Math.Max(
                    1.0,
                    radius +
                        maximumElevation) *
                Math.Sin(
                    node.AngularRadiusRadians *
                        0.5);
            var elevationRadius =
                Math.Abs(
                    maximumElevation -
                        minimumElevation) *
                0.5;

            node.LocalCenter =
                ToVector3(
                    node.CenterDirection *
                        centerRadius);
            node.BoundingRadiusMeters =
                surfaceChordRadius +
                elevationRadius +
                ResolveSkirtDepthMeters(
                    node.Address);

            var offset =
                observerLocalPosition -
                ToDoubleVector3(
                    node.LocalCenter);
            node.DistanceMeters =
                Math.Max(
                    1.0,
                    Magnitude(offset) -
                        node.BoundingRadiusMeters);
            var arcSize =
                CubeSpherePatchGrid
                    .GetApproximateArcSizeMeters(
                        node.Address,
                        radius);
            var cellSize =
                arcSize /
                Math.Max(
                    1,
                    surfaceRuntime.PatchResolution -
                        1);
            var conservativeError =
                cellSize *
                CurvatureErrorFraction;

            if (node.Data != null)
            {
                conservativeError =
                    Math.Max(
                        conservativeError,
                        node.Data
                            .GeometricErrorMeters);
            }

            node.ProjectedErrorPixels =
                surfaceRuntime.LodPolicy
                    .EstimateProjectedErrorPixels(
                        conservativeError,
                        node.DistanceMeters,
                        observerState);
            node.PotentiallyVisible =
                IsInsideHorizon(
                    node,
                    radius) &&
                IsInsideFrustum(
                    node);

            if (!node.PotentiallyVisible)
            {
                culledPatchCount++;
            }
        }

        private bool ShouldSubdivide(
            PatchNode node,
            bool wasSplit)
        {
            var policy =
                surfaceRuntime.LodPolicy;

            if (node.Data == null ||
                node.Address.Level >=
                    policy.MaximumLevel)
            {
                return false;
            }

            if (node.Address.Level <
                policy.MinimumLevel)
            {
                return true;
            }

            var threshold =
                policy.MaximumScreenErrorPixels;

            if (wasSplit)
            {
                threshold *=
                    1.0 -
                    policy.HysteresisFraction;
            }

            return
                node.ProjectedErrorPixels >
                threshold;
        }

        private void EnsureChildren(
            PatchNode node)
        {
            if (node.Children != null)
            {
                return;
            }

            node.Children =
                new PatchNode[4];

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                if (!node.Address.TryGetChild(
                        (CubeSpherePatchQuadrant)index,
                        out var childAddress))
                {
                    continue;
                }

                node.Children[index] =
                    CreateNode(
                        childAddress,
                        node);
            }
        }

        private void BalanceDesiredTree()
        {
            var maximumLeafCount =
                Mathf.Max(
                    16,
                    maximumDesiredPatchCount);
            var changed = true;

            while (changed &&
                desiredLeafCount + 3 <=
                    maximumLeafCount)
            {
                changed = false;
                CollectDesiredLeaves();

                for (var leafIndex = 0;
                    leafIndex < desiredLeaves.Count &&
                    !changed;
                    leafIndex++)
                {
                    var leaf =
                        desiredLeaves[leafIndex];

                    for (var edgeIndex = 0;
                        edgeIndex < Edges.Length;
                        edgeIndex++)
                    {
                        if (!CubeSpherePatchTopology.TryGetNeighbor(
                                leaf.Address,
                                Edges[edgeIndex],
                                out var neighborAddress))
                        {
                            continue;
                        }

                        var coveringNeighbor =
                            FindCoveringDesiredLeaf(
                                neighborAddress);

                        if (coveringNeighbor == null ||
                            leaf.Address.Level -
                                coveringNeighbor.Address.Level <=
                                surfaceRuntime.LodPolicy
                                    .MaximumNeighborLevelDifference)
                        {
                            continue;
                        }

                        ForceBalancedSplit(
                            coveringNeighbor);
                        desiredLeafCount += 3;
                        changed = true;
                        break;
                    }
                }
            }
        }

        private void ForceBalancedSplit(
            PatchNode node)
        {
            node.DesiredSplit = true;
            EnsureChildren(
                node);

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                var child =
                    node.Children[index];

                if (child == null)
                {
                    continue;
                }

                child.DesiredSplit = false;
                RefreshPatchData(
                    child);
                UpdateSpatialState(
                    child);
                patchGenerator.RequestPatch(
                    child.Address,
                    ResolveGenerationPriority(
                        child),
                    CelestialSurfacePatchRequestClass
                        .Coverage);
            }
        }

        private static CelestialSurfacePatchRequestClass ResolveRequestClass(
            PatchNode node)
        {
            if (!node.PotentiallyVisible)
            {
                return
                    CelestialSurfacePatchRequestClass
                        .Prefetch;
            }

            return
                node.Address.IsRoot
                    ? CelestialSurfacePatchRequestClass
                        .Coverage
                    : CelestialSurfacePatchRequestClass
                        .Visible;
        }

        private double ResolveGenerationPriority(
            PatchNode node)
        {
            if (node.Address.IsRoot)
            {
                return
                    1000000000.0 +
                    (node.PotentiallyVisible
                        ? 1000000.0
                        : 0.0) -
                    (int)node.Address.Face;
            }

            var projectedError =
                IsFinite(
                    node.ProjectedErrorPixels)
                    ? Math.Min(
                        100000.0,
                        Math.Max(
                            0.0,
                            node.ProjectedErrorPixels))
                    : 100000.0;
            var radius =
                surfaceRuntime.BodyDefinition
                    .ReferenceRadiusMeters;

            return
                500000000.0 -
                node.Address.Level *
                    10000000.0 +
                projectedError *
                    10.0 +
                -node.DistanceMeters /
                    Math.Max(
                        1.0,
                        radius);
        }

        private PatchNode FindCoveringDesiredLeaf(
            CubeSpherePatchAddress target)
        {
            var node =
                GetRoot(
                    target.Face);

            while (node != null &&
                node.DesiredSplit &&
                node.Children != null &&
                node.Address.Level <
                    target.Level)
            {
                var childLevel =
                    node.Address.Level + 1;
                var shift =
                    target.Level -
                    childLevel;
                var positiveU =
                    ((target.X >> shift) &
                        1) != 0;
                var positiveV =
                    ((target.Y >> shift) &
                        1) != 0;
                var childIndex =
                    ResolveChildIndex(
                        positiveU,
                        positiveV);
                node =
                    node.Children[childIndex];
            }

            return node;
        }

        private void EvaluateNeighborBalance()
        {
            CollectDesiredLeaves();
            maximumNeighborLevelDifference = 0;

            for (var leafIndex = 0;
                leafIndex < desiredLeaves.Count;
                leafIndex++)
            {
                var leaf =
                    desiredLeaves[leafIndex];

                for (var edgeIndex = 0;
                    edgeIndex < Edges.Length;
                    edgeIndex++)
                {
                    if (!CubeSpherePatchTopology.TryGetNeighbor(
                            leaf.Address,
                            Edges[edgeIndex],
                            out var neighborAddress))
                    {
                        continue;
                    }

                    var neighbor =
                        FindCoveringDesiredLeaf(
                            neighborAddress);

                    if (neighbor == null)
                    {
                        continue;
                    }

                    maximumNeighborLevelDifference =
                        Math.Max(
                            maximumNeighborLevelDifference,
                            Math.Abs(
                                leaf.Address.Level -
                                neighbor.Address.Level));
                }
            }

            neighborBalanceValid =
                maximumNeighborLevelDifference <=
                surfaceRuntime.LodPolicy
                    .MaximumNeighborLevelDifference;
        }

        private void CollectDesiredLeaves()
        {
            desiredLeaves.Clear();

            for (var index = 0;
                index < roots.Length;
                index++)
            {
                CollectDesiredLeaves(
                    roots[index]);
            }
        }

        private void CollectDesiredLeaves(
            PatchNode node)
        {
            if (node == null)
            {
                return;
            }

            if (!node.DesiredSplit ||
                node.Children == null)
            {
                desiredLeaves.Add(
                    node);
                return;
            }

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                CollectDesiredLeaves(
                    node.Children[index]);
            }
        }

        private void ApplyDesiredTree(
            PatchNode node)
        {
            if (node == null)
            {
                return;
            }

            if (!node.PotentiallyVisible)
            {
                SetSubtreeActive(
                    node,
                    false);
                ResetChildTransition(
                    node);
                ReleaseChildren(
                    node);
                return;
            }

            EnsureVisual(
                node);

            if (node.DesiredSplit &&
                node.Children != null)
            {
                var childrenReady =
                    EnsureDirectChildrenReady(
                        node);

                if (childrenReady)
                {
                    node.ChildrenDisplayed =
                        true;
                    SetTransitionTarget(
                        node,
                        1.0f);
                    UpdateChildTransition(
                        node);
                    SetVisualActive(
                        node,
                        false);

                    if (node.TransitionActive)
                    {
                        transitioningBranchCount++;
                        ShowDirectChildren(
                            node);
                        return;
                    }

                    for (var index = 0;
                        index < node.Children.Length;
                        index++)
                    {
                        ApplyVisualMorph(
                            node.Children[index],
                            1.0f);
                        ApplyDesiredTree(
                            node.Children[index]);
                    }

                    return;
                }

                heldParentCount++;
                coverageInvariantValid &=
                    node.Visual != null;
                SetVisualActive(
                    node,
                    true);

                for (var index = 0;
                    index < node.Children.Length;
                    index++)
                {
                    SetSubtreeActive(
                        node.Children[index],
                        false);
                }

                return;
            }

            if (node.ChildrenDisplayed &&
                node.Children != null &&
                EnsureDirectChildrenReady(
                    node))
            {
                if (HasDisplayedDescendants(
                        node) &&
                    !CollapseDisplayedDescendants(
                        node))
                {
                    SetVisualActive(
                        node,
                        false);
                    return;
                }

                SetTransitionTarget(
                    node,
                    0.0f);
                UpdateChildTransition(
                    node);

                if (node.TransitionActive ||
                    node.ChildMorphWeight > 0.0f)
                {
                    transitioningBranchCount++;
                    SetVisualActive(
                        node,
                        false);
                    ShowDirectChildren(
                        node);
                    return;
                }

                node.ChildrenDisplayed =
                    false;
            }

            coverageInvariantValid &=
                node.Visual != null;
            SetVisualActive(
                node,
                true);
            ReleaseChildren(
                node);
        }

        private static bool HasDisplayedDescendants(
            PatchNode node)
        {
            if (node == null ||
                node.Children == null)
            {
                return false;
            }

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                if (node.Children[index] != null &&
                    node.Children[index]
                        .ChildrenDisplayed)
                {
                    return true;
                }
            }

            return false;
        }

        private bool CollapseDisplayedDescendants(
            PatchNode node)
        {
            var descendantsCollapsed = true;

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                var child =
                    node.Children[index];

                if (child == null ||
                    child.Visual == null)
                {
                    coverageInvariantValid = false;
                    descendantsCollapsed = false;
                    continue;
                }

                if (child.ChildrenDisplayed &&
                    child.Children != null)
                {
                    descendantsCollapsed &=
                        CollapseDisplayedBranch(
                            child);
                    continue;
                }

                ApplyVisualMorph(
                    child,
                    1.0f);
                SetSubtreeActive(
                    child,
                    false);
                coverageInvariantValid &=
                    child.Visual != null;
                SetVisualActive(
                    child,
                    true);
            }

            return descendantsCollapsed;
        }

        private bool CollapseDisplayedBranch(
            PatchNode node)
        {
            if (node.Children == null ||
                !node.ChildrenDisplayed)
            {
                ApplyVisualMorph(
                    node,
                    1.0f);
                SetVisualActive(
                    node,
                    true);
                return true;
            }

            if (HasDisplayedDescendants(
                    node) &&
                !CollapseDisplayedDescendants(
                    node))
            {
                SetVisualActive(
                    node,
                    false);
                return false;
            }

            SetTransitionTarget(
                node,
                0.0f);
            UpdateChildTransition(
                node);

            if (node.TransitionActive ||
                node.ChildMorphWeight > 0.0f)
            {
                transitioningBranchCount++;
                SetVisualActive(
                    node,
                    false);
                ShowDirectChildren(
                    node);
                return false;
            }

            node.ChildrenDisplayed = false;
            ApplyVisualMorph(
                node,
                1.0f);
            SetVisualActive(
                node,
                true);
            ReleaseChildren(
                node);
            return true;
        }

        private bool EnsureDirectChildrenReady(
            PatchNode node)
        {
            if (node.Children == null)
            {
                return false;
            }

            var childrenReady = true;

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                var child =
                    node.Children[index];
                RefreshPatchData(
                    child);
                EnsureVisual(
                    child);
                childrenReady &=
                    child != null &&
                    child.Visual != null;
            }

            return childrenReady;
        }

        private void ShowDirectChildren(
            PatchNode node)
        {
            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                var child =
                    node.Children[index];
                ApplyVisualMorph(
                    child,
                    node.ChildMorphWeight);
                SetSubtreeActive(
                    child,
                    false);
                coverageInvariantValid &=
                    child != null &&
                    child.Visual != null;
                SetVisualActive(
                    child,
                    true);
            }
        }

        private void SetTransitionTarget(
            PatchNode node,
            float targetWeight)
        {
            targetWeight =
                Mathf.Clamp01(
                    targetWeight);

            if (Mathf.Approximately(
                    node.TransitionTargetWeight,
                    targetWeight) &&
                (node.TransitionActive ||
                Mathf.Approximately(
                    node.ChildMorphWeight,
                    targetWeight)))
            {
                return;
            }

            node.TransitionStartWeight =
                node.ChildMorphWeight;
            node.TransitionTargetWeight =
                targetWeight;
            node.TransitionStartTime =
                Time.unscaledTime;
            node.TransitionActive =
                lodMorphDurationSeconds > 0.0f &&
                !Mathf.Approximately(
                    node.TransitionStartWeight,
                    node.TransitionTargetWeight);

            if (!node.TransitionActive)
            {
                node.ChildMorphWeight =
                    targetWeight;
            }
        }

        private void UpdateChildTransition(
            PatchNode node)
        {
            if (!node.TransitionActive)
            {
                node.ChildMorphWeight =
                    node.TransitionTargetWeight;
                return;
            }

            var elapsed =
                Time.unscaledTime -
                node.TransitionStartTime;
            var linearWeight =
                lodMorphDurationSeconds > 0.0f
                    ? Mathf.Clamp01(
                        elapsed /
                        lodMorphDurationSeconds)
                    : 1.0f;
            var smoothWeight =
                linearWeight *
                linearWeight *
                (3.0f -
                    2.0f * linearWeight);
            node.ChildMorphWeight =
                Mathf.Lerp(
                    node.TransitionStartWeight,
                    node.TransitionTargetWeight,
                    smoothWeight);

            if (linearWeight >= 1.0f)
            {
                node.ChildMorphWeight =
                    node.TransitionTargetWeight;
                node.TransitionActive =
                    false;
            }
        }

        private static void ResetChildTransition(
            PatchNode node)
        {
            node.ChildrenDisplayed = false;
            node.ChildMorphWeight = 0.0f;
            node.TransitionStartWeight = 0.0f;
            node.TransitionTargetWeight = 0.0f;
            node.TransitionActive = false;
        }

        private static void ApplyVisualMorph(
            PatchNode node,
            float morphWeight)
        {
            if (node == null ||
                node.Visual == null)
            {
                return;
            }

            var visual =
                node.Visual;
            morphWeight =
                Mathf.Clamp01(
                    morphWeight);

            if (visual.ParentVertices == null ||
                visual.DetailVertices == null ||
                visual.WorkingVertices == null ||
                visual.ParentVertices.Length !=
                    visual.DetailVertices.Length ||
                visual.WorkingVertices.Length !=
                    visual.DetailVertices.Length ||
                Mathf.Approximately(
                    visual.AppliedMorphWeight,
                    morphWeight))
            {
                return;
            }

            for (var index = 0;
                index < visual.WorkingVertices.Length;
                index++)
            {
                visual.WorkingVertices[index] =
                    Vector3.LerpUnclamped(
                        visual.ParentVertices[index],
                        visual.DetailVertices[index],
                        morphWeight);
            }

            visual.Mesh.vertices =
                visual.WorkingVertices;
            visual.Mesh.RecalculateBounds();
            visual.AppliedMorphWeight =
                morphWeight;
        }

        private static DoubleVector3 ResolveParentPosition(
            PatchNode node,
            int sampleX,
            int sampleY,
            DoubleVector3 detailPosition,
            double radiusMeters)
        {
            if (node == null ||
                node.Parent == null ||
                node.Parent.Data == null ||
                node.Parent.Visual == null ||
                node.Parent.Visual.DetailVertices == null ||
                node.Data == null ||
                node.Data.Resolution < 2 ||
                node.Parent.Data.Resolution < 2)
            {
                return detailPosition;
            }

            var normalizedX =
                sampleX /
                (double)(node.Data.Resolution - 1);
            var normalizedY =
                sampleY /
                (double)(node.Data.Resolution - 1);
            var parentX =
                ((node.Address.X & 1) == 0
                    ? 0.0
                    : 0.5) +
                normalizedX * 0.5;
            var parentY =
                ((node.Address.Y & 1) == 0
                    ? 0.0
                    : 0.5) +
                normalizedY * 0.5;
            var parentResolution =
                node.Parent.Data.Resolution;
            var parentSampleX =
                parentX *
                (parentResolution - 1);
            var parentSampleY =
                parentY *
                (parentResolution - 1);
            var lowerX =
                Math.Min(
                    (int)Math.Floor(
                        parentSampleX),
                    parentResolution - 2);
            var lowerY =
                Math.Min(
                    (int)Math.Floor(
                        parentSampleY),
                    parentResolution - 2);
            var blendX =
                parentSampleX -
                    lowerX;
            var blendY =
                parentSampleY -
                    lowerY;
            var parentVertices =
                node.Parent.Visual.DetailVertices;
            var expectedCoreVertexCount =
                parentResolution *
                    parentResolution;

            if (parentVertices.Length <
                expectedCoreVertexCount)
            {
                return detailPosition;
            }

            var lowerLeft =
                ToDoubleVector3(
                    parentVertices[
                        lowerY * parentResolution +
                        lowerX]);
            var lowerRight =
                ToDoubleVector3(
                    parentVertices[
                        lowerY * parentResolution +
                        lowerX + 1]);
            var upperLeft =
                ToDoubleVector3(
                    parentVertices[
                        (lowerY + 1) *
                            parentResolution +
                        lowerX]);
            DoubleVector3 interpolated;

            if (blendX + blendY <= 1.0)
            {
                interpolated =
                    lowerLeft +
                    (lowerRight - lowerLeft) *
                        blendX +
                    (upperLeft - lowerLeft) *
                        blendY;
            }
            else
            {
                var upperRight =
                    ToDoubleVector3(
                        parentVertices[
                            (lowerY + 1) *
                                parentResolution +
                            lowerX + 1]);
                interpolated =
                    upperRight +
                    (upperLeft - upperRight) *
                        (1.0 - blendX) +
                    (lowerRight - upperRight) *
                        (1.0 - blendY);
            }

            return interpolated +
                node.Parent.CenterDirection *
                    radiusMeters;
        }

        private void EnsureVisual(
            PatchNode node)
        {
            if (node == null ||
                node.Visual != null ||
                node.Data == null ||
                remainingMeshBuilds <= 0)
            {
                return;
            }

            var visual =
                AcquireVisual();

            try
            {
                using (BuildVisualMarker.Auto())
                {
                    BuildPatchVisual(
                        visual,
                        node);
                }
                node.Visual =
                    visual;
                visual.Owner =
                    node;
                remainingMeshBuilds--;
            }
            catch (Exception exception)
            {
                ReleaseVisual(
                    visual);
                lastError =
                    $"{node.Address}: {exception.GetBaseException().Message}";
            }
        }

        private PatchVisual AcquireVisual()
        {
            if (visualPool.Count > 0)
            {
                return visualPool.Pop();
            }

            var patchObject =
                new GameObject(
                    "Adaptive Surface Patch");
            patchObject.transform.SetParent(
                transform,
                false);
            var meshFilter =
                patchObject.AddComponent<MeshFilter>();
            var meshRenderer =
                patchObject.AddComponent<MeshRenderer>();
            var mesh =
                new Mesh
                {
                    name =
                        "Adaptive Surface Patch Mesh",
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            mesh.MarkDynamic();

            meshFilter.sharedMesh =
                mesh;
            patchObject.SetActive(
                false);
            createdVisualCount++;

            return new PatchVisual
            {
                GameObject =
                    patchObject,
                MeshFilter =
                    meshFilter,
                MeshRenderer =
                    meshRenderer,
                Mesh =
                    mesh,
                PropertyBlock =
                    new MaterialPropertyBlock()
            };
        }

        private void BuildPatchVisual(
            PatchVisual visual,
            PatchNode node)
        {
            var geometryScope =
                BuildGeometryMarker.Auto();
            var geometryScopeActive =
                true;

            try
            {
            var data =
                node.Data;
            var resolution =
                data.Resolution;
            var ringCount =
                4 *
                (resolution - 1);
            var coreVertexCount =
                resolution *
                resolution;
            var vertices =
                new Vector3[
                    coreVertexCount +
                    ringCount];
            var parentVertices =
                new Vector3[
                    vertices.Length];
            var detailVertices =
                new Vector3[
                    vertices.Length];
            var normals =
                new Vector3[vertices.Length];
            var uv =
                new Vector2[vertices.Length];
            var coreTriangleIndexCount =
                (resolution - 1) *
                (resolution - 1) *
                6;
            // Skirt quads are intentionally double-sided. Very sharp relief can
            // twist an edge quad far enough for one half to reverse its facing;
            // rendering the reverse winding keeps the seam covered without
            // disabling back-face culling on the terrain surface itself.
            var skirtTriangleIndexCount =
                ringCount *
                12;
            var triangles =
                new int[
                    coreTriangleIndexCount +
                    skirtTriangleIndexCount];
            var radius =
                surfaceRuntime.BodyDefinition
                    .ReferenceRadiusMeters;
            var referencePosition =
                node.CenterDirection *
                radius;
            for (var sampleY = 0;
                sampleY < resolution;
                sampleY++)
            {
                for (var sampleX = 0;
                    sampleX < resolution;
                    sampleX++)
                {
                    if (!CubeSpherePatchGrid.TryGetSampleDirection(
                            node.Address,
                            resolution,
                            sampleX,
                            sampleY,
                            out var direction))
                    {
                        throw new InvalidOperationException(
                            "Could not resolve an adaptive patch sample direction.");
                    }

                    var elevation =
                        data.GetElevationMeters(
                            sampleX,
                            sampleY);
                    var vertexIndex =
                        sampleY *
                            resolution +
                        sampleX;
                    var detailPosition =
                        direction *
                            (radius + elevation);
                    var parentPosition =
                        ResolveParentPosition(
                            node,
                            sampleX,
                            sampleY,
                            detailPosition,
                            radius);
                    var detailDelta =
                        detailPosition -
                            referencePosition;
                    var parentDelta =
                        parentPosition -
                            referencePosition;

                    detailVertices[vertexIndex] =
                        ToVector3(
                            detailDelta);
                    parentVertices[vertexIndex] =
                        ToVector3(
                            parentDelta);
                    vertices[vertexIndex] =
                        detailVertices[vertexIndex];
                    normals[vertexIndex] =
                        ToVector3(
                            direction).normalized;
                    uv[vertexIndex] =
                        new Vector2(
                            sampleX /
                                (float)(resolution - 1),
                            sampleY /
                                (float)(resolution - 1));
                }
            }

            var triangleIndex = 0;

            for (var sampleY = 0;
                sampleY < resolution - 1;
                sampleY++)
            {
                for (var sampleX = 0;
                    sampleX < resolution - 1;
                    sampleX++)
                {
                    var lowerLeft =
                        sampleY *
                            resolution +
                        sampleX;
                    var lowerRight =
                        lowerLeft +
                        1;
                    var upperLeft =
                        lowerLeft +
                        resolution;
                    var upperRight =
                        upperLeft +
                        1;

                    triangles[triangleIndex++] =
                        lowerLeft;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        upperLeft;
                    triangles[triangleIndex++] =
                        lowerRight;
                    triangles[triangleIndex++] =
                        upperRight;
                    triangles[triangleIndex++] =
                        upperLeft;
                }
            }

            var boundary =
                BuildBoundaryRing(
                    resolution);
            var skirtDepth =
                ResolveSkirtDepthMeters(
                    node.Address);
            var maximumParentDisplacement =
                0.0;

            for (var ringIndex = 0;
                ringIndex < boundary.Length;
                ringIndex++)
            {
                var coreIndex =
                    boundary[ringIndex];
                var displacement =
                    Magnitude(
                        ToDoubleVector3(
                            detailVertices[coreIndex]) -
                        ToDoubleVector3(
                            parentVertices[coreIndex]));

                maximumParentDisplacement =
                    Math.Max(
                        maximumParentDisplacement,
                        displacement);
            }

            // A fixed fraction of cell size is enough for smooth terrain, but
            // not for deliberately aggressive high-frequency relief. Extend
            // the skirt beyond the exact detail-to-parent edge displacement so
            // it continues to overlap the neighboring coarse patch throughout
            // the entire morph.
            skirtDepth =
                Math.Max(
                    skirtDepth,
                    maximumParentDisplacement *
                        1.25 +
                    minimumSkirtDepthMeters);

            for (var ringIndex = 0;
                ringIndex < boundary.Length;
                ringIndex++)
            {
                var coreIndex =
                    boundary[ringIndex];
                var skirtIndex =
                    coreVertexCount +
                    ringIndex;
                var detailCorePosition =
                    ToDoubleVector3(
                        detailVertices[coreIndex]) +
                    referencePosition;
                var direction =
                    Normalize(
                        detailCorePosition);
                var detailSkirtPosition =
                    detailCorePosition -
                        direction *
                            skirtDepth -
                    referencePosition;
                var parentCorePosition =
                    ToDoubleVector3(
                        parentVertices[coreIndex]) +
                    referencePosition;
                var parentDirection =
                    Normalize(
                        parentCorePosition);
                var parentSkirtPosition =
                    parentCorePosition -
                        parentDirection *
                            skirtDepth -
                    referencePosition;

                detailVertices[skirtIndex] =
                    ToVector3(
                        detailSkirtPosition);
                parentVertices[skirtIndex] =
                    ToVector3(
                        parentSkirtPosition);
                vertices[skirtIndex] =
                    detailVertices[skirtIndex];
                normals[skirtIndex] =
                    normals[coreIndex];
                uv[skirtIndex] =
                    uv[coreIndex];
            }

            for (var ringIndex = 0;
                ringIndex < boundary.Length;
                ringIndex++)
            {
                var nextRingIndex =
                    (ringIndex + 1) %
                    boundary.Length;
                var coreFirst =
                    boundary[ringIndex];
                var coreSecond =
                    boundary[nextRingIndex];
                var skirtFirst =
                    coreVertexCount +
                    ringIndex;
                var skirtSecond =
                    coreVertexCount +
                    nextRingIndex;

                triangles[triangleIndex++] =
                    coreFirst;
                triangles[triangleIndex++] =
                    skirtFirst;
                triangles[triangleIndex++] =
                    coreSecond;
                triangles[triangleIndex++] =
                    coreSecond;
                triangles[triangleIndex++] =
                    skirtFirst;
                triangles[triangleIndex++] =
                    skirtSecond;

                triangles[triangleIndex++] =
                    coreSecond;
                triangles[triangleIndex++] =
                    skirtFirst;
                triangles[triangleIndex++] =
                    coreFirst;
                triangles[triangleIndex++] =
                    skirtSecond;
                triangles[triangleIndex++] =
                    skirtFirst;
                triangles[triangleIndex++] =
                    coreSecond;
            }

            geometryScope.Dispose();
            geometryScopeActive =
                false;

            using (UploadMeshMarker.Auto())
            {
                visual.Mesh.Clear();
                visual.Mesh.name =
                    $"Adaptive {node.Address}";
                visual.Mesh.indexFormat =
                    vertices.Length >
                        65535
                        ? IndexFormat.UInt32
                        : IndexFormat.UInt16;
                visual.Mesh.vertices =
                    vertices;
                visual.Mesh.normals =
                    normals;
                visual.Mesh.uv =
                    uv;
                visual.Mesh.triangles =
                    triangles;
                visual.Mesh.RecalculateBounds();
            }

            using (RecalculateTangentsMarker.Auto())
            {
                visual.Mesh.RecalculateTangents();
            }
            visual.ParentVertices =
                parentVertices;
            visual.DetailVertices =
                detailVertices;
            visual.WorkingVertices =
                vertices;
            visual.AppliedMorphWeight =
                1.0f;

            visual.GameObject.name =
                $"Adaptive {node.Address}";
            visual.GameObject.transform.localPosition =
                ToVector3(
                    referencePosition);
            visual.GameObject.transform.localRotation =
                Quaternion.identity;
            visual.GameObject.transform.localScale =
                Vector3.one;
            visual.MeshRenderer.shadowCastingMode =
                castShadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
            visual.MeshRenderer.receiveShadows =
                receiveShadows;

            using (ConfigureMaterialMarker.Auto())
            {
                ConfigureVisualMaterial(
                    visual,
                    node);
            }
            }
            finally
            {
                if (geometryScopeActive)
                {
                    geometryScope.Dispose();
                }
            }
        }

        private static int[] BuildBoundaryRing(
            int resolution)
        {
            var boundary =
                new int[
                    4 *
                    (resolution - 1)];
            var index = 0;

            for (var x = 0;
                x < resolution;
                x++)
            {
                boundary[index++] = x;
            }

            for (var y = 1;
                y < resolution;
                y++)
            {
                boundary[index++] =
                    y *
                        resolution +
                    resolution -
                    1;
            }

            for (var x = resolution - 2;
                x >= 0;
                x--)
            {
                boundary[index++] =
                    (resolution - 1) *
                        resolution +
                    x;
            }

            for (var y = resolution - 2;
                y > 0;
                y--)
            {
                boundary[index++] =
                    y *
                    resolution;
            }

            return boundary;
        }

        private void ConfigureVisualMaterial(
            PatchVisual visual,
            PatchNode node)
        {
            DestroyControlTexture(
                visual);
            visual.PropertyBlock.Clear();

            if (renderMode ==
                CelestialAdaptiveSurfaceRenderMode.LodDebug)
            {
                visual.MeshRenderer.sharedMaterial =
                    GetDebugMaterial(
                        node.Address);
                visual.MeshRenderer.SetPropertyBlock(
                    visual.PropertyBlock);
                return;
            }

            visual.MeshRenderer.sharedMaterial =
                surfaceMaterial;
            var data =
                node.Data;

            if (data.HasSurfaceControlData)
            {
                visual.ControlTexture =
                    CreateControlTexture(
                        data);
                visual.PropertyBlock.SetTexture(
                    "_Control",
                    visual.ControlTexture);
                visual.PropertyBlock.SetFloat(
                    "_LayerCount",
                    data.SurfaceLayerCount);
                ConfigurePatchTextureTransforms(
                    visual.PropertyBlock,
                    node.Address);
            }
            else
            {
                visual.PropertyBlock.SetFloat(
                    "_LayerCount",
                    0.0f);
            }

            visual.PropertyBlock.SetFloat(
                "_TileFade",
                1.0f);
            visual.PropertyBlock.SetFloat(
                "_LodMaskMode",
                0.0f);
            visual.MeshRenderer.SetPropertyBlock(
                visual.PropertyBlock);
        }

        private Texture2D CreateControlTexture(
            CelestialSurfacePatchData data)
        {
            var texture =
                new Texture2D(
                    data.Resolution,
                    data.Resolution,
                    TextureFormat.RGBA32,
                    false,
                    true)
                {
                    name =
                        $"Adaptive Control {data.Address}",
                    wrapMode =
                        TextureWrapMode.Clamp,
                    filterMode =
                        FilterMode.Bilinear,
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            var colors =
                new Color[
                    data.SampleCount];

            for (var y = 0;
                y < data.Resolution;
                y++)
            {
                for (var x = 0;
                    x < data.Resolution;
                    x++)
                {
                    var sampleIndex =
                        y *
                            data.Resolution +
                        x;
                    colors[sampleIndex] =
                        new Color(
                            GetControlWeight(
                                data,
                                x,
                                y,
                                0),
                            GetControlWeight(
                                data,
                                x,
                                y,
                                1),
                            GetControlWeight(
                                data,
                                x,
                                y,
                                2),
                            GetControlWeight(
                                data,
                                x,
                                y,
                                3));
                }
            }

            texture.SetPixels(
                colors);
            texture.Apply(
                false,
                false);
            return texture;
        }

        private static float GetControlWeight(
            CelestialSurfacePatchData data,
            int x,
            int y,
            int layer)
        {
            return layer <
                    data.SurfaceLayerCount
                ? data.GetSurfaceControlWeight(
                    x,
                    y,
                    layer)
                : 0.0f;
        }

        private void ConfigurePatchTextureTransforms(
            MaterialPropertyBlock block,
            CubeSpherePatchAddress address)
        {
            if (!surfaceRuntime.TryCreateMapMagicRequest(
                    address,
                    out var request,
                    out _))
            {
                return;
            }

            for (var layerIndex = 0;
                layerIndex <
                    Math.Min(
                        patchGenerator.TerrainLayerCount,
                        MaximumAdaptedLayerCount);
                layerIndex++)
            {
                var layer =
                    patchGenerator.GetTerrainLayer(
                        layerIndex);

                if (layer == null)
                {
                    continue;
                }

                var surfaceLayer =
                    configuredSurfaceLayers[
                        layerIndex];
                var tileSizeX =
                    surfaceLayer != null
                        ? SafeTileSize(
                            surfaceLayer
                                .TextureScaleMeters)
                        : SafeTileSize(
                            layer.tileSize.x);
                var tileSizeZ =
                    surfaceLayer != null
                        ? tileSizeX
                        : SafeTileSize(
                            layer.tileSize.y);
                var scaleX =
                    request.MapWorldSizeXMeters /
                    tileSizeX;
                var scaleZ =
                    -request.MapWorldSizeZMeters /
                    tileSizeZ;
                var offsetX =
                    (request.MapWorldOriginXMeters +
                        layer.tileOffset.x) /
                    tileSizeX;
                var offsetZ =
                    (request.MapWorldOriginZMeters +
                        request.MapWorldSizeZMeters +
                        layer.tileOffset.y) /
                    tileSizeZ;

                block.SetVector(
                    "_Splat" +
                        layerIndex +
                        "_ST",
                    new Vector4(
                        (float)scaleX,
                        (float)scaleZ,
                        Repeat01(
                            offsetX),
                        Repeat01(
                            offsetZ)));
            }
        }

        private void EnsureRenderMaterials()
        {
            if (renderMode !=
                CelestialAdaptiveSurfaceRenderMode.Surface)
            {
                return;
            }

            var layerCount =
                patchGenerator.TerrainLayerCount;
            var surfaceAppearance =
                surfaceRuntime.SurfaceDefinition
                    .SurfaceAppearance;

            if (surfaceMaterial != null &&
                configuredTerrainLayerCount ==
                    layerCount &&
                configuredSurfaceAppearance ==
                    surfaceAppearance)
            {
                return;
            }

            DestroyRuntimeMaterial(
                ref surfaceMaterial);
            ResolveConfiguredSurfaceLayers(
                surfaceAppearance,
                layerCount);
            surfaceMaterial =
                CreateSurfaceMaterial(
                    layerCount);
            configuredTerrainLayerCount =
                layerCount;
            configuredSurfaceAppearance =
                surfaceAppearance;
            RefreshVisualMaterials();
        }

        private Material CreateSurfaceMaterial(
            int layerCount)
        {
            var template =
                surfaceRuntime.SurfaceDefinition
                    .Material;
            Material material;

            if (layerCount > 0 &&
                !IsCompatibleLayerTemplate(
                    template))
            {
                var terrainShader =
                    Shader.Find(
                        DefaultTerrainShaderName);

                if (terrainShader == null)
                {
                    throw new InvalidOperationException(
                        $"Shader '{DefaultTerrainShaderName}' was not found for the adaptive surface renderer.");
                }

                material =
                    new Material(
                        terrainShader);
            }
            else if (template != null)
            {
                material =
                    new Material(
                        template);
            }
            else
            {
                var fallbackShader =
                    Shader.Find(
                        DefaultTerrainShaderName) ??
                    Shader.Find(
                        "Standard") ??
                    Shader.Find(
                        "Unlit/Color");

                if (fallbackShader == null)
                {
                    throw new InvalidOperationException(
                        "No compatible terrain shader was found for the adaptive surface renderer.");
                }

                material =
                    new Material(
                        fallbackShader);
            }

            material.name =
                "Adaptive Celestial Surface Material";
            material.hideFlags =
                HideFlags.HideAndDontSave;
            SetFloatIfPresent(
                material,
                "_LayerCount",
                layerCount);
            SetFloatIfPresent(
                material,
                "_TileFade",
                1.0f);
            SetFloatIfPresent(
                material,
                "_LodMaskMode",
                0.0f);

            var surfaceAppearance =
                surfaceRuntime.SurfaceDefinition
                    .SurfaceAppearance;

            if (surfaceAppearance != null)
            {
                SetFloatIfPresent(
                    material,
                    "_SurfaceLightingMode",
                    (float)surfaceAppearance.LightingMode);
            }

            for (var index = 0;
                index < MaximumAdaptedLayerCount;
                index++)
            {
                ConfigureTerrainLayer(
                    material,
                    index < layerCount
                        ? patchGenerator.GetTerrainLayer(
                            index)
                        : null,
                    configuredSurfaceLayers[
                        index],
                    index);
            }

            return material;
        }

        private void ResolveConfiguredSurfaceLayers(
            CelestialSurfaceAppearance surfaceAppearance,
            int terrainLayerCount)
        {
            Array.Clear(
                configuredSurfaceLayers,
                0,
                configuredSurfaceLayers.Length);

            if (surfaceAppearance == null ||
                !surfaceAppearance.HasValidSettings)
            {
                return;
            }

            var resolvedCount =
                Math.Min(
                    terrainLayerCount,
                    MaximumAdaptedLayerCount);

            for (var terrainIndex = 0;
                terrainIndex < resolvedCount;
                terrainIndex++)
            {
                var terrainLayer =
                    patchGenerator.GetTerrainLayer(
                        terrainIndex);

                for (var appearanceIndex = 0;
                    appearanceIndex <
                        surfaceAppearance.LayerCount;
                    appearanceIndex++)
                {
                    var candidate =
                        surfaceAppearance.GetLayer(
                            appearanceIndex);

                    if (candidate != null &&
                        candidate.MapMagicTerrainLayer != null &&
                        candidate.MapMagicTerrainLayer ==
                            terrainLayer)
                    {
                        configuredSurfaceLayers[
                            terrainIndex] =
                                candidate;
                        break;
                    }
                }

                if (configuredSurfaceLayers[
                        terrainIndex] != null)
                {
                    continue;
                }

                var orderedCandidate =
                    surfaceAppearance.GetLayer(
                        terrainIndex);

                if (orderedCandidate != null &&
                    orderedCandidate.MapMagicTerrainLayer ==
                        null)
                {
                    configuredSurfaceLayers[
                        terrainIndex] =
                            orderedCandidate;
                }
            }
        }

        private static void ConfigureTerrainLayer(
            Material material,
            TerrainLayer terrainLayer,
            CelestialSurfaceLayerDefinition surfaceLayer,
            int index)
        {
            var suffix =
                index.ToString();
            var albedoTexture =
                surfaceLayer != null &&
                surfaceLayer.AlbedoTexture != null
                    ? surfaceLayer.AlbedoTexture
                    : terrainLayer != null &&
                        terrainLayer.diffuseTexture != null
                        ? terrainLayer.diffuseTexture
                        : Texture2D.whiteTexture;
            var normalTexture =
                surfaceLayer != null &&
                surfaceLayer.NormalTexture != null
                    ? surfaceLayer.NormalTexture
                    : terrainLayer != null
                        ? terrainLayer.normalMapTexture
                        : null;
            var maskTexture =
                surfaceLayer != null &&
                surfaceLayer.MaskTexture != null
                    ? surfaceLayer.MaskTexture
                    : terrainLayer != null
                        ? terrainLayer.maskMapTexture
                        : null;
            var emissionTexture =
                surfaceLayer != null &&
                surfaceLayer.EmissionTexture != null
                    ? surfaceLayer.EmissionTexture
                    : Texture2D.whiteTexture;
            var normalStrength =
                surfaceLayer != null
                    ? surfaceLayer.NormalStrength
                    : terrainLayer != null
                        ? terrainLayer.normalScale
                        : 1.0f;
            var metallic =
                surfaceLayer != null
                    ? surfaceLayer.Metallic
                    : terrainLayer != null
                        ? terrainLayer.metallic
                        : 0.0f;
            var smoothness =
                surfaceLayer != null
                    ? surfaceLayer.Smoothness
                    : terrainLayer != null
                        ? terrainLayer.smoothness
                        : 0.0f;
            var tint =
                surfaceLayer != null
                    ? surfaceLayer.Tint
                    : Color.white;
            var occlusionStrength =
                surfaceLayer != null
                    ? surfaceLayer.OcclusionStrength
                    : 1.0f;
            var emissionColor =
                surfaceLayer != null
                    ? surfaceLayer.EmissionColor
                    : Color.white;
            var emissionIntensity =
                surfaceLayer != null
                    ? surfaceLayer.EmissionIntensity
                    : 0.0f;

            SetTextureIfPresent(
                material,
                "_Splat" + suffix,
                albedoTexture);
            SetTextureIfPresent(
                material,
                "_Normal" + suffix,
                normalTexture);
            SetTextureIfPresent(
                material,
                "_Mask" + suffix,
                maskTexture);
            SetTextureIfPresent(
                material,
                "_EmissionMap" + suffix,
                emissionTexture);
            SetFloatIfPresent(
                material,
                "_HasNormal" + suffix,
                normalTexture != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_HasMask" + suffix,
                maskTexture != null
                    ? 1.0f
                    : 0.0f);
            SetFloatIfPresent(
                material,
                "_NormalScale" + suffix,
                normalStrength);
            SetFloatIfPresent(
                material,
                "_Metallic" + suffix,
                metallic);
            SetFloatIfPresent(
                material,
                "_Smoothness" + suffix,
                smoothness);
            SetColorIfPresent(
                material,
                "_Tint" + suffix,
                tint);
            SetFloatIfPresent(
                material,
                "_OcclusionStrength" + suffix,
                occlusionStrength);
            SetColorIfPresent(
                material,
                "_EmissionColor" + suffix,
                emissionColor);
            SetFloatIfPresent(
                material,
                "_EmissionIntensity" + suffix,
                emissionIntensity);
        }

        private Material GetDebugMaterial(
            CubeSpherePatchAddress address)
        {
            var key =
                (int)address.Face *
                    100 +
                address.Level;

            if (debugMaterials.TryGetValue(
                    key,
                    out var material))
            {
                return material;
            }

            var shader =
                Shader.Find(
                    "Unlit/Color") ??
                Shader.Find(
                    "Standard");

            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible debug shader was found for adaptive LOD coloring.");
            }

            material =
                new Material(
                    shader)
                {
                    name =
                        $"Adaptive LOD {address.Face} L{address.Level}",
                    hideFlags =
                        HideFlags.HideAndDontSave
                };
            var hue =
                Mathf.Repeat(
                    address.Level *
                        0.137f +
                    (int)address.Face *
                        0.067f,
                    1.0f);
            var color =
                Color.HSVToRGB(
                    hue,
                    0.72f,
                    0.95f);

            if (material.HasProperty(
                    "_Color"))
            {
                material.SetColor(
                    "_Color",
                    color);
            }

            if (material.HasProperty(
                    "_BaseColor"))
            {
                material.SetColor(
                    "_BaseColor",
                    color);
            }

            debugMaterials.Add(
                key,
                material);
            return material;
        }

        private void RefreshVisualMaterials()
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                RefreshVisualMaterials(
                    roots[index]);
            }
        }

        private void RefreshVisualMaterials(
            PatchNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.Visual != null &&
                node.Data != null)
            {
                ConfigureVisualMaterial(
                    node.Visual,
                    node);
            }

            if (node.Children == null)
            {
                return;
            }

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                RefreshVisualMaterials(
                    node.Children[index]);
            }
        }

        private void SetVisualActive(
            PatchNode node,
            bool active)
        {
            if (node == null ||
                node.Visual == null)
            {
                return;
            }

            node.Visual.GameObject.SetActive(
                active);
        }

        private void RefreshActiveStatistics()
        {
            activePatchCount = 0;
            maximumActiveLevel = 0;

            for (var index = 0;
                index < roots.Length;
                index++)
            {
                RefreshActiveStatistics(
                    roots[index]);
            }
        }

        private void RefreshActiveStatistics(
            PatchNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.Visual != null &&
                node.Visual.GameObject.activeSelf)
            {
                activePatchCount++;
                maximumActiveLevel =
                    Math.Max(
                        maximumActiveLevel,
                        node.Address.Level);
            }

            if (node.Children == null)
            {
                return;
            }

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                RefreshActiveStatistics(
                    node.Children[index]);
            }
        }

        private void SetSubtreeActive(
            PatchNode node,
            bool active)
        {
            if (node == null)
            {
                return;
            }

            SetVisualActive(
                node,
                active);

            if (node.Children == null)
            {
                return;
            }

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                SetSubtreeActive(
                    node.Children[index],
                    active);
            }
        }

        private void SetAllNodesActive(
            bool active)
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                SetSubtreeActive(
                    roots[index],
                    active);
            }
        }

        private void ReleaseChildren(
            PatchNode node)
        {
            ResetChildTransition(
                node);

            if (node.Children == null)
            {
                return;
            }

            for (var index = 0;
                index < node.Children.Length;
                index++)
            {
                ReleaseNode(
                    node.Children[index]);
            }

            node.Children = null;
        }

        private void ReleaseRootDescendants()
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                if (roots[index] != null)
                {
                    ReleaseChildren(
                        roots[index]);
                    roots[index].DesiredSplit =
                        false;
                }
            }
        }

        private void ReleaseNode(
            PatchNode node)
        {
            if (node == null)
            {
                return;
            }

            ReleaseChildren(
                node);

            if (node.Visual != null)
            {
                ReleaseVisual(
                    node.Visual);
                node.Visual = null;
            }

            node.Data = null;
            node.Parent = null;
            node.DesiredSplit = false;
        }

        private void ReleaseVisual(
            PatchVisual visual)
        {
            if (visual == null)
            {
                return;
            }

            visual.GameObject.SetActive(
                false);
            visual.MeshRenderer.SetPropertyBlock(
                null);
            visual.MeshRenderer.sharedMaterial =
                null;
            DestroyControlTexture(
                visual);
            visual.Mesh.Clear();
            visual.Owner = null;
            visual.ParentVertices = null;
            visual.DetailVertices = null;
            visual.WorkingVertices = null;
            visual.AppliedMorphWeight =
                float.NaN;
            visualPool.Push(
                visual);
        }

        private static void DestroyControlTexture(
            PatchVisual visual)
        {
            if (visual.ControlTexture == null)
            {
                return;
            }

            Destroy(
                visual.ControlTexture);
            visual.ControlTexture = null;
        }

        private void ResolveObserverCamera()
        {
            if (observerCamera == null)
            {
                observerCamera =
                    Camera.main;
            }
        }

        private bool TryBuildObserverState()
        {
            if (!surfaceRuntime.TrySceneToBodyLocal(observerCamera.transform.position, out observerLocalPosition))
                return false;
            var distanceFromCenter =
                Magnitude(
                    observerLocalPosition);
            observerAltitudeMeters =
                distanceFromCenter -
                surfaceRuntime.BodyDefinition
                    .ReferenceRadiusMeters;
            observerState =
                new CelestialSurfaceObserverState(
                    observerCamera.GetInstanceID()
                        .ToString(),
                    observerLocalPosition,
                    observerCamera.fieldOfView,
                    Mathf.Max(
                        1,
                        observerCamera.pixelHeight),
                    true,
                    false);
            return observerState.IsValid;
        }

        private bool IsInsideHorizon(
            PatchNode node,
            double radius)
        {
            var observerDistance =
                observerState
                    .DistanceFromBodyCenterMeters;

            if (observerDistance <=
                radius)
            {
                return true;
            }

            var observerDirection =
                Normalize(
                    observerLocalPosition);
            var centerDot =
                Clamp(
                    Dot(
                        observerDirection,
                        node.CenterDirection),
                    -1.0,
                    1.0);
            var centerAngle =
                Math.Acos(
                    centerDot);
            var horizonAngle =
                Math.Acos(
                    Clamp(
                        radius /
                            observerDistance,
                        -1.0,
                        1.0));

            return
                centerAngle <=
                horizonAngle +
                    node.AngularRadiusRadians +
                    0.01;
        }

        private bool IsInsideFrustum(
            PatchNode node)
        {
            var worldCenter =
                transform.TransformPoint(
                    node.LocalCenter);
            var scale =
                MaximumAbsoluteComponent(
                    transform.lossyScale);
            var diameter =
                (float)Math.Min(
                    float.MaxValue,
                    node.BoundingRadiusMeters *
                        2.0 *
                        scale);
            var bounds =
                new Bounds(
                    worldCenter,
                    Vector3.one *
                        diameter);

            return
                frustumPlanes == null ||
                GeometryUtility.TestPlanesAABB(
                    frustumPlanes,
                    bounds);
        }

        private bool AreAllRootsReady()
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                if (roots[index] == null ||
                    patchGenerator.GetPatchState(
                        roots[index].Address) !=
                    CelestialSurfacePatchGenerationState.Ready)
                {
                    return false;
                }
            }

            return true;
        }

        private void ReportReadiness(
            bool coarseReady,
            bool visibleReady)
        {
            coarseSurfaceReady =
                coarseReady;
            visibleSurfaceReady =
                visibleReady;

            if (surfaceRuntime != null &&
                surfaceRuntime.Body != null)
            {
                surfaceRuntime.Body.ReportSurfaceReadiness(
                    coarseReady,
                    visibleReady,
                    surfaceRuntime.CollisionRuntime != null &&
                    surfaceRuntime.CollisionRuntime.CoverageReady);
            }
        }

        private void ClearFrameStatistics()
        {
            desiredLeafCount = 0;
            activePatchCount = 0;
            culledPatchCount = 0;
            heldParentCount = 0;
            transitioningBranchCount = 0;
            maximumActiveLevel = 0;
            maximumNeighborLevelDifference = 0;
            neighborBalanceValid = true;
            coverageInvariantValid = true;
        }

        private PatchNode GetRoot(
            CubeSphereFace face)
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                if (roots[index] != null &&
                    roots[index].Address.Face ==
                        face)
                {
                    return roots[index];
                }
            }

            return null;
        }

        private double ResolveSkirtDepthMeters(
            CubeSpherePatchAddress address)
        {
            var arcSize =
                CubeSpherePatchGrid
                    .GetApproximateArcSizeMeters(
                        address,
                        surfaceRuntime.BodyDefinition
                            .ReferenceRadiusMeters);
            var cellSize =
                arcSize /
                Math.Max(
                    1,
                    surfaceRuntime.PatchResolution -
                        1);

            return Math.Max(
                minimumSkirtDepthMeters,
                cellSize *
                    skirtDepthCellFraction);
        }

        private static double CalculateAngularRadius(
            CubeSpherePatchAddress address,
            DoubleVector3 centerDirection)
        {
            var maximumAngle = 0.0;
            var cornerU =
                new[]
                {
                    address.MinimumU,
                    address.MaximumU
                };
            var cornerV =
                new[]
                {
                    address.MinimumV,
                    address.MaximumV
                };

            for (var uIndex = 0;
                uIndex < cornerU.Length;
                uIndex++)
            {
                for (var vIndex = 0;
                    vIndex < cornerV.Length;
                    vIndex++)
                {
                    var direction =
                        CubeSphereMapping.AddressToDirection(
                            new CubeSphereAddress(
                                address.Face,
                                cornerU[uIndex],
                                cornerV[vIndex],
                                0.0));
                    maximumAngle =
                        Math.Max(
                            maximumAngle,
                            Math.Acos(
                                Clamp(
                                    Dot(
                                        centerDirection,
                                        direction),
                                    -1.0,
                                    1.0)));
                }
            }

            return maximumAngle;
        }

        private static int ResolveChildIndex(
            bool positiveU,
            bool positiveV)
        {
            if (positiveV)
            {
                return positiveU
                    ? (int)CubeSpherePatchQuadrant
                        .PositiveUPositiveV
                    : (int)CubeSpherePatchQuadrant
                        .NegativeUPositiveV;
            }

            return positiveU
                ? (int)CubeSpherePatchQuadrant
                    .PositiveUNegativeV
                : (int)CubeSpherePatchQuadrant
                    .NegativeUNegativeV;
        }

        private void ReleaseAllNodes()
        {
            for (var index = 0;
                index < roots.Length;
                index++)
            {
                ReleaseNode(
                    roots[index]);
                roots[index] = null;
            }
        }

        private void DestroyRuntimeResources()
        {
            ReleaseAllNodes();

            while (visualPool.Count > 0)
            {
                var visual =
                    visualPool.Pop();
                DestroyControlTexture(
                    visual);

                if (visual.Mesh != null)
                {
                    Destroy(
                        visual.Mesh);
                }

                if (visual.GameObject != null)
                {
                    Destroy(
                        visual.GameObject);
                }
            }

            DestroyRuntimeMaterial(
                ref surfaceMaterial);

            foreach (var material in
                debugMaterials.Values)
            {
                if (material != null)
                {
                    Destroy(
                        material);
                }
            }

            debugMaterials.Clear();
        }

        private void OnDisable()
        {
            SetAllNodesActive(
                false);
            ReportReadiness(
                false,
                false);
        }

        private void OnDestroy()
        {
            DestroyRuntimeResources();
        }

        private static bool IsCompatibleLayerTemplate(
            Material material)
        {
            return
                material != null &&
                material.HasProperty(
                    "_Control") &&
                material.HasProperty(
                    "_LayerCount") &&
                material.HasProperty(
                    "_Splat0");
        }

        private static void SetTextureIfPresent(
            Material material,
            string propertyName,
            Texture texture)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetTexture(
                    propertyName,
                    texture);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetFloat(
                    propertyName,
                    value);
            }
        }

        private static void SetColorIfPresent(
            Material material,
            string propertyName,
            Color value)
        {
            if (material.HasProperty(
                    propertyName))
            {
                material.SetColor(
                    propertyName,
                    value);
            }
        }

        private static void DestroyRuntimeMaterial(
            ref Material material)
        {
            if (material == null)
            {
                return;
            }

            Destroy(
                material);
            material = null;
        }

        private static float SafeTileSize(
            float value)
        {
            return
                IsFinite(value) &&
                Mathf.Abs(value) >
                    Mathf.Epsilon
                    ? Mathf.Abs(value)
                    : 1.0f;
        }

        private static float Repeat01(
            double value)
        {
            return
                (float)(
                    value -
                    Math.Floor(value));
        }

        private static float MaximumAbsoluteComponent(
            Vector3 value)
        {
            return Mathf.Max(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
        }

        private static DoubleVector3 Normalize(
            DoubleVector3 value)
        {
            var magnitude =
                Magnitude(value);

            return magnitude > 0.0
                ? value *
                    (1.0 /
                        magnitude)
                : default;
        }

        private static double Magnitude(
            DoubleVector3 value)
        {
            return Math.Sqrt(
                value.x * value.x +
                value.y * value.y +
                value.z * value.z);
        }

        private static double Dot(
            DoubleVector3 first,
            DoubleVector3 second)
        {
            return
                first.x * second.x +
                first.y * second.y +
                first.z * second.z;
        }

        private static double Clamp(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value));
        }

        private static Vector3 ToVector3(
            DoubleVector3 value)
        {
            return new Vector3(
                (float)value.x,
                (float)value.y,
                (float)value.z);
        }

        private static DoubleVector3 ToDoubleVector3(
            Vector3 value)
        {
            return new DoubleVector3(
                value.x,
                value.y,
                value.z);
        }

        private static bool IsFinite(
            float value)
        {
            return
                !float.IsNaN(value) &&
                !float.IsInfinity(value);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
