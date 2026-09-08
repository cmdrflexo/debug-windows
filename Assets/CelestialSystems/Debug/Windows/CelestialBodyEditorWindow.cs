/*
 * Presents the detached celestial-body editor model through the generic tabbed debug-window framework.
 */

using System;
using System.Collections.Generic;
using jcan.DebugWindows;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class CelestialBodyEditorWindow : MonoBehaviour
    {
        private const string WindowId = "jcan.celestialsystems.body-editor";

        [SerializeField] private CelestialDefinitionCatalog catalog;
        [SerializeField] private CelestialBodyFactory factory;
        [SerializeField] private CelestialBodyDefinition defaultDefinition;
        [SerializeField] private CelestialBodyDefinitionLibrary definitionLibrary;
        [SerializeField] private Vector2 preferredContentSize = new Vector2(500.0f, 380.0f);
        [SerializeField] private Vector2 defaultPosition = new Vector2(12.0f, -220.0f);

        private CelestialBodyEditorModel model;
        private DebugTabbedWindow window;
        private string status = "Ready.";
        private string loadedSavedDefinitionId;
        private string selectedBodyOptionId;
        private bool applicationIsQuitting;

        private void OnEnable()
        {
            applicationIsQuitting = false;
            if (factory == null)
                factory = FindFirstObjectByType<CelestialBodyFactory>();
            if (definitionLibrary == null)
                definitionLibrary = GetComponent<CelestialBodyDefinitionLibrary>();
            if (definitionLibrary == null)
                definitionLibrary = gameObject.AddComponent<CelestialBodyDefinitionLibrary>();
            definitionLibrary.Initialize(catalog);

            ResetModel();
            window = new DebugTabbedWindow(
                WindowId,
                "Body Configuration",
                new[]
                {
                    new DebugTabbedPage("definition", "Definition", BuildDefinitionPage),
                    new DebugTabbedPage("physical", "Physical", BuildPhysicalPage),
                    new DebugTabbedPage("surface", "Surface", BuildSurfacePage),
                    new DebugTabbedPage("ocean", "Ocean", BuildOceanPage),
                    new DebugTabbedPage("motion", "Motion / Orbit", BuildMotionPage),
                    new DebugTabbedPage("spawn", "Spawn", BuildSpawnPage)
                },
                BuildFooter,
                preferredContentSize,
                DebugWindowDisplayState.Closed,
                defaultPosition);

            if (!DebugWindowRegistry.Register(window.CreateRegistration()))
                window = null;
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }

        private void OnDisable()
        {
            if (!applicationIsQuitting)
                DebugWindowRegistry.Unregister(WindowId);
            window = null;
        }

        private void BuildDefinitionPage(DebugWindowFormContent content)
        {
            content.AddChoice(
                "Body Preset",
                BodyOptions(),
                selectedBodyOptionId,
                LoadBodyOption);
            content.AddTextField("Instance ID", model.instanceId, value => model.instanceId = value);
            content.AddTextField("Definition ID", model.definitionId, value => model.definitionId = value);
            content.AddNumberField("Generation Seed", model.generationSeed, value => model.generationSeed = (int)value);
        }

        private void BuildPhysicalPage(DebugWindowFormContent content)
        {
            content.AddNumberField("Mass", model.massKilograms, value => model.massKilograms = value, "kg");
            content.AddNumberField("Reference Radius", model.referenceRadiusMeters, value => model.referenceRadiusMeters = value, "m");
            content.AddReadOnly("Diameter", () => (model.referenceRadiusMeters * 2.0).ToString("N1") + " m");
            content.AddReadOnly(
                "Surface Gravity",
                () => model.referenceRadiusMeters > 0.0
                    ? (6.67430e-11 * model.massKilograms /
                        (model.referenceRadiusMeters * model.referenceRadiusMeters)).ToString("N3") + " m/s²"
                    : "--");
        }

        private void BuildSurfacePage(DebugWindowFormContent content)
        {
            content.AddChoice(
                "Surface System",
                EnumOptions<CelestialSurfaceSystem>(),
                model.surfaceSystem.ToString(),
                option =>
                {
                    if (option?.Value is CelestialSurfaceSystem value)
                        model.surfaceSystem = value;
                });
            content.AddChoice(
                "Surface Preset",
                SurfaceOptions(),
                model.surfaceDefinition != null ? model.surfaceDefinition.name : "none",
                option => model.surfaceDefinition = option?.Value as RoundMapMagicSurfaceDefinition);
            content.AddChoice(
                "Quality Profile",
                QualityOptions(),
                model.qualityProfile != null ? model.qualityProfile.name : "none",
                option => model.qualityProfile = option?.Value as RoundMapMagicSurfaceQualityProfile);
        }

        private void BuildOceanPage(DebugWindowFormContent content)
        {
            content.AddChoice(
                "Ocean Preset",
                OceanOptions(),
                model.oceanDefinition != null ? model.oceanDefinition.name : "none",
                option => model.oceanDefinition = option?.Value as OceanDefinition);
            content.AddReadOnly(
                "Surface Elevation",
                () => model.oceanDefinition != null
                    ? model.oceanDefinition.GlobalSurfaceElevationMeters.ToString("N1") + " m"
                    : "--");
        }

        private void BuildMotionPage(DebugWindowFormContent content)
        {
            content.AddChoice(
                "Motion Mode",
                EnumOptions<CelestialBodySpawnMode>(),
                model.spawnMode.ToString(),
                option =>
                {
                    if (option?.Value is CelestialBodySpawnMode value)
                    {
                        model.spawnMode = value;
                        window.RefreshActivePage();
                    }
                });

            if (model.spawnMode == CelestialBodySpawnMode.FreeSimulation)
                BuildFreeMotionFields(content);
            else
                BuildOrbitFields(content);
        }

        private void BuildFreeMotionFields(DebugWindowFormContent content)
        {
            AddDoubleVector(content, "Position X", "Position Y", "Position Z",
                () => model.freePositionMetersFromFrameOrigin,
                value => model.freePositionMetersFromFrameOrigin = value,
                "m");
            AddDoubleVector(content, "Velocity X", "Velocity Y", "Velocity Z",
                () => model.freeVelocityMetersPerSecond,
                value => model.freeVelocityMetersPerSecond = value,
                "m/s");
            content.AddNumberField("Rotation X", model.rotationEulerDegrees.x, value => SetRotation(0, value), "°");
            content.AddNumberField("Rotation Y", model.rotationEulerDegrees.y, value => SetRotation(1, value), "°");
            content.AddNumberField("Rotation Z", model.rotationEulerDegrees.z, value => SetRotation(2, value), "°");
        }

        private void BuildOrbitFields(DebugWindowFormContent content)
        {
            content.AddChoice(
                "Parent Body",
                ParentOptions(),
                model.orbitParent != null ? model.orbitParent.InstanceId : "none",
                option => model.orbitParent = option?.Value as CelestialBodyRuntimeContext);
            content.AddNumberField("Semi-major Axis", model.orbit.semiMajorAxisMeters, value => model.orbit.semiMajorAxisMeters = value, "m");
            content.AddNumberField("Eccentricity", model.orbit.eccentricity, value => model.orbit.eccentricity = value);
            content.AddNumberField("Inclination", model.orbit.inclinationDegrees, value => model.orbit.inclinationDegrees = value, "°");
            content.AddNumberField("Ascending Node", model.orbit.longitudeAscendingNodeDegrees, value => model.orbit.longitudeAscendingNodeDegrees = value, "°");
            content.AddNumberField("Periapsis Argument", model.orbit.argumentOfPeriapsisDegrees, value => model.orbit.argumentOfPeriapsisDegrees = value, "°");
            content.AddNumberField("True Anomaly", model.orbit.trueAnomalyDegrees, value => model.orbit.trueAnomalyDegrees = value, "°");
        }

        private void BuildSpawnPage(DebugWindowFormContent content)
        {
            content.AddReadOnly("Factory Ready", () => factory != null && factory.CanSpawnBodies ? "Yes" : "No");
            content.AddReadOnly("Motion Mode", () => model.spawnMode.ToString());
            content.AddReadOnly("Status", () => status);
        }

        private void BuildFooter(DebugWindowFormContent content)
        {
            content.AddButton("new", "New", ResetAndRefresh);
            content.AddButton("save", "Save", SaveDefinition);
            content.AddButton("delete", "Delete", DeleteDefinition);
            content.AddButton("spawn", "Spawn", Spawn);
        }

        private void SaveDefinition()
        {
            if (definitionLibrary == null)
            {
                status = "The definition library is unavailable.";
                return;
            }

            if (!definitionLibrary.Save(model, loadedSavedDefinitionId, out var error))
            {
                status = error;
                return;
            }

            loadedSavedDefinitionId = model.definitionId.Trim();
            selectedBodyOptionId = "saved:" + loadedSavedDefinitionId;
            status = $"Saved '{loadedSavedDefinitionId}'.";
            window.RefreshActivePage();
        }

        private void DeleteDefinition()
        {
            if (definitionLibrary == null ||
                string.IsNullOrWhiteSpace(loadedSavedDefinitionId))
            {
                status = "Select a saved definition before deleting.";
                return;
            }

            if (!definitionLibrary.Delete(loadedSavedDefinitionId, out var error))
            {
                status = error;
                return;
            }

            ResetModel();
            status = "Deleted saved definition.";
            window.RefreshActivePage();
        }

        private void Spawn()
        {
            if (factory == null)
            {
                status = "No CelestialBodyFactory is assigned.";
                return;
            }

            if (!model.TryBuildSpawnRequest(factory.UniverseFrame, out var request, out var error))
            {
                status = error;
                return;
            }

            if (!factory.TrySpawnBody(request, out var body))
            {
                status = factory.LastError;
                return;
            }

            status = $"Spawned '{body.InstanceId}'.";
            window.RefreshActivePage();
        }

        private void ResetAndRefresh()
        {
            ResetModel();
            status = "New unsaved body.";
            window?.RefreshActivePage();
        }

        private void ResetModel()
        {
            model = new CelestialBodyEditorModel();
            loadedSavedDefinitionId = null;
            selectedBodyOptionId = null;
            var source = defaultDefinition;
            if (source == null && catalog != null && catalog.BodyDefinitions.Count > 0)
                source = catalog.BodyDefinitions[0];
            if (source != null)
            {
                model.Load(source);
                model.instanceId = source.DefinitionId + "-01";
                selectedBodyOptionId = "asset:" + source.DefinitionId;
            }
        }

        private void LoadBodyOption(DebugChoiceOption option)
        {
            if (option?.Value is CelestialBodyDefinition asset)
            {
                model.Load(asset);
                loadedSavedDefinitionId = null;
                selectedBodyOptionId = option.UniqueId;
                status = $"Loaded catalog definition '{asset.DefinitionId}'.";
            }
            else if (option?.Value is SavedCelestialBodyDefinition saved &&
                definitionLibrary != null &&
                definitionLibrary.LoadInto(saved.DefinitionId, model, out var error))
            {
                loadedSavedDefinitionId = saved.DefinitionId;
                selectedBodyOptionId = option.UniqueId;
                status = $"Loaded saved definition '{saved.DefinitionId}'.";
            }
            else
            {
                status = "The selected definition could not be loaded.";
            }

            window.RefreshActivePage();
        }

        private List<DebugChoiceOption> BodyOptions()
        {
            var result = new List<DebugChoiceOption>();
            if (catalog != null)
            {
                for (var i = 0; i < catalog.BodyDefinitions.Count; i++)
                {
                    var value = catalog.BodyDefinitions[i];
                    if (value != null)
                    {
                        result.Add(new DebugChoiceOption(
                            "asset:" + value.DefinitionId,
                            value.DefinitionId,
                            value));
                    }
                }
            }

            if (definitionLibrary != null)
            {
                for (var i = 0; i < definitionLibrary.Definitions.Count; i++)
                {
                    var value = definitionLibrary.Definitions[i];
                    if (value != null)
                    {
                        result.Add(new DebugChoiceOption(
                            "saved:" + value.DefinitionId,
                            value.DefinitionId + " [saved]",
                            value));
                    }
                }
            }

            return result;
        }

        private List<DebugChoiceOption> SurfaceOptions()
        {
            var result = NoneOption();
            if (catalog != null)
                AddAssets(result, catalog.SurfaceDefinitions);
            return result;
        }

        private List<DebugChoiceOption> OceanOptions()
        {
            var result = NoneOption();
            if (catalog != null)
                AddAssets(result, catalog.OceanDefinitions);
            return result;
        }

        private List<DebugChoiceOption> QualityOptions()
        {
            var result = NoneOption();
            if (catalog != null)
                AddAssets(result, catalog.QualityProfiles);
            return result;
        }

        private static List<DebugChoiceOption> ParentOptions()
        {
            var result = NoneOption();
            foreach (var body in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (body != null && !string.IsNullOrWhiteSpace(body.InstanceId))
                    result.Add(new DebugChoiceOption(body.InstanceId, body.InstanceId, body));
            }
            return result;
        }

        private static List<DebugChoiceOption> EnumOptions<T>() where T : struct, Enum
        {
            var result = new List<DebugChoiceOption>();
            foreach (var value in Enum.GetValues(typeof(T)))
                result.Add(new DebugChoiceOption(value.ToString(), SplitName(value.ToString()), value));
            return result;
        }

        private static List<DebugChoiceOption> NoneOption() =>
            new List<DebugChoiceOption> { new DebugChoiceOption("none", "None") };

        private static void AddAssets<T>(List<DebugChoiceOption> result, IReadOnlyList<T> assets)
            where T : UnityEngine.Object
        {
            for (var i = 0; i < assets.Count; i++)
            {
                var value = assets[i];
                if (value != null)
                    result.Add(new DebugChoiceOption(value.name, value.name, value));
            }
        }

        private static string SplitName(string value)
        {
            for (var i = 1; i < value.Length; i++)
            {
                if (char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
                    value = value.Insert(i++, " ");
            }
            return value;
        }

        private static void AddDoubleVector(
            DebugWindowFormContent content,
            string xLabel, string yLabel, string zLabel,
            Func<DoubleVector3> get, Action<DoubleVector3> set, string units)
        {
            content.AddNumberField(xLabel, get().x, value => { var v = get(); v.x = value; set(v); }, units);
            content.AddNumberField(yLabel, get().y, value => { var v = get(); v.y = value; set(v); }, units);
            content.AddNumberField(zLabel, get().z, value => { var v = get(); v.z = value; set(v); }, units);
        }

        private void SetRotation(int axis, double value)
        {
            var rotation = model.rotationEulerDegrees;
            if (axis == 0) rotation.x = (float)value;
            else if (axis == 1) rotation.y = (float)value;
            else rotation.z = (float)value;
            model.rotationEulerDegrees = rotation;
        }
    }
}
