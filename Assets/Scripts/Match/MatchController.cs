using System;
using System.Collections.Generic;
using UnityEngine;
using VeilWar.Bot;
using VeilWar.Core;
using VeilWar.Fog;
using VeilWar.Grid;
using VeilWar.Megapot;
using VeilWar.Units;

namespace VeilWar.Match
{
    /// <summary>
    /// Day-1 spine: visible grid duel vs bot. Day-2 swaps in commit-reveal fog.
    /// Win unlocks Megapot ticket credit via <see cref="MegapotRewardGate"/>.
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] GridBoard board;
        [SerializeField] BotOpponent bot;
        [SerializeField] MegapotRewardGate megapot;
        [SerializeField] bool startVsBotOnAwake = true;
        [SerializeField] bool enableFog = false; // Day 1 = false; Day 2 = true

        readonly List<UnitActor> _units = new();
        FogVisibilityMap _fog;
        MatchPhase _phase = MatchPhase.Lobby;
        int _turn;
        PlayerId _winner = PlayerId.None;
        string _matchId;

        public MatchPhase Phase => _phase;
        public int TurnIndex => _turn;
        public PlayerId Winner => _winner;
        public string MatchId => _matchId;
        public event Action<MatchSnapshot> StateChanged;
        public event Action<string> LogEmitted;

        void Start()
        {
            if (startVsBotOnAwake) BeginQuickDuelVsBot();
        }

        public void BeginQuickDuelVsBot()
        {
            if (config == null || board == null)
            {
                Debug.LogError("[VeilWar] MatchController missing references.");
                return;
            }

            _matchId = Guid.NewGuid().ToString("N")[..10];
            _turn = 0;
            _winner = PlayerId.None;
            _units.Clear();
            board.Build();
            _fog = new FogVisibilityMap(config.GridSize);

            // Day 1: place units visibly. Day 2: replace with commits.
            PlaceStarterUnits(PlayerId.Local, sideY: 0);
            PlaceStarterUnits(PlayerId.Opponent, sideY: config.GridSize - 1);

            if (enableFog) ApplyOwnerFog(PlayerId.Local);
            else RevealAllForDebug();

            _phase = MatchPhase.Playing;
            EmitLog($"Match {_matchId} — Quick Duel vs Bot");
            PushState();
        }

        public bool TryAttack(GridCoord target)
        {
            if (_phase != MatchPhase.Playing || _winner != PlayerId.None) return false;
            if (!config.InBounds(target)) return false;

            _phase = MatchPhase.ResolvingCombat;
            var hit = ResolveAttack(PlayerId.Local, target);
            if (board.TryGetCell(target, out var cell))
            {
                cell.SetVisibility(hit ? CellVisibility.RevealedEnemy : CellVisibility.ExploredEmpty);
                if (hit) cell.PlayHitShake();
                else if (enableFog) _fog.RevealEmpty(target);
            }

            EmitLog(hit
                ? $"Attack {target} — hit"
                : $"Attack {target} — miss / empty");

            if (!CheckWinner())
            {
                bot?.TakeTurn(this);
                CheckWinner();
            }

            _turn++;
            if (_winner == PlayerId.None && _turn >= config.MaxTurns)
            {
                // Stalemate: denser board / HQ proximity could decide later; for now draw = no ticket.
                _phase = MatchPhase.Finished;
                EmitLog("Max turns reached — draw");
            }
            else if (_winner == PlayerId.None)
            {
                _phase = MatchPhase.Playing;
            }

            PushState();
            return true;
        }

        public IReadOnlyList<UnitActor> Units => _units;

        public void RegisterBotAttack(GridCoord target)
        {
            var hit = ResolveAttack(PlayerId.Opponent, target);
            EmitLog(hit
                ? $"Bot attacks {target} — hit"
                : $"Bot attacks {target} — miss");
            if (board.TryGetCell(target, out var cell) && hit)
                cell.PlayHitShake();
        }

        bool ResolveAttack(PlayerId attacker, GridCoord target)
        {
            for (var i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                if (!u.Alive || u.Coord != target) continue;
                if (u.Owner == attacker) return false;
                u.Kill();
                if (enableFog) _fog.RevealUnit(target, friendly: false);
                return true;
            }

            if (enableFog) _fog.RevealEmpty(target);
            return false;
        }

        bool CheckWinner()
        {
            var localAlive = CountAlive(PlayerId.Local);
            var oppAlive = CountAlive(PlayerId.Opponent);
            if (oppAlive == 0)
            {
                Finish(PlayerId.Local);
                return true;
            }
            if (localAlive == 0)
            {
                Finish(PlayerId.Opponent);
                return true;
            }
            return false;
        }

        void Finish(PlayerId winner)
        {
            _winner = winner;
            _phase = MatchPhase.Finished;
            EmitLog(winner == PlayerId.Local ? "Victory — Megapot ticket unlocked" : "Defeat");
            if (winner == PlayerId.Local)
                megapot?.UnlockTicketCredit(_matchId, winner);
            PushState();
        }

        int CountAlive(PlayerId owner)
        {
            var n = 0;
            foreach (var u in _units)
                if (u.Alive && u.Owner == owner) n++;
            return n;
        }

        void PlaceStarterUnits(PlayerId owner, int sideY)
        {
            var count = config.UnitsPerPlayer;
            var mid = config.GridSize / 2;
            for (var i = 0; i < count; i++)
            {
                var x = Mathf.Clamp(mid - (count / 2) + i, 0, config.GridSize - 1);
                var coord = new GridCoord(x, sideY);
                var kind = i == 0 ? UnitKind.Hq : UnitKind.Striker;
                var actor = UnitActor.Spawn(coord, owner, kind, config, board.transform);
                _units.Add(actor);
                if (!enableFog)
                {
                    if (board.TryGetCell(coord, out var cell))
                        cell.SetVisibility(owner == PlayerId.Local
                            ? CellVisibility.RevealedFriendly
                            : CellVisibility.RevealedEnemy);
                }
            }
        }

        void RevealAllForDebug()
        {
            for (var y = 0; y < config.GridSize; y++)
            for (var x = 0; x < config.GridSize; x++)
            {
                var c = new GridCoord(x, y);
                if (board.TryGetCell(c, out var cell))
                    cell.SetVisibility(CellVisibility.ExploredEmpty);
            }

            foreach (var u in _units)
            {
                if (!board.TryGetCell(u.Coord, out var cell)) continue;
                cell.SetVisibility(u.Owner == PlayerId.Local
                    ? CellVisibility.RevealedFriendly
                    : CellVisibility.RevealedEnemy);
            }
        }

        void ApplyOwnerFog(PlayerId observer)
        {
            for (var y = 0; y < config.GridSize; y++)
            for (var x = 0; x < config.GridSize; x++)
                _fog.Set(new GridCoord(x, y), CellVisibility.Unknown);

            foreach (var u in _units)
            {
                if (!u.Alive) continue;
                if (u.Owner == observer)
                    _fog.RevealUnit(u.Coord, friendly: true);
            }

            board.ApplyVisibility(_fog);
        }

        void PushState()
        {
            StateChanged?.Invoke(BuildSnapshot());
        }

        MatchSnapshot BuildSnapshot()
        {
            var commits = new UnitCommit[_units.Count];
            for (var i = 0; i < _units.Count; i++)
            {
                var u = _units[i];
                commits[i] = new UnitCommit
                {
                    Revealed = true,
                    RevealedCoord = u.Coord,
                    Kind = u.Kind,
                    Owner = u.Owner,
                    Alive = u.Alive
                };
            }

            return new MatchSnapshot
            {
                MatchId = _matchId,
                Phase = _phase,
                TurnIndex = _turn,
                MaxTurns = config.MaxTurns,
                Winner = _winner,
                Units = commits,
                Visibility = _fog?.Cells ?? Array.Empty<CellVisibility>(),
                TicketUnlocked = _winner == PlayerId.Local
            };
        }

        void EmitLog(string line) => LogEmitted?.Invoke(line);
    }
}
