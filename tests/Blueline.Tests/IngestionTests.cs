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
