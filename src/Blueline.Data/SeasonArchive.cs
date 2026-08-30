using System.IO.Compression;
using System.Text.Json;
using Blueline.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Blueline.Data;

public record ArchiveSummary(
    int SeasonId, int Teams, int Players, int Games, int SkaterLines, int GoalieLines, int TeamLines)
{
    public int TotalRows => Teams + Players + Games + SkaterLines + GoalieLines + TeamLines;
}

/// <summary>
/// Writes a season out to a single portable file, and reads it back.
///
/// The point is to make a season installable without re-ingesting it: a fresh deployment
/// otherwise spends several minutes and around 1,500 requests rebuilding data that has not
/// changed since the season ended.
///
/// Gzipped JSON Lines rather than a copy of the SQLite file. A database file would be smaller and
/// simpler, but it would bind the archive to SQLite — and the connection string is deliberately
/// overridable so a deployment can move to another provider. Rows go through the model, so an
/// archive taken from SQLite loads into anything EF Core supports. Line-per-record keeps both
/// ends streaming, so a season never has to sit in memory whole.
/// </summary>
public class SeasonArchive(BluelineDbContext db, ILogger<SeasonArchive> logger)
{
    /// <summary>Bumped only for a change the reader could not otherwise cope with.</summary>
    public const int FormatVersion = 1;

    private const int ImportBatchSize = 2000;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<ArchiveSummary> ExportAsync(int seasonId, string path, CancellationToken ct = default)
    {
        var games = await db.Games.AsNoTracking().Where(g => g.SeasonId == seasonId)
            .OrderBy(g => g.Id).ToListAsync(ct);

        if (games.Count == 0)
            throw new InvalidOperationException($"Season {seasonId} has no stored games to export.");

        var gameIds = games.Select(g => g.Id).ToHashSet();

        var skaters = await db.SkaterGameStats.AsNoTracking()
            .Where(s => gameIds.Contains(s.GameId)).OrderBy(s => s.GameId).ThenBy(s => s.PlayerId).ToListAsync(ct);
        var goalies = await db.GoalieGameStats.AsNoTracking()
            .Where(s => gameIds.Contains(s.GameId)).OrderBy(s => s.GameId).ThenBy(s => s.PlayerId).ToListAsync(ct);
        var teamLines = await db.TeamGameStats.AsNoTracking()
            .Where(s => gameIds.Contains(s.GameId)).OrderBy(s => s.GameId).ThenBy(s => s.TeamId).ToListAsync(ct);

        // Only the teams and players this season actually involves, so an archive stays a season
        // rather than quietly carrying the whole database.
        var playerIds = skaters.Select(s => s.PlayerId).Concat(goalies.Select(s => s.PlayerId)).ToHashSet();
        var teamIds = games.Select(g => g.HomeTeamId).Concat(games.Select(g => g.AwayTeamId)).ToHashSet();

        var players = await db.Players.AsNoTracking()
            .Where(p => playerIds.Contains(p.Id)).OrderBy(p => p.Id).ToListAsync(ct);
        var teams = await db.Teams.AsNoTracking()
            .Where(t => teamIds.Contains(t.Id)).OrderBy(t => t.Id).ToListAsync(ct);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        await using (var file = File.Create(path))
        await using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
        await using (var writer = new StreamWriter(gzip))
        {
            await WriteAsync(writer, "header", new ArchiveHeader(
                FormatVersion, seasonId, DateTimeOffset.UtcNow,
                teams.Count, players.Count, games.Count, skaters.Count, goalies.Count, teamLines.Count));

            // Teams and players first: the rows that follow reference them.
            foreach (var team in teams) await WriteAsync(writer, "team", team);
            foreach (var player in players) await WriteAsync(writer, "player", player);
            foreach (var game in games) await WriteAsync(writer, "game", game);
            foreach (var line in skaters) await WriteAsync(writer, "skater", line);
            foreach (var line in goalies) await WriteAsync(writer, "goalie", line);
            foreach (var line in teamLines) await WriteAsync(writer, "teamGame", line);
        }

        var summary = new ArchiveSummary(
            seasonId, teams.Count, players.Count, games.Count, skaters.Count, goalies.Count, teamLines.Count);

        logger.LogInformation(
            "Exported season {Season}: {Rows} rows to {Path} ({Size:F1} MB).",
            seasonId, summary.TotalRows, path, new FileInfo(path).Length / 1e6);

        return summary;
    }

    /// <summary>
    /// Loads an archive. Idempotent in the same way ingestion is — every row is matched on the
    /// league's own identifiers and updated rather than duplicated — so importing twice, or over
    /// a season that is already partly present, converges rather than corrupting.
    /// </summary>
    public async Task<ArchiveSummary> ImportAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"No archive at {path}.", path);

        ArchiveHeader? header = null;
        var counts = new Dictionary<string, int>();
        var pending = 0;

        // One transaction for the whole archive, for two reasons.
        //
        // Readers see nothing until it commits. Rows arrive in dependency order — games before
        // the stat lines that reference them — so a partly applied import is not merely
        // incomplete but actively wrong: leaderboards computed from games whose stats have not
        // landed yet report the wrong leaders.
        //
        // And an interrupted import leaves no trace. Otherwise a failure halfway would strand a
        // partial season, which the empty-database seeding check would then see as data and
        // never offer to load again.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        await using var file = File.OpenRead(path);
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var document = JsonDocument.Parse(line);
            var type = document.RootElement.GetProperty("type").GetString()!;
            var payload = document.RootElement.GetProperty("data").GetRawText();

            if (type == "header")
            {
                header = JsonSerializer.Deserialize<ArchiveHeader>(payload, Json)!;
                if (header.FormatVersion > FormatVersion)
                {
                    throw new NotSupportedException(
                        $"The archive is format version {header.FormatVersion}; this build understands {FormatVersion}.");
                }

                logger.LogInformation(
                    "Importing season {Season}, exported {Exported:u}.", header.SeasonId, header.ExportedUtc);
                continue;
            }

            if (header is null)
                throw new InvalidDataException("The archive does not begin with a header.");

            await ApplyAsync(type, payload, ct);
            counts[type] = counts.GetValueOrDefault(type) + 1;

            // Saved in batches: one save at the end would hold 60,000 tracked entities.
            if (++pending >= ImportBatchSize)
            {
                await db.SaveChangesAsync(ct);
                db.ChangeTracker.Clear();
                pending = 0;
            }
        }

        if (header is null) throw new InvalidDataException("The archive is empty.");

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();

        var summary = new ArchiveSummary(
            header.SeasonId,
            counts.GetValueOrDefault("team"), counts.GetValueOrDefault("player"), counts.GetValueOrDefault("game"),
            counts.GetValueOrDefault("skater"), counts.GetValueOrDefault("goalie"), counts.GetValueOrDefault("teamGame"));

        logger.LogInformation("Imported {Rows} rows for season {Season}.", summary.TotalRows, summary.SeasonId);
        return summary;
    }

    private async Task ApplyAsync(string type, string payload, CancellationToken ct)
    {
        switch (type)
        {
            case "team":
            {
                var row = JsonSerializer.Deserialize<Team>(payload, Json)!;
                var existing = await db.Teams.FindAsync([row.Id], ct);
                if (existing is null) db.Teams.Add(row);
                else db.Entry(existing).CurrentValues.SetValues(row);
                break;
            }

            case "player":
            {
                var row = JsonSerializer.Deserialize<Player>(payload, Json)!;
                var existing = await db.Players.FindAsync([row.Id], ct);
                if (existing is null) db.Players.Add(row);
                else db.Entry(existing).CurrentValues.SetValues(row);
                break;
            }

            case "game":
            {
                var row = JsonSerializer.Deserialize<Game>(payload, Json)!;
                var existing = await db.Games.FindAsync([row.Id], ct);
                if (existing is null) db.Games.Add(row);
                else db.Entry(existing).CurrentValues.SetValues(row);
                break;
            }

            case "skater":
            {
                var row = JsonSerializer.Deserialize<SkaterGameStat>(payload, Json)!;
                var existing = await db.SkaterGameStats.FindAsync([row.GameId, row.PlayerId], ct);
                if (existing is null) db.SkaterGameStats.Add(row);
                else db.Entry(existing).CurrentValues.SetValues(row);
                break;
            }

            case "goalie":
            {
                var row = JsonSerializer.Deserialize<GoalieGameStat>(payload, Json)!;
                var existing = await db.GoalieGameStats.FindAsync([row.GameId, row.PlayerId], ct);
                if (existing is null) db.GoalieGameStats.Add(row);
                else db.Entry(existing).CurrentValues.SetValues(row);
                break;
            }

            case "teamGame":
            {
                var row = JsonSerializer.Deserialize<TeamGameStat>(payload, Json)!;
                var existing = await db.TeamGameStats.FindAsync([row.GameId, row.TeamId], ct);
                if (existing is null) db.TeamGameStats.Add(row);
                else db.Entry(existing).CurrentValues.SetValues(row);
                break;
            }

            default:
                // Forward compatible: a newer export may carry record types this build predates.
                logger.LogDebug("Skipping unrecognised archive record type {Type}.", type);
                break;
        }
    }

    private static async Task WriteAsync<T>(StreamWriter writer, string type, T data) =>
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { type, data }, Json));

    private record ArchiveHeader(
        int FormatVersion,
        int SeasonId,
        DateTimeOffset ExportedUtc,
        int Teams,
        int Players,
        int Games,
        int SkaterLines,
        int GoalieLines,
        int TeamLines);
}
