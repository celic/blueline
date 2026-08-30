namespace Blueline.Ingestion;

public class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Set false to disable the daily background job (useful when running the CLI by hand).</summary>
    public bool DailyJobEnabled { get; set; } = true;

    /// <summary>
    /// When the daily job runs, in UTC. Defaults to 11:00 UTC (roughly 07:00 Eastern), by which
    /// point every North American game from the previous night has finished and been scored.
    /// </summary>
    public TimeOnly DailyRunTimeUtc { get; set; } = new(11, 0);

    /// <summary>
    /// How many days back each daily run re-reads. The league revises box scores after the fact,
    /// so re-reading a short window is what keeps stored stats correct.
    /// </summary>
    public int LookbackDays { get; set; } = 3;

    /// <summary>Run one ingestion pass at startup rather than waiting for the next scheduled time.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Season the site seeds itself with when the database is empty. Zero disables seeding.</summary>
    public int SeedSeasonId { get; set; } = 20252026;

    /// <summary>
    /// Archive to load when the database is empty, in preference to re-ingesting from the league.
    /// Relative paths resolve against the application directory. Leave unset to use the
    /// convention <c>seed/{SeedSeasonId}.blueline.gz</c>; set it to an empty string to ignore any
    /// archive and always ingest.
    /// </summary>
    public string? SeedArchivePath { get; set; }
}
