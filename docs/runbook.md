# Blueline — deployment runbook

One container and one volume. TLS terminates in front of it; the container speaks plain HTTP on
8080.

Every command here has been run against the image built from this repository. Where something has
**not** been exercised, it says so rather than implying otherwise.

---

## First deploy

```bash
docker compose up -d --build
```

The site is on `http://localhost:8080`. On an empty database it loads any season archives baked into
the image, which takes seconds:

```
Database is empty; loading 2 season archive(s).
Imported 61185 rows for season 20242025.
```

With no archives present it falls back to the league's API instead — several minutes and about 1,500
requests per season, during which the site is up and readiness correctly says "not yet".

Check it came up:

```bash
docker compose ps
```

`Up 25 seconds (healthy)` is the goal. The container's own `HEALTHCHECK` asserts a 200 from
`/health`; it does not use `curl --fail`, which treats only 400 and above as failure and would pass
on a redirect.

### The step that matters more than the rest

**Seeding triggers on an empty database, not on a first-run flag.** If the volume does not genuinely
persist — an ephemeral container filesystem, a free tier that resets disk on redeploy — every restart
re-ingests from scratch. That is slow, it hammers the league's API, and it looks like nothing worse
than a slow start.

Prove the volume survives before trusting anything else:

```bash
docker compose restart
docker compose run --rm --entrypoint dotnet blueline Blueline.Cli.dll status
```

`Games stored` must be unchanged, and the log must **not** say "Database is empty". Verified here:
2,792 games before and after, with no re-seed.

---

## Keeping it current

**The site does not collect data on a schedule.** Nothing in it triggers ingestion and there is no
endpoint that does. Run one from outside, against the same volume, once a morning:

```bash
docker compose exec blueline dotnet Blueline.Cli.dll daily
```

Linux, as a crontab entry at 07:00:

```bash
0 7 * * * cd /srv/blueline && docker compose exec -T blueline dotnet Blueline.Cli.dll daily >> /var/log/blueline.log 2>&1
```

Each run re-reads the last few days rather than only yesterday, so a stat correction the league makes
after the fact is picked up. Running it twice does no harm — every write is an upsert.

**If nothing is scheduled, the data quietly stops moving.** The site keeps serving what it has and
looks perfectly healthy. The home page says how old its figures are, and the Data page shows when
stats were last collected; those are what to check when the numbers look stale.

---

## Watching it

| Endpoint | Answers | Use it for |
| --- | --- | --- |
| `/health` | Is the database reachable | Liveness. Restart on failure |
| `/health/ready` | Is there data, and is ingestion keeping up | Readiness. **Never** restart on failure |

```bash
curl -s http://localhost:8080/health/ready
```

```json
{"status":"Healthy","checks":[
  {"name":"database","status":"Healthy","description":"Database reachable."},
  {"name":"ingestion","status":"Healthy","description":"2792 games stored.",
   "data":{"gamesStored":2792,"lastRunKind":"none","lastRunStatus":"none","lastRunGamesFailed":0}}]}
```

**Do not point a restart policy at readiness.** A first run spends minutes loading a season, during
which readiness correctly reports "not yet"; restarting on that abandons the work and starts it
again, forever, while looking merely slow. That is why the image's own `HEALTHCHECK` probes
`/health`.

`Degraded` returns 200 and means the site is serving but something needs a look — a failed ingestion
run, or a run that could not read some games. `lastRunGamesFailed` above zero is the case for
`reconcile`, below.

---

## Backups

**Copying `blueline.db` while the app is running is not safe.** Write-ahead logging keeps recent
commits in the `-wal` sidecar, so a plain `cp` can produce a file that is missing the newest data or
is outright inconsistent. It looks like it works right up until it doesn't.

Export each season instead. This reads through the model, so the result is consistent, portable
across database providers, and restorable with one command:

```bash
docker compose run --rm -v /srv/blueline/backup:/backup \
  --entrypoint dotnet blueline Blueline.Cli.dll export 20252026 /backup/20252026.blueline.gz
```

About 0.9 MB per season, 61,000 rows. Repeat per season; `status` lists which are stored.

> From Git Bash on Windows, prefix the command with `MSYS_NO_PATHCONV=1`. Without it the shell
> rewrites `/backup/...` into a Windows path before Docker sees it, and the container fails with
> `Access to the path '/app/C:' is denied`.

### Restore

Into a fresh volume, before starting the site:

```bash
docker run --rm -v blueline-data:/data -v /srv/blueline/backup:/backup \
  -e Ingestion__SeedSeasonId=0 --entrypoint dotnet blueline-blueline \
  Blueline.Cli.dll import /backup/20252026.blueline.gz
```

`Ingestion__SeedSeasonId=0` stops the app seeding underneath the restore. Import is idempotent —
running it twice, or over a season already present, converges rather than duplicating.

Verified end to end: both seasons exported from a running deployment and imported into an empty
volume returned all 2,792 games.

---

## Upgrades and rollback

```bash
docker compose up -d --build
```

Replace the image, keep the volume. Verified: rebuilding with the volume in place left all 2,792
games and did not re-seed.

**Schema changes are the asymmetry.** `MigrateAsync` runs at startup, so a failed migration is a
failed boot rather than a degraded service — and rolling the image back does *not* roll the schema
back. Before deploying a release that adds a migration, take an export; if the new image fails to
start, restoring into a fresh volume is the way back, not redeploying the old image against the
migrated one.

This is the one procedure here that has **not** been exercised — no migration has yet failed on this
deployment.

---

## Recovery

If the host was asleep or the schedule was missed for longer than the daily lookback window, the
daily job will not notice: it only re-reads the last few days. Close the gap:

```bash
docker compose run --rm --entrypoint dotnet blueline Blueline.Cli.dll reconcile 20252026
```

It diffs the league's schedule for the season against what is stored and ingests only what is
missing — 33 requests and no box scores fetched when nothing is wrong. It also re-reads games stored
with no stat lines, which is the failure no count of games would reveal.

---

## Settings, and the ones to leave alone

| Setting | Why |
| --- | --- |
| `BLUELINE_DATA_DIR` | Already `/data` in the image; point the volume there |
| `Ingestion__SeedSeasonId` | `0` disables self-seeding, for a restore or a database you fill yourself |
| `Ingestion__DailyJobEnabled` | `true` moves the schedule into the site instead of a job outside it |
| `Blueline__UseForwardedHeaders` | `true` behind a proxy that terminates TLS, so the app sees the original scheme and client address |
| `ASPNETCORE_HTTPS_PORT` | **Leave unset.** See below |

**Never set an HTTPS port inside the container.** `UseHttpsRedirection` redirects to whatever HTTPS
port it can find, and with one configured a plain-HTTP request to `/health` becomes
`307 → https://localhost:8443/health`. Proxied user traffic is unaffected — forwarded headers mark it
already secure — but the health probe arrives locally without them and gets redirected. With no port
configured the middleware has no destination and never redirects, which the container log states
outright: `Failed to determine the https port for redirect`.

---

## Two things that will catch you out

**`--entrypoint dotnet` is not optional for CLI commands.** The image's entrypoint is the site, so
arguments given without it are appended rather than replacing it:
`docker run … blueline dotnet Blueline.Cli.dll status` starts a *second web server* against the same
volume and sits there. It does not fail.

**Do not push an image built with archives present to a public registry.** The build copies whatever
is in `seed/`, so an image built after `build-seasons.ps1` carries a couple of MB of the league's
statistics. That is the point when the deployment is yours, and a publication when the registry is
not.

---

## Not yet exercised

- **Bind mounts on a Linux host.** Docker Desktop mounts host directories world-writable, so the
  non-root `app` user wrote to one here without trouble. A Linux host passes its own ownership
  through, and the directory may need `chown 1654:1654` before the app can create its database.
- **A failed migration**, as above.
- **A live game day.** The daily job has been run against past dates but has never fired during a
  season; 2026-27 opens 2026-09-29.
