using System;
using UnityEditor;
using UnityEngine;

namespace jcan.CelestialSystems.Editor
{
    /// <summary>Exercises the external-root trajectory path used by star-system generation.</summary>
    public sealed class ConicTrajectorySpawnTestWindow : EditorWindow
    {
        [SerializeField] private CelestialBodyFactory factory;
        [SerializeField] private CelestialBodyRuntimeContext referenceBody;
        [SerializeField] private CelestialBodyDefinition bodyDefinition;
        [SerializeField] private double periapsisMeters = 384400000;
        [SerializeField] private double eccentricity = 0.5;
        private CelestialBodySystemFactory systemFactory;
        private CelestialBodySystemFactory.GeneratedSystem system;
        private CelestialBodySystemDefinition runtimeDefinition;
        private CelestialBodyRuntimeContext spawnedBody;
        private CelestialBodyRuntimeContext spawnedReference;
        private string status = "Enter Play Mode, assign a factory, reference body, and body definition.";
        private double expectedPeriapsis;
        private double expectedApoapsis;

        [MenuItem("Tools/Celestial Systems/Conic Trajectory Spawn Test")]
        public static void Open() => GetWindow<ConicTrajectorySpawnTestWindow>("Conic Spawn Test");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Spawns one test body at periapsis with e = 0.5 by default. " +
                "The selected reference body keeps its existing motion. Use your normal time controls to watch the orbit.",
                MessageType.Info);
            using (new EditorGUI.DisabledScope(spawnedBody != null))
            {
                factory = (CelestialBodyFactory)EditorGUILayout.ObjectField("Factory", factory, typeof(CelestialBodyFactory), true);
                referenceBody = (CelestialBodyRuntimeContext)EditorGUILayout.ObjectField("Reference Body", referenceBody, typeof(CelestialBodyRuntimeContext), true);
                bodyDefinition = (CelestialBodyDefinition)EditorGUILayout.ObjectField("Body Definition", bodyDefinition, typeof(CelestialBodyDefinition), false);
                periapsisMeters = EditorGUILayout.DoubleField("Periapsis (m)", periapsisMeters);
                eccentricity = EditorGUILayout.DoubleField("Eccentricity", eccentricity);
                using (new EditorGUI.DisabledScope(!Application.isPlaying))
                    if (GUILayout.Button("Spawn Test Body")) Spawn();
            }
            using (new EditorGUI.DisabledScope(spawnedBody == null))
                if (GUILayout.Button("Despawn Test Body")) Cleanup();
            EditorGUILayout.HelpBox(status, MessageType.None);
            if (spawnedBody != null && spawnedReference != null &&
                spawnedBody.TryGetMotionState(out var bodyState) &&
                spawnedReference.TryGetMotionState(out var referenceState) &&
                bodyState.Position.TryGetOffsetMetersFrom(referenceState.Position, out var offset))
            {
                EditorGUILayout.LabelField("Current distance (m)", offset.Magnitude.ToString("N0"));
                EditorGUILayout.LabelField("Expected periapsis (m)", expectedPeriapsis.ToString("N0"));
                EditorGUILayout.LabelField("Expected apoapsis (m)", double.IsPositiveInfinity(expectedApoapsis)
                    ? "Open trajectory" : expectedApoapsis.ToString("N0"));
                EditorGUILayout.LabelField("Motion provider", spawnedBody.MotionProviderName);
            }
        }

        private void OnInspectorUpdate() => Repaint();

        private void Spawn()
        {
            if (factory == null || referenceBody == null || bodyDefinition == null ||
                CelestialTimeController.Instance == null)
            {
                status = "Assign all three references and ensure a Celestial Time Controller is active.";
                return;
            }
            Cleanup();
            var conic = new KeplerianConicTrajectory(referenceBody.InstanceId, periapsisMeters,
                eccentricity, CelestialTimeController.Instance.UniversalTimeSeconds, 0, 0, 0, 0,
                CelestialOrbitDirection.Prograde);
            var trajectory = new CelestialTrajectoryDefinition(conic);
            if (!trajectory.TryValidate(out var error)) { status = error; return; }
            var entry = new CelestialBodySystemDefinition.BodyEntry("orbiter", bodyDefinition, null, null,
                CelestialBodySpawnMode.PrescribedTrajectory, default, default, default, default);
            runtimeDefinition = CelestialBodySystemDefinition.CreateRuntime("conic-spawn-test", new[] { entry });
            systemFactory = new CelestialBodySystemFactory(factory);
            if (!systemFactory.TryGenerate("conic-test-" + Guid.NewGuid().ToString("N"),
                runtimeDefinition, default, default, Quaternion.identity, null,
                CelestialBodySpawnMode.PrescribedTrajectory, referenceBody, trajectory, out system))
            {
                error = systemFactory.LastError;
                Cleanup();
                status = error;
                return;
            }
            system.TryGetBody("orbiter", out spawnedBody);
            spawnedReference = referenceBody;
            expectedPeriapsis = periapsisMeters;
            expectedApoapsis = eccentricity < 1 ? periapsisMeters * (1 + eccentricity) / (1 - eccentricity)
                : double.PositiveInfinity;
            if (spawnedBody == null || spawnedBody.GravityBody != null ||
                !spawnedBody.TryGetMotionState(out var state) ||
                !referenceBody.TryGetMotionState(out var referenceState) ||
                !state.Position.TryGetOffsetMetersFrom(referenceState.Position, out var offset) ||
                Math.Abs(offset.Magnitude - periapsisMeters) > Math.Max(0.1, periapsisMeters * 1e-10))
            {
                Cleanup();
                status = "Spawn validation failed: expected a prescribed body at periapsis with no registered GE body.";
                Debug.LogError(status);
                return;
            }
            status = "Conic factory spawn PASS. Body starts at periapsis using the prescribed conic provider. " +
                "At e = 0.5, apoapsis is three times periapsis.";
            Debug.Log(status, spawnedBody);
            Selection.activeGameObject = spawnedBody.gameObject;
        }

        private void OnDisable() => Cleanup();

        private void Cleanup()
        {
            if (Application.isPlaying && system != null) systemFactory?.TryDespawn(system);
            system = null;
            spawnedBody = null;
            spawnedReference = null;
            if (runtimeDefinition != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(runtimeDefinition);
                else UnityEngine.Object.DestroyImmediate(runtimeDefinition);
            }
            runtimeDefinition = null;
            status = "Test body removed.";
        }
    }
}
