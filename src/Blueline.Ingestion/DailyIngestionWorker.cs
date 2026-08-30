using Blueline.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blueline.Ingestion;

/// <summary>
/// Two jobs that look alike and are not: seeding an empty database so the site has something to
/// show, and keeping a populated one current.
///
/// **Seeding always runs.** It is about the site having data at all, so it is gated only by
/// <see cref="IngestionOptions.SeedSeasonId"/>.
///
/// **The daily pass is off unless asked for.** Staying current is a schedule, and a schedule
/// belongs outside the site — see the README. <see cref="IngestionOptions.DailyJobEnabled"/> turns
/// the in-process one on for anyone who would rather not run one.
///
/// The two were previously gated together, which meant switching the schedule off also switched
/// off first-run seeding, leaving a deployment permanently empty for a reason nothing announced.
///
/// A failed pass is logged and swallowed rather than crashing the host: the next run re-reads the
/// same lookback window, so a transient API outage heals itself.
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

        await SeedIfEmptyAsync(settings, stoppingToken);

        if (!settings.DailyJobEnabled)
        {
            logger.LogInformation(
                "The in-process daily ingestion job is off; new games are expected from a scheduled run outside the site.");
            return;
        }

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

            // Archives are preferred over the league API: seconds instead of minutes, and no
            // requests at all. Every archive present is loaded, so a deployment can carry several
            // past seasons. Ingestion is the fallback for when none is shipped.
            var archives = FindSeedArchives(settings);
            if (archives.Count > 0)
            {
                logger.LogInformation("Database is empty; loading {Count} season archive(s).", archives.Count);

                var archive = scope.ServiceProvider.GetRequiredService<SeasonArchive>();
                var games = 0;

                foreach (var path in archives)
                {
                    // Each import is its own transaction, so one unreadable archive costs only
                    // its own season rather than the seasons already loaded.
                    try
                    {
                        var imported = await archive.ImportAsync(path, ct);
                        games += imported.Games;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Could not load the season archive at {Path}; skipping it.", path);
                    }
                }

                logger.LogInformation("Seeded {Count} games from archives.", games);
                if (games > 0) return;

                logger.LogWarning("No archive could be loaded; falling back to the league API.");
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

    /// <summary>Extension every season archive carries.</summary>
    public const string ArchiveExtension = ".blueline.gz";

    /// <summary>
    /// Every archive available to seed from, oldest season first so the newest wins any overlap.
    /// An explicitly empty directory setting means "never use an archive".
    /// </summary>
    internal static IReadOnlyList<string> FindSeedArchives(IngestionOptions settings)
    {
        if (settings.SeedArchiveDirectory is not { Length: > 0 } configured) return [];

        var directory = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(AppContext.BaseDirectory, configured);

        if (!Directory.Exists(directory)) return [];

        return Directory.GetFiles(directory, $"*{ArchiveExtension}")
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToList();
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
