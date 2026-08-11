import {
  createPublicClient,
  createWalletClient,
  formatUnits,
  http,
  type Address,
  type Hex,
} from "viem";
import { privateKeyToAccount } from "viem/accounts";
import { baseSepolia } from "viem/chains";
import {
  MEGAPOT_SEPOLIA,
  MEGAPOT_SOURCE,
  PRECISE_UNIT,
  megapotAbi,
} from "./addresses";

function rpcUrl() {
  return (
    process.env.NEXT_PUBLIC_BASE_SEPOLIA_RPC ||
    "https://base-sepolia-rpc.publicnode.com"
  );
}

function getHouseAccount() {
  const raw = process.env.HOUSE_PRIVATE_KEY?.trim();
  if (!raw) return null;
  const pk = (raw.startsWith("0x") ? raw : `0x${raw}`) as Hex;
  return privateKeyToAccount(pk);
}

export function getHousePublicClient() {
  return createPublicClient({
    chain: baseSepolia,
    transport: http(rpcUrl()),
  });
}

export function getHouseWalletClient() {
  const account = getHouseAccount();
  if (!account) return null;
  return createWalletClient({
    account,
    chain: baseSepolia,
    transport: http(rpcUrl()),
  });
}

export async function readMegapotPool() {
  const client = getHousePublicClient();
  const drawingId = (await client.readContract({
    address: MEGAPOT_SEPOLIA.jackpot,
    abi: megapotAbi,
    functionName: "currentDrawingId",
  })) as bigint;

  const raw = (await client.readContract({
    address: MEGAPOT_SEPOLIA.jackpot,
    abi: megapotAbi,
    functionName: "getDrawingState",
    args: [drawingId],
  })) as unknown;

  const asTuple = Array.isArray(raw)
    ? (raw as readonly [bigint, ...unknown[], bigint, boolean])
    : null;

  const prizePool = asTuple ? (asTuple[0] as bigint) : 0n;
  const ticketPrice = (await client.readContract({
    address: MEGAPOT_SEPOLIA.jackpot,
    abi: megapotAbi,
    functionName: "ticketPrice",
  })) as bigint;

  return {
    drawingId: drawingId.toString(),
    prizePoolUsdc: formatUnits(prizePool, 6),
    ticketPriceUsdc: formatUnits(ticketPrice, 6),
  };
}

/** House buys 1 random Megapot ticket for recipient play wallet. */
export async function mintMegapotTicket(recipient: Address) {
  const account = getHouseAccount();
  const wallet = getHouseWalletClient();
  const publicClient = getHousePublicClient();
  if (!account || !wallet) {
    throw new Error("House wallet not configured (HOUSE_PRIVATE_KEY).");
  }

  const ticketPrice = (await publicClient.readContract({
    address: MEGAPOT_SEPOLIA.jackpot,
    abi: megapotAbi,
    functionName: "ticketPrice",
  })) as bigint;

  const usdcBal = (await publicClient.readContract({
    address: MEGAPOT_SEPOLIA.usdc,
    abi: megapotAbi,
    functionName: "balanceOf",
    args: [account.address],
  })) as bigint;

  if (usdcBal < ticketPrice) {
    throw new Error("JACKPOT_USDC_REFILL");
  }

  const allowance = (await publicClient.readContract({
    address: MEGAPOT_SEPOLIA.usdc,
    abi: megapotAbi,
    functionName: "allowance",
    args: [account.address, MEGAPOT_SEPOLIA.randomBuyer],
  })) as bigint;

  if (allowance < ticketPrice) {
    const approveHash = (await wallet.writeContract({
      address: MEGAPOT_SEPOLIA.usdc,
      abi: megapotAbi,
      functionName: "approve",
      args: [MEGAPOT_SEPOLIA.randomBuyer, ticketPrice * 20n],
      account,
      chain: baseSepolia,
    })) as Hex;
    await publicClient.waitForTransactionReceipt({ hash: approveHash });
  }

  const referrer = account.address;
  const buyHash = (await wallet.writeContract({
    address: MEGAPOT_SEPOLIA.randomBuyer,
    abi: megapotAbi,
    functionName: "buyTickets",
    args: [1n, recipient, [referrer], [PRECISE_UNIT], MEGAPOT_SOURCE],
    account,
    chain: baseSepolia,
  })) as Hex;

  const receipt = await publicClient.waitForTransactionReceipt({ hash: buyHash });
  return {
    txHash: buyHash,
    status: receipt.status,
    recipient,
    explorerUrl: `https://sepolia.basescan.org/tx/${buyHash}`,
  };
}
