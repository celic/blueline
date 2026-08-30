using Blueline.Core.Dtos;
using Blueline.Data.Queries;
using Blueline.Ingestion;
using Blueline.Web.Components.Shared;

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
                string? window = null,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.NotFound();

                var series = await queries.GetPlayerTrendAsync(
                    playerId, seasonId.Value, stat, RollingWindow.Parse(window), GameScopes.Parse(scope), ct);
                return series is null
                    ? Results.NotFound(new { message = $"No player {playerId}, or '{stat}' is not a chartable stat." })
                    : Results.Ok(series);
            })
            .WithSummary("A skater's game-by-game values, cumulative total and rolling average.");

        // Comparison endpoints. Separate from the single-subject ones rather than a parameter on
        // them, so the response is always an array and an existing consumer is unaffected.
        api.MapGet("/players/trends", async (
                StatsQueryService queries,
                CancellationToken ct,
                string? ids = null,
                int? season = null,
                string stat = "points",
                string? window = null,
                string? scope = null) =>
            {
                var playerIds = ParseIds(ids);
                if (playerIds.Count == 0) return Results.BadRequest(new { message = "Pass ids=1,2,3." });

                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<TrendSeries>());

                var series = new List<TrendSeries>();
                foreach (var id in playerIds)
                {
                    var trend = await queries.GetPlayerTrendAsync(
                        id, seasonId.Value, stat, RollingWindow.Parse(window), GameScopes.Parse(scope), ct);
                    if (trend is not null) series.Add(trend);
                }

                return Results.Ok(series);
            })
            .WithSummary("Several skaters' trends in one call, aligned on the same stat and season.");

        api.MapGet("/goalies/trends", async (
                StatsQueryService queries,
                CancellationToken ct,
                string? ids = null,
                int? season = null,
                string stat = "savePctg",
                string? window = null,
                string? scope = null) =>
            {
                var goalieIds = ParseIds(ids);
                if (goalieIds.Count == 0) return Results.BadRequest(new { message = "Pass ids=1,2,3." });

                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<TrendSeries>());

                var series = new List<TrendSeries>();
                foreach (var id in goalieIds)
                {
                    var trend = await queries.GetGoalieTrendAsync(
                        id, seasonId.Value, stat, RollingWindow.Parse(window), GameScopes.Parse(scope), ct);
                    if (trend is not null) series.Add(trend);
                }

                return Results.Ok(series);
            })
            .WithSummary("Several goalies' trends in one call.");

        api.MapGet("/teams/trends", async (
                StatsQueryService queries,
                CancellationToken ct,
                string? ids = null,
                int? season = null,
                string stat = "points",
                string? window = null,
                string? scope = null) =>
            {
                var teamIds = ParseIds(ids);
                if (teamIds.Count == 0) return Results.BadRequest(new { message = "Pass ids=1,2,3." });

                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.Ok(Array.Empty<TrendSeries>());

                var series = new List<TrendSeries>();
                foreach (var id in teamIds)
                {
                    var trend = await queries.GetTeamTrendAsync(
                        id, seasonId.Value, stat, RollingWindow.Parse(window), GameScopes.Parse(scope), ct);
                    if (trend is not null) series.Add(trend);
                }

                return Results.Ok(series);
            })
            .WithSummary("Several teams' trends in one call.");

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
                string? window = null,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.NotFound();

                var series = await queries.GetGoalieTrendAsync(
                    playerId, seasonId.Value, stat, RollingWindow.Parse(window), GameScopes.Parse(scope), ct);
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
                string? window = null,
                string? scope = null) =>
            {
                var seasonId = season ?? await queries.GetLatestSeasonAsync(ct);
                if (seasonId is null) return Results.NotFound();

                var series = await queries.GetTeamTrendAsync(
                    teamId, seasonId.Value, stat, RollingWindow.Parse(window), GameScopes.Parse(scope), ct);
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

        // Deliberately read-only. There was a POST /ingestion/run here and it is not coming back:
        // it let any unauthenticated caller make the site fetch from the league's API as often as
        // they liked, and triggering collection over HTTP is the wrong shape for it regardless.
        // Ingestion is a scheduled job run against the database — see the README.

        return app;
    }

    /// <summary>Keeps caller-supplied sizes inside sane bounds.</summary>
    private static int Clamp(int value, int max) => Math.Clamp(value, 1, max);

    /// <summary>
    /// Parses a comma-separated id list, ignoring anything unparseable rather than rejecting the
    /// whole request. Capped at the number of series a chart can carry, since that is what the
    /// caller can meaningfully plot.
    /// </summary>
    internal static List<int> ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return [];

        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .Take(ChartPalette.MaxSeries)
            .ToList();
    }
}
