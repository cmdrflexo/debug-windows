/*
 * Adds bloom and exposure contributions for intense luminous sources using selectable activation triggers.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    public enum BlazeEffectorTrigger
    {
        Always,
        Manual,
        Proximity
    }

    [DisallowMultipleComponent]
    public sealed class BlazeEffector : ImageEffector
    {
        [SerializeField]
        private BlazeEffectorTrigger trigger = BlazeEffectorTrigger.Proximity;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float manualAmount;

        [Header("Proximity")]
        [SerializeField]
        [Tooltip("Optional transform used instead of the receiver transform when measuring proximity.")]
        private Transform proximityTarget;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Radius of the luminous body, used to measure altitude above its surface.")]
        private float surfaceRadius;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Surface distance at which the effect reaches full strength.")]
        private float fullStrengthDistance;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Surface distance at which the effect reaches zero.")]
        private float maximumDistance = 1000000.0f;

        [SerializeField]
        private AnimationCurve proximityFalloff =
            AnimationCurve.EaseInOut(0.0f, 0.0f, 1.0f, 1.0f);

        [Header("Image Effects")]
        [SerializeField]
        [Min(0.0f)]
        private float maximumBloomBoost = 3.0f;

        [SerializeField]
        [Min(0.0f)]
        private float maximumExposureBoost = 2.0f;

        public BlazeEffectorTrigger Trigger => trigger;
        public float CurrentAmount => EvaluateAmount();

        private void Update()
        {
            Apply();
        }

        public void SetTrigger(BlazeEffectorTrigger value)
        {
            trigger = value;
            Apply();
        }

        public void SetManualAmount(float value)
        {
            manualAmount = Mathf.Clamp01(value);
            Apply();
        }

        protected override ImageEffectContribution BuildContribution()
        {
            var amount = EvaluateAmount();
            return new ImageEffectContribution
            {
                bloomIntensity = amount * maximumBloomBoost,
                exposureCompensation = amount * maximumExposureBoost
            };
        }

        private float EvaluateAmount()
        {
            switch (trigger)
            {
                case BlazeEffectorTrigger.Always:
                    return 1.0f;
                case BlazeEffectorTrigger.Manual:
                    return manualAmount;
                case BlazeEffectorTrigger.Proximity:
                    return EvaluateProximity();
                default:
                    return 0.0f;
            }
        }

        private float EvaluateProximity()
        {
            var target = proximityTarget != null
                ? proximityTarget
                : Receiver != null
                    ? Receiver.transform
                    : null;
            if (target == null)
                return 0.0f;

            var centerDistance = Vector3.Distance(transform.position, target.position);
            var surfaceDistance = Mathf.Max(0.0f, centerDistance - surfaceRadius);
            var outerDistance = Mathf.Max(fullStrengthDistance, maximumDistance);
            if (outerDistance <= fullStrengthDistance)
                return surfaceDistance <= fullStrengthDistance ? 1.0f : 0.0f;

            var amount = Mathf.InverseLerp(
                outerDistance,
                fullStrengthDistance,
                surfaceDistance);
            return Mathf.Clamp01(
                proximityFalloff != null
                    ? proximityFalloff.Evaluate(amount)
                    : amount);
        }

        private void OnValidate()
        {
            surfaceRadius = Mathf.Max(0.0f, surfaceRadius);
            fullStrengthDistance = Mathf.Max(0.0f, fullStrengthDistance);
            maximumDistance = Mathf.Max(fullStrengthDistance, maximumDistance);
            maximumBloomBoost = Mathf.Clamp(maximumBloomBoost, 0.0f, 20.0f);
            maximumExposureBoost = Mathf.Clamp(maximumExposureBoost, 0.0f, 10.0f);

            if (Application.isPlaying)
                Apply();
        }
    }
}
