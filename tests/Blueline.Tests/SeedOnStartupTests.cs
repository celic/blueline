using Blueline.Core.Entities;
using Blueline.Data;
using Blueline.Ingestion;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Blueline.Tests;

/// <summary>
/// Seeding an empty database is not part of the daily schedule, and these tests hold the two
/// apart.
///
/// They were gated together: one switch turned off both. That mattered once ingestion moved out
/// of the site, because the deployment that most needs seeding — a fresh one, with a scheduled
/// job running elsewhere — is exactly the one that would have had it switched off, coming up
/// permanently empty with nothing saying why.
/// </summary>
public class SeedOnStartupTests
{
    private readonly List<SqliteConnection> _connections = [];
    private string _archiveDirectory = "";

    private const int SeasonId = 20252026;
    private const int GamesInArchive = 3;

    [SetUp]
    public void SetUp()
    {
        _archiveDirectory = Path.Combine(Path.GetTempPath(), "blueline-seed", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_archiveDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var connection in _connections) connection.Dispose();
        _connections.Clear();

        try
        {
            if (Directory.Exists(_archiveDirectory)) Directory.Delete(_archiveDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Not worth failing a test over.
        }
    }

    [Test]
    public async Task An_empty_database_is_seeded_even_though_the_daily_job_is_off()
    {
        await WriteArchiveAsync();
        var target = NewDatabase();

        await RunWorkerAsync(target, Settings(dailyJobEnabled: false));

        Assert.That(await CountGamesAsync(target), Is.EqualTo(GamesInArchive),
            "a deployment whose schedule lives outside the site still needs something to serve");
    }

    [Test]
    public async Task Seeding_is_still_switched_off_by_its_own_setting()
    {
        await WriteArchiveAsync();
        var target = NewDatabase();

        await RunWorkerAsync(target, Settings(dailyJobEnabled: false, seedSeasonId: 0));

        Assert.That(await CountGamesAsync(target), Is.Zero,
            "SeedSeasonId is what disables seeding, and it still does");
    }

    [Test]
    public async Task A_database_that_already_has_games_is_left_alone()
    {
        await WriteArchiveAsync();
        var target = NewDatabase();

        await using (var db = NewContext(target))
        {
            Seed(db, games: 1, firstGameId: 999);
            await db.SaveChangesAsync();
        }

        await RunWorkerAsync(target, Settings(dailyJobEnabled: false));

        Assert.That(await CountGamesAsync(target), Is.EqualTo(1),
            "seeding triggers on an empty database, and this one is not");
    }

    /// <summary>Exports a small season so the worker has a real archive to find.</summary>
    private async Task WriteArchiveAsync()
    {
        var source = NewDatabase();

        await using var db = NewContext(source);
        Seed(db, GamesInArchive);
        await db.SaveChangesAsync();

        var archive = new SeasonArchive(db, NullLogger<SeasonArchive>.Instance);
        await archive.ExportAsync(
            SeasonId,
            Path.Combine(_archiveDirectory, $"{SeasonId}{DailyIngestionWorker.ArchiveExtension}"));
    }

    /// <summary>
    /// Runs the worker to completion. With the daily job off it seeds and returns, so awaiting
    /// the execute task is enough — there is no schedule left running to stop.
    /// </summary>
    private async Task RunWorkerAsync(SqliteConnection connection, IngestionOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<BluelineDbContext>(builder => builder.UseSqlite(connection));
        services.AddScoped<SeasonArchive>();

        await using var provider = services.BuildServiceProvider();

        var worker = new DailyIngestionWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            TimeProvider.System,
            NullLogger<DailyIngestionWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        if (worker.ExecuteTask is { } running) await running;
        await worker.StopAsync(CancellationToken.None);
    }

    private IngestionOptions Settings(bool dailyJobEnabled, int seedSeasonId = SeasonId) => new()
    {
        DailyJobEnabled = dailyJobEnabled,
        RunOnStartup = false,
        SeedSeasonId = seedSeasonId,
        SeedArchiveDirectory = _archiveDirectory,
    };

    private SqliteConnection NewDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        _connections.Add(connection);

        using var db = NewContext(connection);
        db.Database.EnsureCreated();
        return connection;
    }

    private static BluelineDbContext NewContext(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<BluelineDbContext>().UseSqlite(connection).Options);

    private static async Task<int> CountGamesAsync(SqliteConnection connection)
    {
        await using var db = NewContext(connection);
        return await db.Games.CountAsync();
    }

    private static void Seed(BluelineDbContext db, int games, long firstGameId = 1)
    {
        db.Teams.Add(new Team { Id = 21, Abbrev = "HME", Name = "Home Club" });
        db.Teams.Add(new Team { Id = 22, Abbrev = "AWY", Name = "Away Club" });

        for (var i = 0; i < games; i++)
        {
            db.Games.Add(new Game
            {
                Id = firstGameId + i,
                SeasonId = SeasonId,
                GameType = GameTypes.Regular,
                GameDate = new DateOnly(2025, 10, 8).AddDays(i),
                HomeTeamId = 21,
                AwayTeamId = 22,
                HomeScore = 3,
                AwayScore = 2,
                LastPeriodType = "REG",
                GameState = "OFF",
            });
        }
    }
}
