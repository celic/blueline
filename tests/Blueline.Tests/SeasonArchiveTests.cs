using System.Text.Json;
using Blueline.Core.Entities;
using Blueline.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blueline.Tests;

/// <summary>
/// Exporting a season and loading it back. This is what lets a deployment install a finished
/// season instead of re-ingesting it, so a round trip that quietly loses or duplicates rows would
/// be worse than not having the feature.
/// </summary>
public class SeasonArchiveTests
{
    private readonly List<SqliteConnection> _connections = [];
    private string _directory = "";

    private const int SeasonId = 20252026;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "blueline-archive", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var connection in _connections) connection.Dispose();
        _connections.Clear();

        try
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // Not worth failing a test over.
        }
    }

    private BluelineDbContext NewDatabase()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        _connections.Add(connection);

        var db = new BluelineDbContext(new DbContextOptionsBuilder<BluelineDbContext>()
            .UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static SeasonArchive ArchiveFor(BluelineDbContext db) =>
        new(db, NullLogger<SeasonArchive>.Instance);

    private static void Seed(BluelineDbContext db, int games = 4, int seasonId = SeasonId)
    {
        if (!db.Teams.Any())
        {
            db.Teams.Add(new Team { Id = 21, Abbrev = "HME", Name = "Home Club" });
            db.Teams.Add(new Team { Id = 22, Abbrev = "AWY", Name = "Away Club" });
            db.Players.Add(new Player { Id = 100, FirstName = "Alexis", LastName = "Skater", Position = "C" });
            db.Players.Add(new Player { Id = 200, FirstName = "Casper", LastName = "Goalie", Position = "G" });
        }

        for (var i = 0; i < games; i++)
        {
            var gameId = seasonId * 100L + i;

            db.Games.Add(new Game
            {
                Id = gameId, SeasonId = seasonId, GameType = GameTypes.Regular,
                GameDate = new DateOnly(2025, 10, 8).AddDays(i),
                HomeTeamId = 21, AwayTeamId = 22, HomeScore = 3, AwayScore = 2,
                LastPeriodType = "REG", GameState = "OFF",
            });

            db.SkaterGameStats.Add(new SkaterGameStat
            {
                GameId = gameId, PlayerId = 100, TeamId = 21,
                Goals = 1, Assists = 1, Points = 2, Shots = 4, Hits = 2, TimeOnIceSeconds = 1234,
            });

            db.GoalieGameStats.Add(new GoalieGameStat
            {
                GameId = gameId, PlayerId = 200, TeamId = 21,
                Saves = 28, ShotsAgainst = 30, GoalsAgainst = 2, TimeOnIceSeconds = 3600, Starter = true,
            });

            db.TeamGameStats.Add(new TeamGameStat
            {
                GameId = gameId, TeamId = 21, OpponentTeamId = 22, IsHome = true,
                GoalsFor = 3, GoalsAgainst = 2, Result = "W", Points = 2,
            });
        }

        db.SaveChanges();
    }

    private string ArchivePath(string name = "season.blueline.gz") => Path.Combine(_directory, name);

    [Test]
    public async Task A_season_round_trips_into_an_empty_database()
    {
        using var source = NewDatabase();
        Seed(source);
        var path = ArchivePath();
        await ArchiveFor(source).ExportAsync(SeasonId, path);

        using var target = NewDatabase();
        var summary = await ArchiveFor(target).ImportAsync(path);

        Assert.Multiple(async () =>
        {
            Assert.That(summary.Games, Is.EqualTo(4));
            Assert.That(await target.Games.CountAsync(), Is.EqualTo(await source.Games.CountAsync()));
            Assert.That(await target.SkaterGameStats.CountAsync(), Is.EqualTo(await source.SkaterGameStats.CountAsync()));
            Assert.That(await target.GoalieGameStats.CountAsync(), Is.EqualTo(await source.GoalieGameStats.CountAsync()));
            Assert.That(await target.TeamGameStats.CountAsync(), Is.EqualTo(await source.TeamGameStats.CountAsync()));
            Assert.That(await target.Players.CountAsync(), Is.EqualTo(await source.Players.CountAsync()));
            Assert.That(await target.Teams.CountAsync(), Is.EqualTo(await source.Teams.CountAsync()));
        });
    }

    [Test]
    public async Task Stat_values_survive_the_round_trip_intact()
    {
        using var source = NewDatabase();
        Seed(source);
        var path = ArchivePath();
        await ArchiveFor(source).ExportAsync(SeasonId, path);

        using var target = NewDatabase();
        await ArchiveFor(target).ImportAsync(path);

        var line = await target.SkaterGameStats.FirstAsync();
        var game = await target.Games.FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(line.Points, Is.EqualTo(2));
            Assert.That(line.TimeOnIceSeconds, Is.EqualTo(1234));
            Assert.That(game.GameDate, Is.EqualTo(new DateOnly(2025, 10, 8)));
            Assert.That(game.LastPeriodType, Is.EqualTo("REG"));
        });
    }

    [Test]
    public async Task Importing_twice_converges_rather_than_duplicating()
    {
        using var source = NewDatabase();
        Seed(source);
        var path = ArchivePath();
        await ArchiveFor(source).ExportAsync(SeasonId, path);

        using var target = NewDatabase();
        await ArchiveFor(target).ImportAsync(path);
        await ArchiveFor(target).ImportAsync(path);

        Assert.Multiple(async () =>
        {
            Assert.That(await target.Games.CountAsync(), Is.EqualTo(4));
            Assert.That(await target.SkaterGameStats.CountAsync(), Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Importing_over_existing_rows_updates_them()
    {
        using var source = NewDatabase();
        Seed(source);
        var path = ArchivePath();
        await ArchiveFor(source).ExportAsync(SeasonId, path);

        // The target already holds the season, but with a stat line that was later corrected.
        using var target = NewDatabase();
        Seed(target);
        var stale = await target.SkaterGameStats.FirstAsync();
        stale.Points = 99;
        await target.SaveChangesAsync();
        target.ChangeTracker.Clear();

        await ArchiveFor(target).ImportAsync(path);

        Assert.That((await target.SkaterGameStats.FirstAsync()).Points, Is.EqualTo(2),
            "the archive is authoritative for the rows it carries");
    }

    [Test]
    public async Task An_archive_carries_only_the_season_asked_for()
    {
        using var source = NewDatabase();
        Seed(source, games: 3, seasonId: SeasonId);
        Seed(source, games: 5, seasonId: 20242025);
        var path = ArchivePath();

        await ArchiveFor(source).ExportAsync(SeasonId, path);

        using var target = NewDatabase();
        var summary = await ArchiveFor(target).ImportAsync(path);

        Assert.Multiple(async () =>
        {
            Assert.That(summary.Games, Is.EqualTo(3));
            Assert.That(await target.Games.CountAsync(g => g.SeasonId == 20242025), Is.Zero);
        });
    }

    [Test]
    public async Task Importing_a_second_season_leaves_the_first_alone()
    {
        using var source = NewDatabase();
        Seed(source, games: 3, seasonId: SeasonId);
        Seed(source, games: 5, seasonId: 20242025);

        var current = ArchivePath("current.gz");
        var previous = ArchivePath("previous.gz");
        await ArchiveFor(source).ExportAsync(SeasonId, current);
        await ArchiveFor(source).ExportAsync(20242025, previous);

        using var target = NewDatabase();
        await ArchiveFor(target).ImportAsync(current);
        await ArchiveFor(target).ImportAsync(previous);

        Assert.Multiple(async () =>
        {
            Assert.That(await target.Games.CountAsync(g => g.SeasonId == SeasonId), Is.EqualTo(3));
            Assert.That(await target.Games.CountAsync(g => g.SeasonId == 20242025), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task The_archive_is_meaningfully_smaller_than_the_rows_it_carries()
    {
        using var source = NewDatabase();
        Seed(source, games: 200);
        var path = ArchivePath();

        await ArchiveFor(source).ExportAsync(SeasonId, path);

        // Compression is the reason this is shippable at all; a regression to plain text would
        // quietly multiply the size of anything committed alongside the code.
        Assert.That(new FileInfo(path).Length, Is.LessThan(200 * 1024));
    }

    [Test]
    public async Task A_failed_import_leaves_nothing_behind()
    {
        // Rows arrive in dependency order, so a half-applied archive is not merely incomplete
        // but wrong: leaderboards built from games whose stat lines never landed report the wrong
        // leaders. A stranded partial season would also look like data to the empty-database
        // seeding check, which would then never offer to load it again.
        using var source = NewDatabase();
        Seed(source, games: 50);
        var good = ArchivePath();
        await ArchiveFor(source).ExportAsync(SeasonId, good);

        // Truncate mid-stream and append a line the reader cannot parse.
        var corrupt = ArchivePath("corrupt.gz");
        var lines = await ReadArchiveLinesAsync(good);
        await WriteArchiveLinesAsync(corrupt, lines.Take(20).Append("{ not json"));

        using var target = NewDatabase();

        Assert.CatchAsync<JsonException>(() => ArchiveFor(target).ImportAsync(corrupt));

        Assert.Multiple(async () =>
        {
            Assert.That(await target.Games.CountAsync(), Is.Zero);
            Assert.That(await target.Teams.CountAsync(), Is.Zero);
            Assert.That(await target.Players.CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task A_failed_import_does_not_disturb_a_season_already_present()
    {
        using var source = NewDatabase();
        Seed(source, games: 50);
        var good = ArchivePath();
        await ArchiveFor(source).ExportAsync(SeasonId, good);

        var corrupt = ArchivePath("corrupt.gz");
        var lines = await ReadArchiveLinesAsync(good);
        await WriteArchiveLinesAsync(corrupt, lines.Take(20).Append("{ not json"));

        using var target = NewDatabase();
        await ArchiveFor(target).ImportAsync(good);
        var before = await target.Games.CountAsync();

        Assert.CatchAsync<JsonException>(() => ArchiveFor(target).ImportAsync(corrupt));

        Assert.That(await target.Games.CountAsync(), Is.EqualTo(before));
    }

    private static async Task<List<string>> ReadArchiveLinesAsync(string path)
    {
        await using var file = File.OpenRead(path);
        await using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        var lines = new List<string>();
        while (await reader.ReadLineAsync() is { } line) lines.Add(line);
        return lines;
    }

    private static async Task WriteArchiveLinesAsync(string path, IEnumerable<string> lines)
    {
        await using var file = File.Create(path);
        await using var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionLevel.Fastest);
        await using var writer = new StreamWriter(gzip);

        foreach (var line in lines) await writer.WriteLineAsync(line);
    }

    [Test]
    public void Exporting_a_season_that_is_not_stored_fails_loudly()
    {
        using var db = NewDatabase();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => ArchiveFor(db).ExportAsync(19992000, ArchivePath()));
    }

    [Test]
    public void Importing_a_missing_file_fails_loudly()
    {
        using var db = NewDatabase();

        Assert.ThrowsAsync<FileNotFoundException>(
            () => ArchiveFor(db).ImportAsync(ArchivePath("nothing-here.gz")));
    }

    [Test]
    public async Task An_archive_from_a_newer_format_is_refused_rather_than_half_read()
    {
        var path = ArchivePath("future.gz");
        await using (var file = File.Create(path))
        await using (var gzip = new System.IO.Compression.GZipStream(file, System.IO.Compression.CompressionLevel.Fastest))
        await using (var writer = new StreamWriter(gzip))
        {
            var futureVersion = SeasonArchive.FormatVersion + 1;
            var header =
                """{"type":"header","data":{"formatVersion":VERSION,"seasonId":20252026,"exportedUtc":"2026-01-01T00:00:00+00:00","teams":0,"players":0,"games":0,"skaterLines":0,"goalieLines":0,"teamLines":0}}"""
                    .Replace("VERSION", futureVersion.ToString());

            await writer.WriteLineAsync(header);
        }

        using var db = NewDatabase();

        Assert.ThrowsAsync<NotSupportedException>(() => ArchiveFor(db).ImportAsync(path));
    }
}
