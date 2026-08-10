using UnityEngine;
using VeilWar.Bot;
using VeilWar.CameraRig;
using VeilWar.Fog;
using VeilWar.Grid;
using VeilWar.Input;
using VeilWar.Match;
using VeilWar.Megapot;
using VeilWar.Network;

namespace VeilWar.Presentation
{
    /// <summary>
    /// Composition root. Wire FoW stack: FogOfWarManager ← IncoNetworkBridge ← sandbox/live.
    /// </summary>
    public sealed class VeilWarBootstrap : MonoBehaviour
    {
        [SerializeField] Core.GameConfig config;
        [SerializeField] GridBoard board;
        [SerializeField] MatchController match;
        [SerializeField] BotOpponent bot;
        [SerializeField] CellSelector selector;
        [SerializeField] BoardCameraController boardCamera;
        [SerializeField] MegapotRewardGate megapot;

        [Header("Fog of War pipeline")]
        [SerializeField] FogOfWarManager fogOfWar;
        [SerializeField] IncoNetworkBridge incoBridge;
        [SerializeField] FoWSandboxTester sandboxTester;
        [SerializeField] GridFogPresenter gridFogPresenter;
        [SerializeField] FogWorldQuadController fogQuad;
        [SerializeField] UI.Strategy.FogOfWarMinimap minimap;
        [SerializeField] bool enableSandboxHotkeys = true;
        [SerializeField] bool autoBuildOnPlay = true;

        void Awake()
        {
            Validate();
            if (autoBuildOnPlay && board != null) board.Build();
            if (boardCamera != null && board != null) boardCamera.Focus(board.transform);
            if (sandboxTester != null) sandboxTester.enabled = enableSandboxHotkeys;
        }

        void Validate()
        {
            if (config == null) Debug.LogError("[VeilWar] Assign GameConfig ScriptableObject.");
            if (board == null) Debug.LogError("[VeilWar] Assign GridBoard.");
            if (match == null) Debug.LogWarning("[VeilWar] MatchController missing (optional if FoW sandbox-only).");
            if (fogOfWar == null) Debug.LogError("[VeilWar] FogOfWarManager required for reveal pipeline.");
            if (incoBridge == null) Debug.LogError("[VeilWar] IncoNetworkBridge required for decrypt → FoW events.");
            if (fogQuad == null) Debug.LogWarning("[VeilWar] FogWorldQuadController missing — no atmospheric fog quad.");
            if (minimap == null) Debug.LogWarning("[VeilWar] FogOfWarMinimap missing — no strategy minimap.");
            if (bot == null) Debug.LogWarning("[VeilWar] BotOpponent missing — judges need Quick Duel vs Bot.");
            if (megapot == null) Debug.LogWarning("[VeilWar] MegapotRewardGate missing — win→ticket loop incomplete.");
            if (selector == null) Debug.LogWarning("[VeilWar] CellSelector missing — no attack input.");
        }
    }
}
