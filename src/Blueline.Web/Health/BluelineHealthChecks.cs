using Blueline.Core.Entities;
using Blueline.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blueline.Web.Health;

/// <summary>Can the process reach its database at all? The one thing a restart might fix.</summary>
public class DatabaseHealthCheck(BluelineDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (await db.Database.CanConnectAsync(cancellationToken))
                return HealthCheckResult.Healthy("Database reachable.");

            return HealthCheckResult.Unhealthy("Database is not reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check threw.", ex);
        }
    }
}

/// <summary>
/// Is there anything to serve, and is ingestion keeping up?
///
/// Deliberately not part of liveness. A fresh deployment spends several minutes seeding its
/// first season, during which there is genuinely nothing to show — but restarting the container
/// would abandon that work and start it again, forever. Liveness answers "is this process
/// worth keeping"; this answers "is it worth sending traffic to yet".
/// </summary>
public class IngestionHealthCheck(BluelineDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var games = await db.Games.CountAsync(cancellationToken);
            var lastRun = await db.IngestionRuns
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync(cancellationToken);

            var data = new Dictionary<string, object>
            {
                ["gamesStored"] = games,
                ["lastRunKind"] = lastRun?.Kind ?? "none",
                ["lastRunStatus"] = lastRun?.Status.ToString() ?? "none",
                ["lastRunCompletedUtc"] = lastRun?.CompletedUtc?.ToString("O") ?? "never",
                ["lastRunGamesFailed"] = lastRun?.GamesFailed ?? 0,
            };

            if (games == 0)
            {
                // Either seeding is still running or it never happened. Both mean "not ready",
                // and neither is improved by killing the process.
                return HealthCheckResult.Unhealthy("No games stored yet; the first season may still be loading.", data: data);
            }

            if (lastRun?.Status == IngestionStatus.Failed)
                return HealthCheckResult.Degraded($"The last ingestion run failed: {lastRun.Error}", data: data);

            if (lastRun?.GamesFailed > 0)
            {
                return HealthCheckResult.Degraded(
                    $"The last run could not read {lastRun.GamesFailed} game(s). Run reconcile to fill the gap.",
                    data: data);
            }

            return HealthCheckResult.Healthy($"{games} games stored.", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Ingestion check threw.", ex);
        }
    }
}
