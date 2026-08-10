using System;
using UnityEngine;

namespace VeilWar.Megapot
{
    /// <summary>
    /// Win → ticket credit. Actual USDC approve + JackpotRandomTicketBuyer.buyTickets
    /// happens in the companion web shell (Base Sepolia) or a future WalletConnect Unity SDK.
    /// This gate is the Unity-side product hook so Megapot stays in the main loop.
    /// </summary>
    public sealed class MegapotRewardGate : MonoBehaviour
    {
        [SerializeField] string webBuyUrl = "https://veil.sithunyein.com/result";
        [SerializeField] bool openWebOnUnlock = true;

        public string LastMatchId { get; private set; }
        public bool HasCredit { get; private set; }
        public event Action<string> TicketCreditUnlocked;

        public void UnlockTicketCredit(string matchId, Core.PlayerId winner)
        {
            if (winner != Core.PlayerId.Local) return;
            LastMatchId = matchId;
            HasCredit = true;
            PlayerPrefs.SetString("veil_ticket_credit", matchId);
            PlayerPrefs.SetInt("veil_ticket_ready", 1);
            PlayerPrefs.Save();
            TicketCreditUnlocked?.Invoke(matchId);
            Debug.Log($"[VeilWar] Megapot ticket credit unlocked for match {matchId}");

            if (openWebOnUnlock && !string.IsNullOrWhiteSpace(webBuyUrl))
            {
                var url = $"{webBuyUrl}?match={Uri.EscapeDataString(matchId)}&outcome=win";
                Application.OpenURL(url);
            }
        }

        public bool ConsumeCredit()
        {
            if (!HasCredit && PlayerPrefs.GetInt("veil_ticket_ready", 0) != 1) return false;
            HasCredit = false;
            PlayerPrefs.DeleteKey("veil_ticket_ready");
            return true;
        }
    }
}
