# Blueline — work outstanding

Ordered by value, not by effort. Items in group 1 are cheap because the data already exists;
group 2 is what I would want fixed before this is exposed to the internet.

Status of each item is one of: `todo`, `in progress`, `done`.

**Revised 2026-08-30 against the answers in `questions.md`.** Groups 1 and 2 are now complete apart
from 1.6, and what remains is mostly one piece of work:

- **The home page is a streaks dashboard** — group 6, complete.
- **Deployment**: the image is built and verified (3.2) and the runbook is written from what was
  actually run (3.3). Only 4.3 remains, waiting for the 2026-27 season to open on 2026-09-29.

Both questions raised by the last revision are now answered. **Question 10 confirms 2.6 as built**:
the schedule runs on the same host as a separate process, never behind a reachable API. **Question
11 settles how streaks rank** — by how far a run departs from what that player normally does, not by
total, since league leaders have their own page. Only question 7, on caching, is still open.

Settled without work: box score stats only, no Corsi/Fenwick/xG (so no play-by-play ingestion and
no third-party feed); two seasons is enough (4.2 is complete, not merely done-for-now); goalies
route by position and never share a comparison with skaters, which is how 1.1 was already built;
mobile stays best-effort (group 5).

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

Delivered. A `GameScope` (`RegularSeason` / `Playoffs`, and at the time also `All`, removed later
under 1.5) threads through every query, the API takes it as `?scope=`, and a shared `ScopePicker`
sits on all seven pages. The default is
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

That left the rolling window measured in games, so across a layoff a 10-game average spanned far
more calendar time than its width on the date axis suggested. Closed by 1.6.

### 1.5 Drop the combined scope — `done`

`GameScope.All` is gone. The toggle offers regular season or playoffs, and never merges them.

Smaller than expected, because the scope was built as one enum threaded through every query rather
than as a flag repeated per page. `ScopePicker` enumerates `GameScope` instead of listing options,
so the control on all seven pages lost its third entry without being touched.

- **A bookmarked `?scope=All` still renders**, falling back to the regular season, which is what
  `Parse` already did for anything unrecognised.
- **`Parse` gained a defined-value check while it was open.** `Enum.TryParse` also accepts digits
  and hands back whatever number it is given, so `?scope=7` previously parsed as a `GameScope` no
  switch arm matches. It reached the same regular-season answer through every `_ =>` default, but
  by accident rather than by decision.
- **The tests that asserted on combined totals now assert the separation instead** — that a playoff
  game never reaches a regular-season standings total, and that each game is counted once under one
  scope only. That is the property worth pinning now.

The second half of the answer — playoff charts numbered within the playoffs — **was already how it
worked**, and now has a test. Rows are scope-filtered before `BuildPoints`, so the axis is an index
into the filtered set: game 1 in playoff scope is a club's first playoff game, not their 83rd.

**A UI test flaked while verifying this, and it was a real defect in the test.** After changing the
stat, `Changing_the_stat_re_queries_and_repaints` waited for the stat tile to update and then read
the chart directly. The tile is Blazor's own markup and updates when the query returns; the chart is
rebuilt afterwards through JS interop. So the tile was never evidence the chart had caught up, and
the read landed in the window where the old chart is destroyed and the new one does not yet exist —
`WaitForChartAsync` exists precisely for that window. It failed roughly one run in three on this
branch and not at all on `main`, which is what a timing change looks like rather than a regression:
one fewer option to render is enough to move the race. Now polled; 4 consecutive full runs pass.

### 1.6 Add a days-based rolling window — `done`

Every rolling window now carries a unit. `10 games` and `14 days` sit in the same control, because
they answer the same question measured differently and a second dropdown would imply otherwise.

- **Both average per game.** A days window divides by the games that fell inside it, not by the
  number of days, so the rolling line keeps the units the rest of the chart uses and the two window
  kinds can be compared directly.
- **"Full" means something different for each.** A games window fills once enough games sit behind
  it; a days window fills once the *season* spans the period, however few games fell in between.
  Without that, opening night would report a "14-day average" over one game — form, from a single
  data point.
- **`?window=` takes `10`, `10g` or `14d`**, and a bare number still means games, so every existing
  URL keeps its meaning. Unrecognised values fall back to ten games rather than erroring. The
  response says which unit it used, serialised by name — the enum default would have put a bare
  `1` in the JSON, which tells a reader nothing and changes meaning if the members are reordered.

**The best-stretch tile was quietly wrong for any days window, and fixing it needed new data.** It
computed the best stretch as the highest rolling average times the window size, which is only
correct when the window is counted in games. Over fourteen days a player might play four times or
eight, so multiplying by fourteen would have reported a total nobody came close to. `TrendPoint`
now carries `RollingTotal` — what the window's games actually add up to — which is null for a rate,
where totalling per-game percentages would produce a number with no unit.

Verified against the live database rather than only the seeded one: Draisaitl's best ten-game
stretch is 23 points, his best fourteen-day stretch 17, and the chart legend reads "14-day average".
The UI test pins the same distinction on seeded data where the numbers are exactly predictable —
30 against 12, across a twenty-day layoff.

20 unit tests cover the window itself, including the inclusive boundary, games sharing a date, and
the layoff case that motivated the whole item.

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
to overlap — and since 2.6 removed the button, it needs two scheduled runs to overlap, which is
harder still. Lowering `CommandTimeout` is no longer worth doing.

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

### 2.5 Browser-driven UI tests — `done`, and later found to be testing stale code

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

**A hole in the harness, found later and worth recording.** The test project had no reference to
`Blueline.Web` — the fixture launches the site from its own build output, so none was needed to
compile. The consequence is that `dotnet test tests/Blueline.UiTests` on its own started whatever
DLL was last left in `bin`, and a green run could be a green run against yesterday's code. That is
not hypothetical: a render bug that took down the circuit on every trend page survived a full pass
this way. Verified by reintroducing the bug — with a project reference added, seven tests fail; without it, all seven pass. The reference is
`ReferenceOutputAssembly="false"`, since it exists to order the build rather than to be used.

**Getting the browser was itself a trap, fixed later.** The note here used to say to run
`playwright.ps1 install chromium` once. Two things were wrong with it. Windows blocks unsigned
PowerShell scripts by default, so on this machine that command answers `running scripts is disabled
on this system` and installs nothing; and chromium and its headless shell are separate downloads,
where a headless run needs the shell. What a developer saw instead was `Executable doesn't exist at
...chrome-headless-shell.exe`, which names a file rather than a cause.

The fixture now installs both browsers itself before the run, through Playwright's own driver rather
than a shell script. It is a no-op once they are present, costing a second or so against a first-run
download of a few hundred MB, and `BLUELINE_SKIP_PLAYWRIGHT_INSTALL` opts out. Verified against an
empty cache — `PLAYWRIGHT_BROWSERS_PATH` pointed at a fresh directory, the run downloaded both
browsers and passed.

### 2.6 Remove the ingestion trigger, and move the schedule outside the app — `done`

`POST /api/ingestion/run` and the Data page's "Refresh now" button are gone, and the `/api`
surface is now read-only. Removing them was worth doing whatever replaced them: the endpoint was
unauthenticated, so any visitor could make the site fetch from the league's API as often as they
liked.

Scheduling is out of the site as well, per question 6. `Ingestion:DailyJobEnabled` now defaults to
**off**, and the schedule is a cron entry or scheduled task invoking the CLI's `daily` verb against
the same database — recipes for Linux and Windows are in the README, alongside the compose file.

**Question 10 has since confirmed this shape**: same host, a CLI invocation or sidecar, a separate
process for separation of responsibility rather than a separate machine, and never reachable as an
API. That is what was built, so nothing here needs revisiting.

**Turning the schedule off exposed a coupling that would have been a bad first-boot bug.** Seeding
an empty database lived behind the same `DailyJobEnabled` check as the daily pass, so a deployment
following the new advice — schedule outside, switch the in-process job off — would have come up
permanently empty, having silently skipped the season load, with the site looking merely
unpopulated rather than misconfigured. The two are now separate: seeding is about the site having
anything to serve and is gated only by `SeedSeasonId`, while `DailyJobEnabled` governs the schedule
alone. `SeedOnStartupTests` pins all three cases.

**The Data page reports which arrangement is in force** rather than describing one and hoping. It
previously said "new games load automatically once a day", which on a deployment with no scheduled
job would have been a page stating something false — and worse, it would have made a stale
last-run time read as a glitch rather than as the symptom it is.

Verified against the running app: `/data` renders the new wording with no Refresh button,
`GET /api/ingestion/run` is a 404 like any unknown route, and the log line on startup confirms the
schedule is off. The POST returns 400 rather than 404, which is Blazor's antiforgery middleware
rejecting every unmatched POST — `/api/nonsense/xyz` behaves identically, so the endpoint is
genuinely gone rather than merely erroring.

Still open, and it is the cost of this change: **nothing announces a schedule that was never set
up.** The site serves what it has and looks healthy. The README says so plainly and the Data page
shows the last run, but a deployment whose cron entry was never created goes stale in silence. A
readiness check for staleness was considered and left out — it would report Degraded all summer,
when no games are being played and nothing is wrong.

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

`export` and `import` in the CLI. Archives stay **out of the repository and unpublished**: they
are collected data needed only where the site runs, about 1 MB per season, and redistributing a
league's statistics in bulk is not a call to make casually. `scripts/build-seasons.ps1` exports
them and records checksums in `seed/manifest.json`; moving them to a deployment is a deliberate
manual step.

An empty database loads **every** archive present, so a deployment can carry several past seasons.
Two are built: 2025-26 and 2024-25, about 61,000 rows and 0.90 MB each. Both load into an empty
database in 23 seconds, against several minutes and ~1,500 requests per season through the API.

- **Gzipped JSON Lines, not a copy of the SQLite file**, so an archive taken from SQLite loads into
  any provider EF Core supports. A dacpac was the original suggestion, but that is SQL Server
  specific and schema-oriented; the schema already travels as EF migrations.
- **Each archive imports in its own transaction.** One unreadable archive costs only its own
  season. Within a season the import is all-or-nothing, because rows arrive in dependency order
  and a half-applied season reports wrong leaders rather than merely thin ones.
- Seasons therefore appear one at a time on a first boot. The site becomes ready once the first
  has landed and the rest follow within seconds; each is internally consistent when it appears.

**Building the second season exposed a real bug**, which is the value of having done it rather
than assumed it. See 4.2.

**Publishing was considered and rejected.** An earlier iteration fetched archives from a GitHub
release. That was dropped once the repository became public: the data is only needed in a deployed
environment, and bulk redistribution of the league's statistics is a question better avoided than
answered. The fetch script was removed with it.

### 3.2 Verify the image — `done`

Docker runs on this machine now, and the image has been built and exercised. What blocked it was
never Docker: virtualization was disabled in the firmware, so no hypervisor could start, WSL had no
distribution registered, and Docker Desktop's VM never came up — the engine answered 500 through
every pipe. `systeminfo` said it in one line, `Virtualization Enabled In Firmware: No`, which is
where this should have been checked first rather than after two restarts and a reinstall theory.

All three checks pass:

- **The image builds**, and both the site and the CLI run from it. The site answers `/` and
  `/health` on the published port; `Blueline.Cli.dll status` reports the database path and its
  contents against the same volume.
- **The non-root `app` user (uid 1654) can write to a mounted volume** — a named volume and a bind
  mount both, with `blueline.db` and its WAL sidecars owned by `app`. On Docker Desktop a bind mount
  arrives `drwxrwxrwx`, so this says nothing about a Linux host, where the directory carries the
  host's ownership and may still need chowning. The Dockerfile already says so.
- **`UseHttpsRedirection` behaves as measured outside the container.** The container log carries
  `Failed to determine the https port for redirect`, which is the mechanism: no HTTPS port is
  configured, so nothing redirects and the probe reaches the endpoint.

**The hardened HEALTHCHECK works where it matters.** `docker ps` reported `Up 25 seconds (healthy)`,
which is the first time that check has run inside a container rather than as a shell command against
the app.

Two findings that only a real build could produce:

- **`docker run … blueline dotnet Blueline.Cli.dll status` silently starts a second web server.**
  Arguments are appended to the entrypoint rather than replacing it, so the site starts, ignores
  them, and sits there — and in this case a second writer against the same SQLite volume. The
  documented form with `--entrypoint dotnet` is correct and now says why it is not optional.
- **The image carries the season archives** — `/app/seed` holds both, 1.9 MB, because the build
  copies whatever is in `seed/`. That is deliberate, and it is what let a container come up with
  2,792 games in seconds. But an image is a distribution artifact, and the archives are meant to
  stay private, so pushing one built after `build-seasons.ps1` to a public registry publishes them.
  Excluding `seed/` from the build context was tried and reverted: the Dockerfile copies it on
  purpose, and the honest fix is the warning rather than a redesign.

### 3.3 Write the deployment runbook — `done`

`docs/runbook.md`. **Every procedure in it was run against the real image rather than written from
the source**, which is what waiting for 3.2 bought:

- **First deploy** — an empty volume loaded both archives in seconds and came up healthy with 2,792
  games.
- **The volume actually persisting**, which the item called the single most important step. Restart,
  then confirm the count is unchanged and the log does not say "Database is empty". Verified.
- **Backups by `export` per season, not `cp`.** Copying the database file while the app runs can
  miss the `-wal` sidecar and produce something inconsistent. Export reads through the model, so it
  is consistent and portable. Round trip verified: both seasons exported from a running deployment
  and imported into an empty volume returned all 2,792 games.
- **Upgrades** — rebuilt with the volume in place, no re-seed, data intact.
- **Recovery** by `reconcile` after a missed stretch, and the readiness/liveness split with the real
  JSON both endpoints return.

It also carries the traps found in 3.2 — `--entrypoint dotnet`, not pushing an image built with
archives, leaving `ASPNETCORE_HTTPS_PORT` unset — and one found while writing it: from Git Bash on
Windows a `-v` path is rewritten before Docker sees it, and the export fails with
`Access to the path '/app/C:' is denied` until `MSYS_NO_PATHCONV=1` is set.

**What has not been exercised says so, in its own section.** A failed migration, bind mounts on a
Linux host (Docker Desktop mounts world-writable, which proves nothing about a host that passes its
own ownership through), and a live game day.

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

### 4.2 Load more seasons — `done`

2024-25 is loaded and archived alongside 2025-26. Question 1 asked for two seasons, to prove
multi-season works, with older data explicitly not a concern — so this is finished rather than
paused. Adding another season, if that changes, is a `backfill` followed by an `export`.

**Loading a second season failed at first**, and the cause was a design error rather than bad
data: `UNIQUE constraint failed: Teams.Abbrev`.

When Utah rebranded from Hockey Club to Mammoth the league issued the franchise a **new team id**
— 59 became 68 — while keeping the abbreviation `UTA`. The unique index on `Team.Abbrev` assumed
abbreviations identify a club. They do not: the id is the identity and the abbreviation is a
label, which relocations and rebrands change. The index is now non-unique, and a regression test
ingests two seasons where different ids share `UTA`.

The same investigation turned up a non-NHL club, id 7509 `MUN`, arriving from preseason fixtures
on a club schedule — a European side on a Global Series trip. Teams were being recorded from every
scheduled game including ones never ingested, so it would have been stored as a team that plays no
game we hold. Teams are now taken only from games that will actually be stored.

Worth knowing before going further back: box score detail thins out with age. Hits, blocked shots,
giveaways and takeaways are not reliably populated in older seasons, so a chart for those would be
empty or wrong rather than obviously missing. Verify per-stat coverage before publishing a season
much older than these two.

### 4.3 Confirm the daily job on a live game day — `todo`

It has been exercised against past dates and runs on startup, but has never fired during an
actual season. 2026-27 opens **2026-09-29**. Watch the first few days and confirm new games
appear without a manual run.

---

## 5. Polish

- **Caching.** `/api/leaders` aggregates ~50,000 rows on every page load. Completed seasons never
  change, so a memory cache keyed on season + stat would make this free. Question 7 was answered
  "depends how much sits in cache, needs further discussion", which is the right instinct — so the
  first move is to **measure** what a season's cached leaders actually weigh rather than argue about
  it. Group 6 raises the stakes: a dashboard of streaks is many aggregations per page load, not one.
- **Mobile.** Best-effort, per question 8. The CSS has responsive breakpoints but has only been
  checked at desktop width. Fix it if it looks broken; do not treat phones as a first-class target.
- **Accessibility — `done`.** Compared subjects now carry a shape as well as a
  colour: a circle, triangle, square, diamond, star or cross, drawn on the line every tenth point,
  repeated in the legend, and repeated again on the comparison chip — so the same mark identifies a
  subject everywhere it appears. Markers are switched on only for multi-subject charts, where colour
  was the sole distinction; a single-subject chart already separates its two series with a dash.

  Also done: a skip link ahead of the five nav entries; focus-visible outlines on links, buttons and
  chips, which had been relying on a browser default close to invisible on this background; a text
  equivalent on every chart, since a canvas is a picture and nothing more to a screen reader
  ("Cumulative points by game number. Connor McDavid ends at 138; Leon Draisaitl ends at 97");
  `scope="col"` on table headers; named remove buttons on the chips; and a live region on the
  comparison picker, whose results previously appeared in silence.

  **Contrast was measured rather than eyeballed** — every pairing in the palette clears AA against
  its background, the closest being muted text on the raised surface at 6.06:1. Nothing needed
  changing, which is worth recording so it is not re-litigated.

  **Two Blazor traps, both found by driving the live page.** `aria-pressed="@(condition)"` looks
  right and is not: Blazor treats a bool attribute value as a boolean attribute, so true renders
  `aria-pressed=""` and false omits it entirely — either way the state is lost. They are written as
  words now. And a Razor comment placed inside an element's attribute list **compiles**, then
  throws at render time when the browser is asked to `setAttribute` on a name that is a paragraph of
  English; it took the circuit down on every trend page.

  **Chart.js tooltips remain pointer-only**, which is not something to fix in Chart.js: the answer
  is that every chart now has the same numbers beside it as a table. The team page was the one
  without, so a club's game-by-game figures were reachable only with a mouse; it has a game log now,
  matching the player and goalie pages. Verified against Edmonton's season — 82 rows, newest first,
  standings points per game beside the running total.
- **Team colours in charts — `done`.** Every chart now draws a club in its own colour, and a player
  in their club's — the side the item did not ask for and the one that makes the dashboard read like
  hockey rather than like a palette. McDavid's line is Oilers orange; the streak panels are a wall of
  club colours.

  Three decisions worth keeping:

  - **Keyed on the abbreviation, not the team id.** The league reissues ids — Utah went from 59 to 68
    on its rebrand while keeping `UTA` — so the abbreviation is the stable key for what is, after
    all, a label.
  - **Brand colours adapted to a dark background, not brand colours.** Several clubs are primarily
    black or navy, which is invisible here, so Boston is gold, Los Angeles silver, Vegas gold. Every
    entry clears 3:1 against the card — the floor for a graphical object — and a test fails if an
    edit drops one below it. Colorado's burgundy needed lightening to pass at all.
  - **A second club wearing the same red falls back to the palette.** Half the league is in red, and
    two lines an eye cannot separate read worse than the unfamiliar colour they replaced. Verified
    live with two teammates: McDavid takes Oilers orange, Draisaitl the palette, because orange is
    already on the chart.

  **The chips had to learn where the colour came from.** They previously took it straight from the
  palette by index, which was right when the palette was the only source and wrong the moment the
  chart could choose. Each page now records the colour it drew each subject in, and a UI test pins
  the chip to the line it names.
- **Empty and error states — `done`.** Pages handled "no data" carefully and "the query failed" not
  at all: an exception during a load took the circuit down and left the reader with Blazor's yellow
  strip and a page that no longer answered. An `ErrorBoundary` around the page body now keeps the
  failure inside the page — an explanation, a Try again that re-runs the load, and a link to the
  Data page — while the exception still reaches the server log in full through
  `IErrorBoundaryLogger`. The boundary is recovered on navigation, without which one failed page
  would blank every page visited after it.

  **A failing API call now answers in the format its caller asked for.** It returned the `/Error`
  page's HTML with its 500, which is unreadable to anything expecting JSON. Ordering turned out to
  be the whole of it: the ProblemDetails handler has to be registered *after* the page handler, so
  that it sits inside it and sees an `/api` exception first. Registered the other way round — which
  is how it was written first, and how it looked correct — the page handler catches everything and
  the branch never runs. Measured in a Production build with a temporary throwing endpoint:
  `application/problem+json`, status 500, a traceId to match against the log, and no stack trace.

  **The failure state is exercised rather than assumed.** `/dev/throw` is a page that does nothing
  but fail, and redirects to the not-found page outside Development — verified, it answers 302 in a
  Production build. Two UI tests cover it: the fallback appears with Blazor's strip staying hidden,
  and navigating away afterwards lands on a working page.

---

## 6. The home page — streaks and a dashboard

New, from question 9. The largest piece of outstanding work in this document, and the only group
that is entirely unbuilt. The ask: the home page should surface **the most interesting active
streaks** — most points over the last 10 games, goals over the last 20, best save percentage over
the past two weeks — and be graphically dense, with charts and comparisons, changing day to day.
Leaders moves off the home page to a page of its own.

Sequenced so each step is useful on its own.

### 6.1 Move Leaders off `/` — `done`

`Home.razor` is now `Leaders.razor` at `/leaders`, and `/` is a landing page of its own — which is
what group 6 needed: an empty page for the dashboard to grow into rather than a working table to be
grafted onto.

**Thin, but not a placeholder.** A root that said "coming soon" would be worse than what it
replaced. It carries the four sections and a line of real numbers — 2,792 games across two seasons,
2024-25 to 2025-26 — read from the database rather than written into the markup, so it says
something true about the deployment it is running on and shows the empty state when there is
nothing stored.

The nav gained a Home entry rather than relying on the brand alone; `Match="NavLinkMatch.All"`
keeps it from lighting up on every page, since every path starts with `/`.

Two UI tests navigated to `/` expecting leaders and now go to `/leaders`. A new one clicks from the
root through to the leaders table, which is the part of this move a reader would actually notice —
the old bookmark still resolving. 26 UI tests pass.

Verified in a browser at both routes: the landing page renders its four cards and the season line,
and `/leaders` still lists McDavid at 138 points.

### 6.2 Compute streaks — `done`

`StreaksQueryService` answers "who is hot", against the rate each subject normally produces.
`GET /api/streaks` for skaters, `GET /api/streaks/goalies` for save percentage, both taking a
window in games or days.

**It disagrees with the leaderboard, which is the entire point.** On the live 2025-26 season the
ten-game points board reads Soderblom (8 points, 3.6× his rate), Samoskevich, Hartman — not McDavid,
who sits at a lift of about 1.0 because he is producing exactly what he always does. That is what
question 11 asked for, and the tests pin it with two players whose ten-game totals are identical
and whose lifts are not.

The guards matter more than the ranking, and they are relative rather than a table of per-stat
constants:

- **A floor at 40% of the board's own leader.** One assist against a leader's ten is an enormous
  multiple of a fringe player's baseline and is not a streak. Expressed against the best run in that
  window, this needs no number invented per stat or per window size.
- **The same idea on goalie workload**, which is what keeps a backup's .1000 over eighteen shots off
  the board — the specific failure that makes a rate leaderboard useless over a short window.
- **Ten games of season before a subject has a baseline at all**, and three appearances inside a
  days window before it is called form.
- **The baseline includes the window.** Excluding it would leave a player whose only production came
  in the window dividing by zero; including it also bounds the lift naturally.

Windows end on the **newest game stored**, not today, so the boards keep answering in the
off-season. What they then describe is the closing weeks of the last season played, which is 6.4's
problem to say out loud.

**Cost was the open risk, so it was measured rather than argued about.** Warm, against the real
two-season database:

| Board | Before | After |
| --- | --- | --- |
| Points, 10-game window | 250-390 ms | **64-92 ms** |
| Goals, 20-game window | 265-394 ms | **62-73 ms** |
| Hits, 14-day window | 58-87 ms | **43-52 ms** |
| Goalie save percentage, 14 days | 8-10 ms | **9-11 ms** |
| `/api/leaders`, for comparison | 40 ms | 40 ms |

Two changes account for the difference, and both were found by measuring:

- **`AsNoTracking` on the window fetch**, worth roughly 2.5× on its own. The projection carried the
  whole entity, so the change tracker was taking an identity snapshot of every row — and a six-week
  window across the league is tens of thousands of rows, not the eighty a single player's trend
  pulls.
- **Selecting three columns instead of the entity.** `SkaterValuesSince` picks the stat in SQL,
  which needs the same eleven-branch switch the leaderboards use. A plain `Select` into a named type
  translates fine; it is only `GroupBy` that will not.

A six-panel dashboard is therefore roughly 300-400 ms of query time if the panels run in sequence,
which they must — they share a scoped `DbContext`. That is the number question 7 should be answered
against, and it is what makes caching load-bearing rather than polish for 6.3.

**A bug found while verifying against real data.** Every goalie on the board had a blank club.
`GetPrimaryTeamAbbrevsAsync` counts *skater* rows, of which a goalie has none, so it returned
nothing and the board rendered five nameless clubs. There was already a goalie equivalent; the
board now picks the right one. The unit tests had not caught it because they assert on ids, and it
took a board printed from the live database to see it.

`SkaterTotalsAsync` was extracted from `GetLeadersAsync` so the season baseline is computed once
rather than copied. Projecting the grouped rows into a named type inside SQL was tried first and
does not translate — the tests caught it — so the rows are materialised and named in memory, which
costs nothing against the aggregation that produced them.

### 6.3 Build the dashboard — `done`

`/` is now five panels of runs — points over ten games, goals over twenty, assists over ten, hits
over a fortnight, and goalie save percentage over a fortnight — each row a name, the run, a
sparkline and the multiple it represents, linking through to the trend that produced it.

**The sparklines cost nothing extra.** `StreakLeader` carries the window's own per-game figures,
which were already in hand when the board was computed; fetching them per player would have turned
one query per panel into one per row on it. They are drawn as inline SVG rather than Chart.js:
twenty-five canvases with their own animation loops is a great deal of browser for a line with no
axes, no legend and no tooltip, and the SVG arrives with the page instead of after an interop
round trip.

**Cost was measured before deciding about caching, and the answer was not to.** The whole page
renders warm in **235-270 ms**, five panels included — panels run in sequence because they share a
scoped `DbContext`, so this is the honest serial number. That is tolerable without a cache, so none
was added; the measurement is recorded against question 7 instead, where the open half is how much
memory the cached results would occupy.

Details worth keeping:

- **The window travels with the link.** The trend pages now accept `?window=`, so clicking a
  fourteen-day run lands on a fourteen-day average rather than silently switching to ten games and
  showing different numbers than the ones that were clicked.
- **A panel nobody qualifies for says so.** In a quiet week most of them are empty, and on a short
  season a twenty-game window cannot be filled at all — which is exactly what the seeded UI test
  exercises.
- **A flat run sits on the midline** rather than at the top. There is no range to scale against, and
  dividing by zero span would have put every point at full height, reading as "off the chart" when
  it means "steady".
- **Sparklines are the first thing dropped below 420px**, where a name and a number still say what
  happened.

Verified against the live database: Soderblom at 3.6x his rate leads the points panel, and McDavid
appears nowhere — which is the whole point of the page. Six UI tests cover the panels, the empty
state, the link-through and the window travelling with it.

**A test assertion had to be weakened, and the reason is worth recording.** Asserting the sparkline
was *visible* failed: the seeded player scores three every night, so the line is perfectly flat, and
a zero-height box is invisible to Playwright while being on screen and correct. The test now counts
the vertices instead — one per game in the window — which is the stronger claim anyway.

### 6.4 Off-season and thin-data behaviour — `done`

A trailing window is silent about its own age. "Most points in the last ten games" reads identically
in March and in August, when those ten games are four months old — so the dashboard now says which
it is, in three states classified by how long the silence has lasted:

- **Current**, within three days. Clubs play every second or third night and thirty-two of them are
  doing it at once, so a day or two of quiet is normal and a week is not.
- **Behind**, four to twenty-one days. Either a scheduled break or a collector that stopped.
- **Off-season**, beyond that. The longest breaks a season contains — an all-star weekend, an
  Olympic break — run to about a fortnight, so three weeks of nothing is not a gap in the schedule.

**The site cannot tell a finished season from a stalled collector by looking at games alone, so it
does not pretend to.** When a gap opens it checks whether ingestion itself is current: with a
successful run behind it in the last two days, the silence is evidence that the league is not
playing; without one, the notice adds that stats may also be behind and points at the Data page.
A database with no ingestion runs at all — which is exactly what the UI tests build — gets the
honest version rather than the confident one.

**Thin data is a separate failure, and it was invisible.** An empty panel read "Nobody clears the
bar over this window" whether nobody stood out or nobody had played the window at all. `StreakBoard`
now reports how many subjects held a full window before any floor was applied, so a twenty-game
panel on a six-game season says "Nobody has played 20 games yet this season" instead of implying a
quiet week. That is the state every panel will be in for the opening fortnight of 2026-27.

Verified against the live database, which is in the state this item exists for: the page leads with
"The 2025-26 season is over. The last game was 4 months ago, on 16 April 2026, so these panels
describe how it finished rather than current form."

Group 6 is complete.

