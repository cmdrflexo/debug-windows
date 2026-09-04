/*
 * Exposes a Gravity Engine NBody as the authoritative motion provider for a packaged celestial body.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(-95)]
    [DisallowMultipleComponent]
    public sealed class GravityEngineCelestialBodyMotionProvider :
        MonoBehaviour,
        ICelestialBodyMotionProvider
    {
        [SerializeField]
        private UniverseFrameController universeFrame;

        [SerializeField]
        private NBody sourceBody;

        [Header("Runtime")]
        [SerializeField]
        private bool isReady;

        [SerializeField]
        private bool hasMotionState;

        [SerializeField]
        private CelestialBodyMotionState motionState;

        [SerializeField]
        private string lastError;

        private GravityEngine gravityEngine;

        public string ProviderName =>
            "Gravity Engine";

        public bool IsReady =>
            isReady;

        public UniverseFrameController UniverseFrame =>
            universeFrame;

        public NBody SourceBody =>
            sourceBody;

        public bool HasMotionState =>
            hasMotionState;

        public CelestialBodyMotionState MotionState =>
            motionState;

        public string LastError =>
            lastError;

        private void Start()
        {
            gravityEngine =
                GravityEngine.Instance();
            RefreshMotionState();
        }

        private void LateUpdate()
        {
            RefreshMotionState();
        }

        public void Initialize(
            UniverseFrameController newUniverseFrame,
            NBody newSourceBody)
        {
            universeFrame =
                newUniverseFrame;
            sourceBody =
                newSourceBody;
            gravityEngine =
                GravityEngine.Instance();

            RefreshMotionState();
        }

        public bool TryGetMotionState(
            out CelestialBodyMotionState currentMotionState)
        {
            RefreshMotionState();

            currentMotionState =
                motionState;
            return hasMotionState;
        }

        private void RefreshMotionState()
        {
            isReady = false;
            hasMotionState = false;

            if (universeFrame == null)
            {
                lastError =
                    "Universe frame is not assigned.";
                return;
            }

            if (sourceBody == null)
            {
                lastError =
                    "Gravity Engine body is not assigned.";
                return;
            }

            gravityEngine ??=
                GravityEngine.Instance();

            if (gravityEngine == null ||
                !gravityEngine.IsSetup())
            {
                lastError =
                    "Gravity Engine is not ready.";
                return;
            }

            if (!universeFrame.FrameOriginInitialized)
            {
                lastError =
                    "Universe frame origin is not initialized.";
                return;
            }

            var physicalScale =
                gravityEngine.GetPhysicalScale();

            if (!IsFinite(physicalScale) ||
                physicalScale <= 0.0)
            {
                lastError =
                    "Gravity Engine returned an invalid physical scale.";
                return;
            }

            var physicsPosition =
                gravityEngine.GetPositionDoubleV3(
                    sourceBody);

            if (!IsFinite(physicsPosition.x) ||
                !IsFinite(physicsPosition.y) ||
                !IsFinite(physicsPosition.z))
            {
                lastError =
                    "Gravity Engine returned a non-finite body position.";
                return;
            }

            var universePosition =
                universeFrame.FrameOrigin;

            universePosition.AddLocalMeters(
                physicsPosition.x * physicalScale,
                physicsPosition.y * physicalScale,
                physicsPosition.z * physicalScale);

            motionState =
                new CelestialBodyMotionState(
                    universePosition,
                    sourceBody.transform.rotation);
            hasMotionState = true;
            isReady = true;
            lastError = string.Empty;
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
