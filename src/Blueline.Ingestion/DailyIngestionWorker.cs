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

            logger.LogInformation("Database is empty; seeding season {Season}.", settings.SeedSeasonId);

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

    private TimeSpan TimeUntilNextRun(TimeOnly runTimeUtc)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var next = now.Date + runTimeUtc.ToTimeSpan();
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }
}
