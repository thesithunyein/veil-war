# Security Policy

## Supported versions

| Version | Supported |
| ------- | --------- |
| `master` (live at [veil.sithunyein.com](https://veil.sithunyein.com/)) | Yes |
| Older commits | Best effort |

## Reporting a vulnerability

**Please do not open public GitHub issues for security problems.**

If you discover a vulnerability in Veil War (game client, API routes, Supabase integration, or on-chain claim flow), report it privately:

1. Email or DM the maintainer via [GitHub](https://github.com/thesithunyein) with subject **Veil War Security**
2. Include steps to reproduce, impact, and affected URLs or contract interactions
3. Allow up to **72 hours** for an initial response

### What to include

- Description of the issue
- Proof of concept (if available)
- Whether it affects production (`veil.sithunyein.com`) or testnet only
- Your contact for follow-up

### Out of scope

- Social engineering against players
- Issues in third-party services (Supabase, Vercel, Base RPC) — report to those vendors
- Unity `Assets/` prototype unless it ships to production

### Safe harbor

We appreciate responsible disclosure. Valid reports will be acknowledged and fixed where appropriate before public disclosure when possible.

## Security practices in this repo

- **House wallet** (`HOUSE_PRIVATE_KEY`) must never be committed — use Vercel env vars only
- **Supabase service role** is server-side only
- Megapot claims require authenticated Google session + server-side credit check
- Play wallets are generated client-side; MetaMask linking is optional

## Bug bounty

No formal bounty program at this time. Critical fixes may be credited in release notes at the reporter's discretion.
