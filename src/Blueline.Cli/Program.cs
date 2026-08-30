using Blueline.Data;
using Blueline.Data.Queries;
using Blueline.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddBluelineCore(builder.Configuration);
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

// A backfill makes thousands of HTTP and SQL calls; per-call logs would bury the progress lines.
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
// The resilience pipeline logs every attempt, including the successes.
builder.Logging.AddFilter("Polly", LogLevel.Warning);

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var services = scope.ServiceProvider;

var db = services.GetRequiredService<BluelineDbContext>();
await db.Database.MigrateAsync();

var command = args.FirstOrDefault()?.ToLowerInvariant();

switch (command)
{
    case "backfill":
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var seasonId))
        {
            Console.Error.WriteLine("Usage: backfill <seasonId>   e.g. backfill 20252026");
            return 1;
        }

        Console.WriteLine($"Backfilling season {StatsQueryService.FormatSeason(seasonId)}. This takes a few minutes.");
        var ingestion = services.GetRequiredService<NhlIngestionService>();
        var count = await ingestion.BackfillSeasonAsync(seasonId);
        Console.WriteLine($"Done. {count} games ingested into {BluelineDbPath.DatabaseFile}");
        return 0;
    }

    case "daily":
    {
        var days = args.Length > 1 && int.TryParse(args[1], out var d) ? d : 3;

        // An explicit date lets you fill a gap by hand after an outage.
        var through = args.Length > 2 && DateOnly.TryParse(args[2], out var parsed)
            ? parsed
            : DateOnly.FromDateTime(DateTime.UtcNow);

        var ingestion = services.GetRequiredService<NhlIngestionService>();
        var count = await ingestion.IngestRecentAsync(through, days);
        Console.WriteLine($"Refreshed {count} games from the {days} day(s) ending {through}.");
        return 0;
    }

    case "reconcile":
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var seasonId))
        {
            Console.Error.WriteLine("Usage: reconcile <seasonId>   e.g. reconcile 20252026");
            return 1;
        }

        Console.WriteLine($"Checking season {StatsQueryService.FormatSeason(seasonId)} against the league schedule.");
        var ingestion = services.GetRequiredService<NhlIngestionService>();
        var count = await ingestion.ReconcileSeasonAsync(seasonId);
        Console.WriteLine(count == 0
            ? "Nothing missing."
            : $"Filled {count} gap(s).");
        return 0;
    }

    case "export":
    {
        if (args.Length < 2 || !int.TryParse(args[1], out var seasonId))
        {
            Console.Error.WriteLine("Usage: export <seasonId> [file]   e.g. export 20252026 seed/20252026.blueline.gz");
            return 1;
        }

        var file = args.Length > 2 ? args[2] : $"{seasonId}.blueline.gz";
        var archive = services.GetRequiredService<SeasonArchive>();
        var summary = await archive.ExportAsync(seasonId, file);

        Console.WriteLine($"Exported {summary.TotalRows} rows to {file} " +
                          $"({new FileInfo(file).Length / 1e6:F1} MB).");
        return 0;
    }

    case "import":
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: import <file>   e.g. import seed/20252026.blueline.gz");
            return 1;
        }

        var archive = services.GetRequiredService<SeasonArchive>();
        var summary = await archive.ImportAsync(args[1]);

        Console.WriteLine($"Imported season {StatsQueryService.FormatSeason(summary.SeasonId)}: " +
                          $"{summary.Games} games, {summary.SkaterLines} skater lines, " +
                          $"{summary.GoalieLines} goalie lines.");
        return 0;
    }

    case "status":
    {
        var queries = services.GetRequiredService<StatsQueryService>();
        var status = await queries.GetIngestionStatusAsync();
        var seasons = await queries.GetSeasonsAsync();

        Console.WriteLine($"Database:     {BluelineDbPath.DatabaseFile}");
        Console.WriteLine($"Games stored: {status.GamesInDatabase}");
        Console.WriteLine($"Latest game:  {status.LatestGameDate?.ToString() ?? "none"}");
        Console.WriteLine($"Last run:     {status.LastRunKind ?? "never"} / {status.LastRunStatus ?? "-"}");
        if (status.LastRunError is not null) Console.WriteLine($"Last error:   {status.LastRunError}");

        foreach (var season in seasons)
            Console.WriteLine($"  {season.Label}: {season.GameCount} games ({season.FirstGame} to {season.LastGame})");
        return 0;
    }

    default:
        Console.WriteLine("""
            Blueline data loader.

              backfill <seasonId>   Load a full season, e.g. backfill 20252026
              daily [days] [date]   Re-read the N days ending on date (defaults: 3 days, today)
              reconcile <seasonId>  Ingest any games the league lists but we do not have
              export <seasonId> [f] Write a season to a portable archive file
              import <file>         Load a season archive, no API calls needed
              status                Show what is currently stored
            """);
        return command is null ? 0 : 1;
}
