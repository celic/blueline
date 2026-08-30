using Blueline.Core.Entities;
using Blueline.Data;
using Blueline.Ingestion;
using Blueline.Ingestion.Nhl;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueline.Tests;

/// <summary>
/// Reconciliation: the safety net for gaps the daily job's short lookback would never notice.
/// </summary>
public class ReconcileTests
{
    private SqliteConnection _connection = null!;
    private BluelineDbContext _db = null!;

    private const int SeasonId = 20252026;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _db = new BluelineDbContext(new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite(_connection).Options);
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
        return new NhlIngestionService(_db, new NhlApiClient(http, NullLogger<NhlApiClient>.Instance),
            NullLogger<NhlIngestionService>.Instance);
    }

    /// <summary>Two clubs, a three-game season, and box scores available for all of them.</summary>
    private static StubNhlApi SeasonOfThreeGames()
    {
        var games = new (long, int, string, int, string, int, string)[]
        {
            (2025020001, GameTypes.Regular, "2026-01-15", 21, "HME", 22, "AWY"),
            (2025020002, GameTypes.Regular, "2026-01-16", 21, "HME", 22, "AWY"),
            (2025020003, GameTypes.Regular, "2026-01-17", 22, "AWY", 21, "HME"),
        };

        var schedule = StubNhlApi.ClubSchedule(games);

        return new StubNhlApi()
            .Add("standings/2026-04-01", StubNhlApi.Standings("HME", "AWY"))
            .Add("club-schedule-season/HME/20252026", schedule)
            .Add("club-schedule-season/AWY/20252026", schedule)
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3))
            .Add("gamecenter/2025020002/boxscore",
                StubNhlApi.Boxscore(2025020002, "2026-01-16", 21, "HME", 22, "AWY", 4, 2))
            .Add("gamecenter/2025020003/boxscore",
                StubNhlApi.Boxscore(2025020003, "2026-01-17", 22, "AWY", 21, "HME", 2, 5));
    }

    [Test]
    public async Task Reconciling_an_empty_database_ingests_the_whole_season()
    {
        var count = await BuildService(SeasonOfThreeGames()).ReconcileSeasonAsync(SeasonId);

        Assert.Multiple(async () =>
        {
            Assert.That(count, Is.EqualTo(3));
            Assert.That(await _db.Games.CountAsync(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Reconciling_fills_only_the_gap_and_leaves_the_rest_alone()
    {
        var stub = SeasonOfThreeGames();
        var service = BuildService(stub);

        // Simulate the app being down for the middle night: ingest the outer two only.
        await service.ReconcileSeasonAsync(SeasonId);
        var middle = await _db.Games.SingleAsync(g => g.Id == 2025020002);
        _db.Games.Remove(middle);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var boxscoreCallsBefore = stub.RequestedPaths.Count(p => p.StartsWith("gamecenter"));
        var count = await service.ReconcileSeasonAsync(SeasonId);
        var boxscoreCallsAfter = stub.RequestedPaths.Count(p => p.StartsWith("gamecenter"));

        Assert.Multiple(async () =>
        {
            Assert.That(count, Is.EqualTo(1), "only the missing game is ingested");
            Assert.That(await _db.Games.CountAsync(), Is.EqualTo(3));
            Assert.That(boxscoreCallsAfter - boxscoreCallsBefore, Is.EqualTo(1),
                "the games already stored must not be re-fetched");
        });
    }

    [Test]
    public async Task Reconciling_a_complete_season_fetches_no_box_scores_at_all()
    {
        var stub = SeasonOfThreeGames();
        var service = BuildService(stub);
        await service.ReconcileSeasonAsync(SeasonId);

        var before = stub.RequestedPaths.Count(p => p.StartsWith("gamecenter"));
        var count = await service.ReconcileSeasonAsync(SeasonId);
        var after = stub.RequestedPaths.Count(p => p.StartsWith("gamecenter"));

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.Zero);
            Assert.That(after, Is.EqualTo(before), "a clean season should cost only the schedule walk");
        });
    }

    [Test]
    public async Task A_game_stored_without_any_player_stats_is_re_read()
    {
        var stub = SeasonOfThreeGames();
        var service = BuildService(stub);
        await service.ReconcileSeasonAsync(SeasonId);

        // A half-applied box score: the game row survives but nobody played in it.
        var orphaned = await _db.SkaterGameStats.Where(s => s.GameId == 2025020002).ToListAsync();
        _db.SkaterGameStats.RemoveRange(orphaned);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var count = await service.ReconcileSeasonAsync(SeasonId);

        Assert.Multiple(async () =>
        {
            Assert.That(count, Is.EqualTo(1), "a game with no stat lines charts as though nobody played");
            Assert.That(await _db.SkaterGameStats.CountAsync(s => s.GameId == 2025020002), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task Reconciling_picks_up_the_games_an_earlier_run_recorded_as_failed()
    {
        // A night where one box score was unavailable, exactly as the daily job would leave it.
        var partial = new StubNhlApi()
            .Add("score/2026-01-15", StubNhlApi.Score("2026-01-15", 2025020001, 2025020002))
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3));

        await BuildService(partial).IngestRecentAsync(new DateOnly(2026, 1, 15), lookbackDays: 0);

        var failedRun = await _db.IngestionRuns.OrderByDescending(r => r.Id).FirstAsync();
        Assert.That(failedRun.FailedGameIds, Is.EqualTo("2025020002"), "precondition");

        // Later, the API is healthy again.
        var count = await BuildService(SeasonOfThreeGames()).ReconcileSeasonAsync(SeasonId);

        Assert.Multiple(async () =>
        {
            Assert.That(await _db.Games.AnyAsync(g => g.Id == 2025020002), Is.True);
            Assert.That(count, Is.EqualTo(2), "the previously failed game plus the one never attempted");
        });
    }

    [Test]
    public async Task A_reconcile_run_is_recorded_under_its_own_kind()
    {
        await BuildService(SeasonOfThreeGames()).ReconcileSeasonAsync(SeasonId);

        var run = await _db.IngestionRuns.OrderByDescending(r => r.Id).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(run.Kind, Is.EqualTo("reconcile"));
            Assert.That(run.SeasonId, Is.EqualTo(SeasonId));
            Assert.That(run.Status, Is.EqualTo(IngestionStatus.Succeeded));
            Assert.That(run.GamesIngested, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task A_clean_reconcile_still_completes_its_run_record()
    {
        var service = BuildService(SeasonOfThreeGames());
        await service.ReconcileSeasonAsync(SeasonId);
        await service.ReconcileSeasonAsync(SeasonId);

        var run = await _db.IngestionRuns.OrderByDescending(r => r.Id).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(IngestionStatus.Succeeded));
            Assert.That(run.GamesIngested, Is.Zero);
            Assert.That(run.CompletedUtc, Is.Not.Null, "a no-op run must still be closed out");
        });
    }

    [Test]
    public void Reconciling_a_season_with_no_resolvable_teams_fails_loudly()
    {
        // Nothing registered, so standings 404 and there are no stored teams to fall back on.
        var service = BuildService(new StubNhlApi());

        Assert.ThrowsAsync<InvalidOperationException>(() => service.ReconcileSeasonAsync(SeasonId));
    }

    [Test]
    public async Task A_failed_reconcile_is_recorded_rather_than_left_running()
    {
        try
        {
            await BuildService(new StubNhlApi()).ReconcileSeasonAsync(SeasonId);
        }
        catch (InvalidOperationException)
        {
            // Expected — the assertion is about what was written down.
        }

        var run = await _db.IngestionRuns.OrderByDescending(r => r.Id).FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(run.Status, Is.EqualTo(IngestionStatus.Failed));
            Assert.That(run.Error, Is.Not.Null);
            Assert.That(run.CompletedUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task Preseason_games_on_the_schedule_are_never_reconciled_in()
    {
        var games = new (long, int, string, int, string, int, string)[]
        {
            (2025010001, GameTypes.Preseason, "2025-09-21", 21, "HME", 22, "AWY"),
            (2025020001, GameTypes.Regular, "2026-01-15", 21, "HME", 22, "AWY"),
        };
        var schedule = StubNhlApi.ClubSchedule(games);

        var stub = new StubNhlApi()
            .Add("standings/2026-04-01", StubNhlApi.Standings("HME", "AWY"))
            .Add("club-schedule-season/HME/20252026", schedule)
            .Add("club-schedule-season/AWY/20252026", schedule)
            .Add("gamecenter/2025020001/boxscore",
                StubNhlApi.Boxscore(2025020001, "2026-01-15", 21, "HME", 22, "AWY", 1, 3));

        var count = await BuildService(stub).ReconcileSeasonAsync(SeasonId);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(1));
            Assert.That(stub.RequestedPaths, Does.Not.Contain("gamecenter/2025010001/boxscore"),
                "preseason stats count towards nothing and must not be chased");
        });
    }
}
