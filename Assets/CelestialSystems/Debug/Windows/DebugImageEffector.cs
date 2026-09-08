/*
 * Provides the first prototype image-effector source for manually simulating star proximity and atmospheric pressure.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class DebugImageEffector : MonoBehaviour
    {
        private const string SourceId =
            "jcan.celestialsystems.debug-image-effector";

        [SerializeField]
        private ImageEffectorReceiver receiver;

        [SerializeField]
        private bool overridesEnabled;

        [Header("Star Proximity")]
        [SerializeField]
        private bool starProximityEnabled = true;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float starProximity;

        [SerializeField]
        [Min(0.0f)]
        private float maximumBloomBoost = 3.0f;

        [SerializeField]
        [Min(0.0f)]
        private float maximumExposureBoost = 2.0f;

        [Header("Atmosphere")]
        [SerializeField]
        private bool atmosphereEnabled = true;

        [SerializeField]
        [Min(0.0f)]
        private float atmospherePressure;

        [SerializeField]
        [Range(-1.0f, 1.0f)]
        private float lensDistortionPerAtmosphere = 0.1f;

        public bool OverridesEnabled => overridesEnabled;
        public bool StarProximityEnabled => starProximityEnabled;
        public float StarProximity => starProximity;
        public float MaximumBloomBoost => maximumBloomBoost;
        public float MaximumExposureBoost => maximumExposureBoost;
        public bool AtmosphereEnabled => atmosphereEnabled;
        public float AtmospherePressure => atmospherePressure;
        public float LensDistortionPerAtmosphere =>
            lensDistortionPerAtmosphere;

        private void Awake()
        {
            if (receiver == null)
                receiver = GetComponent<ImageEffectorReceiver>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            receiver?.ClearContribution(SourceId);
        }

        public void BindReceiver(ImageEffectorReceiver value)
        {
            if (receiver != null && receiver != value)
                receiver.ClearContribution(SourceId);

            receiver = value;
            Apply();
        }

        public void SetOverridesEnabled(bool value)
        {
            overridesEnabled = value;
            Apply();
        }

        public void SetStarProximityEnabled(bool value)
        {
            starProximityEnabled = value;
            Apply();
        }

        public void SetStarProximity(float value)
        {
            starProximity = Mathf.Clamp01(value);
            Apply();
        }

        public void SetMaximumBloomBoost(float value)
        {
            maximumBloomBoost = Mathf.Clamp(value, 0.0f, 20.0f);
            Apply();
        }

        public void SetMaximumExposureBoost(float value)
        {
            maximumExposureBoost = Mathf.Clamp(value, 0.0f, 10.0f);
            Apply();
        }

        public void SetAtmosphereEnabled(bool value)
        {
            atmosphereEnabled = value;
            Apply();
        }

        public void SetAtmospherePressure(float value)
        {
            atmospherePressure = Mathf.Clamp(value, 0.0f, 20.0f);
            Apply();
        }

        public void SetLensDistortionPerAtmosphere(float value)
        {
            lensDistortionPerAtmosphere = Mathf.Clamp(value, -1.0f, 1.0f);
            Apply();
        }

        private void Apply()
        {
            if (receiver == null)
                return;

            if (!isActiveAndEnabled || !overridesEnabled)
            {
                receiver.ClearContribution(SourceId);
                return;
            }

            var starAmount = starProximityEnabled
                ? starProximity
                : 0.0f;
            var pressure = atmosphereEnabled
                ? atmospherePressure
                : 0.0f;
            receiver.SetContribution(
                SourceId,
                new ImageEffectContribution
                {
                    bloomIntensity =
                        starAmount * maximumBloomBoost,
                    exposureCompensation =
                        starAmount * maximumExposureBoost,
                    lensDistortionIntensity =
                        pressure * lensDistortionPerAtmosphere
                });
        }
    }
}
