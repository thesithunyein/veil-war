# Veil War — Unity client (Fog Duel × Megapot)

3D presentation layer for the **5×5 fog-of-war duel** in `PLAN.md`.  
Scope freeze still applies: not Dark Forest, not RTS, not poker.

## Fog of War runtime (network → texture → 3D agents)

```
IncoNetworkBridge.PublishDecrypted(packet)
  → event DecryptedPacketReceived
  → FogOfWarManager.ApplyDecryptedVision(coord, radius)
  → FogTexture (R8) + FogUpdated event
  → FogOfWarAgent toggles MeshRenderer / Canvas / desaturate
```

| Script | Role |
|--------|------|
| `Fog/FogOfWarManager.cs` | Vision texture + cell samples + events |
| `Network/IncoNetworkBridge.cs` | Decrypt packet bus → FoW |
| `Fog/FogOfWarAgent.cs` | Enemy hide / shroud desaturate |
| `Fog/FoWSandboxTester.cs` | Editor hotkeys mocking Inco packets |
| `Grid/GridFogPresenter.cs` | Optional board mist sync |

### Sandbox hotkeys
- **WASD** — move friendly sensor  
- **1** — re-push vision circle  
- **C** — clear fog  
- **E / Q** — teleport selected enemy into / out of vision  
- **Tab** — cycle enemy  
- **R** — random enemy hop  

### Scene wire-up
1. Add `FogOfWarManager` + assign `GameConfig`  
2. Add `IncoNetworkBridge` (auto-finds FoW)  
3. Add `FoWSandboxTester` for Play Mode tests  
4. Put `FogOfWarAgent` on enemy prefabs (or rely on `UnitActor.Spawn`)  

## Architecture (UI/UX)

```
HomeScreenView (jackpot hero + Quick Duel)
  → MatchController + GridBoard (edge-to-edge 5×5)
      → FogVisibilityMap + CellView (mist overlay)
      → UnitActor (2–3 units / side)
      → BotOpponent (required for judges)
      → CellSelector (tap cell = attack)
  → ResultPanelView (win → Megapot ticket CTA)
  → MegapotRewardGate → opens web Sepolia buy URL
```

**Day 1:** `MatchController.enableFog = false` (visible duel + Megapot unlock).  
**Day 2:** enable fog + `CommitReveal` deploy hashes.

## Scripts added

| Path | Role |
|------|------|
| `Core/GameConfig.cs` | ScriptableObject — grid 5–6, turns 8–12, colors |
| `Core/MatchTypes.cs` | Phases, visibility, commits, snapshot |
| `Fog/CommitReveal.cs` | SHA256 commit-reveal (mechanic A) |
| `Fog/FogVisibilityMap.cs` | Per-cell fog state |
| `Grid/GridBoard.cs` / `CellView.cs` | 3D board + reveal flash / hit shake |
| `Match/MatchController.cs` | Duel loop, win → ticket |
| `Units/UnitActor.cs` | Capsule placeholder units |
| `Bot/BotOpponent.cs` | Solo judge path |
| `Input/CellSelector.cs` | Click/touch attack |
| `Camera/BoardCameraController.cs` | Elevated orbit |
| `Megapot/MegapotRewardGate.cs` | Win credit → web buy |
| `UI/*` | Home / Match HUD / Result |
| `Presentation/VeilWarBootstrap.cs` | Scene composition root |

## Unity setup (15 min)

1. Create Unity 6 / 2022 LTS 3D (URP) project, copy `Assets/Scripts` in.
2. Add TextMeshPro (import essentials).
3. Create `GameConfig` asset: **Create → Veil War → Game Config**.
4. Create a Cell prefab: cube + child fog quad + BoxCollider + `CellView`.
5. Scene objects: `GridBoard`, `MatchController`, `BotOpponent`, `CellSelector`, `BoardCameraController`, `MegapotRewardGate`, UI canvas, `VeilWarBootstrap`.
6. Wire references; press Play → Quick Duel.

## Megapot

Onchain buy stays on Base Sepolia via the web companion (`veil.sithunyein.com` / Next shell).  
Unity unlocks credit and deep-links — keeps Megapot in the **main loop** without shipping wallet SDK on Day 1.

## Not in this pass

- Inco Lightning encrypted coords (Day 2+ only after commit-reveal works)
- Friend join / Solidity `FogDuel`
- Full volumetric fog lighting
