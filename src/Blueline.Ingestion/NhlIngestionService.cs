using Blueline.Core.Entities;
using Blueline.Data;
using Blueline.Ingestion.Nhl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Blueline.Ingestion;

/// <summary>
/// Pulls games from the league API into the database.
///
/// Every write is an upsert keyed on the league's own ids, so a backfill, a daily run and a
/// manual re-run of the same date all converge on the same rows. That matters because the
/// daily job deliberately re-reads recent dates to pick up late stat corrections.
/// </summary>
public class NhlIngestionService(
    BluelineDbContext db,
    NhlApiClient api,
    ILogger<NhlIngestionService> logger)
{
    /// <summary>Boxscores fetched concurrently per batch. Kept modest to stay a polite API citizen.</summary>
    private const int FetchConcurrency = 6;

    /// <summary>Loads an entire season: teams, every final game, then player names.</summary>
    public async Task<int> BackfillSeasonAsync(int seasonId, CancellationToken ct = default)
    {
        var run = await StartRunAsync("backfill", seasonId, ct);
        try
        {
            var abbrevs = await SyncTeamsAsync(seasonId, ct);
            if (abbrevs.Count == 0)
                throw new InvalidOperationException($"Could not resolve any teams for season {seasonId}.");

            var gameIds = await DiscoverSeasonGameIdsAsync(seasonId, abbrevs, ct);
            logger.LogInformation("Season {Season}: {Count} completed games to ingest.", seasonId, gameIds.Count);

            var outcome = await IngestGamesAsync(gameIds, ct);
            await EnrichPlayerNamesAsync(seasonId, abbrevs, ct);

            await CompleteRunAsync(run, outcome, ct);
            return outcome.Ingested;
        }
        catch (Exception ex)
        {
            await FailRunAsync(run, ex, ct);
            throw;
        }
    }

    /// <summary>
    /// Ingests a window of recent dates. The daily job uses a multi-day lookback because the
    /// league revises box scores for a day or two after a game.
    /// </summary>
    public async Task<int> IngestRecentAsync(DateOnly throughDate, int lookbackDays, CancellationToken ct = default)
    {
        var run = await StartRunAsync("daily", null, ct);
        try
        {
            var gameIds = new List<long>();
            for (var offset = lookbackDays; offset >= 0; offset--)
            {
                var date = throughDate.AddDays(-offset);
                var score = await api.GetScoreAsync(date, ct);
                if (score?.Games is null) continue;

                gameIds.AddRange(score.Games.Where(IsIngestableGame).Select(g => g.Id));
            }

            var outcome = await IngestGamesAsync(gameIds.Distinct().ToList(), ct);

            // New season, new rosters: refresh names for any player we only know by initial.
            if (outcome.Ingested > 0)
            {
                var season = await db.Games.OrderByDescending(g => g.GameDate).Select(g => g.SeasonId).FirstOrDefaultAsync(ct);
                if (season != 0)
                {
                    var abbrevs = await db.Teams.Select(t => t.Abbrev).ToListAsync(ct);
                    await EnrichPlayerNamesAsync(season, abbrevs, ct);
                }
            }

            await CompleteRunAsync(run, outcome, ct);
            return outcome.Ingested;
        }
        catch (Exception ex)
        {
            await FailRunAsync(run, ex, ct);
            throw;
        }
    }

    /// <summary>
    /// Compares the league's schedule for a season against what is stored, and ingests whatever
    /// is absent.
    ///
    /// This is the safety net under the daily job. That job only looks back a few days, so any
    /// stretch where the app was not running — a free host asleep, a machine off over a
    /// weekend — leaves a hole nothing else would ever notice. It also picks up the games that
    /// an earlier run recorded as failed.
    ///
    /// Cheap to run when there is nothing wrong: the schedule walk is 33 requests, and no box
    /// score is fetched unless something is genuinely missing.
    /// </summary>
    public async Task<int> ReconcileSeasonAsync(int seasonId, CancellationToken ct = default)
    {
        var run = await StartRunAsync("reconcile", seasonId, ct);
        try
        {
            var abbrevs = await SyncTeamsAsync(seasonId, ct);
            if (abbrevs.Count == 0)
                throw new InvalidOperationException($"Could not resolve any teams for season {seasonId}.");

            var expected = await DiscoverSeasonGameIdsAsync(seasonId, abbrevs, ct);
            var missing = await FindGamesNeedingIngestionAsync(seasonId, expected, ct);

            if (missing.Count == 0)
            {
                logger.LogInformation(
                    "Season {Season} is complete: all {Count} games are stored.", seasonId, expected.Count);
                await CompleteRunAsync(run, IngestionOutcome.Nothing, ct);
                return 0;
            }

            logger.LogInformation(
                "Season {Season}: {Missing} of {Expected} games need ingesting.",
                seasonId, missing.Count, expected.Count);

            var outcome = await IngestGamesAsync(missing, ct);
            if (outcome.Ingested > 0) await EnrichPlayerNamesAsync(seasonId, abbrevs, ct);

            await CompleteRunAsync(run, outcome, ct);
            return outcome.Ingested;
        }
        catch (Exception ex)
        {
            await FailRunAsync(run, ex, ct);
            throw;
        }
    }

    /// <summary>
    /// Games the league lists that we cannot show: either absent entirely, or present as a row
    /// with no stat lines behind it. The second case matters because a game whose box score was
    /// only half applied looks stored while charting as though nobody played.
    /// </summary>
    private async Task<List<long>> FindGamesNeedingIngestionAsync(
        int seasonId, IReadOnlyList<long> expected, CancellationToken ct)
    {
        var stored = (await db.Games
                .Where(g => g.SeasonId == seasonId)
                .Select(g => g.Id)
                .ToListAsync(ct))
            .ToHashSet();

        var empty = await db.Games
            .Where(g => g.SeasonId == seasonId && !g.SkaterGameStats.Any())
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (empty.Count > 0)
            logger.LogWarning("{Count} stored game(s) have no player stats and will be re-read.", empty.Count);

        return expected
            .Where(id => !stored.Contains(id))
            .Concat(empty)
            .Distinct()
            .Order()
            .ToList();
    }

    /// <summary>Regular season and playoffs only, and only once the game is actually over.</summary>
    internal static bool IsIngestableGame(ScheduleGame g) =>
        g.GameType is GameTypes.Regular or GameTypes.Playoffs && g.GameState is "OFF" or "FINAL";

    private async Task<List<string>> SyncTeamsAsync(int seasonId, CancellationToken ct)
    {
        // Standings carry the abbreviations we need to walk club schedules; the numeric team
        // ids only appear on the games themselves.
        var seasonEndYear = seasonId % 10000;
        var standings = await api.GetStandingsAsync(new DateOnly(seasonEndYear, 4, 1), ct);
        if (standings?.Standings is null or { Count: 0 })
        {
            logger.LogWarning("No standings available for season {Season}; falling back to known teams.", seasonId);
            return await db.Teams.Select(t => t.Abbrev).ToListAsync(ct);
        }

        return standings.Standings
            .Select(r => r.TeamAbbrev?.Default)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a!)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Walks all 32 club schedules. Each game appears twice (once per club), which is exactly
    /// what we want: it also yields both teams' numeric ids and abbreviations for free.
    /// </summary>
    private async Task<List<long>> DiscoverSeasonGameIdsAsync(int seasonId, List<string> abbrevs, CancellationToken ct)
    {
        var gameIds = new HashSet<long>();
        var teamAbbrevs = new Dictionary<int, string>();

        foreach (var abbrev in abbrevs)
        {
            var schedule = await api.GetClubScheduleAsync(abbrev, seasonId, ct);
            if (schedule?.Games is null) continue;

            foreach (var game in schedule.Games)
            {
                if (IsIngestableGame(game)) gameIds.Add(game.Id);

                foreach (var team in new[] { game.HomeTeam, game.AwayTeam })
                {
                    if (team is { Id: > 0 } && team.Abbrev?.Default is { Length: > 0 } a)
                        teamAbbrevs[team.Id] = a;
                }
            }
        }

        await UpsertTeamsAsync(teamAbbrevs, ct);
        return [.. gameIds.Order()];
    }

    private async Task UpsertTeamsAsync(Dictionary<int, string> teamAbbrevs, CancellationToken ct)
    {
        var existing = await db.Teams.ToDictionaryAsync(t => t.Id, ct);
        foreach (var (id, abbrev) in teamAbbrevs)
        {
            if (existing.TryGetValue(id, out var team))
                team.Abbrev = abbrev;
            else
                db.Teams.Add(new Team { Id = id, Abbrev = abbrev, Name = abbrev });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>What one pass over a set of games managed to store, and what it could not.</summary>
    internal record IngestionOutcome(int Ingested, IReadOnlyList<long> FailedGameIds)
    {
        public static readonly IngestionOutcome Nothing = new(0, []);
    }

    private async Task<IngestionOutcome> IngestGamesAsync(IReadOnlyList<long> gameIds, CancellationToken ct)
    {
        var ingested = 0;
        var failed = new List<long>();

        foreach (var batch in gameIds.Chunk(FetchConcurrency))
        {
            ct.ThrowIfCancellationRequested();

            // Fetch concurrently, persist serially: DbContext is not thread-safe. Ids are paired
            // with their responses so a null can be attributed to the game it belongs to.
            var boxscores = await Task.WhenAll(batch.Select(id => api.GetBoxscoreAsync(id, ct)));

            foreach (var (id, box) in batch.Zip(boxscores))
            {
                if (box is null)
                {
                    // The client has already retried and logged. Record it rather than moving on:
                    // a silently skipped game is indistinguishable from one that was never played.
                    failed.Add(id);
                    continue;
                }

                await ApplyBoxscoreAsync(box, ct);
                ingested++;
            }

            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            if (ingested > 0 && ingested % 120 == 0)
                logger.LogInformation("Ingested {Count}/{Total} games.", ingested, gameIds.Count);
        }

        if (failed.Count > 0)
            logger.LogWarning("{Count} game(s) could not be read: {GameIds}", failed.Count, string.Join(", ", failed));

        return new IngestionOutcome(ingested, failed);
    }

    private async Task ApplyBoxscoreAsync(BoxscoreResponse box, CancellationToken ct)
    {
        if (box.HomeTeam is null || box.AwayTeam is null) return;
        if (!DateOnly.TryParse(box.GameDate, out var gameDate)) return;

        await UpsertTeamFromBoxscoreAsync(box.HomeTeam, ct);
        await UpsertTeamFromBoxscoreAsync(box.AwayTeam, ct);

        var game = await db.Games.FindAsync([box.Id], ct);
        if (game is null)
        {
            game = new Game { Id = box.Id };
            db.Games.Add(game);
        }

        game.SeasonId = box.Season;
        game.GameType = box.GameType;
        game.GameDate = gameDate;
        game.HomeTeamId = box.HomeTeam.Id;
        game.AwayTeamId = box.AwayTeam.Id;
        game.HomeScore = box.HomeTeam.Score;
        game.AwayScore = box.AwayTeam.Score;
        game.GameState = box.GameState;
        game.LastPeriodType = box.GameOutcome?.LastPeriodType ?? "REG";

        await UpsertTeamGameStatAsync(game, isHome: true, ct);
        await UpsertTeamGameStatAsync(game, isHome: false, ct);

        if (box.PlayerByGameStats is null) return;

        await UpsertPlayerStatsAsync(box.Id, box.HomeTeam.Id, box.PlayerByGameStats.HomeTeam, ct);
        await UpsertPlayerStatsAsync(box.Id, box.AwayTeam.Id, box.PlayerByGameStats.AwayTeam, ct);
    }

    // Every lookup below uses FindAsync rather than a query: games are ingested in batches, so
    // the same team or player is often already tracked as a pending insert from an earlier game
    // in the batch, and a query would miss it and try to add a duplicate.
    private async Task UpsertTeamFromBoxscoreAsync(BoxscoreTeam boxTeam, CancellationToken ct)
    {
        var team = await db.Teams.FindAsync([boxTeam.Id], ct);

        // "Florida" + "Panthers" reads better than either half alone.
        var fullName = string.Join(' ', new[] { boxTeam.PlaceName?.Default, boxTeam.CommonName?.Default }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var abbrev = boxTeam.Abbrev?.Default ?? "";

        if (team is null)
        {
            db.Teams.Add(new Team
            {
                Id = boxTeam.Id,
                Abbrev = abbrev,
                Name = string.IsNullOrWhiteSpace(fullName) ? abbrev : fullName,
                LogoUrl = boxTeam.Logo,
            });
            return;
        }

        if (!string.IsNullOrWhiteSpace(abbrev)) team.Abbrev = abbrev;
        if (!string.IsNullOrWhiteSpace(fullName)) team.Name = fullName;
        if (!string.IsNullOrWhiteSpace(boxTeam.Logo)) team.LogoUrl = boxTeam.Logo;
    }

    private async Task UpsertTeamGameStatAsync(Game game, bool isHome, CancellationToken ct)
    {
        var teamId = isHome ? game.HomeTeamId : game.AwayTeamId;
        var goalsFor = isHome ? game.HomeScore : game.AwayScore;
        var goalsAgainst = isHome ? game.AwayScore : game.HomeScore;

        var stat = await db.TeamGameStats.FindAsync([game.Id, teamId], ct);
        if (stat is null)
        {
            stat = new TeamGameStat { GameId = game.Id, TeamId = teamId };
            db.TeamGameStats.Add(stat);
        }

        var won = goalsFor > goalsAgainst;

        // Standings points are a regular-season construct; the playoffs award none at all.
        // Recording playoff points would invent a number that does not exist, and summing a
        // combined season would then overstate a club's actual standings total.
        var awardsStandingsPoints = game.GameType == GameTypes.Regular;

        // A regular-season loss past regulation still banks a point; a regulation loss banks
        // nothing. In the playoffs an overtime loss is simply a loss.
        var lostBeyondRegulation = !won && awardsStandingsPoints && game.LastPeriodType is "OT" or "SO";

        stat.OpponentTeamId = isHome ? game.AwayTeamId : game.HomeTeamId;
        stat.IsHome = isHome;
        stat.GoalsFor = goalsFor;
        stat.GoalsAgainst = goalsAgainst;
        stat.Result = won ? "W" : lostBeyondRegulation ? "OTL" : "L";
        stat.Points = !awardsStandingsPoints ? 0 : won ? 2 : lostBeyondRegulation ? 1 : 0;
    }

    private async Task UpsertPlayerStatsAsync(long gameId, int teamId, TeamPlayers? players, CancellationToken ct)
    {
        if (players is null) return;

        foreach (var s in players.AllSkaters)
        {
            await EnsurePlayerAsync(s.PlayerId, s.Name?.Default, s.Position ?? "", ct);

            var stat = await db.SkaterGameStats.FindAsync([gameId, s.PlayerId], ct);
            if (stat is null)
            {
                stat = new SkaterGameStat { GameId = gameId, PlayerId = s.PlayerId };
                db.SkaterGameStats.Add(stat);
            }

            stat.TeamId = teamId;
            stat.Goals = s.Goals;
            stat.Assists = s.Assists;
            stat.Points = s.Points;
            stat.PlusMinus = s.PlusMinus;
            stat.Pim = s.Pim;
            stat.Hits = s.Hits;
            stat.BlockedShots = s.BlockedShots;
            stat.Shots = s.Sog;
            stat.PowerPlayGoals = s.PowerPlayGoals;
            stat.Giveaways = s.Giveaways;
            stat.Takeaways = s.Takeaways;
            stat.Shifts = s.Shifts;
            stat.TimeOnIceSeconds = TimeOnIce.ToSeconds(s.Toi);
            stat.FaceoffWinPctg = s.FaceoffWinningPctg;
        }

        foreach (var g in players.Goalies ?? [])
        {
            await EnsurePlayerAsync(g.PlayerId, g.Name?.Default, "G", ct);

            var stat = await db.GoalieGameStats.FindAsync([gameId, g.PlayerId], ct);
            if (stat is null)
            {
                stat = new GoalieGameStat { GameId = gameId, PlayerId = g.PlayerId };
                db.GoalieGameStats.Add(stat);
            }

            stat.TeamId = teamId;
            stat.Starter = g.Starter;
            stat.ShotsAgainst = g.ShotsAgainst;
            stat.Saves = g.Saves;
            stat.GoalsAgainst = g.GoalsAgainst;
            stat.Pim = g.Pim;
            stat.TimeOnIceSeconds = TimeOnIce.ToSeconds(g.Toi);
        }
    }

    /// <summary>
    /// Creates the player if we have not seen them. Boxscores only carry an abbreviated
    /// "D. Tarasov", so this stores a placeholder that <see cref="EnrichPlayerNamesAsync"/> replaces.
    /// </summary>
    private async Task EnsurePlayerAsync(int playerId, string? boxscoreName, string position, CancellationToken ct)
    {
        var player = await db.Players.FindAsync([playerId], ct);
        if (player is not null)
        {
            if (string.IsNullOrWhiteSpace(player.Position) && !string.IsNullOrWhiteSpace(position))
                player.Position = position;
            return;
        }

        var (first, last) = SplitBoxscoreName(boxscoreName);
        db.Players.Add(new Player
        {
            Id = playerId,
            FirstName = first,
            LastName = last,
            Position = position,
        });
    }

    /// <summary>
    /// True while a player still carries the placeholder taken from a boxscore, where the first
    /// name is an initial such as "D." rather than "Daniil".
    /// </summary>
    internal static bool NeedsRealName(Player player) =>
        string.IsNullOrWhiteSpace(player.FirstName) || player.FirstName.EndsWith('.');

    internal static (string First, string Last) SplitBoxscoreName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ("", "");
        var idx = name.IndexOf(' ');
        return idx <= 0 ? ("", name.Trim()) : (name[..idx].Trim(), name[(idx + 1)..].Trim());
    }

    /// <summary>
    /// Replaces the abbreviated boxscore names with real first/last names and headshots,
    /// using each club's season stat roster. Costs one request per team per game type.
    /// </summary>
    private async Task EnrichPlayerNamesAsync(int seasonId, IReadOnlyList<string> abbrevs, CancellationToken ct)
    {
        var players = await db.Players.ToDictionaryAsync(p => p.Id, ct);

        // Costs one request per team, so skip it entirely on the common daily case where every
        // player already has a real name and only their stat lines changed.
        if (!players.Values.Any(NeedsRealName))
        {
            logger.LogDebug("All known players already have full names; skipping roster lookup.");
            return;
        }

        var updated = 0;

        foreach (var abbrev in abbrevs)
        {
            foreach (var gameType in new[] { GameTypes.Regular, GameTypes.Playoffs })
            {
                var stats = await api.GetClubStatsAsync(abbrev, seasonId, gameType, ct);
                if (stats is null) continue;

                foreach (var entry in (stats.Skaters ?? []).Concat(stats.Goalies ?? []))
                {
                    if (!players.TryGetValue(entry.PlayerId, out var player)) continue;

                    var first = entry.FirstName?.Default;
                    var last = entry.LastName?.Default;
                    if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(last)) continue;

                    if (player.FirstName != first || player.LastName != last || player.HeadshotUrl != entry.Headshot)
                    {
                        player.FirstName = first;
                        player.LastName = last;
                        player.HeadshotUrl = entry.Headshot ?? player.HeadshotUrl;
                        if (!string.IsNullOrWhiteSpace(entry.PositionCode)) player.Position = entry.PositionCode;
                        updated++;
                    }
                }
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Resolved full names for {Count} players.", updated);
    }

    private async Task<IngestionRun> StartRunAsync(string kind, int? seasonId, CancellationToken ct)
    {
        var run = new IngestionRun
        {
            Kind = kind,
            SeasonId = seasonId,
            StartedUtc = DateTimeOffset.UtcNow,
            Status = IngestionStatus.Running,
        };
        db.IngestionRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }

    /// <summary>
    /// How much of the failed-id list is kept. Enough to act on a bad night without letting one
    /// broken run write an unbounded string; the count stays exact either way.
    /// </summary>
    internal const int MaxRecordedFailedIds = 50;

    private async Task CompleteRunAsync(IngestionRun run, IngestionOutcome outcome, CancellationToken ct)
    {
        // The change tracker is cleared between batches, so re-attach before finishing the record.
        db.IngestionRuns.Attach(run);
        run.GamesIngested = outcome.Ingested;
        run.GamesFailed = outcome.FailedGameIds.Count;
        run.FailedGameIds = FormatFailedIds(outcome.FailedGameIds);
        run.CompletedUtc = DateTimeOffset.UtcNow;
        run.Status = IngestionStatus.Succeeded;
        await db.SaveChangesAsync(ct);
    }

    internal static string? FormatFailedIds(IReadOnlyList<long> failedGameIds)
    {
        if (failedGameIds.Count == 0) return null;

        var kept = string.Join(",", failedGameIds.Take(MaxRecordedFailedIds));
        return failedGameIds.Count > MaxRecordedFailedIds
            ? $"{kept},… ({failedGameIds.Count - MaxRecordedFailedIds} more)"
            : kept;
    }

    private async Task FailRunAsync(IngestionRun run, Exception ex, CancellationToken ct)
    {
        try
        {
            db.ChangeTracker.Clear();
            db.IngestionRuns.Attach(run);
            run.CompletedUtc = DateTimeOffset.UtcNow;
            run.Status = IngestionStatus.Failed;
            run.Error = ex.Message;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception saveEx)
        {
            logger.LogError(saveEx, "Could not record the failure of ingestion run {RunId}.", run.Id);
        }
    }
}
