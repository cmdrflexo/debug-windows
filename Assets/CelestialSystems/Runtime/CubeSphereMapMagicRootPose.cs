/*
 * Positions and orients one MapMagic root as the right-handed tangent coordinate frame of its assigned cube-sphere face.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(250)]
    [DisallowMultipleComponent]
    public sealed class CubeSphereMapMagicRootPose :
        MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        private GePlanetSurfaceFrame surfaceFrame;

        [SerializeField]
        private CubeSphereMapMagicCoordinateDriver coordinateDriver;

        [SerializeField]
        private double radialOffsetMeters;

        [Header("Runtime")]
        [SerializeField]
        private bool hasPose;

        [SerializeField]
        private CubeSphereFace activeFace;

        [SerializeField]
        private Vector3 planetCenterScenePosition;

        public bool HasPose =>
            hasPose;

        public CubeSphereFace ActiveFace =>
            activeFace;

        private void Reset()
        {
            coordinateDriver =
                GetComponent<CubeSphereMapMagicCoordinateDriver>();
        }

        private void Start()
        {
            if (surfaceFrame == null)
            {
                Debug.LogError(
                    "The MapMagic root pose requires a planet surface frame.",
                    this);
            }

            if (coordinateDriver == null)
            {
                Debug.LogError(
                    "The MapMagic root pose requires a coordinate driver on the same root.",
                    this);
            }

            if (!IsFinite(radialOffsetMeters))
            {
                Debug.LogError(
                    "The MapMagic root pose requires a finite radial offset.",
                    this);
            }
        }

        private void LateUpdate()
        {
            hasPose = false;

            if (surfaceFrame == null ||
                coordinateDriver == null ||
                !coordinateDriver.HasMapMagicCoordinate ||
                !surfaceFrame.TryGetPlanetCenterScenePosition(
                    out planetCenterScenePosition))
            {
                return;
            }

            var drawingRadiusMeters =
                surfaceFrame.PlanetRadiusMeters +
                radialOffsetMeters;

            if (!IsFinite(drawingRadiusMeters) ||
                drawingRadiusMeters <= 0.0 ||
                drawingRadiusMeters >
                    float.MaxValue)
            {
                return;
            }

            activeFace =
                coordinateDriver.ActiveFace;

            var outward =
                ToVector3(
                    CubeSphereTopology.GetFaceNormal(
                        activeFace)).normalized;
            var forward =
                -ToVector3(
                    CubeSphereTopology.GetFaceVAxis(
                        activeFace)).normalized;
            var rootPosition =
                planetCenterScenePosition +
                outward *
                    (float)drawingRadiusMeters;
            var rootRotation =
                Quaternion.LookRotation(
                    forward,
                    outward);

            transform.SetPositionAndRotation(
                rootPosition,
                rootRotation);
            hasPose = true;
        }

        private static Vector3 ToVector3(
            DoubleVector3 vector)
        {
            return new Vector3(
                (float)vector.x,
                (float)vector.y,
                (float)vector.z);
        }

        private static bool IsFinite(double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }
    }
}
