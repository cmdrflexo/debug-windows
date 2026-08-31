/*
 * Moves a nonphysical universe anchor using camera-relative free-flight controls without giving the camera origin authority.
 */

using CW.Common;
using SpaceGraphicsToolkit;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class FreeUniverseAnchorController : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField]
        private Transform movementReference;

        [Header("Movement")]
        [SerializeField]
        private bool listen = true;

        [SerializeField]
        private float damping = 10.0f;

        [SerializeField]
        private float speedMin = 1.0f;

        [SerializeField]
        private float speedMax = 10.0f;

        [SerializeField]
        private float speedRange = 100.0f;

        [SerializeField]
        [Range(0.0f, 0.5f)]
        private float speedWheel = 0.1f;

        [Header("Controls")]
        [SerializeField]
        private CwInputManager.Axis horizontalControls =
            new CwInputManager.Axis(
                2,
                false,
                CwInputManager.AxisGesture.HorizontalDrag,
                1.0f,
                KeyCode.A,
                KeyCode.D,
                KeyCode.LeftArrow,
                KeyCode.RightArrow,
                100.0f);

        [SerializeField]
        private CwInputManager.Axis depthControls =
            new CwInputManager.Axis(
                2,
                false,
                CwInputManager.AxisGesture.HorizontalDrag,
                1.0f,
                KeyCode.S,
                KeyCode.W,
                KeyCode.DownArrow,
                KeyCode.UpArrow,
                100.0f);

        [SerializeField]
        private CwInputManager.Axis verticalControls =
            new CwInputManager.Axis(
                3,
                false,
                CwInputManager.AxisGesture.HorizontalDrag,
                1.0f,
                KeyCode.F,
                KeyCode.R,
                KeyCode.None,
                KeyCode.None,
                100.0f);

        private Vector3 remainingDelta;

        private void OnEnable()
        {
            CwInputManager.EnsureThisComponentExists();
        }

        private void Update()
        {
            if (listen)
            {
                AddToDelta(GetDelta(Time.deltaTime));
                DampenDelta();
            }

            if (CwInput.GetMouseExists())
            {
                speedRange *=
                    1.0f -
                    Mathf.Clamp(CwInput.GetMouseWheelDelta(), -1.0f, 1.0f) *
                    speedWheel;
            }
        }

        private Vector3 GetDelta(float deltaTime)
        {
            return new Vector3(
                horizontalControls.GetValue(deltaTime),
                verticalControls.GetValue(deltaTime),
                depthControls.GetValue(deltaTime));
        }

        private void AddToDelta(Vector3 localDelta)
        {
            var reference = movementReference;

            if (reference == null)
            {
                var mainCamera = Camera.main;
                reference = mainCamera != null ? mainCamera.transform : transform;
            }

            remainingDelta +=
                reference.TransformDirection(localDelta) *
                GetSpeedMultiplier();
        }

        private void DampenDelta()
        {
            var factor = CwHelper.DampenFactor(damping, Time.deltaTime);
            var newDelta = Vector3.Lerp(remainingDelta, Vector3.zero, factor);

            transform.position += remainingDelta - newDelta;
            remainingDelta = newDelta;
        }

        private float GetSpeedMultiplier()
        {
            if (speedMax <= 0.0f)
            {
                return 0.0f;
            }

            var distance = float.PositiveInfinity;

            SgtCommon.InvokeCalculateDistance(transform.position, ref distance);

            var distance01 = Mathf.InverseLerp(
                speedMin * speedRange,
                speedMax * speedRange,
                distance);

            return Mathf.Lerp(speedMin, speedMax, distance01);
        }
    }
}
