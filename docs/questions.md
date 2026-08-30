# Blueline — open questions

Questions where your answer changes what gets built. Each one lists what I will do **if you say
nothing**, so none of these block progress — they just risk me building the wrong thing.

Answer inline under each question; I will clear them out as they are resolved.

---

## Scope and content

### 1. How many seasons do you want loaded?

Only 2025-26 is in the database. The schema and UI are already multi-season, so this is purely a
question of how much history you want and how far back the league's API stays reliable. Each
season is ~1,400 games, a few minutes to load, and about 5 MB.

Worth knowing: box score detail thins out the further back you go. Hits, blocked shots,
giveaways and takeaways are not reliably populated in older seasons, so a 2008 trend chart for
"hits" may be empty or wrong rather than obviously missing.

**Default if unanswered:** load the last five completed seasons (2021-22 through 2025-26) and
verify per-stat coverage before showing older data.

> **Answer:**

### 2. How should goalies be presented?

Their stats are already stored. Options: a separate `/goalies` section, or route the existing
player page to a goalie view when the player is a goalie. The second is less navigation but
means the "compare with" list has to avoid mixing goalies and skaters, since save percentage and
points share no axis.

**Default if unanswered:** route the player page by position, and restrict comparison to players
of the same kind.

> **Answer:**

### 3. Do you want advanced stats — Corsi, Fenwick, expected goals?

These are the stats a trends site is usually wanted for, and **the endpoints I am using do not
carry them.** They would need either play-by-play ingestion (`/v1/gamecenter/{id}/play-by-play`,
which is a much larger ingest and a real modelling exercise for xG) or a third-party source such
as MoneyPuck or Natural Stat Trick, each with its own licensing and reliability questions.

This is the largest potential change of scope in this document, which is why I have not started
on it.

**Default if unanswered:** stay with official box score stats only.

> **Answer:**

### 4. Regular season and playoffs — merged or separate?

Playoff games are stored but filtered out everywhere. A "game number" x axis is misleading across
a playoff run, since series vary in length and gaps between rounds are long.

**Default if unanswered:** a Regular / Playoffs toggle that never merges the two, with playoff
charts plotted by date rather than game number.

> **Answer:**

---

## Deployment

### 5. Where do you want this hosted, and should I set it up?

You raised free self-hosting early and it shaped the architecture — SQLite specifically so a
deployment is one container plus one small volume. I have not built any deployment assets yet
because the target changes what they look like.

Rough shape of the options, all of which need verifying against current terms:

- **Oracle Cloud Always Free** — genuinely free and generous, but you manage a VM.
- **Azure App Service F1** — native .NET, but 60 CPU-minutes/day is tight for Blazor Server,
  which holds a WebSocket per visitor.
- **Fly.io / Render** — container hosting with a persistent volume; watch for instances that
  sleep on inactivity, which interacts badly with the daily ingestion job (see plan.md 2.3).

Also: do you want a real domain, or is a platform subdomain fine?

**Default if unanswered:** write a Dockerfile that runs anywhere with a volume, and leave the
host choice to you.

> **Answer:**

### 6. Does the site need to be private?

There is no authentication at all right now — the site and the whole API are open, including
`POST /api/ingestion/run`, which any visitor could call to trigger fetching. That endpoint at
minimum should be protected or removed before this is public.

**Default if unanswered:** keep the site public and read-only, and require a shared secret for
the ingestion endpoint.

> **Answer:**

---

## Smaller calls

### 7. Should completed seasons be cached aggressively?

A finished season never changes, so leaders and trends for it could be cached indefinitely and
served instantly. The only cost is that a correction to a past season would not appear until a
restart or explicit eviction.

**Default if unanswered:** cache completed seasons in memory, leave the in-progress season
uncached.

> **Answer:**

### 8. Is mobile a first-class target?

The CSS has breakpoints but I have only verified desktop width. If phones matter, the controls
row and the wide stat tables need real work, not just a media query.

**Default if unanswered:** treat mobile as best-effort and fix it only if it looks broken.

> **Answer:**

### 9. Anything you want that is not in `plan.md`?

Player-vs-team splits, home/away splits, streak detection ("longest point streak"), milestone
markers on charts, and export to CSV are all natural extensions I have not assumed you want.

> **Answer:**
