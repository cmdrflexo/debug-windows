/*
 * Temporary moonlet test component. Attach it to a planet/body to create one
 * simple spherical moonlet in that body's local ring plane. It deliberately
 * contains only the data and circular motion needed for visual testing; later
 * generation can replace the sphere and use this orbit to create displacement
 * waves in nearby rings.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class MoonletGenerator : MonoBehaviour
    {
        [Header("Moonlet Properties")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Physical diameter of the temporary sphere, in metres.")]
        private float physicalSizeMeters = 100.0f;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Stored physical mass for future ring-wave and gravity work, in kilograms.")]
        private double massKilograms = 1.0e12;

        [Header("Circular Test Orbit")]
        [SerializeField]
        [Min(0.01f)]
        [Tooltip("Distance from this body's centre to the moonlet centre, in metres.")]
        private float orbitRadiusMeters = 10000.0f;

        [SerializeField]
        [Tooltip("Initial position around the local XZ ring plane, in degrees.")]
        private float orbitPhaseDegrees;

        [SerializeField]
        [Min(0.0f)]
        [Tooltip("Time for one full visual orbit. Set to zero to leave the moonlet stationary.")]
        private float orbitPeriodSeconds = 120.0f;

        [SerializeField]
        private bool orbitClockwise;

        [Header("Temporary Sphere")]
        [SerializeField]
        [Tooltip("Optional material for the temporary primitive sphere.")]
        private Material material;

        [SerializeField]
        private bool castShadows = true;

        [SerializeField]
        private bool receiveShadows = true;

        [SerializeField, HideInInspector]
        private GameObject moonletObject;

        private float currentPhaseDegrees;

        public float PhysicalSizeMeters => physicalSizeMeters;
        public double MassKilograms => massKilograms;
        public float OrbitRadiusMeters => orbitRadiusMeters;
        public float OrbitPhaseDegrees => currentPhaseDegrees;

        private void OnEnable()
        {
            currentPhaseDegrees = orbitPhaseDegrees;
            EnsureMoonlet();
            UpdateMoonletTransform();
        }

        private void Update()
        {
            if (!Application.isPlaying || orbitPeriodSeconds <= 0.0f)
            {
                return;
            }

            var direction = orbitClockwise ? -1.0f : 1.0f;
            currentPhaseDegrees = Mathf.Repeat(
                currentPhaseDegrees + direction * 360.0f * Time.deltaTime / orbitPeriodSeconds,
                360.0f);

            UpdateMoonletTransform();
        }

        private void OnValidate()
        {
            physicalSizeMeters = Mathf.Max(0.01f, physicalSizeMeters);
            orbitRadiusMeters = Mathf.Max(0.01f, orbitRadiusMeters);
            orbitPeriodSeconds = Mathf.Max(0.0f, orbitPeriodSeconds);
            currentPhaseDegrees = orbitPhaseDegrees;

            if (moonletObject != null)
            {
                ConfigureRenderer();
                UpdateMoonletTransform();
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying && moonletObject != null)
            {
                Destroy(moonletObject);
                moonletObject = null;
            }
        }

        private void EnsureMoonlet()
        {
            if (moonletObject == null)
            {
                moonletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moonletObject.name = "Test Moonlet";
                moonletObject.transform.SetParent(transform, false);
            }

            ConfigureRenderer();
        }

        private void ConfigureRenderer()
        {
            moonletObject.transform.localScale = Vector3.one * physicalSizeMeters;

            var renderer = moonletObject.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            renderer.shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On
                : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
        }

        private void UpdateMoonletTransform()
        {
            if (moonletObject == null)
            {
                return;
            }

            var phaseRadians = currentPhaseDegrees * Mathf.Deg2Rad;
            moonletObject.transform.localPosition = new Vector3(
                Mathf.Cos(phaseRadians) * orbitRadiusMeters,
                0.0f,
                Mathf.Sin(phaseRadians) * orbitRadiusMeters);

            // Keep the visual tangent-aligned; this makes a later non-spherical
            // generated moonlet face a consistent orbital direction.
            moonletObject.transform.localRotation =
                Quaternion.Euler(0.0f, -currentPhaseDegrees, 0.0f);
        }
    }
}
