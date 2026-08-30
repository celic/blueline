namespace Blueline.Core.Entities;

public enum IngestionStatus { Running, Succeeded, Failed }

/// <summary>Audit record for a backfill or daily ingestion pass.</summary>
public class IngestionRun
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public int? SeasonId { get; set; }
    public DateTimeOffset StartedUtc { get; set; }
    public DateTimeOffset? CompletedUtc { get; set; }
    public int GamesIngested { get; set; }

    /// <summary>
    /// Games whose box score could not be read, after the HTTP client's retries were spent.
    /// A run with failures still counts as succeeded — the rest of the night is worth keeping —
    /// but the count makes the shortfall visible instead of silently absent.
    /// </summary>
    public int GamesFailed { get; set; }

    /// <summary>
    /// The failed game identifiers, comma separated, so a later pass knows exactly what to
    /// re-fetch. Truncated rather than allowed to grow without bound; <see cref="GamesFailed"/>
    /// remains the true count.
    /// </summary>
    public string? FailedGameIds { get; set; }

    public IngestionStatus Status { get; set; }
    public string? Error { get; set; }
}
