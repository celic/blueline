using Blueline.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blueline.Ingestion;

/// <summary>
/// Runs one ingestion pass a day, plus an optional pass at startup.
///
/// A failed pass is logged and swallowed rather than crashing the host: the next day's run
/// re-reads the same lookback window, so a transient API outage heals itself.
/// </summary>
public class DailyIngestionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    TimeProvider time,
    ILogger<DailyIngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.DailyJobEnabled)
        {
            logger.LogInformation("Daily ingestion is disabled by configuration.");
            return;
        }

        await SeedIfEmptyAsync(settings, stoppingToken);

        if (settings.RunOnStartup)
            await RunOnceAsync(settings, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeUntilNextRun(settings.DailyRunTimeUtc);
            logger.LogInformation("Next ingestion run in {Hours:F1} hours.", delay.TotalHours);

            try
            {
                await Task.Delay(delay, time, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await RunOnceAsync(settings, stoppingToken);
        }
    }

    /// <summary>
    /// Loads a full season the first time the app runs against an empty database, so a fresh
    /// deployment comes up with something to show instead of an empty site. Takes a few minutes,
    /// and is skipped entirely once any game is stored.
    /// </summary>
    private async Task SeedIfEmptyAsync(IngestionOptions settings, CancellationToken ct)
    {
        if (settings.SeedSeasonId == 0) return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BluelineDbContext>();
            if (await db.Games.AnyAsync(ct)) return;

            // An archive is preferred over the league API: it takes seconds instead of minutes
            // and costs no requests at all. Ingestion is the fallback for when none is shipped.
            var archivePath = ResolveSeedArchive(settings);
            if (archivePath is not null)
            {
                logger.LogInformation("Database is empty; loading the season archive at {Path}.", archivePath);

                var archive = scope.ServiceProvider.GetRequiredService<SeasonArchive>();
                var imported = await archive.ImportAsync(archivePath, ct);

                logger.LogInformation("Seeded {Count} games from the archive.", imported.Games);
                return;
            }

            logger.LogInformation(
                "Database is empty and no archive was found; ingesting season {Season} from the league API. " +
                "This takes several minutes.", settings.SeedSeasonId);

            var ingestion = scope.ServiceProvider.GetRequiredService<NhlIngestionService>();
            var count = await ingestion.BackfillSeasonAsync(settings.SeedSeasonId, ct);

            logger.LogInformation("Seeded {Count} games.", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down mid-seed; the next start picks it up again.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Seeding failed. Load a season by hand with the CLI, or restart to retry.");
        }
    }

    /// <summary>
    /// Locates the archive to seed from, or null to fall back to ingesting. An explicitly empty
    /// setting means "never use an archive", which is distinct from leaving it unset.
    /// </summary>
    internal static string? ResolveSeedArchive(IngestionOptions settings)
    {
        if (settings.SeedArchivePath is { Length: 0 }) return null;

        var configured = settings.SeedArchivePath
                         ?? Path.Combine("seed", $"{settings.SeedSeasonId}.blueline.gz");

        var path = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);

        return File.Exists(path) ? path : null;
    }

    private async Task RunOnceAsync(IngestionOptions settings, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var ingestion = scope.ServiceProvider.GetRequiredService<NhlIngestionService>();

            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            var count = await ingestion.IngestRecentAsync(today, settings.LookbackDays, ct);

            logger.LogInformation("Daily ingestion complete: {Count} games refreshed.", count);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down; nothing to report.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Daily ingestion failed. The next scheduled run will retry the same window.");
        }
    }

    private TimeSpan TimeUntilNextRun(TimeOnly runTimeUtc) =>
        TimeUntilNextRun(time.GetUtcNow().UtcDateTime, runTimeUtc);

    /// <summary>
    /// How long until the next occurrence of the daily run time. Today's slot if it has not yet
    /// passed, otherwise tomorrow's. Pure so the scheduling arithmetic can be tested directly.
    /// </summary>
    internal static TimeSpan TimeUntilNextRun(DateTime nowUtc, TimeOnly runTimeUtc)
    {
        var next = nowUtc.Date + runTimeUtc.ToTimeSpan();
        if (next <= nowUtc) next = next.AddDays(1);
        return next - nowUtc;
    }
}
