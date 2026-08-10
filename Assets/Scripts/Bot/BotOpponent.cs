using UnityEngine;
using VeilWar.Core;
using VeilWar.Match;

namespace VeilWar.Bot
{
    /// <summary>
    /// Required for judges who solo. Picks a random unexplored / hostile-looking cell.
    /// </summary>
    public sealed class BotOpponent : MonoBehaviour
    {
        [SerializeField] GameConfig config;

        public void TakeTurn(MatchController match)
        {
            if (match == null || config == null) return;
            if (match.Phase is MatchPhase.Finished) return;

            // Prefer attacking known local unit cells; else random in-bounds probe.
            foreach (var unit in match.Units)
            {
                if (unit.Alive && unit.Owner == PlayerId.Local)
                {
                    match.RegisterBotAttack(unit.Coord);
                    return;
                }
            }

            var x = Random.Range(0, config.GridSize);
            var y = Random.Range(0, config.GridSize);
            match.RegisterBotAttack(new GridCoord(x, y));
        }
    }
}
