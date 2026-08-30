using Blueline.Core.Dtos;
using Blueline.Data;
using Blueline.Data.Queries;
using Blueline.Ingestion.Nhl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Blueline.Ingestion;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database, the read-side queries and the league API client. Shared by the
    /// web host and the CLI so both talk to the same database the same way.
    /// </summary>
    public static IServiceCollection AddBluelineCore(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = BluelineDbPath.ResolveConnectionString(configuration.GetConnectionString("Blueline"));

        services.AddDbContext<BluelineDbContext>(options => options
            .UseSqlite(connectionString)
            // Without this, the background ingestion job's writes block page reads.
            .AddInterceptors(new SqliteConnectionInterceptor()));
        services.AddScoped<StatsQueryService>();
        services.AddScoped<NhlIngestionService>();

        services.Configure<IngestionOptions>(configuration.GetSection(IngestionOptions.SectionName));
        services.Configure<DisplayOptions>(configuration.GetSection(DisplayOptions.SectionName));
        services.TryAddSingletonTimeProvider();

        services.AddHttpClient<NhlApiClient>(client =>
        {
            client.BaseAddress = new Uri(NhlApiClient.DefaultBaseAddress);
            client.Timeout = TimeSpan.FromSeconds(30);
            // The API is undocumented and unauthenticated; identify ourselves anyway.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Blueline/1.0 (+hockey trend viewer)");
        });

        return services;
    }

    /// <summary>Registers the background job that keeps the database current.</summary>
    public static IServiceCollection AddBluelineDailyIngestion(this IServiceCollection services)
    {
        services.AddHostedService<DailyIngestionWorker>();
        return services;
    }

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
            services.AddSingleton(TimeProvider.System);
    }
}
