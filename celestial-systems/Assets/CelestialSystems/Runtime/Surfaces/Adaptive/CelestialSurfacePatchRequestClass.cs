/*
 * Classifies adaptive surface requests so shared generation capacity favors coverage and visible work over background preparation.
 */

namespace jcan.CelestialSystems
{
    public enum CelestialSurfacePatchRequestClass
    {
        Background = 0,
        Prefetch = 1,
        Visible = 2,
        Coverage = 3,
        Collision = 4
    }
}
