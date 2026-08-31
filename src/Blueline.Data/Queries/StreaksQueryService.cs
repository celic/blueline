using Blueline.Core.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Blueline.Data.Queries;

/// <summary>
/// Who is hot right now, as opposed to who is best.
///
/// A streak here is a leaderboard over a trailing window, not a run of consecutive games with a
/// point. Ranking is by how far a run departs from what that subject normally does, not by the
/// raw total: the highest totals belong to the same handful of stars for weeks at a time, and
/// those already have a leaderboard of their own. A fourth-liner at three times his usual rate is
/// the more interesting fact, and the one that changes as the season goes.
///
/// Departure alone is not enough, though — at the bottom of a roster the ratios are enormous and
/// meaningless, because one good night against a season of nothing is a huge multiple of nothing.
/// Every board therefore applies a floor relative to its own leader (see
/// <see cref="MinimumShareOfLeader"/>) rather than a table of per-stat thresholds, which would
/// need a number invented for each stat and each window size.
/// </summary>
public class StreaksQueryService(BluelineDbContext db, StatsQueryService stats)
{
    /// <summary>
    /// How much of the raw leader's production a subject needs before their lift is considered.
    ///
    /// This is the guard that keeps the boards honest, and it scales itself: it needs no constant
    /// per stat or per window, because it is expressed against whatever the best run in that
    /// window happens to be. Two points in ten games is a large multiple of a fringe player's
    /// baseline and is not a streak; eight against a leader's twenty is.
    /// </summary>
    public const double MinimumShareOfLeader = 0.4;

    /// <summary>
    /// A days window needs more than one appearance behind it. A single three-point night inside
    /// a fortnight is a good night, not a fortnight's form, and without this it would outrank
    /// every sustained run on the board.
    /// </summary>
    public const int MinimumGamesInDaysWindow = 3;

    /// <summary>
    /// Games a subject needs across the season before their baseline means anything. A player
    /// recalled last week has no established rate to depart from.
    /// </summary>
    public const int MinimumSeasonGames = 10;

    /// <summary>
    /// How far back a games window is allowed to reach for its games.
    ///
    /// A games window has no natural cut-off in the calendar, but an *active* streak does: ten
    /// games ending in March is not news in April. Six weeks comfortably covers ten games for
    /// anyone playing regularly while excluding players who stopped — the injured and the
    /// long-since traded, whose last ten games are a historical record rather than current form.
    /// </summary>
    public const int GamesWindowLookbackDays = 42;

    /// <summary>
    /// Shots a goalie needs inside the window, as a share of the busiest goalie's workload there.
    ///
    /// Rates need this more than counting stats do, and the failure it prevents is specific: over
    /// a fortnight a backup with one quiet start can post a .960 and top the board, which is noise
    /// presented as a finding. Judged relatively for the same reason as
    /// <see cref="MinimumShareOfLeader"/> — a fortnight in November and a fortnight in a
    /// compressed March schedule are different amounts of hockey.
    /// </summary>
    public const double MinimumGoalieShareOfWorkload = 0.4;

    /// <summary>Shots a goalie needs across the season before their save percentage is a baseline.</summary>
    public const int MinimumSeasonShots = 200;

    /// <summary>
    /// The hottest skaters for one stat over one window.
    ///
    /// Returns null for a stat that does not exist, and an empty board — rather than null — when
    /// the season simply has nobody who qualifies, which is a normal answer for a window that
    /// lands in a quiet week.
    /// </summary>
    public async Task<StreakBoard?> GetSkaterStreaksAsync(
        int seasonId, string stat, RollingWindow window = default, int take = 5,
        GameScope scope = GameScope.RegularSeason, CancellationToken ct = default)
    {
        var definition = StatDefinition.FindSkater(stat);
        if (definition is null) return null;

        if (window.Size <= 0) window = RollingWindow.Default;

        var asOf = await LatestGameDateAsync(seasonId, scope, ct);
        if (asOf is null) return null;

        var rows = await stats
            .SkaterValuesSince(seasonId, definition.Key, scope, EarliestDate(asOf.Value, window))
            .ToListAsync(ct);

        var runs = rows
            .GroupBy(r => r.PlayerId)
            .Select(g => Run(
                g.Key,
                g.OrderByDescending(r => r.Date).Select(r => (r.Date, r.Value)).ToList(),
                window))
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToList();

        var seasonTotals = (await stats.SkaterTotalsAsync(seasonId, definition.Key, scope, ct))
            .ToDictionary(t => t.PlayerId);

        var leaders = Rank(
            runs,
            id => seasonTotals.TryGetValue(id, out var t) && t.GamesPlayed >= MinimumSeasonGames
                // Time on ice is stored in seconds and read everywhere else in minutes; the
                // window values already came through SkaterValue, which converts.
                ? (definition.Key == "toi" ? t.Total / 60d : t.Total) / t.GamesPlayed
                : null,
            take);

        return await BoardAsync(
            definition, window, asOf.Value, seasonId, scope, leaders, goalies: false, runs.Count, ct);
    }

    /// <summary>
    /// The hottest goalies by save percentage over one window.
    ///
    /// Save percentage only, deliberately. It is the goalie stat a run is read in, and the one
    /// the calendar window exists for — a fortnight is four starts for one goalie and eight for
    /// another, which no games window can compare fairly.
    /// </summary>
    public async Task<StreakBoard?> GetGoalieStreaksAsync(
        int seasonId, RollingWindow window = default, int take = 5,
        GameScope scope = GameScope.RegularSeason, CancellationToken ct = default)
    {
        var definition = StatDefinition.FindGoalie("savePctg")!;

        if (window.Size <= 0) window = RollingWindow.Default;

        var asOf = await LatestGameDateAsync(seasonId, scope, ct);
        if (asOf is null) return null;

        // Only appearances with ice time. A backup logs a zero-minute row for every game on the
        // bench, and counting those as games would make a fortnight look busier than it was.
        var rows = await stats.GoalieStats(seasonId, scope)
            .AsNoTracking()
            .Where(s => s.TimeOnIceSeconds > 0 && s.Game!.GameDate >= EarliestDate(asOf.Value, window))
            .Select(s => new { s.PlayerId, s.Game!.GameDate, s.Saves, s.ShotsAgainst })
            .ToListAsync(ct);

        var seasonRows = await stats.GoalieStats(seasonId, scope)
            .Where(s => s.TimeOnIceSeconds > 0)
            .GroupBy(s => s.PlayerId)
            .Select(g => new { PlayerId = g.Key, Saves = g.Sum(x => x.Saves), Shots = g.Sum(x => x.ShotsAgainst) })
            .ToListAsync(ct);

        var baselines = seasonRows
            .Where(r => r.Shots >= MinimumSeasonShots)
            .ToDictionary(r => r.PlayerId, r => (double)r.Saves / r.Shots);

        var candidates = new List<(int Id, int Games, double Saves, double Shots, List<double> Values)>();

        foreach (var group in rows.GroupBy(r => r.PlayerId))
        {
            var appearances = group.OrderByDescending(r => r.GameDate).ToList();
            if (window.Unit == WindowUnit.Games)
            {
                if (appearances.Count < window.Size) continue;
                appearances = appearances.Take(window.Size).ToList();
            }
            else if (appearances.Count < MinimumGamesInDaysWindow)
            {
                continue;
            }

            candidates.Add((
                group.Key, appearances.Count,
                appearances.Sum(a => (double)a.Saves), appearances.Sum(a => (double)a.ShotsAgainst),
                appearances
                    .Select(a => a.ShotsAgainst > 0 ? (double)a.Saves / a.ShotsAgainst : 0)
                    .Reverse()
                    .ToList()));
        }

        var busiest = candidates.Count == 0 ? 0 : candidates.Max(c => c.Shots);

        var leaders = candidates
            .Where(c => c.Shots > 0 && c.Shots >= busiest * MinimumGoalieShareOfWorkload)
            .Where(c => baselines.ContainsKey(c.Id))
            .Select(c =>
            {
                var pctg = c.Saves / c.Shots;
                var baseline = baselines[c.Id];
                return new StreakLeader(
                    c.Id, "", null, null, c.Games,
                    Math.Round(pctg, 4), Math.Round(pctg, 4), Math.Round(baseline, 4),
                    // A difference, not a multiple: .930 against .910 is twenty points of save
                    // percentage, and a ratio of 1.02 would say nothing anyone recognises.
                    Math.Round(pctg - baseline, 4), c.Values);
            })
            .OrderByDescending(l => l.Lift)
            .ThenByDescending(l => l.Total)
            .ThenBy(l => l.SubjectId)
            .Take(take)
            .ToList();

        return await BoardAsync(
            definition, window, asOf.Value, seasonId, scope, leaders, goalies: true, candidates.Count, ct);
    }

    /// <summary>
    /// One subject's window, or null when it does not hold one: too few games for a days window,
    /// or fewer games than a games window asks for.
    /// </summary>
    private static (int Id, int Games, double Total, List<double> Values)? Run(
        int subjectId, List<(DateOnly Date, double Value)> newestFirst, RollingWindow window)
    {
        var inWindow = newestFirst;

        if (window.Unit == WindowUnit.Games)
        {
            if (inWindow.Count < window.Size) return null;
            inWindow = inWindow.Take(window.Size).ToList();
        }
        else if (inWindow.Count < MinimumGamesInDaysWindow)
        {
            return null;
        }

        return (
            subjectId,
            inWindow.Count,
            inWindow.Sum(r => r.Value),
            // Reversed, because the rows arrived newest first and a line is read left to right.
            inWindow.Select(r => r.Value).Reverse().ToList());
    }

    /// <summary>
    /// Applies the floor, then ranks by lift. Subjects with no usable baseline are dropped rather
    /// than ranked against an assumed one.
    /// </summary>
    private static List<StreakLeader> Rank(
        List<(int Id, int Games, double Total, List<double> Values)> runs,
        Func<int, double?> baselineFor, int take)
    {
        if (runs.Count == 0) return [];

        var best = runs.Max(r => r.Total);
        if (best <= 0) return [];

        return runs
            .Where(r => r.Total >= best * MinimumShareOfLeader)
            .Select(r => (Run: r, Baseline: baselineFor(r.Id)))
            .Where(x => x.Baseline is > 0)
            .Select(x =>
            {
                var perGame = x.Run.Total / x.Run.Games;
                return new StreakLeader(
                    x.Run.Id, "", null, null, x.Run.Games,
                    Math.Round(x.Run.Total, 2), Math.Round(perGame, 3), Math.Round(x.Baseline!.Value, 3),
                    Math.Round(perGame / x.Baseline!.Value, 2), x.Run.Values);
            })
            .OrderByDescending(l => l.Lift)
            // A tie on lift goes to the bigger run, which is the more convincing version of the
            // same story.
            .ThenByDescending(l => l.Total)
            .ThenBy(l => l.SubjectId)
            .Take(take)
            .ToList();
    }

    /// <summary>Fills in the names, clubs and headshots — for the handful that made the board, not the field.</summary>
    private async Task<StreakBoard> BoardAsync(
        StatDefinition definition, RollingWindow window, DateOnly asOf, int seasonId,
        GameScope scope, List<StreakLeader> leaders, bool goalies, int considered, CancellationToken ct)
    {
        var ids = leaders.Select(l => l.SubjectId).ToList();
        var players = await db.Players.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        // A goalie's club has to be read from their own appearances. The skater lookup counts
        // skater rows, of which a goalie has none, so it silently labels every one of them with
        // nothing at all — which is how this was found, on a board of five nameless clubs.
        var teams = goalies
            ? await stats.GetGoaliePrimaryTeamsAsync(seasonId, ids, scope, ct)
            : await stats.GetPrimaryTeamAbbrevsAsync(seasonId, ids, scope, ct);

        var named = leaders
            .Where(l => players.ContainsKey(l.SubjectId))
            .Select(l => l with
            {
                SubjectName = players[l.SubjectId].FullName,
                TeamAbbrev = teams.GetValueOrDefault(l.SubjectId),
                HeadshotUrl = players[l.SubjectId].HeadshotUrl,
            })
            .ToList();

        return new StreakBoard(
            definition.Key, definition.Label, window.Size, window.Unit, asOf, definition.IsRate, named,
            considered);
    }

    /// <summary>
    /// The first day the window can reach. A days window is exactly its own length; a games window
    /// gets a generous span, since its length is counted in games and only the calendar can say
    /// whether those games are recent enough to be a streak at all.
    /// </summary>
    private static DateOnly EarliestDate(DateOnly asOf, RollingWindow window) =>
        asOf.AddDays(-((window.Unit == WindowUnit.Days ? window.Size : GamesWindowLookbackDays) - 1));

    /// <summary>
    /// The newest day of hockey stored, which is what every window ends on. Not today: in the
    /// off-season today would produce empty boards for months, and mid-season it would quietly
    /// shorten every window by however long ingestion has been behind.
    /// </summary>
    private async Task<DateOnly?> LatestGameDateAsync(int seasonId, GameScope scope, CancellationToken ct)
    {
        var types = scope.GameTypes();
        return await db.Games
            .Where(g => g.SeasonId == seasonId && types.Contains(g.GameType))
            .OrderByDescending(g => g.GameDate)
            .Select(g => (DateOnly?)g.GameDate)
            .FirstOrDefaultAsync(ct);
    }
}
