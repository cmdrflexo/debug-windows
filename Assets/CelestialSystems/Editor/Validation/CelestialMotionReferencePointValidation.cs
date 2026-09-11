/*
 * Validates that invisible motion reference points supply mass and prescribed motion without becoming bodies.
 */

using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    public static class CelestialMotionReferencePointValidation
    {
        [MenuItem("Tools/Celestial Systems/Validate Motion Reference Points")]
        public static void Validate()
        {
            var root =
                new GameObject(
                    "Motion Reference Point Validation");

            try
            {
                var frameObject =
                    new GameObject(
                        "Frame");
                frameObject.transform.SetParent(
                    root.transform);
                var frame =
                    frameObject.AddComponent<
                        UniverseFrameController>();
                var timeObject =
                    new GameObject(
                        "Time");
                timeObject.transform.SetParent(
                    root.transform);
                var time =
                    timeObject.AddComponent<
                        CelestialTimeController>();
                var pointObject =
                    new GameObject(
                        "test-barycenter");
                pointObject.transform.SetParent(
                    root.transform);
                var provider =
                    pointObject.AddComponent<
                        TrajectoryCelestialBodyMotionProvider>();
                var point =
                    pointObject.AddComponent<
                        CelestialMotionReferencePoint>();
                var position =
                    new DoubleVector3(
                        10.0,
                        20.0,
                        30.0);

                if (!point.Initialize(
                        "test-barycenter",
                        3.0e30,
                        provider) ||
                    !provider.InitializeInertial(
                        frame,
                        time,
                        pointObject.transform,
                        position,
                        new DoubleVector3(),
                        Quaternion.identity,
                        new DoubleVector3()) ||
                    !point.TryGetMotionState(
                        out _) ||
                    !(point is ICelestialMotionStateSource) ||
                    point.GetComponent<
                        CelestialBodyRuntimeContext>() != null ||
                    point.ConfiguredMassKilograms !=
                        3.0e30 ||
                    Vector3.Distance(
                        point.transform.position,
                        new Vector3(
                            10.0f,
                            20.0f,
                            30.0f)) >
                        0.0001f)
                {
                    Debug.LogError(
                        "Motion reference point validation failed: the point did not provide invisible prescribed motion and mass correctly.");
                    return;
                }

                Debug.Log(
                    "Motion reference point PASS. The reference supplies mass and prescribed motion without a celestial-body context, surface, marker identity, or generated-body count.");
            }
            finally
            {
                Object.DestroyImmediate(
                    root);
            }
        }
    }
}
