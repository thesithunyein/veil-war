import {
  createPublicClient,
  createWalletClient,
  formatUnits,
  http,
  maxUint256,
  type Address,
  type Hex,
} from "viem";
import { privateKeyToAccount } from "viem/accounts";
import { baseSepolia } from "viem/chains";
import {
  MEGAPOT_SEPOLIA,
  MEGAPOT_SOURCE,
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

  const asTuple = Array.isArray(raw) ? (raw as unknown[]) : null;
  const prizePool =
    asTuple && typeof asTuple[0] === "bigint" ? asTuple[0] : 0n;
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

async function ensureUsdcAllowance(
  publicClient: ReturnType<typeof getHousePublicClient>,
  wallet: NonNullable<ReturnType<typeof getHouseWalletClient>>,
  account: NonNullable<ReturnType<typeof getHouseAccount>>,
  needed: bigint
) {
  const allowance = (await publicClient.readContract({
    address: MEGAPOT_SEPOLIA.usdc,
    abi: megapotAbi,
    functionName: "allowance",
    args: [account.address, MEGAPOT_SEPOLIA.randomBuyer],
  })) as bigint;

  if (allowance >= needed) return;

  // Some USDC deployments require clearing non-zero allowance before raise.
  if (allowance > 0n) {
    const clearHash = (await wallet.writeContract({
      address: MEGAPOT_SEPOLIA.usdc,
      abi: megapotAbi,
      functionName: "approve",
      args: [MEGAPOT_SEPOLIA.randomBuyer, 0n],
      account,
      chain: baseSepolia,
    })) as Hex;
    await publicClient.waitForTransactionReceipt({ hash: clearHash });
  }

  const approveHash = (await wallet.writeContract({
    address: MEGAPOT_SEPOLIA.usdc,
    abi: megapotAbi,
    functionName: "approve",
    args: [MEGAPOT_SEPOLIA.randomBuyer, maxUint256],
    account,
    chain: baseSepolia,
  })) as Hex;
  await publicClient.waitForTransactionReceipt({ hash: approveHash });
}

/** House buys 1 random Megapot ticket for recipient play wallet. */
export async function mintMegapotTicket(recipient: Address) {
  let lastErr: unknown;
  for (let attempt = 0; attempt < 3; attempt++) {
    try {
      return await mintMegapotTicketOnce(recipient);
    } catch (err) {
      lastErr = err;
      if (attempt < 2) {
        await new Promise((r) => setTimeout(r, 900 * (attempt + 1)));
      }
    }
  }
  throw lastErr;
}

async function mintMegapotTicketOnce(recipient: Address) {
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

  await ensureUsdcAllowance(publicClient, wallet, account, ticketPrice);

  const ethBal = await publicClient.getBalance({ address: account.address });
  if (ethBal < 50000000000000n) {
    throw new Error("JACKPOT_ETH_REFILL");
  }

  const buyArgs = [1n, recipient, [], [], MEGAPOT_SOURCE] as const;
  const { request } = await publicClient.simulateContract({
    address: MEGAPOT_SEPOLIA.randomBuyer,
    abi: megapotAbi,
    functionName: "buyTickets",
    args: buyArgs,
    account,
  });
  const buyHash = (await wallet.writeContract(request)) as Hex;

  const receipt = await publicClient.waitForTransactionReceipt({
    hash: buyHash,
    confirmations: 1,
    timeout: 45_000,
  });
  if (receipt.status !== "success") {
    throw new Error("Megapot buyTickets reverted on-chain.");
  }
  return {
    txHash: buyHash,
    status: receipt.status,
    recipient,
    explorerUrl: `https://sepolia.basescan.org/tx/${buyHash}`,
  };
}
