-- Veil War — full schema (run in Supabase SQL editor)
-- Safe to re-run: uses IF NOT EXISTS / additive ALTERs.

create table if not exists public.veil_player_progress (
  user_id uuid primary key references auth.users(id) on delete cascade,
  megapot_credits int not null default 0,
  tickets_minted int not null default 0,
  play_address text,
  high_score int not null default 0,
  updated_at timestamptz not null default now()
);

alter table public.veil_player_progress
  add column if not exists linked_metamask text,
  add column if not exists shop_credits int not null default 0,
  add column if not exists owned_shop jsonb not null default '{}'::jsonb,
  add column if not exists unlocked_theaters jsonb not null default '["arctic","sunset","pacific"]'::jsonb,
  add column if not exists selected_theater text not null default 'sunset',
  add column if not exists purchase_history jsonb not null default '[]'::jsonb,
  add column if not exists lifetime_tokens int not null default 0;

create unique index if not exists veil_progress_linked_metamask_uidx
  on public.veil_player_progress (lower(linked_metamask))
  where linked_metamask is not null;

create table if not exists public.veil_purchases (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  sku text not null,
  tx_hash text not null,
  payer text not null,
  amount_wei text not null,
  created_at timestamptz not null default now(),
  constraint veil_purchases_tx_hash_unique unique (tx_hash)
);

create index if not exists veil_purchases_user_idx on public.veil_purchases (user_id);

alter table public.veil_player_progress enable row level security;
alter table public.veil_purchases enable row level security;

drop policy if exists "veil_progress_select_own" on public.veil_player_progress;
drop policy if exists "veil_progress_upsert_own" on public.veil_player_progress;
drop policy if exists "veil_progress_update_own" on public.veil_player_progress;
drop policy if exists "veil_purchases_select_own" on public.veil_purchases;

create policy "veil_progress_select_own"
  on public.veil_player_progress for select
  using (auth.uid() = user_id);

create policy "veil_progress_upsert_own"
  on public.veil_player_progress for insert
  with check (auth.uid() = user_id);

create policy "veil_progress_update_own"
  on public.veil_player_progress for update
  using (auth.uid() = user_id);

create policy "veil_purchases_select_own"
  on public.veil_purchases for select
  using (auth.uid() = user_id);

-- Notes for judges:
-- shop / theater unlocks settle via Base Sepolia ETH → house wallet, verified by /api/shop/purchase
-- megapot_credits still earned in combat and claimed via /api/megapot/claim
-- each Google user_id owns one progress row (+ optional linked_metamask)
