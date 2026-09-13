/*
 * Registers one body as an optional gravitational lens and provides camera-relative data to the URP fullscreen material.
 */

using System.Collections.Generic;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DefaultExecutionOrder(425)]
    [DisallowMultipleComponent]
    public sealed class CelestialGravitationalLens :
        MonoBehaviour
    {
        private const int MaximumVisibleLenses = 8;

        private static readonly HashSet<
            CelestialGravitationalLens> ActiveLenses =
                new HashSet<
                    CelestialGravitationalLens>();

        private static readonly List<
            ScreenLens> VisibleLenses =
                new List<
                    ScreenLens>();

        private static readonly Vector4[] LensData =
            new Vector4[
                MaximumVisibleLenses];

        private static readonly Vector4[] LensShapeData =
            new Vector4[
                MaximumVisibleLenses];

        private static readonly int LensCountId =
            Shader.PropertyToID(
                "_CelestialGravitationalLensCount");

        private static readonly int LensDataId =
            Shader.PropertyToID(
                "_CelestialGravitationalLensData");

        private static readonly int LensShapeDataId =
            Shader.PropertyToID(
                "_CelestialGravitationalLensShapeData");

        [SerializeField]
        [Tooltip("Defaults to the nearest runtime body context.")]
        private CelestialBodyRuntimeContext bodyContext;

        [SerializeField]
        private bool isRegistered;

        [SerializeField]
        private int lastVisibleLensCount;

        public CelestialBodyRuntimeContext BodyContext =>
            bodyContext;

        public bool IsActive =>
            bodyContext != null &&
            bodyContext.Definition != null &&
            bodyContext.Definition
                .GravitationalLensing
                .HasValidSettings;

        public int LastVisibleLensCount =>
            lastVisibleLensCount;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveLenses.Clear();
            VisibleLenses.Clear();
            Camera.onPreCull -=
                BindVisibleLenses;
            Camera.onPreCull +=
                BindVisibleLenses;
        }

        private void Reset()
        {
            ResolveBodyContext();
        }

        private void Awake()
        {
            ResolveBodyContext();
        }

        private void OnEnable()
        {
            ResolveBodyContext();
            ActiveLenses.Add(
                this);
            isRegistered = true;
        }

        private void OnDisable()
        {
            ActiveLenses.Remove(
                this);
            isRegistered = false;
        }

        private void OnValidate()
        {
            ResolveBodyContext();
        }

        public void Initialize(
            CelestialBodyRuntimeContext newBodyContext)
        {
            bodyContext =
                newBodyContext;
        }

        private void ResolveBodyContext()
        {
            if (bodyContext == null)
            {
                bodyContext =
                    GetComponentInParent<
                        CelestialBodyRuntimeContext>();
            }
        }

        private static void BindVisibleLenses(
            Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            VisibleLenses.Clear();

            foreach (var lens in ActiveLenses)
            {
                if (lens == null ||
                    !lens.TryBuildScreenLens(
                        camera,
                        out var screenLens))
                {
                    continue;
                }

                VisibleLenses.Add(
                    screenLens);
            }

            VisibleLenses.Sort(
                CompareScreenLenses);

            var count =
                Mathf.Min(
                    MaximumVisibleLenses,
                    VisibleLenses.Count);

            for (var index = 0;
                index < count;
                index++)
            {
                LensData[index] =
                    VisibleLenses[index].ShaderData;
                LensShapeData[index] =
                    new Vector4(
                        VisibleLenses[index].Falloff,
                        0.0f,
                        0.0f,
                        0.0f);
                VisibleLenses[index]
                    .Lens.lastVisibleLensCount =
                        count;
            }

            for (var index = count;
                index < MaximumVisibleLenses;
                index++)
            {
                LensData[index] =
                    Vector4.zero;
                LensShapeData[index] =
                    Vector4.zero;
            }

            Shader.SetGlobalInt(
                LensCountId,
                count);
            Shader.SetGlobalVectorArray(
                LensDataId,
                LensData);
            Shader.SetGlobalVectorArray(
                LensShapeDataId,
                LensShapeData);
        }

        private bool TryBuildScreenLens(
            Camera camera,
            out ScreenLens screenLens)
        {
            screenLens = default;

            if (!IsActive ||
                camera.orthographic ||
                bodyContext.VisualRoot == null)
            {
                return false;
            }

            var settings =
                bodyContext.Definition
                    .GravitationalLensing;
            var worldRadius =
                bodyContext.Definition
                    .ReferenceRadiusMeters *
                settings.InfluenceRadiusMultiplier;

            if (!IsFinite(worldRadius) ||
                worldRadius <= 0.0 ||
                worldRadius > float.MaxValue)
            {
                return false;
            }

            var worldCenter =
                bodyContext.VisualRoot.position;
            var center =
                camera.WorldToViewportPoint(
                    worldCenter);

            if (center.z <= 0.0f)
            {
                return false;
            }

            var radiusPoint =
                camera.WorldToViewportPoint(
                    worldCenter +
                    camera.transform.up *
                    (float)worldRadius);
            var screenRadius =
                Mathf.Abs(
                    radiusPoint.y -
                    center.y);
            screenRadius =
                Mathf.Min(
                    screenRadius,
                    settings.MaximumScreenRadius);

            if (screenRadius <= 0.00001f ||
                center.x + screenRadius < 0.0f ||
                center.x - screenRadius > 1.0f ||
                center.y + screenRadius < 0.0f ||
                center.y - screenRadius > 1.0f)
            {
                return false;
            }

            screenLens =
                new ScreenLens(
                    this,
                    new Vector4(
                        center.x,
                        center.y,
                        screenRadius,
                        settings.Strength),
                    settings.Falloff);
            return true;
        }

        private static int CompareScreenLenses(
            ScreenLens left,
            ScreenLens right)
        {
            return right.ShaderData.z.CompareTo(
                left.ShaderData.z);
        }

        private static bool IsFinite(
            double value)
        {
            return
                !double.IsNaN(value) &&
                !double.IsInfinity(value);
        }

        private readonly struct ScreenLens
        {
            public readonly CelestialGravitationalLens Lens;
            public readonly Vector4 ShaderData;
            public readonly float Falloff;

            public ScreenLens(
                CelestialGravitationalLens newLens,
                Vector4 newShaderData,
                float newFalloff)
            {
                Lens = newLens;
                ShaderData = new Vector4(
                    newShaderData.x,
                    newShaderData.y,
                    newShaderData.z,
                    Mathf.Max(
                        0.1f,
                        newShaderData.w));
                Falloff =
                    Mathf.Max(
                        0.1f,
                        newFalloff);
            }
        }
    }
}
