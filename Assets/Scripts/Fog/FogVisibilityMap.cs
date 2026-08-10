using System;
using UnityEngine;
using VeilWar.Core;

namespace VeilWar.Fog
{
    /// <summary>
    /// Local fog model for the observing player. Unknown cells stay obscured until reveal/attack.
    /// This is gameplay fog — not a full volumetric lighting engine (explicitly out of scope).
    /// </summary>
    public sealed class FogVisibilityMap
    {
        readonly int _size;
        readonly CellVisibility[] _cells;

        public int Size => _size;
        public CellVisibility[] Cells => _cells;

        public FogVisibilityMap(int size)
        {
            if (size < 5) throw new ArgumentOutOfRangeException(nameof(size));
            _size = size;
            _cells = new CellVisibility[size * size];
            Array.Fill(_cells, CellVisibility.Unknown);
        }

        public int IndexOf(GridCoord coord) => coord.Y * _size + coord.X;

        public bool TryGet(GridCoord coord, out CellVisibility visibility)
        {
            if (coord.X < 0 || coord.Y < 0 || coord.X >= _size || coord.Y >= _size)
            {
                visibility = CellVisibility.Unknown;
                return false;
            }

            visibility = _cells[IndexOf(coord)];
            return true;
        }

        public void Set(GridCoord coord, CellVisibility visibility)
        {
            if (coord.X < 0 || coord.Y < 0 || coord.X >= _size || coord.Y >= _size) return;
            _cells[IndexOf(coord)] = visibility;
        }

        public void RevealEmpty(GridCoord coord) => Set(coord, CellVisibility.ExploredEmpty);

        public void RevealUnit(GridCoord coord, bool friendly) =>
            Set(coord, friendly ? CellVisibility.RevealedFriendly : CellVisibility.RevealedEnemy);

        public void MarkHq(GridCoord coord) => Set(coord, CellVisibility.Hq);

        public bool IsUnknown(GridCoord coord) =>
            TryGet(coord, out var v) && v == CellVisibility.Unknown;
    }
}
