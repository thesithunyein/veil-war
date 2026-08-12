import { NextResponse } from "next/server";
import { createClient } from "@supabase/supabase-js";
import { formatEther, type Hex } from "viem";
import { getHousePublicClient } from "@/lib/megapot/client";
import {
  DEFAULT_THEATERS,
  DEFAULT_SELECTED_THEATER,
  SHOP_CATALOG,
  houseAddress,
  type ShopSku,
} from "@/lib/shop/catalog";

export const runtime = "nodejs";
export const maxDuration = 60;

function userClient(authHeader: string | null) {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL!;
  const anon = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!;
  return createClient(url, anon, {
    global: { headers: authHeader ? { Authorization: authHeader } : {} },
    auth: { persistSession: false, autoRefreshToken: false },
  });
}

function adminClient() {
  const url = process.env.NEXT_PUBLIC_SUPABASE_URL!;
  const key = process.env.SUPABASE_SERVICE_ROLE_KEY;
  if (!key) throw new Error("SERVICE_ROLE not configured");
  return createClient(url, key, {
    auth: { persistSession: false, autoRefreshToken: false },
  });
}

function isSku(v: string): v is ShopSku {
  return Object.prototype.hasOwnProperty.call(SHOP_CATALOG, v);
}

/** GET catalog + house treasury address */
export async function GET() {
  const items = Object.entries(SHOP_CATALOG).map(([sku, meta]) => ({
    sku,
    kind: meta.kind,
    label: meta.label,
    eth: formatEther(meta.ethWei),
    theaterId: "theaterId" in meta ? meta.theaterId : null,
  }));
  return NextResponse.json({
    ok: true,
    chainId: 84532,
    chainName: "Base Sepolia",
    house: houseAddress(),
    items,
  });
}

/**
 * Verify a Base Sepolia ETH payment to the house wallet and unlock the SKU
 * on the Google user's progress row.
 */
export async function POST(req: Request) {
  try {
    const authHeader = req.headers.get("authorization");
    if (!authHeader?.startsWith("Bearer ")) {
      return NextResponse.json({ error: "Sign in with Google first." }, { status: 401 });
    }
    const supabase = userClient(authHeader);
    const {
      data: { user },
    } = await supabase.auth.getUser();
    if (!user) {
      return NextResponse.json({ error: "Sign in with Google first." }, { status: 401 });
    }

    const body = (await req.json()) as { sku?: string; txHash?: string };
    const sku = String(body.sku || "");
    const txHash = String(body.txHash || "").toLowerCase();
    if (!isSku(sku)) {
      return NextResponse.json({ error: "Unknown shop SKU." }, { status: 400 });
    }
    if (!/^0x[a-f0-9]{64}$/.test(txHash)) {
      return NextResponse.json({ error: "Invalid tx hash." }, { status: 400 });
    }

    const admin = adminClient();
    const { data: existingTx } = await admin
      .from("veil_purchases")
      .select("id, user_id, sku")
      .eq("tx_hash", txHash)
      .maybeSingle();
    if (existingTx) {
      if (existingTx.user_id === user.id && existingTx.sku === sku) {
        const { data: row } = await admin
          .from("veil_player_progress")
          .select("*")
          .eq("user_id", user.id)
          .maybeSingle();
        return NextResponse.json({
          ok: true,
          already: true,
          ownedShop: row?.owned_shop || {},
          unlockedTheaters: row?.unlocked_theaters || DEFAULT_THEATERS,
        });
      }
      return NextResponse.json({ error: "Tx already used." }, { status: 409 });
    }

    const catalog = SHOP_CATALOG[sku];
    const client = getHousePublicClient();
    const receipt = await client.getTransactionReceipt({ hash: txHash as Hex });
    if (!receipt || receipt.status !== "success") {
      return NextResponse.json({ error: "Tx not successful yet — wait and retry." }, { status: 400 });
    }
    const tx = await client.getTransaction({ hash: txHash as Hex });
    const treasury = houseAddress();
    const to = (tx.to || "").toLowerCase();
    const from = (tx.from || "").toLowerCase();
    if (to !== treasury) {
      return NextResponse.json({ error: "Payment must go to the Veil house wallet." }, { status: 400 });
    }
    if (tx.value < catalog.ethWei) {
      return NextResponse.json(
        {
          error: `Underpaid — need ${formatEther(catalog.ethWei)} ETH on Base Sepolia.`,
        },
        { status: 400 }
      );
    }

    const { data: row } = await admin
      .from("veil_player_progress")
      .select("*")
      .eq("user_id", user.id)
      .maybeSingle();

    const linked = (row?.linked_metamask || "").toLowerCase();
    if (linked && linked !== from) {
      return NextResponse.json(
        { error: "Pay from your linked MetaMask wallet." },
        { status: 400 }
      );
    }

    const ownedShop = {
      ...((row?.owned_shop as Record<string, boolean>) || {}),
    };
    let unlockedTheaters: string[] = Array.isArray(row?.unlocked_theaters)
      ? [...row!.unlocked_theaters]
      : [...DEFAULT_THEATERS];
    const history = Array.isArray(row?.purchase_history)
      ? [...row!.purchase_history]
      : [];

    if (catalog.kind === "shop") {
      if (ownedShop[sku]) {
        return NextResponse.json({ error: "Already owned." }, { status: 409 });
      }
      ownedShop[sku] = true;
    } else {
      if (unlockedTheaters.includes(catalog.theaterId)) {
        return NextResponse.json({ error: "Theater already unlocked." }, { status: 409 });
      }
      unlockedTheaters.push(catalog.theaterId);
    }

    history.push({
      type: catalog.kind === "shop" ? "shop_buy" : "theater_unlock",
      sku,
      name: catalog.label,
      eth: formatEther(catalog.ethWei),
      txHash,
      at: new Date().toISOString(),
    });

    const { error: buyErr } = await admin.from("veil_purchases").insert({
      user_id: user.id,
      sku,
      tx_hash: txHash,
      payer: from,
      amount_wei: tx.value.toString(),
    });
    if (buyErr) throw new Error(buyErr.message);

    const { data: next, error } = await admin
      .from("veil_player_progress")
      .upsert(
        {
          user_id: user.id,
          megapot_credits: Math.max(0, Math.floor(Number(row?.megapot_credits) || 0)),
          tickets_minted: Math.max(0, Math.floor(Number(row?.tickets_minted) || 0)),
          play_address: row?.play_address || null,
          high_score: Math.max(0, Math.floor(Number(row?.high_score) || 0)),
          linked_metamask: linked || from,
          shop_credits: Math.max(0, Math.floor(Number(row?.shop_credits) || 0)),
          owned_shop: ownedShop,
          unlocked_theaters: unlockedTheaters,
          selected_theater: row?.selected_theater || DEFAULT_SELECTED_THEATER,
          purchase_history: history,
          lifetime_tokens: Math.max(0, Math.floor(Number(row?.lifetime_tokens) || 0)),
          updated_at: new Date().toISOString(),
        },
        { onConflict: "user_id" }
      )
      .select("*")
      .single();
    if (error) throw new Error(error.message);

    return NextResponse.json({
      ok: true,
      sku,
      txHash,
      explorerUrl: `https://sepolia.basescan.org/tx/${txHash}`,
      ownedShop: next.owned_shop,
      unlockedTheaters: next.unlocked_theaters,
      linkedMetamask: next.linked_metamask,
      purchaseHistory: next.purchase_history,
    });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "Purchase failed" },
      { status: 500 }
    );
  }
}
