/*
 * Drives a camera-local particle system from the camera's double-precision
 * universe-space motion. Local simulation deliberately isolates particles
 * from floating-origin shifts.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class UniverseCameraDustController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Camera-local dust particle system. Local simulation is enforced at runtime.")]
        private ParticleSystem dustParticles;

        [SerializeField]
        [Tooltip("Universe-pose source. Defaults to the active SGT origin bridge.")]
        private SgtUniverseOriginBridge poseBridge;

        [SerializeField]
        [Tooltip("Camera used to convert universe velocity into the particle system's local axes.")]
        private Transform cameraTransform;

        [Header("Speed Visibility")]
        [SerializeField]
        [Tooltip("Dust begins fading out at this relative camera speed in metres per second.")]
        [Min(0.0f)]
        private float fadeOutStartSpeedMetersPerSecond = 25.0f;

        [SerializeField]
        [Tooltip("Dust emission reaches zero at this relative camera speed in metres per second.")]
        [Min(0.01f)]
        private float fadeOutEndSpeedMetersPerSecond = 100.0f;

        [SerializeField]
        [Tooltip("Largest speed represented by particle motion. This affects only the visual, not camera movement.")]
        [Min(0.01f)]
        private float maximumVisualSpeedMetersPerSecond = 40.0f;

        [SerializeField]
        [Tooltip("Seconds used to smooth apparent dust motion and reject frame-shift noise.")]
        [Min(0.0f)]
        private float velocitySmoothingSeconds = 0.15f;

        [Header("Runtime")]
        [SerializeField]
        private bool hasPoseSample;

        [SerializeField]
        private float rawCameraSpeedMetersPerSecond;

        [SerializeField]
        private float visualDustSpeedMetersPerSecond;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float emissionDensity = 1.0f;

        [SerializeField]
        private Vector3 localDustVelocityMetersPerSecond;

        private UniversePosition previousPosition;
        private Vector3 smoothedUniverseVelocity;
        private float baseEmissionRateMultiplier = 1.0f;
        private bool capturedEmissionRate;

        private void Awake()
        {
            ResolveReferences();
            CaptureParticleSettings();
        }

        private void OnEnable()
        {
            ResolveReferences();
            CaptureParticleSettings();
            ResetMotionSample();
            ConfigureParticleSystem();
        }

        private void OnDisable()
        {
            RestoreEmissionRate();
            ResetMotionSample();
        }

        private void OnValidate()
        {
            fadeOutStartSpeedMetersPerSecond = Mathf.Max(
                0.0f,
                fadeOutStartSpeedMetersPerSecond);
            fadeOutEndSpeedMetersPerSecond = Mathf.Max(
                fadeOutStartSpeedMetersPerSecond + 0.01f,
                fadeOutEndSpeedMetersPerSecond);
            maximumVisualSpeedMetersPerSecond = Mathf.Max(
                0.01f,
                maximumVisualSpeedMetersPerSecond);
            velocitySmoothingSeconds = Mathf.Max(
                0.0f,
                velocitySmoothingSeconds);
        }

        private void LateUpdate()
        {
            ResolveReferences();
            if (dustParticles == null || poseBridge == null ||
                cameraTransform == null ||
                !poseBridge.TryGetUniversePose(out var pose))
            {
                SetEmissionDensity(0.0f);
                return;
            }

            ConfigureParticleSystem();

            if (!hasPoseSample)
            {
                previousPosition = pose.Position;
                hasPoseSample = true;
                smoothedUniverseVelocity = Vector3.zero;
                rawCameraSpeedMetersPerSecond = 0.0f;
                ApplyDustVelocity(Vector3.zero);
                SetEmissionDensity(1.0f);
                return;
            }

            var deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= Mathf.Epsilon ||
                !pose.Position.TryGetOffsetMetersFrom(
                    previousPosition,
                    out var offset))
            {
                return;
            }

            previousPosition = pose.Position;
            var velocity = new Vector3(
                (float)(offset.x / deltaTime),
                (float)(offset.y / deltaTime),
                (float)(offset.z / deltaTime));
            rawCameraSpeedMetersPerSecond = velocity.magnitude;

            var blend = velocitySmoothingSeconds <= Mathf.Epsilon
                ? 1.0f
                : 1.0f - Mathf.Exp(
                    -deltaTime / velocitySmoothingSeconds);
            smoothedUniverseVelocity = Vector3.Lerp(
                smoothedUniverseVelocity,
                velocity,
                blend);

            var visualVelocity = Vector3.ClampMagnitude(
                smoothedUniverseVelocity,
                maximumVisualSpeedMetersPerSecond);
            ApplyDustVelocity(visualVelocity);
            SetEmissionDensity(EvaluateEmissionDensity(
                rawCameraSpeedMetersPerSecond));
        }

        private void ResolveReferences()
        {
            if (cameraTransform == null)
            {
                cameraTransform = Camera.main != null
                    ? Camera.main.transform
                    : transform;
            }

            if (dustParticles == null)
            {
                dustParticles = GetComponent<ParticleSystem>();
            }

            if (poseBridge == null)
            {
                poseBridge = FindFirstObjectByType<SgtUniverseOriginBridge>();
            }
        }

        private void CaptureParticleSettings()
        {
            if (dustParticles == null || capturedEmissionRate)
            {
                return;
            }

            baseEmissionRateMultiplier =
                dustParticles.emission.rateOverTimeMultiplier;
            capturedEmissionRate = true;
        }

        private void ConfigureParticleSystem()
        {
            if (dustParticles == null)
            {
                return;
            }

            var main = dustParticles.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var velocity = dustParticles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
        }

        private void ApplyDustVelocity(Vector3 universeVelocity)
        {
            if (dustParticles == null || cameraTransform == null)
            {
                return;
            }

            // Local particles move opposite the camera's world motion.
            localDustVelocityMetersPerSecond =
                -cameraTransform.InverseTransformDirection(universeVelocity);
            visualDustSpeedMetersPerSecond =
                localDustVelocityMetersPerSecond.magnitude;

            var velocity = dustParticles.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(
                localDustVelocityMetersPerSecond.x);
            velocity.y = new ParticleSystem.MinMaxCurve(
                localDustVelocityMetersPerSecond.y);
            velocity.z = new ParticleSystem.MinMaxCurve(
                localDustVelocityMetersPerSecond.z);
        }

        private float EvaluateEmissionDensity(float speed)
        {
            if (speed <= fadeOutStartSpeedMetersPerSecond)
            {
                return 1.0f;
            }

            if (speed >= fadeOutEndSpeedMetersPerSecond)
            {
                return 0.0f;
            }

            var fraction = Mathf.InverseLerp(
                fadeOutStartSpeedMetersPerSecond,
                fadeOutEndSpeedMetersPerSecond,
                speed);
            return 1.0f - Mathf.SmoothStep(0.0f, 1.0f, fraction);
        }

        private void SetEmissionDensity(float density)
        {
            emissionDensity = Mathf.Clamp01(density);
            if (dustParticles == null)
            {
                return;
            }

            CaptureParticleSettings();
            var emission = dustParticles.emission;
            emission.rateOverTimeMultiplier =
                baseEmissionRateMultiplier * emissionDensity;
        }

        private void RestoreEmissionRate()
        {
            if (dustParticles == null || !capturedEmissionRate)
            {
                return;
            }

            var emission = dustParticles.emission;
            emission.rateOverTimeMultiplier = baseEmissionRateMultiplier;
        }

        private void ResetMotionSample()
        {
            hasPoseSample = false;
            previousPosition = default;
            smoothedUniverseVelocity = Vector3.zero;
            rawCameraSpeedMetersPerSecond = 0.0f;
            visualDustSpeedMetersPerSecond = 0.0f;
            localDustVelocityMetersPerSecond = Vector3.zero;
        }
    }
}
