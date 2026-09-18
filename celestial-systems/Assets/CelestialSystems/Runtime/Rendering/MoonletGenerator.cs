/*
 * Temporary moonlet test component. Attach it to a convenient scene-level
 * object. It follows the current body reported by UniverseLocalEnvironmentContext
 * and creates a basic sphere in that body's local XZ ring plane.
 *
 * The context is resolved by name while its API is still being established.
 * Replace that small adapter with a direct dependency once the context script is
 * committed to this branch.
 */

using System.Reflection;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class MoonletGenerator : MonoBehaviour
    {
        [Header("Local Environment")]
        [SerializeField]
        [Tooltip("Optional explicit UniverseLocalEnvironmentContext. Leave empty to find it in the scene.")]
        private MonoBehaviour localEnvironmentContext;

        [SerializeField, HideInInspector]
        private Transform currentBody;

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
        [Tooltip("Distance from the current body's centre to the moonlet centre, in metres.")]
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
        public Transform CurrentBody => currentBody;

        private void OnEnable()
        {
            currentPhaseDegrees = orbitPhaseDegrees;
            ResolveCurrentBody();
        }

        private void Update()
        {
            ResolveCurrentBody();

            if (currentBody == null)
            {
                return;
            }

            EnsureMoonlet();

            if (Application.isPlaying && orbitPeriodSeconds > 0.0f)
            {
                var direction = orbitClockwise ? -1.0f : 1.0f;
                currentPhaseDegrees = Mathf.Repeat(
                    currentPhaseDegrees + direction * 360.0f * Time.deltaTime / orbitPeriodSeconds,
                    360.0f);
            }

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

        private void ResolveCurrentBody()
        {
            if (localEnvironmentContext == null)
            {
                localEnvironmentContext = FindLocalEnvironmentContext();
            }

            var nextBody = GetContextBodyTransform(localEnvironmentContext);
            if (nextBody == currentBody)
            {
                return;
            }

            currentBody = nextBody;
            if (moonletObject == null)
            {
                return;
            }

            moonletObject.SetActive(currentBody != null);
            if (currentBody == null)
            {
                return;
            }

            moonletObject.transform.SetParent(currentBody, false);
            ConfigureRenderer();
            UpdateMoonletTransform();
        }

        private static MonoBehaviour FindLocalEnvironmentContext()
        {
            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (var behaviour in behaviours)
            {
                if (behaviour.GetType().Name == "UniverseLocalEnvironmentContext")
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static Transform GetContextBodyTransform(MonoBehaviour context)
        {
            if (context == null)
            {
                return null;
            }

            const BindingFlags Flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            // CurrentBody is the intended context API. The alternatives keep the
            // temporary test component usable while the context API is finalized.
            var type = context.GetType();
            object body = null;
            foreach (var memberName in new[]
            {
                "CurrentBody",
                "CurrentBodyContext",
                "LocalBody",
                "LocalBodyContext"
            })
            {
                var property = type.GetProperty(memberName, Flags);
                if (property != null)
                {
                    body = property.GetValue(context);
                    break;
                }

                var field = type.GetField(memberName, Flags);
                if (field != null)
                {
                    body = field.GetValue(context);
                    break;
                }
            }

            return body switch
            {
                Transform transform => transform,
                Component component => component.transform,
                GameObject gameObject => gameObject.transform,
                _ => null
            };
        }

        private void EnsureMoonlet()
        {
            if (currentBody == null)
            {
                return;
            }

            if (moonletObject == null)
            {
                moonletObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                moonletObject.name = "Test Moonlet";
                moonletObject.transform.SetParent(currentBody, false);
            }

            moonletObject.SetActive(true);
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
            if (moonletObject == null || currentBody == null)
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
