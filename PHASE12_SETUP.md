# Veil War — Phase 1+2 (Google + silent wallet + Megapot claim)

## Supabase (project `xcteyramlfparysqdmzg`)

1. Run SQL in SQL Editor: `supabase/veil_player_progress.sql`
2. Auth → Google: enabled (you already did OAuth client)
3. Auth → URL config:
   - Site URL: `https://veil.sithunyein.com`
   - Redirect: `https://veil.sithunyein.com/**`, `http://localhost:3000/**`

## Vercel env (Project Settings → Environment Variables)

```
NEXT_PUBLIC_SUPABASE_URL=https://xcteyramlfparysqdmzg.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=<anon key>
SUPABASE_SERVICE_ROLE_KEY=<service_role — Dashboard → API — never commit>
HOUSE_PRIVATE_KEY=<0x… Base Sepolia funded wallet>
NEXT_PUBLIC_BASE_SEPOLIA_RPC=https://base-sepolia-rpc.publicnode.com
```

Fund `HOUSE_PRIVATE_KEY` on Base Sepolia with:
- ETH for gas
- USDC (`0x036CbD…`) enough for ticket buys

## Local

```bash
npm install
# fill .env.local service role + house key
npm run dev
```

Open http://localhost:3000 → redirects to `/index.html`.

## Player flow

1. SIGN IN (Google) — no MetaMask
2. Silent play wallet created in browser
3. ENGAGE → dogfight
4. Mission end → credits synced
5. CLAIM MEGAPOT TICKET → house mints on Base → BaseScan link
