using Blueline.Core.Entities;
using Blueline.Data;
using Blueline.Web.Health;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blueline.Tests;

public class HealthCheckTests
{
    private SqliteConnection _connection = null!;
    private BluelineDbContext _db = null!;

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

    private static HealthCheckContext Context() => new()
    {
        Registration = new HealthCheckRegistration("test", _ => null!, HealthStatus.Unhealthy, tags: null),
    };

    private void AddGame(long id = 2025020001)
    {
        _db.Teams.Add(new Team { Id = 21, Abbrev = "HME", Name = "Home" });
        _db.Teams.Add(new Team { Id = 22, Abbrev = "AWY", Name = "Away" });
        _db.Games.Add(new Game
        {
            Id = id,
            SeasonId = 20252026,
            GameType = GameTypes.Regular,
            GameDate = new DateOnly(2026, 1, 15),
            HomeTeamId = 21,
            AwayTeamId = 22,
            GameState = "OFF",
        });
        _db.SaveChanges();
    }

    private void AddRun(IngestionStatus status, int gamesFailed = 0, string? error = null)
    {
        _db.IngestionRuns.Add(new IngestionRun
        {
            Kind = "daily",
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Status = status,
            GamesFailed = gamesFailed,
            Error = error,
        });
        _db.SaveChanges();
    }

    // --- liveness ---

    [Test]
    public async Task The_database_check_is_healthy_when_the_database_answers()
    {
        var result = await new DatabaseHealthCheck(_db).CheckHealthAsync(Context());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task The_database_check_is_unhealthy_when_the_database_cannot_be_opened()
    {
        // A path under a directory that does not exist. Closing the in-memory connection would
        // not do: EF simply reopens it onto a fresh empty database and reports itself healthy.
        var unreachable = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing", "x.db");

        using var db = new BluelineDbContext(new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite($"Data Source={unreachable}").Options);

        var result = await new DatabaseHealthCheck(db).CheckHealthAsync(Context());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    // --- readiness ---

    [Test]
    public async Task An_empty_database_is_not_ready_but_is_not_a_reason_to_restart()
    {
        // A fresh deployment seeds for several minutes. Readiness says "not yet"; liveness must
        // stay healthy, or the host kills the seed and starts it over indefinitely.
        var ready = await new IngestionHealthCheck(_db).CheckHealthAsync(Context());
        var live = await new DatabaseHealthCheck(_db).CheckHealthAsync(Context());

        Assert.Multiple(() =>
        {
            Assert.That(ready.Status, Is.EqualTo(HealthStatus.Unhealthy));
            Assert.That(live.Status, Is.EqualTo(HealthStatus.Healthy));
        });
    }

    [Test]
    public async Task A_database_with_games_and_a_clean_run_is_healthy()
    {
        AddGame();
        AddRun(IngestionStatus.Succeeded);

        var result = await new IngestionHealthCheck(_db).CheckHealthAsync(Context());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Data["gamesStored"], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_failed_last_run_is_degraded_rather_than_unhealthy()
    {
        // The site still serves everything already stored, so this must not read as down.
        AddGame();
        AddRun(IngestionStatus.Failed, error: "the league API went away");

        var result = await new IngestionHealthCheck(_db).CheckHealthAsync(Context());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("the league API went away"));
        });
    }

    [Test]
    public async Task Games_the_last_run_could_not_read_show_as_degraded()
    {
        AddGame();
        AddRun(IngestionStatus.Succeeded, gamesFailed: 3);

        var result = await new IngestionHealthCheck(_db).CheckHealthAsync(Context());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Degraded));
            Assert.That(result.Description, Does.Contain("reconcile"), "the report should name the remedy");
            Assert.That(result.Data["lastRunGamesFailed"], Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Data_stored_without_any_run_recorded_is_still_healthy()
    {
        // A database restored from a backup has games but no run history.
        AddGame();

        var result = await new IngestionHealthCheck(_db).CheckHealthAsync(Context());

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
            Assert.That(result.Data["lastRunKind"], Is.EqualTo("none"));
        });
    }

    [Test]
    public async Task The_report_carries_enough_detail_to_diagnose_without_a_shell()
    {
        AddGame();
        AddRun(IngestionStatus.Succeeded);

        var result = await new IngestionHealthCheck(_db).CheckHealthAsync(Context());

        Assert.That(result.Data.Keys, Is.SupersetOf(new[]
        {
            "gamesStored", "lastRunKind", "lastRunStatus", "lastRunCompletedUtc", "lastRunGamesFailed",
        }));
    }
}
