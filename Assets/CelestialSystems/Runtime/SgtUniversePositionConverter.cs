/*
 * Converts between project-owned universe positions and Space Graphics Toolkit cell coordinates.
 */

using System;
using SpaceGraphicsToolkit;

namespace jcan.CelestialSystems
{
    internal static class SgtUniversePositionConverter
    {
        private static readonly long SgtCellsPerUniverseCell =
            (long)(UniversePosition.CellSizeMeters /
                SgtPosition.CELL_SIZE);

        public static UniversePosition ToUniversePosition(
            SgtPosition position)
        {
            ConvertSgtAxis(
                position.GlobalX,
                position.LocalX,
                out var cellX,
                out var localXMeters);
            ConvertSgtAxis(
                position.GlobalY,
                position.LocalY,
                out var cellY,
                out var localYMeters);
            ConvertSgtAxis(
                position.GlobalZ,
                position.LocalZ,
                out var cellZ,
                out var localZMeters);

            return new UniversePosition(
                cellX,
                cellY,
                cellZ,
                localXMeters,
                localYMeters,
                localZMeters);
        }

        public static bool TryToSgtPosition(
            UniversePosition position,
            double offsetXMeters,
            double offsetYMeters,
            double offsetZMeters,
            out SgtPosition converted)
        {
            if (!TryConvertUniverseAxis(
                    position.CellX,
                    position.LocalXMeters + offsetXMeters,
                    out var globalX,
                    out var localX) ||
                !TryConvertUniverseAxis(
                    position.CellY,
                    position.LocalYMeters + offsetYMeters,
                    out var globalY,
                    out var localY) ||
                !TryConvertUniverseAxis(
                    position.CellZ,
                    position.LocalZMeters + offsetZMeters,
                    out var globalZ,
                    out var localZ))
            {
                converted = default;
                return false;
            }

            converted = new SgtPosition
            {
                GlobalX = globalX,
                GlobalY = globalY,
                GlobalZ = globalZ,
                LocalX = localX,
                LocalY = localY,
                LocalZ = localZ
            };
            return true;
        }

        private static void ConvertSgtAxis(
            long sgtCell,
            double sgtLocalMeters,
            out long universeCell,
            out double universeLocalMeters)
        {
            universeCell =
                sgtCell / SgtCellsPerUniverseCell;
            var remainingSgtCells =
                sgtCell % SgtCellsPerUniverseCell;
            universeLocalMeters =
                remainingSgtCells * SgtPosition.CELL_SIZE +
                sgtLocalMeters;
        }

        private static bool TryConvertUniverseAxis(
            long universeCell,
            double universeLocalMeters,
            out long sgtCell,
            out double sgtLocalMeters)
        {
            var localCellShift =
                (long)(universeLocalMeters /
                    SgtPosition.CELL_SIZE);

            try
            {
                sgtCell = checked(
                    universeCell * SgtCellsPerUniverseCell +
                    localCellShift);
            }
            catch (OverflowException)
            {
                sgtCell = default;
                sgtLocalMeters = default;
                return false;
            }

            sgtLocalMeters =
                universeLocalMeters -
                localCellShift * SgtPosition.CELL_SIZE;
            return true;
        }
    }
}
