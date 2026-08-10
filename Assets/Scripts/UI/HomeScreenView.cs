using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VeilWar.Match;

namespace VeilWar.UI
{
    /// <summary>
    /// Home-equivalent in Unity: jackpot hero placeholder + Quick Duel CTA.
    /// Live pool USD should be fed from RPC/web bridge; until then show staged copy.
    /// </summary>
    public sealed class HomeScreenView : MonoBehaviour
    {
        [SerializeField] MatchController match;
        [SerializeField] GameObject homeRoot;
        [SerializeField] GameObject matchRoot;
        [SerializeField] TMP_Text jackpotText;
        [SerializeField] TMP_Text countdownText;
        [SerializeField] Button quickDuelButton;
        [SerializeField] string stagedJackpot = "$128,400";

        void OnEnable()
        {
            if (jackpotText != null) jackpotText.text = stagedJackpot;
            if (countdownText != null) countdownText.text = "Drawing · live on Base Sepolia";
            if (quickDuelButton != null) quickDuelButton.onClick.AddListener(StartDuel);
            ShowHome(true);
        }

        void OnDisable()
        {
            if (quickDuelButton != null) quickDuelButton.onClick.RemoveListener(StartDuel);
        }

        public void SetJackpotLabel(string usd, string countdown)
        {
            if (jackpotText != null) jackpotText.text = usd;
            if (countdownText != null) countdownText.text = countdown;
        }

        void StartDuel()
        {
            ShowHome(false);
            match?.BeginQuickDuelVsBot();
        }

        void ShowHome(bool home)
        {
            if (homeRoot != null) homeRoot.SetActive(home);
            if (matchRoot != null) matchRoot.SetActive(!home);
        }
    }
}
