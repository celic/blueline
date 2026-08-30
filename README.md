# Blueline

A website for reading NHL statistics as **trends over a season** rather than as end-of-year totals.
Every stat is stored per game, so you can see when a player got hot, when a team's pace slipped,
and how two players' seasons compare game by game.

## Status

Working end to end and verified against the live league API. Not yet deployed anywhere.

**Loaded:** the 2025-26 season — 1,394 games (1,312 regular season, 82 playoff), 50,183 skater
stat lines, 5,575 goalie lines, 1,063 players, 32 teams. About 5 MB of SQLite.

**Built and checked in a browser:** season leaders, player trends (cumulative and per-game with a
rolling average), multi-player comparison, goalie leaders and goalie trends, team pace, the
ingestion status page, and all eleven API endpoints. 199 NUnit tests pass; the solution builds
with no warnings.

**Known gaps**, in rough order of how much they'd be missed:

- **Only one season is loaded.** The schema and UI are multi-season already — a season picker is
  on every page — so this is just a matter of running `backfill` for another year.
- **The daily job has not been observed firing on a real game day.** It was exercised against past
  dates and runs on startup, but 2026-27 does not open until 2026-09-29.

## What it does

- **Season leaders** for points, goals, hits, blocks, time on ice and more.
- **Player trends** — cumulative totals, or per-game values with a rolling average over a window
  you choose. Up to three other players can be overlaid on the same chart.
- **Goalie trends** — save percentage, goals-against average, saves, shots and goals against.
  Rates are combined by weighting each appearance by the shots faced, never by averaging the
  per-game percentages.
- **Team trends** — how a club banked standings points, scored and conceded across the year.
- **A games filter** on every page — regular season, playoffs, or both combined. The default is
  configurable via `Display:DefaultGameScope`, and the API takes the same choice as `?scope=`.
- **A JSON API** at `/api` serving the same data.
- **Daily ingestion** that pulls new games automatically and re-reads recent dates so the
  league's after-the-fact stat corrections are picked up.

## Running it

```bash
dotnet run --project src/Blueline.Web
```

The site is at `http://localhost:5084`. On first run against an empty database it seeds itself
with the season named by `Ingestion:SeedSeasonId`, which takes a few minutes; after that it only
fetches new games.

To load data by hand instead:

```bash
dotnet run --project src/Blueline.Cli -- backfill 20252026
```

Other CLI commands:

| Command | What it does |
| --- | --- |
| `backfill <seasonId>` | Load a full season, e.g. `backfill 20252026` |
| `daily [days] [date]` | Re-read the N days ending on a date (default 3 days, today) |
| `reconcile <seasonId>` | Ingest any games the league lists but the database is missing |
| `status` | Show what is stored and how the last run went |

Tests:

```bash
dotnet test
```

## Where the rest is written down

| Document | What's in it |
| --- | --- |
| [docs/plan.md](docs/plan.md) | Outstanding work, ordered by value, with what is already done and why |
| [docs/questions.md](docs/questions.md) | Open decisions, each with the default that applies if left unanswered |

## How it fits together

| Project | Role |
| --- | --- |
| `Blueline.Core` | Entities, DTOs, and the list of chartable stats |
| `Blueline.Data` | EF Core context, migrations, and the read-side query service |
| `Blueline.Ingestion` | League API client, the ingestion pipeline, and the daily background job |
| `Blueline.Web` | ASP.NET Core host: the REST API and the Blazor Server site |
| `Blueline.Cli` | Backfill and maintenance commands |

The Blazor pages call the query service directly rather than going through HTTP — same data, one
less hop. The REST API exists for outside consumers.

### Where the data comes from

The league's public web API at `api-web.nhle.com`. It is undocumented and unauthenticated, so
ingestion is written defensively: a malformed or missing response is logged and skipped rather
than failing a whole run, and a JSON parse failure is logged at error level because it means the
API's shape has moved.

A season is loaded by walking all 32 club schedules to discover game ids, then reading one box
score per game. The box score is the richest per-game source — goals, assists, shots, hits,
blocks, giveaways, takeaways and time on ice for every skater, plus goalie lines. Box scores
abbreviate names to `D. Tarasov`, so a second pass over each club's season roster fills in real
names and headshots. Anyone the rosters miss — a call-up, an emergency backup, a deadline
departure — is then looked up individually, one request per player.

Every write is an upsert keyed on the league's own ids, so backfills, daily runs and manual
re-runs all converge on the same rows.

### How trends are computed

Per-game rows are the only thing stored; cumulative totals and rolling averages are derived at
read time. For a single player or team that means pulling ~82 rows and folding over them in
memory, which is cheaper than expressing window functions through the ORM and keeps the maths
identical across database providers. Season-wide aggregates, which touch tens of thousands of
rows, stay in SQL.

A rolling average is reported only once a full window of games sits behind it — a partial window
makes the opening weeks look far more volatile than they were.

Standings points are a regular-season construct. Playoff games award none — not for a win, and
not for an overtime loss, since the playoffs have no loser point. A combined view therefore shows
a club's real standings total rather than an inflated one, and the pages hide the points columns
whenever the chosen scope includes playoff games.

Rate stats — save percentage and goals-against average — accumulate differently from counting
stats. Both the running and the rolling figures sum numerators and denominators separately and
divide at the end, so a 45-shot night counts for more than a 10-shot night. Averaging the
per-game percentages instead would have put Vasilevskiy's 2025-26 save percentage at .907 rather
than its true .912, which is roughly the gap between an average starter and a top-five goalie.

## Configuration

`src/Blueline.Web/appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Ingestion:DailyJobEnabled` | `true` | Run the background job at all |
| `Ingestion:DailyRunTimeUtc` | `11:00` | When the daily pass runs (≈07:00 Eastern, after every game is scored) |
| `Ingestion:LookbackDays` | `3` | How many days back each run re-reads, to catch stat corrections |
| `Ingestion:RunOnStartup` | `true` | Do a pass at startup instead of waiting |
| `Ingestion:SeedSeasonId` | `20252026` | Season loaded when the database is empty; `0` disables |
| `Display:DefaultGameScope` | `RegularSeason` | Games counted before the reader chooses: `RegularSeason`, `Playoffs` or `All` |
| `ConnectionStrings:Blueline` | empty | Empty means "resolve a SQLite file automatically" |

### Where the database lives

SQLite, at `%LOCALAPPDATA%\Blueline\blueline.db` by default. Set `BLUELINE_DATA_DIR` to move it —
point it at a mounted volume when deploying. The web app and the CLI are separate processes, so
they resolve the path the same explicit way rather than relying on the working directory.

Every SQLite connection is configured by `SqliteConnectionInterceptor`: write-ahead logging (so
reads are never blocked by the ingestion job), a busy timeout (so a contended write waits on the
lock rather than spinning), and `synchronous=NORMAL` (safe under WAL — at worst a power cut costs
the last transaction, and every row is re-derivable from the league API anyway). Non-SQLite
connections are left alone, so the Postgres override below still works.

SQLite was chosen for deployment reasons: it needs no second service, so hosting is one container
plus one small volume. A full season is roughly 50,000 stat rows, which SQLite handles without
strain. Everything goes through EF Core, so moving to PostgreSQL is a provider swap in
`AddBluelineCore` plus a regenerated migration.

## API

| Endpoint | Returns |
| --- | --- |
| `GET /api/seasons` | Seasons stored |
| `GET /api/stats` | Stats that can be charted |
| `GET /api/players?season=&search=&take=` | Player search, ordered by points |
| `GET /api/players/{id}/trend?season=&stat=&window=` | A skater's game-by-game trend |
| `GET /api/goalies?season=&search=&stat=&take=` | Goalie leaders, with a minutes qualification on rates |
| `GET /api/goalies/{id}/trend?season=&stat=&window=` | A goalie's game-by-game trend |
| `GET /api/teams?season=` | Standings |
| `GET /api/teams/{id}/trend?season=&stat=&window=` | A team's pace |
| `GET /api/leaders?season=&stat=&take=` | Season leaders |
| `GET /api/ingestion/status` | What is stored and how the last run went |
| `GET /health` | Liveness: is the database reachable |
| `GET /health/ready` | Readiness: is there data to serve, and is ingestion keeping up |
| `POST /api/ingestion/run?days=` | Run ingestion now |

Every stat endpoint also takes `?scope=RegularSeason|Playoffs|All`; an unrecognised value falls
back to the regular season rather than erroring, so a stale bookmark still renders.

`season` defaults to the most recent season stored. An OpenAPI document is served at
`/openapi/v1.json` in development.

## Notes

Chart.js is vendored at `src/Blueline.Web/wwwroot/lib/chart.umd.js` (v4.4.7, MIT) rather than
loaded from a CDN, so the site has no third-party runtime dependency and works offline.
