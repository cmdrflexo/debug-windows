/*
 * Accepts project-owned window registrations even when the scene's debug Canvas is currently disabled.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace jcan.DebugWindows
{
    public static class DebugWindowRegistry
    {
        private static readonly Dictionary<string, DebugWindowRegistration> Registrations =
            new Dictionary<string, DebugWindowRegistration>(StringComparer.Ordinal);

        internal static event Action<DebugWindowRegistration> WindowRegistered;
        internal static event Action<string> WindowUnregistered;

        public static bool Register(DebugWindowRegistration registration)
        {
            if (registration == null)
                throw new ArgumentNullException(nameof(registration));

            if (Registrations.ContainsKey(registration.UniqueId))
            {
                Debug.LogError($"A debug window with ID '{registration.UniqueId}' is already registered.");
                return false;
            }

            Registrations.Add(registration.UniqueId, registration);
            WindowRegistered?.Invoke(registration);
            return true;
        }

        public static bool Unregister(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId) || !Registrations.Remove(uniqueId))
                return false;

            WindowUnregistered?.Invoke(uniqueId);
            return true;
        }

        internal static IEnumerable<DebugWindowRegistration> GetRegistrations()
        {
            return Registrations.Values;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            Registrations.Clear();
            WindowRegistered = null;
            WindowUnregistered = null;
        }
    }
}
