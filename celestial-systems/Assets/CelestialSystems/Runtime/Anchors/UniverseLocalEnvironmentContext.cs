/*
 * Shared local environmental reference for effects and systems attached to the
 * player's universe anchor. Consumers must use Try* methods because runtime
 * bodies and presentations can be destroyed during regeneration.
 */

using System;
using UnityEngine;

namespace jcan.CelestialSystems
{
    [DisallowMultipleComponent]
    public sealed class UniverseLocalEnvironmentContext : MonoBehaviour
    {
        public enum EnvironmentType
        {
            None,
            Body,
            Ring
        }

        public enum EnvironmentSource
        {
            None,
            FreeFlightLock,
            Explicit
        }

        [Header("Runtime")]
        [SerializeField] private EnvironmentType environmentType;
        [SerializeField] private EnvironmentSource environmentSource;
        [SerializeField] private CelestialBodyRuntimeContext referenceBody;
        [SerializeField] private CelestialRingMeshPresentation referenceRing;
        [SerializeField] private string displayName;

        public event Action<UniverseLocalEnvironmentContext> EnvironmentChanged;

        public EnvironmentType Type
        {
            get
            {
                ValidateEnvironment();
                return environmentType;
            }
        }

        public EnvironmentSource Source
        {
            get
            {
                ValidateEnvironment();
                return environmentSource;
            }
        }

        public CelestialBodyRuntimeContext ReferenceBody
        {
            get
            {
                ValidateEnvironment();
                return referenceBody;
            }
        }

        public CelestialRingMeshPresentation ReferenceRing
        {
            get
            {
                ValidateEnvironment();
                return referenceRing;
            }
        }

        public string DisplayName
        {
            get
            {
                ValidateEnvironment();
                return displayName;
            }
        }

        public bool HasValidEnvironment => ValidateEnvironment();

        public bool SetBody(
            CelestialBodyRuntimeContext body,
            EnvironmentSource source = EnvironmentSource.Explicit)
        {
            if (body == null)
            {
                Clear();
                return false;
            }

            var changed = environmentType != EnvironmentType.Body ||
                environmentSource != source || referenceBody != body ||
                referenceRing != null;
            environmentType = EnvironmentType.Body;
            environmentSource = source;
            referenceBody = body;
            referenceRing = null;
            displayName = body.name;

            if (changed)
            {
                EnvironmentChanged?.Invoke(this);
            }

            return true;
        }

        // A ring currently inherits the supplied owner's motion. If there is
        // no owner, it is not a valid moving reference frame yet.
        public bool SetRing(
            CelestialRingMeshPresentation ring,
            CelestialBodyRuntimeContext owner,
            EnvironmentSource source = EnvironmentSource.Explicit)
        {
            if (ring == null || owner == null)
            {
                Clear();
                return false;
            }

            var changed = environmentType != EnvironmentType.Ring ||
                environmentSource != source || referenceBody != owner ||
                referenceRing != ring;
            environmentType = EnvironmentType.Ring;
            environmentSource = source;
            referenceBody = owner;
            referenceRing = ring;
            displayName = ring.name;

            if (changed)
            {
                EnvironmentChanged?.Invoke(this);
            }

            return true;
        }

        public void Clear()
        {
            if (environmentType == EnvironmentType.None &&
                environmentSource == EnvironmentSource.None &&
                referenceBody == null && referenceRing == null)
            {
                return;
            }

            environmentType = EnvironmentType.None;
            environmentSource = EnvironmentSource.None;
            referenceBody = null;
            referenceRing = null;
            displayName = string.Empty;
            EnvironmentChanged?.Invoke(this);
        }

        public void ClearIfSource(EnvironmentSource source)
        {
            if (environmentSource == source)
            {
                Clear();
            }
        }

        public bool TryGetReferenceMotion(out UniverseMotionState motion)
        {
            if (!ValidateEnvironment() ||
                referenceBody == null ||
                !referenceBody.TryGetMotionState(out motion))
            {
                motion = default;
                return false;
            }

            return true;
        }

        public bool TryGetReferencePosition(out UniversePosition position)
        {
            if (TryGetReferenceMotion(out var motion))
            {
                position = motion.Position;
                return true;
            }

            position = default;
            return false;
        }

        private void Update()
        {
            ValidateEnvironment();
        }

        private bool ValidateEnvironment()
        {
            var valid = environmentType != EnvironmentType.None &&
                referenceBody != null;

            if (valid && environmentType == EnvironmentType.Ring)
            {
                valid = referenceRing != null;
            }

            if (valid)
            {
                return true;
            }

            if (environmentType != EnvironmentType.None ||
                environmentSource != EnvironmentSource.None ||
                referenceBody != null || referenceRing != null)
            {
                Clear();
            }

            return false;
        }
    }
}
