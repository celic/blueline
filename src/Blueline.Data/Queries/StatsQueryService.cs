using Blueline.Core.Dtos;
using Blueline.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blueline.Data.Queries;

/// <summary>
/// Read-side queries for the site and the REST API.
///
/// Trends are built by pulling one subject's game rows in date order and folding cumulative
/// totals and rolling averages over them in memory. A subject plays at most ~110 games in a
/// season, so this stays cheaper than expressing window functions through the ORM and keeps
/// the maths identical across database providers. Season-wide aggregates, which do touch tens
/// of thousands of rows, stay in SQL.
/// </summary>
public class StatsQueryService(BluelineDbContext db)
{
    public async Task<IReadOnlyList<SeasonSummary>> GetSeasonsAsync(CancellationToken ct = default)
    {
        var rows = await db.Games
            .GroupBy(g => g.SeasonId)
            .Select(g => new
            {
                SeasonId = g.Key,
                Count = g.Count(),
                First = g.Min(x => x.GameDate),
                Last = g.Max(x => x.GameDate),
            })
            .OrderByDescending(g => g.SeasonId)
            .ToListAsync(ct);

        return rows
            .Select(r => new SeasonSummary(r.SeasonId, FormatSeason(r.SeasonId), r.Count, r.First, r.Last))
            .ToList();
    }

    /// <summary>Turns 20252026 into "2025-26".</summary>
    public static string FormatSeason(int seasonId) => $"{seasonId / 10000}-{seasonId % 10000 % 100:D2}";

    public async Task<int?> GetLatestSeasonAsync(CancellationToken ct = default)
    {
        var seasons = await db.Games.Select(g => g.SeasonId).Distinct().OrderByDescending(s => s).ToListAsync(ct);
        return seasons.Count == 0 ? null : seasons[0];
    }

    public async Task<IReadOnlyList<PlayerSummary>> SearchPlayersAsync(
        int seasonId, string? search, int take = 25, CancellationToken ct = default)
    {
        var stats = RegularSeasonSkaterStats(seasonId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            stats = stats.Where(s =>
                EF.Functions.Like(s.Player!.LastName, $"%{term}%") ||
                EF.Functions.Like(s.Player!.FirstName, $"%{term}%"));
        }

        var rows = await stats
            .GroupBy(s => s.PlayerId)
            .Select(g => new
            {
                PlayerId = g.Key,
                GamesPlayed = g.Count(),
                Goals = g.Sum(x => x.Goals),
                Assists = g.Sum(x => x.Assists),
                Points = g.Sum(x => x.Points),
            })
            .OrderByDescending(g => g.Points)
            .ThenByDescending(g => g.Goals)
            .Take(take)
            .ToListAsync(ct);

        var ids = rows.Select(r => r.PlayerId).ToList();
        var players = await db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var teams = await GetPrimaryTeamAbbrevsAsync(seasonId, ids, ct);

        return rows
            .Where(r => players.ContainsKey(r.PlayerId))
            .Select(r =>
            {
                var p = players[r.PlayerId];
                return new PlayerSummary(
                    p.Id, p.FullName, p.Position, p.HeadshotUrl,
                    teams.GetValueOrDefault(p.Id),
                    r.GamesPlayed, r.Goals, r.Assists, r.Points);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<TeamSummary>> GetTeamsAsync(int seasonId, CancellationToken ct = default)
    {
        var rows = await db.TeamGameStats
            .Where(s => s.Game!.SeasonId == seasonId && s.Game.GameType == GameTypes.Regular)
            .GroupBy(s => s.TeamId)
            .Select(g => new
            {
                TeamId = g.Key,
                GamesPlayed = g.Count(),
                Wins = g.Count(x => x.Result == "W"),
                Losses = g.Count(x => x.Result == "L"),
                OvertimeLosses = g.Count(x => x.Result == "OTL"),
                Points = g.Sum(x => x.Points),
            })
            .ToListAsync(ct);

        var teams = await db.Teams.ToDictionaryAsync(t => t.Id, ct);

        return rows
            .Where(r => teams.ContainsKey(r.TeamId))
            .OrderByDescending(r => r.Points)
            .ThenByDescending(r => r.Wins)
            .Select(r =>
            {
                var t = teams[r.TeamId];
                return new TeamSummary(t.Id, t.Abbrev, t.Name, t.LogoUrl,
                    r.GamesPlayed, r.Wins, r.Losses, r.OvertimeLosses, r.Points);
            })
            .ToList();
    }

    public async Task<TrendSeries?> GetPlayerTrendAsync(
        int playerId, int seasonId, string stat, int rollingWindow = 10, CancellationToken ct = default)
    {
        var definition = StatDefinition.FindSkater(stat);
        if (definition is null) return null;

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == playerId, ct);
        if (player is null) return null;

        // One player's season is ~82 rows, so pull the columns and pick the stat in memory.
        var rows = await RegularSeasonSkaterStats(seasonId)
            .Where(s => s.PlayerId == playerId)
            .OrderBy(s => s.Game!.GameDate)
            .ThenBy(s => s.GameId)
            .Select(s => new
            {
                s.GameId,
                s.Game!.GameDate,
                IsHome = s.TeamId == s.Game.HomeTeamId,
                Opponent = s.TeamId == s.Game.HomeTeamId ? s.Game.AwayTeam!.Abbrev : s.Game.HomeTeam!.Abbrev,
                Stat = s,
            })
            .ToListAsync(ct);

        var points = BuildPoints(
            rows.Select(r => new GameRow(
                r.GameId, r.GameDate, r.IsHome, r.Opponent, SkaterValue(r.Stat, definition.Key))).ToList(),
            rollingWindow);

        return new TrendSeries(
            player.FullName, playerId, definition.Key, definition.Label, seasonId, rollingWindow, points);
    }

    public async Task<TrendSeries?> GetTeamTrendAsync(
        int teamId, int seasonId, string stat, int rollingWindow = 10, CancellationToken ct = default)
    {
        var definition = StatDefinition.FindTeam(stat);
        if (definition is null) return null;

        var team = await db.Teams.FirstOrDefaultAsync(t => t.Id == teamId, ct);
        if (team is null) return null;

        var rows = await db.TeamGameStats
            .Where(s => s.TeamId == teamId && s.Game!.SeasonId == seasonId && s.Game.GameType == GameTypes.Regular)
            .OrderBy(s => s.Game!.GameDate)
            .ThenBy(s => s.GameId)
            .Select(s => new
            {
                s.GameId,
                s.Game!.GameDate,
                s.IsHome,
                s.OpponentTeamId,
                s.Points,
                s.GoalsFor,
                s.GoalsAgainst,
            })
            .ToListAsync(ct);

        var abbrevs = await db.Teams.ToDictionaryAsync(t => t.Id, t => t.Abbrev, ct);

        var points = BuildPoints(
            rows.Select(r => new GameRow(
                r.GameId, r.GameDate, r.IsHome,
                abbrevs.GetValueOrDefault(r.OpponentTeamId, "?"),
                definition.Key switch
                {
                    "goalsFor" => r.GoalsFor,
                    "goalsAgainst" => r.GoalsAgainst,
                    "goalDifferential" => r.GoalsFor - r.GoalsAgainst,
                    _ => r.Points,
                })).ToList(),
            rollingWindow);

        return new TrendSeries(
            team.Name, teamId, definition.Key, definition.Label, seasonId, rollingWindow, points);
    }

    public async Task<IReadOnlyList<LeaderRow>> GetLeadersAsync(
        int seasonId, string stat, int take = 20, CancellationToken ct = default)
    {
        var definition = StatDefinition.FindSkater(stat);
        if (definition is null) return [];

        // Season-wide, so the sum has to happen in SQL.
        //
        // Every branch below projects to the same anonymous type, so they unify into one query
        // type. The repetition is deliberate and cannot be factored into a helper: EF Core will
        // not translate a GroupBy whose key or aggregate is projected into a named type, and an
        // anonymous type cannot cross a method boundary.
        var stats = RegularSeasonSkaterStats(seasonId);
        var aggregated = definition.Key switch
        {
            "goals" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Goals) }),
            "assists" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Assists) }),
            "shots" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Shots) }),
            "hits" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Hits) }),
            "blockedShots" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.BlockedShots) }),
            "pim" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Pim) }),
            "plusMinus" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.PlusMinus) }),
            "takeaways" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Takeaways) }),
            "giveaways" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Giveaways) }),
            "toi" => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.TimeOnIceSeconds) }),
            _ => stats.GroupBy(s => s.PlayerId)
                .Select(g => new { PlayerId = g.Key, GamesPlayed = g.Count(), Total = g.Sum(x => x.Points) }),
        };

        var totals = await aggregated
            .OrderByDescending(t => t.Total)
            .Take(take)
            .ToListAsync(ct);

        var ids = totals.Select(t => t.PlayerId).ToList();
        var players = await db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var teams = await GetPrimaryTeamAbbrevsAsync(seasonId, ids, ct);

        return totals
            .Where(t => players.ContainsKey(t.PlayerId))
            .Select((t, i) =>
            {
                var p = players[t.PlayerId];
                return new LeaderRow(
                    i + 1, p.Id, p.FullName, p.Position,
                    teams.GetValueOrDefault(p.Id), p.HeadshotUrl, t.GamesPlayed,
                    // Time on ice is stored in seconds but only ever read as minutes.
                    definition.Key == "toi" ? Math.Round(t.Total / 60d, 1) : t.Total);
            })
            .ToList();
    }

    public async Task<IngestionStatusDto> GetIngestionStatusAsync(CancellationToken ct = default)
    {
        // Ordered by id, not StartedUtc: the id is monotonic and SQLite cannot sort DateTimeOffset.
        var last = await db.IngestionRuns.OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        var gameCount = await db.Games.CountAsync(ct);
        var latestDate = await db.Games
            .OrderByDescending(g => g.GameDate)
            .Select(g => (DateOnly?)g.GameDate)
            .FirstOrDefaultAsync(ct);

        return new IngestionStatusDto(
            last?.Kind, last?.StartedUtc, last?.CompletedUtc,
            last?.Status.ToString(), last?.Error, gameCount, latestDate);
    }

    private IQueryable<SkaterGameStat> RegularSeasonSkaterStats(int seasonId) =>
        db.SkaterGameStats.Where(s => s.Game!.SeasonId == seasonId && s.Game.GameType == GameTypes.Regular);

    /// <summary>
    /// A player can be traded mid-season, so label them by the club they played most games for.
    /// </summary>
    private async Task<Dictionary<int, string>> GetPrimaryTeamAbbrevsAsync(
        int seasonId, List<int> playerIds, CancellationToken ct)
    {
        var counts = await RegularSeasonSkaterStats(seasonId)
            .Where(s => playerIds.Contains(s.PlayerId))
            .GroupBy(s => new { s.PlayerId, s.TeamId })
            .Select(g => new { g.Key.PlayerId, g.Key.TeamId, Games = g.Count() })
            .ToListAsync(ct);

        var abbrevs = await db.Teams.ToDictionaryAsync(t => t.Id, t => t.Abbrev, ct);

        return counts
            .GroupBy(c => c.PlayerId)
            .ToDictionary(
                g => g.Key,
                g => abbrevs.GetValueOrDefault(g.OrderByDescending(x => x.Games).First().TeamId, ""));
    }

    private static double SkaterValue(SkaterGameStat s, string key) =>
        key switch
        {
            "goals" => s.Goals,
            "assists" => s.Assists,
            "shots" => s.Shots,
            "hits" => s.Hits,
            "blockedShots" => s.BlockedShots,
            "pim" => s.Pim,
            "plusMinus" => s.PlusMinus,
            "takeaways" => s.Takeaways,
            "giveaways" => s.Giveaways,
            // Minutes read better on an axis than seconds.
            "toi" => Math.Round(s.TimeOnIceSeconds / 60d, 2),
            _ => s.Points,
        };

    /// <summary>Projection shape shared by the player and team trend folds.</summary>
    internal record GameRow(long GameId, DateOnly Date, bool IsHome, string Opponent, double Value);

    /// <summary>Folds raw per-game values into cumulative totals and a trailing rolling average.</summary>
    internal static List<TrendPoint> BuildPoints(List<GameRow> rows, int rollingWindow)
    {
        var window = Math.Max(1, rollingWindow);
        var points = new List<TrendPoint>(rows.Count);
        var cumulative = 0d;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            cumulative += row.Value;

            // Only report a rolling average once a full window sits behind it; a partial window
            // makes the opening games of a season look far more volatile than they are.
            double? rolling = null;
            if (i + 1 >= window)
            {
                var sum = 0d;
                for (var j = i - window + 1; j <= i; j++) sum += rows[j].Value;
                rolling = Math.Round(sum / window, 3);
            }

            points.Add(new TrendPoint(
                i + 1, row.GameId, row.Date, row.Opponent, row.IsHome,
                row.Value, Math.Round(cumulative, 3), rolling));
        }

        return points;
    }
}
