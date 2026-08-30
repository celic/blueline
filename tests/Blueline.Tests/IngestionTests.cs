using Blueline.Core.Dtos;
using Blueline.Core.Entities;
using Blueline.Data;
using Blueline.Data.Queries;
using Blueline.Ingestion;
using Blueline.Ingestion.Nhl;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueline.Tests;

/// <summary>
/// Exercises ingestion end to end against a real (in-memory) SQLite database and a stubbed API.
/// </summary>
public class IngestionTests
{
    private SqliteConnection _connection = null!;
    private BluelineDbContext _db = null!;

    [SetUp]
    public void SetUp()
    {
        // A shared in-memory database lives as long as the connection is open.
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new BluelineDbContext(options);
        _db.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private NhlIngestionService BuildService(StubNhlApi stub)
    {
        var http = new HttpClient(stub) { BaseAddress = new Uri(NhlApiClient.DefaultBaseAddress) };
        var api = new NhlApiClient(http, NullLogger<NhlApiClient>.Instance);
        return new NhlIngestionService(_db, api, NullLogger<NhlIngestionService>.Instance);
    }

    /// <summary>Two games on one night that share the home team — the case that once produced duplicates.</summary>
    private static StubNhlApi TwoGamesSharingATeam() => new StubNhlApi()
        .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002))
        .Add("gamecenter/2025020001/boxscore",
            StubNhlApi.Boxscore(2025020001, "2026-01-15", homeTeamId: 21, "HME", awayTeamId: 22, "AWY",
                homeScore: 1, awayScore: 3))
        .Add("gamecenter/2025020002/boxscore",
            StubNhlApi.Boxscore(2025020002, "2026-01-15", homeTeamId: 21, "HME", awayTeamId: 23, "OTH",
                homeScore: 4, awayScore: 2));

    [Test]
    public async Task Games_sharing_a_team_in_one_batch_are_ingested_without_duplicating_it()
    {
        var service = BuildService(TwoGamesSharingATeam());

        var count = await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        Assert.Multiple(async () =>
        {
            Assert.That(count, Is.EqualTo(2));
            Assert.That(await _db.Games.CountAsync(), Is.EqualTo(2));
            Assert.That(await _db.Teams.CountAsync(), Is.EqualTo(3), "the shared home team must be stored once");
        });
    }

    [Test]
    public async Task Re_ingesting_the_same_night_updates_rows_instead_of_adding_them()
    {
        var service = BuildService(TwoGamesSharingATeam());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        // A second pass is exactly what the daily job's lookback window does.
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.Games.CountAsync(), Is.EqualTo(2));
            Assert.That(await _db.Teams.CountAsync(), Is.EqualTo(3));
            Assert.That(await _db.SkaterGameStats.CountAsync(), Is.EqualTo(4));
            Assert.That(await _db.GoalieGameStats.CountAsync(), Is.EqualTo(4));
            Assert.That(await _db.TeamGameStats.CountAsync(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task A_corrected_box_score_overwrites_the_stored_stat_line()
    {
        var first = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3, homeSkaterGoals: 1));

        await BuildService(first).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        // The league revises the game: the home forward is credited with a second goal.
        var corrected = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3, homeSkaterGoals: 2));

        await BuildService(corrected).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var stat = await _db.SkaterGameStats.SingleAsync(s => s.PlayerId == 101);
        Assert.Multiple(() =>
        {
            Assert.That(stat.Goals, Is.EqualTo(2));
            Assert.That(stat.Points, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task A_regulation_result_gives_the_winner_two_points_and_the_loser_none()
    {
        var service = BuildService(TwoGamesSharingATeam());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var home = await _db.TeamGameStats.SingleAsync(s => s.GameId == 2025020001 && s.TeamId == 21);
        var away = await _db.TeamGameStats.SingleAsync(s => s.GameId == 2025020001 && s.TeamId == 22);

        Assert.Multiple(() =>
        {
            Assert.That(away.Result, Is.EqualTo("W"));
            Assert.That(away.Points, Is.EqualTo(2));
            Assert.That(home.Result, Is.EqualTo("L"));
            Assert.That(home.Points, Is.Zero);
            Assert.That(home.IsHome, Is.True);
            Assert.That(home.GoalsFor, Is.EqualTo(1));
            Assert.That(home.GoalsAgainst, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Losing_past_regulation_still_banks_a_point()
    {
        var stub = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY",
                    homeScore: 2, awayScore: 3, lastPeriodType: "OT"));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var home = await _db.TeamGameStats.SingleAsync(s => s.TeamId == 21);
        Assert.Multiple(() =>
        {
            Assert.That(home.Result, Is.EqualTo("OTL"));
            Assert.That(home.Points, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Time_on_ice_is_stored_in_seconds()
    {
        await BuildService(TwoGamesSharingATeam()).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var stat = await _db.SkaterGameStats.FirstAsync(s => s.PlayerId == 100);
        Assert.That(stat.TimeOnIceSeconds, Is.EqualTo(18 * 60 + 30));
    }

    [Test]
    public async Task A_failed_run_is_recorded_rather_than_leaving_the_run_marked_running()
    {
        // Nothing registered, so every call 404s and no games are found.
        await BuildService(new StubNhlApi()).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var run = await _db.IngestionRuns.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(IngestionStatus.Succeeded));
            Assert.That(run.GamesIngested, Is.Zero);
            Assert.That(run.CompletedUtc, Is.Not.Null);
        });
    }

    /// <summary>A regular-season game and a playoff game, each an overtime home loss.</summary>
    private static StubNhlApi RegularAndPlayoffOvertimeLosses() => new StubNhlApi()
        .Add("score/2026-01-15", StubNhlApi.ScoreOfType("2026-01-15", 2, 2025020001))
        .Add("gamecenter/2025020001/boxscore",
            StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY",
                homeScore: 2, awayScore: 3, lastPeriodType: "OT", gameType: 2))
        .Add("score/2026-05-15", StubNhlApi.ScoreOfType("2026-05-15", 3, 2025030001))
        .Add("gamecenter/2025030001/boxscore",
            StubNhlApi.Boxscore(2025030001, "2026-05-15", 21, "HME", 22, "AWY",
                homeScore: 2, awayScore: 3, lastPeriodType: "OT", gameType: 3));

    [Test]
    public async Task A_playoff_overtime_loss_banks_no_point_unlike_a_regular_season_one()
    {
        var service = BuildService(RegularAndPlayoffOvertimeLosses());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);
        await service.IngestRecentAsync(new DateOnly(2026, 5, 15), lookbackDays: 0);

        var regular = await _db.TeamGameStats.SingleAsync(s => s.GameId == 2025020001 && s.TeamId == 21);
        var playoff = await _db.TeamGameStats.SingleAsync(s => s.GameId == 2025030001 && s.TeamId == 21);

        Assert.Multiple(() =>
        {
            Assert.That(regular.Result, Is.EqualTo("OTL"));
            Assert.That(regular.Points, Is.EqualTo(1));

            // There is no loser point in the playoffs; an overtime loss is simply a loss.
            Assert.That(playoff.Result, Is.EqualTo("L"));
            Assert.That(playoff.Points, Is.Zero);
        });
    }

    [Test]
    public async Task A_playoff_win_awards_no_standings_points()
    {
        var stub = new StubNhlApi()
            .Add("score/2026-05-15", StubNhlApi.ScoreOfType("2026-05-15", 3, 2025030001))
            .Add("gamecenter/2025030001/boxscore",
                StubNhlApi.Boxscore(2025030001, "2026-05-15", 21, "HME", 22, "AWY",
                    homeScore: 4, awayScore: 2, gameType: 3));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 5, 15), lookbackDays: 0);

        var winner = await _db.TeamGameStats.SingleAsync(s => s.TeamId == 21);
        Assert.Multiple(() =>
        {
            Assert.That(winner.Result, Is.EqualTo("W"), "the result is still a win");
            Assert.That(winner.Points, Is.Zero, "but the playoffs award no standings points");
        });
    }

    [Test]
    public async Task A_combined_season_reports_only_the_regular_seasons_standings_points()
    {
        var service = BuildService(RegularAndPlayoffOvertimeLosses());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);
        await service.IngestRecentAsync(new DateOnly(2026, 5, 15), lookbackDays: 0);

        var queries = new StatsQueryService(_db);
        var regular = (await queries.GetTeamsAsync(20252026, GameScope.RegularSeason)).Single(t => t.Id == 21);
        var combined = (await queries.GetTeamsAsync(20252026, GameScope.All)).Single(t => t.Id == 21);

        Assert.Multiple(() =>
        {
            Assert.That(regular.StandingsPoints, Is.EqualTo(1), "one regular-season overtime loss");
            Assert.That(combined.GamesPlayed, Is.EqualTo(2), "the playoff game still counts as a game");
            Assert.That(combined.StandingsPoints, Is.EqualTo(1),
                "adding the playoff game must not inflate the standings total");
        });
    }

    [Test]
    public async Task Scope_decides_which_games_a_trend_counts()
    {
        var service = BuildService(RegularAndPlayoffOvertimeLosses());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);
        await service.IngestRecentAsync(new DateOnly(2026, 5, 15), lookbackDays: 0);

        var queries = new StatsQueryService(_db);

        var regular = await queries.GetPlayerTrendAsync(101, 20252026, "goals", 1, GameScope.RegularSeason);
        var playoffs = await queries.GetPlayerTrendAsync(101, 20252026, "goals", 1, GameScope.Playoffs);
        var combined = await queries.GetPlayerTrendAsync(101, 20252026, "goals", 1, GameScope.All);

        Assert.Multiple(() =>
        {
            Assert.That(regular!.Points, Has.Count.EqualTo(1));
            Assert.That(playoffs!.Points, Has.Count.EqualTo(1));
            Assert.That(combined!.Points, Has.Count.EqualTo(2), "combined counts both games");
            Assert.That(combined.Total, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Scope_decides_which_games_a_leaderboard_counts()
    {
        var service = BuildService(RegularAndPlayoffOvertimeLosses());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);
        await service.IngestRecentAsync(new DateOnly(2026, 5, 15), lookbackDays: 0);

        var queries = new StatsQueryService(_db);

        var regular = await queries.GetLeadersAsync(20252026, "points", 10, GameScope.RegularSeason);
        var combined = await queries.GetLeadersAsync(20252026, "points", 10, GameScope.All);

        Assert.Multiple(() =>
        {
            Assert.That(regular[0].GamesPlayed, Is.EqualTo(1));
            Assert.That(combined[0].GamesPlayed, Is.EqualTo(2));
            Assert.That(combined[0].Value, Is.EqualTo(regular[0].Value * 2));
        });
    }

    [Test]
    public async Task Season_summaries_report_the_regular_and_playoff_split()
    {
        var service = BuildService(RegularAndPlayoffOvertimeLosses());
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);
        await service.IngestRecentAsync(new DateOnly(2026, 5, 15), lookbackDays: 0);

        var season = (await new StatsQueryService(_db).GetSeasonsAsync()).Single();

        Assert.Multiple(() =>
        {
            Assert.That(season.GameCount, Is.EqualTo(2));
            Assert.That(season.RegularSeasonGames, Is.EqualTo(1));
            Assert.That(season.PlayoffGames, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Full_names_replace_the_abbreviations_the_box_score_carries()
    {
        var stub = TwoGamesSharingATeam()
            .Add("club-stats/HME/20252026/2", StubNhlApi.ClubStats(
                (101, "Hometown", "Forward", "L"), (201, "Hometown", "Goalie", "G")))
            .Add("club-stats/AWY/20252026/2", StubNhlApi.ClubStats(
                (100, "Awayville", "Forward", "C"), (200, "Awayville", "Goalie", "G")));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var forward = await _db.Players.SingleAsync(p => p.Id == 101);
        var goalie = await _db.Players.SingleAsync(p => p.Id == 201);

        Assert.Multiple(() =>
        {
            // The box score only supplied "H. Forward".
            Assert.That(forward.FullName, Is.EqualTo("Hometown Forward"));
            Assert.That(forward.HeadshotUrl, Is.EqualTo("https://example.test/101.png"));
            Assert.That(goalie.FullName, Is.EqualTo("Hometown Goalie"));
            Assert.That(goalie.Position, Is.EqualTo("G"));
        });
    }

    [Test]
    public async Task A_player_missing_from_the_roster_keeps_their_abbreviated_name()
    {
        // Someone who played once and was gone before the end-of-season roster was published.
        var stub = TwoGamesSharingATeam()
            .Add("club-stats/HME/20252026/2", StubNhlApi.ClubStats((101, "Hometown", "Forward", "L")));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var unresolved = await _db.Players.SingleAsync(p => p.Id == 100);

        Assert.Multiple(() =>
        {
            Assert.That(unresolved.LastName, Is.EqualTo("Forward"), "their stats are still correct");
            Assert.That(NhlIngestionService.NeedsRealName(unresolved), Is.True, "only the display name is short");
        });
    }

    [Test]
    public async Task The_roster_lookup_is_skipped_once_every_player_has_a_real_name()
    {
        var stub = TwoGamesSharingATeam()
            .Add("club-stats/HME/20252026/2", StubNhlApi.ClubStats(
                (101, "Hometown", "Forward", "L"), (201, "Hometown", "Goalie", "G")))
            .Add("club-stats/AWY/20252026/2", StubNhlApi.ClubStats(
                (100, "Awayville", "Forward", "C"), (200, "Awayville", "Goalie", "G")))
            .Add("club-stats/OTH/20252026/2", StubNhlApi.ClubStats());

        var service = BuildService(stub);
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var callsAfterFirstPass = stub.RequestedPaths.Count(p => p.StartsWith("club-stats"));
        await service.IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);
        var callsAfterSecondPass = stub.RequestedPaths.Count(p => p.StartsWith("club-stats"));

        Assert.Multiple(() =>
        {
            Assert.That(callsAfterFirstPass, Is.GreaterThan(0), "the first pass has names to resolve");
            Assert.That(callsAfterSecondPass, Is.EqualTo(callsAfterFirstPass),
                "a daily run over already-named players must not re-fetch every club roster");
        });
    }

    [Test]
    public async Task A_game_whose_box_score_cannot_be_read_is_recorded_rather_than_skipped_silently()
    {
        // The score lists two games but only one box score is available.
        var stub = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3));

        var ingested = await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var run = await _db.IngestionRuns.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(ingested, Is.EqualTo(1), "the readable game is still stored");
            Assert.That(run.GamesFailed, Is.EqualTo(1));
            Assert.That(run.FailedGameIds, Is.EqualTo("2025020002"),
                "the identifier must be recorded so a later pass knows what to re-fetch");
        });
    }

    [Test]
    public async Task One_unreadable_game_does_not_abandon_the_rest_of_the_batch()
    {
        // The failure sits first, so a naive implementation would lose the games behind it.
        var stub = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002, 2025020003))
            .Add("gamecenter/2025020002/boxscore",
                StubNhlApi.Boxscore(2025020002, "2026-01-15", 21, "HME", 22, "AWY", 1, 3))
            .Add("gamecenter/2025020003/boxscore",
                StubNhlApi.Boxscore(2025020003, "2026-01-15", 21, "HME", 23, "OTH", 4, 2));

        var ingested = await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        Assert.Multiple(async () =>
        {
            Assert.That(ingested, Is.EqualTo(2));
            Assert.That(await _db.Games.CountAsync(), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task A_failed_game_is_attributed_to_the_right_identifier()
    {
        // Pairing responses back to their ids by position is easy to get subtly wrong.
        var stub = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002, 2025020003))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3))
            .Add("gamecenter/2025020003/boxscore",
                StubNhlApi.Boxscore(2025020003, "2026-01-15", 21, "HME", 23, "OTH", 4, 2));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var run = await _db.IngestionRuns.SingleAsync();

        Assert.That(run.FailedGameIds, Is.EqualTo("2025020002"), "the middle game is the missing one");
    }

    [Test]
    public async Task A_run_with_failures_still_succeeds_so_the_rest_of_the_night_is_kept()
    {
        var stub = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var run = await _db.IngestionRuns.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(IngestionStatus.Succeeded));
            Assert.That(run.Error, Is.Null, "a partial shortfall is not a run-level error");
        });
    }

    [Test]
    public async Task A_clean_run_records_no_failures()
    {
        await BuildService(TwoGamesSharingATeam()).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var run = await _db.IngestionRuns.SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(run.GamesFailed, Is.Zero);
            Assert.That(run.FailedGameIds, Is.Null, "null rather than an empty string, so the column reads cleanly");
        });
    }

    [Test]
    public async Task The_ingestion_status_reports_the_shortfall()
    {
        var stub = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3));

        await BuildService(stub).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var status = await new StatsQueryService(_db).GetIngestionStatusAsync();

        Assert.Multiple(() =>
        {
            Assert.That(status.LastRunGamesFailed, Is.EqualTo(1));
            Assert.That(status.LastRunFailedGameIds, Is.EqualTo("2025020002"));
        });
    }

    [Test]
    public void A_long_list_of_failures_is_truncated_but_still_counted()
    {
        var ids = Enumerable.Range(1, NhlIngestionService.MaxRecordedFailedIds + 10)
            .Select(i => (long)(2025020000 + i))
            .ToList();

        var formatted = NhlIngestionService.FormatFailedIds(ids);

        Assert.Multiple(() =>
        {
            Assert.That(formatted, Does.Contain("(10 more)"));
            Assert.That(formatted!.Split(',').Length, Is.LessThanOrEqualTo(NhlIngestionService.MaxRecordedFailedIds + 1));
        });
    }

    [Test]
    public void No_failures_formats_as_null_rather_than_an_empty_string() =>
        Assert.That(NhlIngestionService.FormatFailedIds([]), Is.Null);

    [Test]
    public async Task Trends_read_back_the_games_that_were_ingested()
    {
        await BuildService(TwoGamesSharingATeam()).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var queries = new StatsQueryService(_db);
        var trend = await queries.GetPlayerTrendAsync(101, 20252026, "goals", rollingWindow: 1);

        Assert.That(trend, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // Player 101 is the home forward in both games, scoring once each time.
            Assert.That(trend!.Points, Has.Count.EqualTo(2));
            Assert.That(trend.Total, Is.EqualTo(2));
            Assert.That(trend.Points[1].Cumulative, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Season_leaders_aggregate_across_games()
    {
        await BuildService(TwoGamesSharingATeam()).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var leaders = await new StatsQueryService(_db).GetLeadersAsync(20252026, "points", take: 10);

        // The away forward records two points per game across two games; the home forward one each.
        Assert.That(leaders[0].Value, Is.EqualTo(4));
        Assert.That(leaders[0].GamesPlayed, Is.EqualTo(2));
    }
}
