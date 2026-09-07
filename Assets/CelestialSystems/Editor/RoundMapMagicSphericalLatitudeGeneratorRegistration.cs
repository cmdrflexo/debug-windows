/*
 * Registers the spherical-latitude source in MapMagic's node creation menu.
 */

using MapMagic.Nodes.GUI;
using UnityEditor;

namespace jcan.CelestialSystems.Editor
{
    public static class RoundMapMagicSphericalLatitudeGeneratorRegistration
    {
        [InitializeOnLoadMethod]
        private static void RegisterGenerator()
        {
            CreateRightClick.generatorTypes.Add(typeof(jcan.CelestialSystems.RoundMapMagicSphericalLatitudeGenerator));
        }
    }
}
