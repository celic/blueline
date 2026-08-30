# Blueline — open questions

Questions where your answer changes what gets built. Each one lists what I will do **if you say
nothing**, so none of these block progress — they just risk me building the wrong thing.

Answer inline under each question; I will clear them out as they are resolved.

**Updated 2026-08-30.** Questions 1-6, 8 and 9 are answered and folded into `plan.md`; they are
kept below in condensed form as a record of what was decided. Question 7 is still open, and
answering 9 raised two new ones — 10 and 11.

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

### 10. How should the scheduled ingestion actually run?

From your answer to 6: no trigger API, and a cron job on a separate system inserting into the
existing database. The first half is unambiguous and I will do it. The second half does not fit
SQLite — the database is a file, and reaching it from another machine means a network share, where
SQLite's locking is unreliable in a way that corrupts rather than stalls.

Three readings, described in full in plan.md 2.6:

1. **Scheduled on the same host, sharing the volume** — `docker exec` or a sidecar running the CLI's
   `daily` verb. Outside the web app, no trigger API, one machine writing one file.
2. **Genuinely another machine**, which means moving off SQLite. Possible by design — the connection
   string is overridable — but it gives up the one-container shape.
3. **Keep the in-process worker**, reading your answer as being about the trigger API only.

**Default if unanswered:** option 1, and the endpoint and Refresh button removed regardless.

**Built on that default.** The removals are done, and the schedule now lives outside the site by
default — the README carries cron and scheduled-task recipes. Only *where* the job runs is still
yours to say; switching to option 3 is one setting (`Ingestion__DailyJobEnabled=true`), and option 2
is a connection string plus a provider.

### 11. On the new home page, what makes a streak "interesting"?

You asked for the most interesting active streaks and for content that changes day to day. Those two
pull against each other, and which one wins is a design decision I should not make silently.

- **Ranked by raw total**, the same handful of stars hold the page for weeks. Accurate, and static.
- **Ranked by departure from a player's own baseline**, a fourth-liner with 8 points in 10 games
  outranks McDavid with 12. More surprising, more genuinely daily, and arguably the more interesting
  fact — but the page stops being a list of the best players.

**Default if unanswered:** rank by total, with a qualification threshold, and revisit once there is a
live season to look at — the answer is much easier to judge against a real page than in the abstract.

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

**9. Anything not in plan.md?** The home page should surface the most interesting active streaks —
most points over the last 10 games, goals over the last 20, best save percentage over the past two
weeks — and be graphically dense with charts and comparisons, changing day to day. Leaders moves to
its own page. *(plan.md group 6, plus 1.6 for the days-based window it needs.)*
