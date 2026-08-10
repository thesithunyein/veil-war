using UnityEngine;
using VeilWar.Core;

namespace VeilWar.Grid
{
    /// <summary>
    /// Spawns a 5×5 (or 6×6) board of cell views. Edge-to-edge playfield is the hero visual.
    /// </summary>
    public sealed class GridBoard : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] CellView cellPrefab;
        [SerializeField] Transform cellRoot;

        CellView[,] _cells;

        public GameConfig Config => config;
        public int Size => config != null ? config.GridSize : 0;

        public void Build()
        {
            if (config == null || cellPrefab == null)
            {
                Debug.LogError("[VeilWar] GridBoard missing config or cell prefab.");
                return;
            }

            Clear();
            var size = config.GridSize;
            _cells = new CellView[size, size];
            var root = cellRoot != null ? cellRoot : transform;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var coord = new GridCoord(x, y);
                var view = Instantiate(cellPrefab, root);
                view.name = $"Cell_{x}_{y}";
                view.Initialize(coord, config.CellToWorld(coord), config);
                _cells[x, y] = view;
            }
        }

        public bool TryGetCell(GridCoord coord, out CellView view)
        {
            view = null;
            if (_cells == null || config == null || !config.InBounds(coord)) return false;
            view = _cells[coord.X, coord.Y];
            return view != null;
        }

        public void ApplyVisibility(Fog.FogVisibilityMap map)
        {
            if (_cells == null || map == null) return;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var coord = new GridCoord(x, y);
                if (map.TryGet(coord, out var vis))
                    _cells[x, y].SetVisibility(vis);
            }
        }

        void Clear()
        {
            if (_cells == null) return;
            foreach (var cell in _cells)
            {
                if (cell != null) Destroy(cell.gameObject);
            }
            _cells = null;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild Grid")]
        void RebuildInEditor() => Build();
#endif
    }
}
