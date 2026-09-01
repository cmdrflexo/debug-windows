/*
 * Converts a tracked or pool-assigned cube-sphere tile address into MapMagic's bounded coordinate-generation input.
 */

using System;
using Den.Tools;
using MapMagic.Core;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicCoordinateDriver :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private CubeSphereTerrainAddressTracker addressTracker;

        [SerializeField]
        private bool followTrackerPrimaryAddress = true;

        [SerializeField]
        private MapMagicObject mapMagicObject;

        [SerializeField]
        private bool takeGenerationControl = true;

        [Header("Runtime Source")]
        [SerializeField]
        private bool hasExternalAddress;

        [SerializeField]
        private CubeSphereTileAddress externalAddress;

        [Header("Runtime Coordinate")]
        [SerializeField]
        private bool hasMapMagicCoordinate;

        [SerializeField]
        private CubeSphereFace activeFace;

        [SerializeField]
        private long sourceTileU;

        [SerializeField]
        private long sourceTileV;

        [SerializeField]
        private double sourceLocalUMeters;

        [SerializeField]
        private double sourceLocalVMeters;

        [SerializeField]
        private int mapMagicTileX;

        [SerializeField]
        private int mapMagicTileZ;

        private bool generationControlApplied;
        private bool previousGenerateAroundMainCamera;
        private bool previousGenerateAroundObjectsTag;
        private bool previousGenerateAroundTransforms;
        private bool previousGenerateAroundCoordinates;
        private Coord[] previousGenerationCoordinates;
        private Coord currentCoordinate;
        private bool currentCoordinateSet;
        private bool coordinateRangeErrorLogged;

        public bool HasMapMagicCoordinate =>
            hasMapMagicCoordinate;

        public CubeSphereFace ActiveFace =>
            activeFace;

        public int MapMagicTileX =>
            mapMagicTileX;

        public int MapMagicTileZ =>
            mapMagicTileZ;

        public bool FollowTrackerPrimaryAddress =>
            followTrackerPrimaryAddress;

        private void Reset()
        {
            mapMagicObject =
                GetComponent<MapMagicObject>();
        }

        private void Start()
        {
            if (followTrackerPrimaryAddress &&
                addressTracker == null)
            {
                Debug.LogError(
                    "A MapMagic coordinate driver following the primary address requires a cube-sphere terrain address tracker.",
                    this);
            }

            if (mapMagicObject == null)
            {
                Debug.LogError(
                    "The MapMagic coordinate driver requires a MapMagic object.",
                    this);
            }

            if (addressTracker != null &&
                mapMagicObject != null &&
                (!Approximately(
                    addressTracker.TileSizeMeters,
                    mapMagicObject.tileSize.x) ||
                !Approximately(
                    addressTracker.TileSizeMeters,
                    mapMagicObject.tileSize.z)))
            {
                Debug.LogError(
                    "The cube-sphere tracker and MapMagic root must use the same tile size.",
                    this);
            }
        }

        private void LateUpdate()
        {
            hasMapMagicCoordinate = false;

            if (mapMagicObject == null ||
                !TryGetSourceAddress(
                    out var address))
            {
                return;
            }

            activeFace =
                address.Face;
            sourceTileU =
                address.TileU;
            sourceTileV =
                address.TileV;
            sourceLocalUMeters =
                address.LocalUMeters;
            sourceLocalVMeters =
                address.LocalVMeters;

            if (!TryConvertCoordinate(
                    sourceTileU,
                    sourceTileV,
                    out mapMagicTileX,
                    out mapMagicTileZ))
            {
                if (!coordinateRangeErrorLogged)
                {
                    Debug.LogError(
                        $"Cube-sphere tile ({sourceTileU}, {sourceTileV}) is outside MapMagic's 32-bit coordinate range.",
                        this);
                    coordinateRangeErrorLogged = true;
                }

                return;
            }

            coordinateRangeErrorLogged = false;
            hasMapMagicCoordinate = true;

            if (!takeGenerationControl)
            {
                return;
            }

            ApplyGenerationControl();

            var nextCoordinate =
                new Coord(
                    mapMagicTileX,
                    mapMagicTileZ);

            if (!currentCoordinateSet ||
                currentCoordinate !=
                    nextCoordinate ||
                mapMagicObject.tiles.genCoordinates ==
                    null ||
                mapMagicObject.tiles.genCoordinates.Length !=
                    1 ||
                mapMagicObject.tiles.genCoordinates[0] !=
                    nextCoordinate)
            {
                mapMagicObject.tiles.genCoordinates =
                    new[]
                    {
                        nextCoordinate
                    };
                currentCoordinate =
                    nextCoordinate;
                currentCoordinateSet = true;
            }
        }

        public void FollowPrimaryTrackerAddress()
        {
            followTrackerPrimaryAddress = true;
            hasExternalAddress = false;
        }

        public void SetExternalAddress(
            CubeSphereTileAddress address)
        {
            followTrackerPrimaryAddress = false;
            externalAddress = address;
            hasExternalAddress = true;
        }

        public void ClearExternalAddress()
        {
            followTrackerPrimaryAddress = false;
            hasExternalAddress = false;
            hasMapMagicCoordinate = false;
        }

        private void OnDisable()
        {
            RestoreGenerationControl();
        }

        private bool TryGetSourceAddress(
            out CubeSphereTileAddress address)
        {
            if (followTrackerPrimaryAddress)
            {
                if (addressTracker == null ||
                    !addressTracker.HasPrimaryTileAddress)
                {
                    address = default;
                    return false;
                }

                address =
                    addressTracker.PrimaryTileAddress;
                return true;
            }

            if (!hasExternalAddress)
            {
                address = default;
                return false;
            }

            address = externalAddress;
            return true;
        }

        private void ApplyGenerationControl()
        {
            if (!generationControlApplied)
            {
                previousGenerateAroundMainCamera =
                    mapMagicObject.tiles.genAroundMainCam;
                previousGenerateAroundObjectsTag =
                    mapMagicObject.tiles.genAroundObjsTag;
                previousGenerateAroundTransforms =
                    mapMagicObject.tiles.genAroundTfms;
                previousGenerateAroundCoordinates =
                    mapMagicObject.tiles.genAroundCoordinates;
                previousGenerationCoordinates =
                    mapMagicObject.tiles.genCoordinates != null
                        ? (Coord[])
                            mapMagicObject.tiles.genCoordinates.Clone()
                        : new Coord[0];
                generationControlApplied = true;
            }

            mapMagicObject.tiles.genAroundMainCam =
                false;
            mapMagicObject.tiles.genAroundObjsTag =
                false;
            mapMagicObject.tiles.genAroundTfms =
                false;
            mapMagicObject.tiles.genAroundCoordinates =
                true;
        }

        private void RestoreGenerationControl()
        {
            if (!generationControlApplied ||
                mapMagicObject == null)
            {
                return;
            }

            mapMagicObject.tiles.genAroundMainCam =
                previousGenerateAroundMainCamera;
            mapMagicObject.tiles.genAroundObjsTag =
                previousGenerateAroundObjectsTag;
            mapMagicObject.tiles.genAroundTfms =
                previousGenerateAroundTransforms;
            mapMagicObject.tiles.genAroundCoordinates =
                previousGenerateAroundCoordinates;
            mapMagicObject.tiles.genCoordinates =
                previousGenerationCoordinates ??
                new Coord[0];

            generationControlApplied = false;
            currentCoordinateSet = false;
        }

        private static bool TryConvertCoordinate(
            long tileU,
            long tileV,
            out int tileX,
            out int tileZ)
        {
            if (tileU < int.MinValue ||
                tileU > int.MaxValue ||
                tileV < int.MinValue ||
                tileV > int.MaxValue)
            {
                tileX = default;
                tileZ = default;
                return false;
            }

            tileX = (int)tileU;
            tileZ =
                (int)(-tileV - 1L);
            return true;
        }

        private static bool Approximately(
            double first,
            double second)
        {
            var scale =
                Math.Max(
                    1.0,
                    Math.Max(
                        Math.Abs(first),
                        Math.Abs(second)));

            return Math.Abs(first - second) <=
                scale * 0.000000001;
        }
    }
}
