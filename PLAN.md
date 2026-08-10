# Megapot × Fog of War — 3-Day Build Plan (Hard Mode)

**Track:** Megapot Prize Track  
**Why this path:** Telegram signal — guessing/Wordle clones are crowded. Fog of War is rarer and more “wow” if it actually works.  
**Cost:** Base Sepolia Megapot ($0) + optional Inco Lightning for hidden state  
**Do not mix with:** pi River poker repo

**Honest verdict:** Possible in 3 days **only** as a **mini Fog of War**, not Dark Forest / full RTS.  
If scope creeps → you lose. Stick to §1.

---

## 0) Product lock

### Pitch
> Tiny fog-of-war duel: your units are hidden onchain. Explore, clash, winner earns a Megapot ticket.

### Why Megapot judges still accept this
Fog of War alone is an **Inco** story. For **Megapot 1st** you must make the jackpot the **reason the match matters**:

```
Match lobby → hidden deploy → explore/attack rounds → victory
  → unlock Megapot ticket credit → buy on Base Sepolia → tickets / claim
```

Megapot = end-of-match reward + home jackpot hero (not a footer link).

### Name ideas (new brand, not pi)
| Name | Domain |
|------|--------|
| **Veil War** | `veil.sithunyein.com` |
| **Fog Duel** | `fog.sithunyein.com` |
| **Shroud** | `shroud.sithunyein.com` |

---

## 1) Scope freeze — what “Fog of War” means in 3 days

### YOU ARE BUILDING: **1v1 Grid Duel (5×5 or 6×6)**

| Feature | In | Out |
|---------|----|-----|
| Grid map | ✅ 5×5 | ❌ Big maps, fog canvases, hex RTS |
| Hidden unit positions | ✅ via Inco ciphertext / commit-reveal | ❌ Full fog lighting engine |
| 2 players (or 1 vs bot) | ✅ | ❌ 4+ players, MMO |
| Turns | ✅ simultaneous or alternate, max **8–12 turns** | ❌ Real-time action |
| Win condition | ✅ destroy enemy HQ / flag OR control center | ❌ Economy, tech trees |
| Megapot ticket on win | ✅ | ❌ Mid-match ticket spam |
| Shop / cosmetics / chat | ❌ | — |
| Poker reuse | ❌ | — |

### Minimum Fog mechanic (pick ONE — recommend A)

**A — Commit-reveal positions (fastest, still “fog”)**  
- Each player commits `hash(x,y,salt)` for 2–3 units at deploy  
- Opponent only sees “unit exists in fog,” not cell  
- On move/attack: reveal that unit’s cell with proof  
- Works on Sepolia without perfect Inco if needed; upgrade to Inco encrypted coords if time  

**B — Inco encrypted coordinates (best story, more risk)**  
- Store encrypted (x,y) with Lightning  
- Attested decrypt only for owner / on combat resolve  
- Use only after A works end-to-end  

**Day-1 rule:** Ship **A** first. Add **B** only if buy-ticket + match already work.

### Bot mode (required for judges)
Judges often solo. Include **Quick Duel vs Bot** so one person can finish a match and earn a ticket without a friend.

---

## 2) Megapot integration (non-negotiable for this track)

### Main loop
1. Home: live Megapot pool + countdown (hero)  
2. Play Fog Duel (vs bot / friend)  
3. Win → “Ticket unlocked”  
4. Buy random ticket (Sepolia `JackpotRandomTicketBuyer`) + your referrer  
5. Tickets list + Claim  

### Qualification checklist
- [ ] Public live URL  
- [ ] Real Megapot contract calls on Base (Sepolia OK)  
- [ ] Megapot in main loop (win → ticket)  
- [ ] Public GitHub + README integration write-up  

### $0 path
- Chain: Base Sepolia  
- USDC test: `0x036CbD53842c5426634e7929541eC2318f3dCF7e`  
- Addresses: [llms.megapot.io/contracts/reference](https://llms.megapot.io/contracts/reference)  
- Faucets: Base Sepolia ETH + USDC (CDP / Alchemy)

---

## 3) HD premium UX (pi-quality, new visual identity)

Same product bar as pi River: mobile-first, atmospheric, expressive type — **military/fog** mood, not poker felt.

### Visual direction
- Deep ink + cold mist gradients (teal/slate mist, not purple)  
- Grid as the hero visual (edge-to-edge playfield)  
- Fog = CSS blur / noise overlay on unknown cells — simple, readable  
- Display font bold; mono for coords  
- One accent (signal green or flare gold)  

### Screens (only these)
```
/            Welcome + Enter
/home        Jackpot hero + Quick Duel + Challenge
/match/[id]  Fog grid + actions + log
/result      Win/lose → Buy ticket
/tickets     Megapot tickets + Claim
```

Bottom nav: **Home · Play · Tickets**

### Motion (2–3)
- Cell reveal flash  
- Attack hit shake  
- Ticket unlock burst on result  

---

## 4) Tech stack

| Layer | Choice |
|-------|--------|
| App | Next.js 15 + TS + Tailwind |
| Wallet | wagmi + viem, Base Sepolia |
| Match state | Solidity minimal `FogDuel` **or** server+commit for day 1 speed |
| Hidden info | Commit-reveal first; Inco second |
| Megapot | Sepolia Jackpot + RandomTicketBuyer |
| Host | Vercel + new subdomain |
| Repo | **new** GitHub repo |

### Suggested contracts (keep tiny)
`FogDuel.sol`:
- `createMatch(buyIn optional 0)`  
- `join(matchId)`  
- `commitDeploy(hash)`  
- `revealMove` / `attack(cell)`  
- `finalize` → winner address  
Frontend listens for winner → grants ticket credit → Megapot buy  

If Solidity threatens the deadline: **API-orchestrated match** + onchain only Megapot buy still qualifies Megapot track — but mention honestly in README. Prefer at least deploy commits onchain for credibility.

---

## 5) 3-day calendar

### Day 1 — Spine (do not polish art yet)
**Morning**
- New folder/repo/domain  
- Next shell + theme + Home jackpot UI (RPC read)  
- Wallet connect  

**Afternoon**
- Megapot approve + buy 1 random ticket + tickets page  
- Claim empty state  

**Evening**
- 5×5 grid UI (all visible first)  
- Vs-bot moves working with **no fog**  

**Exit Day 1:** Buy real Sepolia ticket from UI + finish a visible bot duel.

### Day 2 — Fog + win → ticket
- Commit-reveal deploy (2 units each)  
- Fog rendering (unknown cells obscured)  
- Attack / reveal rules  
- Win → ticket credit → Buy CTA  
- Friend join link (optional if bot solid)  

**Exit Day 2:** Full loop: fog duel → win → Megapot ticket.

### Day 3 — HD + demo + submit
- Polish fog visuals, mobile, errors, wrong network  
- Bot personality / short battle log  
- README + 90s demo video  
- Typeform **Megapot track**  
- Freeze tag `v1-jam`  

**Buffer:** 4–6h for faucet / contract / deploy fires.

---

## 6) Demo script (≤90s)

1. Home jackpot (Megapot)  
2. Quick Duel — deploy in fog  
3. Attack — cell reveals — win  
4. Buy Megapot ticket — show id  
5. Live URL  

Say once: “Hidden positions onchain; winners earn real Megapot tickets.”

---

## 7) Risks (read before you commit)

| Risk | Reality check |
|------|----------------|
| Full Dark Forest | **Impossible** in 3 days — don’t try |
| Inco fog from hour 0 | Can burn Day 1–2; use commit-reveal first |
| Guessing games crowded | Valid reason to differentiate — Fog is rarer |
| Megapot depth weak | If ticket is only a badge, you lose 30% — keep Home jackpot + win→buy mandatory |
| Burnout vs pi | New folder only; don’t open pi River |

---

## 8) Success bar

**Ship:** Live fog duel (vs bot) + Sepolia Megapot buy/claim + public repo.  
**Competitive for 1st:** Feels like a real micro-war game, fog is believable, ticket flow is obvious, UX HD, demo crisp.

---

## 9) Day-0 checklist

- [ ] New Cursor folder e.g. `C:\Users\sithu\Projects\veil-war`  
- [ ] Copy this file as `PLAN.md`  
- [ ] New GitHub repo + Vercel project + subdomain  
- [ ] Sepolia ETH + USDC in demo wallet  
- [ ] Referrer address ready  
- [ ] Kickoff: Phase Day 1 spine only  

### Kickoff prompt for new Cursor chat
```
Read PLAN.md. Build Veil War (or chosen name): 5×5 fog-of-war duel for Megapot track.
Day 1 only: branded shell, Base Sepolia Megapot buy/claim, visible grid vs bot.
Commit-reveal fog comes Day 2. No poker, no pi River code, no Wordle.
```

---

## 10) Final call

| Path | Crowd | 3-day risk | Megapot fit | Differentiation |
|------|-------|------------|-------------|-----------------|
| Guessing | High | Low | Excellent | Low |
| **Fog Duel (this plan)** | Lower | **High** | Good if win→ticket | **High** |

**Yes, try Fog of War** — but only as **5×5 duel + bot + Megapot ticket on win**.  
That is the hard path that can still finish in 3 days.

*Plan version: 2026-08-10 — Fog of War × Megapot, 3-day hard mode.*
