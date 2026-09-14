/*
 * Runtime test harness for ring-cell sampling and object-family selection.
 * It deliberately creates no objects; Gizmos show the deterministic result.
 */

using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialRingObjectSetDebugGizmos :
        MonoBehaviour
    {
        [SerializeField]
        private CelestialRingObjectSet objectSet;

        [SerializeField]
        private Camera observerCamera;

        [SerializeField]
        [Min(0)]
        private int ringBandIndex;

        [SerializeField]
        [Range(1, 64)]
        private int radialCellCount =
            8;

        [SerializeField]
        [Range(4, 256)]
        private int angularCellCount =
            48;

        [SerializeField]
        [Range(0.0f, 1.0f)]
        private float minimumPopulation =
            0.05f;

        [Header("Runtime Diagnostics")]
        [SerializeField]
        private int sampledCellCount;

        [SerializeField]
        private int occupiedCellCount;

        [SerializeField]
        private string lastSelectionSummary;

        private CelestialBodyRuntimeContext body;

        public CelestialRingObjectSet ObjectSet =>
            objectSet;

        public int SampledCellCount =>
            sampledCellCount;

        public int OccupiedCellCount =>
            occupiedCellCount;

        public string LastSelectionSummary =>
            lastSelectionSummary;

        private void Awake()
        {
            body =
                GetComponent<
                    CelestialBodyRuntimeContext>();
        }

        private void OnValidate()
        {
            radialCellCount =
                Mathf.Clamp(
                    radialCellCount,
                    1,
                    64);
            angularCellCount =
                Mathf.Clamp(
                    angularCellCount,
                    4,
                    256);
            minimumPopulation =
                Mathf.Clamp01(
                    minimumPopulation);
        }

        private void Update()
        {
            RefreshDiagnostics();
        }

        private void OnDrawGizmosSelected()
        {
            RefreshDiagnostics(
                true);
        }

        private void RefreshDiagnostics(
            bool drawGizmos = false)
        {
            body ??=
                GetComponent<
                    CelestialBodyRuntimeContext>();

            if (body == null ||
                body.Definition == null ||
                body.VisualRoot == null ||
                objectSet == null ||
                !body.Definition.HasRingSystemProperties ||
                ringBandIndex < 0 ||
                ringBandIndex >=
                    body.Definition
                        .RingBandInnerRadiiMeters
                        .Count ||
                ringBandIndex >=
                    body.Definition
                        .RingBandOuterRadiiMeters
                        .Count)
            {
                sampledCellCount = 0;
                occupiedCellCount = 0;
                lastSelectionSummary =
                    "Assign a ringed runtime body and ring object set.";
                return;
            }

            var definition =
                body.Definition;
            var innerRadius =
                definition.RingBandInnerRadiiMeters[
                    ringBandIndex];
            var outerRadius =
                definition.RingBandOuterRadiiMeters[
                    ringBandIndex];
            var radialWidth =
                outerRadius -
                innerRadius;

            if (radialWidth <=
                0.0 ||
                outerRadius >
                    float.MaxValue)
            {
                lastSelectionSummary =
                    "Selected band has invalid radii.";
                return;
            }

            var orientation =
                Quaternion.FromToRotation(
                    Vector3.up,
                    definition.NorthAxis.normalized);
            var radialStep =
                radialWidth /
                radialCellCount;
            sampledCellCount = 0;
            occupiedCellCount = 0;
            lastSelectionSummary =
                "No occupied cells.";

            for (var radialIndex = 0;
                radialIndex < radialCellCount;
                radialIndex++)
            {
                var radiusFraction =
                    (radialIndex + 0.5f) /
                    radialCellCount;

                if (!CelestialRingSampling.TrySample(
                        definition,
                        ringBandIndex,
                        radiusFraction,
                        out var sample))
                {
                    continue;
                }

                for (var angularIndex = 0;
                    angularIndex < angularCellCount;
                    angularIndex++)
                {
                    sampledCellCount++;
                    var cell =
                        new CelestialRingPolarCell(
                            ringBandIndex,
                            radialIndex,
                            angularIndex);
                    var occupancyHash =
                        CelestialRingSampling
                            .GetStableCellHash(
                                definition.GenerationSeed,
                                definition.DefinitionId,
                                cell,
                                0u);
                    var occupied =
                        sample.Population >=
                            minimumPopulation &&
                        CelestialRingSampling
                            .HashToUnitFloat(
                                occupancyHash) <
                            sample.Population;

                    if (!occupied)
                    {
                        continue;
                    }

                    occupiedCellCount++;
                    var selectionHash =
                        CelestialRingSampling
                            .GetStableCellHash(
                                definition.GenerationSeed,
                                definition.DefinitionId,
                                cell,
                                1u);
                    objectSet.TrySelectFamily(
                        sample,
                        CelestialRingSampling
                            .HashToUnitFloat(
                                selectionHash),
                        out var family);
                    lastSelectionSummary =
                        family != null
                            ? $"Band {ringBandIndex + 1}, cell {radialIndex}/{angularIndex}: {family.DisplayName}"
                            : $"Band {ringBandIndex + 1}, cell {radialIndex}/{angularIndex}: no eligible family";

                    if (drawGizmos)
                    {
                        DrawCellMarker(
                            orientation,
                            sample,
                            angularIndex,
                            radialStep,
                            family);
                    }
                }
            }
        }

        private void DrawCellMarker(
            Quaternion orientation,
            CelestialRingSample sample,
            int angularIndex,
            double radialStep,
            CelestialRingObjectFamily family)
        {
            var angle =
                (angularIndex + 0.5f) /
                angularCellCount *
                Mathf.PI *
                2.0f;
            var localDirection =
                orientation *
                new Vector3(
                    Mathf.Cos(
                        angle),
                    0.0f,
                    Mathf.Sin(
                        angle));
            var localPosition =
                localDirection *
                (float)sample.RadiusMeters;
            var worldPosition =
                body.VisualRoot.TransformPoint(
                    localPosition);
            var brightness =
                Mathf.Lerp(
                    0.2f,
                    1.0f,
                    sample.Density);
            var color =
                new Color(
                    sample.Albedo.r *
                        brightness,
                    sample.Albedo.g *
                        brightness,
                    sample.Albedo.b *
                        brightness,
                    1.0f);

            Gizmos.color =
                family != null
                    ? color
                    : Color.magenta;
            var arcLength =
                2.0 * Mathf.PI *
                sample.RadiusMeters /
                angularCellCount;
            var markerRadius =
                Mathf.Clamp(
                    Mathf.Min(
                        (float)radialStep,
                        (float)arcLength) *
                    0.18f,
                    1.0f,
                    25000.0f);
            Gizmos.DrawSphere(
                worldPosition,
                markerRadius);
        }
    }
}
