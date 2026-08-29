using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Blueline.Ingestion.Nhl;

/// <summary>
/// Thin typed wrapper over the league's public web API (api-web.nhle.com).
/// It is an undocumented public API, so every call tolerates a missing or malformed
/// payload by returning null rather than throwing the whole ingestion run away.
/// </summary>
public class NhlApiClient(HttpClient http, ILogger<NhlApiClient> logger)
{
    public const string DefaultBaseAddress = "https://api-web.nhle.com/v1/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new LocalizedTextConverter() },
    };

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct) where T : class
    {
        try
        {
            using var response = await http.GetAsync(path, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                logger.LogDebug("NHL API returned 404 for {Path}", path);
                return null;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
        }
        catch (JsonException ex)
        {
            // Not transient: the API's shape has changed and ingestion will keep losing this data.
            logger.LogError(ex, "Could not parse the NHL API response for {Path}. The response shape may have changed.", path);
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "NHL API call failed for {Path}", path);
            return null;
        }
    }

    /// <summary>All 32 teams as they stood on the given date.</summary>
    public Task<StandingsResponse?> GetStandingsAsync(DateOnly date, CancellationToken ct) =>
        GetAsync<StandingsResponse>($"standings/{date:yyyy-MM-dd}", ct);

    public Task<ClubScheduleResponse?> GetClubScheduleAsync(string teamAbbrev, int seasonId, CancellationToken ct) =>
        GetAsync<ClubScheduleResponse>($"club-schedule-season/{teamAbbrev}/{seasonId}", ct);

    public Task<ScoreResponse?> GetScoreAsync(DateOnly date, CancellationToken ct) =>
        GetAsync<ScoreResponse>($"score/{date:yyyy-MM-dd}", ct);

    public Task<BoxscoreResponse?> GetBoxscoreAsync(long gameId, CancellationToken ct) =>
        GetAsync<BoxscoreResponse>($"gamecenter/{gameId}/boxscore", ct);

    /// <summary>
    /// Season totals for a club. Ingestion uses this only to recover full first/last names and
    /// headshots, which the boxscore abbreviates to "D. Tarasov".
    /// </summary>
    public Task<ClubStatsResponse?> GetClubStatsAsync(string teamAbbrev, int seasonId, int gameType, CancellationToken ct) =>
        GetAsync<ClubStatsResponse>($"club-stats/{teamAbbrev}/{seasonId}/{gameType}", ct);
}
