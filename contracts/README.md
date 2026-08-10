# Veil War — Confidential Contracts

Cross-chain architecture for the Megapot Hackathon Track:

| Contract | Chain | Role |
|----------|-------|------|
| `VeilWarCore.sol` | Inco Lightning (fhEVM) | Encrypted 16×16 fog grid, LOS, Megapot Vaults |
| `MegapotRewardController.sol` | Base Sepolia | Scout-to-earn ticket buys + Jackpot Stake Match |
| `lib/IncoLightning.sol` | Local shim | Swap for official Inco SDK on deploy |

## Base Sepolia Megapot addresses

```
USDC:                     0x036CbD53842c5426634e7929541eC2318f3dCF7e
Jackpot:                  0x465dA3c859f193A3807386387bEE941B2A4c3279
JackpotRandomTicketBuyer: 0x53c04e7e5044B28Ea8A4F9c4b26E3Ac1aeb63746
```

## Scout-to-Earn loop

1. Player moves scout on Inco → `VeilWarCore.moveScout`
2. Encrypted vault hit → `VaultLooted` + `onVeilReward(..., reason=3)`
3. Bot kill / match win → reasons `1` / `2`
4. `MegapotRewardController` buys Megapot tickets for the player via `buyTickets`

## Jackpot Stake Match

1. `createStakeMatch(amount)` — player A locks USDC
2. `joinStakeMatch(id, amount)` — player B locks USDC
3. After duel, `settleStakeMatch(id, winner)` — `stakeJackpotBps` (default 20%) buys shared tickets; remainder to winner

## Local compile (Foundry)

```bash
cd contracts
forge init --force --no-commit   # if needed
# remappings: see foundry.toml
forge build
```

Production: replace `lib/IncoLightning.sol` with the official Inco Lightning dependency and pass ciphertext handles from the client SDK instead of clear `uint8` coords.
