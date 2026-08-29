# Blueline — work outstanding

Ordered by value, not by effort. Items in group 1 are cheap because the data already exists;
group 2 is what I would want fixed before this is exposed to the internet.

Status of each item is one of: `todo`, `in progress`, `done`.

---

## 1. Data already collected but unreachable

These need no new ingestion. The rows are in the database and the query layer can already reach
them; what is missing is a way to ask for them.

### 1.1 Surface goalie stats — `todo`

5,575 goalie game lines are stored and never read. `GoalieGameStat` appears nowhere in
`Blueline.Data/Queries` or `Blueline.Web`.

- Add a `StatDefinition.Goalie` array in `src/Blueline.Core/Dtos/StatDefinition.cs`:
  save percentage, saves, goals against, shots against, and a goals-against average.
- Add `GetGoalieTrendAsync` and goalie leaders to `StatsQueryService`. Save percentage is an
  average, not a sum, so it cannot reuse the cumulative fold as-is — a cumulative
  saves-over-shots ratio is the meaningful "trend" line, not a running total.
- `GoalieGameStat.SavePctg` already returns null when a goalie faced no shots. Keep that
  distinction; a backup who never faced a shot must not read as 0.000.
- Route the player page to a goalie view when `Player.Position == "G"`, or add `/goalies`.
- Add `/api/goalies/{id}/trend` and extend `/api/leaders` to accept goalie stats.

### 1.2 Make playoff games viewable — `todo`

All 82 playoff games are ingested. Every query filters them out — `GameTypes.Regular` is
hardcoded in three places in `StatsQueryService.cs` (`RegularSeasonSkaterStats`,
`GetTeamsAsync`, `GetTeamTrendAsync`).

- Thread a game-type selector through the query methods and add a Regular / Playoffs (/ Both)
  control to the pages and a `gameType` query parameter to the API.
- **Fix the resulting inconsistency while here:** `GetSeasonsAsync` counts *all* game types, so
  the Data page reports 1,394 games while every leaderboard covers only 1,312. Either report the
  split or filter it to match.
- Playoff series are not evenly sized, so "game number" on the x axis means something different
  than it does in the regular season. Decide whether playoff trends chart by game number or date.

### 1.3 Extend multi-player comparison — `todo`

Overlaying several players' stats on one chart **is already built and working** on the player
page (`src/Blueline.Web/Components/Pages/PlayerTrend.razor`): pick players from "Compare with",
they appear as removable coloured chips, and their series are drawn on the same axes in both
cumulative and per-game views. Shorter seasons are padded with nulls so every line stays aligned
by game number, and the per-game view drops the raw bars once more than one player is shown,
since overlapping bars are unreadable.

What is missing is reach, not the mechanism:

- **Only the top 40 scorers can be selected.** Both calls that build the candidate list are
  `SearchPlayersAsync(_seasonId, null, 40)`, so 1,023 of the league's 1,063 players are silently
  absent from the picker — every depth forward, most defencemen, every goalie. This is the real
  limitation and the one worth fixing first. `SearchPlayersAsync` already accepts a search term;
  the picker needs to be a search box rather than a fixed dropdown.
- **Capped at 3 comparisons** (4 lines total), matching the 4-colour palette in
  `ChartSpec.cs`. Raising the cap means extending `ChartPalette.Series` with colours that stay
  distinguishable on the dark background — past roughly 6 lines a chart stops being readable, so
  this should be a deliberate ceiling rather than unbounded.
- **Same season only.** Comparisons re-fetch using the page's `_seasonId`, so a player cannot be
  compared against their own earlier season. Career-arc comparison ("McDavid at 24 vs at 22") is
  a different and arguably more interesting question, and needs the compared series to carry
  their own season.
- **Teams cannot be compared at all.** `TeamTrend.razor` has no comparison UI, though
  `GetTeamTrendAsync` would support it unchanged — two clubs' points pace on one chart is the
  natural way to read a playoff race.
- **The API cannot express it.** `/api/players/{id}/trend` returns one subject, so an external
  consumer has to make N calls and align the series itself. A `?compare=id,id` parameter, or an
  endpoint accepting several ids, would let the API answer the same question the UI does.

---

## 2. Robustness — before deploying

### 2.1 Enable SQLite WAL mode — `todo`

Nothing sets the journal mode, so SQLite uses the default rollback journal, where a writer
blocks readers. The daily ingestion job writes on a background thread while Blazor circuits read
on request threads — the exact shape that produces intermittent `database is locked` errors under
any real traffic.

- Execute `PRAGMA journal_mode=WAL;` on startup (and set a `busy_timeout`) in
  `AddBluelineCore` in `src/Blueline.Ingestion/ServiceCollectionExtensions.cs`.
- This has not caused a visible failure yet only because the site has had one user at a time.

### 2.2 Retry transient API failures, and record games that fail — `todo`

`NhlApiClient` makes one attempt per request and returns null on failure. In
`IngestGamesAsync`, a null box score hits `if (box is null) continue;` — the game is skipped
silently, never counted, and never retried. A backfill fires ~1,400 requests, so a single
network blip permanently loses a game with nothing recording which one.

- Add a retry with backoff to the `AddHttpClient<NhlApiClient>` registration
  (`AddStandardResilienceHandler`, or Polly directly).
- Record failed game ids on the `IngestionRun` so a failure is visible rather than silent.

### 2.3 Add a reconcile command to close gaps — `todo`

The daily job only looks back `LookbackDays` (3). If the app is down for longer — a free host
sleeping on inactivity makes this likely — those games are missed permanently, and nothing
detects it.

- Add `reconcile <seasonId>` to `src/Blueline.Cli/Program.cs`: diff the league's schedule for
  the season against stored game ids and ingest whatever is missing.
- This also repairs anything lost to 2.2.

### 2.4 Add a health endpoint — `todo`

No `AddHealthChecks`. Most hosts want a liveness URL and will restart or mark the app unhealthy
without one. `/health` returning database reachability and last successful ingestion time.

---

## 3. Deployment

### 3.1 Dockerfile and deployment notes — `todo`

No `Dockerfile` or `.dockerignore` yet.

- Multi-stage build on the .NET 10 SDK / ASP.NET runtime images.
- Set `BLUELINE_DATA_DIR` to a mounted volume path — the resolver in
  `src/Blueline.Data/BluelineDbPath.cs` already handles this, so no code change is needed.
- Blazor Server holds a WebSocket per visitor: the host must support long-lived connections, and
  if it ever scales past one instance it needs sticky sessions or a Redis backplane.
- Add forwarded-headers handling for running behind a proxy, and confirm
  `UseHttpsRedirection` behaves correctly when TLS terminates upstream (it currently logs
  "Failed to determine the https port" under the http-only profile).

### 3.2 Decide and set up the host — `todo`

Depends on a decision — see `questions.md`. Free tiers shift, so verify current terms before
committing.

---

## 4. Data quality and coverage

### 4.1 Fix the 30 abbreviated player names — `todo`

30 of 1,063 players still read as `D. Tarasov`. `EnrichPlayerNamesAsync` reads each club's
end-of-season roster, which misses anyone who appeared briefly and was gone by April. Their
stats are correct; only the display name is short.

- Fall back to `/v1/player/{id}/landing` for players still matching `NeedsRealName` after the
  roster pass. That is one request per unnamed player — 30, not 1,063.

### 4.2 Load more seasons — `todo`

Schema and UI are already multi-season; a season picker is on every page. This is just running
`backfill` per season. Scope is a question — see `questions.md`.

### 4.3 Confirm the daily job on a live game day — `todo`

It has been exercised against past dates and runs on startup, but has never fired during an
actual season. 2026-27 opens **2026-09-29**. Watch the first few days and confirm new games
appear without a manual run.

---

## 5. Polish

- **Caching.** `/api/leaders` aggregates ~50,000 rows on every page load. Completed seasons never
  change, so a memory cache keyed on season + stat would make this free.
- **Mobile.** The CSS has responsive breakpoints but has only been checked at desktop width. The
  chart, the controls row, and the wide tables all need a look on a phone.
- **Accessibility.** Colour alone currently distinguishes compared players; the chips carry a
  colour swatch but no other marker. Chart tooltips are not keyboard reachable.
- **Team colours in charts.** The palette is four fixed colours. Team pages could use each club's
  own colour.
- **Empty and error states.** Pages handle "no data" but not "the query failed".
