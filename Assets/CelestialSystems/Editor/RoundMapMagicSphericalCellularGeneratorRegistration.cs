/*
 * Registers the external spherical-cellular generator with MapMagic's editor-only node creation menu.
 */

using MapMagic.Nodes.GUI;
using UnityEditor;

namespace jcan.CelestialSystems.Editor
{
    public static class RoundMapMagicSphericalCellularGeneratorRegistration
    {
        [InitializeOnLoadMethod]
        private static void RegisterGenerator()
        {
            CreateRightClick.generatorTypes.Add(
                typeof(jcan.CelestialSystems.RoundMapMagicSphericalCellularGenerator));
        }
    }
}
