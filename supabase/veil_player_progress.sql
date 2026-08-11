-- Veil War progress (run in Supabase SQL editor)
create table if not exists public.veil_player_progress (
  user_id uuid primary key references auth.users(id) on delete cascade,
  megapot_credits int not null default 0,
  tickets_minted int not null default 0,
  play_address text,
  high_score int not null default 0,
  updated_at timestamptz not null default now()
);

alter table public.veil_player_progress enable row level security;

create policy "veil_progress_select_own"
  on public.veil_player_progress for select
  using (auth.uid() = user_id);

create policy "veil_progress_upsert_own"
  on public.veil_player_progress for insert
  with check (auth.uid() = user_id);

create policy "veil_progress_update_own"
  on public.veil_player_progress for update
  using (auth.uid() = user_id);
