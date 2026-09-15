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

        [Header("Reference Frame")]
        [SerializeField]
        [Tooltip("When Free mode is locked to a body, subtract that body's motion before driving dust.")]
        private bool useLockedBodyReferenceFrame = true;

        [SerializeField]
        [Tooltip("Optional body whose motion defines the dust reference frame when no Free-mode lock is active.")]
        private CelestialBodyRuntimeContext referenceBody;

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
        private string activeReferenceFrameName;

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
        private UniversePosition previousReferencePosition;
        private CelestialBodyRuntimeContext sampledReferenceBody;
        private bool hasReferenceFrameSample;
        private Vector3 smoothedUniverseVelocity;
        private FreeUniverseAnchorController freeFlight;
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
            var velocityX = offset.x / deltaTime;
            var velocityY = offset.y / deltaTime;
            var velocityZ = offset.z / deltaTime;

            // The dust frame follows a locked body (or an explicitly supplied
            // body) in the same rendered time domain. Sampling positions rather
            // than using physical velocity also stays correct under time scale.
            if (TryGetReferenceFramePosition(out var referencePosition,
                    out var currentReferenceBody))
            {
                if (hasReferenceFrameSample &&
                    sampledReferenceBody == currentReferenceBody &&
                    referencePosition.TryGetOffsetMetersFrom(
                        previousReferencePosition,
                        out var referenceOffset))
                {
                    velocityX -= referenceOffset.x / deltaTime;
                    velocityY -= referenceOffset.y / deltaTime;
                    velocityZ -= referenceOffset.z / deltaTime;
                }

                previousReferencePosition = referencePosition;
                sampledReferenceBody = currentReferenceBody;
                hasReferenceFrameSample = true;
            }
            else
            {
                hasReferenceFrameSample = false;
                sampledReferenceBody = null;
                activeReferenceFrameName = string.Empty;
            }

            var rawSpeed = Math.Sqrt(
                velocityX * velocityX +
                velocityY * velocityY +
                velocityZ * velocityZ);
            rawCameraSpeedMetersPerSecond = (float)Math.Min(
                rawSpeed,
                float.MaxValue);

            // Clamp before converting to float. Universe-space teleports or
            // time-scale jumps can otherwise overflow a Unity Vector3.
            var visualScale = rawSpeed > maximumVisualSpeedMetersPerSecond &&
                rawSpeed > double.Epsilon
                ? maximumVisualSpeedMetersPerSecond / rawSpeed
                : 1.0;
            var velocity = new Vector3(
                (float)(velocityX * visualScale),
                (float)(velocityY * visualScale),
                (float)(velocityZ * visualScale));

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

            if (freeFlight == null)
            {
                freeFlight = FindFirstObjectByType<FreeUniverseAnchorController>();
            }
        }

        private bool TryGetReferenceFramePosition(
            out UniversePosition position,
            out CelestialBodyRuntimeContext body)
        {
            body = useLockedBodyReferenceFrame && freeFlight != null &&
                freeFlight.IsFeatureLocked
                ? freeFlight.LockedFeatureBody
                : referenceBody;

            if (body != null && body.TryGetMotionState(out var motion))
            {
                position = motion.Position;
                activeReferenceFrameName = body.name;
                return true;
            }

            position = default;
            return false;
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
            previousReferencePosition = default;
            sampledReferenceBody = null;
            hasReferenceFrameSample = false;
            activeReferenceFrameName = string.Empty;
            smoothedUniverseVelocity = Vector3.zero;
            rawCameraSpeedMetersPerSecond = 0.0f;
            visualDustSpeedMetersPerSecond = 0.0f;
            localDustVelocityMetersPerSecond = Vector3.zero;
        }
    }
}
