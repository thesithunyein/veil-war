/**
 * Veil War — Google auth (Supabase) + silent play wallet (no MetaMask for play).
 * MetaMask is optional: linked for Base Sepolia shop / theater unlocks.
 */
import { createClient } from "https://cdn.jsdelivr.net/npm/@supabase/supabase-js@2/+esm";
import { generatePrivateKey, privateKeyToAccount } from "https://esm.sh/viem@2.55.11/accounts";

const SUPABASE_URL =
  window.__VEIL_SUPABASE_URL__ || "https://xcteyramlfparysqdmzg.supabase.co";
const SUPABASE_ANON =
  window.__VEIL_SUPABASE_ANON__ ||
  "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InhjdGV5cmFtbGZwYXJ5c3FkbXpnIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODY0NDY0MTksImV4cCI6MjEwMjAyMjQxOX0.kZUzVOhyxE3rjh2LH-HmLQCW0lQjtA5gv07zqyVB-6M";

const STORAGE_KEY = "veil_war_play_wallets_v1";
const BASE_SEPOLIA_CHAIN_ID = "0x14a34"; // 84532
const HOUSE =
  window.__VEIL_HOUSE_ADDRESS__ || "0xa399Ad139F2393bdFc88CfdafDfd3d5dEDA004D5";

const ETH_PRICES = {
  hull: "0x1c6bf52634000", // 0.0005 ETH
  muzzle: "0x2d79883d20000", // 0.0008 ETH
  radiator: "0x221b262dd8000", // 0.0006 ETH
  theater_crimson: "0x38d7ea4c68000", // 0.001 ETH
  theater_neon: "0x71afd498d0000", // 0.002 ETH
  theater_village: "0x5543df729c000", // 0.0015 ETH
  theater_city: "0x8e1bc9bf04000", // 0.0025 ETH
};

/** ~0.0002 ETH headroom for Base Sepolia gas on small shop txs */
const GAS_HEADROOM_WEI = 200000000000000n;

function shortAddr(a) {
  if (!a || a.length < 10) return a || "";
  return a.slice(0, 6) + "…" + a.slice(-4);
}

function formatEthFromWei(hexOrBig) {
  const wei = typeof hexOrBig === "bigint" ? hexOrBig : BigInt(hexOrBig);
  const eth = Number(wei) / 1e18;
  if (eth >= 0.001) return eth.toFixed(3) + " ETH";
  return eth.toFixed(4) + " ETH";
}

function parseMetaMaskError(err) {
  const msg = String(err?.message || err || "");
  if (err?.code === 4001 || /user rejected/i.test(msg)) {
    return "MetaMask transaction cancelled.";
  }
  if (/insufficient funds|exceeds balance|gas required exceeds/i.test(msg)) {
    return (
      "Not enough Base Sepolia ETH for payment + gas. " +
      "Get testnet ETH (Alchemy or Coinbase faucet), then retry."
    );
  }
  if (/network fee|gas/i.test(msg) && /unavailable|estimate/i.test(msg)) {
    return (
      "MetaMask could not estimate gas — usually not enough Base Sepolia ETH. " +
      "Fund your linked wallet with testnet ETH, then retry."
    );
  }
  return msg || "MetaMask transaction failed.";
}

async function waitForTxReceipt(txHash, maxMs = 90000) {
  const started = Date.now();
  while (Date.now() - started < maxMs) {
    const receipt = await window.ethereum.request({
      method: "eth_getTransactionReceipt",
      params: [txHash],
    });
    if (receipt) return receipt;
    await new Promise((r) => setTimeout(r, 1500));
  }
  return null;
}

async function fetchShopConfig() {
  try {
    const res = await fetch("/api/shop/purchase");
    const data = await res.json();
    if (data.ok && data.house) {
      VeilAuth.houseAddress = data.house;
    }
  } catch {
    /* static preview */
  }
}

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

function authHeaders() {
  if (!VeilAuth.accessToken) throw new Error("Sign in with Google first.");
  return {
    Authorization: `Bearer ${VeilAuth.accessToken}`,
    "Content-Type": "application/json",
  };
}

export const VeilAuth = {
  sb,
  user: null,
  playAddress: null,
  accessToken: null,
  megapotCredits: 0,
  ticketsMinted: 0,
  linkedMetamask: null,
  shopCredits: 0,
  ownedShop: {},
  unlockedTheaters: ["arctic", "sunset", "pacific"],
  selectedTheater: "arctic",
  purchaseHistory: [],
  lifetimeTokens: 0,
  highScore: 0,
  houseAddress: HOUSE,
  ethPrices: ETH_PRICES,

  async init() {
    await fetchShopConfig();
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
        this.linkedMetamask = null;
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
    this.linkedMetamask = null;
  },

  applyProgress(data) {
    if (!data || !data.ok) return;
    this.megapotCredits = data.megapotCredits || 0;
    this.ticketsMinted = data.ticketsMinted || 0;
    this.highScore = data.highScore || 0;
    this.linkedMetamask = data.linkedMetamask || null;
    this.shopCredits = data.shopCredits || 0;
    this.ownedShop = data.ownedShop || {};
    this.unlockedTheaters = data.unlockedTheaters || ["arctic", "sunset", "pacific"];
    this.selectedTheater = data.selectedTheater || "arctic";
    this.purchaseHistory = data.purchaseHistory || [];
    this.lifetimeTokens = data.lifetimeTokens || 0;
  },

  async refreshProgress() {
    if (!this.accessToken) return;
    try {
      const res = await fetch("/api/progress", { headers: authHeaders() });
      const data = await res.json();
      this.applyProgress(data);
    } catch {
      /* API may be offline in pure static preview */
    }
  },

  async syncMissionCredits(addCredits, highScore, extra = {}) {
    if (!this.accessToken || !this.playAddress) return null;
    const res = await fetch("/api/progress", {
      method: "POST",
      headers: authHeaders(),
      body: JSON.stringify({
        addCredits: addCredits || 0,
        playAddress: this.playAddress,
        highScore: highScore || 0,
        ...extra,
      }),
    });
    const data = await res.json();
    this.applyProgress(data);
    return data;
  },

  async linkMetamask(address) {
    if (!/^0x[a-fA-F0-9]{40}$/.test(address || "")) {
      throw new Error("Invalid MetaMask address");
    }
    const res = await fetch("/api/progress", {
      method: "POST",
      headers: authHeaders(),
      body: JSON.stringify({
        playAddress: this.playAddress,
        linkedMetamask: address.toLowerCase(),
      }),
    });
    const data = await res.json();
    if (!res.ok || !data.ok) throw new Error(data.error || "Link failed");
    this.applyProgress(data);
    return data;
  },

  async ensureBaseSepolia() {
    if (!window.ethereum) throw new Error("MetaMask required for shop unlocks");
    const chainId = await window.ethereum.request({ method: "eth_chainId" });
    if (chainId === BASE_SEPOLIA_CHAIN_ID) return;
    try {
      await window.ethereum.request({
        method: "wallet_switchEthereumChain",
        params: [{ chainId: BASE_SEPOLIA_CHAIN_ID }],
      });
    } catch (e) {
      if (e && e.code === 4902) {
        await window.ethereum.request({
          method: "wallet_addEthereumChain",
          params: [
            {
              chainId: BASE_SEPOLIA_CHAIN_ID,
              chainName: "Base Sepolia",
              nativeCurrency: { name: "ETH", symbol: "ETH", decimals: 18 },
              rpcUrls: ["https://base-sepolia-rpc.publicnode.com"],
              blockExplorerUrls: ["https://sepolia.basescan.org"],
            },
          ],
        });
      } else {
        throw e;
      }
    }
  },

  async connectMetamask() {
    if (!window.ethereum) throw new Error("Install MetaMask to link a payment wallet");
    await this.ensureBaseSepolia();
    const accounts = await window.ethereum.request({ method: "eth_requestAccounts" });
    if (!accounts || !accounts[0]) throw new Error("No MetaMask account");
    return this.linkMetamask(accounts[0]);
  },

  async ensureBaseSepolia() {
    if (!window.ethereum) throw new Error("MetaMask required for shop unlocks");
    const chainId = await window.ethereum.request({ method: "eth_chainId" });
    if (chainId === BASE_SEPOLIA_CHAIN_ID) return;
    try {
      await window.ethereum.request({
        method: "wallet_switchEthereumChain",
        params: [{ chainId: BASE_SEPOLIA_CHAIN_ID }],
      });
    } catch (e) {
      if (e && e.code === 4902) {
        await window.ethereum.request({
          method: "wallet_addEthereumChain",
          params: [
            {
              chainId: BASE_SEPOLIA_CHAIN_ID,
              chainName: "Base Sepolia",
              nativeCurrency: { name: "ETH", symbol: "ETH", decimals: 18 },
              rpcUrls: ["https://base-sepolia-rpc.publicnode.com"],
              blockExplorerUrls: ["https://sepolia.basescan.org"],
            },
          ],
        });
      } else {
        throw e;
      }
    }
    const after = await window.ethereum.request({ method: "eth_chainId" });
    if (after !== BASE_SEPOLIA_CHAIN_ID) {
      throw new Error("Switch MetaMask to Base Sepolia network, then retry.");
    }
  },

  async getLinkedOrActiveAccount() {
    await this.ensureBaseSepolia();
    const accounts = await window.ethereum.request({ method: "eth_requestAccounts" });
    const from = accounts?.[0];
    if (!from) throw new Error("No MetaMask account selected.");
    if (
      this.linkedMetamask &&
      this.linkedMetamask.toLowerCase() !== from.toLowerCase()
    ) {
      throw new Error(
        "Use your linked MetaMask wallet (" +
          shortAddr(this.linkedMetamask) +
          ") or relink in Profile."
      );
    }
    if (!this.linkedMetamask) {
      await this.linkMetamask(from);
    }
    return from;
  },

  async purchaseWithEth(sku) {
    if (!this.signedIn()) throw new Error("Sign in with Google first");
    if (!window.ethereum) {
      throw new Error("Install MetaMask to buy armory / theater unlocks.");
    }

    const value = ETH_PRICES[sku];
    if (!value) throw new Error("Unknown item");
    const valueWei = BigInt(value);
    const treasury = (this.houseAddress || HOUSE).toLowerCase();

    const from = await this.getLinkedOrActiveAccount();
    const fromLower = from.toLowerCase();
    if (fromLower === treasury) {
      throw new Error(
        "Use a personal MetaMask wallet — not the Veil house treasury wallet."
      );
    }

    const balanceHex = await window.ethereum.request({
      method: "eth_getBalance",
      params: [from, "latest"],
    });
    const balance = BigInt(balanceHex || "0x0");
    if (balance < valueWei + GAS_HEADROOM_WEI) {
      throw new Error(
        "Need " +
          formatEthFromWei(valueWei) +
          " + gas on Base Sepolia (you have " +
          formatEthFromWei(balance) +
          "). Get testnet ETH from a Base Sepolia faucet, then retry."
      );
    }

    const tx = {
      from,
      to: this.houseAddress || HOUSE,
      value,
    };

    let gasHex = "0x5208";
    try {
      gasHex = await window.ethereum.request({
        method: "eth_estimateGas",
        params: [tx],
      });
    } catch (e) {
      throw new Error(parseMetaMaskError(e));
    }
    tx.gas = gasHex;

    let txHash;
    try {
      txHash = await window.ethereum.request({
        method: "eth_sendTransaction",
        params: [tx],
      });
    } catch (e) {
      throw new Error(parseMetaMaskError(e));
    }
    if (!txHash) throw new Error("MetaMask did not return a transaction hash.");

    const receipt = await waitForTxReceipt(txHash);
    if (receipt && receipt.status === "0x0") {
      throw new Error("Payment transaction failed on-chain.");
    }

    let lastErr = null;
    for (let i = 0; i < 10; i++) {
      const res = await fetch("/api/shop/purchase", {
        method: "POST",
        headers: authHeaders(),
        body: JSON.stringify({ sku, txHash }),
      });
      const data = await res.json();
      if (res.ok && data.ok) {
        this.applyProgress({
          ok: true,
          megapotCredits: this.megapotCredits,
          ticketsMinted: this.ticketsMinted,
          highScore: this.highScore,
          linkedMetamask: data.linkedMetamask || this.linkedMetamask,
          shopCredits: this.shopCredits,
          ownedShop: data.ownedShop || this.ownedShop,
          unlockedTheaters: data.unlockedTheaters || this.unlockedTheaters,
          selectedTheater: this.selectedTheater,
          purchaseHistory: data.purchaseHistory || this.purchaseHistory,
          lifetimeTokens: this.lifetimeTokens,
        });
        return data;
      }
      lastErr = data.error || "Purchase verify failed";
      if (res.status !== 400) break;
      await new Promise((r) => setTimeout(r, 2000));
    }
    throw new Error(
      lastErr ||
        "Payment sent — verification pending. Wait ~30s and tap unlock again."
    );
  },

  async claimMegapotTicket() {
    if (!this.accessToken || !this.playAddress) {
      throw new Error("Sign in with Google to claim.");
    }
    const res = await fetch("/api/megapot/claim", {
      method: "POST",
      headers: authHeaders(),
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
