using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blueline.Web.Health;

public static class HealthEndpoints
{
    /// <summary>Checks tagged this way answer "is this process worth keeping".</summary>
    public const string LiveTag = "live";

    /// <summary>Checks tagged this way answer "is it worth sending traffic here yet".</summary>
    public const string ReadyTag = "ready";

    public static IServiceCollection AddBluelineHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [LiveTag, ReadyTag])
            .AddCheck<IngestionHealthCheck>("ingestion", tags: [ReadyTag]);

        return services;
    }

    public static IEndpointRouteBuilder MapBluelineHealthChecks(this IEndpointRouteBuilder app)
    {
        // Liveness deliberately excludes the data checks: a host that restarts the container
        // while the first season is still seeding would abandon that work and loop forever.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(LiveTag),
            ResponseWriter = WriteJsonAsync,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = WriteJsonAsync,
        });

        return app;
    }

    /// <summary>
    /// The default writer returns the bare word "Healthy", which is enough for a probe and
    /// useless to a person. This reports each check so a degraded service says why.
    /// </summary>
    private static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                data = entry.Value.Data.Count == 0 ? null : entry.Value.Data,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
