import { parseEther } from "viem";

/** Base Sepolia ETH prices for armory + paid theaters (paid to house wallet). */
export const SHOP_CATALOG = {
  hull: {
    kind: "shop" as const,
    label: "Heavy Armored Hull",
    ethWei: parseEther("0.0005"),
  },
  muzzle: {
    kind: "shop" as const,
    label: "Muzzle-Velocity Boost",
    ethWei: parseEther("0.0008"),
  },
  radiator: {
    kind: "shop" as const,
    label: "Overheat Radiator Mod",
    ethWei: parseEther("0.0006"),
  },
  theater_crimson: {
    kind: "theater" as const,
    theaterId: "crimson",
    label: "Overgrown Deep Forest Storm",
    ethWei: parseEther("0.001"),
  },
  theater_neon: {
    kind: "theater" as const,
    theaterId: "neon",
    label: "Majestic Sunset Mountain Ridge",
    ethWei: parseEther("0.002"),
  },
} as const;

export type ShopSku = keyof typeof SHOP_CATALOG;

export const DEFAULT_THEATERS = ["arctic", "sunset", "pacific"] as const;

export function houseAddress(): `0x${string}` {
  const fromEnv = process.env.NEXT_PUBLIC_HOUSE_ADDRESS?.trim();
  if (fromEnv && /^0x[a-fA-F0-9]{40}$/.test(fromEnv)) {
    return fromEnv.toLowerCase() as `0x${string}`;
  }
  // Generated house wallet used for Megapot claims + shop treasury
  return "0xa399ad139f2393bdfc88cfdafdfd3d5deda004d5";
}
