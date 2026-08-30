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

### 1.4 Offer a date x axis as well as game number — `done`

Every trend page now has a Game / Date toggle. Both axes are kept, because they answer different
questions: game number for per-game pace and for comparing players whose games played differ,
date for when someone was hot and what they missed.

It is a real time scale, not date-formatted category labels — the distinction the item warned
about. Verified numerically rather than by eye: on Draisaitl's season a 21-day absence renders 90
pixels wide against 9 pixels for an ordinary two-day gap, a ratio matching the elapsed time. On
the game-number axis that layoff was invisible, the line climbing as though he had played
throughout.

- `chartjs-adapter-date-fns` is vendored beside `chart.umd.js`, so there is still no CDN
  dependency at runtime.
- `TrendDatasets.From` builds whichever shape the axis needs: padded and index-aligned for the
  category axis, or carrying its own dates and unpadded for the time axis, where padding would
  invent points with no date and where the gaps padding exists to hide are the entire point.

**A bug found while verifying, worth recording.** Passing the category labels alongside a time
scale made Chart.js parse the game numbers "1".."82" as dates, producing an axis running from the
year 1000 to 6500 and collapsing every point onto one pixel. The labels are now withheld when the
time axis is in use. It would have shipped looking broken rather than subtly wrong, but only
because the spacing was measured — a glance at the chart shape would not have caught the cause.

Still open: the rolling window remains "N games", so across a layoff a 10-game average spans far
more calendar time than its width on the date axis suggests. A days-based window would be a
separate decision.

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

### 2.5 Browser-driven UI tests — `done`

24 Playwright tests in `tests/Blueline.UiTests`, covering page loads, the goalie redirect, empty
states, every trend control, and comparison. They run in about 3 seconds.

**They earned their keep immediately**, finding a bug the unit suite could not: a player with no
games in the selected season or scope rendered stat tiles reading zero above a blank chart
instead of an empty state. `GetPlayerTrendAsync` returns an *empty series* rather than null for a
known player, and `PlayerTrend` only checked for null — the goalie and team pages already handled
both. Fixed.

How the harness works:

- **The site runs as a real process**, not an in-memory test server. Blazor Server needs a genuine
  socket for its circuit, and whether the browser connects to it is the entire point.
- **The database is built by the fixture**, through migrations rather than `EnsureCreated` — the
  app runs `MigrateAsync` at startup and would otherwise try to create tables that already exist
  and fail to boot. Ingestion is switched off, so no test touches the league's API and none
  depends on whichever season a developer happens to have loaded.
- **Console errors fail the test.** A page that renders while throwing underneath is not passing.
- **The charts are read through the live Chart.js instance**, since a canvas offers no DOM. That
  covers dataset counts, labels and scale types — the wiring — while the arithmetic stays in
  `TrendCalculationTests` where it belongs.

Playwright over Selenium, as the item argued: the assertions poll, which suits Blazor Server where
every interaction is a round trip and nothing is synchronous. No explicit sleeps were needed.

Note for CI: `playwright.ps1 install chromium` must run once, and it downloads roughly 300 MB.

---

## 3. Deployment

### 3.1 Dockerfile and deployment notes — `done`

Multi-stage build, a `.dockerignore`, and a `docker-compose.yml` that demonstrates the volume.
Forwarded-headers support added to the app, opt-in via `Blueline:UseForwardedHeaders`, since
trusting those headers with nothing in front would let a caller spoof the client address.

**Not built or run.** Docker's daemon is not available on this machine, so the image is unverified.
What was checked is the part most likely to break: both projects publish cleanly into one
directory, with `wwwroot` and the static asset manifest intact, and the app boots and serves with
forwarded headers enabled.

Two decisions worth recording:

- **The image carries the CLI as well as the site**, published side by side. One image can
  therefore seed, reconcile or report status against the very same volume the site is using,
  without a second image or an SDK in production.
- **`HEALTHCHECK` targets `/health`, not `/health/ready`.** This is the 2.4 split earning its
  keep: a first run spends several minutes loading a season, readiness correctly says "not yet",
  and probing that here would kill the container mid-load and start it over forever.

How data reaches the database is documented in the README, with three routes — self-seed on first
run, an explicit `backfill` through the CLI, or restoring a `blueline.db` — and the warning that
outranks the choice: seeding fires on an *empty database*, not a first-run flag, so storage that
does not survive a restart re-ingests ~1,400 games on every boot while merely looking slow.

Still to confirm once a daemon is available: that the image builds, that a non-root `app` user can
write to a mounted volume, and the behaviour of `UseHttpsRedirection` behind a TLS-terminating
proxy.

### 3.4 Ship seasons as installable archives — `done`

`export` and `import` in the CLI, and `seed/20252026.blueline.gz` committed alongside the code:
61,035 rows in 941 KB, 18% the size of the database it came from. The image carries it, and an
empty database now loads it in seconds rather than spending several minutes and ~1,500 requests
rebuilding data that has not changed since the season ended. Ingestion remains the fallback when
no archive is present.

- **Gzipped JSON Lines, not a copy of the SQLite file.** A file copy would be smaller and simpler
  but would tie the archive to SQLite, and the connection string is deliberately overridable so a
  deployment can change provider. Rows go through the model, so an archive taken from SQLite
  loads into anything EF Core supports. Line-per-record keeps both ends streaming.
- **A dacpac was the original suggestion**, but that is SQL Server specific and schema-oriented;
  the schema here already travels as EF migrations. What was missing was the data.
- **The import is one transaction.** Found by measuring rather than reasoning: readiness first
  went healthy three seconds into a boot, while the site served leaderboards showing Jack Eichel
  on 16 points. Rows arrive in dependency order, so games land before the stat lines that
  reference them and anything computed in between is wrong rather than merely thin. Atomic
  import also means a failure leaves nothing behind, instead of stranding a partial season that
  the empty-database check would take for real data.

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
