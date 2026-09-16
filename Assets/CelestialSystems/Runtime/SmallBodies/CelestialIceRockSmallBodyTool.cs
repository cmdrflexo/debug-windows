/*
 * Compatibility alias for scenes created before the tool was renamed to the
 * more general CelestialIceBodySmallBodyTool.
 */

using System;

namespace jcan.CelestialSystems
{
    [Obsolete(
        "Use CelestialIceBodySmallBodyTool instead.")]
    public sealed class CelestialIceRockSmallBodyTool :
        CelestialIceBodySmallBodyTool
    {
    }
}
