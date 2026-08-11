import { NextResponse } from "next/server";
import { createClient } from "@supabase/supabase-js";
import { mintMegapotTicket, readMegapotPool } from "@/lib/megapot/client";

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

export async function GET() {
  try {
    const pool = await readMegapotPool();
    return NextResponse.json({ ok: true, ...pool });
  } catch (err) {
    return NextResponse.json(
      {
        ok: false,
        error: err instanceof Error ? err.message : "Could not read Megapot.",
      },
      { status: 200 }
    );
  }
}

export async function POST(req: Request) {
  try {
    const authHeader = req.headers.get("authorization");
    if (!authHeader?.startsWith("Bearer ")) {
      return NextResponse.json({ error: "Sign in with Google to claim." }, { status: 401 });
    }

    const supabase = userClient(authHeader);
    const {
      data: { user },
      error: userErr,
    } = await supabase.auth.getUser();
    if (userErr || !user) {
      return NextResponse.json({ error: "Sign in with Google to claim." }, { status: 401 });
    }

    const body = (await req.json()) as { recipient?: string };
    const recipient = body.recipient;
    if (!recipient || !/^0x[a-fA-F0-9]{40}$/.test(recipient)) {
      return NextResponse.json({ error: "Play wallet recipient required." }, { status: 400 });
    }

    const admin = adminClient();
    const { data: row } = await admin
      .from("veil_player_progress")
      .select("*")
      .eq("user_id", user.id)
      .maybeSingle();

    const credits = Math.max(0, Math.floor(Number(row?.megapot_credits) || 0));
    if (credits <= 0) {
      return NextResponse.json(
        { error: "No Megapot credits. Win a mission to earn credits." },
        { status: 402 }
      );
    }

    const minted = await mintMegapotTicket(recipient as `0x${string}`);

    const nextCredits = credits - 1;
    const nextMinted = Math.max(0, Math.floor(Number(row?.tickets_minted) || 0)) + 1;
    await admin.from("veil_player_progress").upsert(
      {
        user_id: user.id,
        megapot_credits: nextCredits,
        tickets_minted: nextMinted,
        play_address: recipient.toLowerCase(),
        updated_at: new Date().toISOString(),
      },
      { onConflict: "user_id" }
    );

    let pool = null;
    try {
      pool = await readMegapotPool();
    } catch {
      /* ignore */
    }

    return NextResponse.json({
      ok: true,
      ...minted,
      pool,
      megapotCredits: nextCredits,
      ticketsMinted: nextMinted,
    });
  } catch (err) {
    const raw = err instanceof Error ? err.message : "Megapot claim failed.";
    const error =
      raw === "JACKPOT_USDC_REFILL" || /USDC|usdc/i.test(raw)
        ? "House USDC empty — refill Base Sepolia USDC on the house wallet, then retry."
        : raw;
    return NextResponse.json(
      { error, code: raw === "JACKPOT_USDC_REFILL" ? "usdc_refill" : "claim_failed" },
      { status: 500 }
    );
  }
}
