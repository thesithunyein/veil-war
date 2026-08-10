using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VeilWar.Core;
using VeilWar.Match;

namespace VeilWar.UI
{
    /// <summary>
    /// Match HUD: turn, phase, battle log. Keep chrome thin — grid stays the hero.
    /// </summary>
    public sealed class MatchHudView : MonoBehaviour
    {
        [SerializeField] MatchController match;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text turnText;
        [SerializeField] TMP_Text logText;
        [SerializeField] Button attackHintButton;
        [SerializeField] int maxLogLines = 6;

        readonly System.Collections.Generic.Queue<string> _log = new();

        void OnEnable()
        {
            if (match == null) return;
            match.StateChanged += OnState;
            match.LogEmitted += OnLog;
        }

        void OnDisable()
        {
            if (match == null) return;
            match.StateChanged -= OnState;
            match.LogEmitted -= OnLog;
        }

        void OnState(MatchSnapshot snap)
        {
            if (titleText != null)
                titleText.text = $"Veil War · {snap.MatchId}";
            if (turnText != null)
                turnText.text = snap.Phase == MatchPhase.Finished
                    ? (snap.Winner == PlayerId.Local ? "Victory" : "Defeat")
                    : $"Turn {snap.TurnIndex + 1}/{snap.MaxTurns}";
        }

        void OnLog(string line)
        {
            _log.Enqueue(line);
            while (_log.Count > maxLogLines) _log.Dequeue();
            if (logText != null) logText.text = string.Join("\n", _log);
        }
    }
}
