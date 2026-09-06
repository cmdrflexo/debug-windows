/*
 * Stores named free-camera anchor poses independently of the debug-window presentation.
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [Serializable]
    public sealed class CameraAnchorPose
    {
        [SerializeField]
        private string uniqueId;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private UniversePosition position;

        [SerializeField]
        private Quaternion rotation;

        public string UniqueId => uniqueId;
        public string DisplayName => displayName;
        public UniversePosition Position => position;
        public Quaternion Rotation => rotation;

        internal CameraAnchorPose(
            string uniqueId,
            string displayName,
            UniverseMotionState pose)
        {
            this.uniqueId = uniqueId;
            this.displayName = displayName;
            position = pose.Position;
            rotation = pose.Rotation;
        }

        internal void SetPose(string newDisplayName, UniverseMotionState pose)
        {
            displayName = newDisplayName;
            position = pose.Position;
            rotation = pose.Rotation;
        }

        internal void Normalize()
        {
            position = new UniversePosition(
                position.CellX,
                position.CellY,
                position.CellZ,
                position.LocalXMeters,
                position.LocalYMeters,
                position.LocalZMeters);

            var magnitudeSquared =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;
            rotation = magnitudeSquared > Mathf.Epsilon
                ? Quaternion.Normalize(rotation)
                : Quaternion.identity;
        }
    }

    [DisallowMultipleComponent]
    public sealed class CameraAnchorPoseLibrary : MonoBehaviour
    {
        [Serializable]
        private sealed class SavedPoseFile
        {
            public int version = 1;
            public List<CameraAnchorPose> poses = new List<CameraAnchorPose>();
        }

        [Header("Anchor")]
        [SerializeField]
        private SgtGravityOriginBridge anchorSource;

        [Header("Persistence")]
        [SerializeField]
        private string fileName = "camera-anchor-poses.json";

        [Header("Runtime")]
        [SerializeField]
        private string filePath;

        [SerializeField]
        private string lastPersistenceError;

        private readonly List<CameraAnchorPose> poses =
            new List<CameraAnchorPose>();

        public IReadOnlyList<CameraAnchorPose> Poses => poses;
        public string FilePath => filePath;
        public string LastPersistenceError => lastPersistenceError;

        public event Action PosesChanged;

        private void Awake()
        {
            if (anchorSource == null)
                anchorSource = FindFirstObjectByType<SgtGravityOriginBridge>();

            filePath = Path.Combine(
                Application.persistentDataPath,
                string.IsNullOrWhiteSpace(fileName)
                    ? "camera-anchor-poses.json"
                    : fileName.Trim());

            LoadFile();
        }

        public string CreateDefaultPoseName()
        {
            return "saved_pose-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        public bool SaveCurrentPose(string displayName)
        {
            var normalizedName = displayName?.Trim();
            if (string.IsNullOrEmpty(normalizedName))
                normalizedName = CreateDefaultPoseName();

            if (anchorSource == null ||
                !anchorSource.TryGetUniversePose(out var pose))
            {
                return SetError("The active free-camera anchor pose is unavailable.");
            }

            var existing = FindByName(normalizedName);
            if (existing != null)
            {
                existing.SetPose(normalizedName, pose);
                poses.Remove(existing);
                poses.Insert(0, existing);
            }
            else
            {
                poses.Insert(0, new CameraAnchorPose(
                    Guid.NewGuid().ToString("N"),
                    normalizedName,
                    pose));
            }
            if (!SaveFile())
                return false;

            PosesChanged?.Invoke();
            return true;
        }

        public bool LoadPose(string uniqueId)
        {
            var savedPose = FindById(uniqueId);
            if (savedPose == null)
                return SetError("The selected camera pose no longer exists.");

            if (anchorSource == null ||
                !anchorSource.TrySetUniversePose(new UniverseMotionState(
                    savedPose.Position,
                    savedPose.Rotation)))
            {
                return SetError("The camera pose could not be loaded by the active free anchor.");
            }

            poses.Remove(savedPose);
            poses.Insert(0, savedPose);
            if (!SaveFile())
                return false;

            PosesChanged?.Invoke();
            return true;
        }

        public bool DeletePose(string uniqueId)
        {
            var savedPose = FindById(uniqueId);
            if (savedPose == null)
                return SetError("The selected camera pose no longer exists.");

            poses.Remove(savedPose);
            if (!SaveFile())
                return false;

            PosesChanged?.Invoke();
            return true;
        }

        private void LoadFile()
        {
            poses.Clear();
            lastPersistenceError = string.Empty;

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            try
            {
                var saved = JsonUtility.FromJson<SavedPoseFile>(
                    File.ReadAllText(filePath));
                if (saved?.poses == null)
                    return;

                var usedIds = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < saved.poses.Count; i++)
                {
                    var pose = saved.poses[i];
                    if (pose == null ||
                        string.IsNullOrWhiteSpace(pose.UniqueId) ||
                        string.IsNullOrWhiteSpace(pose.DisplayName) ||
                        !usedIds.Add(pose.UniqueId))
                    {
                        continue;
                    }

                    pose.Normalize();
                    poses.Add(pose);
                }

            }
            catch (Exception exception)
            {
                SetError(exception.Message);
            }
        }

        private bool SaveFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var saved = new SavedPoseFile();
                saved.poses.AddRange(poses);
                File.WriteAllText(filePath, JsonUtility.ToJson(saved, true));
                lastPersistenceError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                return SetError(exception.Message);
            }
        }

        private CameraAnchorPose FindById(string uniqueId)
        {
            return poses.Find(pose =>
                string.Equals(
                    pose.UniqueId,
                    uniqueId,
                    StringComparison.Ordinal));
        }

        private CameraAnchorPose FindByName(string displayName)
        {
            return poses.Find(pose =>
                string.Equals(
                    pose.DisplayName,
                    displayName,
                    StringComparison.OrdinalIgnoreCase));
        }

        private bool SetError(string error)
        {
            lastPersistenceError = error ?? string.Empty;
            Debug.LogWarning(lastPersistenceError, this);
            return false;
        }
    }
}
