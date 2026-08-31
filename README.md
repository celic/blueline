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

- **A home page of runs, not totals** — the players furthest above their own usual rate over a
  trailing window, each drawn as a sparkline and linking through to the trend behind it. It says
  when its windows are old, so a finished season is not presented as current form.
- **Season leaders** for points, goals, hits, blocks, time on ice and more.
- **Player trends** — cumulative totals, or per-game values with a rolling average over a window
  you choose, counted either in games or in days. Search any player in the league and overlay up
  to five of them on one chart. Teams and goalies compare the same way.
- **Goalie trends** — save percentage, goals-against average, saves, shots and goals against.
  Rates are combined by weighting each appearance by the shots faced, never by averaging the
  per-game percentages.
- **Team trends** — how a club banked standings points, scored and conceded across the year.
- **A date or game-number x axis.** Game number compares like for like; the date axis spaces
  games by when they were actually played, so an injury layoff or the gap between playoff rounds
  shows up instead of being flattened away.
- **A games filter** on every page — regular season or playoffs, never merged, because the two
  are scored differently and a combined total is a figure nobody quotes. The default is
  configurable via `Display:DefaultGameScope`, and the API takes the same choice as `?scope=`.
- **A JSON API** at `/api` serving the same data.
- **Daily ingestion** that pulls new games and re-reads recent dates so the league's
  after-the-fact stat corrections are picked up. Run on a schedule from outside the site, which
  has no endpoint or button that triggers collection.

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
| `export <seasonId> [file]` | Write a season to a portable archive |
| `import <file>` | Load a season archive, without any API calls |
| `status` | Show what is stored and how the last run went |

Tests:

```bash
dotnet test tests/Blueline.Tests
```

Browser tests drive the real site through Playwright. They need the browser downloaded once:

```bash
tests/Blueline.UiTests/bin/Debug/net10.0/playwright.ps1 install chromium
```

## Running it in Docker

```bash
docker compose up --build
```

The site is then on `http://localhost:8080`.

The image publishes the site and the CLI side by side, so one image runs either. That is what
lets you seed, reconcile or inspect the very same volume the site is using:

```bash
docker compose run --rm --entrypoint dotnet blueline Blueline.Cli.dll status
```

### How data gets into the database

**From season archives — no API calls, a few seconds each.** An archive is a compressed export of
one finished season, roughly 1 MB for around 60,000 rows. On startup against an empty database the
app loads every archive it finds in `seed/`, so a deployment can carry several past seasons.
Re-ingesting the same data from the league would take minutes per season and about 1,500 requests.

**Archives are not in this repository and are not published anywhere.** They are collected data,
needed only where the site actually runs, and they are not ours to redistribute. Build them from a
database you have already filled:

```bash
dotnet run --project src/Blueline.Cli -- backfill 20252026
pwsh ./scripts/build-seasons.ps1     # or: powershell -File ./scripts/build-seasons.ps1
```

`seed/manifest.json` records what was built and each checksum, so an archive can be verified after
being moved. Getting them to the deployment is a deliberate manual step: copy them into `seed/`
before building the image so they are baked in, or mount them into the container's volume and
import them there. With no archives present the image still builds and the app falls back to
ingesting a season from the league API on first run.

Each import runs in a single transaction, so the site serves nothing from that season until all of
it has landed. Rows arrive in dependency order, so a partly applied import is not merely
incomplete but wrong — leaderboards built from games whose stat lines have not arrived yet report
the wrong leaders. It also means a failure leaves no trace instead of stranding a partial season
that the empty-database check would mistake for real data. One unreadable archive costs only its
own season, and seasons therefore appear one at a time on a first boot.

Other routes:

| What | How |
| --- | --- |
| Load an archive already on the volume | `docker compose run --rm --entrypoint dotnet blueline Blueline.Cli.dll import /data/20252026.blueline.gz` |
| Export a single season | `dotnet run --project src/Blueline.Cli -- export 20252026 seed/20252026.blueline.gz` |
| Ingest from the league instead | Set `Ingestion__SeedArchiveDirectory=""` |
| Load nothing at all | Set `Ingestion__SeedSeasonId=0` |

Archives are portable rather than copies of the database file: rows go through the model, so one
taken from SQLite loads into any provider EF Core supports. Importing is idempotent, so running it
twice, or over a season already present, converges instead of duplicating.

**The setting that still matters most: the volume must genuinely persist.** Seeding is triggered by
finding an *empty database*, not by a first-run flag. Archives make a repeat far cheaper than it
was — seconds and no requests — but storage thrown away on restart still means rebuilding on every
boot. Restart once and check `status` reports the same games before trusting anything else.

### Keeping it current

**The site does not collect data on a schedule.** Nothing in the web app triggers ingestion, and
there is no endpoint that does — collection is a scheduled job run against the database, so it can
be watched and retried without a request pipeline in front of it.

Run one from outside the container, against the same volume, once a morning:

```bash
docker compose exec blueline dotnet Blueline.Cli.dll daily
```

On Linux, a crontab entry at 07:00:

```bash
0 7 * * * cd /srv/blueline && docker compose exec -T blueline dotnet Blueline.Cli.dll daily >> /var/log/blueline.log 2>&1
```

On Windows, the same thing as a scheduled task:

```bash
schtasks /create /tn Blueline /sc daily /st 07:00 /tr "docker compose -f C:\srvlueline\docker-compose.yml exec -T blueline dotnet Blueline.Cli.dll daily"
```

Each run re-reads the last few days, not just yesterday, so a stat correction the league makes
after the fact is picked up. Running it twice does no harm — every write is an upsert.

A deployment that would rather not run a scheduler can set `Ingestion__DailyJobEnabled=true` and
have the site do it in-process instead. Seeding an empty database happens either way.

**If nothing is scheduled, the data quietly stops moving.** The site keeps serving what it has and
looks perfectly healthy, so the last-run time on the Data page — which says which arrangement is in
force — is the thing to check when the numbers look stale.

If the schedule was missed for longer than the lookback window, fill the gap:

```bash
docker compose run --rm --entrypoint dotnet blueline Blueline.Cli.dll reconcile 20252026
```

### Deployment notes

| Setting | Why |
| --- | --- |
| `BLUELINE_DATA_DIR` | Already `/data` in the image; point the volume there |
| `Blueline__UseForwardedHeaders` | Set `true` behind a proxy that terminates TLS, so the app sees the original scheme and client address. Off by default, since trusting those headers with nothing in front would let a caller spoof them |
| `Ingestion__SeedSeasonId` | `0` disables self-seeding |
| `Ingestion__DailyJobEnabled` | `true` moves the daily schedule into the site, instead of a job outside it |

Blazor Server holds a WebSocket per visitor, so the host must allow long-lived connections. Past a
single instance it would need sticky sessions or a Redis backplane; one instance is assumed here.

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

A rolling window is counted either in games or in days, and both are kept because they answer
different questions. "Last 10 games" is right for per-game pace and for comparing players who have
played different numbers of games. "Last 14 days" is right for who is hot now — and it is the only
fair way to ask it of goalies, since a fortnight is four starts for one and eight for another. A
games window ignores the calendar entirely: across a three-week injury it still spans ten games,
so on the date axis its width bears no relation to the time it covers.

Either way the average is per game — a days window divides by the games that fell inside it, not
by the number of days — and it is reported only once a full window sits behind it, since a partial
one makes the opening weeks look far more volatile than they were. For a days window "full" is a
property of the calendar: the season has to reach back the whole period, however few games fell in
between.

### Streaks

`/api/streaks` answers a different question from `/api/leaders`: not who is best, but who is
furthest above their own usual rate over a trailing window. Ranking by raw total would put the same
handful of stars on the board for weeks, and those already have a leaderboard.

Lift is a multiple for a counting stat — 3.6 means three and a half times the player's season rate
— and a difference for a rate, where a goalie at .965 against a .907 baseline reads as +.058. The
baseline deliberately includes the window: excluding it would leave a player whose only production
came in the window with nothing to divide by.

Departure alone is not enough, because one good night against a season of nothing is an enormous
multiple of nothing. Each board therefore applies a floor relative to its own leader — 40% of the
best run in that window — rather than a threshold invented per stat. Goalies get the same treatment
on workload, which is what stops a backup's quiet .1000 over eighteen shots topping the board. A
days window also needs at least three appearances behind it, and any subject needs ten games of
season before they are said to have a rate at all.

Windows end on the newest game stored rather than on today, so the boards still answer in the
off-season — they describe the closing weeks of the season played. The home page classifies that
silence: under three days is current, up to three weeks is a break or a stalled collector, and
beyond that the season is over. It checks whether ingestion is itself current before blaming the
schedule, because the games alone cannot tell those apart.

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
| `Ingestion:DailyJobEnabled` | `false` | Have the site schedule its own daily pass, instead of a job outside it |
| `Ingestion:DailyRunTimeUtc` | `11:00` | When the daily pass runs (≈07:00 Eastern, after every game is scored) |
| `Ingestion:LookbackDays` | `3` | How many days back each run re-reads, to catch stat corrections |
| `Ingestion:RunOnStartup` | `true` | Do a pass at startup instead of waiting. Only applies when `DailyJobEnabled` is on |
| `Ingestion:SeedSeasonId` | `20252026` | Season loaded when the database is empty; `0` disables |
| `Display:DefaultGameScope` | `RegularSeason` | Games counted before the reader chooses: `RegularSeason` or `Playoffs` |
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
| `GET /api/players/trends?ids=1,2,3` | Several skaters at once, for comparison (also `/goalies/trends`, `/teams/trends`) |
| `GET /api/goalies?season=&search=&stat=&take=` | Goalie leaders, with a minutes qualification on rates |
| `GET /api/goalies/{id}/trend?season=&stat=&window=` | A goalie's game-by-game trend |
| `GET /api/teams?season=` | Standings |
| `GET /api/teams/{id}/trend?season=&stat=&window=` | A team's pace |
| `GET /api/leaders?season=&stat=&take=` | Season leaders |
| `GET /api/streaks?season=&stat=&window=&take=` | Skaters furthest above their own season rate |
| `GET /api/streaks/goalies?season=&window=&take=` | Goalies furthest above their own save percentage |
| `GET /api/ingestion/status` | What is stored and how the last run went |
| `GET /health` | Liveness: is the database reachable |
| `GET /health/ready` | Readiness: is there data to serve, and is ingestion keeping up |

Trend endpoints take `?window=` as either games or days: `10` and `10g` are ten games, `14d` is
fourteen days. Games cap at 41 and days at 90, and anything unrecognised falls back to ten games.
The response reports which unit it used as `rollingWindowUnit`.

Every stat endpoint also takes `?scope=RegularSeason|Playoffs`; an unrecognised value falls
back to the regular season rather than erroring, so a stale bookmark still renders — including
`?scope=All`, which earlier builds accepted.

`season` defaults to the most recent season stored. An OpenAPI document is served at
`/openapi/v1.json` in development.

## Notes

Chart.js is vendored at `src/Blueline.Web/wwwroot/lib/chart.umd.js` (v4.4.7, MIT), together with
`chartjs-adapter-date-fns` (v3.0.0, MIT) for the date axis, rather than loaded from a CDN — so the
site has no third-party runtime dependency and works offline.
