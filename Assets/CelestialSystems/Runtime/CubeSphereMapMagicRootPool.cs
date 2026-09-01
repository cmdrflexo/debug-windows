/*
 * Manages three reusable MapMagic roots while preserving each face-to-root assignment across cube-sphere handoffs.
 */

using UnityEngine;
using UnityEngine.Serialization;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicRootPool :
        MonoBehaviour
    {
        private enum RootRole
        {
            Primary,
            UAdjacent,
            VAdjacent
        }

        private sealed class RootSlot
        {
            public CubeSphereMapMagicCoordinateDriver Driver;
            public bool HasAssignedFace;
            public CubeSphereFace AssignedFace;
            public bool InUse;

            public RootSlot(
                CubeSphereMapMagicCoordinateDriver driver)
            {
                Driver = driver;
            }
        }

        private struct DesiredRoot
        {
            public RootRole Role;
            public CubeSphereTileAddress Address;
            public CubeSphereMapMagicCoordinateDriver Root;
        }

        [Header("Configuration")]
        [SerializeField]
        private CubeSphereTerrainAddressTracker addressTracker;

        [FormerlySerializedAs("primaryRoot")]
        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver rootA;

        [FormerlySerializedAs("uAdjacentRoot")]
        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver rootB;

        [FormerlySerializedAs("vAdjacentRoot")]
        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver rootC;

        [Header("Runtime")]
        [SerializeField]
        private int activeRootCount;

        [SerializeField]
        private bool hasPrimaryFace;

        [SerializeField]
        private CubeSphereFace primaryFace;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver primaryAssignedRoot;

        [SerializeField]
        private bool hasUAdjacentFace;

        [SerializeField]
        private CubeSphereFace uAdjacentFace;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver uAdjacentAssignedRoot;

        [SerializeField]
        private bool hasVAdjacentFace;

        [SerializeField]
        private CubeSphereFace vAdjacentFace;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver vAdjacentAssignedRoot;

        private RootSlot[] rootSlots;
        private DesiredRoot[] desiredRoots;

        public int ActiveRootCount =>
            activeRootCount;

        private void Awake()
        {
            BuildRootSlots();
        }

        private void Start()
        {
            if (addressTracker == null)
            {
                Debug.LogError(
                    "The MapMagic root pool requires a cube-sphere terrain address tracker.",
                    this);
            }

            if (rootA == null ||
                rootB == null ||
                rootC == null)
            {
                Debug.LogError(
                    "The MapMagic root pool requires three coordinate-driver slots.",
                    this);
                return;
            }

            if (!RootSlotsAreDistinct())
            {
                Debug.LogError(
                    "Each MapMagic root-pool slot must reference a different coordinate driver.",
                    this);
                return;
            }

            if (!PoolOwnerIsSeparate())
            {
                Debug.LogError(
                    "The MapMagic root pool must not be placed on one of the pooled root GameObjects.",
                    this);
                return;
            }

            DeactivateRoot(rootB);
            DeactivateRoot(rootC);
        }

        private void LateUpdate()
        {
            ClearRuntimeState();

            if (!RootSlotsAreValid())
            {
                return;
            }

            if (rootSlots == null ||
                rootSlots.Length != 3)
            {
                BuildRootSlots();
            }

            if (addressTracker == null ||
                !addressTracker.HasPrimaryTileAddress)
            {
                DeactivateAllRoots();
                return;
            }

            ClearFrameAssignments();

            var desiredCount = 0;
            AddDesiredRoot(
                ref desiredCount,
                RootRole.Primary,
                addressTracker.PrimaryTileAddress);

            if (addressTracker.HasUAdjacentTileAddress)
            {
                AddDesiredRoot(
                    ref desiredCount,
                    RootRole.UAdjacent,
                    addressTracker.UAdjacentTileAddress);
            }

            if (addressTracker.HasVAdjacentTileAddress)
            {
                AddDesiredRoot(
                    ref desiredCount,
                    RootRole.VAdjacent,
                    addressTracker.VAdjacentTileAddress);
            }

            AssignMatchingRoots(desiredCount);
            AssignRemainingRoots(desiredCount);
            ApplyDesiredRoots(desiredCount);
            DeactivateUnusedRoots();
        }

        private void BuildRootSlots()
        {
            rootSlots = new[]
            {
                new RootSlot(rootA),
                new RootSlot(rootB),
                new RootSlot(rootC)
            };
            desiredRoots =
                new DesiredRoot[3];
        }

        private void AddDesiredRoot(
            ref int desiredCount,
            RootRole role,
            CubeSphereTileAddress address)
        {
            desiredRoots[desiredCount] =
                new DesiredRoot
                {
                    Role = role,
                    Address = address
                };
            desiredCount++;

            switch (role)
            {
                case RootRole.Primary:
                    hasPrimaryFace = true;
                    primaryFace = address.Face;
                    break;

                case RootRole.UAdjacent:
                    hasUAdjacentFace = true;
                    uAdjacentFace = address.Face;
                    break;

                case RootRole.VAdjacent:
                    hasVAdjacentFace = true;
                    vAdjacentFace = address.Face;
                    break;
            }
        }

        private void AssignMatchingRoots(
            int desiredCount)
        {
            for (var desiredIndex = 0;
                desiredIndex < desiredCount;
                desiredIndex++)
            {
                var desiredFace =
                    desiredRoots[desiredIndex].Address.Face;

                for (var slotIndex = 0;
                    slotIndex < rootSlots.Length;
                    slotIndex++)
                {
                    var slot =
                        rootSlots[slotIndex];

                    if (slot.InUse ||
                        !slot.HasAssignedFace ||
                        slot.AssignedFace != desiredFace)
                    {
                        continue;
                    }

                    AssignSlot(
                        desiredIndex,
                        slot);
                    break;
                }
            }
        }

        private void AssignRemainingRoots(
            int desiredCount)
        {
            for (var desiredIndex = 0;
                desiredIndex < desiredCount;
                desiredIndex++)
            {
                if (desiredRoots[desiredIndex].Root !=
                    null)
                {
                    continue;
                }

                var slot =
                    FindAvailableSlot(
                        true) ??
                    FindAvailableSlot(
                        false);

                if (slot == null)
                {
                    continue;
                }

                AssignSlot(
                    desiredIndex,
                    slot);
            }
        }

        private RootSlot FindAvailableSlot(
            bool requireInactive)
        {
            for (var slotIndex = 0;
                slotIndex < rootSlots.Length;
                slotIndex++)
            {
                var slot =
                    rootSlots[slotIndex];

                if (slot.InUse ||
                    slot.Driver == null ||
                    (requireInactive &&
                    slot.Driver.gameObject.activeSelf))
                {
                    continue;
                }

                return slot;
            }

            return null;
        }

        private void AssignSlot(
            int desiredIndex,
            RootSlot slot)
        {
            var desired =
                desiredRoots[desiredIndex];

            slot.InUse = true;
            slot.HasAssignedFace = true;
            slot.AssignedFace =
                desired.Address.Face;
            desired.Root =
                slot.Driver;
            desiredRoots[desiredIndex] =
                desired;
        }

        private void ApplyDesiredRoots(
            int desiredCount)
        {
            for (var desiredIndex = 0;
                desiredIndex < desiredCount;
                desiredIndex++)
            {
                var desired =
                    desiredRoots[desiredIndex];

                if (desired.Root == null)
                {
                    continue;
                }

                ActivateRoot(
                    desired.Root,
                    desired.Address);
                SetRuntimeAssignedRoot(
                    desired.Role,
                    desired.Root);
            }
        }

        private void SetRuntimeAssignedRoot(
            RootRole role,
            CubeSphereMapMagicCoordinateDriver root)
        {
            switch (role)
            {
                case RootRole.Primary:
                    primaryAssignedRoot =
                        root;
                    break;

                case RootRole.UAdjacent:
                    uAdjacentAssignedRoot =
                        root;
                    break;

                case RootRole.VAdjacent:
                    vAdjacentAssignedRoot =
                        root;
                    break;
            }
        }

        private void ActivateRoot(
            CubeSphereMapMagicCoordinateDriver root,
            CubeSphereTileAddress address)
        {
            root.SetExternalAddress(address);

            if (!root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(true);
            }

            activeRootCount++;
        }

        private void DeactivateUnusedRoots()
        {
            for (var slotIndex = 0;
                slotIndex < rootSlots.Length;
                slotIndex++)
            {
                var slot =
                    rootSlots[slotIndex];

                if (!slot.InUse)
                {
                    DeactivateRoot(
                        slot.Driver);
                }
            }
        }

        private static void DeactivateRoot(
            CubeSphereMapMagicCoordinateDriver root)
        {
            if (root == null)
            {
                return;
            }

            root.ClearExternalAddress();

            if (root.gameObject.activeSelf)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void DeactivateAllRoots()
        {
            if (rootSlots == null)
            {
                DeactivateRoot(rootA);
                DeactivateRoot(rootB);
                DeactivateRoot(rootC);
                return;
            }

            for (var slotIndex = 0;
                slotIndex < rootSlots.Length;
                slotIndex++)
            {
                rootSlots[slotIndex].InUse = false;
                DeactivateRoot(
                    rootSlots[slotIndex].Driver);
            }
        }

        private void ClearFrameAssignments()
        {
            for (var slotIndex = 0;
                slotIndex < rootSlots.Length;
                slotIndex++)
            {
                rootSlots[slotIndex].InUse =
                    false;
            }

            for (var desiredIndex = 0;
                desiredIndex < desiredRoots.Length;
                desiredIndex++)
            {
                desiredRoots[desiredIndex] =
                    default;
            }
        }

        private bool RootSlotsAreValid()
        {
            return
                rootA != null &&
                rootB != null &&
                rootC != null &&
                RootSlotsAreDistinct() &&
                PoolOwnerIsSeparate();
        }

        private bool PoolOwnerIsSeparate()
        {
            return
                rootA.gameObject != gameObject &&
                rootB.gameObject != gameObject &&
                rootC.gameObject != gameObject;
        }

        private bool RootSlotsAreDistinct()
        {
            return
                rootA != rootB &&
                rootA != rootC &&
                rootB != rootC;
        }

        private void ClearRuntimeState()
        {
            activeRootCount = 0;
            hasPrimaryFace = false;
            primaryFace = default;
            primaryAssignedRoot = null;
            hasUAdjacentFace = false;
            uAdjacentFace = default;
            uAdjacentAssignedRoot = null;
            hasVAdjacentFace = false;
            vAdjacentFace = default;
            vAdjacentAssignedRoot = null;
        }
    }
}
