/*
 * Persists detached body-editor configurations as local JSON without modifying Unity definition assets.
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public sealed class SavedCelestialBodyDefinition
    {
        public string definitionId;
        public string instanceId;
        public double massKilograms;
        public double referenceRadiusMeters;
        public int generationSeed;
        public Vector3 northAxis;
        public Vector3 poleReferenceAxis;
        public CelestialSurfaceSystem surfaceSystem;
        public string surfaceDefinitionId;
        public string oceanDefinitionId;
        public string qualityProfileId;
        public CelestialBodySpawnMode spawnMode;
        public DoubleVector3 freePositionMetersFromFrameOrigin;
        public DoubleVector3 freeVelocityMetersPerSecond;
        public Vector3 rotationEulerDegrees;
        public DoubleVector3 angularVelocityRadiansPerSecond;
        public string orbitParentInstanceId;
        public double semiMajorAxisMeters;
        public double eccentricity;
        public double inclinationDegrees;
        public double longitudeAscendingNodeDegrees;
        public double argumentOfPeriapsisDegrees;
        public double trueAnomalyDegrees;

        public string DefinitionId => definitionId;
    }

    [DisallowMultipleComponent]
    public sealed class CelestialBodyDefinitionLibrary : MonoBehaviour
    {
        [Serializable]
        private sealed class SavedFile
        {
            public int version = 1;
            public List<SavedCelestialBodyDefinition> definitions =
                new List<SavedCelestialBodyDefinition>();
        }

        [SerializeField] private CelestialDefinitionCatalog catalog;
        [SerializeField] private string fileName = "celestial-body-definitions.json";
        [SerializeField] private string filePath;
        [SerializeField] private string lastPersistenceError;

        private readonly List<SavedCelestialBodyDefinition> definitions =
            new List<SavedCelestialBodyDefinition>();

        public IReadOnlyList<SavedCelestialBodyDefinition> Definitions => definitions;
        public string FilePath => filePath;
        public string LastPersistenceError => lastPersistenceError;
        public event Action DefinitionsChanged;

        private void Awake()
        {
            ResolvePath();
            LoadFile();
        }

        public void Initialize(CelestialDefinitionCatalog newCatalog)
        {
            catalog = newCatalog;
            ResolvePath();
            LoadFile();
        }

        public bool Save(
            CelestialBodyEditorModel model,
            string previousDefinitionId,
            out string error)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.definitionId))
            {
                error = "A Definition ID is required before saving.";
                return false;
            }

            var id = model.definitionId.Trim();
            RemoveFromMemory(id);
            if (!string.IsNullOrWhiteSpace(previousDefinitionId) &&
                !string.Equals(previousDefinitionId, id, StringComparison.Ordinal))
            {
                RemoveFromMemory(previousDefinitionId);
            }

            definitions.Insert(0, Capture(model, id));
            if (!SaveFile())
            {
                error = lastPersistenceError;
                return false;
            }

            DefinitionsChanged?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool Delete(string definitionId, out string error)
        {
            if (!RemoveFromMemory(definitionId))
            {
                error = "The selected saved definition no longer exists.";
                return false;
            }

            if (!SaveFile())
            {
                error = lastPersistenceError;
                return false;
            }

            DefinitionsChanged?.Invoke();
            error = string.Empty;
            return true;
        }

        public bool LoadInto(
            string definitionId,
            CelestialBodyEditorModel model,
            out string error)
        {
            var saved = Find(definitionId);
            if (saved == null || model == null)
            {
                error = "The selected saved definition is unavailable.";
                return false;
            }

            model.definitionId = saved.definitionId;
            model.instanceId = string.IsNullOrWhiteSpace(saved.instanceId)
                ? saved.definitionId + "-01"
                : saved.instanceId;
            model.massKilograms = saved.massKilograms;
            model.referenceRadiusMeters = saved.referenceRadiusMeters;
            model.generationSeed = saved.generationSeed;
            model.northAxis = saved.northAxis;
            model.poleReferenceAxis = saved.poleReferenceAxis;
            model.surfaceSystem = saved.surfaceSystem;
            model.surfaceDefinition = ResolveSurface(saved.surfaceDefinitionId);
            model.oceanDefinition = ResolveOcean(saved.oceanDefinitionId);
            model.qualityProfile = ResolveQuality(saved.qualityProfileId);
            model.spawnMode = saved.spawnMode;
            model.freePositionMetersFromFrameOrigin = saved.freePositionMetersFromFrameOrigin;
            model.freeVelocityMetersPerSecond = saved.freeVelocityMetersPerSecond;
            model.rotationEulerDegrees = saved.rotationEulerDegrees;
            model.angularVelocityRadiansPerSecond = saved.angularVelocityRadiansPerSecond;
            model.orbitParent = ResolveParent(saved.orbitParentInstanceId);
            model.orbit = new CelestialOrbitParameters
            {
                semiMajorAxisMeters = saved.semiMajorAxisMeters,
                eccentricity = saved.eccentricity,
                inclinationDegrees = saved.inclinationDegrees,
                longitudeAscendingNodeDegrees = saved.longitudeAscendingNodeDegrees,
                argumentOfPeriapsisDegrees = saved.argumentOfPeriapsisDegrees,
                trueAnomalyDegrees = saved.trueAnomalyDegrees
            };

            error = string.Empty;
            return true;
        }

        private static SavedCelestialBodyDefinition Capture(
            CelestialBodyEditorModel model,
            string id)
        {
            var orbit = model.orbit ?? new CelestialOrbitParameters();
            return new SavedCelestialBodyDefinition
            {
                definitionId = id,
                instanceId = model.instanceId,
                massKilograms = model.massKilograms,
                referenceRadiusMeters = model.referenceRadiusMeters,
                generationSeed = model.generationSeed,
                northAxis = model.northAxis,
                poleReferenceAxis = model.poleReferenceAxis,
                surfaceSystem = model.surfaceSystem,
                surfaceDefinitionId = model.surfaceDefinition != null ? model.surfaceDefinition.name : string.Empty,
                oceanDefinitionId = model.oceanDefinition != null ? model.oceanDefinition.name : string.Empty,
                qualityProfileId = model.qualityProfile != null ? model.qualityProfile.name : string.Empty,
                spawnMode = model.spawnMode,
                freePositionMetersFromFrameOrigin = model.freePositionMetersFromFrameOrigin,
                freeVelocityMetersPerSecond = model.freeVelocityMetersPerSecond,
                rotationEulerDegrees = model.rotationEulerDegrees,
                angularVelocityRadiansPerSecond = model.angularVelocityRadiansPerSecond,
                orbitParentInstanceId = model.orbitParent != null ? model.orbitParent.InstanceId : string.Empty,
                semiMajorAxisMeters = orbit.semiMajorAxisMeters,
                eccentricity = orbit.eccentricity,
                inclinationDegrees = orbit.inclinationDegrees,
                longitudeAscendingNodeDegrees = orbit.longitudeAscendingNodeDegrees,
                argumentOfPeriapsisDegrees = orbit.argumentOfPeriapsisDegrees,
                trueAnomalyDegrees = orbit.trueAnomalyDegrees
            };
        }

        private void ResolvePath()
        {
            filePath = Path.Combine(
                Application.persistentDataPath,
                string.IsNullOrWhiteSpace(fileName)
                    ? "celestial-body-definitions.json"
                    : fileName.Trim());
        }

        private void LoadFile()
        {
            definitions.Clear();
            lastPersistenceError = string.Empty;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                var saved = JsonUtility.FromJson<SavedFile>(File.ReadAllText(filePath));
                if (saved?.definitions == null)
                    return;

                var usedIds = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < saved.definitions.Count; i++)
                {
                    var definition = saved.definitions[i];
                    if (definition != null &&
                        !string.IsNullOrWhiteSpace(definition.definitionId) &&
                        usedIds.Add(definition.definitionId))
                    {
                        definitions.Add(definition);
                    }
                }
            }
            catch (Exception exception)
            {
                lastPersistenceError = exception.Message;
                Debug.LogWarning($"Could not load saved celestial definitions: {exception.Message}", this);
            }
        }

        private bool SaveFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var saved = new SavedFile();
                saved.definitions.AddRange(definitions);
                File.WriteAllText(filePath, JsonUtility.ToJson(saved, true));
                lastPersistenceError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                lastPersistenceError = exception.Message;
                Debug.LogWarning($"Could not save celestial definitions: {exception.Message}", this);
                return false;
            }
        }

        private SavedCelestialBodyDefinition Find(string id) =>
            definitions.Find(value => value != null &&
                string.Equals(value.definitionId, id, StringComparison.Ordinal));

        private bool RemoveFromMemory(string id)
        {
            var value = Find(id);
            return value != null && definitions.Remove(value);
        }

        private RoundMapMagicSurfaceDefinition ResolveSurface(string id)
        {
            return catalog != null && catalog.TryGetSurface(id, out var value) ? value : null;
        }

        private OceanDefinition ResolveOcean(string id)
        {
            return catalog != null && catalog.TryGetOcean(id, out var value) ? value : null;
        }

        private RoundMapMagicSurfaceQualityProfile ResolveQuality(string id)
        {
            return catalog != null && catalog.TryGetQuality(id, out var value) ? value : null;
        }

        private static CelestialBodyRuntimeContext ResolveParent(string instanceId)
        {
            foreach (var body in CelestialBodyRuntimeContext.ActiveContexts)
            {
                if (body != null && string.Equals(body.InstanceId, instanceId, StringComparison.Ordinal))
                    return body;
            }
            return null;
        }
    }
}
