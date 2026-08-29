using Blueline.Core.Dtos;
using Blueline.Data.Queries;
using Blueline.Ingestion;

namespace Blueline.Web.Api;

/// <summary>
/// The public REST API. The Blazor pages call <see cref="StatsQueryService"/> directly rather
/// than going through these endpoints — same data, one less network hop — so this exists for
/// external consumers and for inspecting the data by hand.
/// </summary>
public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Stats");

        api.MapGet("/seasons", async (StatsQueryService queries, CancellationToken ct) =>
                await queries.GetSeasonsAsync(ct))
            .WithSummary("Seasons currently stored, newest first.");

        api.MapGet("/stats", () => new
            {
                skater = StatDefinition.Skater,
                goalie = StatDefinition.Goalie,
                team = StatDefinition.Team,
                // Accepted by every stat endpoint as ?scope=; anything else falls back to the first.
                scopes = Enum.GetNames<GameScope>(),
            })
            .WithSummary("Stats that can be charted, for skaters, goalies and teams.");

        api.MapGet("/players", async (
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string? search = null,
                int take = 25,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<PlayerSummary>());

                return Results.Ok(await queries.SearchPlayersAsync(
                    seasonId.Value, search, Clamp(take, 100), GameScopes.Parse(scope), ct));
            })
            .WithSummary("Search skaters in a season, ordered by points.");

        api.MapGet("/players/{playerId:int}/trend", async (
                int playerId,
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string stat = "points",
                int window = 10,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.NotFound();

                var series = await queries.GetPlayerTrendAsync(
                    playerId, seasonId.Value, stat, Clamp(window, 41), GameScopes.Parse(scope), ct);
                return series is null
                    ? Results.NotFound(new { message = $"No player {playerId}, or '{stat}' is not a chartable stat." })
                    : Results.Ok(series);
            })
            .WithSummary("A skater's game-by-game values, cumulative total and rolling average.");

        api.MapGet("/goalies", async (
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string? search = null,
                string stat = "savePctg",
                int take = 25,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<GoalieSummary>());

                return Results.Ok(await queries.SearchGoaliesAsync(
                    seasonId.Value, search, stat, Clamp(take, 100), GameScopes.Parse(scope), ct));
            })
            .WithSummary("Goalies in a season, ranked by a stat. Rate stats apply a minutes qualification.");

        api.MapGet("/goalies/{playerId:int}/trend", async (
                int playerId,
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string stat = "savePctg",
                int window = 10,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.NotFound();

                var series = await queries.GetGoalieTrendAsync(
                    playerId, seasonId.Value, stat, Clamp(window, 41), GameScopes.Parse(scope), ct);
                return series is null
                    ? Results.NotFound(new { message = $"No goalie {playerId}, or '{stat}' is not a chartable goalie stat." })
                    : Results.Ok(series);
            })
            .WithSummary("A goalie's game-by-game trend. Rates are weighted by shots faced, not averaged.");

        api.MapGet("/teams", async (
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<TeamSummary>());

                return Results.Ok(await queries.GetTeamsAsync(seasonId.Value, GameScopes.Parse(scope), ct));
            })
            .WithSummary("Team records for a season. Standings points only exist in the regular season.");

        api.MapGet("/teams/{teamId:int}/trend", async (
                int teamId,
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string stat = "points",
                int window = 10,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.NotFound();

                var series = await queries.GetTeamTrendAsync(
                    teamId, seasonId.Value, stat, Clamp(window, 41), GameScopes.Parse(scope), ct);
                return series is null
                    ? Results.NotFound(new { message = $"No team {teamId}, or '{stat}' is not a chartable stat." })
                    : Results.Ok(series);
            })
            .WithSummary("A team's game-by-game results and points pace.");

        api.MapGet("/leaders", async (
                StatsQueryService queries,
                CancellationToken ct,
                int? season = null,
                string stat = "points",
                int take = 20,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<LeaderRow>());

                return Results.Ok(await queries.GetLeadersAsync(
                    seasonId.Value, stat, Clamp(take, 100), GameScopes.Parse(scope), ct));
            })
            .WithSummary("Season leaders for a stat.");

        api.MapGet("/ingestion/status", async (StatsQueryService queries, CancellationToken ct) =>
                await queries.GetIngestionStatusAsync(ct))
            .WithSummary("What is stored and how the last ingestion run went.");

        api.MapPost("/ingestion/run", async (
                NhlIngestionService ingestion,
                CancellationToken ct,
                int days = 3) =>
            {
                var count = await ingestion.IngestRecentAsync(DateOnly.FromDateTime(DateTime.UtcNow), Clamp(days, 30), ct);
                return Results.Ok(new { gamesRefreshed = count });
            })
            .WithSummary("Run the daily ingestion now instead of waiting for the schedule.");

        return app;
    }

    /// <summary>Keeps caller-supplied sizes inside sane bounds.</summary>
    private static int Clamp(int value, int max) => Math.Clamp(value, 1, max);
}
