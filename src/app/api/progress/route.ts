import { NextResponse } from "next/server";
import { createClient } from "@supabase/supabase-js";
import { DEFAULT_THEATERS } from "@/lib/shop/catalog";

export const runtime = "nodejs";

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

function serializeRow(row: Record<string, unknown> | null) {
  return {
    ok: true,
    megapotCredits: Math.max(0, Math.floor(Number(row?.megapot_credits) || 0)),
    ticketsMinted: Math.max(0, Math.floor(Number(row?.tickets_minted) || 0)),
    highScore: Math.max(0, Math.floor(Number(row?.high_score) || 0)),
    playAddress: (row?.play_address as string) || null,
    linkedMetamask: (row?.linked_metamask as string) || null,
    shopCredits: Math.max(0, Math.floor(Number(row?.shop_credits) || 0)),
    ownedShop: (row?.owned_shop as Record<string, boolean>) || {},
    unlockedTheaters: Array.isArray(row?.unlocked_theaters)
      ? row!.unlocked_theaters
      : [...DEFAULT_THEATERS],
    selectedTheater: (row?.selected_theater as string) || "arctic",
    purchaseHistory: Array.isArray(row?.purchase_history) ? row!.purchase_history : [],
    lifetimeTokens: Math.max(0, Math.floor(Number(row?.lifetime_tokens) || 0)),
  };
}

/** Sync combat credits / high score / optional MetaMask link after a mission. */
export async function POST(req: Request) {
  try {
    const authHeader = req.headers.get("authorization");
    if (!authHeader?.startsWith("Bearer ")) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }
    const supabase = userClient(authHeader);
    const {
      data: { user },
    } = await supabase.auth.getUser();
    if (!user) return NextResponse.json({ error: "Unauthorized" }, { status: 401 });

    const body = (await req.json()) as {
      addCredits?: number;
      playAddress?: string;
      highScore?: number;
      linkedMetamask?: string | null;
      shopCredits?: number;
      addShopCredits?: number;
      ownedShop?: Record<string, boolean>;
      unlockedTheaters?: string[];
      selectedTheater?: string;
      purchaseHistory?: unknown[];
      lifetimeTokens?: number;
    };

    const admin = adminClient();
    const { data: row } = await admin
      .from("veil_player_progress")
      .select("*")
      .eq("user_id", user.id)
      .maybeSingle();

    const add = Math.max(0, Math.floor(Number(body.addCredits) || 0));
    const addShop = Math.max(0, Math.floor(Number(body.addShopCredits) || 0));
    const credits = Math.max(0, Math.floor(Number(row?.megapot_credits) || 0)) + add;
    const high = Math.max(
      Math.floor(Number(row?.high_score) || 0),
      Math.floor(Number(body.highScore) || 0)
    );

    let linked = (row?.linked_metamask as string) || null;
    if (body.linkedMetamask === null) linked = null;
    else if (
      typeof body.linkedMetamask === "string" &&
      /^0x[a-fA-F0-9]{40}$/.test(body.linkedMetamask)
    ) {
      linked = body.linkedMetamask.toLowerCase();
    }

    const shopCredits =
      body.shopCredits != null
        ? Math.max(0, Math.floor(Number(body.shopCredits) || 0))
        : Math.max(0, Math.floor(Number(row?.shop_credits) || 0)) + addShop;

    const ownedShop =
      body.ownedShop && typeof body.ownedShop === "object"
        ? body.ownedShop
        : (row?.owned_shop as Record<string, boolean>) || {};

    const unlockedTheaters = Array.isArray(body.unlockedTheaters)
      ? body.unlockedTheaters
      : Array.isArray(row?.unlocked_theaters)
        ? row!.unlocked_theaters
        : [...DEFAULT_THEATERS];

    const selectedTheater =
      typeof body.selectedTheater === "string"
        ? body.selectedTheater
        : (row?.selected_theater as string) || "arctic";

    const purchaseHistory = Array.isArray(body.purchaseHistory)
      ? body.purchaseHistory
      : Array.isArray(row?.purchase_history)
        ? row!.purchase_history
        : [];

    const lifetimeTokens =
      body.lifetimeTokens != null
        ? Math.max(0, Math.floor(Number(body.lifetimeTokens) || 0))
        : Math.max(0, Math.floor(Number(row?.lifetime_tokens) || 0)) + addShop;

    const { data: next, error } = await admin
      .from("veil_player_progress")
      .upsert(
        {
          user_id: user.id,
          megapot_credits: credits,
          tickets_minted: Math.max(0, Math.floor(Number(row?.tickets_minted) || 0)),
          play_address: body.playAddress?.toLowerCase() || row?.play_address || null,
          high_score: high,
          linked_metamask: linked,
          shop_credits: shopCredits,
          owned_shop: ownedShop,
          unlocked_theaters: unlockedTheaters,
          selected_theater: selectedTheater,
          purchase_history: purchaseHistory,
          lifetime_tokens: lifetimeTokens,
          updated_at: new Date().toISOString(),
        },
        { onConflict: "user_id" }
      )
      .select("*")
      .single();

    if (error) throw new Error(error.message);
    return NextResponse.json(serializeRow(next));
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "Sync failed" },
      { status: 500 }
    );
  }
}

export async function GET(req: Request) {
  try {
    const authHeader = req.headers.get("authorization");
    if (!authHeader?.startsWith("Bearer ")) {
      return NextResponse.json({ error: "Unauthorized" }, { status: 401 });
    }
    const supabase = userClient(authHeader);
    const {
      data: { user },
    } = await supabase.auth.getUser();
    if (!user) return NextResponse.json({ error: "Unauthorized" }, { status: 401 });

    const admin = adminClient();
    const { data: row } = await admin
      .from("veil_player_progress")
      .select("*")
      .eq("user_id", user.id)
      .maybeSingle();

    return NextResponse.json(serializeRow(row));
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "Load failed" },
      { status: 500 }
    );
  }
}
