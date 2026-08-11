import { NextResponse } from "next/server";
import { createClient } from "@supabase/supabase-js";

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

/** Sync combat credits / high score after a mission (Google session required). */
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
    };
    const add = Math.max(0, Math.floor(Number(body.addCredits) || 0));
    const admin = adminClient();
    const { data: row } = await admin
      .from("veil_player_progress")
      .select("*")
      .eq("user_id", user.id)
      .maybeSingle();

    const credits = Math.max(0, Math.floor(Number(row?.megapot_credits) || 0)) + add;
    const high = Math.max(
      Math.floor(Number(row?.high_score) || 0),
      Math.floor(Number(body.highScore) || 0)
    );

    const { data: next, error } = await admin
      .from("veil_player_progress")
      .upsert(
        {
          user_id: user.id,
          megapot_credits: credits,
          tickets_minted: Math.max(0, Math.floor(Number(row?.tickets_minted) || 0)),
          play_address: body.playAddress?.toLowerCase() || row?.play_address || null,
          high_score: high,
          updated_at: new Date().toISOString(),
        },
        { onConflict: "user_id" }
      )
      .select("*")
      .single();

    if (error) throw new Error(error.message);
    return NextResponse.json({
      ok: true,
      megapotCredits: next.megapot_credits,
      ticketsMinted: next.tickets_minted,
      highScore: next.high_score,
    });
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

    return NextResponse.json({
      ok: true,
      megapotCredits: Math.max(0, Math.floor(Number(row?.megapot_credits) || 0)),
      ticketsMinted: Math.max(0, Math.floor(Number(row?.tickets_minted) || 0)),
      highScore: Math.max(0, Math.floor(Number(row?.high_score) || 0)),
      playAddress: row?.play_address || null,
    });
  } catch (err) {
    return NextResponse.json(
      { error: err instanceof Error ? err.message : "Load failed" },
      { status: 500 }
    );
  }
}
