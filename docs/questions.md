# Blueline — open questions

Questions where your answer changes what gets built. Each one lists what I will do **if you say
nothing**, so none of these block progress — they just risk me building the wrong thing.

Answer inline under each question; I will clear them out as they are resolved.

**Updated 2026-08-31.** Everything here is answered. The answers are kept below in condensed form
as a record of what was decided; the work they imply lives in `plan.md`.

---

## Open

Nothing outstanding. New questions get added here as they come up.

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

**7. Should completed seasons be cached aggressively?** Aggregates only. You asked how much data
would sit in cache, and measuring answered it: every leaderboard and streak board for both seasons
and both scopes is **0.7 MB of JSON**, while caching every player's trend would take **0.56 GB** —
the expensive queries are small and the big ones are cheap. Leaderboards, streak boards and
standings are cached; per-subject trends are not. *(plan.md group 5.)*

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
