using UnityEngine;
using VeilWar.Core;
using VeilWar.Fog;

namespace VeilWar.Grid
{
    /// <summary>
    /// Pushes FogOfWarManager samples into CellView mist overlays.
    /// </summary>
    public sealed class GridFogPresenter : MonoBehaviour
    {
        [SerializeField] FogOfWarManager fog;
        [SerializeField] GridBoard board;

        void OnEnable()
        {
            if (fog == null) fog = FogOfWarManager.Instance;
            if (fog != null) fog.FogUpdated += Refresh;
        }

        void OnDisable()
        {
            if (fog != null) fog.FogUpdated -= Refresh;
        }

        void Refresh()
        {
            if (fog == null || board == null) return;
            for (var y = 0; y < fog.Size; y++)
            for (var x = 0; x < fog.Size; x++)
            {
                var coord = new GridCoord(x, y);
                if (!board.TryGetCell(coord, out var cell)) continue;

                var sample = fog.SampleVisibility(coord);
                if (sample >= 0.5f)
                    cell.SetVisibility(CellVisibility.ExploredEmpty);
                else if (sample >= 0.2f)
                    cell.SetVisibility(CellVisibility.ExploredEmpty); // explored band — mist off, dim tile later
                else
                    cell.SetVisibility(CellVisibility.Unknown);
            }
        }
    }
}
