# Blueline — open questions

Questions where your answer changes what gets built. Each one lists what I will do **if you say
nothing**, so none of these block progress — they just risk me building the wrong thing.

Answer inline under each question; I will clear them out as they are resolved.

**Updated 2026-08-30.** Everything here is answered except question 7, which asked for a number
rather than an opinion and is waiting on a measurement I can take. Answers are kept below in
condensed form as a record of what was decided; the work they imply lives in `plan.md`.

---

## Open

### 7. Should completed seasons be cached aggressively?

A finished season never changes, so leaders and trends for it could be cached indefinitely and
served instantly. The only cost is that a correction to a past season would not appear until a
restart or explicit eviction.

> **Answer:** Depends on how much data is sitting in cache. Older seasons are less likely to be
> retrieved. Need further discussion.

**The right instinct, and the discussion needs a number rather than an argument.** So the next step
is to measure a season's cached leaders rather than reason about them. Two things bear on it:

- **Retrieval frequency is the wrong axis to decide on if the cost is small.** A cache that expires
  on idle already handles "older seasons are rarely asked for" — the rarely-touched season falls out
  by itself, and the question of how much sits in memory answers itself.
- **The streaks dashboard changes the sums.** A dashboard is many aggregations on the page every
  visitor lands on first, over the *current* season, which is the one that cannot be cached
  indefinitely because it changes daily. That is a shorter expiry keyed to ingestion, not the same
  mechanism.

**Default if unanswered:** measure first; then a size-bounded cache with idle expiry rather than
indefinite retention, and a separate short expiry for the in-progress season, invalidated when
ingestion writes.

**First measurements, from building the streak boards (plan.md 6.2).** Warm, against the real
two-season database: a season leaderboard is 40 ms, a streak board 43-92 ms depending on the window,
a goalie board 10 ms. A six-panel dashboard is therefore 300-400 ms of query time, since panels
share a scoped `DbContext` and cannot run concurrently. That is the number worth deciding against —
not enormous, but it is paid on the page every visitor lands on first, and every panel of it is
recomputing figures that only change when a game is ingested. What is still unmeasured is the size
of the cached results, which is the half of the question you actually asked.

**And the dashboard was built without one.** The whole page renders warm in 235-270 ms with its five
panels, which is tolerable, so no cache was added on the strength of a guess. The case for caching
is now about what happens under more than one visitor at a time, not about the page being slow.

---

## Settled

**1. How many seasons?** Two — 2025-26 and 2024-25 — to show multiple seasons work. Older data is
not a concern. *(plan.md 4.2, complete.)*

**2. How should goalies be presented?** Route the player page by position; goalies are never compared
with skaters. *(Already how 1.1 was built — skater and goalie searches draw from separate tables.)*

**3. Advanced stats — Corsi, Fenwick, xG?** No. Official box score stats only. *(Closes play-by-play
ingestion and any third-party feed.)*

**4. Regular season and playoffs — merged or separate?** A toggle that never merges them. Playoff
charts numbered within the playoffs themselves. *(The merge option has to be removed — plan.md 1.5.
The numbering half already works.)*

**5. Where hosted?** The Dockerfile is the run tool for now. *(No host to choose; plan.md 3.2 is now
about verifying the image.)*

**6. Does the site need to be private?** No trigger API for data collection; scheduling belongs
outside the app. *(plan.md 2.6, and question 10 above.)*

**8. Is mobile first-class?** No — best-effort.

**10. How should the scheduled ingestion run?** On the same host, as a CLI invocation or a sidecar —
a separate process for separation of responsibility, not a separate machine — and never triggered
through an externally reachable API. *(This is what plan.md 2.6 built; no change needed.)*

**11. What makes a streak "interesting"?** The most interesting ones, not the highest totals —
league leaders already have their own page. Changing week by week rather than day by day is fine.
*(Settles the ranking question in plan.md 6.3: rank by how far a run departs from what that player
normally does, not by raw total.)*

**9. Anything not in plan.md?** The home page should surface the most interesting active streaks —
most points over the last 10 games, goals over the last 20, best save percentage over the past two
weeks — and be graphically dense with charts and comparisons, changing day to day. Leaders moves to
its own page. *(plan.md group 6, plus 1.6 for the days-based window it needs.)*
