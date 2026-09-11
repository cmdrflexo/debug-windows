/*
 * Stores named universe-generation seeds in a small persistent JSON library.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public sealed class SavedUniverseSeed
    {
        public string uniqueId;
        public int seed;
        public string displayName;
    }

    [DisallowMultipleComponent]
    public sealed class UniverseSeedLibrary : MonoBehaviour
    {
        [Serializable]
        private sealed class SavedData
        {
            public int version = 1;
            public List<SavedUniverseSeed> entries =
                new List<SavedUniverseSeed>();
        }

        [SerializeField]
        private string fileName = "jcan-universe-seeds.json";

        [SerializeField]
        private string filePath;

        [SerializeField]
        private string lastError;

        private readonly List<SavedUniverseSeed> entries =
            new List<SavedUniverseSeed>();

        public IReadOnlyList<SavedUniverseSeed> Entries => entries;
        public string FilePath => filePath;
        public string LastError => lastError;

        public event Action EntriesChanged;

        private void Awake()
        {
            filePath = Path.Combine(
                Application.persistentDataPath,
                string.IsNullOrWhiteSpace(fileName)
                    ? "jcan-universe-seeds.json"
                    : fileName.Trim());
            Load();
        }

        public bool TrySave(
            string existingId,
            string seedText,
            string displayName,
            out SavedUniverseSeed saved)
        {
            saved = null;
            if (!int.TryParse(
                    seedText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var seed))
            {
                lastError =
                    "Seed must be a whole number from " +
                    int.MinValue.ToString(CultureInfo.InvariantCulture) +
                    " to " +
                    int.MaxValue.ToString(CultureInfo.InvariantCulture) +
                    ".";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(existingId))
                saved = Find(existingId);
            if (saved == null)
                saved = FindBySeed(seed);

            var created = saved == null;
            if (created)
            {
                saved = new SavedUniverseSeed
                {
                    uniqueId = Guid.NewGuid().ToString("N")
                };
                entries.Add(saved);
            }

            var previousSeed = saved.seed;
            var previousName = saved.displayName;
            saved.seed = seed;
            saved.displayName = displayName?.Trim() ?? string.Empty;
            if (SaveChanges())
                return true;

            if (created)
                entries.Remove(saved);
            else
            {
                saved.seed = previousSeed;
                saved.displayName = previousName;
            }

            saved = null;
            return false;
        }

        public bool Delete(string uniqueId)
        {
            var entry = Find(uniqueId);
            if (entry == null)
            {
                lastError = "The selected seed no longer exists.";
                return false;
            }

            var index = entries.IndexOf(entry);
            entries.RemoveAt(index);
            if (SaveChanges())
                return true;

            entries.Insert(index, entry);
            return false;
        }

        public SavedUniverseSeed Find(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
                return null;

            return entries.Find(
                value => value != null &&
                    string.Equals(
                        value.uniqueId,
                        uniqueId,
                        StringComparison.Ordinal));
        }

        private SavedUniverseSeed FindBySeed(int seed)
        {
            return entries.Find(
                value => value != null && value.seed == seed);
        }

        private void Load()
        {
            entries.Clear();
            lastError = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath) ||
                !File.Exists(filePath))
            {
                return;
            }

            try
            {
                var data = JsonUtility.FromJson<SavedData>(
                    File.ReadAllText(filePath));
                if (data?.entries != null)
                {
                    for (var i = 0; i < data.entries.Count; i++)
                    {
                        var entry = data.entries[i];
                        if (entry == null)
                            continue;
                        if (string.IsNullOrWhiteSpace(entry.uniqueId))
                            entry.uniqueId = Guid.NewGuid().ToString("N");
                        entry.displayName =
                            entry.displayName?.Trim() ?? string.Empty;
                        entries.Add(entry);
                    }
                }
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning(
                    $"Could not load universe seeds: {exception.Message}",
                    this);
            }
        }

        private bool SaveChanges()
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var data = new SavedData();
                data.entries.AddRange(entries);
                File.WriteAllText(
                    filePath,
                    JsonUtility.ToJson(data, true));
                lastError = string.Empty;
                EntriesChanged?.Invoke();
                return true;
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                Debug.LogWarning(
                    $"Could not save universe seeds: {exception.Message}",
                    this);

                return false;
            }
        }
    }
}
