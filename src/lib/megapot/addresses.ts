import { keccak256, parseAbi, toBytes, type Address } from "viem";

export const MEGAPOT_SEPOLIA = {
  jackpot: "0x465dA3c859f193A3807386387bEE941B2A4c3279" as const,
  usdc: "0x036CbD53842c5426634e7929541eC2318f3dCF7e" as const,
  randomBuyer: "0x53c04e7e5044B28Ea8A4F9c4b26E3Ac1aeb63746" as const,
};

export const MEGAPOT_SOURCE = keccak256(toBytes("veil-war"));

/**
 * Megapot referral split uses PRECISE_UNIT (1e18 = 100%), despite the ABI name
 * `_referralSplitBps`. Passing 10000 reverts on Base Sepolia.
 */
export const PRECISE_UNIT = 10n ** 18n;

export const megapotAbi = parseAbi([
  "function ticketPrice() view returns (uint256)",
  "function currentDrawingId() view returns (uint256)",
  "function getDrawingState(uint256 _drawingId) view returns ((uint256 prizePool, uint256 ticketPrice, uint256 edgePerTicket, uint256 referralWinShare, uint256 referralFee, uint256 globalTicketsBought, uint256 lpEarnings, uint256 drawingTime, uint256 winningTicket, uint8 ballMax, uint8 bonusballMax, address payoutCalculator, bool jackpotLock))",
  "function buyTickets(uint256 _count, address _recipient, address[] _referrers, uint256[] _referralSplitBps, bytes32 _source) returns (uint256[] ticketIds)",
  "function balanceOf(address account) view returns (uint256)",
  "function allowance(address owner, address spender) view returns (uint256)",
  "function approve(address spender, uint256 amount) returns (bool)",
]);

export type MegapotRecipient = Address;
