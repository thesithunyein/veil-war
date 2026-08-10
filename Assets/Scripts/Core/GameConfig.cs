using System;
using UnityEngine;

namespace VeilWar.Core
{
    /// <summary>
    /// Scope-locked product constants from PLAN.md §1.
    /// Do not expand grid size, turn count, or unit count without an explicit product decision.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Veil War/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Grid (scope freeze)")]
        [SerializeField, Range(5, 6)] private int gridSize = 5;
        [SerializeField] private float cellWorldSize = 1.1f;
        [SerializeField] private float cellGap = 0.08f;

        [Header("Match")]
        [SerializeField, Range(8, 12)] private int maxTurns = 10;
        [SerializeField, Range(2, 3)] private int unitsPerPlayer = 2;
        [SerializeField] private bool simultaneousTurns;

        [Header("Win")]
        [SerializeField] private WinCondition winCondition = WinCondition.DestroyEnemyHq;

        [Header("Presentation")]
        [SerializeField] private Color mistDeep = new(0.05f, 0.09f, 0.12f, 1f);
        [SerializeField] private Color mistTeal = new(0.25f, 0.55f, 0.55f, 0.55f);
        [SerializeField] private Color accentSignal = new(0.35f, 0.95f, 0.55f, 1f);

        public int GridSize => gridSize;
        public float CellWorldSize => cellWorldSize;
        public float CellGap => cellGap;
        public float CellStride => cellWorldSize + cellGap;
        public int MaxTurns => maxTurns;
        public int UnitsPerPlayer => unitsPerPlayer;
        public bool SimultaneousTurns => simultaneousTurns;
        public WinCondition WinCondition => winCondition;
        public Color MistDeep => mistDeep;
        public Color MistTeal => mistTeal;
        public Color AccentSignal => accentSignal;

        public Vector3 CellToWorld(GridCoord coord)
        {
            float origin = -(gridSize - 1) * CellStride * 0.5f;
            return new Vector3(
                origin + coord.X * CellStride,
                0f,
                origin + coord.Y * CellStride);
        }

        public bool InBounds(GridCoord coord) =>
            coord.X >= 0 && coord.Y >= 0 && coord.X < gridSize && coord.Y < gridSize;
    }

    public enum WinCondition : byte
    {
        DestroyEnemyHq = 0,
        ControlCenter = 1
    }

    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int X;
        public int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public override string ToString() => $"({X},{Y})";
        public static bool operator ==(GridCoord a, GridCoord b) => a.Equals(b);
        public static bool operator !=(GridCoord a, GridCoord b) => !a.Equals(b);
    }
}
