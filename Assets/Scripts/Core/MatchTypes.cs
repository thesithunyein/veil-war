using System;
using UnityEngine;

namespace VeilWar.Core
{
    public enum MatchPhase : byte
    {
        Lobby = 0,
        DeployCommit = 1,
        DeployReveal = 2,
        Playing = 3,
        ResolvingCombat = 4,
        Finished = 5
    }

    public enum PlayerId : byte
    {
        None = 0,
        Local = 1,
        Opponent = 2
    }

    public enum CellVisibility : byte
    {
        Unknown = 0,
        ExploredEmpty = 1,
        RevealedFriendly = 2,
        RevealedEnemy = 3,
        Hq = 4
    }

    public enum UnitKind : byte
    {
        Scout = 0,
        Striker = 1,
        Hq = 2
    }

    [Serializable]
    public struct UnitCommit
    {
        public string CommitHashHex;
        public bool Revealed;
        public GridCoord RevealedCoord;
        public UnitKind Kind;
        public PlayerId Owner;
        public bool Alive;
    }

    [Serializable]
    public sealed class MatchSnapshot
    {
        public string MatchId;
        public MatchPhase Phase;
        public int TurnIndex;
        public int MaxTurns;
        public PlayerId Winner;
        public UnitCommit[] Units = Array.Empty<UnitCommit>();
        public CellVisibility[] Visibility = Array.Empty<CellVisibility>();
        public bool TicketUnlocked;
    }
}
