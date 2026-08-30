using Blueline.Core.Dtos;
using Blueline.Core.Entities;

namespace Blueline.Tests;

public class SkaterAndTeamQueryTests : QueryFixture
{
    /// <summary>Two skaters over three games, with deliberately different stat profiles.</summary>
    private async Task SeedSkatersAsync()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddPlayer(1, "Point", "Machine");
        AddPlayer(2, "Physical", "Defender", "D");

        for (var i = 0; i < 3; i++)
        {
            AddGame(2025020001 + i, i, 21, 22);
            AddSkaterLine(2025020001 + i, 1, 21, goals: 2, assists: 1, shots: 5, hits: 0, toiSeconds: 1200);
            AddSkaterLine(2025020001 + i, 2, 21, goals: 0, assists: 0, shots: 1, hits: 6, blockedShots: 4, toiSeconds: 1500);
        }

        await SaveAsync();
    }

    [Test]
    public async Task Leaders_rank_by_the_requested_stat_not_always_by_points()
    {
        await SeedSkatersAsync();

        var points = await Queries.GetLeadersAsync(SeasonId, "points");
        var hits = await Queries.GetLeadersAsync(SeasonId, "hits");

        Assert.Multiple(() =>
        {
            Assert.That(points[0].PlayerId, Is.EqualTo(1));
            Assert.That(points[0].Value, Is.EqualTo(9), "three goals plus assists per game across three games");
            Assert.That(hits[0].PlayerId, Is.EqualTo(2), "the defender leads hits despite having no points");
            Assert.That(hits[0].Value, Is.EqualTo(18));
        });
    }

    [TestCase("goals", 6)]
    [TestCase("assists", 3)]
    [TestCase("points", 9)]
    [TestCase("shots", 15)]
    public async Task Each_stat_aggregates_its_own_column(string stat, double expected)
    {
        await SeedSkatersAsync();

        var leaders = await Queries.GetLeadersAsync(SeasonId, stat);

        Assert.That(leaders.Single(l => l.PlayerId == 1).Value, Is.EqualTo(expected));
    }

    [Test]
    public async Task Time_on_ice_is_reported_in_minutes_not_seconds()
    {
        await SeedSkatersAsync();

        var leaders = await Queries.GetLeadersAsync(SeasonId, "toi");

        // Player 2 logs 1,500 seconds across three games = 4,500 seconds = 75 minutes.
        Assert.That(leaders.Single(l => l.PlayerId == 2).Value, Is.EqualTo(75));
    }

    [Test]
    public async Task Leaders_are_ranked_from_one_and_contiguously()
    {
        await SeedSkatersAsync();

        var leaders = await Queries.GetLeadersAsync(SeasonId, "points");

        Assert.That(leaders.Select(l => l.Rank), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task An_unknown_stat_yields_no_leaders()
    {
        await SeedSkatersAsync();

        Assert.That(await Queries.GetLeadersAsync(SeasonId, "nonsense"), Is.Empty);
    }

    [Test]
    public async Task Take_limits_the_leaderboard()
    {
        await SeedSkatersAsync();

        Assert.That(await Queries.GetLeadersAsync(SeasonId, "points", take: 1), Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Player_search_matches_either_half_of_the_name_case_insensitively()
    {
        await SeedSkatersAsync();

        var byLast = await Queries.SearchPlayersAsync(SeasonId, "machine");
        var byFirst = await Queries.SearchPlayersAsync(SeasonId, "Physical");

        Assert.Multiple(() =>
        {
            Assert.That(byLast.Select(p => p.Id), Is.EqualTo(new[] { 1 }));
            Assert.That(byFirst.Select(p => p.Id), Is.EqualTo(new[] { 2 }));
        });
    }

    [Test]
    public async Task Player_search_with_no_term_returns_everyone_ordered_by_points()
    {
        await SeedSkatersAsync();

        var all = await Queries.SearchPlayersAsync(SeasonId, null);

        Assert.That(all.Select(p => p.Id), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public async Task A_traded_skater_is_labelled_by_the_club_they_played_most_for()
    {
        AddTeam(21, "OLD");
        AddTeam(22, "NEW");
        AddPlayer(1, "Traded", "Skater");

        AddGame(2025020001, 0, 21, 22);
        AddSkaterLine(2025020001, 1, teamId: 21, goals: 1);
        for (var i = 1; i <= 3; i++)
        {
            AddGame(2025020001 + i, i, 22, 21);
            AddSkaterLine(2025020001 + i, 1, teamId: 22, goals: 1);
        }
        await SaveAsync();

        var player = (await Queries.SearchPlayersAsync(SeasonId, null)).Single();

        Assert.That(player.TeamAbbrev, Is.EqualTo("NEW"));
    }

    [Test]
    public async Task A_player_trend_is_ordered_by_date_and_labels_the_opponent_and_venue()
    {
        // Named for identity rather than venue: the same two clubs meet twice, swapping rink.
        AddTeam(21, "OUR");
        AddTeam(22, "OPP");
        AddPlayer(1, "Test", "Skater");

        // Added out of order to prove the query sorts rather than relying on insertion order.
        AddGame(2025020002, 5, homeTeamId: 22, awayTeamId: 21);
        AddSkaterLine(2025020002, 1, teamId: 21, goals: 2);
        AddGame(2025020001, 1, homeTeamId: 21, awayTeamId: 22);
        AddSkaterLine(2025020001, 1, teamId: 21, goals: 1);
        await SaveAsync();

        var trend = await Queries.GetPlayerTrendAsync(1, SeasonId, "goals");

        Assert.Multiple(() =>
        {
            Assert.That(trend!.Points[0].GameId, Is.EqualTo(2025020001), "the earlier date comes first");
            Assert.That(trend.Points[0].IsHome, Is.True);

            Assert.That(trend.Points[1].IsHome, Is.False, "the second game was on the road");
            Assert.That(trend.Points[1].Cumulative, Is.EqualTo(3));

            // The opponent is the other club either way; only the venue changes.
            Assert.That(trend.Points.Select(p => p.Opponent), Is.All.EqualTo("OPP"));
        });
    }

    [Test]
    public async Task An_unknown_player_or_stat_yields_no_trend()
    {
        await SeedSkatersAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await Queries.GetPlayerTrendAsync(999, SeasonId, "points"), Is.Null);
            Assert.That(await Queries.GetPlayerTrendAsync(1, SeasonId, "nonsense"), Is.Null);
        });
    }

    // --- teams ---

    private async Task SeedTeamRecordsAsync()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");

        // Home team: a win, an overtime loss, a regulation loss.
        AddGame(2025020001, 0, 21, 22);
        AddTeamLine(2025020001, 21, 22, true, "W", 2, goalsFor: 4, goalsAgainst: 1);
        AddGame(2025020002, 1, 21, 22, lastPeriodType: "OT");
        AddTeamLine(2025020002, 21, 22, true, "OTL", 1, goalsFor: 2, goalsAgainst: 3);
        AddGame(2025020003, 2, 21, 22);
        AddTeamLine(2025020003, 21, 22, true, "L", 0, goalsFor: 0, goalsAgainst: 5);

        await SaveAsync();
    }

    [Test]
    public async Task Team_records_tally_results_and_standings_points()
    {
        await SeedTeamRecordsAsync();

        var team = (await Queries.GetTeamsAsync(SeasonId)).Single();

        Assert.Multiple(() =>
        {
            Assert.That(team.GamesPlayed, Is.EqualTo(3));
            Assert.That(team.Wins, Is.EqualTo(1));
            Assert.That(team.Losses, Is.EqualTo(1));
            Assert.That(team.OvertimeLosses, Is.EqualTo(1));
            Assert.That(team.StandingsPoints, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task A_team_trend_can_chart_goal_differential_including_negative_swings()
    {
        await SeedTeamRecordsAsync();

        var trend = await Queries.GetTeamTrendAsync(21, SeasonId, "goalDifferential");

        Assert.Multiple(() =>
        {
            // +3, then -1, then -5.
            Assert.That(trend!.Points.Select(p => p.Value), Is.EqualTo(new double[] { 3, -1, -5 }));
            Assert.That(trend.Points[^1].Cumulative, Is.EqualTo(-3));
        });
    }

    [TestCase("goalsFor", 6)]
    [TestCase("goalsAgainst", 9)]
    [TestCase("points", 3)]
    public async Task A_team_trend_accumulates_the_requested_stat(string stat, double expectedTotal)
    {
        await SeedTeamRecordsAsync();

        var trend = await Queries.GetTeamTrendAsync(21, SeasonId, stat);

        Assert.That(trend!.Total, Is.EqualTo(expectedTotal));
    }

    [Test]
    public async Task An_unknown_team_or_stat_yields_no_trend()
    {
        await SeedTeamRecordsAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await Queries.GetTeamTrendAsync(999, SeasonId, "points"), Is.Null);
            Assert.That(await Queries.GetTeamTrendAsync(21, SeasonId, "nonsense"), Is.Null);
        });
    }

    // --- seasons and status ---

    [Test]
    public async Task Seasons_report_their_game_split_and_date_range()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddGame(2025020001, 0, 21, 22);
        AddGame(2025020002, 10, 21, 22);
        AddGame(2025030001, 200, 21, 22, gameType: GameTypes.Playoffs);
        await SaveAsync();

        var season = (await Queries.GetSeasonsAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(season.Label, Is.EqualTo("2025-26"));
            Assert.That(season.GameCount, Is.EqualTo(3));
            Assert.That(season.RegularSeasonGames, Is.EqualTo(2));
            Assert.That(season.PlayoffGames, Is.EqualTo(1));
            Assert.That(season.FirstGame, Is.EqualTo(new DateOnly(2025, 10, 8)));
            Assert.That(season.LastGame, Is.EqualTo(new DateOnly(2026, 4, 26)));
        });
    }

    [Test]
    public async Task The_latest_season_is_the_highest_id_and_null_when_nothing_is_stored()
    {
        Assert.That(await Queries.GetLatestSeasonAsync(), Is.Null);

        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddGame(2025020001, 0, 21, 22);
        await SaveAsync();

        Assert.That(await Queries.GetLatestSeasonAsync(), Is.EqualTo(SeasonId));
    }

    [Test]
    public async Task Ingestion_status_reports_an_empty_database_without_throwing()
    {
        var status = await Queries.GetIngestionStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.GamesInDatabase, Is.Zero);
            Assert.That(status.LastRunKind, Is.Null);
            Assert.That(status.LatestGameDate, Is.Null);
        });
    }

    [Test]
    public async Task Ingestion_status_reports_the_most_recent_run_and_latest_game()
    {
        AddTeam(21, "HME");
        AddTeam(22, "AWY");
        AddGame(2025020001, 0, 21, 22);
        AddGame(2025020002, 30, 21, 22);
        Db.IngestionRuns.Add(new IngestionRun
        {
            Kind = "backfill", StartedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            CompletedUtc = DateTimeOffset.UtcNow, Status = IngestionStatus.Succeeded, GamesIngested = 2,
        });
        Db.IngestionRuns.Add(new IngestionRun
        {
            Kind = "daily", StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow, Status = IngestionStatus.Failed, Error = "boom",
        });
        await SaveAsync();

        var status = await Queries.GetIngestionStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.GamesInDatabase, Is.EqualTo(2));
            Assert.That(status.LastRunKind, Is.EqualTo("daily"), "the most recent run, by id");
            Assert.That(status.LastRunStatus, Is.EqualTo("Failed"));
            Assert.That(status.LastRunError, Is.EqualTo("boom"));
            Assert.That(status.LatestGameDate, Is.EqualTo(new DateOnly(2025, 11, 7)));
        });
    }

    [Test]
    public async Task A_player_can_be_fetched_by_id_and_is_null_when_absent()
    {
        AddPlayer(1, "Known", "Player");
        await SaveAsync();

        Assert.Multiple(async () =>
        {
            Assert.That((await Queries.GetPlayerAsync(1))?.FullName, Is.EqualTo("Known Player"));
            Assert.That(await Queries.GetPlayerAsync(999), Is.Null);
        });
    }
}
