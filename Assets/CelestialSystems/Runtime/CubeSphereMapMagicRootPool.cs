/*
 * Assigns the primary and edge-adjacent cube-sphere terrain addresses to a reusable pool of up to three MapMagic roots.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicRootPool :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private CubeSphereTerrainAddressTracker addressTracker;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver primaryRoot;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver uAdjacentRoot;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver vAdjacentRoot;

        [Header("Runtime")]
        [SerializeField]
        private int activeRootCount;

        [SerializeField]
        private bool hasPrimaryFace;

        [SerializeField]
        private CubeSphereFace primaryFace;

        [SerializeField]
        private bool hasUAdjacentFace;

        [SerializeField]
        private CubeSphereFace uAdjacentFace;

        [SerializeField]
        private bool hasVAdjacentFace;

        [SerializeField]
        private CubeSphereFace vAdjacentFace;

        public int ActiveRootCount =>
            activeRootCount;

        private void Start()
        {
            if (addressTracker == null)
            {
                Debug.LogError(
                    "The MapMagic root pool requires a cube-sphere terrain address tracker.",
                    this);
            }

            if (primaryRoot == null ||
                uAdjacentRoot == null ||
                vAdjacentRoot == null)
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

            DeactivateRoot(uAdjacentRoot);
            DeactivateRoot(vAdjacentRoot);
        }

        private void LateUpdate()
        {
            ClearRuntimeState();

            if (addressTracker == null ||
                !RootSlotsAreValid() ||
                !addressTracker.HasPrimaryTileAddress)
            {
                DeactivateAllRoots();
                return;
            }

            var primaryAddress =
                addressTracker.PrimaryTileAddress;

            ActivateRoot(
                primaryRoot,
                primaryAddress);
            hasPrimaryFace = true;
            primaryFace =
                primaryAddress.Face;

            if (addressTracker.HasUAdjacentTileAddress)
            {
                var uAddress =
                    addressTracker.UAdjacentTileAddress;

                ActivateRoot(
                    uAdjacentRoot,
                    uAddress);
                hasUAdjacentFace = true;
                uAdjacentFace =
                    uAddress.Face;
            }
            else
            {
                DeactivateRoot(
                    uAdjacentRoot);
            }

            if (addressTracker.HasVAdjacentTileAddress)
            {
                var vAddress =
                    addressTracker.VAdjacentTileAddress;

                ActivateRoot(
                    vAdjacentRoot,
                    vAddress);
                hasVAdjacentFace = true;
                vAdjacentFace =
                    vAddress.Face;
            }
            else
            {
                DeactivateRoot(
                    vAdjacentRoot);
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
            DeactivateRoot(primaryRoot);
            DeactivateRoot(uAdjacentRoot);
            DeactivateRoot(vAdjacentRoot);
        }

        private bool RootSlotsAreValid()
        {
            return
                primaryRoot != null &&
                uAdjacentRoot != null &&
                vAdjacentRoot != null &&
                RootSlotsAreDistinct();
        }

        private bool RootSlotsAreDistinct()
        {
            return
                primaryRoot != uAdjacentRoot &&
                primaryRoot != vAdjacentRoot &&
                uAdjacentRoot != vAdjacentRoot;
        }

        private void ClearRuntimeState()
        {
            activeRootCount = 0;
            hasPrimaryFace = false;
            primaryFace = default;
            hasUAdjacentFace = false;
            uAdjacentFace = default;
            hasVAdjacentFace = false;
            vAdjacentFace = default;
        }
    }
}
