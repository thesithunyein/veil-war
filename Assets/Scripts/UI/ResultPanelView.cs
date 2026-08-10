using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VeilWar.Match;
using VeilWar.Megapot;

namespace VeilWar.UI
{
    /// <summary>
    /// Result surface: win → Buy Megapot ticket CTA (opens web Sepolia buy flow).
    /// </summary>
    public sealed class ResultPanelView : MonoBehaviour
    {
        [SerializeField] MatchController match;
        [SerializeField] MegapotRewardGate megapot;
        [SerializeField] GameObject root;
        [SerializeField] TMP_Text headline;
        [SerializeField] TMP_Text body;
        [SerializeField] Button buyTicketButton;
        [SerializeField] Button rematchButton;

        void OnEnable()
        {
            if (root != null) root.SetActive(false);
            if (match != null) match.StateChanged += OnState;
            if (buyTicketButton != null) buyTicketButton.onClick.AddListener(OnBuy);
            if (rematchButton != null) rematchButton.onClick.AddListener(OnRematch);
        }

        void OnDisable()
        {
            if (match != null) match.StateChanged -= OnState;
            if (buyTicketButton != null) buyTicketButton.onClick.RemoveListener(OnBuy);
            if (rematchButton != null) rematchButton.onClick.RemoveListener(OnRematch);
        }

        void OnState(Core.MatchSnapshot snap)
        {
            if (snap.Phase != Core.MatchPhase.Finished)
            {
                if (root != null) root.SetActive(false);
                return;
            }

            if (root != null) root.SetActive(true);
            var win = snap.Winner == Core.PlayerId.Local;
            if (headline != null) headline.text = win ? "Ticket unlocked" : "Shroud holds";
            if (body != null)
                body.text = win
                    ? "You earned 1 Megapot ticket credit on Base Sepolia."
                    : "Come back for another Quick Duel. Jackpot still waits.";
            if (buyTicketButton != null) buyTicketButton.gameObject.SetActive(win);
        }

        void OnBuy()
        {
            megapot?.UnlockTicketCredit(match != null ? match.MatchId : "rematch", Core.PlayerId.Local);
        }

        void OnRematch()
        {
            match?.BeginQuickDuelVsBot();
        }
    }
}
