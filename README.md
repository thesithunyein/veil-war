# Veil War — WWII Aerial Combat × Megapot

**Live:** https://veil.sithunyein.com/  
**Track:** Megapot Prize Track · Summer Game Jam 2026  
**Chain:** Base Sepolia

Fly a 3D WWII dogfight in the browser. Earn **Megapot credits** from combat, then **claim real Megapot tickets on-chain** via the house wallet on Base Sepolia.

---

## Judge path (90 seconds)

1. Open **https://veil.sithunyein.com/** — tap **<< ENGAGE >>** (no sign-in required to fly)
2. WASD to fly · SPACE to fire · destroy scouts → earn **Base Chips** → convert to **Megapot credits**
3. Finish mission → **CLAIM MEGAPOT TICKET** (sign in with Google first to sync & mint)
4. House calls Megapot `buyTickets` on Base Sepolia → **BaseScan** link on screen

**Demo script:** see [`DEMO.md`](DEMO.md)

---

## Megapot core loop

| Step | What happens |
|------|----------------|
| **Play** | Instant guest mode or Google sign-in — dogfight earns chips & credits |
| **Earn** | 3 Base Chips → 1 Megapot credit · boss kill & mission clear bonuses |
| **Sync** | Sign in with Google to persist credits to Supabase |
| **Claim** | End screen → house mints 1 Megapot ticket via `buyTickets(recipient)` |
| **Proof** | Live jackpot panel shows drawing #, pool USDC, countdown, global tickets, your odds |

**House wallet (funds on-chain claims):** `0xa399Ad139F2393bdFc88CfdafDfd3d5dEDA004D5`  
Needs Base Sepolia **ETH** (gas) + **USDC** (`0x036CbD53842c5426634e7929541eC2318f3dCF7e`).

**Megapot contracts (Base Sepolia):** jackpot `0x465dA3c859f193A3807386387bEE941B2A4c3279`

---

## Tech stack

- **Game:** `web-sandbox/index.html` — Three.js aerial combat, theaters, shop, HUD
- **Auth:** Supabase Google OAuth + silent in-browser play wallet
- **Backend:** Next.js API routes on Vercel
  - `GET/POST /api/megapot/claim` — pool read + on-chain ticket mint
  - `POST /api/progress` — cloud save
  - `POST /api/shop/purchase` — Base Sepolia ETH unlocks
- **Deploy:** push `master` → Vercel (`veil.sithunyein.com`) + GitHub Pages (`gh-pages`)

```bash
npm install
npm run dev          # syncs web-sandbox → public, serves on :3000
npm run build        # production build
```

---

## Features (live product)

- Instant **guest play** — fly without OAuth; credits local until sign-in
- **Daily bonus** — first mission of the day grants +1 Base Chip
- **Live Megapot panel** — jackpot pool, drawing countdown, global tickets, odds, recent claims ticker
- Multiple **theaters** (Arctic, Pacific, Jungle, Mountains, Village, City)
- **Combat XP shop** + optional MetaMask ETH unlocks on Base Sepolia
- Endcard **flyby SFX** + on-chain claim with retry

---

## Unity client (separate / legacy)

The repo also contains a Unity fog-duel prototype under `Assets/` (5×5 grid, Inco FoW research).  
**Judges evaluating the Megapot track should use the live web sandbox above**, not the Unity build.

---

## Env (Vercel / local)

See `PHASE12_SETUP.md`. Required for claims:

- `HOUSE_PRIVATE_KEY` — funded Base Sepolia wallet
- `NEXT_PUBLIC_SUPABASE_URL` / `NEXT_PUBLIC_SUPABASE_ANON_KEY`
- `SUPABASE_SERVICE_ROLE_KEY`

---

## Links

- Live game: https://veil.sithunyein.com/
- GitHub Pages mirror: https://thesithunyein.github.io/veil-war/
- Submit: [Inco Summer Game Jam](https://www.inco.org/blog/summer-game-jam-resources-and-what-to-build)
