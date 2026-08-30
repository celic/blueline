# Blueline — work outstanding

Ordered by value, not by effort. Items in group 1 are cheap because the data already exists;
group 2 is what I would want fixed before this is exposed to the internet.

Status of each item is one of: `todo`, `in progress`, `done`.

---

## 1. Data already collected but unreachable

These need no new ingestion. The rows are in the database and the query layer can already reach
them; what is missing is a way to ask for them.

### 1.1 Surface goalie stats — `done`

Delivered. `/goalies` lists goalie leaders and `/goalies/{id}` charts a goalie's season, with
comparison against up to three others. `/api/goalies` and `/api/goalies/{id}/trend` expose the
same data. `/players/{id}` redirects to the goalie page when the player is a goalie.

Two things worth knowing about how it was built:

- **Rates do not reuse the counting fold.** `BuildPoints` now takes an optional
  `RateDenominator` on each row and, when present, sums numerators and denominators separately
  for both the running and the rolling figures. Averaging per-game percentages instead would
  have reported Vasilevskiy's season at .907 rather than .912 — about the gap between an average
  starter and a top-five goalie. Covered by tests in `TrendCalculationTests`.
- **A bug was found and fixed while verifying.** Goalie games played was counting games
  *dressed* rather than *played*, because a backup logs a zero-minute row for every game on the
  bench. Starters were being reported at 78-80 games instead of their actual 57-64.
  `SearchGoaliesAsync` now counts only appearances with ice time, matching the trend query.

Rate leaderboards apply a 1,500-minute qualification (`StatDefinition.RateQualificationMinutes`),
dropped automatically if nobody clears it so a part-loaded season does not show an empty table.

Not done, and deliberately: goalie **wins/losses** are not shown. A goalie's decision is not in
the box score `playerByGameStats` payload, so it would have to be inferred by joining the
appearance to the game result and working out who was in net at the end — guessable, but not
reliably, for relief appearances. Worth doing properly if you want a wins column.

### 1.2 Make playoff games viewable — `done`

Delivered. A `GameScope` (`RegularSeason` / `Playoffs` / `All`) threads through every query, the
API takes it as `?scope=`, and a shared `ScopePicker` sits on all seven pages. The default is
configurable via `Display:DefaultGameScope` and stays `RegularSeason`, because that is what a
published stat line means — nobody's "42 goals" silently includes playoff goals.
`GetSeasonsAsync` now reports the regular/playoff split, so the Data page no longer shows 1,394
games beside leaderboards covering 1,312.

**Two data bugs surfaced once the playoffs became visible**, both fixed in ingestion and repaired
by re-running the backfill:

- **22 playoff overtime losses were recorded as `OTL` with a standings point each.** There is no
  loser point in the playoffs; an overtime loss is simply a loss.
- **Playoff wins were awarding 2 standings points.** The playoffs award none at all, so a
  combined view was overstating clubs' standings totals — Colorado read as 137 points rather
  than their actual 121.

`TeamGameStat.Points` now means strictly "standings points earned", zero for every playoff game.
The pages hide the OTL/PTS/PTS% columns whenever the scope includes playoff games, and the team
trend page explains why a points line flattens if you chart points across the playoffs.

Still open, deliberately: playoff trends are plotted by game number like everything else, which
hides the long gaps between rounds. Tracked as its own item now — see 1.4.

### 1.3 Extend multi-player comparison — `done`

Four of the five gaps are closed.

- **The whole league is reachable now.** The fixed dropdown is gone, replaced by a shared
  `ComparePicker` that searches. This was the real defect: the old list offered the top 40 scorers,
  so 1,023 players could not be compared at all and nothing on screen said so. Verified by
  comparing McDavid against Yakov Trenin, a 23-point checker who could never have appeared in it.
- **The cap is 5 comparisons, six series in all**, matching an extended palette. Six is a
  deliberate ceiling: past roughly that many lines a trend chart stops being readable however good
  the colours are.
- **Teams can be compared**, which they could not at all before — `GetTeamTrendAsync` supported it
  unchanged, only the UI was missing. Colorado against Carolina reads their points pace side by
  side.
- **The API can express it**: `/api/players/trends?ids=`, `/api/goalies/trends` and
  `/api/teams/trends`. These are separate endpoints rather than a parameter on the single-subject
  ones, so the response is always an array and existing consumers are untouched. Ids are
  de-duplicated, unparseable entries dropped rather than failing the request, and the list capped
  at what a chart can carry.

Still open: **cross-season comparison**. Comparisons re-fetch using the page's season, so a player
cannot be charted against their own earlier year. "McDavid at 24 against McDavid at 22" is a
genuinely different question — each series needs to carry its own season, and the x axis has to
decide whether it aligns by game number or by age. Worth its own item if wanted.

### 1.4 Offer a date x axis as well as game number — `todo`

Every chart plots game number on the x axis, evenly spaced. `TrendPoint.Date` is already
computed, stored and serialised to the browser, so this is presentation only — no query or
schema change.

Game number hides every gap in the calendar:

- **A player who misses six weeks injured draws an unbroken line.** Game 40 sits right beside
  game 41 as though nothing happened. For a site whose whole premise is trends over time, this
  is the most distorting case, and it has nothing to do with the playoffs.
- **Playoff series vary in length and the rounds have long gaps between them**, so a run reads
  as evenly paced when it was not.
- **In the combined scope the week between the regular season and the playoffs vanishes**, and
  the two stretches run together as though continuous.

Keep both axes rather than replacing one. They answer different questions: game number is the
honest axis for per-game pace and for comparing players whose games played differ, while a date
axis answers "when was he hot" and shows layoffs for what they are.

**The trap worth naming**, because falling into it looks like success: simply formatting the
existing category-axis labels as dates is *not* this feature. The spacing stays uniform, so every
gap above remains invisible — the chart would read as fixed while still misleading. It needs a
real time scale with proportional spacing.

Implementation notes:

- Chart.js needs a date adapter for its time scale (`chartjs-adapter-date-fns` or the Luxon
  equivalent), which means vendoring a second library beside `chart.umd.js`. That is the bulk of
  the cost and the reason this has not been done in passing.
- The data shape changes for this mode: series become `{x, y}` points carrying their own dates
  rather than sharing one label index. `ChartSpec.Labels` and the `Pad` helpers in
  `PlayerTrend.razor` and `GoalieTrend.razor` exist only to align comparison series by game
  number, and are unnecessary once each series carries its own x values.
- Decide what the rolling window means here. It stays "N games", so over a stretch containing a
  layoff a 10-game average spans a much longer calendar period than its width on the axis
  suggests. Either accept that, or offer a days-based window alongside it.
- The control belongs next to the existing View toggle on the three trend pages. No API change
  is needed — points already carry `date`, so external consumers can plot by date today.

---

## 2. Robustness — before deploying

### 2.1 SQLite connection settings — `done`

Done, but **the original diagnosis here was wrong on its main point** and the correction is worth
recording, because it changes how urgent this was.

The claim was that nothing set the journal mode, so SQLite fell back to its blocking rollback
journal. That came from grepping the source for `PRAGMA` and `journal` and finding nothing.
Measuring instead of grepping shows **EF Core's SQLite provider already enables WAL** — raw
`Microsoft.Data.Sqlite` defaults to `delete`, but the EF provider does not. So the headline risk,
readers being blocked by the ingestion job, was never real.

Two things measurement did turn up:

- **`busy_timeout` is 0 under EF.** SQLite never sleeps on a contended lock. It does not fail on
  contact, as first assumed — the provider retries at its own level until `CommandTimeout` — but
  that retrying is a busy spin rather than an efficient wait.
- **A contended write blocks for the full command timeout, 30 seconds by default.** The risk was
  never a `database is locked` error; it was a long stall. Setting `busy_timeout` does not shorten
  that — measured at ~30s either way — it only makes the waiting cheap. `CommandTimeout` is the
  knob that bounds it, and it is left at the default for now.

Severity is lower than this item originally implied: under WAL, plain reads never contend, and the
only writers are the daily job and the Data page's "Refresh now" button. The stall needs those two
to overlap.

`SqliteConnectionInterceptor` now applies `journal_mode=WAL` (explicit rather than trusting a
provider default that could shift), `busy_timeout` and `synchronous=NORMAL` to every SQLite
connection, skipping non-SQLite connections so the documented Postgres override still works.
Covered by `SqliteConcurrencyTests`, including a test that fails if the provider ever stops
defaulting to WAL.

Still open: whether to lower `CommandTimeout` so an overlapping manual refresh fails in a few
seconds rather than stalling for thirty.

### 2.2 Retry transient API failures, and record games that fail — `done`

`AddStandardResilienceHandler` now sits on the league API client, so a transient blip is retried
with backoff rather than costing a game outright. Anything still unread after those retries is
recorded on the ingestion run — `GamesFailed` plus the identifiers in `FailedGameIds` — and shown
on the Data page, instead of being skipped in silence.

Details worth keeping:

- **The client's `catch` had to widen.** The resilience pipeline throws its own types on a
  spent retry or an open circuit, and neither is an `HttpRequestException`. Leaving the old
  narrow catch in place would have let one of them escape and abandon an entire backfill —
  adding resilience would have made things worse. Cancellation still propagates.
- **A run with failures is still `Succeeded`.** The rest of the night is worth keeping, and a
  partial shortfall is not a run-level error. The count is what makes it visible.
- **Responses are zipped back to their ids** so a null is attributed to the game it belongs to,
  rather than by position alone.
- **`FailedGameIds` is truncated** at 50 ids so one bad run cannot write an unbounded string;
  `GamesFailed` stays exact.
- Polly logs every attempt at information level, including successes, so it is filtered to
  warnings alongside the existing HTTP and EF filters.

Games recorded here are exactly what 2.3 should re-fetch.

### 2.3 Add a reconcile command to close gaps — `done`

`reconcile <seasonId>` diffs the league's schedule for a season against what is stored and
ingests whatever is absent. It is the safety net under the daily job, whose three-day lookback
would never notice a longer outage — a free host asleep, a machine off over a weekend — and it
also picks up games an earlier run recorded as failed under 2.2.

- **Cheap when nothing is wrong.** The schedule walk is 33 requests and no box score is fetched
  unless something is genuinely missing. Verified against the live 2025-26 season: reported all
  1,394 games present and fetched nothing.
- **It also re-reads games stored with no stat lines**, not just absent ones. A half-applied box
  score leaves a game row that looks stored while charting as though nobody played, which no
  count of games would ever reveal.
- Preseason is excluded, so a schedule full of September exhibitions is not chased.
- Runs are recorded under their own `reconcile` kind, and a no-op run is still closed out.

Verified end to end by deleting a real game from the database: reconcile reported "1 of 1394
games need ingesting" and restored the game with all 36 skater lines, 4 goalie lines and 2 team
lines.

### 2.4 Add a health endpoint — `done`

Two endpoints rather than one, because the distinction prevents a specific disaster.

- **`/health`** — liveness. Only asks whether the database can be reached. Answers "is this
  process worth keeping".
- **`/health/ready`** — readiness. Also asks whether there is anything to serve and whether
  ingestion is keeping up. Answers "is it worth sending traffic here yet".

**Why they are separate.** A fresh deployment spends several minutes seeding its first season, so
a single endpoint reporting "no data" would be read as unhealthy, the host would restart the
container, and the seed would begin again — forever, while looking merely slow. Liveness stays
healthy throughout.

A failed ingestion run reports **Degraded**, not Unhealthy, because the site still serves
everything already stored. So does a run that could not read some games, and that report names
`reconcile` as the remedy rather than leaving someone to work it out. Degraded still returns 200,
so a probe will not act on it while a human can still see it.

The response is JSON per check with its description and data — games stored, last run kind,
status, completion time and failed count — since the framework default returns the bare word
"Healthy", which is enough for a probe and useless to a person.

Verified against the running app: both return 200 with the expected detail.

One thing to watch when 3.1 lands: `UseHttpsRedirection` runs before endpoint routing, so it
would redirect a plain-HTTP probe. In a container with TLS terminating upstream no HTTPS port is
configured and it does not redirect, which is why this works today — but a host that does
configure one needs checking.

### 2.5 Browser-driven UI tests — `todo`

Ranks below the rest of this group — nothing here blocks a deployment — but it covers the one
layer 224 unit tests cannot reach.

Everything currently tested stops at the query service. Nothing exercises the Blazor circuit
actually connecting, the JS interop that draws a chart, or a control re-querying and repainting.
Those have broken in practice during this project: a comparison dropdown kept a stale selection,
and a chart rendered against an untranslatable query that only surfaced as a runtime error in the
browser. Both were caught by hand.

Worth covering:

- A page loads and its circuit connects, with no console errors.
- Changing the stat, season or games filter re-queries and updates both the summary tiles and the
  table.
- Adding and removing a comparison updates the chips and the chart, and the picker resets.
- `/players/{id}` for a goalie redirects to `/goalies/{id}`.
- Empty states render when a season has no data.

**Tooling.** Selenium is the familiar name, but Playwright for .NET is the better fit here: it
auto-waits on elements rather than needing explicit waits, which matters for Blazor Server where
every interaction is a round trip over a WebSocket and nothing is synchronous. It also drives
headless cleanly for CI and can read console errors directly. Selenium would work; it will simply
need more sleeping and produce more flakes.

**The honest limitation: the charts are a `<canvas>`.** There is no DOM to assert against, so a
browser test can confirm a chart was created and inspect the data through the Chart.js instance
in JavaScript, but it cannot see whether the picture is right without screenshot comparison —
which is brittle and probably not worth it. Chart *maths* is already covered properly by
`TrendCalculationTests`; the browser layer should test the wiring, not the arithmetic.

**Fixture shape.** These need the app running against a database in a known state. A
`WebApplicationFactory` with `BLUELINE_DATA_DIR` pointed at a temporary directory and a small
seeded database is the cheapest route, and keeps them off the live league API. Do not point them
at the developer's real database — the tests would depend on whatever season happens to be
loaded.

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

### 3.3 Write the deployment runbook — `todo`

3.1 produces the image and 3.2 picks the host; this is the document for actually running it and
keeping it running. Nothing of the sort exists, so today the only person who could deploy this is
someone who has read the source.

It needs to cover:

- **First deploy.** Provision the volume, set `BLUELINE_DATA_DIR` to it, start the container. On
  an empty database the app seeds a whole season by itself, which takes several minutes of
  requests before the site has anything to show.
- **The trap that ruins the free-tier story.** Seeding triggers on an *empty* database. If the
  volume is not genuinely persistent — an ephemeral container filesystem, a free tier that resets
  disk on redeploy — every restart re-ingests ~1,400 games. That is slow, hammers the league's
  API, and looks like the app is merely slow to start. Verifying the volume actually survives a
  restart is the single most important step, and the runbook should say so first.
- **Migrations run automatically at startup** (`MigrateAsync` in `Program.cs`). Convenient, but it
  means a failed migration is a failed boot rather than a degraded service. Say what to do when
  that happens, and note that rolling the image back does not roll the schema back.
- **Backups.** Copying `blueline.db` while the app is running is not safe on its own — with
  write-ahead logging the recent commits live in the `-wal` sidecar. Use `VACUUM INTO` or the
  SQLite backup API against a live connection. Worth stating plainly, because the naive `cp` looks
  like it works right up until it doesn't.
- **Upgrades and rollback.** Replace the image, keep the volume. Schema changes are the asymmetry.
- **What to watch.** The health endpoint from 2.4, and the ingestion status the Data page already
  surfaces — including the failed-game counts added in 2.2.
- **Recovery.** If a stretch of games is missed while the host was asleep, `reconcile` is the fix
  (2.3). The runbook should name it rather than leaving someone to rediscover it.

Worth writing only once 3.1 and 3.2 are settled, since the specifics depend on the host.

---

## 4. Data quality and coverage

### 4.1 Fix the abbreviated player names — `done`

Resolved. Anyone the club rosters cannot account for is now looked up individually through
`/v1/player/{id}/landing`, one request each, and only for players who actually need it.

**The count was wrong, and the reason mattered.** Of the 30 flagged, only 25 were genuinely
abbreviated. The other five — J.T. Miller, T.J. Tynan, A.J. Greer, J.J. Moser, J.T. Compher —
have first names that really are initialised. `NeedsRealName` matched anything ending in a
period, so it swept them up.

That was not a miscount but the engine of the recurring cost. The league's own player endpoint
returns `"J.T."` for Miller, because that *is* his name, so those five could never be satisfied
by any lookup. While one player remained outstanding the enrichment guard stayed open, and every
run re-walked all 32 club rosters to resolve nothing. Adding the individual lookup alone would
not have closed it. The check now matches a single initial only — one letter and a period.

Verified against the live database: 25 of 25 genuinely abbreviated names resolved
(`C. Petersen` is now `Cal Petersen`), the five initialised names were correctly left alone, and
a second run performed no enrichment at all — the nightly roster walk is gone.

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
