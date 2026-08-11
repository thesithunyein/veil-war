/**
 * Veil War — Google auth (Supabase) + silent play wallet (no MetaMask for play).
 * Loaded as ES module from the game page.
 */
import { createClient } from "https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2/+esm";
import { generatePrivateKey, privateKeyToAccount } from "https://esm.sh/viem@2.55.11/accounts";

const SUPABASE_URL =
  window.__VEIL_SUPABASE_URL__ || "https://xcteyramlfparysqdmzg.supabase.co";
const SUPABASE_ANON =
  window.__VEIL_SUPABASE_ANON__ ||
  "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InhjdGV5cmFtbGZwYXJ5c3FkbXpnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODY0NDY0MTksImV4cCI6MjEwMjAyMjQxOX0.kZUzVOhyxE3rjh2LH-HmLQCW0lQjtA5gv07zqyVB-6M";

const STORAGE_KEY = "veil_war_play_wallets_v1";

const sb = createClient(SUPABASE_URL, SUPABASE_ANON);

function readStore() {
  try {
    return JSON.parse(localStorage.getItem(STORAGE_KEY) || "{}");
  } catch {
    return {};
  }
}

function writeStore(store) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(store));
  } catch {
    /* private mode */
  }
}

export function getOrCreatePlayAddress(googleUserId) {
  const id = String(googleUserId || "").trim();
  if (!id) throw new Error("Google sign-in required");
  const store = readStore();
  if (!store[id]) {
    store[id] = generatePrivateKey();
    writeStore(store);
  }
  return privateKeyToAccount(store[id]).address;
}

export const VeilAuth = {
  sb,
  user: null,
  playAddress: null,
  accessToken: null,
  megapotCredits: 0,
  ticketsMinted: 0,

  async init() {
    const { data } = await sb.auth.getSession();
    if (data.session?.user) {
      this.user = data.session.user;
      this.accessToken = data.session.access_token;
      this.playAddress = getOrCreatePlayAddress(this.user.id);
      await this.refreshProgress();
    }
    sb.auth.onAuthStateChange(async (_evt, session) => {
      this.user = session?.user || null;
      this.accessToken = session?.access_token || null;
      if (this.user) {
        this.playAddress = getOrCreatePlayAddress(this.user.id);
        await this.refreshProgress();
      } else {
        this.playAddress = null;
        this.megapotCredits = 0;
      }
      if (typeof window.__veilOnAuthChange === "function") {
        window.__veilOnAuthChange(this);
      }
    });
    return this;
  },

  signedIn() {
    return !!(this.user && this.playAddress);
  },

  async signInWithGoogle() {
    const { error } = await sb.auth.signInWithOAuth({
      provider: "google",
      options: {
        redirectTo: window.location.origin + "/index.html",
        queryParams: { prompt: "select_account" },
      },
    });
    if (error) throw error;
  },

  async signOut() {
    await sb.auth.signOut();
    this.user = null;
    this.playAddress = null;
    this.accessToken = null;
  },

  async refreshProgress() {
    if (!this.accessToken) return;
    try {
      const res = await fetch("/api/progress", {
        headers: { Authorization: `Bearer ${this.accessToken}` },
      });
      const data = await res.json();
      if (data.ok) {
        this.megapotCredits = data.megapotCredits || 0;
        this.ticketsMinted = data.ticketsMinted || 0;
      }
    } catch {
      /* API may be offline in pure static preview */
    }
  },

  async syncMissionCredits(addCredits, highScore) {
    if (!this.accessToken || !this.playAddress) return null;
    const res = await fetch("/api/progress", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${this.accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        addCredits: addCredits || 0,
        playAddress: this.playAddress,
        highScore: highScore || 0,
      }),
    });
    const data = await res.json();
    if (data.ok) {
      this.megapotCredits = data.megapotCredits;
      this.ticketsMinted = data.ticketsMinted;
    }
    return data;
  },

  async claimMegapotTicket() {
    if (!this.accessToken || !this.playAddress) {
      throw new Error("Sign in with Google to claim.");
    }
    const res = await fetch("/api/megapot/claim", {
      method: "POST",
      headers: {
        Authorization: `Bearer ${this.accessToken}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify({ recipient: this.playAddress }),
    });
    const data = await res.json();
    if (!res.ok || !data.ok) {
      throw new Error(data.error || "Claim failed");
    }
    this.megapotCredits = data.megapotCredits ?? this.megapotCredits;
    this.ticketsMinted = data.ticketsMinted ?? this.ticketsMinted;
    return data;
  },
};

window.VeilAuth = VeilAuth;
