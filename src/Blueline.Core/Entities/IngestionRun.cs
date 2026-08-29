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
    public IngestionStatus Status { get; set; }
    public string? Error { get; set; }
}
