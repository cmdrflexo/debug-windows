/*
 * Registers the spherical contour-fiber node in the MapMagic creation menu.
 */

using MapMagic.Nodes.GUI;
using UnityEditor;

namespace jcan.CelestialSystems.Editor
{
    public static class RoundMapMagicSphericalContourFibersGeneratorRegistration
    {
        [InitializeOnLoadMethod]
        private static void RegisterGenerator()
        {
            CreateRightClick.generatorTypes.Add(
                typeof(jcan.CelestialSystems.RoundMapMagicSphericalContourFibersGenerator));
        }
    }
}
